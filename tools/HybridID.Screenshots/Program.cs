using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HybridID;
using HybridID.Services;
using HybridID.Views;
using HybridID.Views.Pages;

// Renders MainWindow headlessly in preview mode (sample data) and saves one PNG per
// scenario and theme, so a design can be reviewed without a DC, a tenant, or a display.
//
//   dotnet run --project tools/HybridID.Screenshots -- [outDir] [identity ...]
//
// No identities = the default set. Use "idle" for the empty state, "w=640" to change width,
// "settings" / "about" for those pages, a "+" suffix ("settings+") to expand every expander,
// and "settings-dirty" for staged (unsaved, never written) edits that show the SaveBar.
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

        var window = new MainWindow { Width = width, Height = 2000 };
        // After construction: MainWindow applies the user's saved theme.
        ThemeService.Apply(theme);
        window.Show();
        Pump(TimeSpan.FromMilliseconds(300));

        var pageName = scenario.TrimEnd('+');
        if (pageName is "settings" or "about" or "settings-dirty")
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
