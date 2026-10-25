using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HybridID.Models;
using Microsoft.Graph;
using Microsoft.Graph.Models.ODataErrors;

namespace HybridID.Services;

/// <summary>
/// Cross-platform replacement for Get-MgUser / Get-MgGroup, using the official Microsoft.Graph
/// C# SDK directly instead of shelling out to the Graph PowerShell cmdlets.
/// </summary>
public sealed class GraphIdentityService
{
    private static readonly string[] UserSelect =
    {
        "id", "displayName", "userPrincipalName", "mail", "accountEnabled", "lastPasswordChangeDateTime",
        "onPremisesSyncEnabled", "onPremisesLastSyncDateTime", "proxyAddresses", "assignedLicenses",
    };

    private static readonly string[] GroupSelect =
    {
        "id", "displayName", "mail", "groupTypes", "mailEnabled", "securityEnabled",
    };

    // SKU part number (licenseDetails.skuPartNumber) -> friendly name for the SKUs a helpdesk
    // tech most often sees. Part numbers are stable and readable, so an unmapped SKU still shows
    // something meaningful (e.g. "VISIOCLIENT") instead of a GUID. Deliberately not fetched from
    // /subscribedSkus at runtime: that endpoint needs Organization.Read.All, a permission this
    // app doesn't otherwise need - not worth the extra admin-consent ask. (The old GUID table
    // mixed up several SKU IDs and service-plan IDs, e.g. M365 E5 showed as "E3 (no Teams)".)
    private static readonly Dictionary<string, string> KnownSkuParts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SPE_E5"] = "Microsoft 365 E5",
        ["SPE_E3"] = "Microsoft 365 E3",
        ["SPE_F1"] = "Microsoft 365 F3",
        ["SPB"] = "Microsoft 365 Business Premium",
        ["O365_BUSINESS_PREMIUM"] = "Microsoft 365 Business Standard",
        ["O365_BUSINESS_ESSENTIALS"] = "Microsoft 365 Business Basic",
        ["STANDARDPACK"] = "Office 365 E1",
        ["ENTERPRISEPACK"] = "Office 365 E3",
        ["ENTERPRISEPREMIUM"] = "Office 365 E5",
        ["DESKLESSPACK"] = "Office 365 F3",
        ["EXCHANGESTANDARD"] = "Exchange Online (Plan 1)",
        ["EXCHANGEENTERPRISE"] = "Exchange Online (Plan 2)",
        ["EXCHANGEDESKLESS"] = "Exchange Online Kiosk",
        ["EMS"] = "Enterprise Mobility + Security E3",
        ["EMSPREMIUM"] = "Enterprise Mobility + Security E5",
        ["AAD_PREMIUM"] = "Microsoft Entra ID P1",
        ["AAD_PREMIUM_P2"] = "Microsoft Entra ID P2",
        ["Microsoft_365_Copilot"] = "Microsoft 365 Copilot",
        ["POWER_BI_PRO"] = "Power BI Pro",
        ["POWER_BI_STANDARD"] = "Power BI (free)",
        ["FLOW_FREE"] = "Microsoft Power Automate Free",
    };

    private readonly GraphServiceClient _client;

    public GraphIdentityService(GraphServiceClient client)
    {
        _client = client;
    }

    /// <summary>Returns every matching object (0, 1, or more - the caller decides how to handle ambiguity).</summary>
    public async Task<IReadOnlyList<GraphLookupResult>> FindObjectsAsync(string identity, string? userPrincipalNameHint)
    {
        var upnToTry = string.IsNullOrEmpty(userPrincipalNameHint) ? identity : userPrincipalNameHint;

        // A direct id/UPN lookup is inherently unique - only the group filter path below can
        // return more than one match, so if a user resolves, that's the answer.
        var user = await TryGetUserAsync(upnToTry);
        if (user is not null)
        {
            return new[] { MapUser(user, await GetLicenseNamesAsync(user)) };
        }

        var groups = await FindGroupsAsync(identity);
        var results = new List<GraphLookupResult>(groups.Count);
        foreach (var group in groups)
        {
            results.Add(await MapGroupAsync(group));
        }
        return results;
    }

    private async Task<Microsoft.Graph.Models.User?> TryGetUserAsync(string userIdOrUpn)
    {
        try
        {
            return await _client.Users[userIdOrUpn].GetAsync(rq =>
            {
                rq.QueryParameters.Select = UserSelect;
            });
        }
        catch (ODataError ex) when (IsNotFound(ex))
        {
            // 404 not found or 400 for an identity string Graph doesn't accept as a user key:
            // fall through to the group lookup. Anything else (401/403, throttling, outages)
            // propagates so the search reports Entra ID as not searched instead of "no match".
            return null;
        }
    }

    private async Task<List<Microsoft.Graph.Models.Group>> FindGroupsAsync(string identity)
    {
        // Try a direct id lookup first (needed to cleanly re-resolve an ambiguous candidate the
        // user picked, whose Identity is a Graph object id, not a mail/displayName).
        try
        {
            var byId = await _client.Groups[identity].GetAsync(rq => rq.QueryParameters.Select = GroupSelect);
            if (byId is not null)
            {
                return new List<Microsoft.Graph.Models.Group> { byId };
            }
        }
        catch (ODataError ex) when (IsNotFound(ex))
        {
        }

        try
        {
            var escaped = identity.Replace("'", "''");
            var response = await _client.Groups.GetAsync(rq =>
            {
                rq.QueryParameters.Filter = $"mail eq '{escaped}' or displayName eq '{escaped}'";
                rq.QueryParameters.Select = GroupSelect;
                rq.QueryParameters.Count = true;
                rq.Headers.Add("ConsistencyLevel", "eventual");
            });
            return response?.Value ?? new List<Microsoft.Graph.Models.Group>();
        }
        catch (ODataError ex) when (IsNotFound(ex))
        {
            return new List<Microsoft.Graph.Models.Group>();
        }
    }

    private static bool IsNotFound(ODataError ex) => ex.ResponseStatusCode is 400 or 404;

    /// <summary>
    /// Friendly names from licenseDetails (same User.Read.All scope). If that call fails, falls
    /// back to the assigned SKU IDs so the license count is still right.
    /// </summary>
    private async Task<IReadOnlyList<string>> GetLicenseNamesAsync(Microsoft.Graph.Models.User user)
    {
        if (user.AssignedLicenses is not { Count: > 0 } assigned)
        {
            return Array.Empty<string>();
        }

        try
        {
            var details = await _client.Users[user.Id].LicenseDetails.GetAsync(rq =>
                rq.QueryParameters.Select = new[] { "skuId", "skuPartNumber" });
            if (details?.Value is { Count: > 0 } list)
            {
                return list.Select(d => DescribeSku(d.SkuPartNumber, d.SkuId)).ToList();
            }
        }
        catch (ODataError)
        {
        }

        return assigned.Select(l => DescribeSku(null, l.SkuId)).ToList();
    }

    private static GraphLookupResult MapUser(Microsoft.Graph.Models.User user, IReadOnlyList<string> licenses) => new()
    {
        Kind = GraphObjectKind.User,
        Id = user.Id!,
        DisplayName = user.DisplayName ?? user.UserPrincipalName ?? user.Id!,
        UserPrincipalName = user.UserPrincipalName,
        Mail = user.Mail,
        AccountEnabled = user.AccountEnabled,
        PasswordLastSetUtc = user.LastPasswordChangeDateTime?.UtcDateTime,
        SyncEnabled = user.OnPremisesSyncEnabled,
        LastSyncedUtc = user.OnPremisesLastSyncDateTime?.UtcDateTime,
        ProxyAddresses = user.ProxyAddresses?.ToList() ?? new List<string>(),
        Licenses = licenses,
    };

    private async Task<GraphLookupResult> MapGroupAsync(Microsoft.Graph.Models.Group group)
    {
        var memberCount = await TryGetMemberCountAsync(group.Id!);

        return new GraphLookupResult
        {
            Kind = GraphObjectKind.Group,
            Id = group.Id!,
            DisplayName = group.DisplayName ?? group.Id!,
            Mail = group.Mail,
            GroupTypeDescription = DescribeGroupType(group),
            MemberCount = memberCount,
        };
    }

    private async Task<int?> TryGetMemberCountAsync(string groupId)
    {
        try
        {
            return (int?)await _client.Groups[groupId].Members.Count.GetAsync(rq =>
                rq.Headers.Add("ConsistencyLevel", "eventual"));
        }
        catch (ODataError)
        {
            return null;
        }
    }

    private static string DescribeGroupType(Microsoft.Graph.Models.Group group)
    {
        if (group.GroupTypes?.Contains("Unified") == true)
        {
            return "Microsoft 365 Group";
        }
        if (group.MailEnabled == true && group.SecurityEnabled == true)
        {
            return "Mail-Enabled Security";
        }
        if (group.SecurityEnabled == true)
        {
            return "Security";
        }
        if (group.MailEnabled == true)
        {
            return "Distribution";
        }
        return "Unknown";
    }

    private static string DescribeSku(string? partNumber, Guid? skuId)
    {
        if (!string.IsNullOrEmpty(partNumber))
        {
            return KnownSkuParts.TryGetValue(partNumber, out var name) ? name : partNumber;
        }

        return skuId is { } id ? $"License ({id})" : "Unknown license";
    }
}
