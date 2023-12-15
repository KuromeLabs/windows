
namespace Application.Devices;

public interface IDeviceRepository
{
    public List<Device> GetSavedDevices();
    public List<Device> GetActiveDevices();
    public void AddActiveDevice(Device device);
    public void RemoveActiveDevice(Device device);
}