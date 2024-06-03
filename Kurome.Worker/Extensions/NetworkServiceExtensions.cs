using System.Security.Cryptography.X509Certificates;
using Infrastructure.Network;
using Kurome.Core.Interfaces;
using Kurome.Network;
using Microsoft.Extensions.DependencyInjection;

namespace Kurome.Extensions;

public static class NetworkServiceExtensions
{
    public static IServiceCollection AddNetworkServices(this IServiceCollection services)
    {
        services.AddSingleton<DeviceService>();
        services.AddSingleton<ISecurityService<X509Certificate2>, SslService>();
        services.AddSingleton<IpcService>();
        services.AddSingleton<NetworkService>();
        services.AddHostedService<KuromeService>();
        return services;
    }
}