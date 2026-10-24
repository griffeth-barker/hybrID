using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Threading;

namespace HybridID.Controls;

/// <summary>
/// One transient toast at a time, bottom-center of the content frame (docs/design/ui-spec.md
/// section 6.10). A new toast replaces the current one. Not hit-testable. The shell also sends
/// the text to its screen-reader announcer.
/// </summary>
public partial class ToastHost : UserControl
{
    private int _version;

    public ToastHost()
    {
        InitializeComponent();
    }

    public async void Show(string message, ToastKind kind = ToastKind.Success)
    {
        var version = ++_version;

        Toast.Classes.Set("success", kind == ToastKind.Success);
        Toast.Classes.Set("info", kind == ToastKind.Info);
        Toast.Classes.Set("error", kind == ToastKind.Error);
        ToastText.Text = message;

        // Enter: fade + 8 px rise (transitions defined in XAML).
        Toast.IsVisible = true;
        Toast.Opacity = 0;
        Toast.RenderTransform = TransformOperations.Parse("translateY(8px)");
        await Task.Yield();
        Dispatcher.UIThread.Post(() =>
        {
            if (version != _version)
            {
                return;
            }

            Toast.Opacity = 1;
            Toast.RenderTransform = TransformOperations.Parse("translateY(0px)");
        }, DispatcherPriority.Render);

        await Task.Delay(kind == ToastKind.Error ? 4000 : 2000);
        if (version != _version)
        {
            return;
        }

        Toast.Opacity = 0;
        await Task.Delay(TimeSpan.FromMilliseconds(167));
        if (version == _version)
        {
            Toast.IsVisible = false;
        }
    }
}
