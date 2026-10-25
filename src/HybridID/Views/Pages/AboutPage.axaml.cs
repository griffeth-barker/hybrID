using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using HybridID.Controls;
using HybridID.Services;

namespace HybridID.Views.Pages;

/// <summary>Version, links, notices and system info (docs/design/ui-spec.md section 5.3).</summary>
public partial class AboutPage : UserControl, IShellPage
{
    private const string SourceUrl = "https://github.com/griffeth-barker/hybrID";
    private const string IssuesUrl = SourceUrl + "/issues";

    private static readonly (string Name, string License)[] ThirdParty =
    {
        ("Avalonia", "MIT"),
        ("Fluent UI System Icons", "MIT"),
        ("Inter", "SIL OFL 1.1"),
        ("Cascadia Mono", "SIL OFL 1.1"),
        ("Microsoft Graph SDK for .NET", "MIT"),
        ("Azure Identity", "MIT"),
    };

    private readonly IAppShell? _shell;

    /// <summary>For the XAML previewer only.</summary>
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = $"Version {AppVersion.Current}";

        foreach (var (name, license) in ThirdParty)
        {
            ThirdPartyRows.Children.Add(new SettingsCard { Classes = { "nested" }, Header = name, Description = license });
        }
    }

    public AboutPage(IAppShell shell) : this()
    {
        _shell = shell;

        foreach (var (label, value) in SystemInfo())
        {
            SystemRows.Children.Add(new SettingsCard
            {
                Classes = { "nested" },
                Header = label,
                Description = new SelectableTextBlock { Text = value, Classes = { "caption", "secondary" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            });
        }

        SourceCard.Click += (_, _) => Open(SourceUrl);
        IssuesCard.Click += (_, _) => Open(IssuesUrl);
        BtnCopyVersion.Click += async (_, _) => await CopyAsync(VersionInfo, "Version info copied");
        BtnCopySystem.Click += async (_, _) =>
            await CopyAsync(string.Join(Environment.NewLine, SystemInfo().Select(i => $"{i.Label}: {i.Value}")), "System information copied");
    }

    private static string VersionInfo =>
        $"hybrID {AppVersion.Current} · {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture} · .NET {Environment.Version}";

    private (string Label, string Value)[] SystemInfo() => new[]
    {
        ("hybrID", AppVersion.Current),
        ("Operating system", RuntimeInformation.OSDescription),
        ("Architecture", RuntimeInformation.OSArchitecture.ToString()),
        (".NET runtime", Environment.Version.ToString()),
        ("Configuration file", _shell?.Session.ConfigService.ConfigPath ?? ""),
    };

    // ------------------------------------------------------------------ IShellPage

    public bool HandleShortcut(AppShortcut shortcut) => false;

    public Task<bool> CanLeaveAsync() => Task.FromResult(true);

    public void OnNavigatedTo()
    {
    }

    public void OnNavigatedFrom()
    {
    }

    public void FocusDefault() => BtnCopyVersion.Focus();

    // ------------------------------------------------------------------ Helpers

    private async Task CopyAsync(string text, string toast)
    {
        if (_shell is not null && await _shell.CopyTextAsync(text))
        {
            _shell.ShowToast(toast);
        }
    }

    private void Open(string url)
    {
        try
        {
            UrlLauncher.Open(url);
        }
        catch (Exception ex)
        {
            _shell?.ShowToast($"Couldn't open the browser: {ex.Message}", ToastKind.Error);
        }
    }
}
