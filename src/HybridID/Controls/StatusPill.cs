using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace HybridID.Controls;

/// <summary>
/// Tinted status badge: icon + text, never color alone (docs/design/ui-spec.md section 6.4).
/// Theme: Themes/Controls/StatusPill.axaml. Add <c>Classes="small"</c> for the 20 px tag
/// variant used in list rows.
/// </summary>
[PseudoClasses(":onprem", ":hybrid", ":cloud", ":success", ":caution", ":critical", ":neutral", ":icon")]
public class StatusPill : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<StatusPill, string?>(nameof(Text));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<StatusPill, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<PillKind> KindProperty =
        AvaloniaProperty.Register<StatusPill, PillKind>(nameof(Kind), PillKind.Neutral);

    public StatusPill()
    {
        Focusable = false;
        UpdatePseudoClasses();
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public PillKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty || change.Property == IconProperty)
        {
            UpdatePseudoClasses();
        }
        else if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text);
        }
    }

    private void UpdatePseudoClasses()
    {
        SetKindPseudoClasses(PseudoClasses, Kind);
        PseudoClasses.Set(":icon", Icon is not null);
    }

    internal static void SetKindPseudoClasses(IPseudoClasses classes, PillKind? kind)
    {
        classes.Set(":onprem", kind == PillKind.OnPrem);
        classes.Set(":hybrid", kind == PillKind.Hybrid);
        classes.Set(":cloud", kind == PillKind.Cloud);
        classes.Set(":success", kind == PillKind.Success);
        classes.Set(":caution", kind == PillKind.Caution);
        classes.Set(":critical", kind == PillKind.Critical);
        classes.Set(":neutral", kind == PillKind.Neutral);
    }
}
