using System.Collections.Concurrent;
using Application.Interfaces;

namespace Infrastructure.Devices;

public class DeviceAccessorRepository : IDeviceAccessorRepository
{
    
    private readonly ConcurrentDictionary<string, IDeviceAccessor> _accessors = new();
    
    public void Add(string id, IDeviceAccessor deviceAccessor)
    {
        _accessors.TryAdd(id, deviceAccessor);
    }

    public void Remove(string id)
    {
        _accessors.TryRemove(id, out _);
    }

    public IDeviceAccessor? Get(string id)
    {
        _accessors.TryGetValue(id, out var deviceMonitor);
        return deviceMonitor;
    }
}