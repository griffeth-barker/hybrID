using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using Avalonia.Platform;
using Avalonia.Threading;
using HybridID.Controls;
using HybridID.Services;
using HybridID.Views.Dialogs;
using HybridID.Views.Pages;

namespace HybridID.Views;

/// <summary>
/// The only OS window: per-OS chrome and backdrop, nav rail, cached pages, overlay dialogs,
/// toasts, announcer and global shortcuts (docs/design/ui-spec.md sections 2, 4, 7.1). Pages talk
/// to it only through <see cref="IAppShell"/>.
/// </summary>
public partial class MainWindow : Window, IAppShell
{
    private const double PaneAutoOpenWidth = 1008;
    private static readonly Easing Decelerate = new SplineEasing(0.1, 0.9, 0.2, 1);

    private readonly SearchPage _searchPage;
    private readonly SettingsPage _settingsPage;
    private readonly AboutPage _aboutPage;

    private AppPage _currentPage = AppPage.Search;
    private bool _isNavigating;
    private bool _suppressNavEvents;
    private bool? _lastWide;
    private bool _closeConfirmed;
    private DeviceCodeDialog? _chipDeviceCodeDialog;

    public MainWindow()
    {
        InitializeComponent();

        Session = new AppSession();
        ThemeService.Apply(Session.Config.Settings.Theme);

        ConfigureChrome();
        ConfigureBackdrop();

        _searchPage = new SearchPage(this, CreateLookupSource(Session));
        _settingsPage = new SettingsPage(this);
        _aboutPage = new AboutPage(this);

        PageHost.PageTransition = new CrossFade(TimeSpan.FromMilliseconds(167)) { FadeInEasing = Decelerate };
        PageHost.Content = _searchPage;

        TitleBar.Attach(Session);
        TitleBar.PaneToggleClick += (_, _) => NavSplitView.IsPaneOpen = !NavSplitView.IsPaneOpen;
        TitleBar.SettingsRequested += (_, _) => Navigate(AppPage.Settings);
        TitleBar.AdSignInRequested += async (_, _) => await SignInToAdFromChipAsync();
        TitleBar.ShowSignInCodeRequested += async (_, _) => await ShowPendingDeviceCodeAsync();
        Session.StateChanged += OnSessionStateChanged;

        NavSearch.IsCheckedChanged += (_, _) => OnNavItemChecked(NavSearch, AppPage.Search);
        NavSettings.IsCheckedChanged += (_, _) => OnNavItemChecked(NavSettings, AppPage.Settings);
        NavAbout.IsCheckedChanged += (_, _) => OnNavItemChecked(NavAbout, AppPage.About);
        NavSplitView.PropertyChanged += (_, e) =>
        {
            if (e.Property == SplitView.IsPaneOpenProperty)
            {
                UpdatePaneState();
            }
        };
        UpdatePaneState();

        Overlay.IsOpenChanged += (_, _) => OnOverlayOpenChanged();
        Overlay.RestoreFocusFallback = () => CurrentShellPage.FocusDefault();

        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        SizeChanged += (_, e) => UpdatePaneForWidth(e.NewSize.Width);
        Activated += (_, _) => TitleBar.IsWindowActive = true;
        Deactivated += (_, _) => TitleBar.IsWindowActive = false;
        Opened += (_, _) =>
        {
            _searchPage.OnNavigatedTo();
            _searchPage.FocusDefault();
        };
        Closing += OnWindowClosing;
        Closed += (_, _) => Session.Dispose();
    }

    public AppSession Session { get; }

    /// <summary>True when started with --preview or HYBRID_PREVIEW=1: Search shows sample data.</summary>
    public static bool IsPreviewMode =>
        Environment.GetCommandLineArgs().Contains("--preview", StringComparer.OrdinalIgnoreCase) ||
        Environment.GetEnvironmentVariable("HYBRID_PREVIEW") == "1";

    private ILookupSource CreateLookupSource(AppSession session) =>
        IsPreviewMode ? new PreviewLookupSource(session.Config) : new LiveLookupSource(this);

    public bool IsOverlayOpen => Overlay.IsOpen;

    // ------------------------------------------------------------------ IAppShell

    public void Navigate(AppPage page) => _ = NavigateAsync(page);

    public Task<OverlayDialogResult> ShowDialogAsync(OverlayDialog dialog) => Overlay.ShowAsync(dialog);

    public void ShowToast(string message, ToastKind kind = ToastKind.Success)
    {
        Toasts.Show(message, kind);
        Announce(message);
    }

    public void Announce(string text)
    {
        // Clear first so repeating the same message is announced again.
        Announcer.Text = "";
        AutomationProperties.SetName(Announcer, "");
        Dispatcher.UIThread.Post(() =>
        {
            Announcer.Text = text;
            AutomationProperties.SetName(Announcer, text);
        });
    }

    public async Task<bool> CopyTextAsync(string text)
    {
        if (await ClipboardHelper.TrySetTextAsync(this, text))
        {
            return true;
        }

        ShowToast("Couldn't access the clipboard.", ToastKind.Error);
        return false;
    }

    // ------------------------------------------------------------------ Navigation

    private IShellPage CurrentShellPage => PageFor(_currentPage);

    private IShellPage PageFor(AppPage page) => page switch
    {
        AppPage.Settings => _settingsPage,
        AppPage.About => _aboutPage,
        _ => _searchPage,
    };

    private RadioButton NavItemFor(AppPage page) => page switch
    {
        AppPage.Settings => NavSettings,
        AppPage.About => NavAbout,
        _ => NavSearch,
    };

    /// <summary>Navigates, honoring the current page's CanLeaveAsync. Returns false if cancelled.</summary>
    public async Task<bool> NavigateAsync(AppPage page)
    {
        if (page == _currentPage)
        {
            SyncNavSelection();
            return true;
        }

        if (_isNavigating)
        {
            SyncNavSelection();
            return false;
        }

        _isNavigating = true;
        try
        {
            if (!await CurrentShellPage.CanLeaveAsync())
            {
                SyncNavSelection();
                return false;
            }

            var from = CurrentShellPage;
            _currentPage = page;
            var to = (Control)PageFor(page);
            SyncNavSelection();

            PlayEntrance(to);
            PageHost.Content = to;

            from.OnNavigatedFrom();
            PageFor(page).OnNavigatedTo();
            return true;
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private void OnNavItemChecked(RadioButton item, AppPage page)
    {
        if (_suppressNavEvents || item.IsChecked != true)
        {
            return;
        }

        Navigate(page);
    }

    private void SyncNavSelection()
    {
        _suppressNavEvents = true;
        try
        {
            NavItemFor(_currentPage).IsChecked = true;
        }
        finally
        {
            _suppressNavEvents = false;
        }
    }

    /// <summary>
    /// Page entrance: TranslateY 16 → 0 over 250 ms (decelerate). Opacity is driven by the
    /// PageHost CrossFade (167 ms); animating it here too would fight the CrossFade animation.
    /// </summary>
    private static void PlayEntrance(Control page)
    {
        page.Transitions ??= new Transitions
        {
            new TransformOperationsTransition
            {
                Property = RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(250),
                Easing = Decelerate,
            },
        };

        var transitions = page.Transitions;
        page.Transitions = null;
        page.RenderTransform = TransformOperations.Parse("translateY(16px)");
        page.Transitions = transitions;
        Dispatcher.UIThread.Post(
            () => page.RenderTransform = TransformOperations.Parse("translateY(0px)"),
            DispatcherPriority.Render);
    }

    private void UpdatePaneForWidth(double width)
    {
        // Only react when crossing the threshold, so a manual toggle sticks until the next crossing.
        var wide = width >= PaneAutoOpenWidth;
        if (wide == _lastWide)
        {
            return;
        }

        _lastWide = wide;
        NavSplitView.IsPaneOpen = wide;
    }

    private void UpdatePaneState()
    {
        var open = NavSplitView.IsPaneOpen;
        TitleBar.IsPaneOpen = open;
        foreach (var item in new[] { NavSearch, NavAbout, NavSettings })
        {
            ToolTip.SetTip(item, open ? null : item.Content);
        }
    }

    // ------------------------------------------------------------------ Chrome & backdrop

    private void ConfigureChrome()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            WindowDecorations = WindowDecorations.Full;
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = 48;
        }
        else
        {
            // Linux: native WM decorations (Avalonia-drawn decorations vary by WM/compositor).
            ExtendClientAreaToDecorationsHint = false;
        }

        if (OperatingSystem.IsMacOS())
        {
            PropertyChanged += (_, e) =>
            {
                if (e.Property == WindowStateProperty)
                {
                    TitleBar.SetMacFullScreen(WindowState == WindowState.FullScreen);
                }
            };
        }
    }

    private void ConfigureBackdrop()
    {
        var highContrast = Application.Current?.PlatformSettings?.GetColorValues().ContrastPreference ==
                           ColorContrastPreference.High;

        if (highContrast || !(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()))
        {
            TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
        }
        else if (OperatingSystem.IsWindows())
        {
            // Windows 10 has no Mica → None → solid fallback. No AcrylicBlur on Win10 (resize lag).
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Mica, WindowTransparencyLevel.None };
        }
        else
        {
            TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None };
        }

        // macOS: keep the sidebar vibrant but make the dense content frame opaque.
        PropertyChanged += (_, e) =>
        {
            if (e.Property == ActualTransparencyLevelProperty)
            {
                UpdateContentFrameOpacity();
            }
        };
        UpdateContentFrameOpacity();
    }

    private void UpdateContentFrameOpacity() =>
        ContentFrame.Classes.Set("opaque",
            OperatingSystem.IsMacOS() && ActualTransparencyLevel == WindowTransparencyLevel.AcrylicBlur);

    // ------------------------------------------------------------------ Overlay

    private void OnOverlayOpenChanged()
    {
        // Keep programmatic Tab moves out of the background while a dialog is open (spec 6.7).
        // IsEnabled=False is deliberately not used: it would gray the background out.
        var mode = Overlay.IsOpen ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue;
        KeyboardNavigation.SetTabNavigation(TitleBar, mode);
        KeyboardNavigation.SetTabNavigation(NavPane, mode);
        KeyboardNavigation.SetTabNavigation(PageHost, mode);
    }

    private async Task SignInToAdFromChipAsync()
    {
        if (!Session.IsAdConfigured)
        {
            Navigate(AppPage.Settings);
            return;
        }

        var dialog = new AdCredentialDialog(Session.AdServerLabel, Session.ConnectLdapAsync, AdCredentialPurpose.Search);
        var result = await ShowDialogAsync(dialog);
        if (result != OverlayDialogResult.Primary && Session.AdState != AdConnectionState.Connected)
        {
            Session.SkipLdapForSession();
        }
    }

    private async Task ShowPendingDeviceCodeAsync()
    {
        if (Session.PendingDeviceCode is not { } info)
        {
            return;
        }

        _chipDeviceCodeDialog = new DeviceCodeDialog(info);
        _chipDeviceCodeDialog.CodeCopied += (_, ok) =>
            ShowToast(ok ? "Code copied" : "Couldn't access the clipboard.", ok ? ToastKind.Success : ToastKind.Error);
        await ShowDialogAsync(_chipDeviceCodeDialog);
        _chipDeviceCodeDialog = null;
    }

    private void OnSessionStateChanged(object? sender, EventArgs e)
    {
        // A dialog reopened from the Entra chip closes itself once the wait is over.
        if (_chipDeviceCodeDialog is { } dialog && Session.GraphState != GraphConnectionState.WaitingForSignIn)
        {
            dialog.Hide(OverlayDialogResult.None);
        }
    }

    // ------------------------------------------------------------------ Keyboard (spec 7.1)

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        // While a dialog is open only it handles keys (Esc/Enter via IsCancel/IsDefault, Tab cycles).
        if (Overlay.IsOpen)
        {
            return;
        }

        var command = Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
        var mods = e.KeyModifiers;
        var isCommand = mods == command;
        var isCommandShift = mods == (command | KeyModifiers.Shift);

        if (isCommand && e.Key is Key.L or Key.F)
        {
            e.Handled = true;
            _ = FocusSearchAsync();
            return;
        }

        if (isCommand && e.Key == Key.OemComma)
        {
            e.Handled = true;
            Navigate(AppPage.Settings);
            return;
        }

        AppShortcut? shortcut = null;
        if (mods == KeyModifiers.None && e.Key == Key.Escape)
        {
            shortcut = AppShortcut.Escape;
        }
        else if (isCommandShift && e.Key == Key.C)
        {
            shortcut = AppShortcut.CopySummary;
        }
        else if (isCommand && e.Key is Key.D1 or Key.NumPad1)
        {
            shortcut = AppShortcut.OpenIdentityPortal;
        }
        else if (isCommand && e.Key is Key.D2 or Key.NumPad2)
        {
            shortcut = AppShortcut.OpenMailboxPortal;
        }
        else if ((mods == KeyModifiers.None && e.Key == Key.F5) || (isCommand && e.Key == Key.R))
        {
            shortcut = AppShortcut.SearchAgain;
        }
        else if (isCommand && e.Key == Key.S)
        {
            shortcut = AppShortcut.Save;
        }

        if (shortcut is { } s && CurrentShellPage.HandleShortcut(s))
        {
            e.Handled = true;
        }
    }

    private async Task FocusSearchAsync()
    {
        if (await NavigateAsync(AppPage.Search))
        {
            _searchPage.HandleShortcut(AppShortcut.FocusSearch);
        }
    }

    // ------------------------------------------------------------------ Closing (spec 5.2.4)

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed)
        {
            return;
        }

        var canLeave = CurrentShellPage.CanLeaveAsync();
        if (canLeave.IsCompleted && canLeave.Result)
        {
            return;
        }

        e.Cancel = true;
        if (await canLeave)
        {
            _closeConfirmed = true;
            Close();
        }
    }
}
