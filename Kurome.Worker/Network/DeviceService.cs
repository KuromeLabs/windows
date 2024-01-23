using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net.Security;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using DokanNet;
using DokanNet.Logging;
using DynamicData;
using FlatSharp;
using Kurome.Core.Devices;
using Kurome.Core.Filesystem;
using Kurome.Core.Interfaces;
using Kurome.Core.Network;
using Kurome.Fbs;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Kurome.Network;

public class DeviceService(ILogger<DeviceService> logger, ISecurityService<X509Certificate2> sslService)
{
    private readonly Dokan _dokan = new(new NullLogger());
    private readonly Dictionary<Guid, DeviceContext> _activeDevices = new();

    private readonly SourceCache<DeviceState, Guid> _deviceStates = new(t => t.Device.Id);
    public IObservableCache<DeviceState, Guid> DeviceStates => _deviceStates.AsObservableCache();

    public async Task HandleIncomingTcp(TcpClient client, CancellationToken cancellationToken)
    {
        await Task.Yield();
        var info = await ReadIdentityAsync(client, cancellationToken);
        if (info == null)
        {
            logger.LogError("Failed to read device identity from incoming connection");
            return;
        }

        var id = info.Item1;
        var name = info.Item2;
        _deviceStates.AddOrUpdate(new DeviceState(new Device(id, name))
        {
            Status = DeviceState.State.Connecting
        });
        logger.LogInformation("Checking existing devices");
        var existingDeviceContext = _activeDevices.GetValueOrDefault(id);
        if (existingDeviceContext != null)
        {
            logger.LogInformation("Device {Name} ({Id}) is already active, reconnecting", name, id);
            // _fileSystemHost.Unmount("E");
            existingDeviceContext.Dispose();
            _activeDevices.Remove(id);
            _deviceStates.AddOrUpdate(new DeviceState(new Device(id, name))
            {
                Status = DeviceState.State.Disconnected
            });
        }

        Link? link;
        try
        {
            var stream = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
            await stream.AuthenticateAsServerAsync(sslService.GetSecurityContext(), true, SslProtocols.None, true);
            stream.ReadTimeout = 10000;
            link = new Link(stream);
        }
        catch (Exception e)
        {
            logger.LogError($"{e}");
            return;
        }

        logger.LogInformation("Link established with {Name} ({Id})", info.Item2, id);

        var deviceAccessor = new DeviceAccessor(link, new Device(id, name));
        var mountPoint = "E:";
        var deviceContext = new DeviceContext(deviceAccessor, link, _dokan, mountPoint);

        link.IsConnected
            .ObserveOn(TaskPoolScheduler.Default)
            .Subscribe(onConnected =>
                {
                    _deviceStates.AddOrUpdate(new DeviceState(new Device(id, name))
                    {
                        Status = DeviceState.State.ConnectedTrusted
                    });
                },
                () =>
                {
                    logger.LogInformation("Link disconnected from {Name} ({Id})", info.Item2, id);
                    deviceContext.Dispose();
                    _activeDevices.Remove(id);
                    _deviceStates.AddOrUpdate(new DeviceState(new Device(id, name))
                    {
                        Status = DeviceState.State.Disconnected
                    });
                });

        link.Start(cancellationToken);
        var instance = Mount(mountPoint, deviceAccessor);
        if (instance != null) deviceContext.SetDokanInstance(instance);
    }


    private async Task<Tuple<Guid, string>?> ReadIdentityAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var sizeBuffer = new byte[4];
        try
        {
            await client.GetStream().ReadExactlyAsync(sizeBuffer, 0, 4, cancellationToken);
            var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
            var readBuffer = ArrayPool<byte>.Shared.Rent(size);
            await client.GetStream().ReadExactlyAsync(readBuffer, 0, size, cancellationToken);
            var info = Packet.Serializer.Parse(readBuffer).Component?.DeviceQueryResponse;
            ArrayPool<byte>.Shared.Return(readBuffer);
            return new Tuple<Guid, string>(Guid.Parse(info!.Id!), info.Name!);
        }
        catch (Exception e)
        {
            logger.LogError("{@Exception}", e.ToString());
            return null;
        }
    }


    public DokanInstance? Mount(string mountPoint, DeviceAccessor deviceAccessor)
    {
        var fs = new KuromeFs(deviceAccessor);
        if (!fs.Init())
        {
            logger.LogError("Could not mount filesystem - device returned null root node");
            return null;
        }

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
            return instance;
        }
        catch (Exception e)
        {
            logger.LogError("Could not mount filesystem - exception: {@Exception}", e);
        }

        return null;
    }

    private class DeviceContext(DeviceAccessor accessor, Link link, Dokan dokan, string mountPoint) : IDisposable
    {
        private readonly ILogger _logger = Log.ForContext<DeviceContext>();
        private bool Disposed { get; set; }
        private DokanInstance? _instance;

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed) return;
            _logger.Information($"Disposing DeviceContext {accessor.Device.Id}");
            if (disposing)
            {
                dokan.RemoveMountPoint(mountPoint);
                _instance?.WaitForFileSystemClosed(uint.MaxValue);
                _instance?.Dispose();
                link.Dispose();
            }

            Disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void SetDokanInstance(DokanInstance instance)
        {
            _instance = instance;
        }
    }
}

public class DeviceState(Device device)
{
    public Device Device { get; set; } = device;
    public State Status { get; set; } = State.Disconnected;

    public enum State
    {
        Disconnected,
        Connecting,
        ConnectedTrusted,
        ConnectedUntrusted
    }
}