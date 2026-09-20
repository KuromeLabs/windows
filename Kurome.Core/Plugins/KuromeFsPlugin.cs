using DokanNet;
using DokanNet.Logging;
using Kurome.Core.Devices;
using Kurome.Core.Filesystem;
using Kurome.Fbs.Ipc;
using Kurome.Network;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Kurome.Core.Plugins;

public class KuromeFsPlugin(DeviceHandle handle) : IPlugin
{
    
    public bool Disposed { get; private set; }
    private readonly Dokan _dokan = new(new NullLogger());
    private DokanInstance? _dokanInstance;
    private string? _mountPoint;
    private readonly ILogger _logger = Log.ForContext<KuromeFsPlugin>();

    public string? MountPoint => _dokanInstance == null ? null : _mountPoint;

    public bool IsMounted => _dokanInstance != null;

    protected virtual void Dispose(bool disposing)
    {
        if (Disposed) return;
        if (disposing)
        {
            Disposed = true;
            Unmount();
        }
    }

    public void Dispose()
    {
        Log.Information("Disposing KuromeFsPlugin for handle {name} ({id})", handle.Name, handle.Id);
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    private bool Mount(string mountPoint, DeviceAccessor deviceAccessor)
    {
        var fs = new KuromeFs(mountPoint, deviceAccessor);
    
        _logger.Information("Mounting filesystem");
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
            _dokanInstance = builder.Build(fs);
            _logger.Information("Successfully mounted filesystem at {MountPoint}", mountPoint);
            return true;
        }
        catch (Exception e)
        {
            _logger.Error("Could not mount filesystem - exception: {@Exception}", e);
        }
    
        return false;
    }
    
    private bool Unmount()
    {
        if (_dokanInstance == null || _mountPoint == null) return false;
        try
        {
            if (!_dokan.RemoveMountPoint(_mountPoint)) return false;
            _dokanInstance.WaitForFileSystemClosed(uint.MaxValue);
            _dokanInstance.Dispose();
            _logger.Information("Unmounted filesystem at {MountPoint}", _mountPoint);
            return true;
        }
        catch (Exception e)
        {
            _logger.Error(e, "Could not unmount filesystem at {MountPoint}", _mountPoint);
            return false;
        }
        finally
        {
            _dokanInstance = null;
            _mountPoint = null;
        }
    }
    
    private bool MountToAvailableMountPoint()
    {
        var deviceAccessor = new DeviceAccessor(handle.Link, handle.Name, handle.Id);
        var list = Enumerable.Range('C', 'Z' - 'C').Select(i => (char)i + ":")
            .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
        if (list.Count == 0)
        {
            _logger.Error("No free drive letter available to mount {Name}", handle.Name);
            return false;
        }

        _mountPoint = list[0];
        return Mount(_mountPoint, deviceAccessor);
    }

    public void Subscribe()
    {
    }

    public void Start()
    {
        if (handle.PairState == PairState.Paired)
            MountToAvailableMountPoint();
    }
}