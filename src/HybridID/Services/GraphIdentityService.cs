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
        "id", "displayName", "userPrincipalName", "accountEnabled", "lastPasswordChangeDateTime",
        "onPremisesSyncEnabled", "onPremisesLastSyncDateTime", "proxyAddresses", "assignedLicenses",
    };

    private static readonly string[] GroupSelect =
    {
        "id", "displayName", "groupTypes", "mailEnabled", "securityEnabled",
    };

    // Common commercial SKU GUID -> friendly name. Microsoft's full list is long and stable;
    // this covers the SKUs a helpdesk tech is most likely to actually see. Unmapped SKUs fall
    // back to showing the raw GUID rather than failing - see DescribeSku. Deliberately not
    // fetched from /subscribedSkus at runtime: that endpoint needs Organization.Read.All, a
    // permission this app doesn't otherwise need - not worth the extra admin-consent ask.
    private static readonly Dictionary<string, string> KnownSkus = new(StringComparer.OrdinalIgnoreCase)
    {
        ["06ebc4ee-1bb5-47dd-8120-11324bc54e06"] = "Microsoft 365 E5",
        ["c7df2760-2c81-4ef7-b578-5b5392b571df"] = "Microsoft 365 E5 (no Teams)",
        ["05e9a617-0261-4cee-bb44-138d3ef5d965"] = "Microsoft 365 E3",
        ["06ebc4ee-1bb5-47dd-8120-11324bc54e06"] = "Microsoft 365 E3 (no Teams)",
        ["cbdc14ab-d96c-4c30-b9f4-6ada7cdc1d46"] = "Microsoft 365 Business Premium",
        ["3b555118-da6a-4418-894f-7df1e2f96fae"] = "Microsoft 365 Business Standard",
        ["dab7782a-93b1-4074-8bb1-0e61318bea0b"] = "Microsoft 365 Business Basic",
        ["18181a46-0d4e-45cd-891e-60aabd171b4e"] = "Office 365 E1",
        ["6634e0ce-1a9f-428c-a498-f84ec7b8aa2e"] = "Office 365 E3",
        ["c7df2760-2c81-4ef7-b578-5b5392b571df"] = "Office 365 E5",
        ["4b9405b0-7788-4568-add1-99614e613b69"] = "Exchange Online (Plan 1)",
        ["19ec0d23-8335-4cbd-94ac-6050e30712fa"] = "Exchange Online (Plan 2)",
        ["e43b5b99-8dfb-405f-9987-dc307f34980e"] = "Exchange Online Kiosk",
        ["f30db892-07e9-47e9-837c-80727f46fd3d"] = "Microsoft Power Automate Free",
        ["e97c048c-37a4-45fb-ab50-922fbf07a370"] = "Windows Store for Business",
        ["f245ecc8-75af-4f8e-b61f-27d8114de5f3"] = "Entra ID P1",
        ["eec0eb4f-6444-4f95-aba0-50c24d67f998"] = "Entra ID P2",
        ["11dee6af-eca8-419f-8061-6864517c1875"] = "Copilot for Microsoft 365",
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
            return new[] { MapUser(user) };
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
        catch (ODataError)
        {
            // Mirrors the original's Get-MgUser -ErrorAction SilentlyContinue: any Graph error
            // here (404 not found, 400 for an identity string Graph doesn't accept as a user
            // key, etc.) should fall through to the group lookup, not abort it.
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
        catch (ODataError)
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
        catch (ODataError)
        {
            return new List<Microsoft.Graph.Models.Group>();
        }
    }

    private static GraphLookupResult MapUser(Microsoft.Graph.Models.User user) => new()
    {
        Kind = GraphObjectKind.User,
        Id = user.Id!,
        DisplayName = user.DisplayName ?? user.UserPrincipalName ?? user.Id!,
        AccountEnabled = user.AccountEnabled,
        PasswordLastSetUtc = user.LastPasswordChangeDateTime?.UtcDateTime,
        SyncEnabled = user.OnPremisesSyncEnabled,
        LastSyncedUtc = user.OnPremisesLastSyncDateTime?.UtcDateTime,
        ProxyAddresses = user.ProxyAddresses?.ToList() ?? new List<string>(),
        Licenses = user.AssignedLicenses?.Select(l => DescribeSku(l.SkuId)).ToList() ?? new List<string>(),
    };

    private async Task<GraphLookupResult> MapGroupAsync(Microsoft.Graph.Models.Group group)
    {
        var memberCount = await TryGetMemberCountAsync(group.Id!);

        return new GraphLookupResult
        {
            Kind = GraphObjectKind.Group,
            Id = group.Id!,
            DisplayName = group.DisplayName ?? group.Id!,
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

    private static string DescribeSku(Guid? skuId)
    {
        if (skuId is not { } id)
        {
            return "Unknown license";
        }

        return KnownSkus.TryGetValue(id.ToString(), out var name) ? name : $"License ({id})";
    }
}
