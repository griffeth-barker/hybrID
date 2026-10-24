using System;

namespace HybridID.Views;

/// <summary>
/// Per-OS shortcut labels for tooltips and the empty-state shortcut list (spec section 7.1):
/// "Ctrl+Shift+C" on Windows/Linux, "⌘⇧C" on macOS. F-keys and Enter/Esc stay as written.
/// </summary>
public static class ShortcutText
{
    /// <summary>Formats a gesture written in Windows notation ("Ctrl+Shift+C", "Ctrl+,", "F5").</summary>
    public static string Format(string windowsGesture)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return windowsGesture;
        }

        var parts = windowsGesture.Split('+');
        var result = "";
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var isLast = i == parts.Length - 1;
            if (isLast && part.Length == 0 && windowsGesture.EndsWith("+", StringComparison.Ordinal))
            {
                part = "+";
            }

            result += part switch
            {
                "Ctrl" when !isLast => "⌘",
                "Shift" when !isLast => "⇧",
                "Alt" when !isLast => "⌥",
                _ => part,
            };
        }

        return result;
    }
}
