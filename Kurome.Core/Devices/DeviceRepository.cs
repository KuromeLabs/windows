

namespace Kurome.Core.Devices;

public class DeviceRepository : IDeviceRepository
{

    private readonly Dictionary<Guid, Device> _activeDevices = new();
    

    public List<Device> GetSavedDevices()
    {
        throw new NotImplementedException();
    }

    public List<Device> GetActiveDevices()
    {
        return _activeDevices.Values.ToList();
    }

    public void AddActiveDevice(Device device)
    {
        _activeDevices.TryAdd(device.Id, device);
        DeviceAdded?.Invoke(this, device);
    }

    public void RemoveActiveDevice(Device device)
    {
        _activeDevices.Remove(device.Id);
        DeviceRemoved?.Invoke(this, device);
    }

    public event EventHandler<Device>? DeviceAdded;
    public event EventHandler<Device>? DeviceRemoved;
}