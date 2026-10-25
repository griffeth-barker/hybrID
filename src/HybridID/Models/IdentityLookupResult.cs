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

/// <summary>Where (and how) the object's mailbox is managed - drives the mailbox verdict card.</summary>
public enum MailboxLocation
{
    None,
    NotApplicable,
    OnPremMailbox,
    RemoteMailbox,
    OnPremMailEnabledGroup,
    ExchangeOnline,
}

public enum ObjectKind
{
    User,
    Group,
    Other,
}

public enum CandidateSource
{
    ActiveDirectory,
    EntraId,
}

/// <summary>One candidate in an ambiguous (multiple-match) result, re-searchable by Identity.</summary>
public sealed class AmbiguousCandidate
{
    public required string DisplayName { get; init; }
    public required string Detail { get; init; }
    public required string Identity { get; init; }
    public CandidateSource? Source { get; init; }
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

    // Display-only additions (docs/design/ui-spec.md section 11 [M1]); routing doesn't read these.
    public MailboxLocation? MailboxLocation { get; init; }
    public ObjectKind? ObjectKind { get; init; }
    public string? SamAccountName { get; init; }
    public string? UserPrincipalName { get; init; }
    public string? Mail { get; init; }
    public Guid? ObjectGuid { get; init; }
    public string? DistinguishedName { get; init; }
    public string? TargetAddress { get; init; }
    public string? EntraObjectId { get; init; }
    public bool? EntraSyncEnabled { get; init; }

    /// <summary>Read-only "View in ..." links for systems that mirror, but don't manage, the object.</summary>
    public IReadOnlyList<ManagementLink> ViewLinks { get; init; } = Array.Empty<ManagementLink>();

    // Service-behavior additions (ui-spec.md section 11 [M2]): a directory that errored is
    // reported instead of silently reading as "not found" (which routes synced objects wrong).
    /// <summary>Active Directory query error (e.g. a dead cached connection); null when AD answered or wasn't queried.</summary>
    public string? AdError { get; init; }

    /// <summary>Microsoft Graph error (auth, permissions, throttling); null when Graph answered.</summary>
    public string? GraphError { get; init; }

    /// <summary>Non-null and populated only when the search matched more than one object.</summary>
    public IReadOnlyList<AmbiguousCandidate>? Candidates { get; init; }
}
