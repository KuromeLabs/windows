using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using DokanNet;
using DokanNet.Logging;
using Kurome.Core.Devices;
using Kurome.Core.Filesystem;
using Kurome.Core.Network;
using Kurome.Fbs.Ipc;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Kurome.Network;

public class  DeviceHandle(Link link, Guid id, string name, bool isDeviceTrusted, X509Certificate2 certificate): IDisposable
{
    public Link Link { get; set; } = link;
    public bool Disposed;
    public Timer? IncomingPairTimer { get; set; }
    public Guid Id { get; set; } = id;
    public X509Certificate2 Certificate = certificate;
    public string Name { get; set; } = name;
    private ILogger _logger = Log.ForContext<DeviceHandle>();
    private readonly Dokan _dokan = new(new NullLogger());
    private DokanInstance? _dokanInstance;
    private string? _mountPoint;
    

    public PairState PairState = isDeviceTrusted ? PairState.Paired : PairState.Unpaired;

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
    
    public bool MountToAvailableMountPoint()
    {
        var deviceAccessor = new DeviceAccessor(Link, Name, Id);
        var list = Enumerable.Range('C', 'Z' - 'C').Select(i => (char)i + ":")
            .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
        _mountPoint = list[0];
        return Mount(_mountPoint, deviceAccessor);
    }
    
    private bool Unmount()
    {
        if (!_dokan.RemoveMountPoint(_mountPoint!)) return false;
        _dokanInstance!.WaitForFileSystemClosed(uint.MaxValue);
        _dokanInstance!.Dispose();
        return true;
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (Disposed || !disposing) return;
        Disposed = true;
        IncomingPairTimer?.Dispose();
        Link.Dispose();
        Unmount();
    }

    public void Dispose()
    {
        _logger.Information("Disposing DeviceHandler ${Id}", Id);
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public DeviceState ToDeviceState()
    {
        return new DeviceState
        {
            Id = Id.ToString(),
            IsConnected = !Disposed,
            Name = Name,
            State = PairState
        };
    }
    
}