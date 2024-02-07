using DynamicData;
using Kurome.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kurome.Core.Devices;

public class DeviceRepository : IDeviceRepository
{
    private readonly DataContext _context;

    public DeviceRepository(DataContext context)
    {
        _context = context;
    }

    public async Task<List<Device>> GetSavedDevices()
    {
        return await _context.Devices
            .ToListAsync();
    }

    public async Task<Device?> GetSavedDevice(Guid id)
    {
        return await _context.Devices
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public int SaveDevice(Device device)
    {
        _context.Devices.Add(device);
        return _context.SaveChanges();
    }
}