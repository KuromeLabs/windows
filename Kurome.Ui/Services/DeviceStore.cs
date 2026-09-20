using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData;
using Kurome.Fbs.Ipc;
using Kurome.Ui.Models;
using Serilog;

namespace Kurome.Ui.Services;

public class DeviceStore : IDisposable
{
    private readonly SourceCache<DeviceItem, string> _devices = new(d => d.Id);
    private readonly ILogger _logger = Log.ForContext<DeviceStore>();
    private readonly IDisposable _subscriptions;

    public DeviceStore(KuromeClient client)
    {
        var listSubscription = client.DeviceList
            .Subscribe(Reconcile);

        var deviceSubscription = client.DeviceChanged
            .Subscribe(state => _devices.Edit(updater => Merge(updater, state)));

        var pairSubscription = client.PairEvents
            .Subscribe(e => _devices.Edit(updater => Merge(updater, e.DeviceState!)));

        var connectionSubscription = client.ConnectionState
            .Where(state => state != PipeConnectionState.Connected)
            .Subscribe(_ => _devices.Edit(updater =>
            {
                foreach (var device in updater.Items) device.MarkDisconnected();
                DropStale(updater);
            }));

        _subscriptions = new CompositeDisposable(
            listSubscription, deviceSubscription, pairSubscription, connectionSubscription);
    }

    public IObservable<IChangeSet<DeviceItem, string>> Connect() => _devices.Connect();

    public DeviceItem? Find(string id)
    {
        var lookup = _devices.Lookup(id);
        return lookup.HasValue ? lookup.Value : null;
    }

    private void Reconcile(IReadOnlyList<DeviceState> states)
    {
        _devices.Edit(updater =>
        {
            var incoming = states
                .Where(s => !string.IsNullOrEmpty(s.Id))
                .ToDictionary(s => s.Id!, s => s);

            var removed = updater.Keys.Where(key => !incoming.ContainsKey(key)).ToList();
            if (removed.Count > 0) updater.RemoveKeys(removed);

            foreach (var state in incoming.Values) Merge(updater, state);
        });
    }

    private void Merge(ISourceUpdater<DeviceItem, string> updater, DeviceState state)
    {
        if (string.IsNullOrEmpty(state.Id)) return;

        var existing = updater.Lookup(state.Id!);
        if (existing.HasValue)
        {
            existing.Value.ApplyFrom(state);
            if (existing.Value.IsStale)
            {
                _logger.Debug("Forgetting {Name}: unpaired and gone", existing.Value.Name);
                updater.RemoveKey(state.Id!);
            }

            return;
        }

        var item = DeviceItem.From(state);
        if (!item.IsStale) updater.AddOrUpdate(item);
    }

    private static void DropStale(ISourceUpdater<DeviceItem, string> updater)
    {
        var stale = updater.Items.Where(d => d.IsStale).Select(d => d.Id).ToList();
        if (stale.Count > 0) updater.RemoveKeys(stale);
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
        _devices.Dispose();
        GC.SuppressFinalize(this);
    }
}
