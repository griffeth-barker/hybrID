# hybrID UI/UX Design Specification (v1, targets hybrID 0.3.0)

Status: **Approved for implementation**. Audience: the Avalonia engineer building the new views.
Stack: .NET 10, Avalonia **12.1.3**, `Avalonia.Themes.Fluent` 12.1.3, `Avalonia.Fonts.Inter`. No third-party UI libraries. No MVVM framework.

This spec is decisive on purpose. Where it says "do X", do X. Anything marked **[M1]** / **[M2]** is a flagged model/service addition (see §11). Anything marked **FUTURE** is a roadmap slot: build the layout hook, do not fake data.

Sources studied: every file under `src/HybridID/` (all `Views/Legacy/*`, `Services/*`, `Models/*`, `Themes/*`, `App.axaml(.cs)`, `Program.cs`, `config.default.json`, `HybridID.csproj`), `docs/roadmap.md`, `docs/changelog.md`, `docs/getting-started.md`, `docs/contributing.md`, `README.md`, `CLAUDE.md`. Avalonia 12.1.3 API surface was verified against the reference assemblies' XML docs in the local NuGet cache (see §12 "Verified Avalonia 12 facts").

---

## Table of contents

1. Design principles & rationale
2. Information architecture (windows, pages, dialogs, file/class list)
3. Design tokens (color, type, spacing, radii, elevation, strokes, motion)
4. Window chrome (size, title bar, backdrop, per-OS behavior)
5. Screen-by-screen specs (Search page states, Settings page, About page, dialogs)
6. Component specs
7. Interaction & motion (shortcuts, focus, loading, feedback, errors)
8. Roadmap accommodation
9. Icons
10. Accessibility
11. Model/service additions (flagged)
12. Implementation notes, pitfalls & phased checklist
13. Appendix: legacy → new feature mapping, copy catalogue

---

## 1. Design principles & rationale

1. **Verdict first.** The single question a technician asks is "where do I manage this?". The Search result leads with two big *Management cards* (Identity, Mailbox) that name the system and carry the button that opens it. Everything else (attributes, proxy addresses, licenses) is secondary and below the fold if necessary.
2. **Keyboard-first speed.** Focus lands in the search box on launch, Enter searches, focus stays in the search box with text selected after results so the next identity can be typed immediately. Every primary action has a shortcut (§7.1).
3. **Honest about coverage.** Hybrid routing is only correct if both directories were actually searched. Every result shows a *coverage line* ("Active Directory: match · Entra ID: match") and a Caution InfoBar whenever a directory was skipped, unreachable, or possibly unauthenticated. A synced user must never silently look "Cloud-only" because AD was down.
4. **Native Windows 11 look, respectful elsewhere.** Mica backdrop, extended title bar, left navigation rail, layered content frame, card-based settings, InfoBars, Segoe UI Variable on Windows. On macOS: traffic lights + vibrancy, Inter. On Linux: native decorations, solid background, Inter.
5. **Rebuild WinUI patterns from Avalonia primitives.** No FluentAvalonia. Rationale: the app needs ~8 small composite controls (InfoBar, SettingsCard, nav item, overlay dialog, progress ring, pill, result field, management card), each < 150 lines. A third-party dependency would own our chrome/theming and lag Avalonia 12's changed chrome APIs (`WindowDecorations`, `ElementRole`), which FluentAvalonia's `AppWindow` targets differently. Owning them keeps theming in one `Theme.axaml`.
6. **Tokens, not colors.** Views never contain hex values or assign brushes from code. Code-behind toggles `Classes`/pseudo-classes; styles resolve `DynamicResource` tokens. This is what makes live theme switching work (and fixes a latent legacy bug where badge brushes assigned via `FindResource` froze on theme change).
7. **One window.** Settings, About and all prompts live inside the main window (pages + in-window overlay dialogs). No child OS windows, no z-order or owner problems across three platforms.

---

## 2. Information architecture

### 2.1 Structure

```
MainWindow (the only OS window)
├── AppTitleBar                 (custom, extended into client area on Windows/macOS)
├── SplitView (nav rail)
│   ├── Pane
│   │   ├── top items:    Search
│   │   └── footer items: About, Settings
│   └── Content: content frame → TransitioningContentControl (page host)
│         ├── SearchPage   (default, cached instance)
│         ├── SettingsPage (cached instance)
│         └── AboutPage    (cached instance)
├── ToastHost                   (bottom-center transient messages)
├── OverlayHost                 (smoke layer + one OverlayDialog at a time)
│     ├── AdCredentialDialog
│     ├── DeviceCodeDialog
│     └── MessageDialog
└── Announcer                   (0×0 live-region TextBlock for screen readers)
```

Decisions:
- **Settings and About become pages** in the main window's nav (Win11 pattern). Settings sits in the nav footer (bottom), About directly above it. Settings also ends with an "About hybrID" card that navigates to the About page (Win11 Settings > System > About pattern).
- **All modal flows are in-window overlay dialogs** (ContentDialog pattern with a smoke layer). The legacy `AlertDialog` "Settings saved" confirmation is **removed** and replaced by an inline Success InfoBar on the Settings page (Win11 does not confirm saves modally).
- **Pages are created once and cached** by `MainWindow`, so navigating Search → Settings → Search keeps the last result on screen.
- **No status bar.** Legacy status-bar messages are redistributed: connection state → title-bar status chips; progress → search progress bar + ring; outcomes → coverage line, InfoBars, announcer (§13.1).

### 2.2 Exact file / class list

All paths relative to `src/HybridID/`. Namespaces follow folders.

| File | Class / kind | Namespace | Notes |
|---|---|---|---|
| `Views/MainWindow.axaml(.cs)` | `MainWindow : Window, IAppShell` | `HybridID.Views` | Keeps `App.axaml.cs` (`desktop.MainWindow = new MainWindow();`) unchanged. Owns `AppSession`, pages, OverlayHost, ToastHost, global shortcuts, backdrop/chrome setup. |
| `Views/IAppShell.cs` | `interface IAppShell` | `HybridID.Views` | What pages may ask of the shell (§2.3). |
| `Views/Pages/SearchPage.axaml(.cs)` | `SearchPage : UserControl` | `HybridID.Views.Pages` | Search box, all result states. |
| `Views/Pages/SettingsPage.axaml(.cs)` | `SettingsPage : UserControl` | `HybridID.Views.Pages` | All settings; explicit Save bar. |
| `Views/Pages/AboutPage.axaml(.cs)` | `AboutPage : UserControl` | `HybridID.Views.Pages` | Version, links, notices, system info. |
| `Views/Dialogs/AdCredentialDialog.axaml(.cs)` | `AdCredentialDialog : OverlayDialog` | `HybridID.Views.Dialogs` | Root element `<controls:OverlayDialog x:Class=...>`. |
| `Views/Dialogs/DeviceCodeDialog.axaml(.cs)` | `DeviceCodeDialog : OverlayDialog` | `HybridID.Views.Dialogs` | |
| `Views/Dialogs/MessageDialog.axaml(.cs)` | `MessageDialog : OverlayDialog` | `HybridID.Views.Dialogs` | Replaces `AlertDialog`; also used for confirm prompts. |
| `Controls/AppTitleBar.axaml(.cs)` | `AppTitleBar : UserControl` | `HybridID.Controls` | Pane toggle, icon, title, status chips, caption-button reserve. |
| `Controls/InfoBar.cs` | `InfoBar : TemplatedControl` | `HybridID.Controls` | Theme in `Themes/Controls/InfoBar.axaml`. |
| `Controls/SettingsCard.cs` | `SettingsCard : ContentControl` | `HybridID.Controls` | Theme in `Themes/Controls/SettingsCard.axaml`. |
| `Controls/StatusPill.cs` | `StatusPill : TemplatedControl` | `HybridID.Controls` | Theme in `Themes/Controls/StatusPill.axaml`. |
| `Controls/ResultField.cs` | `ResultField : TemplatedControl` | `HybridID.Controls` | Theme in `Themes/Controls/ResultField.axaml`. |
| `Controls/ResultListField.cs` | `ResultListField : TemplatedControl` | `HybridID.Controls` | Same theme file as ResultField. |
| `Controls/ManagementCard.cs` | `ManagementCard : TemplatedControl` | `HybridID.Controls` | Theme in `Themes/Controls/ManagementCard.axaml`. |
| `Controls/OverlayDialog.cs` | `OverlayDialog : ContentControl` | `HybridID.Controls` | Theme in `Themes/Controls/OverlayDialog.axaml`. `StyleKeyOverride => typeof(OverlayDialog)`. |
| `Controls/OverlayHost.axaml(.cs)` | `OverlayHost : UserControl` | `HybridID.Controls` | Smoke + queue + focus trap. |
| `Controls/ToastHost.axaml(.cs)` | `ToastHost : UserControl` | `HybridID.Controls` | |
| `Controls/ProgressRing.cs` | `ProgressRing : TemplatedControl` | `HybridID.Controls` | Theme in `Themes/Controls/ProgressRing.axaml`. Avalonia 12 has no ProgressRing. |
| `Controls/CopyButton.cs` | `CopyButton : Button` | `HybridID.Controls` | Copy icon ↔ check swap. `StyleKeyOverride => typeof(Button)`; styled via `Button.copy`. |
| `Themes/Theme.axaml` | ResourceDictionary | — | **Rewritten**: color tokens (§3.1), radii, shadows, font families. |
| `Themes/Typography.axaml` | `Styles` | — | New: type ramp classes (§3.2). |
| `Themes/AppStyles.axaml` | `Styles` | — | New: button variants, TextBox, ComboBox, Expander (`settings`), ListBox (`candidates`), nav item, chips, flyout, tooltip overrides. Replaces the inline `<Application.Styles>` block in `App.axaml`. |
| `Themes/Controls/*.axaml` | ResourceDictionary of `ControlTheme`s | — | One per templated control. Merged by `Themes/Controls.axaml`. |
| `Themes/Controls.axaml` | ResourceDictionary | — | Merges all `Themes/Controls/*.axaml`. |
| `Themes/Icons.axaml` | ResourceDictionary | — | Existing; **must be merged in App.axaml** (currently it is not). Add icons in §9. |
| `Services/AppSession.cs` | `AppSession` (+ enums `AdConnectionState`, `GraphConnectionState`) | `HybridID.Services` | **New, additive**. Owns config + services + LDAP connection + Graph client + connection state (moved out of legacy MainWindow code-behind). No UI types. |
| `Services/ResultSummaryFormatter.cs` | `static class ResultSummaryFormatter` | `HybridID.Services` | **New, additive**. Pure function → plain-text ticket summary (§7.5). |
| `Services/DisplayFormat.cs` | `static class DisplayFormat` | `HybridID.Services` | **New, additive**. Date + relative-age formatting, proxy-address parsing (§5.1.9). Pure functions. |

`App.axaml` final shape:

```xml
<Application ... RequestedThemeVariant="Default">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceInclude Source="avares://HybridID/Themes/Theme.axaml"/>
        <ResourceInclude Source="avares://HybridID/Themes/Icons.axaml"/>
        <ResourceInclude Source="avares://HybridID/Themes/Controls.axaml"/>
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
  <Application.Styles>
    <FluentTheme />
    <StyleInclude Source="avares://HybridID/Themes/Typography.axaml"/>
    <StyleInclude Source="avares://HybridID/Themes/AppStyles.axaml"/>
  </Application.Styles>
  <!-- macOS application menu, see §4.5 -->
</Application>
```

`Views/Legacy/*` stays compiled until Phase 7 and is then deleted (§12.3). New code must never reference legacy views.

### 2.3 `IAppShell` (the only page→shell contract)

```csharp
public interface IAppShell
{
    AppSession Session { get; }
    void Navigate(AppPage page);                       // enum AppPage { Search, Settings, About }
    Task<OverlayDialogResult> ShowDialogAsync(OverlayDialog dialog);
    void ShowToast(string message, ToastKind kind = ToastKind.Success); // Success | Info | Error
    void Announce(string text);                        // screen-reader live region
    Task CopyTextAsync(string text);                   // TopLevel.Clipboard.SetTextAsync
    bool IsOverlayOpen { get; }
}
```
`OverlayDialogResult` enum: `None` (Esc/Close button), `Primary`, `Secondary`. Pages receive `IAppShell` via constructor (`new SearchPage(this)`); a parameterless constructor must also exist for the XAML previewer.

### 2.4 `AppSession` (new service, additive)

Moves the orchestration state that lived in `LegacyMainWindow`/`LegacySettingsWindow` code-behind into one plain class so Search and Settings pages share it. Members:

```csharp
public sealed class AppSession
{
    public ConfigService ConfigService { get; }
    public AppConfig Config { get; private set; }
    public LdapIdentityService Ldap { get; }
    public IdentityLookupService Lookup { get; private set; }
    public GraphAuthService GraphAuth { get; private set; }

    public LdapConnection? LdapConnection { get; }
    public string? LdapBaseDn { get; }
    public string? LdapBoundUser { get; }              // for the AD chip flyout
    public AdConnectionState AdState { get; }          // NotConfigured, NotConnected, Connecting, Connected, Skipped, Failed
    public string? AdLastError { get; }
    public GraphConnectionState GraphState { get; }    // Unverified, WaitingForSignIn, SignedIn, Failed
    public DeviceCodeInfo? PendingDeviceCode { get; }  // non-null while WaitingForSignIn
    public event EventHandler? StateChanged;           // raised on the UI thread

    public bool IsAdConfigured { get; }                // !IsNullOrWhiteSpace(Settings.LdapServer)
    public bool IsExchangeFqdnDefault { get; }         // Settings.OnPremExchangeFqdn is "" or "mail.domain.tld"

    // Legacy LegacyMainWindow.EnsureLdapConnectionAsync body, minus the dialog. Returns null on success, else message.
    public Task<string?> ConnectLdapAsync(string username, string password);
    public void SkipLdapForSession();                  // AdState = Skipped; no more prompts until Reconnect/Settings save
    public void DisconnectLdap();                      // dispose, AdState = NotConnected
    public GraphServiceClient EnsureGraphClient(Func<DeviceCodeInfo, CancellationToken, Task> deviceCodeCallback);
    public void MarkGraphSignedIn(); public void MarkGraphWaiting(DeviceCodeInfo info); public void MarkGraphFailed(string message);
    public void ApplySavedConfig(bool connectionSettingsChanged); // legacy OnSettingsSaved: dispose LDAP, null Graph client, rebuild services — only when connection settings changed
}
```
Test-connection helpers (throwaway connections for Settings, legacy `TestLdapConnectionAsync` / `TestGraphConnectionAsync` bodies) also move here as `TestLdapAsync(AppSettings form, user, pwd)` → `(bool ok, string message)` and `TestGraphAsync(tenantId, clientId, callback)` → `(bool ok, string message)`.

---

## 3. Design tokens

All tokens live in `Themes/Theme.axaml`. Colors are declared as `Color` **and** `SolidColorBrush` (`...Brush` suffix) inside `ThemeDictionaries` `Light` and `Dark`; every key must exist in both. Views use `{DynamicResource XxxBrush}` only.

### 3.1 Color

#### 3.1.1 Accent (brand #007ACC)

Theme-invariant values, but declare them in **both** theme dictionaries (Fluent reads them per variant):

| Key | Value | Use |
|---|---|---|
| `SystemAccentColor` | `#007ACC` | Brand. Flows into Fluent controls (ToggleSwitch, CheckBox, ProgressBar, focus underline). |
| `SystemAccentColorLight1` | `#0096FA` | Fluent derived states |
| `SystemAccentColorLight2` | `#61CAFF` | |
| `SystemAccentColorLight3` | `#99E7FF` | |
| `SystemAccentColorDark1` | `#0061B0` | |
| `SystemAccentColorDark2` | `#00428C` | |
| `SystemAccentColorDark3` | `#001E63` | |
| `AccentFillColorDefault` | `#007ACC` | Accent button rest, nav selection pill, links' underline hover |
| `AccentFillColorSecondary` | `#0070BB` | Accent button pointer-over (white text 5.2:1) |
| `AccentFillColorTertiary` | `#0063A6` | Accent button pressed (white text 6.3:1) |
| `TextOnAccentFillColorPrimary` | `#FFFFFF` | Text on accent (4.52:1 on #007ACC — passes AA) |
| `TextOnAccentFillColorSecondary` | `#E6FFFFFF` | Pressed text |

Theme-dependent accent tokens:

| Key | Light | Dark |
|---|---|---|
| `AccentFillColorDisabled` | `#37000000` | `#28FFFFFF` |
| `TextOnAccentFillColorDisabled` | `#FFFFFF` | `#87FFFFFF` |
| `AccentTextFillColorPrimary` (links, accent text) | `#00428C` | `#99E7FF` |
| `AccentTextFillColorSecondary` (link pressed) | `#001E63` | `#99E7FF` |
| `AccentTextFillColorTertiary` (link hover) | `#0061B0` | `#61CAFF` |

Map Fluent's own keys to these (in both theme dictionaries): `AccentButtonBackground`=AccentFillColorDefaultBrush, `AccentButtonBackgroundPointerOver`=Secondary, `AccentButtonBackgroundPressed`=Tertiary, `AccentButtonBackgroundDisabled`=AccentFillColorDisabledBrush, `AccentButtonForeground`/`PointerOver`=TextOnAccentFillColorPrimaryBrush, `AccentButtonForegroundPressed`=TextOnAccentFillColorSecondaryBrush, `AccentButtonForegroundDisabled`=TextOnAccentFillColorDisabledBrush, `AccentButtonBorderBrush*`=Transparent. `HyperlinkButtonForeground`=AccentTextFillColorPrimaryBrush, `HyperlinkButtonForegroundPointerOver`=AccentTextFillColorTertiaryBrush, `HyperlinkButtonForegroundPressed`=AccentTextFillColorSecondaryBrush, `HyperlinkButtonForegroundVisited*` = same as unvisited (we don't track visited).

#### 3.1.2 Text

| Key | Light | Dark |
|---|---|---|
| `TextFillColorPrimary` | `#E4000000` | `#FFFFFF` |
| `TextFillColorSecondary` | `#9E000000` | `#C5FFFFFF` |
| `TextFillColorTertiary` | `#72000000` | `#87FFFFFF` |
| `TextFillColorDisabled` | `#5C000000` | `#5DFFFFFF` |

Tertiary is for placeholders/"—" empty values only (fails 4.5:1 for body text by design, matches WinUI).

#### 3.1.3 Backgrounds / layers

| Key | Light | Dark | Use |
|---|---|---|---|
| `SolidBackgroundFillColorBase` | `#F3F3F3` | `#202020` | `TransparencyBackgroundFallback` of MainWindow; OverlayDialog button bar |
| `SolidBackgroundFillColorSecondary` | `#EEEEEE` | `#1C1C1C` | |
| `SolidBackgroundFillColorTertiary` | `#F9F9F9` | `#282828` | Opaque content frame on macOS vibrancy (§4.3) |
| `SolidBackgroundFillColorQuarternary` | `#FFFFFF` | `#2C2C2C` | Flyout/tooltip/toast/ComboBox dropdown background (popups get no backdrop) |
| `LayerFillColorDefault` | `#80FFFFFF` | `#4C3A3A3A` | Content frame (right of nav) |
| `LayerFillColorAlt` | `#FFFFFF` | `#0DFFFFFF` | OverlayDialog top (content) area |
| `CardBackgroundFillColorDefault` | `#B3FFFFFF` | `#0DFFFFFF` | Cards, SettingsCard rest, InfoBar Informational base |
| `CardBackgroundFillColorSecondary` | `#80F6F6F6` | `#08FFFFFF` | SettingsExpander content area, code box in DeviceCodeDialog |
| `ControlFillColorDefault` | `#B3FFFFFF` | `#0FFFFFFF` | Standard buttons/inputs (Fluent) |
| `ControlFillColorSecondary` | `#80F9F9F9` | `#15FFFFFF` | Clickable SettingsCard pointer-over |
| `ControlFillColorTertiary` | `#4DF9F9F9` | `#08FFFFFF` | Clickable SettingsCard pressed |
| `ControlFillColorDisabled` | `#4DF9F9F9` | `#0BFFFFFF` | |
| `ControlFillColorInputActive` | `#FFFFFF` | `#B31E1E1E` | Focused TextBox (`TextControlBackgroundFocused`) |
| `SubtleFillColorTransparent` | `#00FFFFFF` | `#00FFFFFF` | Subtle button rest |
| `SubtleFillColorSecondary` | `#09000000` | `#0FFFFFFF` | Subtle hover, nav item hover & selected, ResultField row hover |
| `SubtleFillColorTertiary` | `#06000000` | `#0AFFFFFF` | Subtle pressed, nav selected+hover |
| `SmokeFillColorDefault` | `#4D000000` | `#4D000000` | Overlay smoke layer |

#### 3.1.4 Strokes

| Key | Light | Dark | Use |
|---|---|---|---|
| `CardStrokeColorDefault` | `#0F000000` | `#19000000` | Card/InfoBar/content-frame border |
| `CardStrokeColorDefaultSolid` | `#EBEBEB` | `#1C1C1C` | Borders on opaque surfaces |
| `ControlStrokeColorDefault` | `#0F000000` | `#12FFFFFF` | Standard control border |
| `ControlStrokeColorSecondary` | `#29000000` | `#18FFFFFF` | Bottom edge of controls |
| `ControlStrongStrokeColorDefault` | `#72000000` | `#8BFFFFFF` | Hollow status dot ring, unchecked toggle |
| `DividerStrokeColorDefault` | `#14000000` | `#15FFFFFF` | Row dividers inside cards |
| `SurfaceStrokeColorDefault` | `#66757575` | `#66757575` | Flyout/dialog/toast outline |
| `FocusStrokeColorOuter` | `#E4000000` | `#FFFFFF` | Focus ring outer 2 px |
| `FocusStrokeColorInner` | `#B3FFFFFF` | `#B3000000` | Focus ring inner 1 px |

Map Fluent's focus resources: `SystemControlFocusVisualPrimaryBrush`=FocusStrokeColorOuterBrush, `SystemControlFocusVisualSecondaryBrush`=FocusStrokeColorInnerBrush, `SystemControlFocusVisualPrimaryThickness`=2, `SystemControlFocusVisualSecondaryThickness`=1.

#### 3.1.5 Semantic (system) colors

| Key | Light | Dark | Use |
|---|---|---|---|
| `SystemFillColorSuccess` | `#0F7B0F` | `#6CCB5F` | Success icons, connected dot, "Enabled" pill fg |
| `SystemFillColorCaution` | `#9D5D00` | `#FCE100` | Caution icons, skipped dot, caution pill fg |
| `SystemFillColorCritical` | `#C42B1C` | `#FF99A4` | Error icons, failed dot, "Locked out" pill fg, invalid input border |
| `SystemFillColorAttention` | `#0061B0` | `#61CAFF` | Informational icon, waiting dot |
| `SystemFillColorNeutral` | `#72000000` | `#8BFFFFFF` | Neutral pill fg ("Disabled") |
| `SystemFillColorSuccessBackground` | `#DFF6DD` | `#393D1B` | Success InfoBar/pill background |
| `SystemFillColorCautionBackground` | `#FFF4CE` | `#433519` | Caution InfoBar/pill background |
| `SystemFillColorCriticalBackground` | `#FDE7E9` | `#442726` | Critical InfoBar/pill background |
| `SystemFillColorAttentionBackground` | `#80F6F6F6` | `#08FFFFFF` | Informational InfoBar background |
| `SystemFillColorNeutralBackground` | `#06000000` | `#08FFFFFF` | Neutral pill background |

Semantic foreground colors are used for **icons, dots and pill text only**; InfoBar body text is always `TextFillColorPrimary`.

#### 3.1.6 Management-location colors (app-specific)

Tinted "badge" appearance (colored text/icon on a light tint), never solid fills with white text. All text pairs ≥ 5.9:1.

| Key | Light | Dark | Meaning |
|---|---|---|---|
| `LocationOnPremForeground` | `#00666D` | `#5FD4DA` | Active Directory / Exchange Server (on-prem) |
| `LocationOnPremBackground` | `#E6F4F5` | `#0F3A3D` | |
| `LocationHybridForeground` | `#6B3FA0` | `#C7A6F5` | Hybrid (synced) |
| `LocationHybridBackground` | `#F2EDFA` | `#34224F` | |
| `LocationCloudForeground` | `#3B4FC4` | `#A9B8FF` | Entra ID / Exchange Online |
| `LocationCloudBackground` | `#EBEFFC` | `#1F2A5C` | |

Cloud uses cornflower (hue ≈ 230°), deliberately distinct from the #007ACC accent (hue 204°) so location pills never read as buttons.

#### 3.1.7 Legacy key mapping

Keep the old keys in a clearly marked `<!-- LEGACY: delete with Views/Legacy (Phase 7) -->` block until Phase 7. New views use only the new keys.

| Legacy key | Replacement |
|---|---|
| `WindowBg` | `SolidBackgroundFillColorBaseBrush` (via TransparencyBackgroundFallback) |
| `PanelBg` | `CardBackgroundFillColorDefaultBrush` |
| `InputBg` | removed — Fluent `TextControlBackground*` (mapped to `ControlFillColorDefault` / `ControlFillColorInputActive`) |
| `AppBorderBrush` | `CardStrokeColorDefaultBrush` (cards) / `DividerStrokeColorDefaultBrush` (dividers) |
| `PrimaryText` | `TextFillColorPrimaryBrush` |
| `SecondaryText` | `TextFillColorSecondaryBrush` |
| `NotesText` | removed — notes render in an Informational InfoBar with primary text |
| `LinkText` | `AccentTextFillColorPrimaryBrush` |
| `StatusBarBg`, `StatusReadyBrush`, `StatusInfoBrush` | removed (no status bar) |
| `StatusSuccessBrush` | `SystemFillColorSuccessBrush` |
| `StatusErrorBrush` | `SystemFillColorCriticalBrush` |
| `AccentBrush` | `AccentFillColorDefaultBrush` |
| `MutedBrush` | removed — secondary buttons are standard Fluent buttons |
| `SyncOnPremBrush` / `SyncHybridBrush` / `SyncCloudOnlyBrush` | `LocationOnPrem*` / `LocationHybrid*` / `LocationCloud*` |
| `AccountEnabledBrush` / `AccountDisabledBrush` / `AccountLockedBrush` | `SystemFillColorSuccess*` / `SystemFillColorNeutral*` / `SystemFillColorCritical*` |
| `WarningBrush` | `SystemFillColorCautionBrush` |

### 3.2 Typography

Font family resources (in `Theme.axaml`, theme-invariant):

| Key | Value |
|---|---|
| `ContentControlThemeFontFamily` (Fluent override) | `Segoe UI Variable Text, Segoe UI, fonts:Inter#Inter` |
| `FontFamilyText` | same as above |
| `FontFamilyDisplay` | `Segoe UI Variable Display, Segoe UI, fonts:Inter#Inter` |
| `FontFamilySmall` | `Segoe UI Variable Small, Segoe UI, fonts:Inter#Inter` |
| `FontFamilyMono` | `avares://HybridID/Assets/Fonts#Cascadia Mono` |

Effect: Windows 11 → Segoe UI Variable; Windows 10 → Segoe UI; macOS/Linux → bundled Inter (predictable metrics; do not try SF Pro). Keep `.WithInterFont()` in `Program.cs`.

Type ramp (styles in `Themes/Typography.axaml`; selectors use `:is(TextBlock)` so `SelectableTextBlock` matches too):

| Class | Size | Line height | Weight | Family | Use |
|---|---|---|---|---|---|
| `caption` | 12 | 16 | Regular | Small | Hints, field labels, descriptions, chip text |
| `body` (default) | 14 | 20 | Regular | Text | Everything else |
| `bodyStrong` | 14 | 20 | SemiBold | Text | Card titles, section headers, InfoBar titles |
| `bodyLarge` | 18 | 24 | Regular | Display | Empty-state lead text |
| `subtitle` | 20 | 28 | SemiBold | Display | Management-card location name, dialog titles |
| `title` | 28 | 36 | SemiBold | Display | Page titles, result object name |
| `titleLarge` | 40 | 52 | SemiBold | Display | Not used in v1 (reserved) |
| `display` | 68 | 92 | SemiBold | Display | Not used |

Modifier classes (combine): `secondary` → TextFillColorSecondaryBrush; `tertiary` → Tertiary; `accent` → AccentTextFillColorPrimaryBrush; `mono` → FontFamilyMono, size 13, line height 20 (GUIDs, DNs, URLs, device codes, config path); `caps` is **not** used (Fluent does not uppercase).

Also set `ControlContentThemeFontSize` = 14 and `ToolTipContentThemeFontSize` = 12.

### 3.3 Spacing (4 px grid)

| Token | px | Typical use |
|---|---|---|
| `xxs` | 2 | Label→value gap in ResultField |
| `xs` | 4 | Gap between SettingsCards in a group; nav item vertical margin (2 top + 2 bottom) |
| `s` | 8 | Icon→text gap, button spacing, InfoBar stack gap |
| `m` | 12 | Gap between result cards (both axes) |
| `l` | 16 | Card padding, gap header→first card |
| `xl` | 20 | ManagementCard padding |
| `xxl` | 24 | Page top padding, dialog padding |
| `xxxl` | 32 | Page horizontal padding |
| `section` | 28 | Space above a settings section header |

Declare as resources where reused: `PagePadding` = `Thickness 32,24,32,32`, `CardPadding` = `16`, `ManagementCardPadding` = `20`, `DialogPadding` = `24`. Literal values elsewhere must be multiples of 4 (exception: 2 and 1-px strokes).

Content max widths: Search page content `MaxWidth=1200`; Settings and About `MaxWidth=1000`; both `HorizontalAlignment=Center` inside a stretched ScrollViewer.

### 3.4 Corner radii

| Key | Value | Use |
|---|---|---|
| `ControlCornerRadius` (Fluent override) | `4` | Buttons, TextBox, ComboBox, nav items, InfoBar, chips' focus ring |
| `OverlayCornerRadius` (Fluent override) | `8` | Flyouts, dialogs, toasts, tooltips (tooltips: 4 per WinUI — override `ToolTip` style to 4) |
| `CardCornerRadius` | `8` | Cards, SettingsCards, ManagementCards, content frame (top-left only: `8,0,0,0`) |
| `PillCornerRadius` | `12` | StatusPill (height 24 → fully rounded), title-bar chips (height 28 → use 14) |
| Nav selection pill | `2` | 3×16 indicator |

### 3.5 Elevation / shadows

Win11 cards have **no shadow** (stroke only). Shadows only on floating surfaces. Declare as `BoxShadows` resources per theme:

| Key | Light | Dark | Use |
|---|---|---|---|
| `ShadowFlyout` | `0 0 2 0 #1F000000, 0 8 16 0 #24000000` | `0 0 2 0 #3D000000, 0 8 16 0 #47000000` | Flyouts, toast, Settings save bar |
| `ShadowDialog` | `0 0 8 0 #1F000000, 0 32 64 0 #24000000` | `0 0 8 0 #3D000000, 0 32 64 0 #47000000` | OverlayDialog |

If `BoxShadows` does not parse as a keyed XAML resource, set `BoxShadow` literally in the ControlTheme/style per theme via `ThemeVariantScope`-independent styles using `:is(Control)[...]` — but try the resource first (`BoxShadows.Parse` exists).

### 3.6 Stroke widths

1 px for all card/control/divider strokes; content frame `1,1,0,0`; focus ring 2 px outer + 1 px inner; TextBox focus bottom 2 px accent (Fluent default); nav pill 3 px wide; hollow status dot ring 1.5 px.

### 3.7 Motion

| Token | Duration | Easing (Avalonia `Easing` string) | Use |
|---|---|---|---|
| `Faster` | 83 ms | linear | Hover/pressed color changes (`BrushTransition`) |
| `Fast` | 167 ms | `0,0,0,1` | Smoke fade, toast fade, InfoBar open, nav pill scale |
| `Normal` | 250 ms | `0.1,0.9,0.2,1` (decelerate) | Page entrance, dialog entrance, results entrance, save bar slide |
| `Exit` | 167 ms | `0.7,0,1,0.5` (accelerate) | Dialog exit, toast exit |

Use literal `Duration="0:0:0.167"` etc. in transitions (TransitionBase durations don't need to be resources). If the 4-number easing string fails to parse, use `<SplineEasing X1="0.1" Y1="0.9" X2="0.2" Y2="1"/>`. Motion is decorative only; nothing waits on an animation to complete before input is accepted.

---

## 4. Window chrome

### 4.1 Size

- Default `Width=1120 Height=780`, `WindowStartupLocation=CenterScreen`. At default size on Windows the pane is expanded (240) and search content is 816 px wide → two-column details.
- `MinWidth=640 MinHeight=560`.
- Size/position are **not** persisted in v1 (would need a new setting; FUTURE optional).
- `Title="hybrID"`, `Icon="avares://HybridID/Assets/hybrid-icon.png"`.

### 4.2 Title bar (`Controls/AppTitleBar`)

Height **48** on Windows/macOS (WinUI "tall" title bar used with NavigationView); **40** on Linux (it's an in-window app bar under the native title bar).

Windows layout (left → right):

```
┌──────────────────────────────────────────────────────────────────────────────────────────────┐
│8│[≡]│8│[ico]│12│hybrID│  ← drag area (ElementRole=TitleBar) →  │(● Active Directory)│8│(○ Entra ID)│8│ ─  □  ✕ │
│ │40×36│ │16×16│  │caption│                                       │ chip h28           │ │ chip h28   │ │ 138 reserve│
└──────────────────────────────────────────────────────────────────────────────────────────────┘
height 48, all items vertically centered
```

- Root `Grid` columns: `8, 40, 8, 16, 12, Auto, *, Auto, 8, Auto, 8, {CaptionReserve}`. `CaptionReserve` = 138 on Windows (3 × 46 caption buttons), 0 elsewhere.
- Root element has `WindowDecorationProperties.ElementRole="TitleBar"` (makes the whole bar draggable, double-click maximizes, system menu on Alt+Space/right-click handled by OS).
- **Every interactive child** (pane toggle, chips) sets `WindowDecorationProperties.ElementRole="User"`; otherwise clicks fall through to the drag role.
- Pane toggle: `Button.subtle`, 40×36, `PathIcon` `Icon.Navigation` 16×16. Tooltip/automation name "Collapse navigation" / "Expand navigation" (updates with state).
- App icon: `Image` 16×16 of `hybrid-icon.png`, `RenderOptions.BitmapInterpolationMode=HighQuality`.
- Title: `TextBlock.caption` "hybrID", `TextFillColorPrimaryBrush`. When `Window.IsActive=false`: title and icon switch to `TextFillColorTertiaryBrush` / Opacity 0.6 via class `inactive` on the title bar (toggle from `Activated`/`Deactivated`).
- Status chips: see §6.9. Below window width 900: chips show dot + short label ("AD", "Entra").
- `MainWindow`: `ExtendClientAreaToDecorationsHint=True`, `ExtendClientAreaTitleBarHeightHint=48`, `WindowDecorations=Full`.

macOS layout:

```
│ ● ● ●  │[≡]│ ← drag area →                          │(● Active Directory)│8│(○ Entra ID)│12│
│ 80 inset│40 │                                         │                    │ │            │  │
```
- Left inset **80 px** reserved for traffic lights (replace the `8, …` leading columns with `80`). No app icon and no title text (macOS convention with a sidebar; the window title is still set for Mission Control/Window menu). In `WindowState=FullScreen` the traffic lights hide → inset becomes 8.
- Same `ElementRole` rules; `ExtendClientAreaToDecorationsHint=True`, `ExtendClientAreaTitleBarHeightHint=48`. Verify traffic lights are vertically centered in the 48 px bar; if Avalonia keeps them at the default 28 px position, set the hint to `-1` (OS default) and use a 48 px app-drawn bar anyway (traffic lights then sit top-aligned, which is acceptable and matches Xcode-style toolbars).

Linux layout:
- `ExtendClientAreaToDecorationsHint=False` (native WM decorations; Avalonia-drawn decorations vary by WM/compositor and are not worth the risk).
- AppTitleBar renders as a 40 px app bar: `[8][≡ 40×32][*][chips][12]`. No icon/title (native title bar shows them), no `ElementRole`.

### 4.3 Backdrop

Set in `MainWindow` constructor (code-behind):

| OS | `TransparencyLevelHint` | Result |
|---|---|---|
| Windows 11 (build ≥ 22000) | `[Mica, None]` | Mica |
| Windows 10 | `[Mica, None]` | Mica unsupported → None → solid fallback. Do **not** use AcrylicBlur on Win10 (resize lag). |
| macOS | `[AcrylicBlur, None]` | NSVisualEffect vibrancy |
| Linux | `[None]` | Solid |

- `Background="Transparent"` on MainWindow (override Fluent's `SystemRegionBrush`), `TransparencyBackgroundFallback="{DynamicResource SolidBackgroundFillColorBaseBrush}"`.
- Pane area: transparent (backdrop shows through, exactly like WinUI NavigationView Left mode).
- Content frame: `LayerFillColorDefaultBrush`, stroke `CardStrokeColorDefaultBrush` thickness `1,1,0,0`, `CornerRadius 8,0,0,0`.
- macOS only: when `ActualTransparencyLevel == AcrylicBlur`, add class `opaque` to the content frame → background `SolidBackgroundFillColorTertiaryBrush` (desktop colors bleeding through a 50% layer under dense data look noisy on macOS; the sidebar keeps vibrancy, like Finder/Mail).
- If `PlatformSettings.GetColorValues().ContrastPreference == High`, force `TransparencyLevelHint=[None]`.
- Popups (ComboBox dropdown, Flyout, ToolTip, MenuFlyout) are separate top-levels and get no backdrop: set `FlyoutPresenterBackground`/`ToolTipBackground`/`ComboBoxDropDownBackground` to `SolidBackgroundFillColorQuarternaryBrush`, border `SurfaceStrokeColorDefaultBrush`, radius 8 (tooltip 4), shadow `ShadowFlyout`.

### 4.4 Navigation rail (in MainWindow)

- `SplitView DisplayMode="CompactInline" CompactPaneLength="48" OpenPaneLength="240" PaneBackground="Transparent" IsPaneOpen=...`.
- Pane content: `DockPanel`; footer `StackPanel` docked bottom (margin `0,0,0,4`) with About, Settings; top `StackPanel` (margin `0,4,0,0`) with Search.
- Items are `RadioButton GroupName="NavRail" Theme="{StaticResource NavRailItemTheme}"` (§6.3). RadioButton gives mutual exclusion across top/footer groups for free.
- Auto-open rule: window width ≥ 1008 → open; < 1008 → closed (compact). Evaluate on `SizeChanged` only when crossing the 1008 threshold, so a manual toggle sticks until the next crossing. Not persisted.
- Items: Search (`Icon.Search`, "Search"), About (`Icon.Info20`, "About"), Settings (`Icon.Settings`, "Settings"). In compact mode, labels are clipped and each item shows its label as ToolTip (`ToolTip.Tip` set only when pane closed).
- `AutomationProperties.LandmarkType="Navigation"`, `AutomationProperties.Name="Main navigation"` on the pane root.

### 4.5 macOS native menu

Add `NativeMenu.Menu` on `App` (application menu) with: "About hybrID" (navigates to About), separator, "Settings…" `Gesture="Cmd+OemComma"` (navigates to Settings). Avalonia merges these into the app menu. Windows/Linux: no menu bar.

### 4.6 Page host

- `TransitioningContentControl Name="PageHost"`, `PageTransition` = `CrossFade` 167 ms (`FadeInEasing` decelerate). Additionally each page root runs an entrance on attach: `Opacity 0→1` and `TranslateTransform.Y 16→0`, 250 ms decelerate (implemented by setting initial values in `OnAttachedToVisualTree` then posting the final values with `Transitions` defined on the page root).
- Navigating to the already-current page is a no-op. Leaving SettingsPage with unsaved changes goes through the prompt in §5.2.4.

### 4.7 Full-window wireframe (Windows, 1120×780, pane open)

```
x=0                 240                                                                          1120
┌──────────────────────────────────────────────────────────────────────────────────────────────┐ y=0
│ [≡] ◧ hybrID                                   (● Active Directory) (○ Entra ID)    ─   □   ✕ │ 48  AppTitleBar (Mica)
├───────────────────┬──────────────────────────────────────────────────────────────────────────┤ 48
│ ▌🔍 Search        │╭─────────────────────────────────────────────────────────────────────────┤  content frame
│   (36 h, 4 margin)││  PAGE (LayerFillColorDefault, 8px top-left radius, 1px stroke top/left) │
│                   ││                                                                         │
│                   ││                                                                         │
│                   ││                                                                         │
│                   ││                                                                         │
│                   ││                                                                         │
│   ⓘ About         ││                                                                         │
│   ⚙ Settings      ││                                                    ┌──────────────────┐ │
│                   ││                                                    │ ✓ Summary copied │ │ ToastHost (bottom-center, 16 from bottom)
└───────────────────┴┴────────────────────────────────────────────────────┴──────────────────┴─┘ 780
  pane transparent (Mica)
```
OverlayHost covers the whole window (including the title bar) when a dialog is open.

---

## 5. Screen-by-screen specs

### 5.1 SearchPage

#### 5.1.1 Skeleton

```
SearchPage (Grid rows: Auto header, Auto progress, * body;  Container.Name="SearchContent" Container.Sizing="Width" on body)
├── Header (Padding 32,24,32,16; MaxWidth 1200 centered; LandmarkType=Search)
│   ├── Row: [TxtSearch (*)] 8 [BtnLocate (Auto, MinWidth 112)]
│   └── HintLine (caption, margin-top 8)
├── SearchProgress: ProgressBar IsIndeterminate, Height 3, MinHeight 3, full frame width, IsVisible only while searching, CornerRadius 0
└── Body: ScrollViewer (Padding 32,0,32,32) → StackPanel MaxWidth 1200 centered, Spacing 16
    ├── InfoBarStack (StackPanel Spacing 8)       ← page-level InfoBars (§5.1.6)
    ├── EmptyState        (IsVisible in idle)
    ├── SearchingState    (IsVisible while searching)
    ├── NoResultState     (IsVisible when not found)
    ├── CandidatesState   (IsVisible when ambiguous)
    └── ResultState       (IsVisible when a single object resolved)
```
Exactly one of the five states is visible at any time. Code-behind switches them via a single `SetState(SearchViewState)` method (enum `Idle, Searching, NoResult, Candidates, Result`).

#### 5.1.2 Search header

```
┌──────────────────────────────────────────────────────────────────────────────┐ ┌────────────┐
│ 🔍  Search by sAMAccountName, UPN, or ObjectGUID                          ✕  │ │   Locate   │  h=40
└──────────────────────────────────────────────────────────────────────────────┘ └────────────┘
Examples: jsmith · jsmith@contoso.com · 3f2504e0-4f89-11d3-9a0c-0305e82c3301. Press Enter to search.
```
- `TextBox Name="TxtSearch"`: `Height=40`, `VerticalContentAlignment=Center`, `PlaceholderText="Search by sAMAccountName, UPN, or ObjectGUID"`, `MaxLength=256`, `AutomationProperties.Name="Identity to search"`, `AutomationProperties.HelpText` = hint line text.
  - `InnerLeftContent`: `PathIcon Icon.Search16` 16×16, margin `12,0,4,0`, `TextFillColorSecondaryBrush`.
  - `InnerRightContent`: clear button `Button.subtle.icon` 32×32 with `Icon.Dismiss12`, margin `0,0,4,0`, visible only when text non-empty, `AutomationProperties.Name="Clear search"`, tooltip "Clear (Esc)", `Focusable=False` (Esc covers keyboard use).
  - Invalid state: class `invalid` → `BorderBrush=SystemFillColorCriticalBrush` (rest and focus).
- `Button Name="BtnLocate" Classes="accent"`: `Height=40`, `MinWidth=112`, content "Locate". Tooltip "Locate (Enter)". While searching: `IsEnabled=False`, content becomes `[ProgressRing 16] 8 "Locating…"`.
- Hint line `TextBlock.caption.secondary`: text above. Validation replaces it with `[Icon.Error 12, critical] 4 "Enter an identity to search."` in `SystemFillColorCriticalBrush` until the next keystroke.
- Input normalization before search (view-level, display-only): trim; take first line of multi-line paste; strip surrounding quotes and `<…>`; strip `mailto:`; strip a `DOMAIN\` prefix (then the hint line reads "Searching for 'jsmith' (domain prefix removed)."). Put the normalized value back into the box.

#### 5.1.3 Idle / empty state (and first run)

```
                               ┌────┐
                               │icon│  64×64 hybrid-icon.png
                               └────┘
                     Find where an identity is managed                     (title, centered)
      Search Active Directory and Entra ID at once, then jump to the right admin portal.   (bodyLarge secondary, centered, MaxWidth 560)

  ┌──────────────────────┐  ┌──────────────────────────────┐  ┌────────────────────────────────────────┐
  │ 👤 sAMAccountName     │  │ ✉ User principal name        │  │ # ObjectGUID                            │  format cards (UniformGrid 3 cols ≥ 760, 1 col below)
  │ jsmith (mono)         │  │ jsmith@contoso.com (mono)    │  │ 3f2504e0-4f89-11d3-9a0c-0305e82c3301    │
  └──────────────────────┘  └──────────────────────────────┘  └────────────────────────────────────────┘

  ⌨ Keyboard shortcuts                                                              (bodyStrong, margin-top 28)
  Ctrl+L  Focus search     Enter  Locate      Esc  Clear      Ctrl+Shift+C  Copy summary
  Ctrl+1  Open identity portal     Ctrl+2  Open mailbox portal     F5  Search again     Ctrl+,  Settings
```
- Vertically: top margin 48 from InfoBarStack. Format cards: card style (§6.1 "Card"), padding 16, non-interactive (not buttons), icon 16 `Icon.Person`/`Icon.Mail`/`Icon.NumberSymbol16`, caption label, mono example.
- Shortcut list: `WrapPanel`, each item `[Border keycap: radius 4, stroke ControlStrokeColorDefault, bg ControlFillColorDefault, padding 6,2, caption mono] 8 [caption secondary label]`, item spacing 24×8. On macOS render "⌘L", "⌘⇧C", "⌘1", "⌘,"; F5 stays F5.
- **First-run / setup needed** (condition: `!Session.IsAdConfigured || Session.IsExchangeFqdnDefault`): Caution InfoBar at top of InfoBarStack:
  - Title "Finish setting up hybrID"; message (AD missing): "Add your domain controller so on-premises Active Directory is searched. Until then, only Entra ID is searched."; message (FQDN default only): "Set your on-premises Exchange server so Exchange admin center links work."; both missing: "Add your domain controller and Exchange server in Settings. Until then, only Entra ID is searched."
  - Action button "Open settings" → `Navigate(Settings)`. `IsClosable=True`; closing hides it for this session only.

#### 5.1.4 Searching state

Flow executed by `SearchPage.RunSearchAsync(identity)` (legacy logic, re-homed):
1. Validate/normalize. Clear previous result/candidates, hide result InfoBars (keep setup InfoBar). `SetState(Searching)`. Show progress bar. Disable Locate.
2. **AD phase** (only if `IsAdConfigured` and `AdState` is `NotConnected` or `Failed`): phase text "Connecting to Active Directory…"; show `AdCredentialDialog` (purpose = Search). Result: Connected → continue; Skip → `Session.SkipLdapForSession()`. If `AdState == Skipped` or `NotConfigured`: no prompt.
3. **Graph client**: `Session.EnsureGraphClient(OnDeviceCodeReady)`. Exception → Critical InfoBar (§5.1.6 N), `SetState(Idle)`, stop.
4. **Lookup phase**: phase text "Searching Active Directory and Microsoft Graph…" (or "Searching Microsoft Graph…" if AD not in play). If the device-code callback fires: phase text "Waiting for Microsoft sign-in…" and `DeviceCodeDialog` opens (non-blocking — the search awaits the lookup, not the dialog).
5. On completion: close DeviceCodeDialog if open; `MarkGraphSignedIn()` if result contains Graph data; render state; announce; re-enable Locate; focus `TxtSearch` and `SelectAll()`.

```
                                    ◠  ProgressRing 32
                    Searching for 'jsmith'…                          (subtitle, centered, margin-top 64)
                    Searching Active Directory and Microsoft Graph…  (body secondary = phase text; AutomationProperties.LiveSetting=Polite)
```
- Esc during search: does nothing (the lookup is not cancellable — §12 risk R3). Enter/Locate are ignored (legacy `_isSearching` guard).

#### 5.1.5 Result state (single object)

Layout (content width ≥ 760 shown; container query `SearchContent` `min-width:760` → details grid 2 columns; `min-width:600` → management cards side-by-side; below 600 everything stacks):

```
┌ ResultHeader (no card chrome) ─────────────────────────────────────────────────────────────────┐
│ (◯ 👤)  John Smith                                                   [⧉ Copy summary]  [↻]      │  avatar 48, name = title (28), trims w/ tooltip
│  48     User · jsmith@contoso.com                                                             │  body secondary: ObjectKind · UPN[M1] (else ObjectClass only)
│         [⟳ Hybrid · synced]  [✓ Enabled]  [🔑 Password never expires]                          │  StatusPills, spacing 8, margin-top 8
│         ✓ Active Directory: match   ·   ✓ Entra ID: match                                      │  coverage line (caption), margin-top 8
└────────────────────────────────────────────────────────────────────────────────────────────────┘
  16
┌ ManagementCard Identity ─────────────────────────┐ 12 ┌ ManagementCard Mailbox ──────────────────────────┐
│ ┌──┐ Manage identity in                          │    │ ┌──┐ Manage mailbox in                           │
│ │🏢│ Active Directory                            │    │ │🏢│ Exchange Server   [Remote mailbox]          │
│ └──┘                                             │    │ └──┘                                             │
│ No web portal. Use Active Directory Users and    │    │ Mailbox data lives in Exchange Online.           │
│ Computers or PowerShell.                         │    │                                                  │
│ [⧉ Copy DN]          View in Entra ID ↗          │    │ [ Open on-prem ECP ↗ ]  [🔗]   View in EXO ↗     │
└──────────────────────────────────────────────────┘    └──────────────────────────────────────────────────┘
  12
┌ InfoBar (Informational, not closable) ─────────────────────────────────────────────────────────┐
│ ⓘ Guidance  Mailbox is in EXO, but managed as a Remote Mailbox via On-Premises Exchange.       │
│             Sync Status: Synced to Entra ID.                                                   │
│             Edit synced attributes (name, proxy addresses, aliases) on-premises. Changes made   │
│             in Microsoft 365 are overwritten by the next sync.                                 │
└────────────────────────────────────────────────────────────────────────────────────────────────┘
  16
┌ Card: Identity details ─────────────┐ 12 ┌ Card: Mail ─────────────────────────────────────────┐
│ 🪪 Identity details                  │    │ ✉ Mail                                              │
│ Display name                    [⧉] │    │ Primary SMTP address                           [⧉]  │
│ John Smith                          │    │ john.smith@contoso.com                              │
│ ─────────────────────────────────── │    │ ─────────────────────────────────────────────────── │
│ Object type                          │    │ Proxy addresses (4)                     [Copy all]  │
│ user                                 │    │ [Primary] john.smith@contoso.com               [⧉]  │
│ ─────────────────────────────────── │    │ [Alias]   jsmith@contoso.com                   [⧉]  │
│ sAMAccountName [M1]             [⧉] │    │ [Alias]   jsmith@contoso.mail.onmicrosoft.com  [⧉]  │
│ jsmith                               │    │ [X500]    /o=ExchangeLabs/ou=…                 [⧉]  │
│ ... UPN, ObjectGUID(mono),           │    │ ─────────────────────────────────────────────────── │
│ Entra object ID(mono), DN(mono)      │    │ Target address [M1]                            [⧉]  │
└─────────────────────────────────────┘    └─────────────────────────────────────────────────────┘
┌ Card: Account ──────────────────────┐ 12 ┌ Card: Licenses ─────────────────────────────────────┐
│ 👤 Account                           │    │ 🎗 Licenses (2)                         [Copy all]  │
│ Status          ✓ Enabled            │    │ ✓ Microsoft 365 E3                                  │
│ Password        Set 3/14/2026 · 193 days ago                                                    │
│ Last synced to Entra ID  9/23/2026 1:45 PM · 17 min ago                                         │
│ Last sign-in    FUTURE slot          │    │                                                     │
└─────────────────────────────────────┘    └─────────────────────────────────────────────────────┘
```

Details grid implementation: a `Grid Name="DetailsGrid"` with `ColumnDefinitions="*,12,*"`; cards are placed by code-behind into two `StackPanel Spacing=12` columns in a fixed order, left: Identity details, Account; right: Mail, Licenses/Group. Container query `max-width:759` on `SearchContent` sets the right StackPanel `Grid.Column=0`, `Grid.Row=1` (`Grid.RowDefinitions="Auto,12,Auto"`, right column goes to row 2) and collapses the 12-px spacer column (style setters on attached properties). Hidden cards collapse (`IsVisible=False`); an empty column simply renders nothing.

##### Result header specifics
- Avatar: 48×48 `Border` radius 24, background by sync state (`LocationOnPremBackground` / `LocationHybridBackground` / `LocationCloudBackground`), `PathIcon` 20: `Icon.Person20` (user), `Icon.Group20` (group), `Icon.Unknown20` (other object categories), foreground = matching `Location*Foreground`.
- Name: `SelectableTextBlock.title`, `TextTrimming=CharacterEllipsis`, `ToolTip.Tip` = full name. `AutomationProperties.HeadingLevel=1`.
- Subline: `ObjectKindLabel · UPN`. ObjectKindLabel mapping from `ObjectClass`: `user`→"User", `group`→"Group", `Cloud User`→"Cloud user", `Cloud Group`→"Cloud group", anything else → title-cased raw value (e.g. "Computer", "Contact"). UPN appended only with [M1]; else subline is just the kind label.
- Buttons (right-aligned, top): `Button` (standard) "Copy summary" with `Icon.CopySummary` 16 left, tooltip "Copy summary for ticket (Ctrl+Shift+C)"; `Button.subtle.icon` 32×32 `Icon.Refresh`, tooltip/name "Search again (F5)". Below 600 px these collapse to icon-only (`Icon.CopySummary` 32×32 with the same tooltip).
- Pills (§6.4), in this order, each only when data exists:
  1. Sync: `OnPremOnly` → "On-premises only" (`Icon.Building`, class `onprem`); `Hybrid` → "Hybrid · synced" (`Icon.Sync`, `hybrid`); `CloudOnly` → "Cloud-only" (`Icon.Cloud`, `cloud`). If AD coverage is not "searched" (§5.1.7) and SyncState is CloudOnly, text becomes "Cloud-only (AD not checked)" with class `caution` instead of `cloud`.
  2. Account (legacy precedence): `IsLockedOut==true` → "Locked out" (`Icon.Lock`, `critical`); else `AccountEnabled==false` → "Disabled" (`Icon.Prohibited`, `neutral`); else `AccountEnabled==true` → "Enabled" (`Icon.CheckCircle`, `success`); null → none.
  3. `PasswordNeverExpires==true` → "Password never expires" (`Icon.Key`, `caution`).
  4. FUTURE: mailbox type ("Shared mailbox", "Room mailbox", "Equipment mailbox", "Microsoft 365 group") — `neutral`, only when [future model] provides it.
- Coverage line: two items `[icon 12] 4 [caption]` separated by " · " (§5.1.7).

##### Management cards (verdict)
Two `ManagementCard`s (§6.6). Content by outcome — the complete table:

| Outcome (derivation) | Identity card | Mailbox card |
|---|---|---|
| AD object, `SyncState=OnPremOnly` | Location **OnPrem**; title "Active Directory"; body "Exists only in on-premises Active Directory. Not synced to Entra ID."; primary: none (no web portal); secondary action "Copy DN" [M1] | per mailbox rows below |
| AD object, `SyncState=Hybrid` | **OnPrem**; "Active Directory"; body "Source of authority is on-premises AD. Changes sync to Entra ID."; secondary "Copy DN" [M1]; link "View in Entra ID" [M1 `ViewLinks`] | per mailbox rows below |
| Graph-only, `SyncState=CloudOnly` | **Cloud**; "Entra ID"; body "Cloud-only object. Manage it in the Microsoft Entra admin center."; primary accent "Open in Entra admin center" → `ManageIdentity.Url` | **Cloud**; "Exchange Online"; body "Manage in the Exchange admin center."; primary "Open Exchange admin center" → `ManageExchange.Url` |
| Mailbox: remote mailbox (MailboxLocation=`RemoteMailbox` [M1]) | — | **OnPrem**; "Exchange Server"; tag "Remote mailbox"; body "Mailbox data lives in Exchange Online, but it's managed on-premises."; primary "Open on-prem ECP" → `ManageExchange.Url`; link "View in Exchange Online" [M1 `ViewLinks`] |
| Mailbox: on-prem mailbox (`OnPremMailbox`) | — | **OnPrem**; "Exchange Server"; body "Mailbox is hosted on-premises."; primary "Open on-prem ECP" |
| Mailbox: mail-enabled group (`OnPremMailEnabledGroup`) | — | **OnPrem**; "Exchange Server"; tag "Mail-enabled group"; body "Manage Exchange properties in on-prem ECP."; primary "Open on-prem ECP" |
| Mailbox: none (`None`, text "No mailbox detected") | — | **Neutral**; icon `Icon.MailOff20`; title "No mailbox"; body "No Exchange attributes found on this account."; no action |
| Mailbox: not applicable (`NotApplicable`, text "N/A") | — | **Neutral**; icon `Icon.MailOff20`; title "Not mail-enabled"; body "This object has no Exchange properties to manage."; no action |

Rules:
- Primary button only when `ManagementLink.IsLink` (Url non-null). If a link card expects a URL but `Url` is null (e.g. AD object without ObjectGUID), show body caption "Link unavailable: object has no ObjectGUID." and no button.
- Every card with a URL also shows a `Button.subtle.icon` `Icon.Link` 36×36 "Copy link" (legacy "Copy Link"), and the primary button's tooltip shows the full URL (mono caption).
- On-prem ECP links while `Session.IsExchangeFqdnDefault`: primary button stays enabled but a Caution line appears under the body: `[Icon.Warning 12] "Exchange server isn't set — this link points to mail.domain.tld."` + `HyperlinkButton` "Settings".
- Shortcuts: Ctrl+1 activates the Identity card's primary button, Ctrl+2 the Mailbox card's (no-op when absent; toast "No portal link for this object" kind Info).
- Card icon: OnPrem `Icon.Building20`, Cloud `Icon.Cloud20`, Neutral `Icon.MailOff20` (mailbox) — identity card is never neutral.
- **Interim derivation without [M1]** (only if the maintainer rejects the `MailboxLocation` addition): `ManageExchange.Text == "Exchange Online"` → Cloud; `Notes` contains `"Remote Mailbox"` → RemoteMailbox; contains `"resides entirely on-premises"` → OnPremMailbox; starts with `"Mail-enabled group"` → OnPremMailEnabledGroup; `Text == "No mailbox detected"` → None; `Text == "N/A"` → NotApplicable. Identity: `ManageIdentity.IsLink` → Cloud else OnPrem. Put these string constants in one private static method in SearchPage with a comment pointing to `IdentityLookupService.BuildAdFoundResult`.

##### Guidance InfoBar
Informational, `IsClosable=False`, title "Guidance", message = `result.Notes` (verbatim, `\n\n` preserved as paragraph breaks). If `SyncState==Hybrid`, append paragraph: "Edit synced attributes (name, proxy addresses, aliases) on-premises. Changes made in Microsoft 365 are overwritten by the next sync." Hidden when both are empty.

##### Detail cards and rows

| Card (icon, title) | Row (label) | Source | Visible when | Mono | Copy |
|---|---|---|---|---|---|
| Identity details (`Icon.ContactCard16`) | Display name | `Name` | always | – | ✓ |
| | Object type | `ObjectClass` (raw) | always | – | – |
| | sAMAccountName | [M1] `SamAccountName` | non-null | – | ✓ |
| | User principal name | [M1] `UserPrincipalName` | non-null | – | ✓ |
| | ObjectGUID | [M1] `ObjectGuid` | non-null | ✓ | ✓ |
| | Entra object ID | [M1] `EntraObjectId` | non-null | ✓ | ✓ |
| | Distinguished name | [M1] `DistinguishedName` | non-null | ✓ (wrap) | ✓ |
| Account (`Icon.Person`) | Status | `IsLockedOut`/`AccountEnabled` (same text as pill, with icon) | `AccountEnabled != null` | – | – |
| | Password | `DisplayFormat.Password(result)`: "Never expires" / "Set {d} · {n} days ago" / "Unknown" (legacy `FormatPassword`) | object is a user | – | – |
| | Last synced to Entra ID | `LastSyncedUtc` → "{g} · {relative}" or "Unknown" | `SyncState==Hybrid` | – | – |
| | Last sign-in | FUTURE `LastSignInUtc` | FUTURE — omit row | – | – |
| Mail (`Icon.Mail`) | Primary SMTP address | derived: proxy entry with prefix `SMTP:` (case-sensitive), prefix stripped | exists | – | ✓ |
| | Proxy addresses ({n}) | `ProxyAddresses` → `ResultListField` (§6.5) | `Count > 0` | – | per item + Copy all |
| | Target address | [M1] `TargetAddress` | non-null | – | ✓ |
| | (empty) | — | card shows "No proxy addresses." caption secondary when nothing else visible | | |
| Licenses (`Icon.Ribbon16`) | list | `Licenses` → `ResultListField` without tags | user object and `SyncState != OnPremOnly` (empty list → "No licenses found." caption) | – | Copy all |
| Group (`Icon.Group`) — replaces Licenses for groups | Group type | `GroupTypeDescription` | group and non-null | – | – |
| | Member count | `MemberCount` formatted `N0`; if exactly 1500 and SyncState != CloudOnly → "1,500+" + caption "Active Directory returns at most 1,500 members per query." | group and non-null | – | – |
| FUTURE Devices (`Icon.Laptop20`) | Primary device | Intune | FUTURE — omit card | | |

"Object is a user": `ObjectClass` is `user` or `Cloud User`. "Group": `group` or `Cloud Group` (legacy logic; replace with [M1] `ObjectKind` when available).

##### Results entrance
On state change to Result: header, management row, guidance, details each start at `Opacity=0, TranslateY=12` and animate to `1, 0` over 250 ms decelerate, staggered 0/40/80/120 ms. Implementation: each block has `Transitions` (DoubleTransition on Opacity, TransformOperationsTransition on RenderTransform); set the start values, then `await Task.Delay(40 * i)` and set the end values. No stagger when re-rendering the same identity (F5).

##### Announcement
After render: `shell.Announce("Found {Name}. Identity managed in {identityTitle}. Mailbox: {mailboxTitle}.")`.

#### 5.1.6 InfoBars on the Search page (complete list)

Order top→bottom: Setup → Coverage issues → Errors. Max visible: all apply (they're rare together).

| Id | Severity | Condition | Title | Message | Action | Closable |
|---|---|---|---|---|---|---|
| SETUP | Caution | §5.1.3 | Finish setting up hybrID | (per §5.1.3) | Open settings | ✓ (session) |
| AD-UNREACHABLE | Caution | `ConnectLdapAsync` returned an error this search (the user then chose Skip) **or** legacy `ldapError` path | Active Directory wasn't searched | "Couldn't connect: {message}. Results are based on Entra ID only — a synced object can look cloud-only." | Retry (re-opens AdCredentialDialog then re-runs search) | ✓ |
| AD-SKIPPED | Caution | `AdState==Skipped` | Active Directory skipped for this session | "Results are based on Entra ID only — a synced object can look cloud-only." | Sign in (clears skip, re-runs search) | ✓ |
| AD-NOTCONFIG | — | AD not configured | not shown as its own bar (covered by SETUP; coverage line still says "not configured") | | | |
| SYNC-HINT [M1] | Caution | AD not searched and `EntraSyncEnabled==true` | Entra ID says this object is synced from on-premises AD | "Manage it in Active Directory, not in Entra ID. Sign in to Active Directory to see the full picture." | Sign in | ✗ |
| GRAPH-SUSPECT | Caution | DeviceCodeDialog was shown during this search **and** result has no Graph data (SyncState OnPremOnly or not found) — until [M2] | Microsoft sign-in may not have finished | "Entra ID results may be missing. If you completed sign-in, search again." | Search again | ✓ |
| GRAPH-ERROR [M2] | Caution | `result.GraphError != null` | Entra ID wasn't searched | "{GraphError}" | Search again | ✓ |
| GRAPH-INIT | Critical | `EnsureGraphClient` threw | Couldn't start Microsoft Graph sign-in | "{ex.Message} Check the tenant and client ID in Settings." | Open settings | ✓ |

All InfoBars are created in XAML (one named instance per Id, `IsOpen=False`) and toggled from code; messages set via properties. No InfoBar for success — the verdict is the success message.

#### 5.1.7 Coverage line

| Directory | State (view-level) | Icon (12) + color | Text |
|---|---|---|---|
| Active Directory | searched & match (`SyncState` OnPremOnly/Hybrid) | `Icon.CheckCircle` success | "Active Directory: match" |
| | searched, no match | `Icon.NotSet` neutral (secondary text) | "Active Directory: no match" |
| | not configured | `Icon.NotSet` tertiary | "Active Directory: not configured" |
| | skipped (user) | `Icon.Warning` caution | "Active Directory: skipped" |
| | unreachable (connect failed this search) | `Icon.Warning` caution | "Active Directory: unreachable" |
| Entra ID | match (`SyncState` Hybrid/CloudOnly) | `Icon.CheckCircle` success | "Entra ID: match" |
| | no match | `Icon.NotSet` neutral | "Entra ID: no match" |
| | [M2] error / GRAPH-SUSPECT | `Icon.Warning` caution | "Entra ID: may be incomplete" |

Shown on Result, NoResult and Candidates states. Note: an already-open LDAP connection that silently fails (service swallows exceptions) reads as "no match" — known limitation until [M2] `AdError`.

#### 5.1.8 No-result state

```
                                        ┌──┐
                                        │ ? │  Icon.Unknown20 at 48×48, TextFillColorTertiary
                                        └──┘
                            No match for 'jsmith'                                    (subtitle)
          ✓ Active Directory: no match  ·  ✓ Entra ID: no match                      (coverage line, centered)
          Check the spelling, or try the UPN or ObjectGUID instead.                   (body secondary)
                                  [ Search again ]                                    (standard button)
```
If AD coverage is skipped/unreachable/not configured, the AD InfoBar from §5.1.6 also shows (the miss may be a false negative). `result.StatusMessage` ("Object not found in Active Directory or Entra ID.") is used as the announcement.

#### 5.1.9 Candidates (ambiguous) state

```
3 matches for 'jsmith'                                                              (subtitle)
Pick one to see where it's managed.                                                  (body secondary)
coverage line
┌──────────────────────────────────────────────────────────────────────────────────────────────┐
│ ┌──┐ John Smith                                                                           ›  │  64 h
│ │🏢│ Active Directory - jsmith@contoso.com                                                   │
│ └──┘                                                                                          │
├──────────────────────────────────────────────────────────────────────────────────────────────┤
│ ┌──┐ John Smith (Contractor)                                                              ›  │
│ │☁ │ Entra ID - 7c1e…-…                                                                      │
└──────────────────────────────────────────────────────────────────────────────────────────────┘
```
- `ListBox Classes="candidates"` in a card (radius 8, stroke), `SelectionMode=Single`, items separated by 1 px dividers. ItemTemplate: 36×36 tinted icon badge (radius 4; [M1] `Source`: AD → `Icon.Building` OnPrem tint, Entra → `Icon.Cloud` Cloud tint; without [M1] → `Icon.Person` neutral), `DisplayName` bodyStrong, `Detail` caption secondary (verbatim), `Icon.ChevronRight` 16 tertiary at right.
- Activate on click or Enter/Space: set `TxtSearch.Text = candidate.Identity` and run search (legacy behavior).
- Keyboard: from `TxtSearch`, Down arrow moves focus to the first candidate. Up on the first candidate returns to `TxtSearch`.
- Do **not** use SelectableTextBlock inside items (steals clicks).
- Announcement: `result.StatusMessage`.

### 5.2 SettingsPage

#### 5.2.1 Skeleton & save model

```
SettingsPage (Grid rows: *, Auto)
├── ScrollViewer → StackPanel MaxWidth 1000 centered, Margin 32,24,32,96 (space for save bar)
│   ├── "Settings"  (title, HeadingLevel 1, margin-bottom 8)
│   ├── SavedInfoBar (Success, "Settings saved", auto-hides after 4 s)   ← replaces legacy AlertDialog
│   ├── Section "Active Directory"
│   ├── Section "Exchange"
│   ├── Section "Microsoft Graph"
│   ├── Section "Appearance"
│   ├── Section "Management links"
│   ├── Section "Configuration"
│   └── Section "About"
└── SaveBar (row 1, overlaid at bottom, visible only when dirty)
```
- Section header: `TextBlock.bodyStrong`, margin `1,28,0,8` (first one `1,16,0,8`), `AutomationProperties.HeadingLevel=2`. Cards within a section: `StackPanel Spacing=4`.
- **Save model**: Appearance applies and saves **immediately** (Win11 behavior; theme preview is the point). Every other field is staged; editing any staged field shows the **SaveBar**. Save = validate → write into `Session.Config.Settings/ManagementUrls` → `ConfigService.Save` → `Session.ApplySavedConfig(connectionChanged)` → show SavedInfoBar → hide SaveBar. `connectionChanged` = any of LDAP server/port/encryption/base DN/skip-cert, tenant ID, client ID changed. Discard = reload form from config.
- Form population on `OnAttachedToVisualTree` from `Session.Config` (legacy load logic, incl. `EncryptionIndexFor`, `ThemeIndexFor`).
- `Ctrl+S` saves when SaveBar is visible.

SaveBar:
```
┌──────────────────────────────────────────────────────────────────────────────────────┐  h=64, MaxWidth 1000, margin 32,0,32,16
│ ⓘ Unsaved changes                                          [ Discard ]  [ Save ]      │  card: SolidBackgroundFillColorQuarternary, stroke SurfaceStroke, radius 8, ShadowFlyout
│   Saving signs you out of Active Directory and Microsoft Graph.                       │  caption secondary line only if connectionChanged
└──────────────────────────────────────────────────────────────────────────────────────┘
```
Save is `accent`, disabled while validation errors exist; tooltip "Save (Ctrl+S)". Slide in: TranslateY 16→0 + fade, 250 ms.

#### 5.2.2 Section contents

Content control widths: TextBox/ComboBox `Width=280` (`MinWidth=200` when wrapped), NumericUpDown `Width=140`.

**Active Directory**

| Card | Icon | Header / Description | Control | Behavior |
|---|---|---|---|---|
| SettingsCard | `Icon.Server20` | "Domain controller" / "Hostname of the DC to query over LDAP. No domain join required." | TextBox `CfgLdapServer`, placeholder "dc01.contoso.com" | |
| SettingsCard | `Icon.ShieldLock20` | "Encryption" / "LDAPS works on every OS. StartTLS works only on Windows." | ComboBox `CmbLdapEncryption`: "LDAPS (recommended)", "StartTLS (Windows only)", "None (insecure – lab use only)" | Non-Windows: StartTLS item `IsEnabled=False`. Changing to LDAPS while port==389 → 636; to StartTLS/None while port==636 → 389. "None" → inline Critical InfoBar under the card (not closable): title "Credentials are sent unencrypted", message "Use only against a disposable lab domain controller." Config says StartTLS on non-Windows → Caution InfoBar "StartTLS isn't supported on this OS. Switch to LDAPS." |
| SettingsCard | `Icon.NumberSymbol20` | "Port" / "636 for LDAPS, 389 for StartTLS or no encryption." | NumericUpDown `CfgLdapPort` Min 1 Max 65535 `FormatString="0"` `Increment=1` | Invalid/blank keeps previous value (legacy) and shows `invalid` class. |
| SettingsExpander "Advanced" | `Icon.Settings` | "Advanced" / "Base DN and certificate validation" | nested cards ↓ | collapsed by default; auto-expanded if either value is non-default |
| ↳ nested SettingsCard | – | "Base DN" / "Leave blank to auto-discover from the server's RootDSE." | TextBox `CfgLdapBaseDn` (mono), placeholder "DC=contoso,DC=com" | |
| ↳ nested SettingsCard | – | "Skip certificate validation" / Windows: "Self-signed lab DCs only. Never use in production." | ToggleSwitch `ChkLdapSkipCert` | On → Caution InfoBar in the expander "Certificate validation is off." Non-Windows: ToggleSwitch `IsEnabled=False`; description "Not supported on macOS/Linux. Set LDAPTLS_REQCERT=never in your shell before launching hybrID."; extra Button "Copy command" copying `export LDAPTLS_REQCERT=never`. If config value is true on non-Windows: Caution "This setting is ignored on this OS." |
| SettingsCard | `Icon.PlugConnected20` | "Test connection" / result text (see below) | Button `BtnTestLdap` "Test connection" | Opens AdCredentialDialog (purpose Test) using **form** values (legacy). While running: button disabled, ProgressRing 16 left of it. Result description: success → `[Icon.CheckCircle success] "Connected. Base DN: {dn}"`; failure → `[Icon.Error critical] "Connection failed: {message}"`. Initial description: "Uses the values above, even if they aren't saved yet." |

**Exchange**

| Card | Icon | Header / Description | Control | Behavior |
|---|---|---|---|---|
| SettingsCard | `Icon.Mail20` | "On-premises Exchange server" / "FQDN used to build Exchange admin center (ECP) links." | TextBox `CfgFqdn` placeholder "mail.contoso.com" | Value "mail.domain.tld" or blank → Caution line in description "Still set to the placeholder. ECP links won't work." |

**Microsoft Graph**

| Card | Icon | Header / Description | Control | Behavior |
|---|---|---|---|---|
| SettingsCard | `Icon.Cloud20` | "Tenant" / "organizations, common, a tenant ID, or a verified domain." | TextBox `CfgGraphTenant` placeholder "organizations" | Blank saves as "organizations" (legacy). |
| SettingsExpander "Advanced" | `Icon.Settings` | "Advanced" / "Client ID for your own app registration" | nested ↓ | |
| ↳ nested SettingsCard | – | "Client ID" / "Default is the Microsoft Graph Command Line Tools public client. Change only if Conditional Access requires your own app registration." | TextBox `CfgGraphClientId` (mono, Width 320) + `Button.subtle.icon` `Icon.ArrowReset` "Reset to default" | Must be a GUID → else `invalid` + inline caption "Enter a GUID." (blocks Save). Exposes the existing `GraphClientId` setting (previously hand-edit only). |
| SettingsCard | `Icon.PlugConnected20` | "Test connection" / result text | Button `BtnTestGraph` "Test connection" | Legacy behavior (throwaway client from form values; one `users?$top=1` call). DeviceCodeDialog opens if needed. Success: "Signed in. Microsoft Graph responded." + `Session.MarkGraphSignedIn()` only if tenant/client unchanged vs saved. |

**Appearance**

| Card | Icon | Header / Description | Control | Behavior |
|---|---|---|---|---|
| SettingsCard | `Icon.Theme` | "App theme" / "Choose light or dark, or follow your system." | ComboBox `CmbTheme`: "Use system setting", "Light", "Dark" (→ "System"/"Light"/"Dark") | Applies instantly via `ThemeService.Apply` and saves only the `Theme` field immediately (write current *saved* config + new theme; staged edits remain staged). |

**Management links**

| Card | Icon | Header / Description | Control | Behavior |
|---|---|---|---|---|
| SettingsExpander | `Icon.Link20` | "Link templates" / "URL templates for deep links. {id} is the object's ID; {fqdn} is your Exchange server." | nested ↓, collapsed by default | |
| ↳ 6 nested rows | – | "Entra ID – user", "Entra ID – group", "Exchange Online – mailbox", "Exchange Online – group", "Exchange Server (ECP) – mailbox", "Exchange Server (ECP) – group" | Each row wraps: header on top, full-width TextBox (`mono` class, 13 px) below + `Button.subtle.icon` `Icon.ArrowReset` "Reset" at right | Missing `{id}` → `invalid` + "Must contain {id}." (blocks Save). ECP rows missing `{fqdn}` → Caution caption "No {fqdn} placeholder; the Exchange server setting won't be used." (doesn't block). Defaults = `new ManagementUrls()`. |
| ↳ footer row | – | – | Button "Reset all to defaults" | Confirms via MessageDialog ("Reset all link templates?" / "This replaces your 6 templates with the defaults. You still need to save." / Primary "Reset", Close "Cancel"). |

**Configuration**

| Card | Icon | Header / Description | Control | Behavior |
|---|---|---|---|---|
| SettingsCard | `Icon.Document20` | "Configuration file" / `ConfigService.ConfigPath` (SelectableTextBlock mono caption) | Button "Copy path" + Button "Open folder" (`Icon.Folder`) | Open folder: `UrlLauncher.Open(Path.GetDirectoryName(ConfigPath))` (ShellExecute/open/xdg-open all accept folders). |
| FUTURE SettingsCard | `Icon.DocumentText20` | "Logs" / "Recent LDAP/Graph errors for troubleshooting." | Button "Open logs folder" | Omit until error logging ships (§8). |

**About**

| Card | Icon | Header / Description | Control | Behavior |
|---|---|---|---|---|
| SettingsCard `IsClickEnabled=True` | `Icon.Info20` | "About hybrID" / "Version {AppVersion.Current}" | chevron (built-in action icon) | Navigates to About page. |

#### 5.2.3 Settings wireframe (Windows, content 1000)

```
Settings                                                                          (title 28)

Active Directory                                                                  (bodyStrong)
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│ 🖥  Domain controller                                          ┌──────────────────────────┐ │ MinHeight 68
│     Hostname of the DC to query over LDAP. No domain join…     │ dc01.contoso.com         │ │ Padding 16
│                                                                └──────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────────────────────────┘
 4
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│ 🛡  Encryption                                                 [ LDAPS (recommended)   ⌄ ]  │
│     LDAPS works on every OS. StartTLS works only on Windows.                                │
└────────────────────────────────────────────────────────────────────────────────────────────┘
 4
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│ #   Port                                                              [ 636      ˄˅ ]       │
│     636 for LDAPS, 389 for StartTLS or no encryption.                                       │
└────────────────────────────────────────────────────────────────────────────────────────────┘
 4
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│ ⚙   Advanced                                                                            ⌄  │ SettingsExpander header
│     Base DN and certificate validation                                                     │
├────────────────────────────────────────────────────────────────────────────────────────────┤
│        Base DN                                                 ┌──────────────────────────┐ │ nested: Padding 58,8,48,8
│        Leave blank to auto-discover from the server's RootDSE. │ DC=contoso,DC=com        │ │
├────────────────────────────────────────────────────────────────────────────────────────────┤
│        Skip certificate validation                                               Off ( ○ )  │
│        Self-signed lab DCs only. Never use in production.                                   │
└────────────────────────────────────────────────────────────────────────────────────────────┘
 4
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│ 🔌  Test connection                                                   [ Test connection ]  │
│     ✓ Connected. Base DN: DC=contoso,DC=com                                                │
└────────────────────────────────────────────────────────────────────────────────────────────┘
 28
Exchange
...
```

#### 5.2.4 Unsaved-changes prompt

Triggered when navigating away (nav click, Ctrl+L/Ctrl+F, macOS menu About) or closing the window with the SaveBar visible. `MessageDialog`: title "Save changes?", message "You have unsaved settings.", Primary "Save", Secondary "Don't save", Close "Cancel". Primary → save (abort navigation if validation fails). Secondary → discard and continue. Close → stay. Window close: handle `Window.Closing` (cancel, show prompt, then `Close()` again with a guard flag).

### 5.3 AboutPage

```
About                                                                                    (title)
┌────────────────────────────────────────────────────────────────────────────────────────────┐
│ ┌──────┐  hybrID                                                                           │ card, Padding 24
│ │ icon │  Version 0.2.0                                         [⧉ Copy version info]      │ subtitle / body secondary
│ │  64  │  Know where to manage identity objects in hybrid Microsoft environments.          │ body
│ └──────┘                                                                                   │
└────────────────────────────────────────────────────────────────────────────────────────────┘
 4
│ </>  Source code          github.com/griffeth-barker/hybrID                    ↗  │ SettingsCard IsClickEnabled, ActionIcon Icon.Open
│ 🐞  Report an issue       Opens GitHub Issues in your browser                   ↗  │
│ ⚖   License               MIT License · © 2026 Griffeth Barker                     │ non-clickable
│ 📦  Third-party software                                                       ⌄  │ SettingsExpander: Avalonia (MIT) · Fluent UI System Icons (MIT) ·
│                                                                                    │   Inter (SIL OFL 1.1) · Cascadia Mono (SIL OFL 1.1) · Microsoft Graph SDK for .NET (MIT) · Azure Identity (MIT)
│ 🖥  System information                                                         ⌄  │ SettingsExpander: OS, architecture, .NET runtime, config path; [Copy]
```
- URLs: source `https://github.com/griffeth-barker/hybrID` (legacy constant), issues `https://github.com/griffeth-barker/hybrID/issues`. Open via `UrlLauncher.Open`.
- "Copy version info" copies `hybrID {AppVersion.Current} · {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture} · .NET {Environment.Version}` and toasts "Version info copied".
- Icons: `Icon.Code20`, `Icon.Bug20`, `Icon.Certificate20`, `Icon.Box20`, `Icon.Server20`.
- Page max width 1000, same padding as Settings.

### 5.4 Dialogs (all `OverlayDialog`, §6.7)

#### 5.4.1 AdCredentialDialog

```
┌──────────────────────────────────────────────────────┐  Width 440
│ Sign in to Active Directory                           │  subtitle
│ 🏢 dc01.contoso.com · LDAPS · port 636                │  caption secondary, margin-bottom 16
│                                                      │
│ Username                                              │  body, margin-bottom 8
│ ┌──────────────────────────────────────────────────┐ │
│ │ user@contoso.com or CONTOSO\user                 │ │  TextBox h32
│ └──────────────────────────────────────────────────┘ │
│ Password                                     (16 gap) │
│ ┌──────────────────────────────────────────────┬───┐ │
│ │ ••••••••                                     │ 👁 │ │  PasswordChar •, reveal toggle InnerRightContent
│ └──────────────────────────────────────────────┴───┘ │
│ 🔒 Used for this session only. Never saved to disk.   │  caption secondary, margin-top 8
│ [InfoBar Critical, hidden] Couldn't connect: …        │  margin-top 16
├──────────────────────────────────────────────────────┤  1px CardStroke
│ [        Sign in        ]   [        Skip          ]  │  button bar, SolidBackgroundFillColorBase, Padding 24
└──────────────────────────────────────────────────────┘
```
- Constructor `AdCredentialDialog(string serverLabel, Func<string,string,Task<string?>> tryConnect, AdCredentialPurpose purpose)`; `purpose` Search → Close text "Skip"; Test → "Cancel".
- Primary "Sign in" (accent, `IsDefault`), enabled when username non-empty. Click → disable inputs, primary content `[ProgressRing 16] "Signing in…"`, await `tryConnect(user, pwd)`. null → close with `Primary`. Error → show Critical InfoBar (`IsClosable=False`, title "Couldn't connect", message = error), re-enable, select password text, keep open.
- Reveal: `Button.subtle.icon` 28×28 toggling `TextBox.RevealPassword`, icons `Icon.Eye`/`Icon.EyeOff`, name "Show password"/"Hide password".
- Initial focus: Username. Tab order: Username → Password → reveal → Sign in → Skip. Esc = Skip/Cancel.
- Password string is not kept after the dialog closes (clear the TextBox in `OnClosed`).

#### 5.4.2 DeviceCodeDialog

```
┌──────────────────────────────────────────────────────┐  Width 460
│ Sign in to Microsoft Graph                            │  subtitle
│                                                      │
│ 1  Open  microsoft.com/devicelogin ↗                  │  body; HyperlinkButton (VerificationUri)
│ 2  Enter this code:                                   │
│ ┌──────────────────────────────────────────────────┐ │
│ │        ABCD-EFGH                        [⧉]      │ │  CardBackgroundSecondary, radius 4, h 64; mono SemiBold 28, LetterSpacing 2, centered, selectable
│ └──────────────────────────────────────────────────┘ │
│ ⏱ Code expires at 2:17 PM (in 14 min)                 │  caption secondary; refresh every 30 s
│ 3  Sign in with your admin account.                   │
│                                                      │
│ ◠ Waiting for you to finish signing in…               │  ProgressRing 16 + body secondary, LiveSetting Polite
│ Your sign-in is cached in {store}.                    │  caption tertiary
├──────────────────────────────────────────────────────┤
│ [ Copy code and open browser ]   [      Hide       ]  │
└──────────────────────────────────────────────────────┘
```
- Constructor `DeviceCodeDialog(DeviceCodeInfo info)`. Uses `UserCode`, `VerificationUri`, `ExpiresOn`. If `UserCode` is null/empty, show `info.Message` verbatim in the code area instead (body, wrap).
- `{store}`: Windows "Windows Credential Manager", macOS "your macOS Keychain", Linux "the Secret Service keyring".
- Primary (accent, default): copy `UserCode` to clipboard, `UrlLauncher.Open(VerificationUri)`, toast "Code copied". Dialog stays open.
- Close "Hide": closes the overlay only; sign-in continues in the background (the device-code flow has no cancellation — R3). `Session.GraphState` stays `WaitingForSignIn`; the Entra chip pulses and its flyout offers "Show sign-in code" to reopen the same dialog.
- Auto-closes when the owning search/test completes (legacy behavior). If `ExpiresOn` passes: body switches to Caution line "This code expired. Search again to get a new one." and primary becomes disabled.
- Opened from the device-code callback via `Dispatcher.UIThread.Post` (callback runs on a background thread). `ShowDialogAsync` for this dialog is not awaited by the search.

#### 5.4.3 MessageDialog

- Constructor `MessageDialog(string title, string message, string closeText = "OK", string? primaryText = null, string? secondaryText = null, InfoBarSeverity? severity = null)`.
- Optional severity icon (20, filled variant) left of the title.
- Uses: unsaved-changes prompt (§5.2.4), reset templates confirm, `ConfigService.Save` failure ("Couldn't save settings" / `{ex.Message}` / Critical), unexpected exceptions in page handlers (Critical).

---

## 6. Component specs

General rules for every custom control:
- Templated controls get a `ControlTheme` keyed by type (`x:Key="{x:Type controls:InfoBar}"`, `TargetType=...`) in `Themes/Controls/*.axaml`. Subclasses of a themed control override `StyleKeyOverride`.
- Colors via `DynamicResource` only. State changes via pseudo-classes (`PseudoClasses.Set(":error", …)`) or `Classes`, never by assigning brushes in code.
- Every interactive part has a visible `:focus-visible` style (Fluent adorner for Buttons; custom templates must add a 2 px `FocusStrokeColorOuter` border + 1 px inner), `AutomationProperties.Name`, and a hit target ≥ 32×32 (≥ 36 height for text buttons in content).

### 6.1 Card (style, not a control)

`Border.card`: Background `CardBackgroundFillColorDefaultBrush`, BorderBrush `CardStrokeColorDefaultBrush`, BorderThickness 1, CornerRadius 8, Padding 16. Card header row (optional): `[PathIcon 16 secondary] 8 [TextBlock.bodyStrong]` + right-aligned actions, margin-bottom 8. Rows inside separated by `Border.divider` (Height 1, Background `DividerStrokeColorDefaultBrush`, Margin `0,0` — spans card inner width).

### 6.2 InfoBar (`Controls/InfoBar : TemplatedControl`)

Properties (StyledProperty): `Severity` (enum `InfoBarSeverity { Informational, Success, Warning, Error }` — spec text says Caution/Critical for colors; enum mirrors WinUI names), `Title` (string), `Message` (string), `IsOpen` (bool, default true), `IsClosable` (bool, default true), `ActionContent` (object?, typically a Button), `IsIconVisible` (bool, default true). Event `Closed`. Pseudo-classes `:informational :success :warning :error :closable`.

Anatomy:
```
┌─────────────────────────────────────────────────────────────────────────────────────┐ MinHeight 48, radius 4, stroke CardStrokeColorDefault
│16│[icon16]│12│ Title (bodyStrong) Message (body) — wraps as one paragraph │16│[Action]│12│[✕ 40×40]│
└─────────────────────────────────────────────────────────────────────────────────────┘
```
- Grid columns `16, 16, 12, *, Auto, Auto`. Icon top-aligned at y=16 (so multi-line text stays aligned with first line). Text margin `0,14,16,14`; implemented as `SelectableTextBlock` with inlines `Run Title (SemiBold)`, `Run "  "`, `Run Message`; `TextWrapping=Wrap`. Multi-paragraph messages: `\n\n` renders as LineBreaks.
- Action: margin `0,8,12,8`, `VerticalAlignment=Top`, standard Button (not accent).
- Close: `Button.subtle.icon` 40×40 (margin `0,4,4,4`) `Icon.Dismiss` 16, name "Close", tooltip "Close". Click → `IsOpen=false`, raise `Closed`.
- Severity visuals:

| Severity | Background | Icon (filled, 16) | Icon color | LiveSetting |
|---|---|---|---|---|
| Informational | `SystemFillColorAttentionBackgroundBrush` | `Icon.InfoFilled` | `SystemFillColorAttentionBrush` | Polite |
| Success | `SystemFillColorSuccessBackgroundBrush` | `Icon.CheckCircleFilled` | `SystemFillColorSuccessBrush` | Polite |
| Warning (Caution) | `SystemFillColorCautionBackgroundBrush` | `Icon.WarningFilled` | `SystemFillColorCautionBrush` | Polite |
| Error (Critical) | `SystemFillColorCriticalBackgroundBrush` | `Icon.ErrorFilled` | `SystemFillColorCriticalBrush` | Assertive |

- `IsOpen=false` → `IsVisible=false`. Opening: Opacity 0→1 over 167 ms (no height animation).
- Automation: root `AutomationProperties.Name` = "{Severity word}: {Title}. {Message}", set `LiveSetting` (per the table) on the root so the bar is announced when it opens.

### 6.3 Nav rail item (`ControlTheme NavRailItemTheme`, TargetType `RadioButton`)

Content per item (in MainWindow XAML):
```xml
<RadioButton GroupName="NavRail" Theme="{StaticResource NavRailItemTheme}" Name="NavSearch"
             AutomationProperties.Name="Search" Tag="{StaticResource Icon.Search}" Content="Search"/>
```
Template:
```
Border#Root  Height 36, Margin 4,2, CornerRadius 4, Background SubtleFillColorTransparent
└ Grid columns 12(indicator lane incl.), 16 icon, 16 gap, * label
    ├ Border#Pill  Width 3, Height 16, CornerRadius 2, HorizontalAlignment Left, Margin 0, Background AccentFillColorDefault,
    │               RenderTransform scaleY(0) → selected scaleY(1) (TransformOperationsTransition 167 ms), Opacity via IsChecked
    ├ PathIcon Data={TemplateBinding Tag} 16×16, Grid.Column 1, Foreground TextFillColorPrimary
    └ ContentPresenter (label) Grid.Column 3, body, TextTrimming, VerticalAlignment Center
```
Geometry: item is 232 wide in the 240 pane, 40 wide in the 48 compact pane; icon left edge at 12 px inside the item (16 from pane edge) so it doesn't move when the pane toggles.

States: `:pointerover` Root bg `SubtleFillColorSecondary`; `:pressed` `SubtleFillColorTertiary`, pill height 10; `:checked` bg `SubtleFillColorSecondary`, pill visible, label unchanged weight (WinUI keeps regular); `:checked:pointerover` `SubtleFillColorTertiary`; `:disabled` foreground `TextFillColorDisabled`; `:focus-visible` 2 px focus ring on Root (inset 0). Background changes use `BrushTransition` 83 ms. No radio circle.

### 6.4 StatusPill (`Controls/StatusPill : TemplatedControl`)

Properties: `Text`, `Icon` (Geometry?), `Kind` (enum `PillKind { OnPrem, Hybrid, Cloud, Success, Caution, Critical, Neutral }` → pseudo-class `:onprem` …).
Anatomy: `Border` Height 24, CornerRadius 12, Padding `8,0` (`6,0,8,0` with icon), `StackPanel` horizontal Spacing 4: `PathIcon` 12×12 (optional) + `TextBlock.caption` SemiBold. Background/foreground per kind: OnPrem/Hybrid/Cloud → `Location*Background`/`Location*Foreground`; Success/Caution/Critical/Neutral → `SystemFillColor*Background`/`SystemFillColor*`. No border in Light; in Dark add 1 px stroke of the foreground at 20% (`#33` + rgb — define `Location*Stroke` / `SystemFillColor*Stroke` keys: OnPrem `#335FD4DA`, Hybrid `#33C7A6F5`, Cloud `#33A9B8FF`, Success `#336CCB5F`, Caution `#33FCE100`, Critical `#33FF99A4`, Neutral `#1FFFFFFF`; Light values `Transparent`).
Not focusable. `AutomationProperties.Name` = Text. Always icon + text (never color alone).

### 6.5 ResultField / ResultListField

**ResultField** (`TemplatedControl`): `Label` (string), `Value` (string?), `IsMonospace` (bool), `CanCopy` (bool, default true), `CopyText` (string?, defaults to Value), `ValueIcon` (Geometry?, e.g. status icon), `ValueKind` (PillKind? for icon color). The control copies by itself via `TopLevel.GetTopLevel(this).Clipboard.SetTextAsync` and raises a bubbling routed event `Copied` (args: Label) that the page handles to announce.

```
┌───────────────────────────────────────────────────────────────┐  MinHeight 56, Padding 0,8
│ Label (caption secondary)                                [⧉]  │  CopyButton 32×32, VerticalAlignment Center
│ Value (body or mono; SelectableTextBlock; wrap)                │  2 px gap under label
└───────────────────────────────────────────────────────────────┘
```
- Row hover: Background `SubtleFillColorSecondary` with CornerRadius 4 and horizontal bleed (Margin `-8,0`, Padding `8,8`).
- Empty `Value` → show "—" in `TextFillColorTertiary`, CopyButton hidden. (Normally the page hides the whole row instead.)
- CopyButton (`Controls/CopyButton`): `Button.subtle.icon` with `Icon.Copy` 16; on click copies, swaps icon to `Icon.Check` in `SystemFillColorSuccessBrush`, tooltip "Copied", then after 1500 ms restores. `AutomationProperties.Name="Copy {Label}"`, tooltip "Copy". Announces "{Label} copied" via `shell.Announce` (page subscribes to `Copied`).

**ResultListField** (`TemplatedControl`): `Label`, `ItemsSource` (IEnumerable of `ResultListItem { string Text; string CopyText; string? Tag; PillKind TagKind; bool IsEmphasized }`), `CopyAllText` (string), `CollapsedCount` (int, default 5).
```
Proxy addresses (4)                                          [Copy all]      header: caption secondary + Button (standard, small h28, caption text)
[Primary] john.smith@contoso.com                                   [⧉]      rows h36: tag pill (Height 20, caption) + body text (SemiBold if emphasized) + CopyButton 28×28
[Alias]   jsmith@contoso.com                                        [⧉]
…
[ Show all (12) ⌄ ]                                                          HyperlinkButton, only if Count > CollapsedCount
```
Proxy mapping (in `DisplayFormat.ParseProxyAddresses`): prefix before first `:` = type. `SMTP` (uppercase) → Tag "Primary" (Success kind), emphasized; `smtp` → "Alias" (Neutral); others → uppercase type ("SIP", "X500", "EUM", "SPO") Neutral. `Text`/`CopyText` = address without prefix. Sort: primary, aliases (ordinal ignore-case), others. "Copy all" copies the raw values, one per line, with prefixes (legacy behavior; tooltip "Copy all (with prefixes)"). Licenses use no tags; Copy all joins with newlines.

### 6.6 ManagementCard (`Controls/ManagementCard : TemplatedControl`)

Properties: `Overline` ("Manage identity in" / "Manage mailbox in"), `LocationTitle`, `Location` (enum `ManagementLocation { OnPrem, Cloud, Neutral }`), `Icon` (Geometry), `TagText` (string?, e.g. "Remote mailbox"; rendered as a Neutral StatusPill), `Body` (string), `WarningText` (string?), `PrimaryText` (string?), `PrimaryUrl` (string?), `SecondaryContent` (object?, e.g. "Copy DN" button), `ViewLinkText`/`ViewLinkUrl` (string?). The card handles open (`UrlLauncher.Open`) and copy (clipboard + raises bubbling `LinkCopied` for the page to toast) internally.

```
┌──────────────────────────────────────────────────────┐  card, Padding 20, MinHeight 176, radius 8
│ ┌────┐  Manage mailbox in            (caption secondary)
│ │ 🏢 │  Exchange Server  [Remote mailbox]            (subtitle + Neutral StatusPill for TagText)
│ └────┘  40×40 badge radius 4, bg Location*Background, icon 20 Location*Foreground; Neutral → SystemFillColorNeutralBackground/TextFillColorSecondary
│                                                      (12)
│ Mailbox data lives in Exchange Online, but it's      (body secondary, wrap, MaxLines 3)
│ managed on-premises.
│ ⚠ Exchange server isn't set — …  Settings            (caption, caution; optional)
│                                                      (16, pushed to bottom via DockPanel)
│ [ Open on-prem ECP  ↗ ]  [🔗]        View in EXO ↗   (accent h36 MinWidth 160 + Icon.Open12 right; subtle icon 36×36; HyperlinkButton right-aligned)
└──────────────────────────────────────────────────────┘
```
- Primary button: `Classes="accent"`, Height 36, content `[text] 8 [PathIcon Icon.Open12 12]`, `AutomationProperties.Name="{PrimaryText} (opens browser)"`, tooltip = URL (mono caption, MaxWidth 480, wrap). Click → `UrlLauncher.Open(PrimaryUrl)`.
- Copy link: `Button.subtle.icon` 36×36 `Icon.Link`, name "Copy {Overline} link", toast "Link copied".
- No `PrimaryUrl` → action row shows `SecondaryContent` only (may be empty; card keeps MinHeight so the pair stays aligned).
- Both cards in a row share height (`Grid` with two star columns, `VerticalAlignment=Stretch`).
- `AutomationProperties.Name` on the card = "{Overline} {LocationTitle}"; heading level 2 on LocationTitle.

### 6.7 OverlayHost + OverlayDialog

**OverlayDialog** (`ContentControl`): `Title`, `PrimaryButtonText`, `SecondaryButtonText`, `CloseButtonText` (null/empty hides the button), `IsPrimaryButtonEnabled`, `IsSecondaryButtonEnabled`, `DefaultButton` (enum Primary/Secondary/Close/None; default Primary), `PrimaryButtonContent` override (object?, for the "Signing in…" ring state). Events: `PrimaryButtonClick`, `SecondaryButtonClick`, `CloseButtonClick` (all cancelable via `e.Cancel` so a dialog can stay open, e.g. AD sign-in in progress). Method `Hide(OverlayDialogResult)`.
Template:
```
Border#Frame  MinWidth 320, MaxWidth 548 (explicit Width per dialog allowed), MaxHeight = host height − 96,
              CornerRadius 8, BorderBrush SurfaceStrokeColorDefault 1, BoxShadow ShadowDialog, ClipToBounds True
└ Grid rows *, Auto
   ├ Border (LayerFillColorAlt over SolidBackgroundFillColorBase), Padding 24
   │   └ StackPanel: TextBlock.subtitle Title (margin-bottom 12, HeadingLevel 1) + ScrollViewer(ContentPresenter)
   └ Border button bar: Background SolidBackgroundFillColorBase, BorderThickness 0,1,0,0, BorderBrush CardStrokeColorDefault, Padding 24
       └ Grid columns *,8,*,8,* (unused columns collapse → equal-width buttons), buttons Height 32 HorizontalAlignment Stretch
```
The frame's opaque background: `SolidBackgroundFillColorBase` behind the whole frame, top area overlaid with `LayerFillColorAlt`.
Default button gets `Classes="accent"` and `IsDefault=True`; Close button gets `IsCancel=True`.

**OverlayHost** (`UserControl`, last child of MainWindow root Grid, spans all rows/columns, `ZIndex=100`, `IsVisible=false` when idle):
- Layers: `Border#Smoke` Background `SmokeFillColorDefault` (fills everything, consumes pointer input; no ElementRole → title bar underneath isn't draggable while open — intended) + `ContentPresenter#DialogPresenter` centered.
- API: `Task<OverlayDialogResult> ShowAsync(OverlayDialog dialog)` — FIFO queue; one dialog visible at a time; `TaskCompletionSource` completed on hide.
- Open: remember `FocusManager.GetFocusedElement()`, show host, Smoke Opacity 0→1 (167 ms), Frame Opacity 0→1 + Scale 1.05→1 (250 ms decelerate), focus the dialog's first focusable input, else the default button.
- Close: Frame Opacity →0 + Scale →1.05 (167 ms accelerate), smoke fade 167 ms, then hide host and restore focus (if the element is still in the tree and focusable; else focus `TxtSearch` on SearchPage).
- Focus trap: `KeyboardNavigation.TabNavigation="Cycle"` on the Frame (Tab never leaves the dialog). The smoke layer intercepts all pointer input. While open, set `KeyboardNavigation.TabNavigation="None"` on the title bar, nav pane and page host containers (restore on close) so programmatic focus moves can't land behind the dialog. Do **not** use `IsEnabled=False` on the background (it would gray it out). Global shortcuts check `IsOverlayOpen` and ignore everything except what the dialog handles (Esc/Enter/Tab).
- Esc → `CloseButtonClick` (if Close button exists, else nothing). Enter → default button when focus isn't in a multi-line TextBox.

### 6.8 ProgressRing (`Controls/ProgressRing : TemplatedControl`)

Properties: `IsActive` (bool, default true), size via `Width/Height` (16, 20, 32 supported), Foreground (default `AccentFillColorDefaultBrush`; on accent buttons `TextOnAccentFillColorPrimaryBrush`; disabled `TextFillColorDisabledBrush`).
Template: `Panel` → `Arc` (Avalonia.Controls.Shapes.Arc) `StrokeThickness` = 2 (16/20) or 3 (32), `StrokeLineCap=Round`, `StartAngle=-90`, `SweepAngle` animated; parent `Panel` has `RenderTransform=rotate`.
Animations (style `:active`): rotation 0→360°, 1000 ms, linear, infinite; SweepAngle 30→270→30, 1500 ms, `SineEaseInOut`, infinite. Animations run only when `IsActive && IsEffectivelyVisible` (use pseudo-class `:active` set in `OnPropertyChanged` and in `OnAttached/DetachedFromVisualTree`). `AutomationProperties.Name="Loading"`, control type ProgressBar.

### 6.9 Title-bar status chips (style `Button.chip` inside AppTitleBar)

Anatomy: Height 28, CornerRadius 14, Padding `10,0`, Background `SubtleFillColorTransparent`, BorderBrush `ControlStrokeColorDefault` 1, hover `SubtleFillColorSecondary`, pressed `SubtleFillColorTertiary`. Content: `[Ellipse 8 dot] 6 [caption label]`. Each chip has `ElementRole=User` and a `Flyout` (Placement `BottomEdgeAlignedRight`, Width 320, Padding 16).

| Chip | Session state | Dot | Label (full / short) | Tooltip |
|---|---|---|---|---|
| AD | NotConfigured | hollow ring `ControlStrongStrokeColorDefault` | "Active Directory" / "AD" | "Active Directory isn't configured" |
| | NotConnected | hollow ring | same | "Not signed in. You'll be asked on your next search." |
| | Connecting | `SystemFillColorAttention`, opacity pulse 0.4↔1, 1 s | same | "Connecting…" |
| | Connected | `SystemFillColorSuccess` | same | "Connected to {server}" |
| | Skipped | `SystemFillColorCaution` | same | "Skipped for this session" |
| | Failed | `SystemFillColorCritical` | same | "Connection failed" |
| Entra | Unverified | hollow ring | "Entra ID" / "Entra" | "Not verified yet. You'll be asked to sign in on your first search if needed." |
| | WaitingForSignIn | Attention, pulsing | same | "Waiting for sign-in" |
| | SignedIn | Success | same | "Signed in" |
| | Failed | Critical | same | "Sign-in failed" |

AD flyout: bodyStrong "Active Directory"; ResultField-style rows (no copy): Status, Server "{server} · {LDAPS|StartTLS|None} · {port}", Signed in as `{LdapBoundUser}`, Base DN (mono) — rows shown when known; last error (caption critical) when Failed; actions row: Connected → "Disconnect" (`Icon.SignOut16`); NotConnected/Skipped/Failed → accent "Sign in" (opens AdCredentialDialog purpose Search without searching); always "Settings" (HyperlinkButton → Settings page). NotConfigured → only "Set up in Settings" accent.
Entra flyout: Status, Tenant `{GraphTenantId}`; actions: WaitingForSignIn → accent "Show sign-in code" (reopens DeviceCodeDialog with `PendingDeviceCode`); always "Settings". No sign-out (not supported by `GraphAuthService`; FUTURE).
Chips update on `Session.StateChanged`. Chip `AutomationProperties.Name` = "{Label}: {tooltip}".

### 6.10 ToastHost (`Controls/ToastHost : UserControl`)

Bottom-center of the **content frame** (not the pane), margin-bottom 16. Toast: `Border` radius 8, Padding `12,8`, Background `SolidBackgroundFillColorQuarternaryBrush`, BorderBrush `SurfaceStrokeColorDefault`, BoxShadow `ShadowFlyout`, MaxWidth 400; content `[PathIcon 16 (Success: Icon.CheckCircle success; Info: Icon.Info attention; Error: Icon.Error critical)] 8 [body]`. One toast at a time (new replaces current). Enter: fade + TranslateY 8→0 167 ms; visible 2000 ms (Error 4000); exit fade 167 ms. `IsHitTestVisible=False`. Text also sent to `Announce`.

### 6.11 Button variants (in `Themes/AppStyles.axaml`)

| Class | Spec |
|---|---|
| (none) | Fluent standard button. Height 32, Padding `11,5,11,6`, radius 4. |
| `accent` | Fluent built-in accent class; colors via §3.1.1 overrides. Remove the legacy `Button.accent` override (SemiBold/6 radius). |
| `subtle` | Background `SubtleFillColorTransparent`, BorderThickness 0, hover `SubtleFillColorSecondary`, pressed `SubtleFillColorTertiary`, disabled foreground `TextFillColorDisabled`. Replaces legacy `Button.chrome` (which hard-coded `#22FFFFFF`). Style the `/template/ ContentPresenter#PART_ContentPresenter` for pointerover/pressed as Fluent does. |
| `icon` | Combine with subtle: Width/Height 32 (override per use: 28/36/40), Padding 0, content `PathIcon` 16 centered. Must have `ToolTip.Tip` and `AutomationProperties.Name`. |
| `chip` | §6.9 |
| `copy` | Applied by `CopyButton`; = subtle icon. |

`Cursor=Hand` is **not** applied to buttons (not a Windows convention); only HyperlinkButtons get it (Fluent default).

### 6.12 SettingsCard (`Controls/SettingsCard : ContentControl`)

Properties: `Header` (string), `Description` (object — string or rich content such as the test-result line), `HeaderIcon` (Geometry?), `IsClickEnabled` (bool), `ActionIcon` (Geometry?, default `Icon.ChevronRight` when clickable). `Content` = the right-side control. Event `Click` (when clickable). Pseudo-classes `:clickable`, `:wrapped`, `:nested`, `:header` (plus standard `:pointerover :pressed :disabled :focus-visible`).

Anatomy (CommunityToolkit SettingsCard metrics):
```
┌──────────────────────────────────────────────────────────────────────────────────────┐ MinHeight 68, Padding 16, radius 8
│ [icon 20] 20 │ Header (body)                                    │ 24 │ [Content]  [›]  │ stroke CardStrokeColorDefault
│              │ Description (caption secondary, wrap)            │    │ right-aligned   │ bg CardBackgroundFillColorDefault
└──────────────────────────────────────────────────────────────────────────────────────┘
```
- Grid columns `Auto (icon, margin 2,0,20,0), * (text), Auto (content, margin-left 24, MinWidth 120), Auto (action icon 16, margin-left 12)`.
- `:wrapped` (set in `OnSizeChanged` when `Bounds.Width < 476`): content moves to a second row, `Grid.Column=1`, `HorizontalAlignment=Stretch`, margin-top 12; TextBox/ComboBox inside stretch (`MinWidth=200`).
- `:clickable`: `Focusable=True`, `Cursor` default, pointerover bg `ControlFillColorSecondary`, pressed `ControlFillColorTertiary`, Enter/Space raise `Click`, `AutomationProperties.ControlTypeOverride=Button`, Name = Header.
- `:disabled`: header/description `TextFillColorDisabled`.
- Non-clickable cards are not focusable (their inner control is).
- `:nested` (inside a SettingsExpander): no background, no border, radius 0, Padding `58,8,48,8` (aligns text with the expander header text), MinHeight 52; a 1 px `DividerStrokeColorDefault` top border except for the first item.
- `:header` (used as an Expander header): no background/border/padding; the Expander supplies them.

### 6.13 SettingsExpander (style `Expander.settings`, no new class)

- Uses Avalonia `Expander` (`ExpandDirection=Down`) with `Classes="settings"`. Header = `SettingsCard Classes="header"` (icon, header, description); Avalonia's chevron stays at the right.
- `Style.Resources` overrides for `Expander.settings`: `ExpanderHeaderBackground*`=`CardBackgroundFillColorDefaultBrush` (PointerOver: `ControlFillColorSecondaryBrush`, Pressed: `ControlFillColorTertiaryBrush`), `ExpanderHeaderBorderBrush*`=`CardStrokeColorDefaultBrush`, `ExpanderHeaderPadding`=`16`, `ExpanderContentBackground`=`CardBackgroundFillColorSecondaryBrush`, `ExpanderContentBorderBrush`=`CardStrokeColorDefaultBrush`, `ExpanderContentPadding`=`0`. Header MinHeight 68, CornerRadius 8 (bottom corners 0 when expanded — Fluent's template handles this via its `CornerRadius` filter converters).
- Content: `StackPanel` of `SettingsCard Classes="nested"` rows and, where specified, inline InfoBars (margin `58,8,16,8`).
- Expand/collapse uses Fluent's `ContentTransition`; keep default.

---

## 7. Interaction & motion

### 7.1 Keyboard shortcuts

Handled in `MainWindow` via `AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel)` so focused TextBoxes don't swallow them. Primary modifier = `PlatformSettings.HotkeyConfiguration.CommandModifiers` (Ctrl on Windows/Linux, Cmd on macOS). While `IsOverlayOpen`, only Esc/Enter/Tab are processed (by the dialog).

| Shortcut | Action | Scope |
|---|---|---|
| Ctrl+L, Ctrl+F | Navigate to Search (with unsaved prompt if leaving Settings), focus `TxtSearch`, `SelectAll()` | global |
| Enter | Locate | `TxtSearch` focused |
| Esc | If idle on Search: clear box + results → Idle state, focus box. If searching: nothing. On other pages: nothing. | Search page |
| Ctrl+Shift+C | Copy summary (§7.5); toast "Summary copied" or Info toast "Nothing to copy yet" | Search page |
| Ctrl+1 / Ctrl+2 | Open identity / mailbox portal of current result | Search page |
| F5, Ctrl+R | Search again (last identity) | Search page |
| Ctrl+, | Navigate to Settings | global |
| Ctrl+S | Save settings | Settings page with SaveBar |
| Down / Up | Search box ↔ candidate list | Search page, Candidates state |
| Alt+Space | Window system menu (OS) | Windows |

Shortcut labels in tooltips and the empty state are generated per OS ("Ctrl+Shift+C" vs "⌘⇧C").

### 7.2 Focus order & initial focus

- Launch: `Opened` → `TxtSearch.Focus()`.
- Tab order (visual tree order, no TabIndex): title-bar pane toggle → AD chip → Entra chip → nav items (Search, About, Settings) → page content. Search page: `TxtSearch` → Locate → InfoBar actions/close buttons (top to bottom) → Copy summary → Search again → Identity card buttons → Mailbox card buttons → detail-card copy buttons in reading order (left column then right column). Note: macOS title-bar chips are also tab stops.
- After a search completes: focus returns to `TxtSearch` with text selected. For the Candidates state, focus also stays in the box (Down moves into the list).
- After a dialog closes: focus restored (§6.7).

### 7.3 Loading behavior

- Everything network-bound is already async/off-thread in services; UI never blocks. Global `_isSearching` guard (legacy) prevents concurrent searches.
- Show the indeterminate bar immediately (no delay) — searches take ≥ 300 ms anyway and the bar confirms the keypress.
- Settings "Test connection" buttons are independent; each has its own guard (legacy `_isTestingLdap/_isTestingGraph`).
- The page stays navigable during a search (the search continues; returning to Search shows its current state).

### 7.4 Copy feedback

- Per field: CopyButton icon swap (1500 ms) + announcement; no toast.
- Copy link / Copy all / Copy summary / Copy version / Copy path / Copy code: toast.
- Clipboard failures (null clipboard): Error toast "Couldn't access the clipboard."

### 7.5 Copy summary format (`ResultSummaryFormatter.Format(result, context)`)

Plain text, labels padded with spaces to 22 chars (no tabs — ticket systems collapse them), local time with UTC offset, omit lines whose value is absent:

```
hybrID lookup: jsmith (2026-09-23 14:02 -07:00)
Name:                 John Smith
Object type:          user
Sync state:           Hybrid (synced to Entra ID)
Account:              Enabled
Password:             Last set 2026-03-14 (193 days ago)
Identity managed in:  Active Directory
Mailbox managed in:   Exchange Server ECP (remote mailbox)
Mailbox link:         https://mail.contoso.com/ecp/UsersGroups/EditRemoteMailbox.aspx?...&id=...
Identity link:        (none)
Last synced:          2026-09-23 13:45 -07:00
Primary SMTP:         john.smith@contoso.com
Proxy addresses:      SMTP:john.smith@contoso.com; smtp:jsmith@contoso.mail.onmicrosoft.com
Licenses:             Microsoft 365 E3
Notes:                Mailbox is in EXO, but managed as a Remote Mailbox via On-Premises Exchange.
Coverage:             Active Directory: match; Entra ID: match
```
`context` carries the searched identity, coverage states and the verdict titles (so the text matches the screen). Dates in ISO `yyyy-MM-dd` (culture-neutral for tickets).

### 7.6 Error handling UX

- Recoverable directory problems → Caution InfoBars with a fix action (never a modal).
- Configuration problems (Graph init, invalid settings) → Critical InfoBar with "Open settings".
- Unexpected exceptions in UI handlers → caught at the handler, Critical `MessageDialog` "Something went wrong" + `ex.Message`; app keeps running. (FUTURE: "Open logs folder" action once error logging exists.)
- Validation → inline on the control (`invalid` class + caption), never InfoBar.

### 7.7 Motion summary

| Element | Motion |
|---|---|
| Page change | CrossFade 167 ms + new page Opacity/TranslateY 16→0 250 ms decelerate |
| Results | staggered fade/slide 12 px, 250 ms, 40 ms stagger |
| Dialog | smoke fade 167; frame scale 1.05→1 + fade 250 in; reverse 167 accelerate out |
| InfoBar | fade in 167 |
| Toast | fade + 8 px rise 167 in, fade 167 out |
| Nav pill | scaleY 0→1 167 |
| SaveBar | 16 px rise + fade 250 |
| Hover/press colors | BrushTransition 83 |
| Pane open/close | Fluent SplitView defaults (`SplitViewPaneAnimationOpenDuration`) |
| Chip "connecting" dot | opacity pulse 1 s loop |

Avalonia exposes no cross-platform reduced-motion setting; motions are ≤ 250 ms, non-essential, and nothing loops except loading indicators.

---

## 8. Roadmap accommodation

| Roadmap item | Where it goes | Build now? |
|---|---|---|
| Copy results to clipboard for tickets | Result header "Copy summary" button + Ctrl+Shift+C (§7.5) | **Yes** (pure presentation) |
| Expanded object support (shared/room/equipment mailboxes, M365 groups) | (a) Result header pill slot #4 (mailbox type); (b) ManagementCard `TagText` ("Shared mailbox"…); (c) avatar icon: `Icon.MailMultiple20` shared, `Icon.ConferenceRoom20` room, `Icon.Toolbox20` equipment, `Icon.PeopleTeam20` M365 group; (d) Mail card row "Mailbox type" | Slots only; needs model `MailboxType` |
| Graceful error logging | Settings > Configuration "Logs" card with "Open logs folder"; Critical InfoBars/MessageDialog get action "Open logs folder" | Omit until implemented |
| M365 license display | Licenses card (§5.1.5) — data already exists in the model | **Yes** |
| Last sign-in date | Account card row "Last sign-in: {g} · {relative}"; if > 90 days add Caution pill "Inactive 90+ days" to header slot | Omit row until model has `LastSignInUtc` (needs `AuditLog.Read.All` + Entra P1; changes Graph scopes → consent prompt) |
| Bulk search | New nav item "Bulk search" (`Icon.PeopleSearch20`) under Search in the top group → `Views/Pages/BulkSearchPage`: left multi-line TextBox (one identity per line, paste-friendly) + accent "Locate all"; results list (ListBox rows 48 h: name, identity location pill, mailbox location pill, account pill, open buttons) + "Copy as table" (TSV) | Omit nav item until built (no "coming soon" items) |
| Intune primary device + deep link | New detail card "Devices" (`Icon.Laptop20`) in the right column after Licenses: device name, OS, compliance pill, last check-in; action "Open in Intune" (accent) | Omit until built |
| Automated tests | n/a (no UI) | – |

Rule: never render disabled placeholders or fake data for future features. The layout skeleton (grid columns, pill slot ordering, card order) already accommodates them.

---

## 9. Icons

All geometry follows the generation rules in the `Icons.axaml` header (Fluent UI System Icons Regular unless noted, re-tokenized, F1, corner anchors). Render with `PathIcon` at the icon's grid size (16 or 20; 12 for `…12`).

### 9.1 Existing keys (keep)

Source names are inferred from key names and grid size; verify against the path data when regenerating.

| Key | Grid | Fluent source (presumed) | Used for |
|---|---|---|---|
| `Icon.Search` | 20 | ic_fluent_search_20_regular | Nav Search |
| `Icon.Search16` | 16 | ic_fluent_search_16_regular | Search box inner icon |
| `Icon.Dismiss` | 16 | ic_fluent_dismiss_16_regular | InfoBar close |
| `Icon.Dismiss12` | 12 | ic_fluent_dismiss_12_regular | Search clear |
| `Icon.Settings` | 20 | ic_fluent_settings_20_regular | Nav Settings, Advanced expanders |
| `Icon.Copy` | 16 | ic_fluent_copy_16_regular | CopyButton |
| `Icon.Open` / `Icon.Open12` | 16/12 | ic_fluent_open_16/12_regular | External links |
| `Icon.CheckCircle` | 16 | ic_fluent_checkmark_circle_16_regular | Enabled pill, coverage match, toasts |
| `Icon.Check` | 16 | ic_fluent_checkmark_16_regular | Copied state, license rows |
| `Icon.Warning` | 16 | ic_fluent_warning_16_regular | Coverage caution, inline cautions |
| `Icon.Error` | 16 | ic_fluent_error_circle_16_regular | Validation, error toast |
| `Icon.Info` | 16 | ic_fluent_info_16_regular | Info toast, SaveBar |
| `Icon.Unknown` / `Icon.Unknown20` | 16/20 | ic_fluent_question_circle_16/20_regular | Unknown object avatar, no-result |
| `Icon.Lock` | 16 | ic_fluent_lock_closed_16_regular | Locked-out pill, credential note |
| `Icon.Prohibited` | 16 | ic_fluent_prohibited_16_regular | Disabled pill |
| `Icon.Key` | 16 | ic_fluent_key_16_regular | Password never expires |
| `Icon.Clock` | 16 | ic_fluent_clock_16_regular | Code expiry |
| `Icon.Sync` | 16 | ic_fluent_arrow_sync_16_regular | Hybrid pill |
| `Icon.Cloud` / `Icon.Cloud20` | 16/20 | ic_fluent_cloud_16/20_regular | Cloud pill / card / Graph settings |
| `Icon.Building` / `Icon.Building20` | 16/20 | ic_fluent_building_16/20_regular | On-prem pill / card |
| `Icon.Person` / `Icon.Person20` | 16/20 | ic_fluent_person_16/20_regular | Account card, user avatar |
| `Icon.Group` / `Icon.Group20` | 16/20 | ic_fluent_people_16/20_regular | Group card, group avatar |
| `Icon.Mail` / `Icon.Mail20` | 16/20 | ic_fluent_mail_16/20_regular | Mail card, Exchange settings |
| `Icon.MailOff` / `Icon.MailOff20` | 16/20 | ic_fluent_mail_off_16/20_regular | No mailbox |
| `Icon.ChevronRight` / `Icon.ChevronDown` | 16 | ic_fluent_chevron_right/down_16_regular | Candidates, clickable cards, expanders |
| `Icon.NotSet` | 16 | (presumed ic_fluent_subtract_circle_16_regular) | Coverage "no match / not configured" |
| `Icon.Disconnected` / `Icon.Disconnected16` | 20/16 | ic_fluent_plug_disconnected_20/16_regular | Chip flyout failed state |
| `Icon.Refresh` | 16 | ic_fluent_arrow_clockwise_16_regular | Search again |
| `Icon.CopySummary` | 16 | (presumed ic_fluent_clipboard_text_ltr_16_regular) | Copy summary |
| `Icon.Eye` / `Icon.EyeOff` | 16 | ic_fluent_eye_16 / eye_off_16_regular | Password reveal |
| `Icon.Back` | 16 | ic_fluent_arrow_left_16_regular | Unused in v1 (keep) |
| `Icon.Folder` | 16 | (presumed ic_fluent_folder_open_16_regular) | Open folder |
| `Icon.Theme` | 20 | (presumed ic_fluent_dark_theme_20_regular) | App theme card |

### 9.2 New icons to add

| New key | Fluent source | Used for |
|---|---|---|
| `Icon.Navigation` | ic_fluent_navigation_16_regular | Pane toggle |
| `Icon.Info20` | ic_fluent_info_20_regular | Nav About, About card |
| `Icon.InfoFilled` | ic_fluent_info_16_filled | InfoBar Informational |
| `Icon.CheckCircleFilled` | ic_fluent_checkmark_circle_16_filled | InfoBar Success |
| `Icon.WarningFilled` | ic_fluent_warning_16_filled | InfoBar Warning |
| `Icon.ErrorFilled` | ic_fluent_error_circle_16_filled | InfoBar Error |
| `Icon.Server20` | ic_fluent_server_20_regular | Domain controller card, System info |
| `Icon.ShieldLock20` | ic_fluent_shield_lock_20_regular | Encryption card |
| `Icon.NumberSymbol20` / `Icon.NumberSymbol16` | ic_fluent_number_symbol_20/16_regular | Port card; ObjectGUID format card |
| `Icon.PlugConnected20` / `Icon.PlugConnected16` | ic_fluent_plug_connected_20/16_regular | Test connection cards; chip flyout connected |
| `Icon.Link` / `Icon.Link20` | ic_fluent_link_16/20_regular | Copy link; Link templates expander |
| `Icon.ArrowReset` | ic_fluent_arrow_reset_16_regular | Reset template/client ID |
| `Icon.Document20` | ic_fluent_document_20_regular | Configuration file card |
| `Icon.ContactCard16` | ic_fluent_contact_card_16_regular | Identity details card |
| `Icon.Ribbon16` | ic_fluent_ribbon_16_regular | Licenses card |
| `Icon.SignOut16` | ic_fluent_sign_out_16_regular | AD Disconnect |
| `Icon.Keyboard20` | ic_fluent_keyboard_20_regular | Empty-state shortcuts header |
| `Icon.Code20` | ic_fluent_code_20_regular | About: source code |
| `Icon.Bug20` | ic_fluent_bug_20_regular | About: report an issue |
| `Icon.Certificate20` | ic_fluent_certificate_20_regular | About: license |
| `Icon.Box20` | ic_fluent_box_20_regular | About: third-party software |
| FUTURE `Icon.PeopleSearch20` | ic_fluent_people_search_20_regular | Bulk search nav |
| FUTURE `Icon.Laptop20` | ic_fluent_laptop_20_regular | Devices card |
| FUTURE `Icon.MailMultiple20` | ic_fluent_mail_multiple_20_regular | Shared mailbox |
| FUTURE `Icon.ConferenceRoom20` | ic_fluent_conference_room_20_regular | Room mailbox |
| FUTURE `Icon.Toolbox20` | ic_fluent_toolbox_20_regular | Equipment mailbox |
| FUTURE `Icon.PeopleTeam20` | ic_fluent_people_team_20_regular | M365 group |
| FUTURE `Icon.DocumentText20` | ic_fluent_document_text_20_regular | Logs card |

---

## 10. Accessibility

- **Contrast**: all body/caption text on its surface ≥ 4.5:1 (Primary/Secondary text tokens, pills ≥ 5.9:1, accent button text 4.52:1 rest / 5.2:1 hover / 6.3:1 pressed). Tertiary text is only for placeholders and "—". Non-text UI (focus ring, nav pill, dots, input borders) ≥ 3:1 (`#007ACC` on `#202020` = 3.5:1).
- **Never color alone**: pills, coverage items, chips and InfoBars always pair color with icon + text; chip dots have text tooltips/names.
- **Focus**: Fluent focus adorner (2 px outer `FocusStrokeColorOuter` + 1 px inner) on every focusable element; custom templates implement `:focus-visible`. Focus never lands on non-interactive cards.
- **Automation names**: every icon-only button (pane toggle, clear, copy, copy link, refresh, reveal, InfoBar close) has `AutomationProperties.Name` and a ToolTip with the same text. Search box: Name "Identity to search". Nav pane: `LandmarkType=Navigation`; search header: `LandmarkType=Search`; page host: `LandmarkType=Main`.
- **Headings**: page titles and result name `HeadingLevel=1`; section headers, card titles, management-card location names `HeadingLevel=2`.
- **Live regions**: `Announcer` TextBlock (`LiveSetting=Polite`) for results, copies, toasts; InfoBar Error uses Assertive; search phase text Polite.
- **Hit targets**: icon buttons ≥ 32×32 (28×28 only inside dense lists with ≥ 8 px spacing: proxy rows, reveal button); text buttons ≥ 32 high (36 in cards, 40 for Locate); nav items 36 high.
- **Keyboard**: every action reachable without a mouse (§7.1–7.2); dialogs trap focus and support Esc/Enter.
- **Text scaling**: no fixed heights on text containers (use MinHeight); cards wrap.
- **High contrast**: disable backdrop (§4.3). Full HC theme is out of scope.

---

## 11. Model / service additions (flagged)

Existing routing logic in `IdentityLookupService` is **not changed**. These are additive properties populated alongside existing assignments.

### [M1] Display-only additions (recommended for Phase 4; required for the full verdict)

`Models/IdentityLookupResult.cs`:
```csharp
public enum MailboxLocation { None, NotApplicable, OnPremMailbox, RemoteMailbox, OnPremMailEnabledGroup, ExchangeOnline }
public enum ObjectKind { User, Group, Other }

// on IdentityLookupResult (all init-only, nullable/defaulted):
public MailboxLocation? MailboxLocation { get; init; }     // set in the same branches that set `notes`/`manageExchange`
public ObjectKind? ObjectKind { get; init; }
public string? SamAccountName { get; init; }
public string? UserPrincipalName { get; init; }
public string? Mail { get; init; }
public Guid? ObjectGuid { get; init; }
public string? DistinguishedName { get; init; }
public string? TargetAddress { get; init; }
public string? EntraObjectId { get; init; }                // graphObject?.Id
public bool? EntraSyncEnabled { get; init; }               // graphObject?.SyncEnabled
public IReadOnlyList<ManagementLink> ViewLinks { get; init; } = Array.Empty<ManagementLink>();
    // Hybrid: AsLink("View in Entra ID", entraUrl); RemoteMailbox + graph found: AsLink("View in Exchange Online", exoUrl)
    // (both URLs are already computed in BuildResult but currently discarded for AD-found objects)
```
`AmbiguousCandidate`: add `public CandidateSource Source { get; init; }` (`enum CandidateSource { ActiveDirectory, EntraId }`), set in `BuildAmbiguousResult`.

Population is one line per field in `BuildResult` / `BuildAdFoundResult` / `BuildAmbiguousResult`. `BuildAdFoundResult` needs `entraUrl`/`exoUrl` passed in (signature change of a private method only).

### [M2] Service-behavior additions (optional, later)

- `IdentityLookupResult.AdError` / `GraphError` (string?): set in `FindAdCandidatesAsync` / the Graph `catch` blocks instead of silently swallowing. Replaces the GRAPH-SUSPECT heuristic and detects dead cached LDAP connections.
- `CancellationToken` through `LookupAsync` → `GraphIdentityService` calls, enabling a real "Cancel" for device-code sign-in and long searches (Esc during search would then cancel).
- Graph sign-out (clear token cache) in `GraphAuthService` → Entra chip "Sign out".

---

## 12. Implementation notes, pitfalls & phased checklist

### 12.1 Verified Avalonia 12.1.3 facts (from the reference assemblies)

- **Removed/renamed chrome API**: there is no `ExtendClientAreaChromeHints` and no `SystemDecorations`. Use `Window.WindowDecorations` (`Full | BorderOnly | None`), `ExtendClientAreaToDecorationsHint`, `ExtendClientAreaTitleBarHeightHint`, `WindowDecorationMargin`, `IsExtendedIntoWindowDecorations`, and the attached property `WindowDecorationProperties.ElementRole` (`Avalonia.Controls.Chrome` namespace; values in `Avalonia.Input.WindowDecorationsElementRole`: `None, DecorationsElement, User, TitleBar, Resize*, CloseButton, MinimizeButton, MaximizeButton, FullScreenButton`). `ElementRole` only works when the window is extended. Elements with role `None` are invisible to chrome hit-testing, so **interactive children of a `TitleBar`-role element need `ElementRole=User`**.
- Not in Avalonia 12: ProgressRing, InfoBar, NavigationView, ContentDialog, SettingsCard, TeachingTip, NumberBox. Available and used: `SplitView`, `TransitioningContentControl` (+ `CrossFade`, `PageSlide`, `CompositePageTransition`), `HyperlinkButton`, `Expander`, `ToggleSwitch`, `NumericUpDown`, `SelectableTextBlock`, `Shapes.Arc`, `CommandBar` (not used), `NativeMenu`, container queries (`Container.Name`, `Container.Sizing`, `ContainerQuery`), `SplineEasing`, `TopLevel.TransparencyBackgroundFallback`, `TextBox.RevealPassword`, `TextBox.PlaceholderText`, `Button.IsDefault/IsCancel`, `AutomationProperties.LiveSetting/HeadingLevel/LandmarkType`.
- Fluent theme resource keys that exist and are overridden here: `ControlCornerRadius`, `OverlayCornerRadius`, `ContentControlThemeFontFamily`, `ControlContentThemeFontSize`, `ToolTipContentThemeFontSize`, `SystemAccentColor(Light1-3/Dark1-3)`, `AccentButton*`, `HyperlinkButton*`, `ExpanderHeader*`/`ExpanderContent*`, `TextControlBackground*`, `ToggleSwitchFillOn*`, `FlyoutPresenterBackground`, `ToolTipBackground`, `SystemControlFocusVisual*`. The WinUI token names (`TextFillColorPrimary`, `CardBackgroundFillColorDefault`, …) do **not** exist in Avalonia's Fluent theme — we define them ourselves in `Theme.axaml`.

### 12.2 Pitfalls

1. **Mica needs a transparent window.** `Background="Transparent"` on MainWindow (Fluent's Window theme paints `SystemRegionBrush` otherwise) + `TransparencyBackgroundFallback`. Don't put an opaque Panel at the root. Check `ActualTransparencyLevel` for the macOS `opaque` class.
2. **Popups don't get Mica/vibrancy** — give flyouts, tooltips, ComboBox dropdowns solid backgrounds (§4.3).
3. **Live theme switching**: only `DynamicResource` for brushes; never `this.FindResource(...)` + assignment in code (captures the current theme's instance — the legacy badge code has this bug). Toggle `Classes` instead.
4. **Every ThemeDictionary key in both Light and Dark**, or it resolves to nothing in one theme.
5. **Accent shades**: override `SystemAccentColorLight1-3/Dark1-3` explicitly; otherwise Fluent may derive states from the OS accent on some platforms.
6. **Subclassed templated controls** (`AdCredentialDialog : OverlayDialog`, `CopyButton : Button`) need `protected override Type StyleKeyOverride => typeof(Base);` or the ControlTheme won't apply.
7. **Type selectors are exact**: `TextBlock.caption` does not match `SelectableTextBlock`; use `:is(TextBlock).caption`.
8. **Title bar interactivity**: set `ElementRole=User` on chips/pane toggle; test that drag, double-click-maximize and Windows Snap Layouts (hover maximize) still work.
9. **Caption-button reserve (138 px) is an assumption**: at runtime on Windows, verify caption buttons' actual width/height in the extended 48 px bar (Avalonia 12 may draw them via `WindowDecorationsTheme` rather than DWM). Adjust `CaptionReserve` if needed.
10. **macOS**: traffic-light inset 80 px; remove inset in full screen; verify `ExtendClientAreaTitleBarHeightHint=48` behavior (§4.2). Use Cmd shortcuts via `HotkeyConfiguration`.
11. **Linux**: don't extend client area; test on GNOME (Wayland/XWayland) and KDE.
12. **Keyboard shortcuts**: register with `RoutingStrategies.Tunnel`; mark `e.Handled=true` only for handled gestures so TextBox editing keys still work.
13. **Device-code callback is off the UI thread**: `Dispatcher.UIThread.Post` before touching UI or `AppSession` state-changed events.
14. **Container queries** need the container's width to come from its parent (`Container.Sizing="Width"` on a stretched element), and style setters for `Grid.Row/Column` attached properties.
15. **`BoxShadows` and 4-number `Easing` strings in resources/XAML**: verify they parse; fallbacks in §3.5/§3.7.
16. **Cascadia Mono SemiBold**: its legacy family name is "Cascadia Mono SemiBold" (typographic family "Cascadia Mono"). Verify `FontFamily="avares://HybridID/Assets/Fonts#Cascadia Mono"` + `FontWeight=SemiBold` picks the SemiBold file; if not, reference `#Cascadia Mono SemiBold` for the device code.
17. **ProgressRing animations** must stop when hidden (CPU on idle).
18. **No SelectableTextBlock inside ListBox items** (captures pointer, breaks selection).
19. **Page caching**: create pages once in MainWindow; don't recreate on navigation (loses results and in-flight searches).
20. **Settings concurrency**: `IdentityLookupService` holds the same `AppConfig` instance — URL template edits apply on save without rebuilding; connection edits require `ApplySavedConfig(true)` (disposes LDAP, drops Graph client) — never mutate `Session.Config` before Save.
21. **Password handling**: never log or persist; clear the password TextBox on dialog close.
22. **Compiled bindings**: if you use `{Binding}` in DataTemplates (candidate list), set `x:DataType` so they work whether or not compiled bindings are enabled for the project.

### 12.3 Phased checklist

**Phase 0 — Foundations**
- [ ] Rewrite `Themes/Theme.axaml` with all tokens (§3), keep the LEGACY block.
- [ ] Add `Themes/Typography.axaml`, `Themes/AppStyles.axaml` (button variants, flyout/tooltip/ComboBox/Expander overrides), `Themes/Controls.axaml`.
- [ ] Merge `Icons.axaml` in `App.axaml`; add §9.2 non-FUTURE icons.
- [ ] Move `App.axaml` inline styles into `AppStyles.axaml`; delete legacy `Button.accent`/`Button.chrome` overrides only after Phase 7 (legacy views use them) — until then scope new variants under different class names (`subtle`) as specified.
- [ ] Verify both themes render Fluent controls with the new accent.

**Phase 1 — Shell**
- [ ] `Services/AppSession.cs` (port logic from `LegacyMainWindow`/`LegacySettingsWindow`).
- [ ] `Views/IAppShell.cs`, `Views/MainWindow.axaml(.cs)`: chrome per OS, backdrop chain, SplitView + nav rail, page host with transitions, OverlayHost, ToastHost, Announcer, shortcut handler, window-closing unsaved guard.
- [ ] `Controls/AppTitleBar` with chips (states wired to `Session.StateChanged`).
- [ ] macOS `NativeMenu`.

**Phase 2 — Controls**
- [ ] InfoBar, SettingsCard (+ Expander `settings` style), StatusPill, ResultField, ResultListField, CopyButton, ManagementCard, OverlayDialog, ProgressRing, NavRailItemTheme, Button.chip.
- [ ] Temporary gallery: a DEBUG-only page or the previewer to check every control in Light/Dark (don't ship).

**Phase 3 — Dialogs**
- [ ] AdCredentialDialog (inline retry), DeviceCodeDialog (hide/reopen, expiry), MessageDialog.

**Phase 4 — Search page**
- [ ] [M1] model additions (or the interim mapping in §5.1.5 if rejected).
- [ ] `Services/DisplayFormat.cs`.
- [ ] All states §5.1.1–5.1.9, InfoBars §5.1.6, coverage §5.1.7, entrance motion, announcements.
- [ ] Copy summary (`Services/ResultSummaryFormatter.cs`).

**Phase 5 — Settings & About pages**
- [ ] SettingsPage with SaveBar, validation, immediate theme, test connections, templates, config file card, unsaved prompt.
- [ ] AboutPage.

**Phase 6 — Verification**
- [ ] Keyboard-only pass (every flow), screen reader smoke test (Narrator on Windows, VoiceOver on macOS).
- [ ] Light/Dark/System switching live on every page and dialog.
- [ ] OS matrix: Windows 11 (Mica, Snap Layouts), Windows 10 (solid fallback, Segoe UI), macOS (traffic lights, vibrancy + opaque frame, Cmd shortcuts), Linux GNOME + KDE (native decorations).
- [ ] Real or Samba AD DC: on-prem mailbox, remote mailbox, no mailbox, mail-enabled group, group, AD unreachable, skip, bad credentials (inline retry).
- [ ] Graph: first sign-in (device code, hide/reopen), cached token, cloud-only user/group, ambiguous group names.
- [ ] Window sizes: 640×560 min, default, maximized (container-query breakpoints 600/760, pane 1008).

**Phase 7 — Cleanup & docs**
- [ ] Delete `Views/Legacy/*` and the LEGACY token block and legacy button classes.
- [ ] Update `CLAUDE.md` + `docs/contributing.md` (views list; "Adding a new setting" now: 1) model + default JSON, 2) SettingsCard in `SettingsPage.axaml`, 3) populate/stage/save in `SettingsPage.axaml.cs`), `docs/getting-started.md` (Settings is in the nav, no status bar, AD skip, device-code dialog), `README.md` feature list, screenshots, `docs/changelog.md` (0.3.0 "Changed: Windows 11-style UI…"), `build/version.json` + `AppVersion.Current`.

### 12.4 Open risks

| # | Risk | Impact | Mitigation in this spec |
|---|---|---|---|
| R1 | Avalonia 12 chrome API is new (`WindowDecorations`, `ElementRole`); caption-button rendering/size in a 48 px extended bar on Win32 and traffic-light placement on macOS are unverified. | Title bar layout may need per-OS tweaks. | Reserve values are isolated in AppTitleBar (`CaptionReserve`, 80 px macOS inset); verify first in Phase 1. Linux doesn't extend at all. |
| R2 | Graph failures (auth, network, throttling) are swallowed by `IdentityLookupService` and look identical to "not found". | Hybrid user may show as On-premises only; partial failure UX is heuristic. | GRAPH-SUSPECT InfoBar + coverage line; proper fix is [M2] `GraphError`. |
| R3 | Device-code flow and lookups have no cancellation. | "Cancel" is impossible; dialog offers "Hide", search waits until sign-in completes or the code expires (~15 min). | Hide + reopen via Entra chip; [M2] CancellationToken. |
| R4 | When AD is skipped/unreachable/not configured, a synced object is routed as "Cloud User / Cloud-only" by the service. | Technician might edit a synced object in the cloud. | Caution pill text "Cloud-only (AD not checked)", AD InfoBars, coverage line, [M1] `EntraSyncEnabled` InfoBar. |
| R5 | A cached LDAP connection that dies (DC restart, idle timeout) makes AD lookups silently return "no match". | False "Cloud-only"/not-found. | Coverage line wording is honest ("no match"); [M2] `AdError` + reconnect. |
| R6 | [M1] additions are needed for the robust verdict and copyable IDs; the interim string-matching fallback breaks if `Notes` wording changes. | Wrong mailbox card. | Prefer M1 (one line per field); keep interim constants in one method referencing the service. |
| R7 | `GraphIdentityService.KnownSkus` contains duplicate GUID keys with indexer assignment (`06ebc4ee-…` = both "Microsoft 365 E5" and "Microsoft 365 E3 (no Teams)"; `c7df2760-…` = both "Microsoft 365 E5 (no Teams)" and "Office 365 E5"). The later value silently wins. | Licenses card shows wrong SKU names. | Out of UI scope; fix the map in the service (verify GUIDs against Microsoft's SKU list). |
| R8 | Service returns an Exchange Online link for every cloud user/group, including security groups and unlicensed users. | Mailbox card offers a link that 404s. | Displayed as-is; future `MailboxType`/license-aware routing. |
| R9 | AD member counts cap at 1,500 values (no ranged retrieval). | Undercount. | "1,500+" display rule. |
| R10 | Mica/vibrancy behavior differs by OS/compositor; semi-transparent layer tokens over the solid fallback must still look right. | Visual inconsistency. | Fallback chain + `TransparencyBackgroundFallback`; macOS `opaque` frame; test matrix Phase 6. |
| R11 | Parser/format uncertainties: `BoxShadows` as keyed resources, 4-number `Easing` strings, container-query setters on attached properties, Cascadia SemiBold family resolution. | Build/runtime XAML errors. | Fallbacks noted in §3.5, §3.7, §12.2 (#14–16). |
| R12 | Screen-reader live-region support differs (strong on Windows UIA, partial on macOS, weak on Linux AT-SPI). | Announcements may be missed off-Windows. | Visible equivalents exist for every announcement (coverage line, InfoBars, toasts). |

---

## 13. Appendix

### 13.1 Legacy → new mapping (nothing is lost)

| Legacy element | New home |
|---|---|
| `TxtSearch`, `BtnSearch` "Locate" | Search header (§5.1.2) |
| Info "i" tooltip (formats + shortcuts) | Hint line + empty-state format cards + shortcut list |
| `AmbiguousPanel` / `CandidateListPanel` | Candidates state (§5.1.9) |
| `SyncBadge`, `AccountBadge` | StatusPills in result header |
| `ValName` + Copy | Identity details "Display name" ResultField |
| `ValClass` | "Object type" row + header subline |
| `ValPassword` | Account card "Password" |
| `TxtManageIdentity` (+ link click) + "Copy Link" | Identity ManagementCard (primary button + copy link) |
| `ValNotes` | Guidance InfoBar |
| `ValProxyAddresses` + Copy | Mail card ResultListField (+ Copy all with prefixes) |
| `TxtManageExchange` + "Copy Link" | Mailbox ManagementCard |
| `SyncSection` Last synced | Account card "Last synced to Entra ID" |
| `DetailsExpander` group type / member count / licenses | Group card / Licenses card |
| Status bar "Initializing…/Ready" | Removed; setup InfoBar + chips |
| Status bar "Ready (configure AD…)" | SETUP InfoBar + AD chip NotConfigured |
| Status bar "Searching … for 'x'" | Progress bar + searching state |
| Status bar result/error messages | Coverage line, InfoBars (§5.1.6), announcer |
| Status bar "(Active Directory unreachable: …)" suffix | AD-UNREACHABLE InfoBar + coverage |
| `BtnSettings`, `BtnAbout` | Nav footer items |
| Ctrl/Cmd+L, Esc | Kept (§7.1) plus new shortcuts |
| SettingsWindow tabs Exchange / Active Directory / Microsoft Graph / Appearance | Settings sections |
| Test Connection (LDAP/Graph) + status text | Test connection SettingsCards |
| Apply / Cancel | SaveBar Save / Discard (+ unsaved prompt) |
| AlertDialog "Settings Saved" | Success InfoBar on Settings page |
| AboutWindow (version, author, MIT, GitHub link, Close) | AboutPage |
| AdCredentialDialog (username, password, Sign In, Cancel) | AdCredentialDialog overlay (Sign in / Skip, reveal, inline error) |
| DeviceCodeDialog (message) | DeviceCodeDialog overlay (code, copy+open, expiry, hide) |
| (hand-edit only) `graphClientId`, `managementUrls` | Graph Advanced + Link templates (now editable) |

### 13.2 Copy catalogue (strings not already given inline)

- Window title: "hybrID".
- Nav: "Search", "About", "Settings"; pane toggle "Collapse navigation" / "Expand navigation".
- Empty state: "Find where an identity is managed" / "Search Active Directory and Entra ID at once, then jump to the right admin portal." / format labels "sAMAccountName", "User principal name", "ObjectGUID" / "Keyboard shortcuts".
- Shortcut labels: "Focus search", "Locate", "Clear", "Copy summary", "Open identity portal", "Open mailbox portal", "Search again", "Settings".
- Searching: "Searching for '{id}'…"; phases "Connecting to Active Directory…", "Searching Active Directory and Microsoft Graph…", "Searching Microsoft Graph…", "Waiting for Microsoft sign-in…".
- Management overlines: "Manage identity in", "Manage mailbox in". Buttons: "Open in Entra admin center", "Open Exchange admin center", "Open on-prem ECP", "Copy DN", "View in Entra ID", "View in Exchange Online".
- Toasts: "Summary copied", "Link copied", "Code copied", "Path copied", "Version info copied", "Nothing to copy yet", "No portal link for this object", "Couldn't access the clipboard."
- Settings saved InfoBar: title "Settings saved", message "Your changes are in effect." (+ " You'll be asked to sign in again on your next search." when connection settings changed).
- Dates: `g` (culture) for display; relative: "just now" (< 1 min), "{n} min ago", "{n} h ago", "{n} days ago" (≥ 1 day), "1 day ago".
