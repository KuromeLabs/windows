namespace Kurome.Core.Devices;

public interface IDeviceRepository
{
    public Task<List<Device>> GetSavedDevices();
    public Task<Device?> GetSavedDevice(Guid id);
    
    public int SaveDevice(Device device);
}