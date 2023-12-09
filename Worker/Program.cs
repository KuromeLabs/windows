using System;
using System.Collections.Immutable;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Application.Dokany;
using Application.flatbuffers;
using Application.Interfaces;
using Application.Persistence;
using Infrastructure.Devices;
using Infrastructure.Dokany;
using Infrastructure.Network;
using Kurome.Extensions;
using MessagePipe;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

var mutex = new Mutex(false, "Global\\kurome-mutex");
if (!mutex.WaitOne(0, false))
{
    Log.Information("Kurome is already running.\nPress any key to exit...");
    Console.ReadKey();
    Environment.Exit(0);
}

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureAppConfiguration(c =>
{
    c.Sources.Clear();
    c.AddIniFile("appsettings.ini", optional: false, reloadOnChange: true);
});


builder.UseSerilog((hostingContext, services, loggerConfiguration) => loggerConfiguration
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} <{SourceContext}>{NewLine}{Exception}", theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Literate )
        .Enrich.FromLogContext()
        .ReadFrom.Configuration(hostingContext.Configuration));
    
builder.ConfigureServices(services =>
    {
        services.AddDbContext<DataContext>();
        services.AddSingleton<IIdentityProvider, IdentityProvider>();
        services.AddMessagePipe();
        services.AddSingleton<ILinkProvider<TcpClient>, LinkProvider>();
        services.AddNetworkServices();
        services.AddTransient<IDeviceAccessorFactory, DeviceAccessorFactory>();
        services.AddSingleton<IDeviceAccessorHolder, DeviceAccessorHolder>();
        services.AddSingleton<IKuromeOperationsHolder, KuromeOperationsHolder>();
        services.AddSingleton<IKuromeOperationsFactory, KuromeOperatitonsFactory>();
        services.AddSingleton<FlatBufferHelper>();
        services.AddMapster();
        
    });

var host = builder.Build();


//create db if it doesnt exist
using (var scope = host.Services.CreateScope())
{
    var configuration = host.Services.GetRequiredService<IConfiguration>();
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation($"SQLite DB location: {configuration["Database:Location"]}");
    logger.LogInformation("Database not found. Creating...");
    var context = scope.ServiceProvider.GetRequiredService<DataContext>();
    await context.Database.MigrateAsync();
}


Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kurome"));
await host.RunAsync();