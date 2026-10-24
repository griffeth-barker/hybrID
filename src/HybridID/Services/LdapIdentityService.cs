using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Net;
using System.Text;
using HybridID.Models;

namespace HybridID.Services;

/// <summary>
/// Cross-platform replacement for the RSAT ActiveDirectory module's Get-ADObject, built directly
/// on System.DirectoryServices.Protocols (the same base-class-library LDAP client that PSOpenAD
/// wraps). No domain join required; talks raw LDAP to the domain controller.
/// </summary>
public sealed class LdapIdentityService
{
    // userAccountControl / msDS-User-Account-Control-Computed bit flags (same layout on both).
    private const int UfAccountDisable = 0x0002;
    private const int UfLockout = 0x0010;
    private const int UfPasswordNeverExpires = 0x10000;

    // groupType bit flags.
    private const int GroupTypeGlobal = 0x00000002;
    private const int GroupTypeDomainLocal = 0x00000004;
    private const int GroupTypeUniversal = 0x00000008;
    private const int GroupTypeSecurityEnabled = unchecked((int)0x80000000);

    private static readonly string[] RequestedAttributes =
    {
        "distinguishedName", "name", "objectCategory", "objectGUID",
        "sAMAccountName", "userPrincipalName", "mail",
        "targetAddress", "msExchHomeServerName",
        "userAccountControl", "msDS-User-Account-Control-Computed", "pwdLastSet", "proxyAddresses",
        "groupType", "member",
    };

    /// <summary>
    /// StartTLS is Windows-only in practice: System.DirectoryServices.Protocols' Unix
    /// implementation does not support StartTransportLayerSecurity (confirmed by testing against
    /// a real DC on macOS - the extended operation fails regardless of certificate trust). LDAPS
    /// (implicit TLS) works reliably cross-platform and is what this app uses on non-Windows even
    /// if the caller asks for StartTLS.
    /// </summary>
    public LdapConnection Connect(string server, int port, bool useLdaps, bool useStartTls,
        bool skipCertificateCheck, string username, string password)
    {
        var connection = new LdapConnection(new LdapDirectoryIdentifier(server, port))
        {
            AuthType = AuthType.Basic,
            AutoBind = false,
        };
        connection.SessionOptions.ProtocolVersion = 3;

        if (skipCertificateCheck && OperatingSystem.IsWindows())
        {
            // Also Windows-only: setting VerifyServerCertificate throws on Unix. There, cert
            // trust is controlled by the OS/OpenLDAP TLS config (e.g. LDAPTLS_REQCERT set in the
            // environment before the app launches) rather than programmatically at runtime -
            // documented in docs/getting-started.md for lab/self-signed environments.
            connection.SessionOptions.VerifyServerCertificate = (_, _) => true;
        }

        if (useStartTls && !OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "StartTLS is not supported on this OS (a System.DirectoryServices.Protocols limitation, " +
                "not an AD limitation). Switch the Encryption setting to LDAPS.");
        }

        if (useLdaps)
        {
            connection.SessionOptions.SecureSocketLayer = true;
        }
        else if (useStartTls)
        {
            connection.SessionOptions.StartTransportLayerSecurity(null);
        }

        try
        {
            connection.Bind(new NetworkCredential(username, password));
        }
        catch (LdapException) when (skipCertificateCheck && !OperatingSystem.IsWindows())
        {
            // The VerifyServerCertificate bypass above only works on Windows; on Unix this is
            // very likely a self-signed-certificate rejection with no way to fix it in-app.
            throw new InvalidOperationException(
                "Could not connect (likely a certificate trust failure). Skipping certificate " +
                "validation isn't supported by .NET on macOS/Linux - set the environment variable " +
                "LDAPTLS_REQCERT=never before launching hybrID instead.");
        }

        return connection;
    }

    /// <summary>Reads defaultNamingContext from RootDSE, used when no base DN is configured.</summary>
    public string DiscoverDefaultNamingContext(LdapConnection connection)
    {
        var request = new SearchRequest(null, "(objectClass=*)", SearchScope.Base, "defaultNamingContext");
        var response = (SearchResponse)connection.SendRequest(request);

        if (response.Entries.Count == 0)
        {
            throw new InvalidOperationException("Could not read defaultNamingContext from the server's RootDSE.");
        }

        return (string)response.Entries[0].Attributes["defaultNamingContext"][0];
    }

    /// <summary>Returns every matching object (0, 1, or more - the caller decides how to handle ambiguity).</summary>
    public IReadOnlyList<AdLookupResult> FindObjects(LdapConnection connection, string baseDn, string identity)
    {
        var filter = BuildIdentityFilter(identity);
        var request = new SearchRequest(baseDn, filter, SearchScope.Subtree, RequestedAttributes);

        var response = (SearchResponse)connection.SendRequest(request);
        return response.Entries.Cast<SearchResultEntry>().Select(MapEntry).ToList();
    }

    private static string BuildIdentityFilter(string identity)
    {
        var clauses = new StringBuilder();
        clauses.Append($"(sAMAccountName={EscapeFilterValue(identity)})");
        clauses.Append($"(userPrincipalName={EscapeFilterValue(identity)})");

        if (Guid.TryParse(identity, out var guid))
        {
            clauses.Append($"(objectGUID={EscapeGuidForFilter(guid)})");
        }

        return $"(|{clauses})";
    }

    private static string EscapeFilterValue(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\5c"); break;
                case '*': sb.Append("\\2a"); break;
                case '(': sb.Append("\\28"); break;
                case ')': sb.Append("\\29"); break;
                case '\0': sb.Append("\\00"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    private static string EscapeGuidForFilter(Guid guid)
    {
        var sb = new StringBuilder(48);
        foreach (var b in guid.ToByteArray())
        {
            sb.Append('\\').Append(b.ToString("x2"));
        }
        return sb.ToString();
    }

    private static AdLookupResult MapEntry(SearchResultEntry entry)
    {
        var objectCategory = GetObjectCategoryLeaf(entry);
        var uac = GetInt(entry, "userAccountControl");
        var uacComputed = GetInt(entry, "msDS-User-Account-Control-Computed");

        return new AdLookupResult
        {
            DistinguishedName = entry.DistinguishedName,
            Name = GetString(entry, "name") ?? entry.DistinguishedName,
            ObjectCategory = objectCategory,
            SamAccountName = GetString(entry, "sAMAccountName"),
            UserPrincipalName = GetString(entry, "userPrincipalName"),
            Mail = GetString(entry, "mail"),
            TargetAddress = GetString(entry, "targetAddress"),
            MsExchHomeServerName = GetString(entry, "msExchHomeServerName"),
            ObjectGuid = GetGuid(entry, "objectGUID"),
            AccountEnabled = objectCategory == "person" && uac is { } u ? (u & UfAccountDisable) == 0 : null,
            IsLockedOut = objectCategory == "person" && uacComputed is { } uc ? (uc & UfLockout) != 0 : null,
            PasswordNeverExpires = objectCategory == "person" && uac is { } u2 ? (u2 & UfPasswordNeverExpires) != 0 : null,
            PasswordLastSetUtc = GetFileTime(entry, "pwdLastSet"),
            ProxyAddresses = GetStrings(entry, "proxyAddresses"),
            GroupTypeDescription = objectCategory == "group" ? DescribeGroupType(GetInt(entry, "groupType")) : null,
            MemberCount = objectCategory == "group" ? GetStrings(entry, "member").Count : null,
        };
    }

    private static string DescribeGroupType(int? groupType)
    {
        if (groupType is not { } gt)
        {
            return "Unknown";
        }

        var category = (gt & GroupTypeSecurityEnabled) != 0 ? "Security" : "Distribution";
        var scope = (gt & GroupTypeUniversal) != 0 ? "Universal"
            : (gt & GroupTypeGlobal) != 0 ? "Global"
            : (gt & GroupTypeDomainLocal) != 0 ? "Domain Local"
            : "Unknown Scope";

        return $"{category} ({scope})";
    }

    private static string GetObjectCategoryLeaf(SearchResultEntry entry)
    {
        var dn = GetString(entry, "objectCategory");
        if (string.IsNullOrEmpty(dn))
        {
            return "unknown";
        }

        // objectCategory is a DN like "CN=Person,CN=Schema,CN=Configuration,DC=...".
        var firstComma = dn.IndexOf(',');
        var rdn = firstComma >= 0 ? dn[..firstComma] : dn;
        var cn = rdn.StartsWith("CN=", StringComparison.OrdinalIgnoreCase) ? rdn[3..] : rdn;
        return cn.ToLowerInvariant();
    }

    private static string? GetString(SearchResultEntry entry, string attributeName)
    {
        if (!entry.Attributes.Contains(attributeName))
        {
            return null;
        }

        var values = entry.Attributes[attributeName].GetValues(typeof(string));
        return values.Length > 0 ? (string)values[0] : null;
    }

    private static IReadOnlyList<string> GetStrings(SearchResultEntry entry, string attributeName)
    {
        if (!entry.Attributes.Contains(attributeName))
        {
            return Array.Empty<string>();
        }

        // Note: AD servers typically cap the values returned for a multi-valued attribute in a
        // single query (commonly 1500) - a very large group's member count may be an undercount.
        return entry.Attributes[attributeName].GetValues(typeof(string)).Cast<string>().ToList();
    }

    private static int? GetInt(SearchResultEntry entry, string attributeName)
    {
        var raw = GetString(entry, attributeName);
        return raw is not null && int.TryParse(raw, out var value) ? value : null;
    }

    private static DateTime? GetFileTime(SearchResultEntry entry, string attributeName)
    {
        var raw = GetString(entry, attributeName);
        if (raw is null || !long.TryParse(raw, out var fileTime) || fileTime <= 0)
        {
            // 0 means "never set" (e.g. must change password at next logon) - not a real date.
            return null;
        }

        return DateTime.FromFileTimeUtc(fileTime);
    }

    private static Guid? GetGuid(SearchResultEntry entry, string attributeName)
    {
        if (!entry.Attributes.Contains(attributeName))
        {
            return null;
        }

        var values = entry.Attributes[attributeName].GetValues(typeof(byte[]));
        return values.Length > 0 ? new Guid((byte[])values[0]) : null;
    }
}
