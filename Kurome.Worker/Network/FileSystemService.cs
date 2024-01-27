using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using DokanNet;
using DokanNet.Logging;
using Kurome.Core.Devices;
using Kurome.Core.Filesystem;
using Microsoft.Extensions.Logging;

namespace Kurome.Network;

public class FileSystemService(ILogger<FileSystemService> logger)
{
    private readonly Dokan _dokan = new(new NullLogger());
    private readonly ConcurrentDictionary<Guid, (DokanInstance, string)> _mountedDevices = new();

    public bool Mount(string mountPoint, DeviceAccessor deviceAccessor)
    {
        var fs = new KuromeFs(mountPoint, deviceAccessor);

        logger.LogInformation("Mounting filesystem");
        var builder = new DokanInstanceBuilder(_dokan)
            .ConfigureLogger(() => new KuromeDokanLogger())
            .ConfigureOptions(options =>
            {
                options.Options = DokanOptions.FixedDrive;
                options.MountPoint = mountPoint + "\\";
                options.SingleThread = false;
            });
        try
        {
            var instance = builder.Build(fs);
            logger.LogInformation("Successfully mounted filesystem at {MountPoint}", mountPoint);
            _mountedDevices.TryAdd(deviceAccessor.Device.Id, (instance, mountPoint));
            return true;
        }
        catch (Exception e)
        {
            logger.LogError("Could not mount filesystem - exception: {@Exception}", e);
        }

        return false;
    }

    public bool MountToAvailableMountPoint(DeviceAccessor deviceAccessor)
    {
        var list = Enumerable.Range('C', 'Z' - 'C').Select(i => (char) i + ":")
            .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
        var mountPoint = list[0];
        return Mount(mountPoint, deviceAccessor);
    }
    
    public bool Unmount(Guid deviceId)
    {
        if (_mountedDevices.TryGetValue(deviceId, out var value))
        {
            var (instance, mountPoint) = value;
            if (_dokan.RemoveMountPoint(mountPoint))
            {
                instance.WaitForFileSystemClosed(uint.MaxValue);
                instance.Dispose();
                _mountedDevices.TryRemove(deviceId, out _);
                return true;
            }
        }

        return false;
    }
}