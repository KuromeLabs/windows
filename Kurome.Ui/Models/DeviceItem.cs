using Kurome.Fbs.Ipc;
using ReactiveUI;

namespace Kurome.Ui.Models;

public class DeviceItem : ReactiveObject
{
    public DeviceItem(string id, string name)
    {
        Id = id;
        _name = name;
    }

    public static DeviceItem From(DeviceState state)
    {
        var item = new DeviceItem(state.Id!, state.Name ?? "Unknown device");
        item.ApplyFrom(state);
        return item;
    }

    public string Id { get; }

    private string _name;
    public string Name
    {
        get => _name;
        private set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private PairState _pairState = PairState.Unpaired;
    public PairState PairState
    {
        get => _pairState;
        private set
        {
            this.RaiseAndSetIfChanged(ref _pairState, value);
            RaiseDerived();
        }
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isConnected, value);
            RaiseDerived();
        }
    }

    private bool _isMounted;
    public bool IsMounted
    {
        get => _isMounted;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isMounted, value);
            this.RaisePropertyChanged(nameof(MountDescription));
        }
    }

    private string? _mountPoint;
    public string? MountPoint
    {
        get => _mountPoint;
        private set
        {
            this.RaiseAndSetIfChanged(ref _mountPoint, value);
            this.RaisePropertyChanged(nameof(MountDescription));
        }
    }

    public DeviceStatus Status => DeviceStatusExtensions.ToStatus(_pairState, _isConnected);

    public string StatusLabel => Status.Label();

    public bool IsPaired => _pairState == PairState.Paired;

    public string TrustDescription => IsPaired ? "Paired" : "Not paired";

    public string MountDescription => _isMounted && !string.IsNullOrEmpty(_mountPoint)
        ? _mountPoint!
        : "Not mounted";

    public bool IsStale => !IsConnected && !IsPaired;

    public void ApplyFrom(DeviceState state)
    {
        if (!string.IsNullOrEmpty(state.Name)) Name = state.Name!;
        PairState = state.State;
        IsConnected = state.IsConnected;
        IsMounted = state.IsMounted;
        MountPoint = state.MountPoint;
    }

    public void MarkDisconnected()
    {
        IsMounted = false;
        MountPoint = null;
        IsConnected = false;
    }

    public DeviceState ToDeviceState() => new()
    {
        Id = Id,
        Name = Name,
        State = PairState,
        IsConnected = IsConnected,
        IsMounted = IsMounted,
        MountPoint = MountPoint
    };

    private void RaiseDerived()
    {
        this.RaisePropertyChanged(nameof(Status));
        this.RaisePropertyChanged(nameof(StatusLabel));
        this.RaisePropertyChanged(nameof(IsPaired));
        this.RaisePropertyChanged(nameof(TrustDescription));
        this.RaisePropertyChanged(nameof(IsStale));
    }
}
