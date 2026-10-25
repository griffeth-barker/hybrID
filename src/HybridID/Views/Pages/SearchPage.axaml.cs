using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HybridID.Controls;
using HybridID.Models;
using HybridID.Services;
using HybridID.Views.Dialogs;

namespace HybridID.Views.Pages;

public enum SearchViewState
{
    Idle,
    Searching,
    NoResult,
    Candidates,
    Result,
}

/// <summary>One row of the ambiguous-match list (5.1.9).</summary>
public sealed class CandidateItem
{
    public required string DisplayName { get; init; }
    public required string Detail { get; init; }
    public required string Identity { get; init; }
    public Geometry? Icon { get; init; }
    public bool IsActiveDirectory { get; init; }
    public bool IsEntraId { get; init; }
}

/// <summary>
/// Search box and every result state (docs/design/ui-spec.md section 5.1). Orchestrates only:
/// results come from an <see cref="ILookupSource"/>, routing decisions from the result itself.
/// </summary>
public partial class SearchPage : UserControl, IShellPage
{
    private const string DefaultHint =
        "Examples: jsmith · jsmith@contoso.com · 3f2504e0-4f89-11d3-9a0c-0305e82c3301. Press Enter to search.";

    private readonly IAppShell? _shell;
    private readonly ILookupSource _source;

    private bool _isSearching;
    private bool _setupDismissed;
    private string? _lastIdentity;
    private string? _renderedIdentity;
    private LookupOutcome? _lastOutcome;
    private DateTimeOffset _searchedAt;
    private Coverage _adCoverage;
    private Coverage _entraCoverage;

    /// <summary>For the XAML previewer only.</summary>
    public SearchPage() : this(null, new PreviewLookupSource(new AppConfig()))
    {
    }

    public SearchPage(IAppShell shell) : this(shell, new LiveLookupSource(shell))
    {
    }

    public SearchPage(IAppShell? shell, ILookupSource source)
    {
        _shell = shell;
        _source = source;
        InitializeComponent();

        HintText.Text = DefaultHint;
        AutomationPropertiesHelp(DefaultHint);
        ToolTip.SetTip(BtnCopySummary, $"Copy summary for ticket ({ShortcutText.Format("Ctrl+Shift+C")})");
        ToolTip.SetTip(BtnSearchAgain, "Search again (F5)");
        BuildShortcutList();

        TxtSearch.KeyDown += OnSearchBoxKeyDown;
        TxtSearch.TextChanged += (_, _) => OnSearchTextChanged();
        BtnClear.Click += (_, _) => ClearAll();
        BtnLocate.Click += async (_, _) => await RunSearchAsync(TxtSearch.Text);
        BtnSearchAgain.Click += async (_, _) => await SearchAgainAsync();
        BtnNoResultAgain.Click += async (_, _) => await SearchAgainAsync();
        BtnGraphSuspectAgain.Click += async (_, _) => await SearchAgainAsync();
        BtnGraphErrorAgain.Click += async (_, _) => await SearchAgainAsync();
        BtnCopySummary.Click += async (_, _) => await CopySummaryAsync();
        BtnSetupSettings.Click += (_, _) => _shell?.Navigate(AppPage.Settings);
        BtnGraphInitSettings.Click += (_, _) => _shell?.Navigate(AppPage.Settings);
        BtnAdRetry.Click += async (_, _) => await RetryAdAsync();
        BtnAdSignIn.Click += async (_, _) => await RetryAdAsync();
        BtnSyncHintSignIn.Click += async (_, _) => await RetryAdAsync();
        SetupBar.Closed += (_, _) => _setupDismissed = true;

        CandidateList.AddHandler(TappedEvent, OnCandidateTapped);
        CandidateList.KeyDown += OnCandidateKeyDown;

        AddHandler(CopyButton.CopiedEvent, OnCopied);
        AddHandler(ManagementCard.LinkCopiedEvent, OnCopied);

        BodyPanel.SizeChanged += (_, e) => ApplyWidth(e.NewSize.Width);

        if (_source.IsPreview)
        {
            PreviewBar.Message =
                "Showing sample data — nothing is sent to Active Directory or Entra ID. Try: " +
                string.Join(", ", PreviewLookupSource.Samples.Select(s => $"{s.Identity} ({s.Shows})")) + ".";
            PreviewBar.IsOpen = true;
        }

        SetState(SearchViewState.Idle);
    }

    // ------------------------------------------------------------------ IShellPage

    public bool HandleShortcut(AppShortcut shortcut)
    {
        switch (shortcut)
        {
            case AppShortcut.FocusSearch:
                FocusDefault();
                return true;
            case AppShortcut.Escape:
                if (!_isSearching)
                {
                    ClearAll();
                }

                return true;
            case AppShortcut.CopySummary:
                _ = CopySummaryAsync();
                return true;
            case AppShortcut.OpenIdentityPortal:
                OpenPortal(IdentityCard);
                return true;
            case AppShortcut.OpenMailboxPortal:
                OpenPortal(MailboxCard);
                return true;
            case AppShortcut.SearchAgain:
                _ = SearchAgainAsync();
                return true;
            default:
                return false;
        }
    }

    public Task<bool> CanLeaveAsync() => Task.FromResult(true);

    public void OnNavigatedTo() => UpdateSetupBar();

    public void OnNavigatedFrom()
    {
    }

    public void FocusDefault()
    {
        TxtSearch.Focus();
        TxtSearch.SelectAll();
    }

    // ------------------------------------------------------------------ Search flow (5.1.4)

    private async Task RunSearchAsync(string? rawInput)
    {
        if (_isSearching)
        {
            return;
        }

        var (identity, prefixRemoved) = Normalize(rawInput);
        if (identity.Length == 0)
        {
            ShowValidationError();
            return;
        }

        TxtSearch.Text = identity;
        HintText.Text = prefixRemoved ? $"Searching for '{identity}' (domain prefix removed)." : DefaultHint;

        var isRefresh = string.Equals(identity, _renderedIdentity, StringComparison.OrdinalIgnoreCase);
        _isSearching = true;
        _lastIdentity = identity;
        HideResultBars();
        SetSearchingUi(true);
        SearchingTitle.Text = $"Searching for '{identity}'…";
        PhaseText.Text = "Searching Active Directory and Microsoft Graph…";
        SetState(SearchViewState.Searching);

        try
        {
            var outcome = await _source.LookupAsync(identity, phase =>
                Dispatcher.UIThread.Post(() => PhaseText.Text = phase));
            _searchedAt = DateTimeOffset.Now;
            Render(identity, outcome, isRefresh);
        }
        catch (LookupInitException ex)
        {
            GraphInitBar.Message = ex.Message;
            GraphInitBar.IsOpen = true;
            _lastOutcome = null;
            _renderedIdentity = null;
            SetState(SearchViewState.Idle);
            _shell?.Announce($"{GraphInitBar.Title}. {ex.Message}");
        }
        catch (Exception ex)
        {
            _lastOutcome = null;
            _renderedIdentity = null;
            SetState(SearchViewState.Idle);
            if (_shell is not null)
            {
                await _shell.ShowDialogAsync(new MessageDialog("Something went wrong", ex.Message,
                    severity: InfoBarSeverity.Error));
            }
        }
        finally
        {
            _isSearching = false;
            SetSearchingUi(false);
            FocusDefault();
        }
    }

    private async Task SearchAgainAsync()
    {
        if (_lastIdentity is not null)
        {
            await RunSearchAsync(_lastIdentity);
        }
    }

    private async Task RetryAdAsync()
    {
        if (_isSearching)
        {
            return;
        }

        if (await _source.PrepareAdRetryAsync())
        {
            await SearchAgainAsync();
        }
    }

    /// <summary>Display-only cleanup of pasted input (5.1.2): first line, quotes, &lt;&gt;, mailto:, DOMAIN\.</summary>
    internal static (string Identity, bool PrefixRemoved) Normalize(string? raw)
    {
        var s = (raw ?? "").Replace("\r", "").Split('\n')[0].Trim();
        s = s.Trim('"', '\'', '“', '”', '‘', '’').Trim();
        if (s.StartsWith('<') && s.EndsWith('>'))
        {
            s = s[1..^1].Trim();
        }

        if (s.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            s = s[7..].Trim();
        }

        var slash = s.IndexOf('\\');
        if (slash > 0 && slash < s.Length - 1)
        {
            return (s[(slash + 1)..].Trim(), true);
        }

        return (s, false);
    }

    // ------------------------------------------------------------------ Rendering

    private void Render(string identity, LookupOutcome outcome, bool isRefresh)
    {
        var result = outcome.Result;
        _lastOutcome = outcome;

        var hasGraphData = result.SyncState is SyncState.Hybrid or SyncState.CloudOnly;
        var graphError = result.GraphError;
        // Heuristic for sources that can't report Graph errors; a reported error replaces it.
        var graphSuspect = graphError is null && outcome.DeviceCodeShown && !hasGraphData;
        _adCoverage = AdCoverageOf(outcome);
        _entraCoverage = graphSuspect || graphError is not null
            ? new Coverage("Entra ID: may be incomplete", "Icon.Warning", "caution")
            : hasGraphData
                ? new Coverage("Entra ID: match", "Icon.CheckCircle", "success")
                : new Coverage("Entra ID: no match", "Icon.NotSet", "secondary");

        var adNotSearched = outcome.AdCoverage != AdCoverage.Searched;
        if (outcome.AdCoverage == AdCoverage.Unreachable)
        {
            AdUnreachableBar.Message =
                $"Couldn't connect: {outcome.AdError ?? "unknown error"}. Results are based on Entra ID only — a synced object can look cloud-only.";
            AdUnreachableBar.IsOpen = true;
        }

        AdSkippedBar.IsOpen = outcome.AdCoverage == AdCoverage.Skipped;
        SyncHintBar.IsOpen = adNotSearched && result.EntraSyncEnabled == true;
        GraphSuspectBar.IsOpen = graphSuspect;
        GraphErrorBar.Message = graphError;
        GraphErrorBar.IsOpen = graphError is not null;

        if (result.Candidates is { Count: > 0 } candidates)
        {
            RenderCandidates(identity, candidates);
            _renderedIdentity = null;
            _shell?.Announce(result.StatusMessage);
        }
        else if (result.Name is null)
        {
            NoResultTitle.Text = $"No match for '{identity}'";
            BuildCoverage(NoResultCoverage);
            SetState(SearchViewState.NoResult);
            _renderedIdentity = null;
            _shell?.Announce(result.StatusMessage);
        }
        else
        {
            RenderResult(result, outcome);
            SetState(SearchViewState.Result);
            _ = AnimateResultAsync(stagger: !isRefresh);
            _renderedIdentity = identity;
            _shell?.Announce(
                $"Found {result.Name}. Identity managed in {IdentityCard.LocationTitle}. Mailbox: {MailboxCard.LocationTitle}.");
        }
    }

    private void RenderCandidates(string identity, IReadOnlyList<AmbiguousCandidate> candidates)
    {
        CandidatesTitle.Text = $"{candidates.Count} matches for '{identity}'";
        BuildCoverage(CandidatesCoverage);
        CandidateList.ItemsSource = candidates.Select(c => new CandidateItem
        {
            DisplayName = c.DisplayName,
            Detail = c.Detail,
            Identity = c.Identity,
            Icon = Icon(c.Source switch
            {
                CandidateSource.ActiveDirectory => "Icon.Building",
                CandidateSource.EntraId => "Icon.Cloud",
                _ => "Icon.Person",
            }),
            IsActiveDirectory = c.Source == CandidateSource.ActiveDirectory,
            IsEntraId = c.Source == CandidateSource.EntraId,
        }).ToList();
        SetState(SearchViewState.Candidates);
    }

    private void RenderResult(IdentityLookupResult r, LookupOutcome outcome)
    {
        var kind = DisplayFormat.KindOf(r);

        // Header
        SetBadgeLocation(Avatar, r.SyncState switch
        {
            SyncState.OnPremOnly => "onprem",
            SyncState.Hybrid => "hybrid",
            SyncState.CloudOnly => "cloud",
            _ => null,
        });
        AvatarIcon.Data = Icon(kind switch
        {
            ObjectKind.User => "Icon.Person20",
            ObjectKind.Group => "Icon.Group20",
            _ => "Icon.Unknown20",
        });
        ResultName.Text = r.Name;
        ToolTip.SetTip(ResultName, r.Name);
        var kindLabel = DisplayFormat.ObjectKindLabel(r.ObjectClass);
        var subId = r.UserPrincipalName ?? (kind == ObjectKind.Group ? r.Mail : null);
        ResultSubline.Text = subId is null ? kindLabel : $"{kindLabel} · {subId}";
        var adNotChecked = outcome.AdCoverage != AdCoverage.Searched;
        var entraNotChecked = r.GraphError is not null;
        BuildPills(r, adNotChecked, entraNotChecked);
        BuildCoverage(ResultCoverage);

        // Verdict
        RenderIdentityCard(r, adNotChecked, entraNotChecked);
        RenderMailboxCard(r);

        // Guidance
        var guidance = r.Notes;
        if (r.SyncState == SyncState.Hybrid)
        {
            const string hybridHint =
                "Edit synced attributes (name, proxy addresses, aliases) on-premises. Changes made in Microsoft 365 are overwritten by the next sync.";
            guidance = string.IsNullOrWhiteSpace(guidance) ? hybridHint : guidance + "\n\n" + hybridHint;
        }

        GuidanceBar.Message = guidance;
        GuidanceBar.IsOpen = !string.IsNullOrWhiteSpace(guidance);

        // Details
        RenderIdentityRows(r);
        RenderAccountRows(r, kind);
        RenderMailRows(r);
        RenderLicenseRows(r, kind);
        RenderGroupRows(r, kind);
    }

    private void BuildPills(IdentityLookupResult r, bool adNotChecked, bool entraNotChecked)
    {
        PillsPanel.Children.Clear();
        switch (r.SyncState)
        {
            // A directory that wasn't searched can't back the sync claim: say so instead.
            case SyncState.OnPremOnly when entraNotChecked:
                AddPill("On-premises (Entra ID not checked)", "Icon.Building", PillKind.Caution);
                break;
            case SyncState.OnPremOnly:
                AddPill("On-premises only", "Icon.Building", PillKind.OnPrem);
                break;
            case SyncState.Hybrid:
                AddPill("Hybrid · synced", "Icon.Sync", PillKind.Hybrid);
                break;
            case SyncState.CloudOnly when adNotChecked:
                AddPill("Cloud-only (AD not checked)", "Icon.Cloud", PillKind.Caution);
                break;
            case SyncState.CloudOnly:
                AddPill("Cloud-only", "Icon.Cloud", PillKind.Cloud);
                break;
        }

        if (AccountStatus(r) is { } status)
        {
            AddPill(status.Text, status.IconKey, status.Kind);
        }

        if (r.PasswordNeverExpires == true)
        {
            AddPill("Password never expires", "Icon.Key", PillKind.Caution);
        }

        void AddPill(string text, string iconKey, PillKind pillKind) =>
            PillsPanel.Children.Add(new StatusPill { Text = text, Icon = Icon(iconKey), Kind = pillKind });
    }

    /// <summary>Legacy precedence: locked out, then disabled, then enabled.</summary>
    private static (string Text, string IconKey, PillKind Kind)? AccountStatus(IdentityLookupResult r)
    {
        if (r.IsLockedOut == true) return ("Locked out", "Icon.Lock", PillKind.Critical);
        if (r.AccountEnabled == false) return ("Disabled", "Icon.Prohibited", PillKind.Neutral);
        if (r.AccountEnabled == true) return ("Enabled", "Icon.CheckCircle", PillKind.Success);
        return null;
    }

    private void RenderIdentityCard(IdentityLookupResult r, bool adNotChecked, bool entraNotChecked)
    {
        var card = IdentityCard;
        ResetCard(card);

        if (r.SyncState == SyncState.CloudOnly)
        {
            card.Location = ManagementLocation.Cloud;
            card.Icon = Icon("Icon.Cloud20");
            card.LocationTitle = "Entra ID";
            card.Body = adNotChecked
                ? "Found in Entra ID. Active Directory wasn't searched, so it may be synced from on-premises."
                : "Cloud-only object. Manage it in the Microsoft Entra admin center.";
            card.PrimaryText = "Open in Entra admin center";
            card.PrimaryUrl = r.ManageIdentity?.Url;
            return;
        }

        card.Location = ManagementLocation.OnPrem;
        card.Icon = Icon("Icon.Building20");
        card.LocationTitle = "Active Directory";
        card.Body = r.SyncState == SyncState.Hybrid
            ? "Source of authority is on-premises AD. Changes sync to Entra ID."
            : entraNotChecked
                ? "Source of authority is on-premises AD. Entra ID wasn't searched, so sync status is unknown."
                : "Exists only in on-premises Active Directory. Not synced to Entra ID.";

        if (r.DistinguishedName is { } dn)
        {
            var copyDn = new Button { Height = 36 };
            copyDn.Content = IconText("Icon.Copy", "Copy DN");
            ToolTip.SetTip(copyDn, "Copy distinguished name");
            copyDn.Click += async (_, _) =>
            {
                if (_shell is not null && await _shell.CopyTextAsync(dn))
                {
                    _shell.ShowToast("Distinguished name copied");
                }
            };
            card.SecondaryContent = copyDn;
        }

        if (r.SyncState == SyncState.Hybrid && ViewLink(r, "View in Entra ID") is { } view)
        {
            card.ViewLinkText = view.Text;
            card.ViewLinkUrl = view.Url;
        }
    }

    private void RenderMailboxCard(IdentityLookupResult r)
    {
        var card = MailboxCard;
        ResetCard(card);
        var location = MailboxLocationOf(r);
        var isEcp = location is MailboxLocation.RemoteMailbox or MailboxLocation.OnPremMailbox
            or MailboxLocation.OnPremMailEnabledGroup;

        switch (location)
        {
            case MailboxLocation.ExchangeOnline:
                card.Location = ManagementLocation.Cloud;
                card.Icon = Icon("Icon.Cloud20");
                card.LocationTitle = "Exchange Online";
                card.Body = "Manage in the Exchange admin center.";
                card.PrimaryText = "Open Exchange admin center";
                break;
            case MailboxLocation.RemoteMailbox:
                card.TagText = "Remote mailbox";
                card.Body = "Mailbox data lives in Exchange Online, but it's managed on-premises.";
                if (ViewLink(r, "View in Exchange Online") is { } view)
                {
                    card.ViewLinkText = view.Text;
                    card.ViewLinkUrl = view.Url;
                }

                break;
            case MailboxLocation.OnPremMailbox:
                card.Body = "Mailbox is hosted on-premises.";
                break;
            case MailboxLocation.OnPremMailEnabledGroup:
                card.TagText = "Mail-enabled group";
                card.Body = "Manage Exchange properties in on-prem ECP.";
                break;
            case MailboxLocation.NotApplicable:
                card.Location = ManagementLocation.Neutral;
                card.Icon = Icon("Icon.MailOff20");
                card.LocationTitle = "Not mail-enabled";
                card.Body = "This object has no Exchange properties to manage.";
                return;
            default:
                card.Location = ManagementLocation.Neutral;
                card.Icon = Icon("Icon.MailOff20");
                card.LocationTitle = "No mailbox";
                card.Body = "No Exchange attributes found on this account.";
                return;
        }

        if (isEcp)
        {
            card.Location = ManagementLocation.OnPrem;
            card.Icon = Icon("Icon.Building20");
            card.LocationTitle = "Exchange Server";
            card.PrimaryText = "Open on-prem ECP";
        }

        card.PrimaryUrl = r.ManageExchange?.Url;
        if (card.PrimaryUrl is null)
        {
            card.Body += "\nLink unavailable: object has no ObjectGUID.";
        }
        else if (isEcp && _shell?.Session.IsExchangeFqdnDefault == true)
        {
            card.WarningText = $"Exchange server isn't set — this link points to {AppSession.DefaultExchangeFqdn}.";
            var settings = new HyperlinkButton { Content = "Settings", Padding = new Thickness(0) };
            settings.Click += (_, _) => _shell.Navigate(AppPage.Settings);
            card.WarningActionContent = settings;
        }
    }

    /// <summary>
    /// Mailbox verdict. Prefers the [M1] MailboxLocation; otherwise derives it from the strings
    /// IdentityLookupService.BuildAdFoundResult / BuildResult produce (spec 5.1.5 interim mapping).
    /// </summary>
    private static MailboxLocation MailboxLocationOf(IdentityLookupResult r)
    {
        if (r.MailboxLocation is { } known)
        {
            return known;
        }

        var text = r.ManageExchange?.Text;
        if (text == "Exchange Online") return MailboxLocation.ExchangeOnline;
        if (r.Notes.Contains("Remote Mailbox", StringComparison.Ordinal)) return MailboxLocation.RemoteMailbox;
        if (r.Notes.Contains("resides entirely on-premises", StringComparison.Ordinal)) return MailboxLocation.OnPremMailbox;
        if (r.Notes.StartsWith("Mail-enabled group", StringComparison.Ordinal)) return MailboxLocation.OnPremMailEnabledGroup;
        if (text == "N/A") return MailboxLocation.NotApplicable;
        return MailboxLocation.None;
    }

    private static ManagementLink? ViewLink(IdentityLookupResult r, string text) =>
        r.ViewLinks.FirstOrDefault(l => l.Text == text && l.IsLink);

    private static void ResetCard(ManagementCard card)
    {
        card.TagText = null;
        card.WarningText = null;
        card.WarningActionContent = null;
        card.PrimaryText = null;
        card.PrimaryUrl = null;
        card.SecondaryContent = null;
        card.ViewLinkText = null;
        card.ViewLinkUrl = null;
    }

    private void RenderIdentityRows(IdentityLookupResult r)
    {
        var rows = new List<Control>
        {
            Field("Display name", r.Name),
            Field("Object type", r.ObjectClass, canCopy: false),
        };
        AddIf(rows, "sAMAccountName", r.SamAccountName);
        AddIf(rows, "User principal name", r.UserPrincipalName);
        AddIf(rows, "ObjectGUID", r.ObjectGuid?.ToString(), mono: true);
        AddIf(rows, "Entra object ID", r.EntraObjectId, mono: true);
        AddIf(rows, "Distinguished name", r.DistinguishedName, mono: true);
        FillRows(IdentityRows, rows);
    }

    private void RenderAccountRows(IdentityLookupResult r, ObjectKind kind)
    {
        var rows = new List<Control>();
        if (r.AccountEnabled is not null && AccountStatus(r) is { } status)
        {
            var field = Field("Status", status.Text, canCopy: false);
            field.ValueIcon = Icon(status.IconKey);
            field.ValueKind = status.Kind;
            rows.Add(field);
        }

        if (kind == ObjectKind.User)
        {
            rows.Add(Field("Password", DisplayFormat.Password(r), canCopy: false));
        }

        if (r.SyncState == SyncState.Hybrid)
        {
            rows.Add(Field("Last synced to Entra ID",
                r.LastSyncedUtc is { } synced ? DisplayFormat.DateTimeWithAge(synced) : "Unknown", canCopy: false));
        }

        FillRows(AccountRows, rows);
        AccountCard.IsVisible = rows.Count > 0;
    }

    private void RenderMailRows(IdentityLookupResult r)
    {
        var rows = new List<Control>();
        AddIf(rows, "Primary SMTP address", DisplayFormat.PrimarySmtp(r.ProxyAddresses));

        var proxies = DisplayFormat.ParseProxyAddresses(r.ProxyAddresses);
        if (proxies.Count > 0)
        {
            rows.Add(new ResultListField
            {
                Label = $"Proxy addresses ({proxies.Count})",
                ItemsSource = proxies.Select(p => new ResultListItem
                {
                    Text = p.Address,
                    Tag = p.Tag,
                    TagKind = p.IsPrimary ? PillKind.Success : PillKind.Neutral,
                    IsEmphasized = p.IsPrimary,
                }).ToList(),
                CopyAllText = string.Join(Environment.NewLine, r.ProxyAddresses),
                CopyAllToolTip = "Copy all (with prefixes)",
            });
        }

        AddIf(rows, "Target address", r.TargetAddress);
        if (rows.Count == 0)
        {
            rows.Add(new TextBlock { Text = "No proxy addresses.", Classes = { "caption", "secondary" } });
        }

        FillRows(MailRows, rows);
    }

    private void RenderLicenseRows(IdentityLookupResult r, ObjectKind kind)
    {
        LicensesCard.IsVisible = kind == ObjectKind.User && r.SyncState != SyncState.OnPremOnly;
        if (!LicensesCard.IsVisible)
        {
            return;
        }

        LicensesTitle.Text = r.Licenses.Count > 0 ? $"Licenses ({r.Licenses.Count})" : "Licenses";
        Control content = r.Licenses.Count > 0
            ? new ResultListField
            {
                ItemsSource = r.Licenses.Select(l => new ResultListItem { Text = l }).ToList(),
                CopyAllText = string.Join(Environment.NewLine, r.Licenses),
                Label = "Assigned",
            }
            : new TextBlock { Text = "No licenses found.", Classes = { "caption", "secondary" } };
        FillRows(LicenseRows, new List<Control> { content });
    }

    private void RenderGroupRows(IdentityLookupResult r, ObjectKind kind)
    {
        var rows = new List<Control>();
        if (kind == ObjectKind.Group)
        {
            AddIf(rows, "Group type", r.GroupTypeDescription, canCopy: false);
            if (r.MemberCount is { } members)
            {
                rows.Add(Field("Member count", DisplayFormat.MemberCount(members, r.SyncState), canCopy: false));
                if (DisplayFormat.IsMemberCountCapped(members, r.SyncState))
                {
                    rows.Add(new TextBlock
                    {
                        Text = "Active Directory returns at most 1,500 members per query.",
                        Classes = { "caption", "secondary" },
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 8),
                    });
                }
            }
        }

        FillRows(GroupRows, rows);
        GroupCard.IsVisible = rows.Count > 0;
    }

    private static ResultField Field(string label, string? value, bool mono = false, bool canCopy = true) =>
        new() { Label = label, Value = value, IsMonospace = mono, CanCopy = canCopy && !string.IsNullOrEmpty(value) };

    private static void AddIf(List<Control> rows, string label, string? value, bool mono = false, bool canCopy = true)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            rows.Add(Field(label, value, mono, canCopy));
        }
    }

    /// <summary>Rows separated by full-width dividers (6.1 card anatomy); captions attach to the row above.</summary>
    private static void FillRows(Panel host, IReadOnlyList<Control> rows)
    {
        host.Children.Clear();
        for (var i = 0; i < rows.Count; i++)
        {
            if (i > 0 && rows[i] is not TextBlock)
            {
                host.Children.Add(new Border { Classes = { "divider" } });
            }

            host.Children.Add(rows[i]);
        }
    }

    // ------------------------------------------------------------------ Coverage (5.1.7)

    private readonly record struct Coverage(string Text, string IconKey, string IconClass);

    private static Coverage AdCoverageOf(LookupOutcome outcome) => outcome.AdCoverage switch
    {
        AdCoverage.NotConfigured => new("Active Directory: not configured", "Icon.NotSet", "tertiary"),
        AdCoverage.Skipped => new("Active Directory: skipped", "Icon.Warning", "caution"),
        AdCoverage.Unreachable => new("Active Directory: unreachable", "Icon.Warning", "caution"),
        _ when outcome.Result.SyncState is SyncState.OnPremOnly or SyncState.Hybrid =>
            new("Active Directory: match", "Icon.CheckCircle", "success"),
        _ when outcome.Result.Candidates?.Any(c => c.Source == CandidateSource.ActiveDirectory) == true =>
            new("Active Directory: match", "Icon.CheckCircle", "success"),
        _ => new("Active Directory: no match", "Icon.NotSet", "secondary"),
    };

    private void BuildCoverage(WrapPanel panel)
    {
        var entra = _entraCoverage;
        if (_lastOutcome?.Result.Candidates?.Any(c => c.Source == CandidateSource.EntraId) == true)
        {
            entra = new Coverage("Entra ID: match", "Icon.CheckCircle", "success");
        }

        panel.Children.Clear();
        panel.Children.Add(CoverageItem(_adCoverage));
        panel.Children.Add(new TextBlock
        {
            Text = "·",
            Classes = { "caption", "secondary" },
            Margin = new Thickness(8, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        });
        panel.Children.Add(CoverageItem(entra));
        _entraCoverage = entra;
    }

    private static Control CoverageItem(Coverage coverage) => new StackPanel
    {
        Orientation = Avalonia.Layout.Orientation.Horizontal,
        Spacing = 4,
        Children =
        {
            new PathIcon
            {
                Data = Icon(coverage.IconKey),
                Width = 12,
                Height = 12,
                Classes = { coverage.IconClass },
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            },
            new TextBlock
            {
                Text = coverage.Text,
                Classes = { "caption", coverage.IconClass == "tertiary" ? "tertiary" : "secondary" },
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            },
        },
    };

    // ------------------------------------------------------------------ Copy & portals

    private async Task CopySummaryAsync()
    {
        if (_shell is null)
        {
            return;
        }

        if (_lastOutcome is not { Result.Name: not null } outcome || !ResultState.IsVisible)
        {
            _shell.ShowToast("Nothing to copy yet", ToastKind.Info);
            return;
        }

        var mailboxVerdict = MailboxCard.LocationTitle +
            (MailboxCard.PrimaryText == "Open on-prem ECP" ? " ECP" : "") +
            (MailboxCard.TagText is { } tag ? $" ({tag.ToLowerInvariant()})" : "");
        var text = ResultSummaryFormatter.Format(outcome.Result, new SummaryContext(
            _lastIdentity ?? "",
            _searchedAt,
            IdentityCard.LocationTitle ?? "",
            mailboxVerdict,
            _adCoverage.Text,
            _entraCoverage.Text));

        if (await _shell.CopyTextAsync(text))
        {
            _shell.ShowToast("Summary copied");
        }
    }

    private void OpenPortal(ManagementCard card)
    {
        if (!ResultState.IsVisible || !card.ActivatePrimary())
        {
            _shell?.ShowToast("No portal link for this object", ToastKind.Info);
        }
    }

    private void OnCopied(object? sender, CopiedEventArgs e)
    {
        if (_shell is null)
        {
            return;
        }

        if (!e.Succeeded)
        {
            _shell.ShowToast("Couldn't access the clipboard.", ToastKind.Error);
        }
        else if (e.RoutedEvent == ManagementCard.LinkCopiedEvent)
        {
            _shell.ShowToast("Link copied");
        }
        else if (e.IsBulk)
        {
            _shell.ShowToast($"{e.Label ?? "Text"} copied");
        }
        else
        {
            _shell.Announce($"{e.Label} copied");
        }
    }

    // ------------------------------------------------------------------ Input & candidates

    private async void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await RunSearchAsync(TxtSearch.Text);
        }
        else if (e.Key == Key.Down && CandidatesState.IsVisible && CandidateList.ItemCount > 0)
        {
            e.Handled = true;
            CandidateList.SelectedIndex = 0;
            (CandidateList.ContainerFromIndex(0) as Control)?.Focus(NavigationMethod.Directional);
        }
    }

    private void OnSearchTextChanged()
    {
        BtnClear.IsVisible = !string.IsNullOrEmpty(TxtSearch.Text);
        if (HintIcon.IsVisible)
        {
            HintIcon.IsVisible = false;
            HintText.Classes.Set("critical", false);
            HintText.Classes.Set("secondary", true);
            HintText.Text = DefaultHint;
            TxtSearch.Classes.Set("invalid", false);
        }
    }

    private void ShowValidationError()
    {
        HintIcon.IsVisible = true;
        HintText.Text = "Enter an identity to search.";
        HintText.Classes.Set("secondary", false);
        HintText.Classes.Set("critical", true);
        TxtSearch.Classes.Set("invalid", true);
        _shell?.Announce("Enter an identity to search.");
        TxtSearch.Focus();
    }

    private async void OnCandidateTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual v && v.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { DataContext: CandidateItem item })
        {
            await ActivateCandidateAsync(item);
        }
    }

    private async void OnCandidateKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && CandidateList.SelectedItem is CandidateItem item)
        {
            e.Handled = true;
            await ActivateCandidateAsync(item);
        }
        else if (e.Key == Key.Up && CandidateList.SelectedIndex <= 0)
        {
            e.Handled = true;
            FocusDefault();
        }
    }

    private Task ActivateCandidateAsync(CandidateItem item)
    {
        TxtSearch.Text = item.Identity;
        return RunSearchAsync(item.Identity);
    }

    private void ClearAll()
    {
        TxtSearch.Text = "";
        HintText.Text = DefaultHint;
        HideResultBars();
        _lastOutcome = null;
        _renderedIdentity = null;
        SetState(SearchViewState.Idle);
        TxtSearch.Focus();
    }

    // ------------------------------------------------------------------ State & layout

    private void SetState(SearchViewState state)
    {
        EmptyState.IsVisible = state == SearchViewState.Idle;
        SearchingState.IsVisible = state == SearchViewState.Searching;
        NoResultState.IsVisible = state == SearchViewState.NoResult;
        CandidatesState.IsVisible = state == SearchViewState.Candidates;
        ResultState.IsVisible = state == SearchViewState.Result;
        SearchingRing.IsActive = state == SearchViewState.Searching;
        if (state == SearchViewState.Idle)
        {
            UpdateSetupBar();
        }
    }

    private void SetSearchingUi(bool searching)
    {
        SearchProgress.IsVisible = searching;
        BtnLocate.IsEnabled = !searching;
        LocateRing.IsVisible = searching;
        LocateRing.IsActive = searching;
        LocateText.Text = searching ? "Locating…" : "Locate";
    }

    private void HideResultBars()
    {
        AdUnreachableBar.IsOpen = false;
        AdSkippedBar.IsOpen = false;
        SyncHintBar.IsOpen = false;
        GraphSuspectBar.IsOpen = false;
        GraphErrorBar.IsOpen = false;
        GraphInitBar.IsOpen = false;
    }

    private void UpdateSetupBar()
    {
        var session = _shell?.Session;
        if (session is null || _setupDismissed)
        {
            SetupBar.IsOpen = false;
            return;
        }

        var adMissing = !session.IsAdConfigured;
        var fqdnDefault = session.IsExchangeFqdnDefault;
        SetupBar.Message = (adMissing, fqdnDefault) switch
        {
            (true, true) => "Add your domain controller and Exchange server in Settings. Until then, only Entra ID is searched.",
            (true, false) => "Add your domain controller so on-premises Active Directory is searched. Until then, only Entra ID is searched.",
            (false, true) => "Set your on-premises Exchange server so Exchange admin center links work.",
            _ => null,
        };
        SetupBar.IsOpen = adMissing || fqdnDefault;
    }

    /// <summary>Width breakpoints (5.1.5): details 2 columns >= 760, verdict cards side by side >= 600.</summary>
    private void ApplyWidth(double width)
    {
        var wide = width >= 760;
        var medium = width >= 600;

        PlaceSecond(ManagementGrid, MailboxCard, sideBySide: medium);
        PlaceSecond(DetailsGrid, DetailsRight, sideBySide: wide);
        FormatGrid.Columns = wide ? 3 : 1;
        CopySummaryText.IsVisible = medium;
    }

    private static void PlaceSecond(Grid grid, Control second, bool sideBySide)
    {
        Grid.SetColumn(second, sideBySide ? 2 : 0);
        Grid.SetRow(second, sideBySide ? 0 : 2);
        Grid.SetColumnSpan(grid.Children[0], sideBySide ? 1 : 3);
        Grid.SetColumnSpan(second, sideBySide ? 1 : 3);
        grid.RowDefinitions[1].Height = new GridLength(sideBySide ? 0 : 12);
    }

    private async Task AnimateResultAsync(bool stagger)
    {
        Control[] blocks = { ResultHeader, ManagementGrid, GuidanceBar, DetailsGrid };
        if (!stagger)
        {
            return;
        }

        var start = TransformOperations.Parse("translateY(12px)");
        var end = TransformOperations.Parse("translateY(0px)");
        foreach (var block in blocks)
        {
            block.Classes.Remove("reveal");
            block.Opacity = 0;
            block.RenderTransform = start;
            block.Classes.Add("reveal");
        }

        for (var i = 0; i < blocks.Length; i++)
        {
            await Task.Delay(i == 0 ? 16 : 40);
            blocks[i].Opacity = 1;
            blocks[i].RenderTransform = end;
        }
    }

    private void BuildShortcutList()
    {
        (string Keys, string Label)[] shortcuts =
        {
            ("Ctrl+L", "Focus search"),
            ("Enter", "Locate"),
            ("Esc", "Clear"),
            ("Ctrl+Shift+C", "Copy summary"),
            ("Ctrl+1", "Open identity portal"),
            ("Ctrl+2", "Open mailbox portal"),
            ("F5", "Search again"),
            ("Ctrl+,", "Settings"),
        };

        foreach (var (keys, label) in shortcuts)
        {
            ShortcutsPanel.Children.Add(new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new Border
                    {
                        Classes = { "keycap" },
                        Child = new TextBlock { Text = ShortcutText.Format(keys), Classes = { "caption", "mono" } },
                    },
                    new TextBlock
                    {
                        Text = label,
                        Classes = { "caption", "secondary" },
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    },
                },
            });
        }
    }

    private void AutomationPropertiesHelp(string text) =>
        Avalonia.Automation.AutomationProperties.SetHelpText(TxtSearch, text);

    private static void SetBadgeLocation(Border badge, string? location)
    {
        foreach (var c in new[] { "onprem", "hybrid", "cloud" })
        {
            badge.Classes.Set(c, c == location);
        }
    }

    private static Control IconText(string iconKey, string text) => new StackPanel
    {
        Orientation = Avalonia.Layout.Orientation.Horizontal,
        Spacing = 8,
        Children =
        {
            new PathIcon { Data = Icon(iconKey), Width = 16, Height = 16 },
            new TextBlock { Text = text, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
        },
    };

    /// <summary>Icon geometries are theme-invariant, so resolving them once from code is safe.</summary>
    private static Geometry? Icon(string key) =>
        Application.Current?.FindResource(key) as Geometry;
}
