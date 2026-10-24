using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace HybridID.Controls;

/// <summary>Raised (bubbling) by <see cref="CopyButton"/>, <see cref="ResultField"/>,
/// <see cref="ResultListField"/> and <see cref="ManagementCard"/> after a clipboard write.</summary>
public sealed class CopiedEventArgs : RoutedEventArgs
{
    public CopiedEventArgs(RoutedEvent routedEvent, string? label, bool succeeded, bool isBulk)
        : base(routedEvent)
    {
        Label = label;
        Succeeded = succeeded;
        IsBulk = isBulk;
    }

    /// <summary>Field label ("Display name", "Proxy addresses", ...) for the announcement "{Label} copied".</summary>
    public string? Label { get; }

    /// <summary>False when the clipboard was unavailable: show the Error toast "Couldn't access the clipboard."</summary>
    public bool Succeeded { get; }

    /// <summary>True for "Copy all" / "Copy link" style copies, which get a toast instead of only an announcement.</summary>
    public bool IsBulk { get; }
}

/// <summary>Clipboard write used by the copy controls (and the shell).</summary>
public static class ClipboardHelper
{
    public static async Task<bool> TrySetTextAsync(Visual? visual, string? text)
    {
        if (visual is null || string.IsNullOrEmpty(text))
        {
            return false;
        }

        var clipboard = TopLevel.GetTopLevel(visual)?.Clipboard;
        if (clipboard is null)
        {
            return false;
        }

        try
        {
            await clipboard.SetTextAsync(text);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <summary>
/// Subtle icon button that copies <see cref="CopyText"/> and briefly swaps its copy icon for a
/// green check (docs/design/ui-spec.md section 6.5). Styled via the Button theme plus the
/// <c>subtle icon copy</c> classes; the check color comes from the <c>Button.copy:copied</c> style.
/// </summary>
public class CopyButton : Button
{
    public static readonly StyledProperty<string?> CopyTextProperty =
        AvaloniaProperty.Register<CopyButton, string?>(nameof(CopyText));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<CopyButton, string?>(nameof(Label));

    /// <summary>Bubbling event raised after every copy attempt (see <see cref="CopiedEventArgs"/>).</summary>
    public static readonly RoutedEvent<CopiedEventArgs> CopiedEvent =
        RoutedEvent.Register<CopyButton, CopiedEventArgs>("Copied", RoutingStrategies.Bubble);

    private readonly PathIcon _icon = new() { Width = 16, Height = 16 };
    private CancellationTokenSource? _resetCts;

    public CopyButton()
    {
        Classes.Add("subtle");
        Classes.Add("icon");
        Classes.Add("copy");
        Content = _icon;
        ToolTip.SetTip(this, "Copy");
        UpdateAutomationName();
    }

    protected override Type StyleKeyOverride => typeof(Button);

    /// <summary>Text written to the clipboard.</summary>
    public string? CopyText
    {
        get => GetValue(CopyTextProperty);
        set => SetValue(CopyTextProperty, value);
    }

    /// <summary>Field label used for the automation name ("Copy {Label}") and the Copied event.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SetIcon(copied: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            UpdateAutomationName();
        }
    }

    protected override async void OnClick()
    {
        base.OnClick();

        var ok = await ClipboardHelper.TrySetTextAsync(this, CopyText);
        RaiseEvent(new CopiedEventArgs(CopiedEvent, Label, ok, isBulk: false));
        if (!ok)
        {
            return;
        }

        _resetCts?.Cancel();
        var cts = _resetCts = new CancellationTokenSource();
        SetIcon(copied: true);
        try
        {
            await Task.Delay(1500, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => SetIcon(copied: false));
    }

    private void SetIcon(bool copied)
    {
        PseudoClasses.Set(":copied", copied);
        ToolTip.SetTip(this, copied ? "Copied" : "Copy");
        // Geometry is theme-invariant; color changes come from the Button.copy:copied style.
        if (this.TryFindResource(copied ? "Icon.Check" : "Icon.Copy", out var geometry))
        {
            _icon.Data = geometry as Geometry;
        }
    }

    private void UpdateAutomationName() =>
        AutomationProperties.SetName(this, string.IsNullOrEmpty(Label) ? "Copy" : $"Copy {Label}");
}
