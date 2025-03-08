using System.Buffers;
using System.Buffers.Binary;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using FlatSharp;
using Kurome.Core.Interfaces;
using Kurome.Fbs.Device;
using Kurome.Network;
using Serilog;

namespace Kurome.Core.Plugins;

public class IdentityPlugin(IIdentityProvider identityProvider, DeviceHandle handle) : IPlugin
{
    private IDisposable? _subscription;
    public bool Disposed { get; private set; }

    protected virtual void Dispose(bool disposing)
    {
        if (Disposed) return;
        if (disposing)
        {
            _subscription?.Dispose();
            Disposed = true;
        }
    }

    public void Dispose()
    {
        Log.Information("Disposing IdentityPlugin for handle {name} ({id})", handle.Name, handle.Id);
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void Start()
    {
        _subscription = handle.Link.DataReceived
            .Where(x => Packet.Serializer.Parse(x.Data).Component!.Value.Kind == Component.ItemKind.DeviceIdentityQuery)
            .ObserveOn(NewThreadScheduler.Default)
            .Subscribe(
                onNext: incomingBuffer =>
                {
                    var incomingPacket = Packet.Serializer.Parse(incomingBuffer.Data);
                    var component = new Component(new DeviceIdentityResponse
                    {
                        FreeBytes = 0,
                        TotalBytes = 0,
                        Name = identityProvider.GetEnvironmentName(),
                        Id = identityProvider.GetEnvironmentId(),
                        LocalIp = "",
                        Platform = Platform.Windows,
                        TcpListeningPort = 0
                    });
                    var identityPacket = new Packet { Component = component, Id = incomingPacket.Id };
                    var size = Packet.Serializer.GetMaxSize(identityPacket);
                    var buffer = ArrayPool<byte>.Shared.Rent(size);
                    var span = buffer.AsSpan();
                    var length = Packet.Serializer.Write(span[4..], identityPacket);
                    BinaryPrimitives.WriteInt32LittleEndian(span[..4], length);
                    handle.Link.Send(buffer, length + 4);
                    ArrayPool<byte>.Shared.Return(buffer);
                    incomingBuffer.Free();
                },
                onError: exception => { Dispose(); }
            );
    }
}