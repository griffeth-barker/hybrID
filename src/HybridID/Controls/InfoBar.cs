using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace HybridID.Controls;

/// <summary>
/// WinUI-style InfoBar (docs/design/ui-spec.md section 6.2). Theme: Themes/Controls/InfoBar.axaml.
/// Severity colors come from pseudo-classes, never from brushes assigned in code.
/// </summary>
[TemplatePart("PART_Text", typeof(SelectableTextBlock))]
[TemplatePart("PART_CloseButton", typeof(Button))]
[PseudoClasses(":informational", ":success", ":warning", ":error", ":closable")]
public class InfoBar : TemplatedControl
{
    public static readonly StyledProperty<InfoBarSeverity> SeverityProperty =
        AvaloniaProperty.Register<InfoBar, InfoBarSeverity>(nameof(Severity));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<InfoBar, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<InfoBar, string?>(nameof(Message));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<InfoBar, bool>(nameof(IsOpen), true);

    public static readonly StyledProperty<bool> IsClosableProperty =
        AvaloniaProperty.Register<InfoBar, bool>(nameof(IsClosable), true);

    public static readonly StyledProperty<object?> ActionContentProperty =
        AvaloniaProperty.Register<InfoBar, object?>(nameof(ActionContent));

    public static readonly StyledProperty<bool> IsIconVisibleProperty =
        AvaloniaProperty.Register<InfoBar, bool>(nameof(IsIconVisible), true);

    private SelectableTextBlock? _text;
    private Button? _closeButton;

    public InfoBar()
    {
        UpdatePseudoClasses();
        UpdateAutomation();
    }

    public InfoBarSeverity Severity
    {
        get => GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Body text. "\n" renders as a line break, "\n\n" as a paragraph break.</summary>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>False hides the bar (IsVisible=false). Opening fades it in over 167 ms.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public bool IsClosable
    {
        get => GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    /// <summary>Optional action, typically a standard Button.</summary>
    public object? ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }

    public bool IsIconVisible
    {
        get => GetValue(IsIconVisibleProperty);
        set => SetValue(IsIconVisibleProperty, value);
    }

    /// <summary>Raised when the user closes the bar with its close button.</summary>
    public event EventHandler? Closed;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_closeButton is not null)
        {
            _closeButton.Click -= OnCloseClick;
        }

        _text = e.NameScope.Find<SelectableTextBlock>("PART_Text");
        _closeButton = e.NameScope.Find<Button>("PART_CloseButton");

        if (_closeButton is not null)
        {
            _closeButton.Click += OnCloseClick;
        }

        UpdateText();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SeverityProperty || change.Property == IsClosableProperty)
        {
            UpdatePseudoClasses();
            UpdateAutomation();
        }
        else if (change.Property == TitleProperty || change.Property == MessageProperty)
        {
            UpdateText();
            UpdateAutomation();
        }
        else if (change.Property == IsOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            IsVisible = open;
            if (open)
            {
                // Fade in: the theme defines a 167 ms DoubleTransition on Opacity.
                Opacity = 0;
                Dispatcher.UIThread.Post(() => Opacity = 1, DispatcherPriority.Render);
            }
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        IsVisible = IsOpen;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        IsOpen = false;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void UpdatePseudoClasses()
    {
        var s = Severity;
        PseudoClasses.Set(":informational", s == InfoBarSeverity.Informational);
        PseudoClasses.Set(":success", s == InfoBarSeverity.Success);
        PseudoClasses.Set(":warning", s == InfoBarSeverity.Warning);
        PseudoClasses.Set(":error", s == InfoBarSeverity.Error);
        PseudoClasses.Set(":closable", IsClosable);
    }

    private void UpdateText()
    {
        if (_text?.Inlines is not { } inlines)
        {
            return;
        }

        inlines.Clear();
        if (!string.IsNullOrEmpty(Title))
        {
            inlines.Add(new Run(Title) { FontWeight = FontWeight.SemiBold });
            if (!string.IsNullOrEmpty(Message))
            {
                inlines.Add(new Run("  "));
            }
        }

        if (!string.IsNullOrEmpty(Message))
        {
            var lines = Message.Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    inlines.Add(new LineBreak());
                }

                if (lines[i].Length > 0)
                {
                    inlines.Add(new Run(lines[i]));
                }
            }
        }
    }

    private void UpdateAutomation()
    {
        var word = Severity switch
        {
            InfoBarSeverity.Success => "Success",
            InfoBarSeverity.Warning => "Caution",
            InfoBarSeverity.Error => "Error",
            _ => "Information",
        };

        var name = new StringBuilder(word).Append(": ");
        if (!string.IsNullOrEmpty(Title))
        {
            name.Append(Title).Append(". ");
        }

        name.Append(Message);
        AutomationProperties.SetName(this, name.ToString().Trim());
        AutomationProperties.SetLiveSetting(this, Severity == InfoBarSeverity.Error
            ? AutomationLiveSetting.Assertive
            : AutomationLiveSetting.Polite);
    }
}
