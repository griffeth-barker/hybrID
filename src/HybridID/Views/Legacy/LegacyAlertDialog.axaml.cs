using Avalonia.Controls;

namespace HybridID.Views.Legacy;

public partial class LegacyAlertDialog : Window
{
    public LegacyAlertDialog()
    {
        InitializeComponent();
        BtnOk.Click += (_, _) => Close();
    }

    public LegacyAlertDialog(string title, string message) : this()
    {
        Title = title;
        MessageText.Text = message;
    }
}
