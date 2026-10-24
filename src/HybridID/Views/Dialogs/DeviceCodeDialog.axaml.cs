using System;
using Azure.Identity;
using HybridID.Controls;
using HybridID.Services;

namespace HybridID.Views.Dialogs;

/// <summary>
/// Device-code sign-in prompt (spec section 5.4.2). Non-blocking: the search awaits the lookup,
/// not this dialog. "Hide" (<see cref="OverlayDialogResult.None"/>) only closes the overlay; sign-in
/// continues and the Entra chip can reopen a new instance from <c>AppSession.PendingDeviceCode</c>.
/// The owner closes it with <c>Hide(OverlayDialogResult.None)</c> when its search/test completes.
/// PLACEHOLDER implementation; the constructor and events are final.
/// </summary>
public partial class DeviceCodeDialog : OverlayDialog
{
    /// <summary>For the XAML previewer only.</summary>
    public DeviceCodeDialog()
    {
        InitializeComponent();
    }

    public DeviceCodeDialog(DeviceCodeInfo info) : this()
    {
        Info = info;
        UriText.Text = $"Open {info.VerificationUri} and enter this code:";
        CodeText.Text = string.IsNullOrEmpty(info.UserCode) ? info.Message : info.UserCode;
    }

    public DeviceCodeInfo? Info { get; }

    /// <summary>
    /// Raised after the primary button copied the code (arg = true) or failed to (false). The
    /// owner shows the toast ("Code copied" / "Couldn't access the clipboard.").
    /// </summary>
    public event EventHandler<bool>? CodeCopied;

    protected override async void OnPrimaryButtonClick(OverlayDialogButtonClickEventArgs e)
    {
        // The dialog stays open: sign-in happens in the browser.
        e.Cancel = true;
        if (Info is not { } info)
        {
            return;
        }

        var ok = await ClipboardHelper.TrySetTextAsync(this, info.UserCode);
        CodeCopied?.Invoke(this, ok);
        try
        {
            UrlLauncher.Open(info.VerificationUri.ToString());
        }
        catch (Exception)
        {
            // No default browser: the URL is shown in the dialog.
        }
    }

    protected override void OnClosed(OverlayDialogResult result) => WaitingRing.IsActive = false;

    protected override void OnOpened() => WaitingRing.IsActive = true;
}
