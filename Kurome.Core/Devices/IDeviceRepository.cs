using DynamicData;

namespace Kurome.Core.Devices;

public interface IDeviceRepository
{
    public List<Device> GetSavedDevices();
    public IObservableCache<Device, Guid> GetActiveDevices();
    public void AddActiveDevice(Device device);
    public void RemoveActiveDevice(Device device);
}