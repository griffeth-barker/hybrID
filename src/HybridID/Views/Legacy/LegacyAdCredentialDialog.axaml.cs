using Avalonia.Controls;

namespace HybridID.Views.Legacy;

public partial class LegacyAdCredentialDialog : Window
{
    public (string Username, string Password)? Result { get; private set; }

    public LegacyAdCredentialDialog()
    {
        InitializeComponent();

        BtnCancel.Click += (_, _) =>
        {
            Result = null;
            Close();
        };

        BtnOk.Click += (_, _) =>
        {
            Result = (TxtUsername.Text ?? "", TxtPassword.Text ?? "");
            Close();
        };

        Opened += (_, _) => TxtUsername.Focus();
    }
}
