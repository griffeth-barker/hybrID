using System;
using System.DirectoryServices.Protocols;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Azure.Identity;
using HybridID.Models;
using HybridID.Services;

namespace HybridID.Views.Legacy;

public partial class LegacySettingsWindow : Window
{
    private readonly AppConfig _config = null!;
    private readonly ConfigService _configService = null!;
    private readonly Action _onSaved = null!;

    private readonly LdapIdentityService _ldapService = new();

    private bool _isTestingLdap;
    private bool _isTestingGraph;
    private LegacyDeviceCodeDialog? _testDeviceCodeDialog;

    public LegacySettingsWindow()
    {
        InitializeComponent();
    }

    public LegacySettingsWindow(AppConfig config, ConfigService configService, Action onSaved) : this()
    {
        _config = config;
        _configService = configService;
        _onSaved = onSaved;

        CfgFqdn.Text = _config.Settings.OnPremExchangeFqdn;
        CfgLdapServer.Text = _config.Settings.LdapServer;
        CfgLdapPort.Text = _config.Settings.LdapPort.ToString();
        CfgLdapBaseDn.Text = _config.Settings.LdapBaseDn;
        ChkLdapSkipCert.IsChecked = _config.Settings.LdapSkipCertificateCheck;
        CfgGraphTenant.Text = _config.Settings.GraphTenantId;

        CmbLdapEncryption.SelectedIndex = EncryptionIndexFor(_config.Settings);
        CmbTheme.SelectedIndex = ThemeIndexFor(_config.Settings.Theme);

        BtnCancel.Click += (_, _) => Close();
        BtnApply.Click += async (_, _) => await ApplyAsync();

        BtnTestLdap.Click += async (_, _) => await TestLdapConnectionAsync();
        BtnTestGraph.Click += async (_, _) => await TestGraphConnectionAsync();
    }

    private async Task ApplyAsync()
    {
        _config.Settings.OnPremExchangeFqdn = CfgFqdn.Text?.Trim() ?? "";
        _config.Settings.LdapServer = CfgLdapServer.Text?.Trim() ?? "";
        // Keep the previously saved port on invalid/blank input rather than silently
        // substituting an unrelated hardcoded default that may not match the chosen encryption.
        _config.Settings.LdapPort = int.TryParse(CfgLdapPort.Text, out var port) ? port : _config.Settings.LdapPort;
        _config.Settings.LdapBaseDn = CfgLdapBaseDn.Text?.Trim() ?? "";
        _config.Settings.LdapSkipCertificateCheck = ChkLdapSkipCert.IsChecked ?? false;
        _config.Settings.GraphTenantId = string.IsNullOrWhiteSpace(CfgGraphTenant.Text) ? "organizations" : CfgGraphTenant.Text.Trim();

        ApplyEncryptionIndex(_config.Settings, CmbLdapEncryption.SelectedIndex);
        _config.Settings.Theme = ThemeForIndex(CmbTheme.SelectedIndex);

        _configService.Save(_config);
        ThemeService.Apply(_config.Settings.Theme);
        _onSaved();

        var alert = new LegacyAlertDialog("Settings Saved", "Configuration saved successfully.");
        await alert.ShowDialog(this);
        Close();
    }

    /// <summary>
    /// Binds against the form's current (not necessarily saved) Active Directory values, proving
    /// the connection actually works before the user hits Apply. The connection is disposed
    /// immediately afterward - LegacyMainWindow owns the "real" long-lived connection used for lookups.
    /// </summary>
    private async Task TestLdapConnectionAsync()
    {
        if (_isTestingLdap)
        {
            return;
        }

        var dialog = new LegacyAdCredentialDialog();
        await dialog.ShowDialog(this);
        if (dialog.Result is not { } credentials || string.IsNullOrEmpty(credentials.Username))
        {
            return;
        }

        _isTestingLdap = true;
        BtnTestLdap.IsEnabled = false;
        SetTestStatus(TxtLdapTestStatus, "Testing...", null);
        try
        {
            var server = CfgLdapServer.Text?.Trim() ?? "";
            var port = int.TryParse(CfgLdapPort.Text, out var p) ? p : _config.Settings.LdapPort;
            var useLdaps = CmbLdapEncryption.SelectedIndex == 0;
            var useStartTls = CmbLdapEncryption.SelectedIndex == 1;
            var skipCert = ChkLdapSkipCert.IsChecked ?? false;
            var baseDnOverride = CfgLdapBaseDn.Text?.Trim() ?? "";

            LdapConnection? connection = null;
            try
            {
                // System.DirectoryServices.Protocols has no async API; keep the TLS handshake and
                // bind off the UI thread so the window doesn't freeze against a slow/unreachable DC.
                connection = await Task.Run(() => _ldapService.Connect(
                    server, port, useLdaps, useStartTls, skipCert,
                    credentials.Username, credentials.Password));

                var baseDn = string.IsNullOrWhiteSpace(baseDnOverride)
                    ? await Task.Run(() => _ldapService.DiscoverDefaultNamingContext(connection))
                    : baseDnOverride;

                SetTestStatus(TxtLdapTestStatus, $"Connected successfully. Base DN: {baseDn}", true);
            }
            finally
            {
                // Just a test - don't leak the bound connection or leave it around.
                connection?.Dispose();
            }
        }
        catch (Exception ex)
        {
            SetTestStatus(TxtLdapTestStatus, $"Connection failed: {ex.Message}", false);
        }
        finally
        {
            _isTestingLdap = false;
            BtnTestLdap.IsEnabled = true;
        }
    }

    /// <summary>
    /// Builds a throwaway GraphAuthService from the form's current Tenant ID (not necessarily
    /// saved) and makes one cheap authenticated call to prove device-code sign-in and the tenant
    /// ID actually work together.
    /// </summary>
    private async Task TestGraphConnectionAsync()
    {
        if (_isTestingGraph)
        {
            return;
        }

        _isTestingGraph = true;
        BtnTestGraph.IsEnabled = false;
        SetTestStatus(TxtGraphTestStatus, "Testing...", null);
        try
        {
            var tenantId = string.IsNullOrWhiteSpace(CfgGraphTenant.Text) ? "organizations" : CfgGraphTenant.Text.Trim();
            var testConfig = new AppConfig
            {
                Settings = new AppSettings
                {
                    GraphTenantId = tenantId,
                    GraphClientId = _config.Settings.GraphClientId,
                },
            };
            var authService = new GraphAuthService(testConfig);

            try
            {
                var client = authService.GetClient(OnTestDeviceCodeReady);
                await client.Users.GetAsync(rq =>
                {
                    rq.QueryParameters.Top = 1;
                    rq.QueryParameters.Select = new[] { "id" };
                });

                SetTestStatus(TxtGraphTestStatus, "Connected successfully.", true);
            }
            finally
            {
                CloseTestDeviceCodeDialog();
            }
        }
        catch (Exception ex)
        {
            SetTestStatus(TxtGraphTestStatus, $"Connection failed: {ex.Message}", false);
        }
        finally
        {
            _isTestingGraph = false;
            BtnTestGraph.IsEnabled = true;
        }
    }

    private Task OnTestDeviceCodeReady(DeviceCodeInfo info, CancellationToken cancellationToken)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _testDeviceCodeDialog = new LegacyDeviceCodeDialog(info.Message);
            _testDeviceCodeDialog.Show(this);
        });
        return Task.CompletedTask;
    }

    private void CloseTestDeviceCodeDialog()
    {
        // Always called from TestGraphConnectionAsync's finally block, which resumes on the UI
        // thread (Avalonia's dispatcher marshals async continuations back automatically) - no
        // need to hop threads again to close it.
        _testDeviceCodeDialog?.Close();
        _testDeviceCodeDialog = null;
    }

    private void SetTestStatus(TextBlock textBlock, string message, bool? success)
    {
        textBlock.Text = message;
        var resourceKey = success switch
        {
            true => "StatusSuccessBrush",
            false => "StatusErrorBrush",
            null => "SecondaryText",
        };
        textBlock.Foreground = (IBrush?)this.FindResource(resourceKey);
    }

    // Single source of truth for the Encryption combo's item order (LDAPS, StartTLS, None) so
    // load and save can't silently drift apart if the XAML item order ever changes.
    private static int EncryptionIndexFor(AppSettings s) =>
        s.LdapUseStartTls ? 1 : s.LdapUseLdaps ? 0 : 2;

    private static void ApplyEncryptionIndex(AppSettings s, int index)
    {
        s.LdapUseLdaps = index == 0;
        s.LdapUseStartTls = index == 1;
    }

    private static int ThemeIndexFor(string theme) => theme switch
    {
        "Light" => 1,
        "Dark" => 2,
        _ => 0,
    };

    private static string ThemeForIndex(int index) => index switch
    {
        1 => "Light",
        2 => "Dark",
        _ => "System",
    };
}
