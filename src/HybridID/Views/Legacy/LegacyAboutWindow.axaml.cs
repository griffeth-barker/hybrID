using Avalonia.Controls;
using Avalonia.Input;
using HybridID.Services;

namespace HybridID.Views.Legacy;

public partial class LegacyAboutWindow : Window
{
    private const string GitHubUrl = "https://github.com/griffeth-barker/hybrID";

    public LegacyAboutWindow()
    {
        InitializeComponent();

        VersionText.Text = $"hybrID v{AppVersion.Current}";
        BtnClose.Click += (_, _) => Close();
        LnkGitHub.PointerPressed += (_, _) => UrlLauncher.Open(GitHubUrl);
    }
}
