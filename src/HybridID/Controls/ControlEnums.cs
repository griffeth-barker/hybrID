namespace HybridID.Controls;

/// <summary>InfoBar severity. Mirrors WinUI names; the spec's "Caution"/"Critical" colors map to Warning/Error.</summary>
public enum InfoBarSeverity
{
    Informational,
    Success,
    Warning,
    Error,
}

/// <summary>Color family of a <see cref="StatusPill"/> (and of <see cref="ResultField.ValueKind"/> icons).</summary>
public enum PillKind
{
    OnPrem,
    Hybrid,
    Cloud,
    Success,
    Caution,
    Critical,
    Neutral,
}

/// <summary>Where a <see cref="ManagementCard"/> says the object is managed.</summary>
public enum ManagementLocation
{
    OnPrem,
    Cloud,
    Neutral,
}

/// <summary>How an <see cref="OverlayDialog"/> was closed.</summary>
public enum OverlayDialogResult
{
    /// <summary>Esc, the Close button, or a programmatic <c>Hide(None)</c>.</summary>
    None,
    Primary,
    Secondary,
}

/// <summary>Which <see cref="OverlayDialog"/> button is the default (accent, activated by Enter).</summary>
public enum OverlayDialogButton
{
    None,
    Primary,
    Secondary,
    Close,
}

public enum ToastKind
{
    Success,
    Info,
    Error,
}
