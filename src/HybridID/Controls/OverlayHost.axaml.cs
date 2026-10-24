using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace HybridID.Controls;

/// <summary>
/// Smoke layer + one <see cref="OverlayDialog"/> at a time, FIFO queue, focus capture/restore
/// (docs/design/ui-spec.md section 6.7). Lives as the last child of MainWindow's root grid.
/// </summary>
public partial class OverlayHost : UserControl
{
    private static readonly TimeSpan EnterDuration = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(167);
    private static readonly Easing Decelerate = new SplineEasing(0.1, 0.9, 0.2, 1);
    private static readonly Easing Accelerate = new SplineEasing(0.7, 0, 1, 0.5);

    private readonly Queue<Entry> _queue = new();
    private Entry? _current;
    private IInputElement? _focusBeforeOpen;
    private bool _isClosing;

    public OverlayHost()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateMaxHeight();
    }

    /// <summary>True while a dialog is visible (including its exit animation).</summary>
    public bool IsOpen => _current is not null;

    public OverlayDialog? CurrentDialog => _current?.Dialog;

    /// <summary>Raised when <see cref="IsOpen"/> changes.</summary>
    public event EventHandler? IsOpenChanged;

    /// <summary>
    /// Called when the element focused before the dialog opened is gone; should focus a sensible
    /// default (e.g. the search box). Set by MainWindow.
    /// </summary>
    public Action? RestoreFocusFallback { get; set; }

    /// <summary>
    /// Shows <paramref name="dialog"/> (or queues it behind the current one). Completes when the
    /// dialog is hidden. Showing a dialog that is already visible/queued returns its pending task.
    /// </summary>
    public Task<OverlayDialogResult> ShowAsync(OverlayDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        if (_current?.Dialog == dialog)
        {
            return _current.Completion.Task;
        }

        var queued = _queue.FirstOrDefault(q => q.Dialog == dialog);
        if (queued is not null)
        {
            return queued.Completion.Task;
        }

        var entry = new Entry(dialog);
        dialog.HideRequested += OnHideRequested;
        if (_current is null)
        {
            Present(entry);
        }
        else
        {
            _queue.Enqueue(entry);
        }

        return entry.Completion.Task;
    }

    private void Present(Entry entry)
    {
        var wasOpen = IsOpen;
        _current = entry;
        var dialog = entry.Dialog;

        if (!wasOpen)
        {
            _focusBeforeOpen = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        }

        dialog.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        dialog.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        dialog.Opacity = 0;
        dialog.RenderTransform = TransformOperations.Parse("scale(1.05)");
        dialog.IsHitTestVisible = true;
        dialog.Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = EnterDuration, Easing = Decelerate },
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = EnterDuration, Easing = Decelerate },
        };
        KeyboardNavigation.SetTabNavigation(dialog, KeyboardNavigationMode.Cycle);

        DialogLayer.Children.Add(dialog);
        UpdateMaxHeight();
        IsVisible = true;
        dialog.IsOpen = true;

        if (!wasOpen)
        {
            IsOpenChanged?.Invoke(this, EventArgs.Empty);
        }

        // Let the dialog get templated and laid out, then animate in and set initial focus.
        Dispatcher.UIThread.Post(() =>
        {
            if (_current != entry)
            {
                return;
            }

            Smoke.Opacity = 1;
            dialog.Opacity = 1;
            dialog.RenderTransform = TransformOperations.Parse("scale(1)");
            dialog.GetInitialFocusElement()?.Focus(NavigationMethod.Tab);
            dialog.RaiseOpened();
        }, DispatcherPriority.Loaded);
    }

    private void OnHideRequested(OverlayDialog dialog, OverlayDialogResult result)
    {
        if (_current?.Dialog == dialog)
        {
            _ = CloseCurrentAsync(result);
            return;
        }

        // Hidden before it was ever shown (e.g. a device-code dialog auto-closed while queued).
        var remaining = _queue.Where(q => q.Dialog != dialog).ToList();
        var removed = _queue.FirstOrDefault(q => q.Dialog == dialog);
        if (removed is null)
        {
            return;
        }

        _queue.Clear();
        foreach (var q in remaining)
        {
            _queue.Enqueue(q);
        }

        dialog.HideRequested -= OnHideRequested;
        dialog.RaiseClosed(result);
        removed.Completion.TrySetResult(result);
    }

    private async Task CloseCurrentAsync(OverlayDialogResult result)
    {
        if (_current is not { } entry || _isClosing)
        {
            return;
        }

        _isClosing = true;
        var dialog = entry.Dialog;
        dialog.IsHitTestVisible = false;
        dialog.Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = ExitDuration, Easing = Accelerate },
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = ExitDuration, Easing = Accelerate },
        };
        dialog.Opacity = 0;
        dialog.RenderTransform = TransformOperations.Parse("scale(1.05)");

        var hasNext = _queue.Count > 0;
        if (!hasNext)
        {
            Smoke.Opacity = 0;
        }

        await Task.Delay(ExitDuration);

        DialogLayer.Children.Remove(dialog);
        dialog.IsOpen = false;
        dialog.HideRequested -= OnHideRequested;
        _current = null;
        _isClosing = false;

        dialog.RaiseClosed(result);
        entry.Completion.TrySetResult(result);

        if (_queue.Count > 0)
        {
            Present(_queue.Dequeue());
            return;
        }

        IsVisible = false;
        IsOpenChanged?.Invoke(this, EventArgs.Empty);
        RestoreFocus();
    }

    private void RestoreFocus()
    {
        var target = _focusBeforeOpen;
        _focusBeforeOpen = null;

        if (target is Visual visual && visual.IsAttachedToVisualTree() &&
            target is InputElement { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true } input &&
            input.Focus())
        {
            return;
        }

        RestoreFocusFallback?.Invoke();
    }

    private void UpdateMaxHeight()
    {
        var height = Bounds.Height;
        if (height <= 0)
        {
            return;
        }

        foreach (var child in DialogLayer.Children.OfType<OverlayDialog>())
        {
            child.MaxHeight = Math.Max(160, height - 96);
        }
    }

    private sealed class Entry
    {
        public Entry(OverlayDialog dialog) => Dialog = dialog;

        public OverlayDialog Dialog { get; }

        public TaskCompletionSource<OverlayDialogResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
