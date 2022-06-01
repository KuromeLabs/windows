using System.Collections.Concurrent;
using Application.Interfaces;
using AutoMapper;
using DokanNet;
using DokanNet.Logging;
using Domain;
using Infrastructure.Dokany;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Network;

public class DeviceAccessorFactory : IDeviceAccessorFactory
{
    private readonly ILogger<DeviceAccessorFactory> _logger;
    private readonly IMapper _mapper;
    private readonly IIdentityProvider _identityProvider;
    private readonly ConcurrentDictionary<string, IDeviceAccessor> _monitors = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _mountSemaphores = new();

    public DeviceAccessorFactory(ILogger<DeviceAccessorFactory> logger, IMapper mapper, IIdentityProvider identityProvider)
    {
        _logger = logger;
        _mapper = mapper;
        _identityProvider = identityProvider;
    }

    public void Register(string id, IDeviceAccessor deviceAccessor)
    {
        _monitors.TryAdd(id, deviceAccessor);
    }

    public void Unregister(string id)
    {
        _monitors.TryRemove(id, out _);
    }

    public IDeviceAccessor? Get(string id)
    {
        _monitors.TryGetValue(id, out var deviceMonitor);
        return deviceMonitor;
    }

    public IDeviceAccessor Create(ILink link, Device device)
    {
        var monitor = new DeviceAccessor( link, this, device, _identityProvider);
        Register(device.Id.ToString(), monitor);
        return monitor;
    }

    public void Mount(Guid id)
    {
        var driveLetters = Enumerable.Range('C', 'Z' - 'C').Select(i => (char) i + ":")
            .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
        var letter = driveLetters[_monitors.Count - 1][0];
        var dokanLogger = new NullLogger();
        var dokan = new Dokan(dokanLogger);
        var rfs = new KuromeOperations(_mapper, _monitors[id.ToString()]);
        var builder = new DokanInstanceBuilder(dokan)
            // .ConfigureLogger(() => dokanLogger)
            .ConfigureOptions(options =>
            {
                options.Options = DokanOptions.FixedDrive;
                options.MountPoint = letter + ":\\";
            });
        Task.Run(async () =>
        {
            var semaphore = new SemaphoreSlim(0, 1);
            _mountSemaphores.TryAdd(id.ToString(), semaphore);
            using var instance = builder.Build(rfs);
            await semaphore.WaitAsync();
        });
    }
}