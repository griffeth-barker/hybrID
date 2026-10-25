using System;
using Avalonia.Controls;
using Avalonia.Threading;
using Azure.Identity;
using HybridID.Controls;
using HybridID.Services;

namespace HybridID.Views.Dialogs;

/// <summary>
/// Device-code sign-in prompt (spec section 5.4.2). Non-blocking: the search awaits the lookup,
/// not this dialog. "Hide" (<see cref="OverlayDialogResult.None"/>) only closes the overlay; sign-in
/// continues and the Entra chip can reopen a new instance from <c>AppSession.PendingDeviceCode</c>.
/// The owner closes it with <c>Hide(OverlayDialogResult.None)</c> when its search/test completes.
/// </summary>
public partial class DeviceCodeDialog : OverlayDialog
{
    private readonly DispatcherTimer _expiryTimer = new() { Interval = TimeSpan.FromSeconds(30) };

    /// <summary>For the XAML previewer only.</summary>
    public DeviceCodeDialog()
    {
        InitializeComponent();
        CacheText.Text = $"Your sign-in is cached in {TokenStoreName}.";
        _expiryTimer.Tick += (_, _) => UpdateExpiry();
        UriLink.Click += (_, _) => OpenVerificationUri();
        CopyCode.AddHandler(CopyButton.CopiedEvent, OnCodeCopiedByButton);
    }

    public DeviceCodeDialog(DeviceCodeInfo info) : this()
    {
        Info = info;
        UriText.Text = $"{info.VerificationUri.Host}{info.VerificationUri.AbsolutePath}".TrimEnd('/');
        ToolTip.SetTip(UriLink, info.VerificationUri.ToString());

        var hasCode = !string.IsNullOrEmpty(info.UserCode);
        CodeText.Text = info.UserCode;
        CodeText.IsVisible = hasCode;
        CopyCode.IsVisible = hasCode;
        CopyCode.CopyText = info.UserCode;
        MessageText.Text = info.Message;
        MessageText.IsVisible = !hasCode;
        UpdateExpiry();
    }

    public DeviceCodeInfo? Info { get; }

    /// <summary>
    /// Raised after the primary button copied the code (arg = true) or failed to (false). The
    /// owner shows the toast ("Code copied" / "Couldn't access the clipboard.").
    /// </summary>
    public event EventHandler<bool>? CodeCopied;

    /// <summary>Where Azure.Identity's persistent token cache lives on this OS.</summary>
    private static string TokenStoreName =>
        OperatingSystem.IsWindows() ? "Windows Credential Manager"
        : OperatingSystem.IsMacOS() ? "your macOS Keychain"
        : "the Secret Service keyring";

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
        OpenVerificationUri();
    }

    protected override void OnOpened()
    {
        WaitingRing.IsActive = true;
        UpdateExpiry();
        _expiryTimer.Start();
    }

    protected override void OnClosed(OverlayDialogResult result)
    {
        WaitingRing.IsActive = false;
        _expiryTimer.Stop();
    }

    /// <summary>"Code expires at 2:17 PM (in 14 min)"; once past, the caution line and no primary action.</summary>
    private void UpdateExpiry()
    {
        if (Info is not { } info)
        {
            return;
        }

        var remaining = info.ExpiresOn - DateTimeOffset.Now;
        var expired = remaining <= TimeSpan.Zero;
        IsPrimaryButtonEnabled = !expired;
        WaitingLine.IsVisible = !expired;
        CopyCode.IsVisible = !expired && !string.IsNullOrEmpty(info.UserCode);
        ExpiryIcon.Data = Avalonia.Application.Current?.FindResource(expired ? "Icon.Warning" : "Icon.Clock") as Avalonia.Media.Geometry;
        ExpiryIcon.Classes.Set("caution", expired);
        ExpiryIcon.Classes.Set("secondary", !expired);

        if (expired)
        {
            ExpiryText.Text = "This code expired. Search again to get a new one.";
            _expiryTimer.Stop();
            return;
        }

        var minutes = (int)Math.Ceiling(remaining.TotalMinutes);
        ExpiryText.Text = $"Code expires at {info.ExpiresOn.ToLocalTime():t} (in {minutes} min)";
    }

    private void OpenVerificationUri()
    {
        if (Info is not { } info)
        {
            return;
        }

        try
        {
            UrlLauncher.Open(info.VerificationUri.ToString());
        }
        catch (Exception)
        {
            // No default browser: the URL is shown in the dialog.
        }
    }

    private void OnCodeCopiedByButton(object? sender, CopiedEventArgs e)
    {
        e.Handled = true;
        CodeCopied?.Invoke(this, e.Succeeded);
    }
}
