using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using HybridID.Models;
using Microsoft.Graph;

namespace HybridID.Services;

/// <summary>
/// Cross-platform replacement for Assert-GraphConnection.ps1 / Connect-MgGraph. Uses the same
/// public, pre-consented "Microsoft Graph Command Line Tools" client ID the Graph PowerShell
/// SDK uses by default, authenticating via device code so no redirect URI registration is
/// required. Azure.Identity's persistent token cache gives the same "frictionless, cached
/// sign-in" behavior the original app advertised, using each OS's native secure storage.
/// </summary>
public sealed class GraphAuthService
{
    private static readonly string[] Scopes = { "User.Read.All", "Group.Read.All" };

    private readonly AppConfig _config;
    private GraphServiceClient? _client;

    public GraphAuthService(AppConfig config)
    {
        _config = config;
    }

    public GraphServiceClient GetClient(Func<DeviceCodeInfo, CancellationToken, Task> deviceCodeCallback)
    {
        if (_client is not null)
        {
            return _client;
        }

        var credential = new DeviceCodeCredential(new DeviceCodeCredentialOptions
        {
            ClientId = _config.Settings.GraphClientId,
            TenantId = _config.Settings.GraphTenantId,
            DeviceCodeCallback = deviceCodeCallback,
            TokenCachePersistenceOptions = new TokenCachePersistenceOptions { Name = "hybrID" },
        });

        _client = new GraphServiceClient(credential, Scopes);
        return _client;
    }
}
