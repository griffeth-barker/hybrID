using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HybridID.Models;

namespace HybridID.Services;

/// <summary>What the screen showed, so the copied summary matches it (spec section 7.5).</summary>
public sealed record SummaryContext(
    string SearchedIdentity,
    DateTimeOffset SearchedAt,
    string IdentityVerdict,
    string MailboxVerdict,
    string AdCoverageText,
    string EntraCoverageText);

/// <summary>Plain-text ticket summary of one result. Pure function; no UI types.</summary>
public static class ResultSummaryFormatter
{
    // Spaces, not tabs: ticket systems collapse tabs.
    private const int LabelWidth = 22;

    public static string Format(IdentityLookupResult result, SummaryContext context)
    {
        var sb = new StringBuilder();
        sb.Append("hybrID lookup: ").Append(context.SearchedIdentity)
          .Append(" (").Append(Stamp(context.SearchedAt)).Append(')').AppendLine();

        Line(sb, "Name:", result.Name);
        Line(sb, "Object type:", result.ObjectClass);
        Line(sb, "Sync state:", result.SyncState switch
        {
            SyncState.OnPremOnly => "On-premises only (not synced)",
            SyncState.Hybrid => "Hybrid (synced to Entra ID)",
            SyncState.CloudOnly => "Cloud-only",
            _ => null,
        });
        Line(sb, "Account:", result.IsLockedOut == true ? "Locked out"
            : result.AccountEnabled switch { true => "Enabled", false => "Disabled", _ => null });

        if (DisplayFormat.KindOf(result) == ObjectKind.User)
        {
            Line(sb, "Password:", result.PasswordNeverExpires == true ? "Never expires"
                : result.PasswordLastSetUtc is { } set
                    ? $"Last set {set.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} ({DisplayFormat.RelativeAge(set)})"
                    : null);
        }

        Line(sb, "Identity managed in:", context.IdentityVerdict);
        Line(sb, "Mailbox managed in:", context.MailboxVerdict);
        Line(sb, "Identity link:", result.ManageIdentity?.Url ?? "(none)");
        if (result.ManageExchange?.Url is { } mailboxUrl)
        {
            Line(sb, "Mailbox link:", mailboxUrl);
        }

        if (result.SyncState == SyncState.Hybrid && result.LastSyncedUtc is { } synced)
        {
            Line(sb, "Last synced:", Stamp(new DateTimeOffset(synced, TimeSpan.Zero).ToLocalTime()));
        }

        Line(sb, "Primary SMTP:", DisplayFormat.PrimarySmtp(result.ProxyAddresses));
        Line(sb, "Proxy addresses:", Join(result.ProxyAddresses));
        Line(sb, "Licenses:", Join(result.Licenses));
        Line(sb, "Group type:", result.GroupTypeDescription);
        if (result.MemberCount is { } members)
        {
            Line(sb, "Members:", DisplayFormat.MemberCount(members, result.SyncState));
        }

        Line(sb, "Notes:", string.IsNullOrWhiteSpace(result.Notes) ? null : result.Notes.Replace("\n\n", " "));
        Line(sb, "Coverage:", $"{context.AdCoverageText}; {context.EntraCoverageText}");
        return sb.ToString().TrimEnd();
    }

    private static void Line(StringBuilder sb, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            sb.Append(label.PadRight(LabelWidth)).Append(value).AppendLine();
        }
    }

    private static string? Join(IReadOnlyList<string> values) => values.Count == 0 ? null : string.Join("; ", values);

    private static string Stamp(DateTimeOffset at) =>
        at.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture);
}
