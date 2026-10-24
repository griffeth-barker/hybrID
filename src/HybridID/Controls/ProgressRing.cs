using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace HybridID.Controls;

/// <summary>
/// Indeterminate spinner (Avalonia 12 has no ProgressRing). Theme: Themes/Controls/ProgressRing.axaml.
/// Sizes 16/20/32 via Width/Height; 32+ gets a 3 px stroke (<c>:large</c>). Animations run only
/// under <c>:active</c> = IsActive and IsVisible and attached to a visual tree, so hidden rings
/// cost no CPU. Pages should also set IsActive=false when they hide the ring's parent.
/// </summary>
[PseudoClasses(":active", ":large")]
public class ProgressRing : TemplatedControl
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<ProgressRing, bool>(nameof(IsActive), true);

    private bool _attached;

    public ProgressRing()
    {
        Focusable = false;
        AutomationProperties.SetName(this, "Loading");
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.ProgressBar);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        UpdateActive();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        UpdateActive();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty || change.Property == IsVisibleProperty)
        {
            UpdateActive();
        }
        else if (change.Property == WidthProperty || change.Property == HeightProperty)
        {
            var size = double.IsNaN(Width) ? Height : Width;
            PseudoClasses.Set(":large", !double.IsNaN(size) && size >= 32);
        }
    }

    private void UpdateActive() => PseudoClasses.Set(":active", IsActive && IsVisible && _attached);
}
