using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace HybridID.Controls;

/// <summary>
/// CommunityToolkit-style settings row (docs/design/ui-spec.md section 6.12). Theme:
/// Themes/Controls/SettingsCard.axaml. <see cref="ContentControl.Content"/> is the right-side
/// control. Use <c>Classes="nested"</c> for rows inside an <c>Expander.settings</c> and
/// <c>Classes="header"</c> when the card is used as that expander's header.
/// For inputs that must stretch when the card wraps (&lt; 476 px), give the input the
/// <c>settingsInput</c> class instead of a local Width (a local Width would beat the style).
/// </summary>
[TemplatePart("PART_ActionIcon", typeof(PathIcon))]
[PseudoClasses(":clickable", ":wrapped", ":pressed")]
public class SettingsCard : ContentControl
{
    public const double WrapThreshold = 476;

    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<SettingsCard, string?>(nameof(Header));

    public static readonly StyledProperty<object?> DescriptionProperty =
        AvaloniaProperty.Register<SettingsCard, object?>(nameof(Description));

    public static readonly StyledProperty<Geometry?> HeaderIconProperty =
        AvaloniaProperty.Register<SettingsCard, Geometry?>(nameof(HeaderIcon));

    public static readonly StyledProperty<bool> IsClickEnabledProperty =
        AvaloniaProperty.Register<SettingsCard, bool>(nameof(IsClickEnabled));

    public static readonly StyledProperty<Geometry?> ActionIconProperty =
        AvaloniaProperty.Register<SettingsCard, Geometry?>(nameof(ActionIcon));

    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<SettingsCard, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    private PathIcon? _actionIcon;

    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>A string (rendered caption/secondary, wrapping) or rich content such as a status line.</summary>
    public object? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public Geometry? HeaderIcon
    {
        get => GetValue(HeaderIconProperty);
        set => SetValue(HeaderIconProperty, value);
    }

    /// <summary>Makes the whole card a button (focusable, hover states, Enter/Space, Click event).</summary>
    public bool IsClickEnabled
    {
        get => GetValue(IsClickEnabledProperty);
        set => SetValue(IsClickEnabledProperty, value);
    }

    /// <summary>Right-edge icon for clickable cards. Defaults to Icon.ChevronRight.</summary>
    public Geometry? ActionIcon
    {
        get => GetValue(ActionIconProperty);
        set => SetValue(ActionIconProperty, value);
    }

    public event System.EventHandler<RoutedEventArgs>? Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _actionIcon = e.NameScope.Find<PathIcon>("PART_ActionIcon");
        UpdateClickable();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsClickEnabledProperty || change.Property == ActionIconProperty ||
            change.Property == HeaderProperty)
        {
            UpdateClickable();
        }
        else if (change.Property == BoundsProperty)
        {
            var width = change.GetNewValue<Rect>().Width;
            PseudoClasses.Set(":wrapped", width > 0 && width < WrapThreshold);
        }
    }

    private void UpdateClickable()
    {
        var clickable = IsClickEnabled;
        PseudoClasses.Set(":clickable", clickable);
        Focusable = clickable;

        if (clickable)
        {
            AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Button);
            AutomationProperties.SetName(this, Header);
        }
        else
        {
            ClearValue(AutomationProperties.ControlTypeOverrideProperty);
        }

        if (_actionIcon is not null)
        {
            _actionIcon.IsVisible = clickable;
            // Geometry is theme-invariant, so a one-time resource lookup is safe here (unlike brushes).
            _actionIcon.Data = ActionIcon ??
                (this.TryFindResource("Icon.ChevronRight", out var geometry) ? geometry as Geometry : null);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsClickEnabled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            PseudoClasses.Set(":pressed", true);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!IsClickEnabled || !PseudoClasses.Contains(":pressed"))
        {
            return;
        }

        PseudoClasses.Set(":pressed", false);
        var point = e.GetPosition(this);
        if (new Rect(Bounds.Size).Contains(point))
        {
            RaiseEvent(new RoutedEventArgs(ClickEvent));
        }

        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        PseudoClasses.Set(":pressed", false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsClickEnabled && !e.Handled && (e.Key == Key.Enter || e.Key == Key.Space))
        {
            RaiseEvent(new RoutedEventArgs(ClickEvent));
            e.Handled = true;
        }
    }
}
