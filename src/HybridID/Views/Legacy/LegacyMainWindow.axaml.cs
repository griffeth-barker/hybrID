using System;
using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using Azure.Identity;
using HybridID.Models;
using HybridID.Services;

namespace HybridID.Views.Legacy;

public partial class LegacyMainWindow : Window
{
    private readonly ConfigService _configService = new();
    private AppConfig _config = null!;

    private readonly LdapIdentityService _ldapService = new();
    private GraphAuthService _graphAuthService = null!;
    private IdentityLookupService _lookupService = null!;

    private LdapConnection? _ldapConnection;
    private string? _ldapBaseDn;
    private Microsoft.Graph.GraphServiceClient? _graphClient;
    private LegacyDeviceCodeDialog? _deviceCodeDialog;
    private bool _isSearching;

    public LegacyMainWindow()
    {
        InitializeComponent();

        _config = _configService.Load();
        RebuildServices();
        ThemeService.Apply(_config.Settings.Theme);

        TxtManageIdentity.PointerPressed += OnManagementLinkClicked;
        TxtManageExchange.PointerPressed += OnManagementLinkClicked;

        BtnCopyName.Click += async (_, _) => await CopyToClipboardAsync(ValName.Text);
        BtnCopyManageIdentity.Click += async (_, _) => await CopyToClipboardAsync(TxtManageIdentity.Tag as string ?? TxtManageIdentity.Text);
        BtnCopyProxyAddresses.Click += async (_, _) => await CopyToClipboardAsync(ValProxyAddresses.Text);
        BtnCopyManageExchange.Click += async (_, _) => await CopyToClipboardAsync(TxtManageExchange.Tag as string ?? TxtManageExchange.Text);

        BtnSearch.Click += async (_, _) => await RunSearchAsync();
        TxtSearch.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                await RunSearchAsync();
            }
        };

        KeyDown += OnWindowKeyDown;

        BtnSettings.Click += (_, _) =>
        {
            var settings = new LegacySettingsWindow(_config, _configService, OnSettingsSaved);
            settings.ShowDialog(this);
        };

        BtnAbout.Click += (_, _) => new LegacyAboutWindow().ShowDialog(this);

        Opened += (_, _) =>
        {
            SetStatusBar(IdleMessage(), "StatusReadyBrush");
            TxtSearch.Focus();
        };
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        var isFocusSearchGesture = e.Key == Key.L &&
            (e.KeyModifiers == KeyModifiers.Control || e.KeyModifiers == KeyModifiers.Meta);

        if (isFocusSearchGesture)
        {
            TxtSearch.Focus();
            TxtSearch.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            TxtSearch.Text = "";
            ClearFields();
            SetStatusBar(IdleMessage(), "StatusReadyBrush");
            TxtSearch.Focus();
            e.Handled = true;
        }
    }

    private void RebuildServices()
    {
        _graphAuthService = new GraphAuthService(_config);
        _lookupService = new IdentityLookupService(_ldapService, _config);
    }

    private void OnSettingsSaved()
    {
        _ldapConnection?.Dispose();
        _ldapConnection = null;
        _ldapBaseDn = null;
        _graphClient = null;
        RebuildServices();
        SetStatusBar(IdleMessage(), "StatusReadyBrush");
    }

    private string IdleMessage() => string.IsNullOrWhiteSpace(_config.Settings.LdapServer)
        ? "Ready (configure Active Directory in Settings to enable on-prem lookups)"
        : "Ready";

    private async Task RunSearchAsync(string? identityOverride = null)
    {
        if (_isSearching)
        {
            return;
        }

        var identity = identityOverride ?? TxtSearch.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(identity))
        {
            SetStatus("Enter an identity to search.", StatusLevel.Error);
            return;
        }

        _isSearching = true;
        BtnSearch.IsEnabled = false;
        try
        {
            ClearFields();
            SetStatus($"Searching Active Directory and Graph API for '{identity}'...", StatusLevel.Info);

            var ldapError = await EnsureLdapConnectionAsync();

            Microsoft.Graph.GraphServiceClient graphClient;
            try
            {
                graphClient = EnsureGraphClient();
            }
            catch (Exception ex)
            {
                // A bad GraphClientId/GraphTenantId (only editable by hand-editing config.json)
                // would otherwise crash the app here instead of surfacing as a search error.
                SetStatus($"Could not reach Microsoft Graph: {ex.Message}", StatusLevel.Error);
                return;
            }
            var graphIdentityService = new GraphIdentityService(graphClient);

            try
            {
                var result = await _lookupService.LookupAsync(_ldapConnection, _ldapBaseDn ?? "", graphIdentityService, identity);
                ApplyResult(result, ldapError);
            }
            finally
            {
                CloseDeviceCodeDialog();
            }
        }
        finally
        {
            _isSearching = false;
            BtnSearch.IsEnabled = true;
        }
    }

    /// <summary>Returns a human-readable connection error, or null if AD isn't configured or the connection succeeded.</summary>
    private async Task<string?> EnsureLdapConnectionAsync()
    {
        if (_ldapConnection is not null || string.IsNullOrWhiteSpace(_config.Settings.LdapServer))
        {
            return null;
        }

        var dialog = new LegacyAdCredentialDialog();
        await dialog.ShowDialog(this);
        if (dialog.Result is not { } credentials || string.IsNullOrEmpty(credentials.Username))
        {
            return null;
        }

        try
        {
            // System.DirectoryServices.Protocols has no async API; keep the TLS handshake and
            // bind off the UI thread so the window doesn't freeze against a slow/unreachable DC.
            var connection = await Task.Run(() => _ldapService.Connect(
                _config.Settings.LdapServer,
                _config.Settings.LdapPort,
                _config.Settings.LdapUseLdaps,
                _config.Settings.LdapUseStartTls,
                _config.Settings.LdapSkipCertificateCheck,
                credentials.Username,
                credentials.Password));

            _ldapBaseDn = string.IsNullOrWhiteSpace(_config.Settings.LdapBaseDn)
                ? await Task.Run(() => _ldapService.DiscoverDefaultNamingContext(connection))
                : _config.Settings.LdapBaseDn;

            _ldapConnection = connection;
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private Microsoft.Graph.GraphServiceClient EnsureGraphClient() =>
        _graphClient ??= _graphAuthService.GetClient(OnDeviceCodeReady);

    private Task OnDeviceCodeReady(DeviceCodeInfo info, CancellationToken cancellationToken)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _deviceCodeDialog = new LegacyDeviceCodeDialog(info.Message);
            _deviceCodeDialog.Show(this);
        });
        return Task.CompletedTask;
    }

    private void CloseDeviceCodeDialog()
    {
        // Always called from RunSearchAsync's finally block, which resumes on the UI thread
        // (Avalonia's dispatcher marshals async continuations back automatically) - no need to
        // hop threads again to close it.
        _deviceCodeDialog?.Close();
        _deviceCodeDialog = null;
    }

    private void ClearFields()
    {
        AmbiguousPanel.IsVisible = false;
        DetailPanel.IsVisible = true;
        CandidateListPanel.Children.Clear();

        SyncBadge.IsVisible = false;
        AccountBadge.IsVisible = false;

        ValName.Text = "";
        ValClass.Text = "";
        ValPassword.Text = "";
        ValNotes.Text = "";
        ValProxyAddresses.Text = "";
        SetManagementLink(TxtManageIdentity, null);
        SetManagementLink(TxtManageExchange, null);

        SyncSection.IsVisible = false;
        SyncSectionDivider.IsVisible = false;
        ValLastSynced.Text = "";

        SetDetailsRowVisible(LblGroupType, ValGroupType, false);
        SetDetailsRowVisible(LblMemberCount, ValMemberCount, false);
        SetDetailsRowVisible(LblLicenses, ValLicenses, false);
    }

    private void ApplyResult(IdentityLookupResult result, string? ldapError)
    {
        // An AD connection failure would otherwise be silently overwritten by the final status
        // below (e.g. "found in Entra ID" or "not found anywhere") - surface both.
        var message = ldapError is null
            ? result.StatusMessage
            : $"{result.StatusMessage} (Active Directory unreachable: {ldapError})";
        SetStatus(message, result.StatusLevel);

        if (result.Candidates is { Count: > 0 } candidates)
        {
            ShowAmbiguousCandidates(candidates);
            return;
        }

        AmbiguousPanel.IsVisible = false;
        DetailPanel.IsVisible = true;

        ValName.Text = result.Name ?? "";
        ValClass.Text = result.ObjectClass ?? "";
        ValPassword.Text = FormatPassword(result);
        ValNotes.Text = result.Notes;
        ValProxyAddresses.Text = string.Join(Environment.NewLine, result.ProxyAddresses);
        SetManagementLink(TxtManageIdentity, result.ManageIdentity);
        SetManagementLink(TxtManageExchange, result.ManageExchange);

        ApplySyncBadge(result.SyncState);
        ApplyAccountBadge(result.AccountEnabled, result.IsLockedOut);

        var showSync = result.SyncState == Models.SyncState.Hybrid;
        SyncSection.IsVisible = showSync;
        SyncSectionDivider.IsVisible = showSync;
        ValLastSynced.Text = result.LastSyncedUtc is { } t
            ? t.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            : "Unknown";

        var isGroup = string.Equals(result.ObjectClass, "group", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(result.ObjectClass, "Cloud Group", StringComparison.OrdinalIgnoreCase);

        SetDetailsRowVisible(LblGroupType, ValGroupType, isGroup && result.GroupTypeDescription is not null, result.GroupTypeDescription);
        SetDetailsRowVisible(LblMemberCount, ValMemberCount, isGroup && result.MemberCount is not null, result.MemberCount?.ToString(CultureInfo.CurrentCulture));
        SetDetailsRowVisible(LblLicenses, ValLicenses, !isGroup && result.Licenses.Count > 0, string.Join(Environment.NewLine, result.Licenses));
    }

    private void ShowAmbiguousCandidates(System.Collections.Generic.IReadOnlyList<AmbiguousCandidate> candidates)
    {
        DetailPanel.IsVisible = false;
        AmbiguousPanel.IsVisible = true;
        CandidateListPanel.Children.Clear();

        foreach (var candidate in candidates)
        {
            var card = new Border
            {
                Background = (IBrush?)this.FindResource("WindowBg"),
                BorderBrush = (IBrush?)this.FindResource("AppBorderBrush"),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(6),
                Padding = new Avalonia.Thickness(12, 8),
                Cursor = new Cursor(StandardCursorType.Hand),
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = candidate.DisplayName,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Foreground = (IBrush?)this.FindResource("PrimaryText"),
            });
            stack.Children.Add(new TextBlock
            {
                Text = candidate.Detail,
                FontSize = 12,
                Foreground = (IBrush?)this.FindResource("SecondaryText"),
            });
            card.Child = stack;

            card.PointerPressed += async (_, _) =>
            {
                TxtSearch.Text = candidate.Identity;
                await RunSearchAsync(candidate.Identity);
            };

            CandidateListPanel.Children.Add(card);
        }
    }

    private void ApplySyncBadge(Models.SyncState? syncState)
    {
        if (syncState is null)
        {
            SyncBadge.IsVisible = false;
            return;
        }

        var (text, brushKey) = syncState switch
        {
            Models.SyncState.OnPremOnly => ("On-Premises Only", "SyncOnPremBrush"),
            Models.SyncState.Hybrid => ("Hybrid / Synced", "SyncHybridBrush"),
            Models.SyncState.CloudOnly => ("Cloud-Only", "SyncCloudOnlyBrush"),
            _ => ("", "SyncOnPremBrush"),
        };

        SyncBadgeText.Text = text;
        SyncBadge.Background = (IBrush?)this.FindResource(brushKey);
        SyncBadge.IsVisible = true;
    }

    private void ApplyAccountBadge(bool? accountEnabled, bool? isLockedOut)
    {
        string text;
        string brushKey;

        if (isLockedOut == true)
        {
            text = "Locked Out";
            brushKey = "AccountLockedBrush";
        }
        else if (accountEnabled == false)
        {
            text = "Disabled";
            brushKey = "AccountDisabledBrush";
        }
        else if (accountEnabled == true)
        {
            text = "Enabled";
            brushKey = "AccountEnabledBrush";
        }
        else
        {
            AccountBadge.IsVisible = false;
            return;
        }

        AccountBadgeText.Text = text;
        AccountBadge.Background = (IBrush?)this.FindResource(brushKey);
        AccountBadge.IsVisible = true;
    }

    private static string FormatPassword(IdentityLookupResult result)
    {
        if (result.PasswordNeverExpires == true)
        {
            return "Never expires";
        }

        if (result.PasswordLastSetUtc is { } utc)
        {
            var local = utc.ToLocalTime();
            var age = DateTime.UtcNow - utc;
            return $"Set {local:d} ({age.Days} day{(age.Days == 1 ? "" : "s")} ago)";
        }

        return string.IsNullOrEmpty(result.ObjectClass) ? "" : "Unknown";
    }

    private void SetDetailsRowVisible(TextBlock label, TextBox value, bool visible, string? text = null)
    {
        label.IsVisible = visible;
        value.IsVisible = visible;
        if (visible)
        {
            value.Text = text ?? "";
        }
    }

    private void SetStatus(string message, StatusLevel level)
    {
        var resourceKey = level switch
        {
            StatusLevel.Success => "StatusSuccessBrush",
            StatusLevel.Error => "StatusErrorBrush",
            _ => "StatusInfoBrush",
        };
        SetStatusBar(message, resourceKey);
    }

    private void SetStatusBar(string message, string brushResourceKey)
    {
        TxtStatus.Text = message;
        StatusBarBorder.Background = (IBrush?)this.FindResource(brushResourceKey);
    }

    private static void SetManagementLink(TextBlock textBlock, ManagementLink? link)
    {
        textBlock.Classes.Remove("hyperlink");
        textBlock.Cursor = Cursor.Default;
        textBlock.Tag = null;

        if (link is null)
        {
            textBlock.Text = "";
            return;
        }

        textBlock.Text = link.Text;
        if (link.IsLink)
        {
            textBlock.Classes.Add("hyperlink");
            textBlock.Cursor = new Cursor(StandardCursorType.Hand);
            textBlock.Tag = link.Url;
        }
    }

    private static void OnManagementLinkClicked(object? sender, PointerPressedEventArgs e)
    {
        if (sender is TextBlock { Tag: string url })
        {
            UrlLauncher.Open(url);
        }
    }

    private async Task CopyToClipboardAsync(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var clipboard = GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetValueAsync(DataFormat.Text, text);
        }
    }
}
