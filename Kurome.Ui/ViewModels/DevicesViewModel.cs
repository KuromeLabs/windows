using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData;
using Kurome.Network;
using Kurome.Ui.Pages.Devices;
using Kurome.Ui.Services;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wpf.Ui;

namespace Kurome.Ui.ViewModels;

public partial class DevicesViewModel : ReactiveObject
{
    private readonly INavigationService _navigationService;
    private readonly PipeService _pipeService;

    private readonly ReadOnlyObservableCollection<DeviceState> _activeDevices;
    public ReadOnlyObservableCollection<DeviceState> ActiveDevices => _activeDevices;

    [Reactive] public DeviceState SelectedDevice { get; set; }

    public DevicesViewModel(INavigationService navigationService, PipeService pipeService)
    {
        _navigationService = navigationService;
        _pipeService = pipeService;
        _pipeService.ActiveDevices
            .Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _activeDevices)
            .Subscribe();

    }

    public void OnDeviceClicked(DeviceState device)
    {
        SelectedDevice = device;
        _navigationService.NavigateWithHierarchy(typeof(DeviceDetails));
    }
}