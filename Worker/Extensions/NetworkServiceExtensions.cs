using System.Security.Cryptography.X509Certificates;
using Application.Interfaces;
using Infrastructure.Devices;
using Infrastructure.Network;
using Kurome.Network;
using Microsoft.Extensions.DependencyInjection;

namespace Kurome.Extensions;

public static class NetworkServiceExtensions
{
    public static IServiceCollection AddNetworkServices(this IServiceCollection services)
    {
        services.AddSingleton<ISecurityService<X509Certificate2>, SslService>();
        services.AddScoped<DeviceConnectionHandler>();
        services.AddHostedService<TcpListenerService>();
        services.AddHostedService<UdpCastService>();
        services.AddHostedService<UdpListenerService>();
        return services;
    }
}