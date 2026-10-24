using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HybridID.Views;

namespace HybridID;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnAboutMenuClick(object? sender, EventArgs e) => Shell?.Navigate(AppPage.About);

    private void OnSettingsMenuClick(object? sender, EventArgs e) => Shell?.Navigate(AppPage.Settings);

    private IAppShell? Shell =>
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as IAppShell;
}
