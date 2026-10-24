using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace HybridID.Controls;

/// <summary>
/// Label + copyable value row (docs/design/ui-spec.md section 6.5). Theme:
/// Themes/Controls/ResultField.axaml. The copy button copies by itself and raises the bubbling
/// <see cref="CopiedEvent"/> (same event as <see cref="CopyButton.CopiedEvent"/>) for the page to announce.
/// </summary>
[PseudoClasses(":mono", ":empty", ":valueicon", ":onprem", ":hybrid", ":cloud", ":success", ":caution", ":critical", ":neutral")]
public class ResultField : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ResultField, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ResultField, string?>(nameof(Value));

    public static readonly StyledProperty<bool> IsMonospaceProperty =
        AvaloniaProperty.Register<ResultField, bool>(nameof(IsMonospace));

    public static readonly StyledProperty<bool> CanCopyProperty =
        AvaloniaProperty.Register<ResultField, bool>(nameof(CanCopy), true);

    public static readonly StyledProperty<string?> CopyTextProperty =
        AvaloniaProperty.Register<ResultField, string?>(nameof(CopyText));

    public static readonly StyledProperty<Geometry?> ValueIconProperty =
        AvaloniaProperty.Register<ResultField, Geometry?>(nameof(ValueIcon));

    public static readonly StyledProperty<PillKind?> ValueKindProperty =
        AvaloniaProperty.Register<ResultField, PillKind?>(nameof(ValueKind));

    /// <summary>Text actually copied: <see cref="CopyText"/> or, if null, <see cref="Value"/>.</summary>
    public static readonly DirectProperty<ResultField, string?> EffectiveCopyTextProperty =
        AvaloniaProperty.RegisterDirect<ResultField, string?>(nameof(EffectiveCopyText), o => o.EffectiveCopyText);

    public static readonly RoutedEvent<CopiedEventArgs> CopiedEvent = CopyButton.CopiedEvent;

    private string? _effectiveCopyText;

    public ResultField()
    {
        UpdateState();
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool IsMonospace
    {
        get => GetValue(IsMonospaceProperty);
        set => SetValue(IsMonospaceProperty, value);
    }

    public bool CanCopy
    {
        get => GetValue(CanCopyProperty);
        set => SetValue(CanCopyProperty, value);
    }

    public string? CopyText
    {
        get => GetValue(CopyTextProperty);
        set => SetValue(CopyTextProperty, value);
    }

    /// <summary>Optional 12 px icon before the value (e.g. a status icon), colored by <see cref="ValueKind"/>.</summary>
    public Geometry? ValueIcon
    {
        get => GetValue(ValueIconProperty);
        set => SetValue(ValueIconProperty, value);
    }

    public PillKind? ValueKind
    {
        get => GetValue(ValueKindProperty);
        set => SetValue(ValueKindProperty, value);
    }

    public string? EffectiveCopyText
    {
        get => _effectiveCopyText;
        private set => SetAndRaise(EffectiveCopyTextProperty, ref _effectiveCopyText, value);
    }

    public event EventHandler<CopiedEventArgs>? Copied
    {
        add => AddHandler(CopiedEvent, value);
        remove => RemoveHandler(CopiedEvent, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == CopyTextProperty ||
            change.Property == IsMonospaceProperty || change.Property == ValueIconProperty ||
            change.Property == ValueKindProperty)
        {
            UpdateState();
        }
    }

    private void UpdateState()
    {
        PseudoClasses.Set(":mono", IsMonospace);
        PseudoClasses.Set(":empty", string.IsNullOrEmpty(Value));
        PseudoClasses.Set(":valueicon", ValueIcon is not null);
        StatusPill.SetKindPseudoClasses(PseudoClasses, ValueKind);
        EffectiveCopyText = CopyText ?? Value;
    }
}

/// <summary>One row of a <see cref="ResultListField"/>.</summary>
public sealed class ResultListItem
{
    public string Text { get; init; } = "";

    /// <summary>Text copied by the row's copy button; defaults to <see cref="Text"/>.</summary>
    public string? CopyText { get; init; }

    /// <summary>Optional tag pill ("Primary", "Alias", "X500", ...).</summary>
    public string? Tag { get; init; }

    public PillKind TagKind { get; init; } = PillKind.Neutral;

    /// <summary>Renders the text SemiBold (e.g. the primary SMTP address).</summary>
    public bool IsEmphasized { get; init; }

    public string EffectiveCopyText => CopyText ?? Text;
    public bool HasTag => !string.IsNullOrEmpty(Tag);
    public FontWeight TextWeight => IsEmphasized ? FontWeight.SemiBold : FontWeight.Normal;
}

/// <summary>
/// Labelled list of copyable rows with "Copy all" and "Show all" (docs/design/ui-spec.md
/// section 6.5). Theme: Themes/Controls/ResultField.axaml.
/// </summary>
[TemplatePart("PART_Items", typeof(ItemsControl))]
[TemplatePart("PART_CopyAllButton", typeof(Button))]
[TemplatePart("PART_ShowAllButton", typeof(Button))]
public class ResultListField : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ResultListField, string?>(nameof(Label));

    public static readonly StyledProperty<IEnumerable<ResultListItem>?> ItemsSourceProperty =
        AvaloniaProperty.Register<ResultListField, IEnumerable<ResultListItem>?>(nameof(ItemsSource));

    public static readonly StyledProperty<string?> CopyAllTextProperty =
        AvaloniaProperty.Register<ResultListField, string?>(nameof(CopyAllText));

    public static readonly StyledProperty<int> CollapsedCountProperty =
        AvaloniaProperty.Register<ResultListField, int>(nameof(CollapsedCount), 5);

    public static readonly StyledProperty<string?> CopyAllToolTipProperty =
        AvaloniaProperty.Register<ResultListField, string?>(nameof(CopyAllToolTip), "Copy all");

    public static readonly RoutedEvent<CopiedEventArgs> CopiedEvent = CopyButton.CopiedEvent;

    private ItemsControl? _items;
    private Button? _copyAll;
    private Button? _showAll;
    private bool _expanded;

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public IEnumerable<ResultListItem>? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Text for "Copy all" (hidden when null/empty). Proxy addresses: raw values with prefixes, one per line.</summary>
    public string? CopyAllText
    {
        get => GetValue(CopyAllTextProperty);
        set => SetValue(CopyAllTextProperty, value);
    }

    /// <summary>Rows shown before "Show all (n)" appears.</summary>
    public int CollapsedCount
    {
        get => GetValue(CollapsedCountProperty);
        set => SetValue(CollapsedCountProperty, value);
    }

    public string? CopyAllToolTip
    {
        get => GetValue(CopyAllToolTipProperty);
        set => SetValue(CopyAllToolTipProperty, value);
    }

    public event EventHandler<CopiedEventArgs>? Copied
    {
        add => AddHandler(CopiedEvent, value);
        remove => RemoveHandler(CopiedEvent, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_copyAll is not null) _copyAll.Click -= OnCopyAllClick;
        if (_showAll is not null) _showAll.Click -= OnShowAllClick;

        _items = e.NameScope.Find<ItemsControl>("PART_Items");
        _copyAll = e.NameScope.Find<Button>("PART_CopyAllButton");
        _showAll = e.NameScope.Find<Button>("PART_ShowAllButton");

        if (_copyAll is not null) _copyAll.Click += OnCopyAllClick;
        if (_showAll is not null) _showAll.Click += OnShowAllClick;

        UpdateItems();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            _expanded = false;
            UpdateItems();
        }
        else if (change.Property == CollapsedCountProperty || change.Property == CopyAllTextProperty)
        {
            UpdateItems();
        }
    }

    private void UpdateItems()
    {
        var all = ItemsSource?.ToList() ?? new List<ResultListItem>();
        var limit = Math.Max(1, CollapsedCount);
        var collapsed = !_expanded && all.Count > limit;

        if (_items is not null)
        {
            _items.ItemsSource = collapsed ? all.Take(limit).ToList() : all;
        }

        if (_showAll is not null)
        {
            _showAll.IsVisible = collapsed;
            _showAll.Content = $"Show all ({all.Count})";
        }

        if (_copyAll is not null)
        {
            _copyAll.IsVisible = !string.IsNullOrEmpty(CopyAllText);
        }
    }

    private void OnShowAllClick(object? sender, RoutedEventArgs e)
    {
        _expanded = true;
        UpdateItems();
    }

    private async void OnCopyAllClick(object? sender, RoutedEventArgs e)
    {
        var ok = await ClipboardHelper.TrySetTextAsync(this, CopyAllText);
        RaiseEvent(new CopiedEventArgs(CopiedEvent, Label, ok, isBulk: true));
    }
}
