using Application.Interfaces;
using Infrastructure.Devices;
using Kurome.Network;
using Microsoft.Extensions.DependencyInjection;

namespace Kurome.Extensions;

public static class NetworkServiceExtensions
{
    public static IServiceCollection AddNetworkServices(this IServiceCollection services)
    {
        services.AddHostedService<UdpListenerWorker>();
        services.AddSingleton<IDeviceAccessorFactory, DeviceAccessorFactory>();
        return services;
    }
}