using System.Threading.Tasks;
using HybridID.Controls;
using HybridID.Services;

namespace HybridID.Views;

public enum AppPage
{
    Search,
    Settings,
    About,
}

/// <summary>What pages may ask of the shell (docs/design/ui-spec.md section 2.3). Implemented by MainWindow.</summary>
public interface IAppShell
{
    AppSession Session { get; }

    /// <summary>Navigates (no-op if already current). Leaving a page first awaits its <see cref="IShellPage.CanLeaveAsync"/>.</summary>
    void Navigate(AppPage page);

    /// <summary>Shows an in-window overlay dialog (queued FIFO). Completes when it is hidden.</summary>
    Task<OverlayDialogResult> ShowDialogAsync(OverlayDialog dialog);

    /// <summary>Transient bottom-center toast; also announced to screen readers.</summary>
    void ShowToast(string message, ToastKind kind = ToastKind.Success);

    /// <summary>Screen-reader live region (polite).</summary>
    void Announce(string text);

    /// <summary>
    /// Writes text to the clipboard. Returns false (after showing the Error toast "Couldn't access
    /// the clipboard.") when the clipboard is unavailable. Callers show their own success toast.
    /// </summary>
    Task<bool> CopyTextAsync(string text);

    bool IsOverlayOpen { get; }
}

/// <summary>Page-scoped shortcuts that MainWindow forwards to the current page (spec section 7.1).</summary>
public enum AppShortcut
{
    /// <summary>Ctrl/Cmd+L, Ctrl/Cmd+F (after navigating to Search): focus the search box and select all.</summary>
    FocusSearch,
    /// <summary>Esc with no overlay open.</summary>
    Escape,
    /// <summary>Ctrl/Cmd+Shift+C.</summary>
    CopySummary,
    /// <summary>Ctrl/Cmd+1.</summary>
    OpenIdentityPortal,
    /// <summary>Ctrl/Cmd+2.</summary>
    OpenMailboxPortal,
    /// <summary>F5, Ctrl/Cmd+R.</summary>
    SearchAgain,
    /// <summary>Ctrl/Cmd+S.</summary>
    Save,
}

/// <summary>Implemented by every page hosted in MainWindow (SearchPage, SettingsPage, AboutPage).</summary>
public interface IShellPage
{
    /// <summary>Handle a page-scoped shortcut. Return true if handled (the key is then marked handled).</summary>
    bool HandleShortcut(AppShortcut shortcut);

    /// <summary>
    /// Asked before navigating away or closing the window. Return false to stay (e.g. the
    /// unsaved-settings prompt was cancelled). Return a completed task when nothing is pending.
    /// </summary>
    Task<bool> CanLeaveAsync();

    /// <summary>Called after the page became the current page.</summary>
    void OnNavigatedTo();

    /// <summary>Called after the page stopped being the current page.</summary>
    void OnNavigatedFrom();

    /// <summary>Focus the page's primary element (focus fallback after dialogs, initial focus).</summary>
    void FocusDefault();
}
