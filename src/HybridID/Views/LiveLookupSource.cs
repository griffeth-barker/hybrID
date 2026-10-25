using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Azure.Identity;
using HybridID.Controls;
using HybridID.Models;
using HybridID.Services;
using HybridID.Views.Dialogs;
using Microsoft.Graph;

namespace HybridID.Views;

/// <summary>
/// Real searches for SearchPage (docs/design/ui-spec.md section 5.1.4): signs in to Active
/// Directory when needed (AdCredentialDialog), obtains the Graph client (DeviceCodeDialog on first
/// sign-in), runs <see cref="AppSession.LookupAsync"/>, and reports how complete the result is.
/// Lives in Views, not Services, because it drives dialogs; the decisions stay in the services.
/// </summary>
public sealed class LiveLookupSource : ILookupSource
{
    private readonly IAppShell _shell;

    public LiveLookupSource(IAppShell shell)
    {
        _shell = shell;
    }

    private AppSession Session => _shell.Session;

    public bool IsPreview => false;

    public async Task<LookupOutcome> LookupAsync(string identity, Action<string> reportPhase)
    {
        var (adCoverage, adError) = await EnsureAdAsync(reportPhase);

        reportPhase(adCoverage == AdCoverage.Searched
            ? "Searching Active Directory and Microsoft Graph…"
            : "Searching Microsoft Graph…");

        DeviceCodeDialog? codeDialog = null;
        GraphServiceClient client;
        try
        {
            client = Session.EnsureGraphClient((info, ct) =>
            {
                // Device-code callback runs off the UI thread (spec 12.2 #13). The search awaits
                // the lookup, not this dialog; "Hide" lets sign-in continue in the background.
                Dispatcher.UIThread.Post(() =>
                {
                    reportPhase("Waiting for Microsoft sign-in…");
                    codeDialog = new DeviceCodeDialog(info);
                    codeDialog.CodeCopied += OnCodeCopied;
                    _ = _shell.ShowDialogAsync(codeDialog);
                });
                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            throw new LookupInitException($"{ex.Message} Check the tenant and client ID in Settings.", ex);
        }

        IdentityLookupResult result;
        try
        {
            result = await Session.LookupAsync(client, identity);
        }
        finally
        {
            codeDialog?.Hide(OverlayDialogResult.None);
        }

        // Graph either answered (so any device-code sign-in finished) or reported why it didn't.
        if (result.GraphError is { } graphError)
        {
            Session.MarkGraphFailed(graphError);
        }
        else
        {
            Session.MarkGraphSignedIn();
        }

        // An already-open LDAP connection that fails mid-search (DC restarted, idle timeout) is
        // dropped so the next search signs in again, and this one says AD wasn't searched.
        if (adCoverage == AdCoverage.Searched && result.AdError is { } searchError)
        {
            Session.DisconnectLdap();
            adCoverage = AdCoverage.Unreachable;
            adError = searchError;
        }

        return new LookupOutcome { Result = result, AdCoverage = adCoverage, AdError = adError };
    }

    public async Task<bool> PrepareAdRetryAsync()
    {
        if (!Session.IsAdConfigured)
        {
            _shell.Navigate(AppPage.Settings);
            return false;
        }

        await PromptAdSignInAsync();
        return Session.AdState == AdConnectionState.Connected;
    }

    /// <summary>Signs in to AD when it's configured and not connected; never prompts once skipped.</summary>
    private async Task<(AdCoverage Coverage, string? Error)> EnsureAdAsync(Action<string> reportPhase)
    {
        if (!Session.IsAdConfigured)
        {
            return (AdCoverage.NotConfigured, null);
        }

        string? connectError = null;
        if (Session.AdState is AdConnectionState.NotConnected or AdConnectionState.Failed)
        {
            reportPhase("Connecting to Active Directory…");
            connectError = await PromptAdSignInAsync();
        }

        return Session.AdState switch
        {
            AdConnectionState.Connected => (AdCoverage.Searched, null),
            // Tried this search and gave up after an error: AD-UNREACHABLE, not just "skipped".
            _ when connectError is not null => (AdCoverage.Unreachable, connectError),
            AdConnectionState.Connecting => (AdCoverage.Unreachable, "A sign-in is still in progress."),
            _ => (AdCoverage.Skipped, null),
        };
    }

    /// <summary>Shows AdCredentialDialog; on Skip/Cancel the session skips AD. Returns the last connect error.</summary>
    private async Task<string?> PromptAdSignInAsync()
    {
        string? lastError = null;
        var dialog = new AdCredentialDialog(Session.AdServerLabel, async (user, password) =>
        {
            lastError = await Session.ConnectLdapAsync(user, password);
            return lastError;
        }, AdCredentialPurpose.Search);

        await _shell.ShowDialogAsync(dialog);
        if (Session.AdState != AdConnectionState.Connected)
        {
            Session.SkipLdapForSession();
            return lastError;
        }

        return null;
    }

    private void OnCodeCopied(object? sender, bool copied) =>
        _shell.ShowToast(copied ? "Code copied" : "Couldn't access the clipboard.",
            copied ? ToastKind.Success : ToastKind.Error);
}
