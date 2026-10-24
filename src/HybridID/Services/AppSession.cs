using System;
using System.DirectoryServices.Protocols;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Azure.Identity;
using HybridID.Models;
using Microsoft.Graph;

namespace HybridID.Services;

public enum AdConnectionState
{
    /// <summary>No domain controller configured (Settings.LdapServer blank).</summary>
    NotConfigured,
    /// <summary>Configured, no bound connection yet. The next search prompts for credentials.</summary>
    NotConnected,
    Connecting,
    Connected,
    /// <summary>User chose "Skip": no prompts until they sign in again or connection settings change.</summary>
    Skipped,
    /// <summary>The last connect attempt failed (<see cref="AppSession.AdLastError"/>).</summary>
    Failed,
}

public enum GraphConnectionState
{
    /// <summary>No Graph call has succeeded in this session yet (a cached token may still exist).</summary>
    Unverified,
    /// <summary>A device-code prompt is pending (<see cref="AppSession.PendingDeviceCode"/>).</summary>
    WaitingForSignIn,
    SignedIn,
    Failed,
}

/// <summary>
/// Session-wide orchestration state shared by the Search and Settings pages: config, services,
/// the long-lived LDAP connection, the Graph client and connection state. Moved out of the legacy
/// MainWindow/SettingsWindow code-behind (docs/design/ui-spec.md section 2.4). No UI types; all
/// members are meant to be called on the UI thread, and <see cref="StateChanged"/> is always
/// raised on the UI thread.
/// </summary>
public sealed class AppSession : IDisposable
{
    public const string DefaultExchangeFqdn = "mail.domain.tld";

    private GraphServiceClient? _graphClient;
    private Func<DeviceCodeInfo, CancellationToken, Task>? _deviceCodeCallback;
    private int _ldapAttempt;

    public AppSession() : this(new ConfigService())
    {
    }

    public AppSession(ConfigService configService)
    {
        ConfigService = configService;
        Config = configService.Load();
        Ldap = new LdapIdentityService();
        GraphAuth = new GraphAuthService(Config);
        Lookup = new IdentityLookupService(Ldap, Config);
        AdState = IsAdConfigured ? AdConnectionState.NotConnected : AdConnectionState.NotConfigured;
    }

    public ConfigService ConfigService { get; }

    /// <summary>The saved config. Never mutate before saving (IdentityLookupService shares this instance).</summary>
    public AppConfig Config { get; private set; }

    public LdapIdentityService Ldap { get; }
    public IdentityLookupService Lookup { get; private set; }
    public GraphAuthService GraphAuth { get; private set; }

    public LdapConnection? LdapConnection { get; private set; }
    public string? LdapBaseDn { get; private set; }

    /// <summary>Username the current LDAP connection is bound as (for the AD chip flyout).</summary>
    public string? LdapBoundUser { get; private set; }

    public AdConnectionState AdState { get; private set; }
    public string? AdLastError { get; private set; }

    public GraphConnectionState GraphState { get; private set; }
    public string? GraphLastError { get; private set; }

    /// <summary>Non-null while <see cref="GraphState"/> is WaitingForSignIn.</summary>
    public DeviceCodeInfo? PendingDeviceCode { get; private set; }

    /// <summary>Raised on the UI thread whenever any AD/Graph state or the config changes.</summary>
    public event EventHandler? StateChanged;

    public bool IsAdConfigured => !string.IsNullOrWhiteSpace(Config.Settings.LdapServer);

    public bool IsExchangeFqdnDefault =>
        string.IsNullOrWhiteSpace(Config.Settings.OnPremExchangeFqdn) ||
        string.Equals(Config.Settings.OnPremExchangeFqdn.Trim(), DefaultExchangeFqdn, StringComparison.OrdinalIgnoreCase);

    /// <summary>"LDAPS", "StartTLS" or "None" for the saved settings.</summary>
    public string AdEncryptionLabel => EncryptionLabel(Config.Settings);

    /// <summary>"dc01.contoso.com · LDAPS · port 636" (AdCredentialDialog subtitle, chip flyout).</summary>
    public string AdServerLabel => ServerLabel(Config.Settings);

    public static string EncryptionLabel(AppSettings s) =>
        s.LdapUseStartTls ? "StartTLS" : s.LdapUseLdaps ? "LDAPS" : "None";

    public static string ServerLabel(AppSettings s) =>
        $"{s.LdapServer.Trim()} · {EncryptionLabel(s)} · port {s.LdapPort}";

    // ---------------------------------------------------------------- Active Directory

    /// <summary>
    /// Binds a new long-lived LDAP connection with the saved settings (legacy
    /// EnsureLdapConnectionAsync, minus the dialog). Returns null on success, else the error
    /// message. Pass this method as AdCredentialDialog's <c>tryConnect</c> delegate.
    /// </summary>
    public async Task<string?> ConnectLdapAsync(string username, string password)
    {
        if (!IsAdConfigured)
        {
            return "Active Directory isn't configured. Add a domain controller in Settings.";
        }

        var attempt = ++_ldapAttempt;
        DisposeLdap();
        AdState = AdConnectionState.Connecting;
        AdLastError = null;
        RaiseStateChanged();

        var s = Config.Settings;
        try
        {
            // System.DirectoryServices.Protocols has no async API; keep the TLS handshake and
            // bind off the UI thread so the window doesn't freeze against a slow/unreachable DC.
            var connection = await Task.Run(() => Ldap.Connect(
                s.LdapServer.Trim(), s.LdapPort, s.LdapUseLdaps, s.LdapUseStartTls,
                s.LdapSkipCertificateCheck, username, password));

            string baseDn;
            try
            {
                baseDn = string.IsNullOrWhiteSpace(s.LdapBaseDn)
                    ? await Task.Run(() => Ldap.DiscoverDefaultNamingContext(connection))
                    : s.LdapBaseDn.Trim();
            }
            catch
            {
                connection.Dispose();
                throw;
            }

            if (attempt != _ldapAttempt)
            {
                // Superseded (settings saved / disconnected while binding).
                connection.Dispose();
                return "The connection attempt was cancelled.";
            }

            LdapConnection = connection;
            LdapBaseDn = baseDn;
            LdapBoundUser = username;
            AdState = AdConnectionState.Connected;
            RaiseStateChanged();
            return null;
        }
        catch (Exception ex)
        {
            if (attempt == _ldapAttempt)
            {
                AdState = AdConnectionState.Failed;
                AdLastError = ex.Message;
                RaiseStateChanged();
            }

            return ex.Message;
        }
    }

    /// <summary>AdState = Skipped: searches stop prompting until the user signs in or settings change.</summary>
    public void SkipLdapForSession()
    {
        if (!IsAdConfigured || AdState == AdConnectionState.Connected)
        {
            return;
        }

        AdState = AdConnectionState.Skipped;
        RaiseStateChanged();
    }

    /// <summary>Disposes the connection; AdState = NotConnected (or NotConfigured). Also clears a skip.</summary>
    public void DisconnectLdap()
    {
        _ldapAttempt++;
        DisposeLdap();
        AdLastError = null;
        AdState = IsAdConfigured ? AdConnectionState.NotConnected : AdConnectionState.NotConfigured;
        RaiseStateChanged();
    }

    /// <summary>
    /// Throwaway bind against the Settings form's (possibly unsaved) values (legacy
    /// TestLdapConnectionAsync). The connection is disposed immediately.
    /// Ok: Message = "Connected. Base DN: {dn}". Failure: Message = the raw error text.
    /// </summary>
    public async Task<(bool Ok, string Message)> TestLdapAsync(AppSettings form, string username, string password)
    {
        LdapConnection? connection = null;
        try
        {
            connection = await Task.Run(() => Ldap.Connect(
                form.LdapServer.Trim(), form.LdapPort, form.LdapUseLdaps, form.LdapUseStartTls,
                form.LdapSkipCertificateCheck, username, password));

            var baseDn = string.IsNullOrWhiteSpace(form.LdapBaseDn)
                ? await Task.Run(() => Ldap.DiscoverDefaultNamingContext(connection))
                : form.LdapBaseDn.Trim();

            return (true, $"Connected. Base DN: {baseDn}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
        finally
        {
            connection?.Dispose();
        }
    }

    // ---------------------------------------------------------------- Microsoft Graph

    /// <summary>
    /// Returns the session Graph client, creating it on first use. <paramref name="deviceCodeCallback"/>
    /// becomes the current device-code handler (the most recent caller wins). It is invoked on a
    /// background thread: marshal with Dispatcher.UIThread.Post before touching UI. The session
    /// itself sets GraphState = WaitingForSignIn / PendingDeviceCode before invoking it.
    /// Throws if the credential can't be created (bad tenant/client ID); GraphState becomes Failed.
    /// </summary>
    public GraphServiceClient EnsureGraphClient(Func<DeviceCodeInfo, CancellationToken, Task> deviceCodeCallback)
    {
        _deviceCodeCallback = deviceCodeCallback;
        if (_graphClient is not null)
        {
            return _graphClient;
        }

        try
        {
            _graphClient = GraphAuth.GetClient(OnDeviceCode);
            return _graphClient;
        }
        catch (Exception ex)
        {
            MarkGraphFailed(ex.Message);
            throw;
        }
    }

    /// <summary>Convenience wrapper around <see cref="IdentityLookupService.LookupAsync"/> with the session's LDAP state.</summary>
    public Task<IdentityLookupResult> LookupAsync(GraphServiceClient graphClient, string identity) =>
        Lookup.LookupAsync(LdapConnection, LdapBaseDn ?? "", new GraphIdentityService(graphClient), identity);

    public void MarkGraphSignedIn()
    {
        PendingDeviceCode = null;
        GraphLastError = null;
        GraphState = GraphConnectionState.SignedIn;
        RaiseStateChanged();
    }

    public void MarkGraphWaiting(DeviceCodeInfo info)
    {
        PendingDeviceCode = info;
        GraphState = GraphConnectionState.WaitingForSignIn;
        RaiseStateChanged();
    }

    public void MarkGraphFailed(string message)
    {
        PendingDeviceCode = null;
        GraphLastError = message;
        GraphState = GraphConnectionState.Failed;
        RaiseStateChanged();
    }

    /// <summary>
    /// Ends a pending device-code wait without claiming success (search finished without Graph
    /// data, code expired): WaitingForSignIn → Unverified. No-op in other states.
    /// </summary>
    public void ClearGraphWaiting()
    {
        if (GraphState != GraphConnectionState.WaitingForSignIn)
        {
            return;
        }

        PendingDeviceCode = null;
        GraphState = GraphConnectionState.Unverified;
        RaiseStateChanged();
    }

    /// <summary>
    /// Throwaway Graph client from the Settings form's tenant/client ID (legacy
    /// TestGraphConnectionAsync): one users?$top=1 call. Ok: Message = "Signed in. Microsoft Graph
    /// responded." (and the session is marked SignedIn when tenant/client match the saved config).
    /// Failure: Message = the raw error text. <paramref name="deviceCodeCallback"/> runs on a background thread.
    /// </summary>
    public async Task<(bool Ok, string Message)> TestGraphAsync(
        string tenantId, string clientId, Func<DeviceCodeInfo, CancellationToken, Task> deviceCodeCallback)
    {
        tenantId = string.IsNullOrWhiteSpace(tenantId) ? "organizations" : tenantId.Trim();
        clientId = string.IsNullOrWhiteSpace(clientId) ? new AppSettings().GraphClientId : clientId.Trim();

        try
        {
            var testConfig = new AppConfig
            {
                Settings = new AppSettings { GraphTenantId = tenantId, GraphClientId = clientId },
            };
            var client = new GraphAuthService(testConfig).GetClient(deviceCodeCallback);
            await client.Users.GetAsync(rq =>
            {
                rq.QueryParameters.Top = 1;
                rq.QueryParameters.Select = new[] { "id" };
            });

            if (string.Equals(tenantId, Config.Settings.GraphTenantId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(clientId, Config.Settings.GraphClientId, StringComparison.OrdinalIgnoreCase))
            {
                MarkGraphSignedIn();
            }

            return (true, "Signed in. Microsoft Graph responded.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ---------------------------------------------------------------- Config

    /// <summary>
    /// Call after writing the Settings form into <see cref="Config"/> and saving it (legacy
    /// OnSettingsSaved). When connection settings changed: disposes LDAP, drops the Graph client
    /// and rebuilds the services, so the next search signs in again.
    /// </summary>
    public void ApplySavedConfig(bool connectionSettingsChanged)
    {
        if (connectionSettingsChanged)
        {
            _ldapAttempt++;
            DisposeLdap();
            AdLastError = null;
            _graphClient = null;
            PendingDeviceCode = null;
            GraphLastError = null;
            GraphState = GraphConnectionState.Unverified;
            GraphAuth = new GraphAuthService(Config);
            Lookup = new IdentityLookupService(Ldap, Config);
            AdState = IsAdConfigured ? AdConnectionState.NotConnected : AdConnectionState.NotConfigured;
        }
        else if (!IsAdConfigured)
        {
            AdState = AdConnectionState.NotConfigured;
        }
        else if (AdState == AdConnectionState.NotConfigured)
        {
            AdState = AdConnectionState.NotConnected;
        }

        RaiseStateChanged();
    }

    /// <summary>Applies the theme live and saves only the Theme field of the saved config.</summary>
    public void SaveTheme(string theme)
    {
        Config.Settings.Theme = theme;
        ThemeService.Apply(theme);
        ConfigService.Save(Config);
    }

    public void Dispose() => DisposeLdap();

    private void DisposeLdap()
    {
        LdapConnection?.Dispose();
        LdapConnection = null;
        LdapBaseDn = null;
        LdapBoundUser = null;
    }

    private Task OnDeviceCode(DeviceCodeInfo info, CancellationToken cancellationToken)
    {
        // Called by Azure.Identity on a background thread.
        Dispatcher.UIThread.Post(() => MarkGraphWaiting(info));
        return _deviceCodeCallback?.Invoke(info, cancellationToken) ?? Task.CompletedTask;
    }

    private void RaiseStateChanged()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Dispatcher.UIThread.Post(() => StateChanged?.Invoke(this, EventArgs.Empty));
        }
    }
}
