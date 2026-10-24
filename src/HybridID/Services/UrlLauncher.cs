using System.Diagnostics;

namespace HybridID.Services;

/// <summary>
/// Cross-platform replacement for Open-UrlInNewWindow.ps1's Windows-registry browser lookup.
/// Process.Start with UseShellExecute is natively cross-platform: it uses ShellExecute on
/// Windows, `open` on macOS, and `xdg-open` on Linux, each opening the URL in the OS default
/// browser.
/// </summary>
public static class UrlLauncher
{
    public static void Open(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
