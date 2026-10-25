using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace HybridID.Controls;

/// <summary>Arguments of the OverlayDialog button events. Set <see cref="Cancel"/> to keep the dialog open.</summary>
public sealed class OverlayDialogButtonClickEventArgs : EventArgs
{
    public bool Cancel { get; set; }
}

/// <summary>
/// In-window ContentDialog (docs/design/ui-spec.md section 6.7). Theme:
/// Themes/Controls/OverlayDialog.axaml. Shown by <see cref="OverlayHost.ShowAsync"/> (pages use
/// <c>IAppShell.ShowDialogAsync</c>). Subclasses (<c>x:Class</c> dialogs with
/// <c>&lt;controls:OverlayDialog&gt;</c> as root) inherit the theme through
/// <see cref="StyleKeyOverride"/> and typically override the <c>On*ButtonClick</c> methods.
/// </summary>
[TemplatePart("PART_PrimaryButton", typeof(Button))]
[TemplatePart("PART_SecondaryButton", typeof(Button))]
[TemplatePart("PART_CloseButton", typeof(Button))]
[TemplatePart("PART_ButtonGrid", typeof(Grid))]
public class OverlayDialog : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<OverlayDialog, string?>(nameof(Title));

    public static readonly StyledProperty<object?> TitleIconProperty =
        AvaloniaProperty.Register<OverlayDialog, object?>(nameof(TitleIcon));

    public static readonly StyledProperty<string?> PrimaryButtonTextProperty =
        AvaloniaProperty.Register<OverlayDialog, string?>(nameof(PrimaryButtonText));

    public static readonly StyledProperty<string?> SecondaryButtonTextProperty =
        AvaloniaProperty.Register<OverlayDialog, string?>(nameof(SecondaryButtonText));

    public static readonly StyledProperty<string?> CloseButtonTextProperty =
        AvaloniaProperty.Register<OverlayDialog, string?>(nameof(CloseButtonText));

    public static readonly StyledProperty<bool> IsPrimaryButtonEnabledProperty =
        AvaloniaProperty.Register<OverlayDialog, bool>(nameof(IsPrimaryButtonEnabled), true);

    public static readonly StyledProperty<bool> IsSecondaryButtonEnabledProperty =
        AvaloniaProperty.Register<OverlayDialog, bool>(nameof(IsSecondaryButtonEnabled), true);

    public static readonly StyledProperty<OverlayDialogButton> DefaultButtonProperty =
        AvaloniaProperty.Register<OverlayDialog, OverlayDialogButton>(nameof(DefaultButton), OverlayDialogButton.Primary);

    public static readonly StyledProperty<object?> PrimaryButtonContentProperty =
        AvaloniaProperty.Register<OverlayDialog, object?>(nameof(PrimaryButtonContent));

    private Button? _primary;
    private Button? _secondary;
    private Button? _close;
    private Grid? _buttonGrid;

    protected override Type StyleKeyOverride => typeof(OverlayDialog);

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>Optional content (typically a 20 px PathIcon) shown left of the title.</summary>
    public object? TitleIcon { get => GetValue(TitleIconProperty); set => SetValue(TitleIconProperty, value); }

    /// <summary>Null/empty hides the button. Same for the other two button texts.</summary>
    public string? PrimaryButtonText { get => GetValue(PrimaryButtonTextProperty); set => SetValue(PrimaryButtonTextProperty, value); }

    public string? SecondaryButtonText { get => GetValue(SecondaryButtonTextProperty); set => SetValue(SecondaryButtonTextProperty, value); }

    /// <summary>The Close button is also the Esc (IsCancel) action and yields <see cref="OverlayDialogResult.None"/>.</summary>
    public string? CloseButtonText { get => GetValue(CloseButtonTextProperty); set => SetValue(CloseButtonTextProperty, value); }

    public bool IsPrimaryButtonEnabled { get => GetValue(IsPrimaryButtonEnabledProperty); set => SetValue(IsPrimaryButtonEnabledProperty, value); }

    public bool IsSecondaryButtonEnabled { get => GetValue(IsSecondaryButtonEnabledProperty); set => SetValue(IsSecondaryButtonEnabledProperty, value); }

    /// <summary>The default button gets the accent style and IsDefault (Enter).</summary>
    public OverlayDialogButton DefaultButton { get => GetValue(DefaultButtonProperty); set => SetValue(DefaultButtonProperty, value); }

    /// <summary>Overrides the primary button's content (e.g. "[ProgressRing] Signing in…"); null = PrimaryButtonText.</summary>
    public object? PrimaryButtonContent { get => GetValue(PrimaryButtonContentProperty); set => SetValue(PrimaryButtonContentProperty, value); }

    /// <summary>True between being presented by the host and being hidden.</summary>
    public bool IsOpen { get; internal set; }

    public event EventHandler<OverlayDialogButtonClickEventArgs>? PrimaryButtonClick;
    public event EventHandler<OverlayDialogButtonClickEventArgs>? SecondaryButtonClick;
    public event EventHandler<OverlayDialogButtonClickEventArgs>? CloseButtonClick;

    /// <summary>Raised after the dialog became visible and initial focus was set.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised after the dialog was hidden, with the final result.</summary>
    public event EventHandler<OverlayDialogResult>? Closed;

    /// <summary>Hooked by <see cref="OverlayHost"/>.</summary>
    internal event Action<OverlayDialog, OverlayDialogResult>? HideRequested;

    /// <summary>Closes the dialog (or removes it from the host queue if not shown yet).</summary>
    public void Hide(OverlayDialogResult result) => HideRequested?.Invoke(this, result);

    /// <summary>
    /// Element that receives focus when the dialog opens. Default: the first enabled TextBox in
    /// the content, else the default button, else the first focusable element.
    /// </summary>
    public virtual IInputElement? GetInitialFocusElement()
    {
        foreach (var descendant in EnumerateDescendants(this))
        {
            if (descendant is TextBox { IsEffectivelyEnabled: true, IsEffectivelyVisible: true } textBox)
            {
                return textBox;
            }
        }

        var defaultButton = DefaultButton switch
        {
            OverlayDialogButton.Primary => _primary,
            OverlayDialogButton.Secondary => _secondary,
            OverlayDialogButton.Close => _close,
            _ => null,
        };
        if (defaultButton is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true })
        {
            return defaultButton;
        }

        foreach (var descendant in EnumerateDescendants(this))
        {
            if (descendant is InputElement { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true } input)
            {
                return input;
            }
        }

        return null;
    }

    internal void RaiseOpened()
    {
        OnOpened();
        Opened?.Invoke(this, EventArgs.Empty);
    }

    internal void RaiseClosed(OverlayDialogResult result)
    {
        OnClosed(result);
        Closed?.Invoke(this, result);
    }

    protected virtual void OnOpened()
    {
    }

    /// <summary>Clear sensitive state here (e.g. password boxes).</summary>
    protected virtual void OnClosed(OverlayDialogResult result)
    {
    }

    protected virtual void OnPrimaryButtonClick(OverlayDialogButtonClickEventArgs e)
    {
    }

    protected virtual void OnSecondaryButtonClick(OverlayDialogButtonClickEventArgs e)
    {
    }

    protected virtual void OnCloseButtonClick(OverlayDialogButtonClickEventArgs e)
    {
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_primary is not null) _primary.Click -= OnPrimaryClick;
        if (_secondary is not null) _secondary.Click -= OnSecondaryClick;
        if (_close is not null) _close.Click -= OnCloseClick;

        _primary = e.NameScope.Find<Button>("PART_PrimaryButton");
        _secondary = e.NameScope.Find<Button>("PART_SecondaryButton");
        _close = e.NameScope.Find<Button>("PART_CloseButton");
        _buttonGrid = e.NameScope.Find<Grid>("PART_ButtonGrid");

        if (_primary is not null) _primary.Click += OnPrimaryClick;
        if (_secondary is not null) _secondary.Click += OnSecondaryClick;
        if (_close is not null) _close.Click += OnCloseClick;

        UpdateButtons();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PrimaryButtonTextProperty || change.Property == SecondaryButtonTextProperty ||
            change.Property == CloseButtonTextProperty || change.Property == DefaultButtonProperty ||
            change.Property == PrimaryButtonContentProperty || change.Property == IsPrimaryButtonEnabledProperty ||
            change.Property == IsSecondaryButtonEnabledProperty)
        {
            UpdateButtons();
        }
        else if (change.Property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
        }
    }

    private void UpdateButtons()
    {
        if (_primary is null || _secondary is null || _close is null || _buttonGrid is null)
        {
            return;
        }

        Configure(_primary, PrimaryButtonContent ?? PrimaryButtonText, PrimaryButtonText, IsPrimaryButtonEnabled,
            DefaultButton == OverlayDialogButton.Primary);
        Configure(_secondary, SecondaryButtonText, SecondaryButtonText, IsSecondaryButtonEnabled,
            DefaultButton == OverlayDialogButton.Secondary);
        Configure(_close, CloseButtonText, CloseButtonText, true, DefaultButton == OverlayDialogButton.Close);
        _close.IsCancel = !string.IsNullOrEmpty(CloseButtonText);

        // Equal-width buttons: one star column per visible button, 8 px gaps (spec: *,8,*,8,*).
        var visible = new List<Button>();
        foreach (var b in new[] { _primary, _secondary, _close })
        {
            if (b.IsVisible)
            {
                visible.Add(b);
            }
        }

        _buttonGrid.ColumnDefinitions.Clear();
        for (var i = 0; i < visible.Count; i++)
        {
            if (i > 0)
            {
                _buttonGrid.ColumnDefinitions.Add(new ColumnDefinition(8, GridUnitType.Pixel));
            }

            _buttonGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            Grid.SetColumn(visible[i], i * 2);
        }

        if (_buttonGrid.Parent is Control bar)
        {
            bar.IsVisible = visible.Count > 0;
        }
    }

    private static void Configure(Button button, object? content, string? text, bool enabled, bool isDefault)
    {
        var visible = !string.IsNullOrEmpty(text);
        button.IsVisible = visible;
        button.Content = content;
        button.IsEnabled = enabled;
        button.IsDefault = visible && isDefault;
        button.Classes.Set("accent", visible && isDefault);
        AutomationProperties.SetName(button, text);
    }

    private void OnPrimaryClick(object? sender, RoutedEventArgs e)
    {
        var args = new OverlayDialogButtonClickEventArgs();
        PrimaryButtonClick?.Invoke(this, args);
        OnPrimaryButtonClick(args);
        if (!args.Cancel)
        {
            Hide(OverlayDialogResult.Primary);
        }
    }

    private void OnSecondaryClick(object? sender, RoutedEventArgs e)
    {
        var args = new OverlayDialogButtonClickEventArgs();
        SecondaryButtonClick?.Invoke(this, args);
        OnSecondaryButtonClick(args);
        if (!args.Cancel)
        {
            Hide(OverlayDialogResult.Secondary);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        var args = new OverlayDialogButtonClickEventArgs();
        CloseButtonClick?.Invoke(this, args);
        OnCloseButtonClick(args);
        if (!args.Cancel)
        {
            Hide(OverlayDialogResult.None);
        }
    }

    private static IEnumerable<Visual> EnumerateDescendants(Visual root)
    {
        foreach (var child in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(root))
        {
            yield return child;
        }
    }
}
