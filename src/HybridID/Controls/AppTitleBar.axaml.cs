using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using HybridID.Services;

namespace HybridID.Controls;

/// <summary>
/// Custom title bar: pane toggle, app icon/title (Windows), AD and Entra status chips with
/// flyouts, caption-button reserve (docs/design/ui-spec.md sections 4.2, 6.9). The whole bar has
/// <c>ElementRole=TitleBar</c> (drag, double-click maximize) on Windows/macOS; interactive
/// children get <c>ElementRole=User</c>. On Linux it is a plain 40 px in-window app bar.
/// </summary>
public partial class AppTitleBar : UserControl
{
    /// <summary>Caption buttons on Windows (3 x 46). Unverified against Avalonia 12's drawn caption buttons (spec R1).</summary>
    public const double WindowsCaptionReserve = 138;
    public const double MacTrafficLightInset = 80;
    private const double ShortLabelThreshold = 900;

    private AppSession? _session;
    private bool _isPaneOpen = true;
    private bool _shortLabels;

    public AppTitleBar()
    {
        InitializeComponent();
        ConfigureForPlatform();

        PaneToggle.Click += (_, _) => PaneToggleClick?.Invoke(this, EventArgs.Empty);

        AdDisconnectButton.Click += (_, _) =>
        {
            AdChip.Flyout?.Hide();
            _session?.DisconnectLdap();
        };
        AdSignInButton.Click += (_, _) =>
        {
            AdChip.Flyout?.Hide();
            AdSignInRequested?.Invoke(this, EventArgs.Empty);
        };
        AdSetupButton.Click += (_, _) =>
        {
            AdChip.Flyout?.Hide();
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        };
        AdSettingsLink.Click += (_, _) =>
        {
            AdChip.Flyout?.Hide();
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        };
        EntraShowCodeButton.Click += (_, _) =>
        {
            EntraChip.Flyout?.Hide();
            ShowSignInCodeRequested?.Invoke(this, EventArgs.Empty);
        };
        EntraSettingsLink.Click += (_, _) =>
        {
            EntraChip.Flyout?.Hide();
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        };

        SizeChanged += (_, e) =>
        {
            var shortLabels = e.NewSize.Width < ShortLabelThreshold;
            if (shortLabels != _shortLabels)
            {
                _shortLabels = shortLabels;
                UpdateChips();
            }
        };

        UpdatePaneToggle();
        UpdateChips();
    }

    public event EventHandler? PaneToggleClick;
    public event EventHandler? SettingsRequested;

    /// <summary>AD chip flyout "Sign in": the shell opens AdCredentialDialog (purpose Search) without searching.</summary>
    public event EventHandler? AdSignInRequested;

    /// <summary>Entra chip flyout "Show sign-in code": the shell reopens DeviceCodeDialog with Session.PendingDeviceCode.</summary>
    public event EventHandler? ShowSignInCodeRequested;

    /// <summary>Updates the pane toggle's tooltip/automation name.</summary>
    public bool IsPaneOpen
    {
        get => _isPaneOpen;
        set
        {
            _isPaneOpen = value;
            UpdatePaneToggle();
        }
    }

    /// <summary>False dims the icon/title (window deactivated).</summary>
    public bool IsWindowActive
    {
        set => Classes.Set("inactive", !value);
    }

    /// <summary>macOS only: traffic-light inset (80, or 8 in full screen where the lights hide).</summary>
    public void SetMacFullScreen(bool fullScreen)
    {
        if (OperatingSystem.IsMacOS())
        {
            Root.ColumnDefinitions[0].Width = new GridLength(fullScreen ? 8 : MacTrafficLightInset);
        }
    }

    public void Attach(AppSession session)
    {
        if (_session is not null)
        {
            _session.StateChanged -= OnSessionStateChanged;
        }

        _session = session;
        _session.StateChanged += OnSessionStateChanged;
        UpdateChips();
    }

    private void OnSessionStateChanged(object? sender, EventArgs e) => UpdateChips();

    private void ConfigureForPlatform()
    {
        var cols = Root.ColumnDefinitions;
        if (OperatingSystem.IsWindows())
        {
            Height = 48;
            cols[11].Width = new GridLength(WindowsCaptionReserve);
            SetDecorationRoles(extended: true);
        }
        else if (OperatingSystem.IsMacOS())
        {
            // No icon/title: macOS convention with a sidebar (window title still set for Mission Control).
            Height = 48;
            cols[0].Width = new GridLength(MacTrafficLightInset);
            cols[2].Width = new GridLength(0);
            cols[3].Width = new GridLength(0);
            cols[4].Width = new GridLength(0);
            cols[10].Width = new GridLength(12);
            cols[11].Width = new GridLength(0);
            AppIcon.IsVisible = false;
            TitleText.IsVisible = false;
            SetDecorationRoles(extended: true);
        }
        else
        {
            // Linux: native WM decorations; this is an in-window app bar under the native title bar.
            Height = 40;
            PaneToggle.Height = 32;
            cols[2].Width = new GridLength(0);
            cols[3].Width = new GridLength(0);
            cols[4].Width = new GridLength(0);
            cols[10].Width = new GridLength(12);
            cols[11].Width = new GridLength(0);
            AppIcon.IsVisible = false;
            TitleText.IsVisible = false;
        }
    }

    private void SetDecorationRoles(bool extended)
    {
        if (!extended)
        {
            return;
        }

        WindowDecorationProperties.SetElementRole(this, WindowDecorationsElementRole.TitleBar);
        WindowDecorationProperties.SetElementRole(PaneToggle, WindowDecorationsElementRole.User);
        WindowDecorationProperties.SetElementRole(AdChip, WindowDecorationsElementRole.User);
        WindowDecorationProperties.SetElementRole(EntraChip, WindowDecorationsElementRole.User);
    }

    private void UpdatePaneToggle()
    {
        var text = _isPaneOpen ? "Collapse navigation" : "Expand navigation";
        ToolTip.SetTip(PaneToggle, text);
        AutomationProperties.SetName(PaneToggle, text);
    }

    private void UpdateChips()
    {
        AdChipLabel.Text = _shortLabels ? "AD" : "Active Directory";
        EntraChipLabel.Text = _shortLabels ? "Entra" : "Entra ID";

        var session = _session;
        if (session is null)
        {
            SetDot(AdDot, DotState.Hollow);
            SetDot(EntraDot, DotState.Hollow);
            return;
        }

        // ---- Active Directory
        var s = session.Config.Settings;
        var (adDot, adTip, adStatus) = session.AdState switch
        {
            AdConnectionState.NotConfigured => (DotState.Hollow, "Active Directory isn't configured", "Not configured"),
            AdConnectionState.NotConnected => (DotState.Hollow, "Not signed in. You'll be asked on your next search.", "Not signed in"),
            AdConnectionState.Connecting => (DotState.Pulse, "Connecting…", "Connecting…"),
            AdConnectionState.Connected => (DotState.Success, $"Connected to {s.LdapServer.Trim()}", "Connected"),
            AdConnectionState.Skipped => (DotState.Caution, "Skipped for this session", "Skipped for this session"),
            _ => (DotState.Critical, "Connection failed", "Connection failed"),
        };
        SetDot(AdDot, adDot);
        SetDot(AdFlyoutDot, adDot);
        ToolTip.SetTip(AdChip, adTip);
        AutomationProperties.SetName(AdChip, $"Active Directory: {adTip}");
        AdStatusText.Text = adStatus;

        var configured = session.AdState != AdConnectionState.NotConfigured;
        AdServerRow.IsVisible = configured;
        AdServerText.Text = configured ? session.AdServerLabel : null;
        AdUserRow.IsVisible = session.LdapBoundUser is not null;
        AdUserText.Text = session.LdapBoundUser;
        AdBaseDnRow.IsVisible = session.LdapBaseDn is not null;
        AdBaseDnText.Text = session.LdapBaseDn;
        AdErrorText.IsVisible = session.AdState == AdConnectionState.Failed && !string.IsNullOrEmpty(session.AdLastError);
        AdErrorText.Text = session.AdLastError;

        AdDisconnectButton.IsVisible = session.AdState == AdConnectionState.Connected;
        AdSignInButton.IsVisible = session.AdState is AdConnectionState.NotConnected or AdConnectionState.Skipped or AdConnectionState.Failed;
        AdSetupButton.IsVisible = !configured;
        AdSettingsLink.IsVisible = configured;

        // ---- Entra ID
        var (entraDot, entraTip, entraStatus) = session.GraphState switch
        {
            GraphConnectionState.WaitingForSignIn => (DotState.Pulse, "Waiting for sign-in", "Waiting for sign-in"),
            GraphConnectionState.SignedIn => (DotState.Success, "Signed in", "Signed in"),
            GraphConnectionState.Failed => (DotState.Critical, "Sign-in failed", "Sign-in failed"),
            _ => (DotState.Hollow, "Not verified yet. You'll be asked to sign in on your first search if needed.", "Not verified yet"),
        };
        SetDot(EntraDot, entraDot);
        SetDot(EntraFlyoutDot, entraDot);
        ToolTip.SetTip(EntraChip, entraTip);
        AutomationProperties.SetName(EntraChip, $"Entra ID: {entraTip}");
        EntraStatusText.Text = entraStatus;
        EntraTenantText.Text = s.GraphTenantId;
        EntraErrorText.IsVisible = session.GraphState == GraphConnectionState.Failed && !string.IsNullOrEmpty(session.GraphLastError);
        EntraErrorText.Text = session.GraphLastError;
        EntraShowCodeButton.IsVisible = session.GraphState == GraphConnectionState.WaitingForSignIn && session.PendingDeviceCode is not null;
    }

    private enum DotState
    {
        Hollow,
        Pulse,
        Success,
        Caution,
        Critical,
    }

    private static void SetDot(Ellipse dot, DotState state)
    {
        dot.Classes.Set("attention", state == DotState.Pulse);
        dot.Classes.Set("pulse", state == DotState.Pulse);
        dot.Classes.Set("success", state == DotState.Success);
        dot.Classes.Set("caution", state == DotState.Caution);
        dot.Classes.Set("critical", state == DotState.Critical);
    }
}
