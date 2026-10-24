using System.Threading.Tasks;
using Avalonia.Controls;

namespace HybridID.Views.Pages;

/// <summary>PLACEHOLDER page; the full implementation follows the spec in a later phase. Public surface is final.</summary>
public partial class SearchPage : UserControl, IShellPage
{
    private readonly IAppShell? _shell;

    /// <summary>For the XAML previewer only.</summary>
    public SearchPage()
    {
        InitializeComponent();
    }

    public SearchPage(IAppShell shell) : this()
    {
        _shell = shell;
    }

    public bool HandleShortcut(AppShortcut shortcut)
    {
        if (shortcut == AppShortcut.FocusSearch)
        {
            FocusDefault();
            return true;
        }

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
        TxtSearch.Focus();
        TxtSearch.SelectAll();
    }
}
