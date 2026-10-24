using System.Text.Json.Serialization;

namespace HybridID.Models;

public sealed class AppConfig
{
    [JsonPropertyName("settings")]
    public AppSettings Settings { get; set; } = new();

    [JsonPropertyName("managementUrls")]
    public ManagementUrls ManagementUrls { get; set; } = new();
}

public sealed class AppSettings
{
    [JsonPropertyName("onPremExchangeFqdn")]
    public string OnPremExchangeFqdn { get; set; } = "mail.domain.tld";

    /// <summary>System, Light, or Dark.</summary>
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "System";

    [JsonPropertyName("ldapServer")]
    public string LdapServer { get; set; } = "";

    [JsonPropertyName("ldapPort")]
    public int LdapPort { get; set; } = 636;

    /// <summary>
    /// Negotiate StartTLS on the plain LDAP port before binding. Windows-only: not supported by
    /// System.DirectoryServices.Protocols on macOS/Linux. LDAPS is the cross-platform default.
    /// </summary>
    [JsonPropertyName("ldapUseStartTls")]
    public bool LdapUseStartTls { get; set; }

    /// <summary>Connect via implicit TLS (LDAPS). Recommended default - works on every OS.</summary>
    [JsonPropertyName("ldapUseLdaps")]
    public bool LdapUseLdaps { get; set; } = true;

    /// <summary>Only for lab/self-signed environments. Never enable against production AD.</summary>
    [JsonPropertyName("ldapSkipCertificateCheck")]
    public bool LdapSkipCertificateCheck { get; set; }

    /// <summary>Blank = auto-discover from the server's RootDSE defaultNamingContext.</summary>
    [JsonPropertyName("ldapBaseDn")]
    public string LdapBaseDn { get; set; } = "";

    [JsonPropertyName("graphTenantId")]
    public string GraphTenantId { get; set; } = "organizations";

    /// <summary>
    /// Defaults to the public "Microsoft Graph Command Line Tools" client ID, the same
    /// pre-consented public client the Microsoft Graph PowerShell SDK uses. Override with
    /// your own app registration's client ID if your tenant's Conditional Access policies
    /// require it.
    /// </summary>
    [JsonPropertyName("graphClientId")]
    public string GraphClientId { get; set; } = "14d82eec-204b-4c2f-b7e8-296a70dab67e";
}

public sealed class ManagementUrls
{
    [JsonPropertyName("entraUser")]
    public string EntraUser { get; set; } =
        "https://entra.microsoft.com/#view/Microsoft_AAD_UsersAndTenants/UserProfileMenuBlade/~/overview/userId/{id}";

    [JsonPropertyName("entraGroup")]
    public string EntraGroup { get; set; } =
        "https://entra.microsoft.com/#view/Microsoft_AAD_IAM/GroupDetailsMenuBlade/~/Overview/groupId/{id}";

    [JsonPropertyName("exoMailbox")]
    public string ExoMailbox { get; set; } =
        "https://admin.exchange.microsoft.com/#/mailboxes/:/MailboxDetails/{id}";

    [JsonPropertyName("exoGroup")]
    public string ExoGroup { get; set; } =
        "https://admin.exchange.microsoft.com/#/groups/:/GroupDetails/{id}/2";

    [JsonPropertyName("onPremEcpMailbox")]
    public string OnPremEcpMailbox { get; set; } =
        "https://{fqdn}/ecp/UsersGroups/EditRemoteMailbox.aspx?pwmcid=5&ReturnObjectType=1&id={id}";

    [JsonPropertyName("onPremEcpGroup")]
    public string OnPremEcpGroup { get; set; } =
        "https://{fqdn}/ecp/UsersGroups/EditSecurityDistributionGroup.aspx?pwmcid=3&ReturnObjectType=1&id={id}";
}
