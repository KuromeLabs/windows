using System.Configuration;
using System.Data;
using System.Windows;
using Kurome.Network;
using Kurome.Ui.Pages.Devices;
using Kurome.Ui.Services;
using Kurome.Ui.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Wpf.Ui;

namespace Kurome.Ui;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static readonly IHost _host = Host.CreateDefaultBuilder()
        .ConfigureAppConfiguration(c =>
        {
            c.Sources.Clear();
            c.AddIniFile("appsettings.ini", optional: false, reloadOnChange: true);
        })
        .ConfigureServices(
            (context, services) =>
            {
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<DevicesViewModel>();
                services.AddSingleton<Devices>();
                services.AddSingleton<DeviceDetails>();
                
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<IContentDialogService, ContentDialogService>();
                services.AddHostedService<HostService>();
                services.AddHostedService<PipeService>();
            }
            )
        .UseSerilog((hostingContext, services, loggerConfiguration) => loggerConfiguration
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} <{SourceContext}>{NewLine}{Exception}", theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Literate ))
        .Build();


    private void OnStartup(object s, StartupEventArgs e)
    {
        _host.Start();
    }
    
    private void OnExit(object s, ExitEventArgs e)
    {
        _host.StopAsync().Wait();

        _host.Dispose();
    }
}