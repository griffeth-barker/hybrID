using Avalonia.Controls;
using HybridID.Controls;

namespace HybridID.Views.Dialogs;

/// <summary>
/// Generic message / confirm dialog; replaces the legacy AlertDialog (spec section 5.4.3).
/// Result: Primary / Secondary / None (Close button or Esc).
/// </summary>
public partial class MessageDialog : OverlayDialog
{
    /// <summary>For the XAML previewer only.</summary>
    public MessageDialog()
    {
        InitializeComponent();
    }

    public MessageDialog(string title, string message, string closeText = "OK", string? primaryText = null,
        string? secondaryText = null, InfoBarSeverity? severity = null) : this()
    {
        Title = title;
        MessageText.Text = message;
        CloseButtonText = closeText;
        PrimaryButtonText = primaryText;
        SecondaryButtonText = secondaryText;
        // Without a primary button, the close button is the default (Enter) button.
        DefaultButton = string.IsNullOrEmpty(primaryText) ? OverlayDialogButton.Close : OverlayDialogButton.Primary;

        if (severity is { } s)
        {
            TitleIcon = new PathIcon
            {
                Width = 20,
                Height = 20,
                Classes =
                {
                    "severity",
                    s switch
                    {
                        InfoBarSeverity.Success => "success",
                        InfoBarSeverity.Warning => "warning",
                        InfoBarSeverity.Error => "error",
                        _ => "informational",
                    },
                },
            };
        }
    }
}
