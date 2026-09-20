using Kurome.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kurome.Core.Devices;

public class DeviceRepository : IDeviceRepository
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DeviceRepository(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<List<Device>> GetSavedDevices()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        return await context.Devices.AsNoTracking().ToListAsync();
    }

    public async Task<Device?> GetSavedDevice(Guid id)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        return await context.Devices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }

    public int SaveDevice(Device device)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();

        var existing = context.Devices.FirstOrDefault(x => x.Id == device.Id);
        if (existing == null)
        {
            context.Devices.Add(device);
        }
        else
        {
            existing.Name = device.Name;
            existing.Certificate = device.Certificate;
        }

        return context.SaveChanges();
    }

    public async Task<bool> DeleteDevice(Guid id)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();

        var device = await context.Devices.FirstOrDefaultAsync(x => x.Id == id);
        if (device == null) return false;

        context.Devices.Remove(device);
        await context.SaveChangesAsync();
        return true;
    }
}
