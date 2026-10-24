using Avalonia.Controls;

namespace HybridID.Views.Legacy;

public partial class LegacyDeviceCodeDialog : Window
{
    public LegacyDeviceCodeDialog()
    {
        InitializeComponent();
    }

    public LegacyDeviceCodeDialog(string message) : this()
    {
        MessageText.Text = message;
    }
}
