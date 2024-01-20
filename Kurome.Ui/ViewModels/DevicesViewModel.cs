using System.Collections.ObjectModel;
using System.Reactive.Linq;
using DynamicData;
using Kurome.Core;
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

    private readonly ReadOnlyObservableCollection<Device> _activeDevices;
    public ReadOnlyObservableCollection<Device> ActiveDevices => _activeDevices;

    [Reactive] public Device SelectedDevice { get; set; }

    public DevicesViewModel(INavigationService navigationService, PipeService pipeService)
    {
        _navigationService = navigationService;
        _pipeService = pipeService;
        Console.WriteLine("Doing the property thing");
        _pipeService.ActiveDevices
            .Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _activeDevices)
            .Subscribe();

    }

    public void OnDeviceClicked(Device device)
    {
        SelectedDevice = device;
        _navigationService.NavigateWithHierarchy(typeof(DeviceDetails));
    }
}