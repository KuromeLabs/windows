﻿using System;
using System.IO;
using System.Threading;
using Application.Devices;
using Application.flatbuffers;
using Application.Interfaces;
using Application.Persistence;
using Infrastructure.Devices;
using Kurome.Extensions;
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
        services.AddSingleton<IDeviceRepository, DeviceRepository>();
        services.AddNetworkServices();
        
    });

var host = builder.Build();


//create db if it doesnt exist
using (var scope = host.Services.CreateScope())
{
    var configuration = host.Services.GetRequiredService<IConfiguration>();
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation($"SQLite database location: {configuration["Database:Location"]}");
    var context = scope.ServiceProvider.GetRequiredService<DataContext>();
    if (!context.Database.CanConnect()) logger.LogInformation("Database not found. Will be created.");
    
    await context.Database.MigrateAsync();
}


Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kurome"));
await host.RunAsync();