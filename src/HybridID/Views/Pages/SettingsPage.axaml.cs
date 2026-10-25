using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using HybridID.Controls;
using HybridID.Models;
using HybridID.Services;
using HybridID.Views.Dialogs;

namespace HybridID.Views.Pages;

/// <summary>
/// All settings (docs/design/ui-spec.md section 5.2). Appearance applies and saves immediately;
/// every other field is staged behind the SaveBar and written to <see cref="AppSession.Config"/>
/// in place on Save (IdentityLookupService shares that instance, so it must not change before).
/// </summary>
public partial class SettingsPage : UserControl, IShellPage
{
    private const string TestHint = "Uses the values above, even if they aren't saved yet.";
    private const string CertCommand = "export LDAPTLS_REQCERT=never";

    /// <summary>The six link templates, in display order.</summary>
    private static readonly (string Label, bool IsEcp, Func<ManagementUrls, string> Get, Action<ManagementUrls, string> Set)[] Templates =
    {
        ("Entra ID – user", false, u => u.EntraUser, (u, v) => u.EntraUser = v),
        ("Entra ID – group", false, u => u.EntraGroup, (u, v) => u.EntraGroup = v),
        ("Exchange Online – mailbox", false, u => u.ExoMailbox, (u, v) => u.ExoMailbox = v),
        ("Exchange Online – group", false, u => u.ExoGroup, (u, v) => u.ExoGroup = v),
        ("Exchange Server (ECP) – mailbox", true, u => u.OnPremEcpMailbox, (u, v) => u.OnPremEcpMailbox = v),
        ("Exchange Server (ECP) – group", true, u => u.OnPremEcpGroup, (u, v) => u.OnPremEcpGroup = v),
    };

    private readonly IAppShell? _shell;
    private readonly List<(TextBox Box, TextBlock Caption)> _templateRows = new();
    private bool _populating;
    private bool _isTestingLdap;
    private bool _isTestingGraph;
    private bool _saveBarShown;
    private CancellationTokenSource? _savedBarCts;

    /// <summary>For the XAML previewer only.</summary>
    public SettingsPage()
    {
        InitializeComponent();
        BuildTemplateRows();
    }

    public SettingsPage(IAppShell shell) : this()
    {
        _shell = shell;
        ApplyPlatformLimits();
        WireEvents();
        Populate();
    }

    private AppSession? Session => _shell?.Session;

    // ------------------------------------------------------------------ IShellPage

    public bool HandleShortcut(AppShortcut shortcut)
    {
        if (shortcut == AppShortcut.Save && IsDirty())
        {
            Save();
            return true;
        }

        return false;
    }

    public async Task<bool> CanLeaveAsync()
    {
        if (_shell is null || !IsDirty())
        {
            return true;
        }

        var dialog = new MessageDialog("Save changes?", "You have unsaved settings.", "Cancel", "Save", "Don't save");
        switch (await _shell.ShowDialogAsync(dialog))
        {
            case OverlayDialogResult.Primary:
                return Save();
            case OverlayDialogResult.Secondary:
                Populate();
                return true;
            default:
                return false;
        }
    }

    public void OnNavigatedTo()
    {
        if (!IsDirty())
        {
            Populate();
        }
    }

    public void OnNavigatedFrom()
    {
    }

    public void FocusDefault() => CfgLdapServer.Focus();

    // ------------------------------------------------------------------ Setup

    private void BuildTemplateRows()
    {
        foreach (var (label, _, _, _) in Templates)
        {
            var box = new TextBox { Classes = { "mono" } };
            Avalonia.Automation.AutomationProperties.SetName(box, label);
            var reset = new Button
            {
                Classes = { "subtle", "icon" },
                Width = 32,
                Height = 32,
                Content = new PathIcon { Data = Icon("Icon.ArrowReset"), Width = 16, Height = 16 },
            };
            ToolTip.SetTip(reset, "Reset");
            Avalonia.Automation.AutomationProperties.SetName(reset, $"Reset {label}");
            var index = _templateRows.Count;
            reset.Click += (_, _) => box.Text = Templates[index].Get(new ManagementUrls());

            var caption = new TextBlock { Classes = { "caption" }, TextWrapping = TextWrapping.Wrap, IsVisible = false };
            var input = new Grid { ColumnDefinitions = new ColumnDefinitions("*,4,Auto"), Margin = new Thickness(0, 8, 0, 0) };
            input.Children.Add(box);
            Grid.SetColumn(reset, 2);
            input.Children.Add(reset);

            TemplateRows.Children.Add(new Border
            {
                Classes = { "templateRow" },
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = label, Classes = { "body" } },
                        input,
                        caption,
                    },
                },
            });
            _templateRows.Add((box, caption));
        }
    }

    private void ApplyPlatformLimits()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        ItemStartTls.IsEnabled = false;
        ChkLdapSkipCert.IsEnabled = false;
        SkipCertCard.Description =
            "Not supported on macOS/Linux. Set LDAPTLS_REQCERT=never in your shell before launching hybrID.";
        BtnCopyCertCommand.IsVisible = true;
    }

    private void WireEvents()
    {
        CfgLdapServer.TextChanged += (_, _) => OnFormChanged();
        CfgLdapBaseDn.TextChanged += (_, _) => OnFormChanged();
        CfgFqdn.TextChanged += (_, _) => OnFormChanged();
        CfgGraphTenant.TextChanged += (_, _) => OnFormChanged();
        CfgGraphClientId.TextChanged += (_, _) => OnFormChanged();
        CfgLdapPort.ValueChanged += (_, _) => OnFormChanged();
        ChkLdapSkipCert.IsCheckedChanged += (_, _) => OnFormChanged();
        CmbLdapEncryption.SelectionChanged += (_, _) => OnEncryptionChanged();
        CmbTheme.SelectionChanged += (_, _) => OnThemeChanged();
        foreach (var (box, _) in _templateRows)
        {
            box.TextChanged += (_, _) => OnFormChanged();
        }

        BtnResetClientId.Click += (_, _) => CfgGraphClientId.Text = new AppSettings().GraphClientId;
        BtnResetTemplates.Click += async (_, _) => await ResetTemplatesAsync();
        BtnSave.Click += (_, _) => Save();
        BtnDiscard.Click += (_, _) => Populate();
        BtnTestLdap.Click += async (_, _) => await TestLdapAsync();
        BtnTestGraph.Click += async (_, _) => await TestGraphAsync();
        BtnCopyCertCommand.Click += async (_, _) => await CopyAsync(CertCommand, "Command copied");
        BtnCopyConfigPath.Click += async (_, _) => await CopyAsync(Session?.ConfigService.ConfigPath, "Path copied");
        BtnOpenConfigFolder.Click += (_, _) => OpenConfigFolder();
        AboutCard.Click += (_, _) => _shell?.Navigate(AppPage.About);
    }

    // ------------------------------------------------------------------ Form <-> config

    /// <summary>Loads the form from the saved config (legacy load logic). Also Discard.</summary>
    private void Populate()
    {
        if (Session is not { } session)
        {
            return;
        }

        _populating = true;
        try
        {
            var s = session.Config.Settings;
            CfgLdapServer.Text = s.LdapServer;
            CmbLdapEncryption.SelectedIndex = EncryptionIndexFor(s);
            CfgLdapPort.Value = s.LdapPort;
            CfgLdapBaseDn.Text = s.LdapBaseDn;
            ChkLdapSkipCert.IsChecked = s.LdapSkipCertificateCheck;
            CfgFqdn.Text = s.OnPremExchangeFqdn;
            CfgGraphTenant.Text = s.GraphTenantId;
            CfgGraphClientId.Text = s.GraphClientId;
            CmbTheme.SelectedIndex = ThemeIndexFor(s.Theme);
            for (var i = 0; i < Templates.Length; i++)
            {
                _templateRows[i].Box.Text = Templates[i].Get(session.Config.ManagementUrls);
            }

            LdapAdvanced.IsExpanded = !string.IsNullOrWhiteSpace(s.LdapBaseDn) || s.LdapSkipCertificateCheck;
            GraphAdvanced.IsExpanded = !string.Equals(s.GraphClientId, new AppSettings().GraphClientId, StringComparison.OrdinalIgnoreCase);
            ConfigPathText.Text = session.ConfigService.ConfigPath;
            AboutCard.Description = $"Version {AppVersion.Current}";
        }
        finally
        {
            _populating = false;
        }

        OnFormChanged();
    }

    /// <summary>Staged values as settings (Theme is not staged: it's taken from the saved config).</summary>
    private AppSettings ReadSettings()
    {
        var saved = Session?.Config.Settings ?? new AppSettings();
        var form = new AppSettings
        {
            LdapServer = CfgLdapServer.Text?.Trim() ?? "",
            // Invalid/blank port keeps the saved value (legacy) rather than an unrelated default.
            LdapPort = CfgLdapPort.Value is { } port and >= 1m and <= 65535m ? (int)port : saved.LdapPort,
            LdapBaseDn = CfgLdapBaseDn.Text?.Trim() ?? "",
            LdapSkipCertificateCheck = ChkLdapSkipCert.IsChecked == true,
            OnPremExchangeFqdn = CfgFqdn.Text?.Trim() ?? "",
            GraphTenantId = string.IsNullOrWhiteSpace(CfgGraphTenant.Text) ? "organizations" : CfgGraphTenant.Text.Trim(),
            GraphClientId = CfgGraphClientId.Text?.Trim() ?? "",
            Theme = saved.Theme,
        };
        ApplyEncryptionIndex(form, CmbLdapEncryption.SelectedIndex);
        return form;
    }

    private ManagementUrls ReadTemplates()
    {
        var urls = new ManagementUrls();
        for (var i = 0; i < Templates.Length; i++)
        {
            Templates[i].Set(urls, _templateRows[i].Box.Text?.Trim() ?? "");
        }

        return urls;
    }

    private bool IsDirty()
    {
        if (Session is not { } session)
        {
            return false;
        }

        var form = ReadSettings();
        var saved = session.Config.Settings;
        var templates = ReadTemplates();
        return ConnectionChanged(form, saved) ||
               form.OnPremExchangeFqdn != saved.OnPremExchangeFqdn ||
               Templates.Any(t => t.Get(templates) != t.Get(session.Config.ManagementUrls));
    }

    /// <summary>Changes that invalidate the LDAP connection or the Graph client.</summary>
    private static bool ConnectionChanged(AppSettings form, AppSettings saved) =>
        form.LdapServer != saved.LdapServer ||
        form.LdapPort != saved.LdapPort ||
        form.LdapUseLdaps != saved.LdapUseLdaps ||
        form.LdapUseStartTls != saved.LdapUseStartTls ||
        form.LdapBaseDn != saved.LdapBaseDn ||
        form.LdapSkipCertificateCheck != saved.LdapSkipCertificateCheck ||
        !string.Equals(form.GraphTenantId, saved.GraphTenantId, StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(form.GraphClientId, saved.GraphClientId, StringComparison.OrdinalIgnoreCase);

    /// <summary>Validates every staged field, updating inline messages. True when Save is allowed.</summary>
    private bool Validate()
    {
        var valid = true;

        var portValid = CfgLdapPort.Value is >= 1m and <= 65535m;
        CfgLdapPort.Classes.Set("invalid", !portValid);

        var clientIdValid = Guid.TryParse(CfgGraphClientId.Text?.Trim(), out _);
        CfgGraphClientId.Classes.Set("invalid", !clientIdValid);
        ClientIdCard.Description = clientIdValid
            ? "Default is the Microsoft Graph Command Line Tools public client. Change only if Conditional Access requires your own app registration."
            : StatusLine("Icon.Error", "critical", "Enter a GUID.");
        valid &= clientIdValid;

        for (var i = 0; i < Templates.Length; i++)
        {
            var (box, caption) = _templateRows[i];
            var text = box.Text ?? "";
            var hasId = text.Contains("{id}", StringComparison.Ordinal);
            box.Classes.Set("invalid", !hasId);
            caption.Classes.Set("critical", !hasId);
            caption.Classes.Set("caution", hasId);
            caption.Text = !hasId ? "Must contain {id}."
                : Templates[i].IsEcp && !text.Contains("{fqdn}", StringComparison.Ordinal)
                    ? "No {fqdn} placeholder; the Exchange server setting won't be used."
                    : null;
            caption.IsVisible = caption.Text is not null;
            valid &= hasId;
        }

        return valid;
    }

    private void OnFormChanged()
    {
        if (_populating)
        {
            return;
        }

        var valid = Validate();
        UpdateInlineWarnings();

        var dirty = IsDirty();
        BtnSave.IsEnabled = valid;
        ToolTip.SetTip(BtnSave, $"Save ({ShortcutText.Format("Ctrl+S")})");
        SaveBarSignOutText.IsVisible = Session is { } session && ConnectionChanged(ReadSettings(), session.Config.Settings);
        ShowSaveBar(dirty);
    }

    private void UpdateInlineWarnings()
    {
        var encryption = CmbLdapEncryption.SelectedIndex;
        NoEncryptionBar.IsOpen = encryption == 2;
        StartTlsUnsupportedBar.IsOpen = encryption == 1 && !OperatingSystem.IsWindows();

        var skip = ChkLdapSkipCert.IsChecked == true;
        SkipCertBar.IsOpen = skip;
        SkipCertBar.Title = OperatingSystem.IsWindows() ? "Certificate validation is off." : "This setting is ignored on this OS.";

        var fqdn = CfgFqdn.Text?.Trim();
        const string fqdnHint = "FQDN used to build Exchange admin center (ECP) links.";
        FqdnCard.Description = string.IsNullOrEmpty(fqdn) || fqdn == AppSession.DefaultExchangeFqdn
            ? new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock { Text = fqdnHint, Classes = { "caption", "secondary" }, TextWrapping = TextWrapping.Wrap },
                    StatusLine("Icon.Warning", "caution", "Still set to the placeholder. ECP links won't work."),
                },
            }
            : fqdnHint;
    }

    private void OnEncryptionChanged()
    {
        if (!_populating)
        {
            // Keep the port in step with the encryption choice (only from the other default).
            var port = CfgLdapPort.Value;
            if (CmbLdapEncryption.SelectedIndex == 0 && port == 389)
            {
                CfgLdapPort.Value = 636;
            }
            else if (CmbLdapEncryption.SelectedIndex is 1 or 2 && port == 636)
            {
                CfgLdapPort.Value = 389;
            }
        }

        OnFormChanged();
    }

    private void OnThemeChanged()
    {
        if (_populating || Session is not { } session)
        {
            return;
        }

        // Appearance is not staged: apply and save just the Theme field now.
        session.SaveTheme(ThemeForIndex(CmbTheme.SelectedIndex));
    }

    private bool Save()
    {
        if (Session is not { } session || !Validate())
        {
            return false;
        }

        var form = ReadSettings();
        var templates = ReadTemplates();
        var connectionChanged = ConnectionChanged(form, session.Config.Settings);

        // Write into the shared instance (never replace it): IdentityLookupService reads it.
        var s = session.Config.Settings;
        s.LdapServer = form.LdapServer;
        s.LdapPort = form.LdapPort;
        s.LdapUseLdaps = form.LdapUseLdaps;
        s.LdapUseStartTls = form.LdapUseStartTls;
        s.LdapBaseDn = form.LdapBaseDn;
        s.LdapSkipCertificateCheck = form.LdapSkipCertificateCheck;
        s.OnPremExchangeFqdn = form.OnPremExchangeFqdn;
        s.GraphTenantId = form.GraphTenantId;
        s.GraphClientId = form.GraphClientId;
        foreach (var t in Templates)
        {
            t.Set(session.Config.ManagementUrls, t.Get(templates));
        }

        try
        {
            session.ConfigService.Save(session.Config);
        }
        catch (Exception ex)
        {
            _shell?.ShowToast($"Couldn't save settings: {ex.Message}", ToastKind.Error);
            return false;
        }

        session.ApplySavedConfig(connectionChanged);
        Populate();
        _ = ShowSavedBarAsync();
        return true;
    }

    private async Task ShowSavedBarAsync()
    {
        _savedBarCts?.Cancel();
        var cts = _savedBarCts = new CancellationTokenSource();
        SavedBar.IsOpen = true;
        _shell?.Announce("Settings saved");
        try
        {
            await Task.Delay(4000, cts.Token);
            SavedBar.IsOpen = false;
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void ShowSaveBar(bool show)
    {
        if (show == _saveBarShown)
        {
            return;
        }

        _saveBarShown = show;
        SaveBar.IsVisible = show;
        if (!show)
        {
            return;
        }

        // Start values without transitions, then let the style's transitions animate in.
        SaveBar.Transitions = new Avalonia.Animation.Transitions();
        SaveBar.Opacity = 0;
        SaveBar.RenderTransform = TransformOperations.Parse("translateY(16px)");
        SaveBar.ClearValue(TransitionsProperty);
        Dispatcher.UIThread.Post(() =>
        {
            SaveBar.Opacity = 1;
            SaveBar.RenderTransform = TransformOperations.Parse("translateY(0px)");
        }, DispatcherPriority.Render);
    }

    private async Task ResetTemplatesAsync()
    {
        if (_shell is null)
        {
            return;
        }

        var dialog = new MessageDialog("Reset all link templates?",
            "This replaces your 6 templates with the defaults. You still need to save.", "Cancel", "Reset");
        if (await _shell.ShowDialogAsync(dialog) == OverlayDialogResult.Primary)
        {
            var defaults = new ManagementUrls();
            for (var i = 0; i < Templates.Length; i++)
            {
                _templateRows[i].Box.Text = Templates[i].Get(defaults);
            }
        }
    }

    // ------------------------------------------------------------------ Test connection

    private async Task TestLdapAsync()
    {
        if (_isTestingLdap || _shell is null || Session is not { } session)
        {
            return;
        }

        var form = ReadSettings();
        if (string.IsNullOrWhiteSpace(form.LdapServer))
        {
            TestLdapCard.Description = StatusLine("Icon.Error", "critical", "Enter a domain controller first.");
            return;
        }

        _isTestingLdap = true;
        BtnTestLdap.IsEnabled = false;
        SetRing(TestLdapRing, true);
        (bool Ok, string Message)? last = null;
        try
        {
            var dialog = new AdCredentialDialog(AppSession.ServerLabel(form), async (user, password) =>
            {
                var result = await session.TestLdapAsync(form, user, password);
                last = result;
                return result.Ok ? null : result.Message;
            }, AdCredentialPurpose.Test);
            await _shell.ShowDialogAsync(dialog);

            if (last is { } r)
            {
                TestLdapCard.Description = r.Ok
                    ? StatusLine("Icon.CheckCircle", "success", r.Message)
                    : StatusLine("Icon.Error", "critical", $"Connection failed: {r.Message}");
                _shell.Announce(r.Ok ? r.Message : $"Connection failed: {r.Message}");
            }
        }
        finally
        {
            _isTestingLdap = false;
            BtnTestLdap.IsEnabled = true;
            SetRing(TestLdapRing, false);
        }
    }

    private async Task TestGraphAsync()
    {
        if (_isTestingGraph || _shell is null || Session is not { } session)
        {
            return;
        }

        _isTestingGraph = true;
        BtnTestGraph.IsEnabled = false;
        SetRing(TestGraphRing, true);
        TestGraphCard.Description = "Testing…";
        DeviceCodeDialog? codeDialog = null;
        try
        {
            var form = ReadSettings();
            var (ok, message) = await session.TestGraphAsync(form.GraphTenantId, form.GraphClientId, (info, ct) =>
            {
                // Device-code callback runs off the UI thread (spec 12.2 #13).
                Dispatcher.UIThread.Post(() =>
                {
                    codeDialog = new DeviceCodeDialog(info);
                    codeDialog.CodeCopied += (_, copied) => _shell.ShowToast(
                        copied ? "Code copied" : "Couldn't access the clipboard.", copied ? ToastKind.Success : ToastKind.Error);
                    _ = _shell.ShowDialogAsync(codeDialog);
                });
                return Task.CompletedTask;
            });

            TestGraphCard.Description = ok
                ? StatusLine("Icon.CheckCircle", "success", message)
                : StatusLine("Icon.Error", "critical", $"Connection failed: {message}");
            _shell.Announce(ok ? message : $"Connection failed: {message}");
        }
        finally
        {
            codeDialog?.Hide(OverlayDialogResult.None);
            _isTestingGraph = false;
            BtnTestGraph.IsEnabled = true;
            SetRing(TestGraphRing, false);
        }
    }

    // ------------------------------------------------------------------ Helpers

    private async Task CopyAsync(string? text, string toast)
    {
        if (_shell is not null && !string.IsNullOrEmpty(text) && await _shell.CopyTextAsync(text))
        {
            _shell.ShowToast(toast);
        }
    }

    private void OpenConfigFolder()
    {
        var dir = Path.GetDirectoryName(Session?.ConfigService.ConfigPath);
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        try
        {
            UrlLauncher.Open(dir);
        }
        catch (Exception ex)
        {
            _shell?.ShowToast($"Couldn't open the folder: {ex.Message}", ToastKind.Error);
        }
    }

    private static void SetRing(ProgressRing ring, bool active)
    {
        ring.IsVisible = active;
        ring.IsActive = active;
    }

    /// <summary>"[icon 12] text" caption line for card descriptions.</summary>
    private static Control StatusLine(string iconKey, string kind, string text) => new Grid
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,4,*"),
        Children =
        {
            new PathIcon
            {
                Data = Icon(iconKey),
                Width = 12,
                Height = 12,
                Classes = { kind },
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 3, 0, 0),
            },
            WithColumn(new TextBlock { Text = text, Classes = { "caption", "secondary" }, TextWrapping = TextWrapping.Wrap }, 2),
        },
    };

    private static Control WithColumn(Control control, int column)
    {
        Grid.SetColumn(control, column);
        return control;
    }

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

    /// <summary>Icon geometries are theme-invariant, so resolving them once from code is safe.</summary>
    private static Geometry? Icon(string key) => Application.Current?.FindResource(key) as Geometry;
}
