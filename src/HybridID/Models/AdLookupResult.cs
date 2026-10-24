using System;
using System.Collections.Generic;

namespace HybridID.Models;

public sealed class AdLookupResult
{
    public required string DistinguishedName { get; init; }
    public required string Name { get; init; }

    /// <summary>Leaf objectCategory CN, lowercased (e.g. "person", "group", "computer").</summary>
    public required string ObjectCategory { get; init; }

    public string? SamAccountName { get; init; }
    public string? UserPrincipalName { get; init; }
    public string? Mail { get; init; }
    public string? TargetAddress { get; init; }
    public string? MsExchHomeServerName { get; init; }
    public Guid? ObjectGuid { get; init; }

    public bool? AccountEnabled { get; init; }
    public bool? IsLockedOut { get; init; }
    public bool? PasswordNeverExpires { get; init; }
    public DateTime? PasswordLastSetUtc { get; init; }
    public IReadOnlyList<string> ProxyAddresses { get; init; } = Array.Empty<string>();

    /// <summary>e.g. "Security (Global)", "Distribution (Universal)" - null for non-group objects.</summary>
    public string? GroupTypeDescription { get; init; }
    public int? MemberCount { get; init; }

    public bool IsPerson => ObjectCategory == "person";
    public bool IsGroup => ObjectCategory == "group";
}
