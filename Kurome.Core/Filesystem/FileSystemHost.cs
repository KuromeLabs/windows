using System.Collections.Concurrent;
using DokanNet;
using DokanNet.Logging;

namespace Kurome.Core.Filesystem;

public class FileSystemHost : IFileSystemHost
{
    private readonly Dokan _dokan = new(new NullLogger());
    private readonly ConcurrentDictionary<string, DokanInstance> _dokanInstances = new();
    private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<FileSystemHost>();
    
    public void Mount(string mountPoint, Device device)
    {
        var fs = new KuromeFs(device);
        if (!fs.Init())
        {
            _logger.Error("Could not mount filesystem - device returned null root node");
            return;
        }
        var builder = new DokanInstanceBuilder(_dokan)
            .ConfigureLogger(() => new KuromeDokanLogger())
            .ConfigureOptions(options =>
            {
                options.Options = DokanOptions.FixedDrive ;
                options.MountPoint = mountPoint + ":\\";
                options.SingleThread = false;
            });
        var instance = builder.Build(fs);
        
        _dokanInstances.TryAdd(mountPoint, instance);
    }

    public void Unmount(string mountPoint)
    {
        _dokan.RemoveMountPoint(mountPoint);
    }

    public void DisposeInstance(string mountPoint)
    {
        _dokanInstances.TryRemove(mountPoint, out var instance);
        instance?.WaitForFileSystemClosed(uint.MaxValue);
        instance?.Dispose();
    }
}