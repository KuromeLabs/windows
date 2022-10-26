using System.Collections.Concurrent;
using Application.Interfaces;
using Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Devices;

public class DeviceAccessorFactory : IDeviceAccessorFactory
{
    private readonly IServiceProvider _serviceProvider;
    public DeviceAccessorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IDeviceAccessor Create(ILink link, Device device)
    {
        return ActivatorUtilities.CreateInstance<DeviceAccessor>(_serviceProvider, link, device);;
    }
}