using Avalonia;
using Avalonia.Styling;

namespace HybridID.Services;

/// <summary>
/// Cross-platform replacement for Apply-Theme.ps1's HKCU registry poll. Avalonia's
/// RequestedThemeVariant already tracks the OS light/dark setting on every platform when set to
/// Default, so "System" needs no manual detection at all - and unlike the original, switching
/// theme applies live across all open windows with no restart required.
/// </summary>
public static class ThemeService
{
    public static void Apply(string theme)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        app.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
