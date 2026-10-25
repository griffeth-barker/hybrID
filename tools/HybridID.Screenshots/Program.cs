using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HybridID;
using HybridID.Services;
using HybridID.Controls;
using HybridID.Views;
using HybridID.Views.Dialogs;
using HybridID.Views.Pages;

// Renders MainWindow headlessly in preview mode (sample data) and saves one PNG per
// scenario and theme, so a design can be reviewed without a DC, a tenant, or a display.
//
//   dotnet run --project tools/HybridID.Screenshots -- [outDir] [identity ...]
//
// No identities = the default set. Use "idle" for the empty state, "w=640" to change width,
// "settings" / "about" for those pages, a "+" suffix ("settings+") to expand every expander,
// "settings-dirty" for staged (unsaved, never written) edits that show the SaveBar, and
// "dlg-ad", "dlg-ad-busy", "dlg-ad-error", "dlg-code", "dlg-code-expired", "dlg-message",
// "dlg-confirm" for the overlay dialogs (fake callbacks; nothing connects anywhere).
Environment.SetEnvironmentVariable("HYBRID_PREVIEW", "1");

var outDir = args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "screenshots");
var scenarios = args.Skip(1).ToList();
if (scenarios.Count == 0)
{
    scenarios = new() { "idle", "jsmith", "bwayne", "acloud@contoso.com", "finance-team", "smith", "nobody", "adfail", "grapherror" };
}

Directory.CreateDirectory(outDir);

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .WithInterFont()
    .SetupWithoutStarting();

foreach (var theme in new[] { "Light", "Dark" })
{
    double width = 1120;
    foreach (var scenario in scenarios)
    {
        if (scenario.StartsWith("w=", StringComparison.Ordinal))
        {
            width = double.Parse(scenario[2..]);
            continue;
        }

        var isDialog = scenario.StartsWith("dlg-", StringComparison.Ordinal);
        var window = new MainWindow { Width = width, Height = isDialog ? 820 : 2000 };
        // After construction: MainWindow applies the user's saved theme.
        ThemeService.Apply(theme);
        window.Show();
        Pump(TimeSpan.FromMilliseconds(300));

        var pageName = scenario.TrimEnd('+');
        if (isDialog)
        {
            ShowDialog(window, scenario);
            Pump(TimeSpan.FromMilliseconds(900));
        }
        else if (pageName is "settings" or "about" or "settings-dirty")
        {
            window.Navigate(pageName == "about" ? AppPage.About : AppPage.Settings);
            Pump(TimeSpan.FromMilliseconds(600));
            if (pageName == "settings-dirty")
            {
                var settings = window.GetVisualDescendants().OfType<SettingsPage>().First();
                settings.FindControl<TextBox>("CfgLdapServer")!.Text = "dc01.contoso.com";
                settings.FindControl<ComboBox>("CmbLdapEncryption")!.SelectedIndex = 2;
                Pump(TimeSpan.FromMilliseconds(600));
            }

            if (scenario.EndsWith('+'))
            {
                foreach (var expander in window.GetVisualDescendants().OfType<Expander>())
                {
                    expander.IsExpanded = true;
                }

                Pump(TimeSpan.FromMilliseconds(600));
            }
        }
        else if (scenario != "idle")
        {
            var page = window.GetVisualDescendants().OfType<SearchPage>().First();
            page.FindControl<TextBox>("TxtSearch")!.Text = scenario;
            page.FindControl<Button>("BtnLocate")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump(TimeSpan.FromMilliseconds(2600));
        }

        var name = $"{Sanitize(scenario)}-{(int)width}-{theme.ToLowerInvariant()}.png";
#pragma warning disable CS0618 // The options overload adds nothing for a plain PNG.
        window.CaptureRenderedFrame()?.Save(Path.Combine(outDir, name));
#pragma warning restore CS0618
        Console.WriteLine(name);
        window.Close();
    }
}

// Opens one dialog with fake callbacks: nothing here signs in or connects to anything.
static void ShowDialog(MainWindow window, string scenario)
{
    OverlayDialog dialog = scenario switch
    {
        "dlg-ad" or "dlg-ad-busy" or "dlg-ad-error" => new AdCredentialDialog(
            "dc01.contoso.com · LDAPS · port 636",
            scenario == "dlg-ad-busy"
                ? (_, _) => new TaskCompletionSource<string?>().Task
                : (_, _) => Task.FromResult<string?>("The supplied credential is invalid."),
            AdCredentialPurpose.Search),
        "dlg-code" or "dlg-code-expired" => new DeviceCodeDialog(Azure.Identity.IdentityModelFactory.DeviceCodeInfo(
            "ABCD-EFGH", "device-code", new Uri("https://microsoft.com/devicelogin"),
            scenario == "dlg-code" ? DateTimeOffset.Now.AddMinutes(14) : DateTimeOffset.Now.AddMinutes(-1),
            "To sign in, use a web browser to open the page https://microsoft.com/devicelogin and enter the code ABCD-EFGH to authenticate.",
            "client-id", new[] { "User.Read.All" })),
        "dlg-confirm" => new MessageDialog("Save changes?", "You have unsaved settings.", "Cancel", "Save", "Don't save"),
        _ => new MessageDialog("Couldn't save settings", "Access to the path '/Users/me/Library/Application Support/hybrID/config.json' is denied.",
            severity: InfoBarSeverity.Error),
    };

    _ = window.ShowDialogAsync(dialog);
    Pump(TimeSpan.FromMilliseconds(500));

    if (dialog is AdCredentialDialog && scenario != "dlg-ad")
    {
        dialog.FindControl<TextBox>("UsernameBox")!.Text = "CONTOSO\\jsmith";
        dialog.FindControl<TextBox>("PasswordBox")!.Text = "not-a-real-password";
        dialog.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_PrimaryButton")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
}

// Runs the dispatcher and render timer for real time so Task.Delay-based sample lookups,
// transitions and animations complete before capture.
static void Pump(TimeSpan duration)
{
    var until = DateTime.UtcNow + duration;
    while (DateTime.UtcNow < until)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Thread.Sleep(16);
    }
}

static string Sanitize(string s) => string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));
