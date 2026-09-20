using System.Reactive.Linq;
using System.Reactive.Subjects;
using Kurome.Fbs.Ipc;
using Serilog;

namespace Kurome.Ui.Services;

public class KuromeClient
{
    private readonly PipeService _pipe;
    private readonly BehaviorSubject<Kurome.Fbs.Ipc.ServiceInfo?> _serviceInfo = new(null);
    private readonly ILogger _logger = Log.ForContext<KuromeClient>();

    public KuromeClient(PipeService pipe)
    {
        _pipe = pipe;

        var packets = pipe.Packets.Where(p => p.Component != null);

        DeviceChanged = packets
            .Where(p => p.Component!.Value.Kind == Component.ItemKind.DeviceState)
            .Select(p => p.Component!.Value.DeviceState!);

        DeviceList = packets
            .Where(p => p.Component!.Value.Kind == Component.ItemKind.DeviceStateList)
            .Select(p => (IReadOnlyList<DeviceState>)(p.Component!.Value.DeviceStateList.States
                                                      ?? new List<DeviceState>()));

        PairEvents = packets
            .Where(p => p.Component!.Value.Kind == Component.ItemKind.PairEvent)
            .Select(p => p.Component!.Value.PairEvent)
            .Where(e => e.DeviceState?.Id != null);

        packets
            .Where(p => p.Component!.Value.Kind == Component.ItemKind.ServiceInfo)
            .Select(p => p.Component!.Value.ServiceInfo!)
            .Subscribe(info => _serviceInfo.OnNext(info));

        ConnectionState = pipe.ConnectionState;

        ConnectionState
            .Where(state => state == PipeConnectionState.Connected)
            .Subscribe(_ =>
            {
                RequestDeviceList();
                RequestServiceInfo();
            });

        ConnectionState
            .Where(state => state != PipeConnectionState.Connected)
            .Subscribe(_ => _serviceInfo.OnNext(null));
    }

    public IObservable<DeviceState> DeviceChanged { get; }
    public IObservable<IReadOnlyList<DeviceState>> DeviceList { get; }
    public IObservable<PairEvent> PairEvents { get; }

    public IObservable<Kurome.Fbs.Ipc.ServiceInfo> ServiceInfoReceived =>
        _serviceInfo.Where(info => info != null).Select(info => info!);
    public IObservable<PipeConnectionState> ConnectionState { get; }

    public void RequestDeviceList()
    {
        _logger.Debug("Requesting the device list");
        _pipe.Send(new IpcPacket { Component = new DeviceStateListRequest() });
    }

    public void RequestServiceInfo() =>
        _pipe.Send(new IpcPacket { Component = new ServiceInfoRequest() });

    public void AcceptPairing(DeviceState device) =>
        SendPairEvent(PairEventType.PairRequestAccept, device);

    public void RejectPairing(DeviceState device) =>
        SendPairEvent(PairEventType.PairRequestReject, device);

    public void Unpair(DeviceState device)
    {
        _logger.Information("Unpairing {Name}", device.Name);
        SendPairEvent(PairEventType.Unpair, device);
    }

    private void SendPairEvent(PairEventType type, DeviceState device) =>
        _pipe.Send(new IpcPacket { Component = new PairEvent { Value = type, DeviceState = device } });
}
