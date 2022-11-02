using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Application.Devices;
using Application.Interfaces;
using Domain;
using Infrastructure.Devices;
using Infrastructure.Network;
using Kurome.Extensions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Persistence;
using Serilog;

var mutex = new Mutex(false, "Global\\kurome-mutex");
if (!mutex.WaitOne(0, false))
{
    Log.Information("Kurome is already running.\nPress any key to exit...");
    Console.ReadKey();
    Environment.Exit(0);
}

var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "Kurome",
    "devices.zt"
);

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
        services.AddTransient<IDeviceAccessorFactory, DeviceAccessorFactory>();
        services.AddSingleton<IDeviceAccessorRepository, DeviceAccessorRepository>();
        services.AddZoneTree<string, Device>(dbPath, new DeviceSerializer());
    })
    .Build();

Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kurome"));
await host.RunAsync();