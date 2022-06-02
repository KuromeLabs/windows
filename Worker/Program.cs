using System;
using System.Net.Sockets;
using System.Threading;
using Application.Core;
using Application.Devices;
using Application.Interfaces;
using Infrastructure.Devices;
using Infrastructure.Network;
using Kurome;
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
        services.AddHostedService<KuromeWorker>();
        services.AddNetworkServices();
        services.AddDbContext<DataContext>();
        services.AddAutoMapper(typeof(MappingProfile).Assembly);
    })
    .Build();
await host.RunAsync();