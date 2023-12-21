

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
        _activeDevices.Add(device.Id, device);
    }

    public void RemoveActiveDevice(Device device)
    {
        _activeDevices.Remove(device.Id);
    }
}