using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HybridID.Models;

namespace HybridID.Services;

/// <summary>
/// Sample data for reviewing the Search page without a domain controller or tenant
/// (start with --preview or HYBRID_PREVIEW=1). One identity per verdict the page can show;
/// links are built from the real URL templates so they look right, but point at fake IDs.
/// </summary>
public sealed class PreviewLookupSource : ILookupSource
{
    /// <summary>Sample identities and what each demonstrates (listed in the preview banner).</summary>
    public static readonly IReadOnlyList<(string Identity, string Shows)> Samples = new[]
    {
        ("jsmith", "hybrid user, remote mailbox"),
        ("bwayne", "on-prem only, locked out"),
        ("acloud@contoso.com", "cloud-only user"),
        ("finance-team", "hybrid mail-enabled group"),
        ("project-x", "cloud-only Microsoft 365 group"),
        ("svc-backup", "no mailbox"),
        ("smith", "multiple matches"),
        ("adskip", "AD skipped"),
        ("adfail", "AD unreachable"),
        ("signin", "sign-in didn't finish"),
        ("grapherror", "Graph can't start"),
        ("nobody", "no match"),
    };

    private const string Domain = "contoso.com";
    private const string BaseDn = "DC=contoso,DC=com";

    private readonly AppConfig _config;

    public PreviewLookupSource(AppConfig config)
    {
        _config = config;
    }

    public bool IsPreview => true;

    public async Task<LookupOutcome> LookupAsync(string identity, Action<string> reportPhase)
    {
        var key = identity.Trim().ToLowerInvariant();
        if (key.EndsWith("@" + Domain, StringComparison.Ordinal) && key != "acloud@" + Domain)
        {
            key = key[..key.IndexOf('@')];
        }

        if (key == "grapherror")
        {
            await Task.Delay(300);
            throw new LookupInitException(
                "The tenant 'contoso-typo' wasn't found. Check the tenant and client ID in Settings.");
        }

        var adInPlay = key is not ("adskip" or "adfail");
        if (key == "adfail")
        {
            reportPhase("Connecting to Active Directory…");
            await Task.Delay(600);
        }

        if (key == "signin")
        {
            reportPhase("Waiting for Microsoft sign-in…");
            await Task.Delay(1200);
        }

        reportPhase(adInPlay ? "Searching Active Directory and Microsoft Graph…" : "Searching Microsoft Graph…");
        await Task.Delay(800);

        return key switch
        {
            "jsmith" or "3f2504e0-4f89-11d3-9a0c-0305e82c3301" => Outcome(HybridUser()),
            "bwayne" => Outcome(OnPremUser()),
            "acloud@contoso.com" or "acloud" => Outcome(CloudUser()),
            "finance-team" => Outcome(HybridGroup()),
            "project-x" => Outcome(CloudGroup()),
            "svc-backup" => Outcome(NoMailboxUser()),
            "smith" => Outcome(Ambiguous()),
            "jsmith2" => Outcome(HybridUser("John Smith (Contractor)", "jsmith2", "9b1d4c77-2e0a-4f61-8d3e-55c1a0f2b9e4")),
            "7c1e6a52-93d4-4b7e-a1f0-2d8e6b3c9a15" => Outcome(CloudUser("John Smith (Guest)", "john.smith_fabrikam.com#EXT#@contoso.onmicrosoft.com")),
            "adskip" => new LookupOutcome { Result = CloudUser("Ada Skip", "adskip@" + Domain), AdCoverage = AdCoverage.Skipped },
            "adfail" => new LookupOutcome
            {
                Result = CloudUser("Ada Fail", "adfail@" + Domain),
                AdCoverage = AdCoverage.Unreachable,
                AdError = "The LDAP server is unavailable (dc01.contoso.com:636)",
            },
            "signin" => new LookupOutcome { Result = NotFound(), DeviceCodeShown = true },
            _ => Outcome(NotFound()),
        };
    }

    public async Task<bool> PrepareAdRetryAsync()
    {
        await Task.Delay(200);
        return true;
    }

    private static LookupOutcome Outcome(IdentityLookupResult result) => new() { Result = result };

    private string Url(string template, string id) =>
        template.Replace("{id}", id).Replace("{fqdn}", _config.Settings.OnPremExchangeFqdn);

    private IdentityLookupResult HybridUser(
        string name = "John Smith", string sam = "jsmith", string guid = "3f2504e0-4f89-11d3-9a0c-0305e82c3301")
    {
        var entraId = "1d2c3b4a-5e6f-4a7b-8c9d-0e1f2a3b4c5d";
        var upn = $"{sam}@{Domain}";
        return new IdentityLookupResult
        {
            StatusLevel = StatusLevel.Success,
            StatusMessage = "Object located in Active Directory.",
            Name = name,
            ObjectClass = "user",
            ObjectKind = ObjectKind.User,
            Notes = "Mailbox is in EXO, but managed as a Remote Mailbox via On-Premises Exchange.\n\nSync Status: Synced to Entra ID.",
            ManageIdentity = ManagementLink.AsText("Active Directory"),
            ManageExchange = ManagementLink.AsLink("Exchange Server ECP", Url(_config.ManagementUrls.OnPremEcpMailbox, guid)),
            MailboxLocation = MailboxLocation.RemoteMailbox,
            SyncState = SyncState.Hybrid,
            AccountEnabled = true,
            IsLockedOut = false,
            PasswordNeverExpires = false,
            PasswordLastSetUtc = DateTime.UtcNow.AddDays(-193),
            LastSyncedUtc = DateTime.UtcNow.AddMinutes(-17),
            SamAccountName = sam,
            UserPrincipalName = upn,
            Mail = $"john.smith@{Domain}",
            ObjectGuid = Guid.Parse(guid),
            DistinguishedName = $"CN={name},OU=Users,OU=HQ,{BaseDn}",
            TargetAddress = $"SMTP:{sam}@contoso.mail.onmicrosoft.com",
            EntraObjectId = entraId,
            EntraSyncEnabled = true,
            ProxyAddresses = new[]
            {
                $"SMTP:john.smith@{Domain}",
                $"smtp:{sam}@{Domain}",
                $"smtp:{sam}@contoso.mail.onmicrosoft.com",
                $"sip:john.smith@{Domain}",
                "X500:/o=ExchangeLabs/ou=Exchange Administrative Group (FYDIBOHF23SPDLT)/cn=Recipients/cn=4f2a9c1e8d7b4e6a-jsmith",
            },
            Licenses = new[] { "Microsoft 365 E3" },
            ViewLinks = new[]
            {
                ManagementLink.AsLink("View in Entra ID", Url(_config.ManagementUrls.EntraUser, entraId)),
                ManagementLink.AsLink("View in Exchange Online", Url(_config.ManagementUrls.ExoMailbox, entraId)),
            },
        };
    }

    private IdentityLookupResult OnPremUser()
    {
        var guid = "a8f3e2d1-6c5b-4a49-8e7f-1b2c3d4e5f60";
        return new IdentityLookupResult
        {
            StatusLevel = StatusLevel.Success,
            StatusMessage = "Object located in Active Directory.",
            Name = "Bruce Wayne",
            ObjectClass = "user",
            ObjectKind = ObjectKind.User,
            Notes = "Mailbox resides entirely on-premises.",
            ManageIdentity = ManagementLink.AsText("On-Premises Active Directory"),
            ManageExchange = ManagementLink.AsLink("Exchange Server ECP", Url(_config.ManagementUrls.OnPremEcpMailbox, guid)),
            MailboxLocation = MailboxLocation.OnPremMailbox,
            SyncState = SyncState.OnPremOnly,
            AccountEnabled = true,
            IsLockedOut = true,
            PasswordNeverExpires = true,
            PasswordLastSetUtc = DateTime.UtcNow.AddDays(-1240),
            SamAccountName = "bwayne",
            UserPrincipalName = "bwayne@" + Domain,
            Mail = "bruce.wayne@" + Domain,
            ObjectGuid = Guid.Parse(guid),
            DistinguishedName = $"CN=Bruce Wayne,OU=Executives,OU=HQ,{BaseDn}",
            ProxyAddresses = new[] { "SMTP:bruce.wayne@" + Domain, "smtp:bwayne@" + Domain },
        };
    }

    private IdentityLookupResult CloudUser(string name = "Alex Cloud", string upn = "acloud@" + Domain)
    {
        var entraId = "7c1e6a52-93d4-4b7e-a1f0-2d8e6b3c9a15";
        return new IdentityLookupResult
        {
            StatusLevel = StatusLevel.Success,
            StatusMessage = "Object located in Entra ID.",
            Name = name,
            ObjectClass = "Cloud User",
            ObjectKind = ObjectKind.User,
            Notes = "This object does not exist in Active Directory. Manage it entirely in M365/Entra ID.",
            ManageIdentity = ManagementLink.AsLink("Entra ID", Url(_config.ManagementUrls.EntraUser, entraId)),
            ManageExchange = ManagementLink.AsLink("Exchange Online", Url(_config.ManagementUrls.ExoMailbox, entraId)),
            MailboxLocation = MailboxLocation.ExchangeOnline,
            SyncState = SyncState.CloudOnly,
            AccountEnabled = true,
            PasswordLastSetUtc = DateTime.UtcNow.AddDays(-12),
            UserPrincipalName = upn,
            Mail = upn,
            EntraObjectId = entraId,
            EntraSyncEnabled = false,
            ProxyAddresses = new[] { "SMTP:" + upn, $"smtp:{upn[..upn.IndexOf('@')]}@contoso.onmicrosoft.com" },
            Licenses = new[] { "Microsoft 365 E5", "Microsoft Teams Phone Standard", "Power BI Pro" },
        };
    }

    private IdentityLookupResult HybridGroup()
    {
        var guid = "c4d5e6f7-0819-4a2b-9c3d-4e5f60718293";
        var entraId = "e1f2a3b4-c5d6-4e7f-8091-a2b3c4d5e6f7";
        return new IdentityLookupResult
        {
            StatusLevel = StatusLevel.Success,
            StatusMessage = "Object located in Active Directory.",
            Name = "Finance Team",
            ObjectClass = "group",
            ObjectKind = ObjectKind.Group,
            Notes = "Mail-enabled group. Manage exchange properties via On-Prem ECP.\n\nSync Status: Synced to Entra ID.",
            ManageIdentity = ManagementLink.AsText("Active Directory"),
            ManageExchange = ManagementLink.AsLink("Exchange Server ECP", Url(_config.ManagementUrls.OnPremEcpGroup, guid)),
            MailboxLocation = MailboxLocation.OnPremMailEnabledGroup,
            SyncState = SyncState.Hybrid,
            LastSyncedUtc = DateTime.UtcNow.AddMinutes(-42),
            SamAccountName = "finance-team",
            Mail = "finance@" + Domain,
            ObjectGuid = Guid.Parse(guid),
            DistinguishedName = $"CN=Finance Team,OU=Groups,OU=HQ,{BaseDn}",
            EntraObjectId = entraId,
            EntraSyncEnabled = true,
            ProxyAddresses = new[] { "SMTP:finance@" + Domain, "smtp:finance-team@" + Domain },
            GroupTypeDescription = "Distribution (Universal)",
            MemberCount = 1500,
            ViewLinks = new[] { ManagementLink.AsLink("View in Entra ID", Url(_config.ManagementUrls.EntraGroup, entraId)) },
        };
    }

    private IdentityLookupResult CloudGroup()
    {
        var entraId = "0a1b2c3d-4e5f-4061-8273-94a5b6c7d8e9";
        return new IdentityLookupResult
        {
            StatusLevel = StatusLevel.Success,
            StatusMessage = "Object located in Entra ID.",
            Name = "Project X",
            ObjectClass = "Cloud Group",
            ObjectKind = ObjectKind.Group,
            Notes = "This object does not exist in Active Directory. Manage it entirely in M365/Entra ID.",
            ManageIdentity = ManagementLink.AsLink("Entra ID", Url(_config.ManagementUrls.EntraGroup, entraId)),
            ManageExchange = ManagementLink.AsLink("Exchange Online", Url(_config.ManagementUrls.ExoGroup, entraId)),
            MailboxLocation = MailboxLocation.ExchangeOnline,
            SyncState = SyncState.CloudOnly,
            Mail = "project-x@" + Domain,
            EntraObjectId = entraId,
            EntraSyncEnabled = false,
            ProxyAddresses = new[] { "SMTP:project-x@" + Domain, "smtp:project-x@contoso.onmicrosoft.com", "SPO:SPO_8f1e2d3c-4b5a-6978-8a9b-0c1d2e3f4a5b@SPO_contoso" },
            GroupTypeDescription = "Microsoft 365 Group",
            MemberCount = 23,
        };
    }

    private IdentityLookupResult NoMailboxUser()
    {
        var guid = "5e6f7081-92a3-4b4c-8d5e-6f708192a3b4";
        return new IdentityLookupResult
        {
            StatusLevel = StatusLevel.Success,
            StatusMessage = "Object located in Active Directory.",
            Name = "Backup Service",
            ObjectClass = "user",
            ObjectKind = ObjectKind.User,
            ManageIdentity = ManagementLink.AsText("On-Premises Active Directory"),
            ManageExchange = ManagementLink.AsText("No mailbox detected"),
            MailboxLocation = MailboxLocation.None,
            SyncState = SyncState.OnPremOnly,
            AccountEnabled = false,
            IsLockedOut = false,
            PasswordNeverExpires = true,
            PasswordLastSetUtc = DateTime.UtcNow.AddDays(-2210),
            SamAccountName = "svc-backup",
            UserPrincipalName = "svc-backup@" + Domain,
            ObjectGuid = Guid.Parse(guid),
            DistinguishedName = $"CN=Backup Service,OU=Service Accounts,{BaseDn}",
        };
    }

    private static IdentityLookupResult Ambiguous() => new()
    {
        StatusLevel = StatusLevel.Info,
        StatusMessage = "Multiple matches found (3). Select one to continue.",
        Candidates = new[]
        {
            new AmbiguousCandidate
            {
                DisplayName = "John Smith",
                Detail = "Active Directory - jsmith@" + Domain,
                Identity = "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
                Source = CandidateSource.ActiveDirectory,
            },
            new AmbiguousCandidate
            {
                DisplayName = "John Smith (Contractor)",
                Detail = "Active Directory - jsmith2@" + Domain,
                Identity = "jsmith2",
                Source = CandidateSource.ActiveDirectory,
            },
            new AmbiguousCandidate
            {
                DisplayName = "John Smith (Guest)",
                Detail = "Entra ID - 7c1e6a52-93d4-4b7e-a1f0-2d8e6b3c9a15",
                Identity = "7c1e6a52-93d4-4b7e-a1f0-2d8e6b3c9a15",
                Source = CandidateSource.EntraId,
            },
        },
    };

    private static IdentityLookupResult NotFound() => new()
    {
        StatusLevel = StatusLevel.Error,
        StatusMessage = "Object not found in Active Directory or Entra ID.",
    };
}
