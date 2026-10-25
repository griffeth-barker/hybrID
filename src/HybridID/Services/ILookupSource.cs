using System;
using System.Threading.Tasks;
using HybridID.Models;

namespace HybridID.Services;

/// <summary>How Active Directory took part in one search (spec section 5.1.7 coverage line).</summary>
public enum AdCoverage
{
    /// <summary>AD was queried; match / no match is read from the result's SyncState.</summary>
    Searched,
    NotConfigured,
    /// <summary>The technician chose "Skip" (this search or earlier in the session).</summary>
    Skipped,
    /// <summary>Connecting failed during this search.</summary>
    Unreachable,
}

/// <summary>A lookup result plus how complete it is, which the result alone can't say.</summary>
public sealed class LookupOutcome
{
    public required IdentityLookupResult Result { get; init; }
    public AdCoverage AdCoverage { get; init; } = AdCoverage.Searched;

    /// <summary>Connection error when <see cref="AdCoverage"/> is Unreachable.</summary>
    public string? AdError { get; init; }

    /// <summary>A Microsoft sign-in code was shown during this search (drives GRAPH-SUSPECT).</summary>
    public bool DeviceCodeShown { get; init; }
}

/// <summary>Thrown when the lookup can't start at all (e.g. Microsoft Graph client creation failed).</summary>
public sealed class LookupInitException : Exception
{
    public LookupInitException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>
/// What SearchPage searches through. Keeps the page independent of where results come from:
/// <see cref="PreviewLookupSource"/> serves sample data for design review (--preview);
/// <c>Views.LiveLookupSource</c> does real searches (AD sign-in prompt, device-code sign-in,
/// AppSession lookup).
/// </summary>
public interface ILookupSource
{
    /// <summary>True for sample data; the page then shows a preview banner.</summary>
    bool IsPreview { get; }

    /// <summary>Runs one search. <paramref name="reportPhase"/> updates the "Searching…" phase text.</summary>
    Task<LookupOutcome> LookupAsync(string identity, Action<string> reportPhase);

    /// <summary>
    /// "Retry" / "Sign in" on the AD InfoBars: clears a skip or failed state (prompting for
    /// credentials if needed). Returns true when the page should re-run the search.
    /// </summary>
    Task<bool> PrepareAdRetryAsync();
}
