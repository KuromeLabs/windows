using DynamicData;

namespace Kurome.Core.Devices;

public class DeviceRepository : IDeviceRepository
{

    
    private readonly SourceCache<Device, Guid> _activeDeviceCache = new (t => t.Id);

    public List<Device> GetSavedDevices()
    {
        throw new NotImplementedException();
    }

    public IObservableCache<Device, Guid> GetActiveDevices()
    {
        return _activeDeviceCache.AsObservableCache();
    }

    public void AddActiveDevice(Device device)
    {
        _activeDeviceCache.AddOrUpdate(device);
    }

    public void RemoveActiveDevice(Device device)
    {
        _activeDeviceCache.Remove(device);
    }
}