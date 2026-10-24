using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HybridID.Models;

namespace HybridID.Services;

/// <summary>One parsed proxy address (spec section 6.5 proxy mapping).</summary>
public sealed record ProxyAddress(string Raw, string Type, string Address, bool IsPrimary)
{
    /// <summary>"Primary" (SMTP:), "Alias" (smtp:), otherwise the uppercase type ("SIP", "X500").</summary>
    public string Tag => IsPrimary ? "Primary" : Type == "smtp" ? "Alias" : Type.ToUpperInvariant();
}

/// <summary>Pure formatting helpers shared by SearchPage and <see cref="ResultSummaryFormatter"/>.</summary>
public static class DisplayFormat
{
    /// <summary>"User", "Cloud group", or the title-cased raw value ("Computer", "Contact").</summary>
    public static string ObjectKindLabel(string? objectClass) => objectClass switch
    {
        null or "" => "Object",
        "user" => "User",
        "group" => "Group",
        "Cloud User" => "Cloud user",
        "Cloud Group" => "Cloud group",
        _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(objectClass),
    };

    public static ObjectKind KindOf(IdentityLookupResult result) =>
        result.ObjectKind ?? result.ObjectClass switch
        {
            "user" or "Cloud User" => ObjectKind.User,
            "group" or "Cloud Group" => ObjectKind.Group,
            _ => ObjectKind.Other,
        };

    /// <summary>"17 min ago", "3 hours ago", "193 days ago" (never "in ..." - clock skew reads as "just now").</summary>
    public static string RelativeAge(DateTime utc, DateTime? nowUtc = null)
    {
        var age = (nowUtc ?? DateTime.UtcNow) - utc;
        if (age < TimeSpan.FromMinutes(1)) return "just now";
        if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes} min ago";
        if (age < TimeSpan.FromDays(1))
        {
            var hours = (int)age.TotalHours;
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }

        var days = (int)age.TotalDays;
        return days == 1 ? "1 day ago" : $"{days:N0} days ago";
    }

    /// <summary>Local date + time and relative age: "9/23/2026 1:45 PM · 17 min ago".</summary>
    public static string DateTimeWithAge(DateTime utc) =>
        $"{utc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)} · {RelativeAge(utc)}";

    /// <summary>"Never expires" / "Set 3/14/2026 · 193 days ago" / "Unknown" (legacy FormatPassword).</summary>
    public static string Password(IdentityLookupResult result)
    {
        if (result.PasswordNeverExpires == true)
        {
            return result.PasswordLastSetUtc is { } set
                ? $"Never expires · set {set.ToLocalTime().ToString("d", CultureInfo.CurrentCulture)}"
                : "Never expires";
        }

        return result.PasswordLastSetUtc is { } utc
            ? $"Set {utc.ToLocalTime().ToString("d", CultureInfo.CurrentCulture)} · {RelativeAge(utc)}"
            : "Unknown";
    }

    /// <summary>Parses and sorts: primary, aliases (ordinal ignore-case), then other types.</summary>
    public static IReadOnlyList<ProxyAddress> ParseProxyAddresses(IEnumerable<string> raw)
    {
        var parsed = raw
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r =>
            {
                var colon = r.IndexOf(':');
                var type = colon > 0 ? r[..colon] : "";
                var address = colon > 0 ? r[(colon + 1)..] : r;
                return new ProxyAddress(r, type, address, type == "SMTP");
            })
            .ToList();

        return parsed
            .OrderBy(p => p.IsPrimary ? 0 : p.Type == "smtp" ? 1 : 2)
            .ThenBy(p => p.Address, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The address of the case-sensitive "SMTP:" entry, if any.</summary>
    public static string? PrimarySmtp(IEnumerable<string> proxyAddresses) =>
        proxyAddresses.FirstOrDefault(p => p.StartsWith("SMTP:", StringComparison.Ordinal))?[5..];

    /// <summary>"1,500+" for the AD range-retrieval cap on synced/on-prem groups, else N0.</summary>
    public static string MemberCount(int count, SyncState? syncState) =>
        count == 1500 && syncState != SyncState.CloudOnly ? "1,500+" : count.ToString("N0", CultureInfo.CurrentCulture);

    /// <summary>Whether <see cref="MemberCount"/> hit the AD cap (shows the explanatory caption).</summary>
    public static bool IsMemberCountCapped(int count, SyncState? syncState) =>
        count == 1500 && syncState != SyncState.CloudOnly;
}
