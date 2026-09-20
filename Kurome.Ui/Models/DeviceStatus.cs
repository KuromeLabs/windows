using Kurome.Fbs.Ipc;

namespace Kurome.Ui.Models;

public enum DeviceStatus
{
    Connected,

    Offline,

    Available,

    Pending,

    PendingFromPeer,

    Unavailable
}

public static class DeviceStatusExtensions
{
    public static DeviceStatus ToStatus(PairState state, bool isConnected) => (state, isConnected) switch
    {
        (PairState.Paired, false) => DeviceStatus.Offline,
        (_, false) => DeviceStatus.Unavailable,
        (PairState.Paired, true) => DeviceStatus.Connected,
        (PairState.PairRequested, true) => DeviceStatus.Pending,
        (PairState.PairRequestedByPeer, true) => DeviceStatus.PendingFromPeer,
        _ => DeviceStatus.Available
    };

    public static string Label(this DeviceStatus status) => status switch
    {
        DeviceStatus.Connected => "Connected",
        DeviceStatus.Offline => "Offline",
        DeviceStatus.Available => "Available to pair",
        DeviceStatus.Pending => "Waiting for the other device…",
        DeviceStatus.PendingFromPeer => "Wants to pair with you",
        _ => "Unavailable"
    };
}
