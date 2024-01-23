namespace Kurome.Core.Devices;

public interface IDeviceRepository
{
    public List<Device> GetSavedDevices();
}