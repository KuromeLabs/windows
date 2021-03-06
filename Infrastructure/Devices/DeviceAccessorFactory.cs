using System.Collections.Concurrent;
using Application.Interfaces;
using AutoMapper;
using Domain;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Devices;

public class DeviceAccessorFactory : IDeviceAccessorFactory
{
    private readonly ILogger<DeviceAccessorFactory> _logger;
    private readonly IMapper _mapper;
    private readonly IIdentityProvider _identityProvider;
    private readonly ConcurrentDictionary<string, IDeviceAccessor> _monitors = new();

    public DeviceAccessorFactory(ILogger<DeviceAccessorFactory> logger, IMapper mapper,
        IIdentityProvider identityProvider)
    {
        _logger = logger;
        _mapper = mapper;
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
        IDeviceAccessor monitor = new DeviceAccessor(link, this, device, _identityProvider, _mapper);
        Register(device.Id.ToString(), monitor);
        return monitor;
    }
}