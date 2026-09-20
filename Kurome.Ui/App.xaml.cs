using System.Windows;
using System.Windows.Threading;
using Kurome.Ui.Pages.Devices;
using Kurome.Ui.Pages.Settings;
using Kurome.Ui.Services;
using Kurome.Ui.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReactiveUI.Builder;
using Serilog;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace Kurome.Ui;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static readonly bool ReactiveUiInitialized = InitializeReactiveUi();

    private static bool InitializeReactiveUi()
    {
        RxAppBuilder.CreateReactiveUIBuilder()
            .WithWpfScheduler()
            .Build();
        return true;
    }

    private static readonly IHost AppHost = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
        .ConfigureAppConfiguration(c =>
        {
            c.Sources.Clear();
            c.AddIniFile("appsettings.ini", optional: true, reloadOnChange: false);
        })
        .ConfigureServices((context, services) =>
        {
            services.AddNavigationViewPageProvider();

            services.AddSingleton<PipeService>();
            services.AddSingleton<KuromeClient>();
            services.AddSingleton<DeviceStore>();

            services.AddSingleton<SettingsService>();
            services.AddSingleton<AppThemeService>();

            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IContentDialogService, ContentDialogService>();
            services.AddSingleton<ISnackbarService, SnackbarService>();

            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<DevicesViewModel>();
            services.AddSingleton<DeviceDetailsViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<DialogViewModel>();

            services.AddSingleton<Devices>();
            services.AddSingleton<DeviceDetails>();
            services.AddSingleton<SettingsPage>();

            services.AddSingleton<MainWindow>();
            services.AddHostedService<HostService>();
        })
        .UseSerilog((hostingContext, services, loggerConfiguration) => loggerConfiguration
            .MinimumLevel.Debug()
            .WriteTo.Console(
                outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} <{SourceContext}>{NewLine}{Exception}",
                theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Literate))
        .Build();

    private void OnStartup(object s, StartupEventArgs e)
    {
        _ = ReactiveUiInitialized;
        AppHost.Start();
    }

    private void OnExit(object s, ExitEventArgs e)
    {
        AppHost.StopAsync().Wait();
        AppHost.Dispose();
        Log.CloseAndFlush();
    }
    
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception on the UI thread");
        e.Handled = true;
    }
}
