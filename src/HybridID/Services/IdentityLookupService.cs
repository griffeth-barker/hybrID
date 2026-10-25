using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Threading.Tasks;
using HybridID.Models;

namespace HybridID.Services;

/// <summary>
/// Line-by-line port of hybrID/private/Invoke-Search.ps1's routing logic: given an identity,
/// determine where it (and its mailbox, if any) is actually managed. Pure query/decision logic,
/// no UI side effects - the view is responsible for status text and control binding.
/// </summary>
public sealed class IdentityLookupService
{
    private readonly LdapIdentityService _ldap;
    private readonly AppConfig _config;

    public IdentityLookupService(LdapIdentityService ldap, AppConfig config)
    {
        _ldap = ldap;
        _config = config;
    }

    /// <summary>
    /// The Graph service is supplied per-call rather than injected at construction because the
    /// Graph client is only available lazily, after the first device-code sign-in completes.
    /// </summary>
    public async Task<IdentityLookupResult> LookupAsync(
        LdapConnection? ldapConnection, string ldapBaseDn, GraphIdentityService graph, string identity)
    {
        var (adCandidates, adError) = await FindAdCandidatesAsync(ldapConnection, ldapBaseDn, identity);

        // A unique AD match's UserPrincipalName is a strong hint for the Graph lookup, matching
        // the original PowerShell's $upnToSearchInGraph logic. With zero or multiple AD matches
        // there's no single UPN to hint with, so Graph searches on the raw identity instead.
        var upnHint = adCandidates.Count == 1 ? adCandidates[0].UserPrincipalName : null;

        IReadOnlyList<GraphLookupResult> graphCandidates;
        string? graphError = null;
        try
        {
            graphCandidates = await graph.FindObjectsAsync(identity, upnHint);
        }
        catch (Exception ex)
        {
            // Routing still runs on what AD returned (as before), but the error is reported so
            // the UI doesn't present a "not in Entra ID" verdict it can't back up.
            graphCandidates = Array.Empty<GraphLookupResult>();
            graphError = ex.Message;
        }

        var errors = new LookupErrors(adError, graphError);
        if (adCandidates.Count > 1 || graphCandidates.Count > 1)
        {
            return BuildAmbiguousResult(adCandidates, graphCandidates, errors);
        }

        return BuildResult(adCandidates.SingleOrDefault(), graphCandidates.SingleOrDefault(), errors);
    }

    /// <summary>Errors from either directory, carried onto every result shape.</summary>
    private readonly record struct LookupErrors(string? Ad, string? Graph);

    private async Task<(IReadOnlyList<AdLookupResult> Candidates, string? Error)> FindAdCandidatesAsync(
        LdapConnection? ldapConnection, string ldapBaseDn, string identity)
    {
        if (ldapConnection is null)
        {
            return (Array.Empty<AdLookupResult>(), null);
        }

        try
        {
            // System.DirectoryServices.Protocols has no async SendRequest overload; run it
            // off the calling thread so a slow DC doesn't freeze the UI.
            return (await Task.Run(() => _ldap.FindObjects(ldapConnection, ldapBaseDn, identity)), null);
        }
        catch (Exception ex)
        {
            // Connectivity/auth issues still fall through to "not found" for routing (as the
            // original did), but the error is returned so the UI can say AD wasn't searched.
            return (Array.Empty<AdLookupResult>(), ex.Message);
        }
    }

    private static IdentityLookupResult BuildAmbiguousResult(
        IReadOnlyList<AdLookupResult> adCandidates, IReadOnlyList<GraphLookupResult> graphCandidates, LookupErrors errors)
    {
        var candidates = new List<AmbiguousCandidate>();

        foreach (var a in adCandidates)
        {
            candidates.Add(new AmbiguousCandidate
            {
                DisplayName = a.Name,
                Detail = $"Active Directory - {(a.IsPerson ? a.UserPrincipalName ?? a.SamAccountName : a.DistinguishedName)}",
                // ObjectGuid re-search uniquely resolves this exact AD object next time, which
                // in turn re-derives the correct UPN hint for Graph - see LookupAsync above.
                Identity = a.ObjectGuid?.ToString() ?? a.DistinguishedName,
                Source = CandidateSource.ActiveDirectory,
            });
        }

        foreach (var g in graphCandidates)
        {
            candidates.Add(new AmbiguousCandidate
            {
                DisplayName = g.DisplayName,
                Detail = $"Entra ID - {g.Id}",
                Identity = g.Id,
                Source = CandidateSource.EntraId,
            });
        }

        return new IdentityLookupResult
        {
            StatusLevel = StatusLevel.Info,
            StatusMessage = $"Multiple matches found ({candidates.Count}). Select one to continue.",
            Candidates = candidates,
            AdError = errors.Ad,
            GraphError = errors.Graph,
        };
    }

    private IdentityLookupResult BuildResult(AdLookupResult? adObject, GraphLookupResult? graphObject, LookupErrors errors)
    {
        var urls = _config.ManagementUrls;
        var fqdn = _config.Settings.OnPremExchangeFqdn;

        string? entraUrl = null;
        string? exoUrl = null;
        string? onPremExchUrl = null;

        if (graphObject is not null)
        {
            var (entraTemplate, exoTemplate) = graphObject.Kind == GraphObjectKind.User
                ? (urls.EntraUser, urls.ExoMailbox)
                : (urls.EntraGroup, urls.ExoGroup);
            entraUrl = BuildUrl(entraTemplate, graphObject.Id);
            exoUrl = BuildUrl(exoTemplate, graphObject.Id);
        }

        if (adObject is { ObjectGuid: { } guid })
        {
            if (adObject.IsPerson)
            {
                onPremExchUrl = BuildUrl(urls.OnPremEcpMailbox, guid.ToString(), fqdn);
            }
            else if (adObject.IsGroup)
            {
                onPremExchUrl = BuildUrl(urls.OnPremEcpGroup, guid.ToString(), fqdn);
            }
        }

        if (adObject is not null)
        {
            return BuildAdFoundResult(adObject, graphObject, onPremExchUrl, entraUrl, exoUrl, errors);
        }

        if (graphObject is not null)
        {
            return new IdentityLookupResult
            {
                StatusLevel = StatusLevel.Success,
                StatusMessage = "Object located in Entra ID.",
                Name = graphObject.DisplayName,
                ObjectClass = graphObject.Kind == GraphObjectKind.User ? "Cloud User" : "Cloud Group",
                Notes = "This object does not exist in Active Directory. Manage it entirely in M365/Entra ID.",
                ManageIdentity = ManagementLink.AsLink("Entra ID", entraUrl),
                ManageExchange = ManagementLink.AsLink("Exchange Online", exoUrl),
                MailboxLocation = Models.MailboxLocation.ExchangeOnline,
                ObjectKind = graphObject.Kind == GraphObjectKind.User ? Models.ObjectKind.User : Models.ObjectKind.Group,
                UserPrincipalName = graphObject.UserPrincipalName,
                Mail = graphObject.Mail,
                EntraObjectId = graphObject.Id,
                EntraSyncEnabled = graphObject.SyncEnabled,
                AdError = errors.Ad,
                GraphError = errors.Graph,
                SyncState = Models.SyncState.CloudOnly,
                AccountEnabled = graphObject.AccountEnabled,
                PasswordLastSetUtc = graphObject.PasswordLastSetUtc,
                LastSyncedUtc = graphObject.LastSyncedUtc,
                ProxyAddresses = graphObject.ProxyAddresses,
                Licenses = graphObject.Licenses,
                GroupTypeDescription = graphObject.GroupTypeDescription,
                MemberCount = graphObject.MemberCount,
            };
        }

        return new IdentityLookupResult
        {
            StatusLevel = StatusLevel.Error,
            StatusMessage = "Object not found in Active Directory or Entra ID.",
            AdError = errors.Ad,
            GraphError = errors.Graph,
        };
    }

    private static IdentityLookupResult BuildAdFoundResult(
        AdLookupResult adObject, GraphLookupResult? graphObject, string? onPremExchUrl,
        string? entraUrl, string? exoUrl, LookupErrors errors)
    {
        var notes = "";
        ManagementLink manageExchange;
        MailboxLocation mailboxLocation;

        if (adObject.IsPerson)
        {
            if (IsRemoteMailboxTargetAddress(adObject.TargetAddress))
            {
                manageExchange = ManagementLink.AsLink("Exchange Server ECP", onPremExchUrl);
                mailboxLocation = MailboxLocation.RemoteMailbox;
                notes = "Mailbox is in EXO, but managed as a Remote Mailbox via On-Premises Exchange.";
            }
            else if (!string.IsNullOrEmpty(adObject.MsExchHomeServerName))
            {
                manageExchange = ManagementLink.AsLink("Exchange Server ECP", onPremExchUrl);
                mailboxLocation = MailboxLocation.OnPremMailbox;
                notes = "Mailbox resides entirely on-premises.";
            }
            else
            {
                manageExchange = ManagementLink.AsText("No mailbox detected");
                mailboxLocation = MailboxLocation.None;
            }
        }
        else if (adObject.IsGroup)
        {
            if (!string.IsNullOrEmpty(adObject.Mail))
            {
                manageExchange = ManagementLink.AsLink("Exchange Server ECP", onPremExchUrl);
                mailboxLocation = MailboxLocation.OnPremMailEnabledGroup;
                notes = "Mail-enabled group. Manage exchange properties via On-Prem ECP.";
            }
            else
            {
                manageExchange = ManagementLink.AsText("N/A");
                mailboxLocation = MailboxLocation.NotApplicable;
            }
        }
        else
        {
            manageExchange = ManagementLink.AsText("N/A");
            mailboxLocation = MailboxLocation.NotApplicable;
        }

        var manageIdentity = ManagementLink.AsText("On-Premises Active Directory");
        var syncState = Models.SyncState.OnPremOnly;
        if (graphObject is not null)
        {
            notes = notes.Length > 0
                ? notes + "\n\nSync Status: Synced to Entra ID."
                : "Sync Status: Synced to Entra ID.";
            manageIdentity = ManagementLink.AsText("Active Directory");
            syncState = Models.SyncState.Hybrid;
        }

        return new IdentityLookupResult
        {
            StatusLevel = StatusLevel.Success,
            StatusMessage = "Object located in Active Directory.",
            Name = adObject.Name,
            ObjectClass = DisplayObjectClass(adObject.ObjectCategory),
            Notes = notes,
            ManageIdentity = manageIdentity,
            ManageExchange = manageExchange,
            SyncState = syncState,
            MailboxLocation = mailboxLocation,
            ObjectKind = adObject.IsPerson ? Models.ObjectKind.User : adObject.IsGroup ? Models.ObjectKind.Group : Models.ObjectKind.Other,
            SamAccountName = adObject.SamAccountName,
            UserPrincipalName = adObject.UserPrincipalName,
            Mail = adObject.Mail,
            ObjectGuid = adObject.ObjectGuid,
            DistinguishedName = adObject.DistinguishedName,
            TargetAddress = adObject.TargetAddress,
            EntraObjectId = graphObject?.Id,
            EntraSyncEnabled = graphObject?.SyncEnabled,
            ViewLinks = BuildViewLinks(graphObject, mailboxLocation, entraUrl, exoUrl),
            AdError = errors.Ad,
            GraphError = errors.Graph,
            // AD is authoritative for account/password state on hybrid-synced identities - it's
            // what actually gates on-prem auth, which is what technicians are really asking about.
            AccountEnabled = adObject.AccountEnabled,
            IsLockedOut = adObject.IsLockedOut,
            PasswordNeverExpires = adObject.PasswordNeverExpires,
            PasswordLastSetUtc = adObject.PasswordLastSetUtc,
            LastSyncedUtc = graphObject?.LastSyncedUtc,
            ProxyAddresses = adObject.ProxyAddresses.Count > 0 ? adObject.ProxyAddresses : graphObject?.ProxyAddresses ?? Array.Empty<string>(),
            Licenses = graphObject?.Licenses ?? Array.Empty<string>(),
            GroupTypeDescription = adObject.GroupTypeDescription ?? graphObject?.GroupTypeDescription,
            MemberCount = adObject.MemberCount ?? graphObject?.MemberCount,
        };
    }

    /// <summary>
    /// Read-only links for systems that mirror an AD-managed object: Entra ID for synced objects,
    /// Exchange Online for remote mailboxes. Both URLs were already built in BuildResult.
    /// </summary>
    private static IReadOnlyList<ManagementLink> BuildViewLinks(
        GraphLookupResult? graphObject, MailboxLocation mailboxLocation, string? entraUrl, string? exoUrl)
    {
        if (graphObject is null)
        {
            return Array.Empty<ManagementLink>();
        }

        var links = new List<ManagementLink> { ManagementLink.AsLink("View in Entra ID", entraUrl) };
        if (mailboxLocation == MailboxLocation.RemoteMailbox)
        {
            links.Add(ManagementLink.AsLink("View in Exchange Online", exoUrl));
        }

        return links;
    }

    private static string BuildUrl(string template, string id, string? fqdn = null)
    {
        var url = template.Replace("{id}", id);
        return fqdn is null ? url : url.Replace("{fqdn}", fqdn);
    }

    private static bool IsRemoteMailboxTargetAddress(string? targetAddress) =>
        targetAddress is not null &&
        targetAddress.Contains('@', StringComparison.Ordinal) &&
        targetAddress.EndsWith(".mail.onmicrosoft.com", StringComparison.OrdinalIgnoreCase);

    private static string DisplayObjectClass(string objectCategory) => objectCategory switch
    {
        "person" => "user",
        "group" => "group",
        _ => objectCategory,
    };
}
