using Application.Interfaces;
using Domain;
using Microsoft.Extensions.DependencyInjection;

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
        return ActivatorUtilities.CreateInstance<DeviceAccessor>(_serviceProvider, link, device);
    }
}