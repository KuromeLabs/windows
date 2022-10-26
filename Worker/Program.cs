﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Application.Devices;
using Application.Interfaces;
using Infrastructure.Devices;
using Infrastructure.Network;
using Kurome.Extensions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Persistence;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

var mutex = new Mutex(false, "Global\\kurome-mutex");
if (!mutex.WaitOne(0, false))
{
    Log.Information("Kurome is already running.\nPress any key to exit...");
    Console.ReadKey();
    Environment.Exit(0);
}

var host = Host.CreateDefaultBuilder(args)
    .UseSerilog((hostingContext, services, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(hostingContext.Configuration)
        .Enrich.FromLogContext())
    .ConfigureServices(services =>
    {
        services.AddSingleton<IIdentityProvider, IdentityProvider>();
        services.AddMediatR(typeof(Connect.Handler).Assembly);
        services.AddSingleton<ILinkProvider<TcpClient>, LinkProvider>();
        services.AddNetworkServices();
        services.AddScoped<IDeviceAccessorFactory, DeviceAccessorFactory>();
        services.AddSingleton<IDeviceAccessorRepository, DeviceAccessorRepository>();
        services.AddDbContext<DataContext>();
    })
    .Build();

Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kurome"));
await host.Services.GetRequiredService<DataContext>().Database.EnsureCreatedAsync();
await host.RunAsync();