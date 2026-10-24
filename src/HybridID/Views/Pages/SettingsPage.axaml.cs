using System.Threading.Tasks;
using Avalonia.Controls;

namespace HybridID.Views.Pages;

/// <summary>PLACEHOLDER page; the full implementation follows the spec in a later phase. Public surface is final.</summary>
public partial class SettingsPage : UserControl, IShellPage
{
    private readonly IAppShell? _shell;

    /// <summary>For the XAML previewer only.</summary>
    public SettingsPage()
    {
        InitializeComponent();
    }

    public SettingsPage(IAppShell shell) : this()
    {
        _shell = shell;
    }

    public bool HandleShortcut(AppShortcut shortcut)
    {
        return false;
    }

    public Task<bool> CanLeaveAsync() => Task.FromResult(true);

    public void OnNavigatedTo()
    {
    }

    public void OnNavigatedFrom()
    {
    }

    public void FocusDefault()
    {
        Focus();
    }
}
