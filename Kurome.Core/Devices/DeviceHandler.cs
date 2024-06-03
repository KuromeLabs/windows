using System.Buffers;
using System.Buffers.Binary;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DokanNet;
using DokanNet.Logging;
using FlatSharp;
using Kurome.Core.Filesystem;
using Kurome.Core.Network;
using Kurome.Fbs.Device;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Kurome.Core.Devices;

public class DeviceHandler(Link link, Guid id, string name, bool isDeviceTrusted)
{
    public Link Link { get; set; } = link;
    public Timer? IncomingPairTimer { get; set; }
    public Guid Id { get; set; } = id;
    public string Name { get; set; } = name;
    private ILogger _logger = Log.ForContext<DeviceHandler>();
    private readonly Dokan _dokan = new(new NullLogger());
    private DokanInstance? _dokanInstance;
    private string? _mountPoint;

    public bool IsConnected;
    private readonly BehaviorSubject<PairState> _state = new(isDeviceTrusted ? PairState.Paired : PairState.Unpaired);
    // public ISubject<PairState> State = _state;

    public void OnIncomingPairRequestAccepted()
    {
        IncomingPairTimer?.Dispose();
        if (_state.Value != PairState.PairRequestedByPeer) return;

        _state.OnNext(PairState.Paired);
        var packet = new Packet { Component = new Component(new Pair { Value = true }), Id = -1 };
        var maxSize = Packet.Serializer.GetMaxSize(packet);
        var buffer = ArrayPool<byte>.Shared.Rent(maxSize + 4);
        var span = buffer.AsSpan();
        var length = Packet.Serializer.Write(span[4..], packet);
        BinaryPrimitives.WriteInt32LittleEndian(span[..4], length);
        Link?.Send(buffer, length + 4);
        ArrayPool<byte>.Shared.Return(buffer);

    }

    public void OnIncomingPairRequestRejected()
    {
        IncomingPairTimer?.Dispose();
        if (_state.Value != PairState.PairRequestedByPeer) return;
        _state.OnNext(PairState.Unpaired);
        var packet = new Packet { Component = new Component(new Pair { Value = false }), Id = -1 };
        var maxSize = Packet.Serializer.GetMaxSize(packet);
        var buffer = ArrayPool<byte>.Shared.Rent(maxSize + 4);
        var span = buffer.AsSpan();
        var length = Packet.Serializer.Write(span[4..], packet);
        BinaryPrimitives.WriteInt32LittleEndian(span[..4], length);
        Link.Send(buffer, length + 4);
        ArrayPool<byte>.Shared.Return(buffer);
    }

    private void HandleIncomingPairPacket(Pair pair)
    {
        if (pair.Value)
        {
            switch (_state.Value)
            {
                case PairState.Paired:
                    //pair request but we are already paired, ignore
                    break;
                case PairState.PairRequested:
                    //we requested pair and it's accepted
                    break;
                case PairState.Unpaired:
                    //incoming pair request from peer

                    IncomingPairTimer?.Dispose();
                    IncomingPairTimer = new Timer(t =>
                    {
                        _logger.Information("Pair request timed out for {Id}", Id);
                        if (_state.Value != PairState.PairRequestedByPeer) return;
                        IncomingPairTimer?.Dispose();
                    }, null, 25000, Timeout.Infinite);
                    _state.OnNext(PairState.PairRequestedByPeer);
                    break;
            }
        }
        else
        {
            switch (_state.Value)
            {
                case PairState.Paired:
                    //unpair request
                    break;
                case PairState.PairRequested:
                    //we requested pair and it's rejected
                    break;
            }
        }
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

    private bool MountToAvailableMountPoint(DeviceAccessor deviceAccessor)
    {
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

    public void Start()
    {
        if (_state.Value == PairState.Paired)
        {
            var deviceAccessor = new DeviceAccessor(Link!, Name, Id);
            MountToAvailableMountPoint(deviceAccessor);
        }

        Link.DataReceived
            .Where(x => Packet.Serializer.Parse(x.Data).Component?.Kind == Component.ItemKind.Pair)
            .ObserveOn(NewThreadScheduler.Default)
            .Subscribe(buffer =>
            {
                var pair = Packet.Serializer.Parse(buffer.Data).Component?.Pair!;
                HandleIncomingPairPacket(pair);
            }, _ =>
            {
                _logger.Information("Link closed with {Name} ({Id})", Name, Id);
                Stop();
            }, () => { });
    }


    public void Stop()
    {
        Unmount();
        Link.Dispose();
    }
    
}

public enum PairState
{
    Paired,
    Unpaired,
    PairRequested,
    PairRequestedByPeer
}