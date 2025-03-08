using System.Security.Cryptography.X509Certificates;
using DokanNet;
using DokanNet.Logging;
using DynamicData;
using Kurome.Core;
using Kurome.Core.Devices;
using Kurome.Core.Filesystem;
using Kurome.Core.Interfaces;
using Kurome.Core.Network;
using Kurome.Core.Plugins;
using Kurome.Fbs.Ipc;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Kurome.Network;

public class  DeviceHandle(Link link, Guid id, string name, bool isDeviceTrusted, X509Certificate2 certificate, IIdentityProvider identityProvider): IDisposable
{
    public Link Link { get; set; } = link;
    public bool Disposed;
    public Timer? IncomingPairTimer { get; set; }
    public Guid Id { get; set; } = id;
    public X509Certificate2 Certificate = certificate;
    public string Name { get; set; } = name;
    private ILogger _logger = Log.ForContext<DeviceHandle>();
    private readonly List<IPlugin> _plugins = [];
    

    public PairState PairState = isDeviceTrusted ? PairState.Paired : PairState.Unpaired;

    public void ClearPlugins()
    {
        _plugins.ForEach(p => p.Dispose());
        _plugins.Clear();
    }

    public void ReloadPlugins()
    {
        ClearPlugins();
        _plugins.Add(new IdentityPlugin(identityProvider, this));
        _plugins.Add(new KuromeFsPlugin(this));
        _plugins.ForEach(p => p.Start());
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (Disposed || !disposing) return;
        Disposed = true;
        IncomingPairTimer?.Dispose();
        Link.Dispose();
        ClearPlugins();
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