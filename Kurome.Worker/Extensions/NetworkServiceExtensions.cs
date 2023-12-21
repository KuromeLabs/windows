using System.Security.Cryptography.X509Certificates;
using Kurome.Core.Interfaces;
using Infrastructure.Network;
using Kurome.Network;
using Microsoft.Extensions.DependencyInjection;

namespace Kurome.Extensions;

public static class NetworkServiceExtensions
{
    public static IServiceCollection AddNetworkServices(this IServiceCollection services)
    {
        services.AddSingleton<ISecurityService<X509Certificate2>, SslService>();
        services.AddSingleton<IpcService>();
        services.AddSingleton<LinkProvider>();
        services.AddHostedService<KuromeService>();
        return services;
    }
}