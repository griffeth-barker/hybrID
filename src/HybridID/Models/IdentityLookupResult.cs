using System;
using System.Collections.Generic;

namespace HybridID.Models;

public sealed class ManagementLink
{
    public required string Text { get; init; }
    public string? Url { get; init; }
    public bool IsLink => Url is not null;

    public static ManagementLink AsText(string text) => new() { Text = text };
    public static ManagementLink AsLink(string text, string? url) => new() { Text = text, Url = url };
}

public enum StatusLevel
{
    Info,
    Success,
    Error,
}

/// <summary>Where the identity's source of authority currently lives - drives the result badge.</summary>
public enum SyncState
{
    OnPremOnly,
    Hybrid,
    CloudOnly,
}

/// <summary>One candidate in an ambiguous (multiple-match) result, re-searchable by Identity.</summary>
public sealed class AmbiguousCandidate
{
    public required string DisplayName { get; init; }
    public required string Detail { get; init; }
    public required string Identity { get; init; }
}

public sealed class IdentityLookupResult
{
    public required StatusLevel StatusLevel { get; init; }
    public required string StatusMessage { get; init; }

    public string? Name { get; init; }
    public string? ObjectClass { get; init; }
    public string Notes { get; init; } = "";
    public ManagementLink? ManageIdentity { get; init; }
    public ManagementLink? ManageExchange { get; init; }

    public SyncState? SyncState { get; init; }
    public bool? AccountEnabled { get; init; }
    public bool? IsLockedOut { get; init; }
    public bool? PasswordNeverExpires { get; init; }
    public DateTime? PasswordLastSetUtc { get; init; }
    public DateTime? LastSyncedUtc { get; init; }
    public IReadOnlyList<string> ProxyAddresses { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Licenses { get; init; } = Array.Empty<string>();
    public string? GroupTypeDescription { get; init; }
    public int? MemberCount { get; init; }

    /// <summary>Non-null and populated only when the search matched more than one object.</summary>
    public IReadOnlyList<AmbiguousCandidate>? Candidates { get; init; }
}
