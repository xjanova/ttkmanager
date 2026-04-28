using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using TTKManager.App.Services;
using TTKManager.App.ViewModels;
using TTKManager.App.Views;

namespace TTKManager.App;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        Services = Bootstrapper.Build();

        var scheduler = Services.GetRequiredService<SchedulerService>();
        await scheduler.StartAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
            desktop.ShutdownRequested += async (_, _) =>
            {
                await scheduler.DisposeAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
