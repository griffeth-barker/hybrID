using System;
using System.Collections.Generic;

namespace HybridID.Models;

public enum GraphObjectKind
{
    User,
    Group,
}

public sealed class GraphLookupResult
{
    public required GraphObjectKind Kind { get; init; }
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string? UserPrincipalName { get; init; }
    public string? Mail { get; init; }

    public bool? AccountEnabled { get; init; }
    public DateTime? PasswordLastSetUtc { get; init; }
    public bool? SyncEnabled { get; init; }
    public DateTime? LastSyncedUtc { get; init; }
    public IReadOnlyList<string> ProxyAddresses { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Licenses { get; init; } = Array.Empty<string>();

    /// <summary>e.g. "Microsoft 365 Group", "Security", "Distribution" - null for user objects.</summary>
    public string? GroupTypeDescription { get; init; }
    public int? MemberCount { get; init; }
}
