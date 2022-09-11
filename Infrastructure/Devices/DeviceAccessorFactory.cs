using System.Collections.Concurrent;
using Application.Interfaces;
using Domain;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Devices;

public class DeviceAccessorFactory : IDeviceAccessorFactory
{
    private readonly ILogger<DeviceAccessorFactory> _logger;
    private readonly IIdentityProvider _identityProvider;
    private readonly ConcurrentDictionary<string, IDeviceAccessor> _monitors = new();

    public DeviceAccessorFactory(ILogger<DeviceAccessorFactory> logger, IIdentityProvider identityProvider)
    {
        _logger = logger;
        _identityProvider = identityProvider;
    }

    public void Register(string id, IDeviceAccessor deviceAccessor)
    {
        _monitors.TryAdd(id, deviceAccessor);
        _logger.LogInformation("Registered device accessor for {Id}", id);
    }

    public void Unregister(string id)
    {
        _monitors.TryRemove(id, out _);
        _logger.LogInformation("Unregistered DeviceAccessor for {Id}", id);
    }

    public IDeviceAccessor? Get(string id)
    {
        _monitors.TryGetValue(id, out var deviceMonitor);
        return deviceMonitor;
    }

    public IDeviceAccessor Create(ILink link, Device device)
    {
        IDeviceAccessor monitor = new DeviceAccessor(link, this, device, _identityProvider);
        Register(device.Id.ToString(), monitor);
        return monitor;
    }
}