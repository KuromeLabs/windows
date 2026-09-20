using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using DynamicData;
using DynamicData.Binding;
using Kurome.Ui.Models;
using Kurome.Ui.Pages.Devices;
using Kurome.Ui.Services;
using ReactiveUI;
using Wpf.Ui;

namespace Kurome.Ui.ViewModels;

public class DevicesViewModel : ReactiveObject, IDisposable
{
    private readonly INavigationService _navigationService;
    private readonly DeviceDetailsViewModel _detailsViewModel;
    private readonly KuromeClient _client;
    private readonly IDisposable _subscriptions;

    private readonly ReadOnlyObservableCollection<DeviceItem> _pairedDevices;
    private readonly ReadOnlyObservableCollection<DeviceItem> _availableDevices;

    public DevicesViewModel(
        DeviceStore store,
        KuromeClient client,
        INavigationService navigationService,
        DeviceDetailsViewModel detailsViewModel)
    {
        _navigationService = navigationService;
        _detailsViewModel = detailsViewModel;
        _client = client;

        var comparer = SortExpressionComparer<DeviceItem>
            .Descending(d => d.IsConnected)
            .ThenByAscending(d => d.Name);

        var paired = store.Connect()
            .AutoRefresh()
            .Filter(d => d.IsPaired)
            .SortAndBind(out _pairedDevices, comparer)
            .Subscribe(_ => UpdateSectionVisibility());

        var available = store.Connect()
            .AutoRefresh()
            .Filter(d => !d.IsPaired)
            .SortAndBind(out _availableDevices, comparer)
            .Subscribe(_ => UpdateSectionVisibility());

        _subscriptions = new CompositeDisposable(paired, available);
    }

    public ReadOnlyObservableCollection<DeviceItem> PairedDevices => _pairedDevices;
    public ReadOnlyObservableCollection<DeviceItem> AvailableDevices => _availableDevices;

    private bool _hasPairedDevices;
    public bool HasPairedDevices
    {
        get => _hasPairedDevices;
        private set => this.RaiseAndSetIfChanged(ref _hasPairedDevices, value);
    }

    private bool _hasAvailableDevices;
    public bool HasAvailableDevices
    {
        get => _hasAvailableDevices;
        private set => this.RaiseAndSetIfChanged(ref _hasAvailableDevices, value);
    }

    private bool _isEmpty = true;
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => this.RaiseAndSetIfChanged(ref _isEmpty, value);
    }

    public void Refresh() => _client.RequestDeviceList();

    public void OnDeviceClicked(DeviceItem device)
    {
        _detailsViewModel.Show(device);
        _navigationService.NavigateWithHierarchy(typeof(DeviceDetails));
    }

    private void UpdateSectionVisibility()
    {
        HasPairedDevices = _pairedDevices.Count > 0;
        HasAvailableDevices = _availableDevices.Count > 0;
        IsEmpty = !HasPairedDevices && !HasAvailableDevices;
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
        GC.SuppressFinalize(this);
    }
}
