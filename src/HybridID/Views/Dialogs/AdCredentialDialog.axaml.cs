using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using HybridID.Controls;

namespace HybridID.Views.Dialogs;

public enum AdCredentialPurpose
{
    /// <summary>Before/while searching: the Close button reads "Skip".</summary>
    Search,
    /// <summary>Settings "Test connection": the Close button reads "Cancel".</summary>
    Test,
}

/// <summary>
/// LDAP credential prompt with inline retry (spec section 5.4.1). Result: <see cref="OverlayDialogResult.Primary"/>
/// when <c>tryConnect</c> succeeded, <see cref="OverlayDialogResult.None"/> for Skip/Cancel/Esc.
/// </summary>
public partial class AdCredentialDialog : OverlayDialog
{
    private readonly Func<string, string, Task<string?>>? _tryConnect;
    private bool _busy;

    /// <summary>For the XAML previewer only.</summary>
    public AdCredentialDialog()
    {
        InitializeComponent();
        UsernameBox.TextChanged += (_, _) => UpdatePrimaryEnabled();
        BtnReveal.Click += (_, _) => SetRevealed(!PasswordBox.RevealPassword);
        UpdatePrimaryEnabled();
    }

    /// <param name="serverLabel">e.g. <c>AppSession.AdServerLabel</c> ("dc01.contoso.com · LDAPS · port 636").</param>
    /// <param name="tryConnect">(username, password) → null on success, else the error message
    /// (e.g. <c>AppSession.ConnectLdapAsync</c>, or a wrapper around <c>AppSession.TestLdapAsync</c>).</param>
    /// <param name="purpose">Search → "Skip"; Test → "Cancel".</param>
    public AdCredentialDialog(string serverLabel, Func<string, string, Task<string?>> tryConnect, AdCredentialPurpose purpose)
        : this()
    {
        _tryConnect = tryConnect;
        Purpose = purpose;
        ServerText.Text = serverLabel;
        CloseButtonText = purpose == AdCredentialPurpose.Test ? "Cancel" : "Skip";
    }

    public AdCredentialPurpose Purpose { get; }

    /// <summary>Username that connected successfully (null until then).</summary>
    public string? ConnectedUsername { get; private set; }

    protected override async void OnPrimaryButtonClick(OverlayDialogButtonClickEventArgs e)
    {
        e.Cancel = true;
        if (_busy || _tryConnect is null)
        {
            return;
        }

        var username = UsernameBox.Text?.Trim() ?? "";
        if (username.Length == 0)
        {
            return;
        }

        _busy = true;
        UsernameBox.IsEnabled = false;
        PasswordBox.IsEnabled = false;
        IsPrimaryButtonEnabled = false;
        PrimaryButtonContent = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new ProgressRing { Width = 16, Height = 16 },
                new TextBlock { Text = "Signing in…", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
            },
        };
        ErrorBar.IsOpen = false;

        string? error;
        try
        {
            error = await _tryConnect(username, PasswordBox.Text ?? "");
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        _busy = false;
        UsernameBox.IsEnabled = true;
        PasswordBox.IsEnabled = true;
        PrimaryButtonContent = null;
        UpdatePrimaryEnabled();

        if (error is null)
        {
            ConnectedUsername = username;
            Hide(OverlayDialogResult.Primary);
            return;
        }

        ErrorBar.Message = error;
        ErrorBar.IsOpen = true;
        PasswordBox.Focus();
        PasswordBox.SelectAll();
    }

    protected override void OnCloseButtonClick(OverlayDialogButtonClickEventArgs e)
    {
        // A bind in progress can't be cancelled (no async LDAP API); keep the dialog until it returns.
        e.Cancel = _busy;
    }

    protected override void OnClosed(OverlayDialogResult result)
    {
        // Never keep the password around after the dialog closes.
        PasswordBox.Text = "";
        SetRevealed(false);
    }

    private void SetRevealed(bool revealed)
    {
        PasswordBox.RevealPassword = revealed;
        RevealIcon.Data = Application.Current?.FindResource(revealed ? "Icon.EyeOff" : "Icon.Eye") as Geometry;
        var label = revealed ? "Hide password" : "Show password";
        ToolTip.SetTip(BtnReveal, label);
        AutomationProperties.SetName(BtnReveal, label);
    }

    private void UpdatePrimaryEnabled() =>
        IsPrimaryButtonEnabled = !_busy && !string.IsNullOrWhiteSpace(UsernameBox.Text);
}
