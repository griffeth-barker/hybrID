using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using HybridID.Services;

namespace HybridID.Controls;

/// <summary>
/// The verdict card: names the system that manages the identity/mailbox and carries the button
/// that opens it (docs/design/ui-spec.md section 6.6). Theme: Themes/Controls/ManagementCard.axaml.
/// Opening (<see cref="UrlLauncher"/>) and "Copy link" are handled internally; copy raises the
/// bubbling <see cref="LinkCopiedEvent"/> for the page to toast "Link copied".
/// </summary>
[TemplatePart("PART_PrimaryButton", typeof(Button))]
[TemplatePart("PART_CopyLinkButton", typeof(Button))]
[TemplatePart("PART_ViewLinkButton", typeof(Button))]
[PseudoClasses(":onprem", ":cloud", ":neutral", ":primary", ":viewlink", ":warning", ":tag")]
public class ManagementCard : TemplatedControl
{
    public static readonly StyledProperty<string?> OverlineProperty =
        AvaloniaProperty.Register<ManagementCard, string?>(nameof(Overline));

    public static readonly StyledProperty<string?> LocationTitleProperty =
        AvaloniaProperty.Register<ManagementCard, string?>(nameof(LocationTitle));

    public static readonly StyledProperty<ManagementLocation> LocationProperty =
        AvaloniaProperty.Register<ManagementCard, ManagementLocation>(nameof(Location));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ManagementCard, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> TagTextProperty =
        AvaloniaProperty.Register<ManagementCard, string?>(nameof(TagText));

    public static readonly StyledProperty<string?> BodyProperty =
        AvaloniaProperty.Register<ManagementCard, string?>(nameof(Body));

    public static readonly StyledProperty<string?> WarningTextProperty =
        AvaloniaProperty.Register<ManagementCard, string?>(nameof(WarningText));

    public static readonly StyledProperty<object?> WarningActionContentProperty =
        AvaloniaProperty.Register<ManagementCard, object?>(nameof(WarningActionContent));

    public static readonly StyledProperty<string?> PrimaryTextProperty =
        AvaloniaProperty.Register<ManagementCard, string?>(nameof(PrimaryText));

    public static readonly StyledProperty<string?> PrimaryUrlProperty =
        AvaloniaProperty.Register<ManagementCard, string?>(nameof(PrimaryUrl));

    public static readonly StyledProperty<object?> SecondaryContentProperty =
        AvaloniaProperty.Register<ManagementCard, object?>(nameof(SecondaryContent));

    public static readonly StyledProperty<string?> ViewLinkTextProperty =
        AvaloniaProperty.Register<ManagementCard, string?>(nameof(ViewLinkText));

    public static readonly StyledProperty<string?> ViewLinkUrlProperty =
        AvaloniaProperty.Register<ManagementCard, string?>(nameof(ViewLinkUrl));

    public static readonly RoutedEvent<CopiedEventArgs> LinkCopiedEvent =
        RoutedEvent.Register<ManagementCard, CopiedEventArgs>("LinkCopied", RoutingStrategies.Bubble);

    private Button? _primary;
    private Button? _copyLink;
    private Button? _viewLink;

    public ManagementCard()
    {
        UpdateState();
    }

    public string? Overline { get => GetValue(OverlineProperty); set => SetValue(OverlineProperty, value); }
    public string? LocationTitle { get => GetValue(LocationTitleProperty); set => SetValue(LocationTitleProperty, value); }
    public ManagementLocation Location { get => GetValue(LocationProperty); set => SetValue(LocationProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>Rendered as a Neutral StatusPill next to the title (e.g. "Remote mailbox").</summary>
    public string? TagText { get => GetValue(TagTextProperty); set => SetValue(TagTextProperty, value); }

    public string? Body { get => GetValue(BodyProperty); set => SetValue(BodyProperty, value); }

    /// <summary>Optional caution line under the body.</summary>
    public string? WarningText { get => GetValue(WarningTextProperty); set => SetValue(WarningTextProperty, value); }

    /// <summary>Optional content after <see cref="WarningText"/> (e.g. a "Settings" HyperlinkButton).</summary>
    public object? WarningActionContent { get => GetValue(WarningActionContentProperty); set => SetValue(WarningActionContentProperty, value); }

    public string? PrimaryText { get => GetValue(PrimaryTextProperty); set => SetValue(PrimaryTextProperty, value); }

    /// <summary>Primary button and "Copy link" are shown only when this is non-null.</summary>
    public string? PrimaryUrl { get => GetValue(PrimaryUrlProperty); set => SetValue(PrimaryUrlProperty, value); }

    /// <summary>Extra action(s) after the primary/copy buttons (e.g. "Copy DN").</summary>
    public object? SecondaryContent { get => GetValue(SecondaryContentProperty); set => SetValue(SecondaryContentProperty, value); }

    public string? ViewLinkText { get => GetValue(ViewLinkTextProperty); set => SetValue(ViewLinkTextProperty, value); }
    public string? ViewLinkUrl { get => GetValue(ViewLinkUrlProperty); set => SetValue(ViewLinkUrlProperty, value); }

    public event EventHandler<CopiedEventArgs>? LinkCopied
    {
        add => AddHandler(LinkCopiedEvent, value);
        remove => RemoveHandler(LinkCopiedEvent, value);
    }

    /// <summary>Clicks the primary button if there is one (Ctrl+1 / Ctrl+2). Returns false when absent.</summary>
    public bool ActivatePrimary()
    {
        if (string.IsNullOrEmpty(PrimaryUrl))
        {
            return false;
        }

        OpenUrl(PrimaryUrl);
        return true;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_primary is not null) _primary.Click -= OnPrimaryClick;
        if (_copyLink is not null) _copyLink.Click -= OnCopyLinkClick;
        if (_viewLink is not null) _viewLink.Click -= OnViewLinkClick;

        _primary = e.NameScope.Find<Button>("PART_PrimaryButton");
        _copyLink = e.NameScope.Find<Button>("PART_CopyLinkButton");
        _viewLink = e.NameScope.Find<Button>("PART_ViewLinkButton");

        if (_primary is not null) _primary.Click += OnPrimaryClick;
        if (_copyLink is not null) _copyLink.Click += OnCopyLinkClick;
        if (_viewLink is not null) _viewLink.Click += OnViewLinkClick;

        UpdateState();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LocationProperty || change.Property == PrimaryUrlProperty ||
            change.Property == PrimaryTextProperty || change.Property == OverlineProperty ||
            change.Property == LocationTitleProperty || change.Property == ViewLinkUrlProperty ||
            change.Property == ViewLinkTextProperty || change.Property == WarningTextProperty ||
            change.Property == TagTextProperty)
        {
            UpdateState();
        }
    }

    private void UpdateState()
    {
        PseudoClasses.Set(":onprem", Location == ManagementLocation.OnPrem);
        PseudoClasses.Set(":cloud", Location == ManagementLocation.Cloud);
        PseudoClasses.Set(":neutral", Location == ManagementLocation.Neutral);
        PseudoClasses.Set(":primary", !string.IsNullOrEmpty(PrimaryUrl) && !string.IsNullOrEmpty(PrimaryText));
        PseudoClasses.Set(":viewlink", !string.IsNullOrEmpty(ViewLinkUrl) && !string.IsNullOrEmpty(ViewLinkText));
        PseudoClasses.Set(":warning", !string.IsNullOrEmpty(WarningText));
        PseudoClasses.Set(":tag", !string.IsNullOrEmpty(TagText));

        AutomationProperties.SetName(this, $"{Overline} {LocationTitle}".Trim());

        if (_primary is not null)
        {
            AutomationProperties.SetName(_primary, $"{PrimaryText} (opens browser)");
            ToolTip.SetTip(_primary, string.IsNullOrEmpty(PrimaryUrl)
                ? null
                : new TextBlock
                {
                    Text = PrimaryUrl,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 480,
                    Classes = { "caption", "mono" },
                });
        }

        if (_copyLink is not null)
        {
            var name = $"Copy {Overline?.ToLowerInvariant()} link".Replace("  ", " ");
            AutomationProperties.SetName(_copyLink, name);
            ToolTip.SetTip(_copyLink, "Copy link");
        }

        if (_viewLink is not null)
        {
            AutomationProperties.SetName(_viewLink, $"{ViewLinkText} (opens browser)");
            ToolTip.SetTip(_viewLink, ViewLinkUrl);
        }
    }

    private void OnPrimaryClick(object? sender, RoutedEventArgs e) => OpenUrl(PrimaryUrl);

    private void OnViewLinkClick(object? sender, RoutedEventArgs e) => OpenUrl(ViewLinkUrl);

    private async void OnCopyLinkClick(object? sender, RoutedEventArgs e)
    {
        var ok = await ClipboardHelper.TrySetTextAsync(this, PrimaryUrl);
        RaiseEvent(new CopiedEventArgs(LinkCopiedEvent, Overline, ok, isBulk: true));
    }

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        try
        {
            UrlLauncher.Open(url);
        }
        catch (Exception)
        {
            // No default browser / blocked by policy: nothing useful to do from a control;
            // "Copy link" remains available.
        }
    }
}
