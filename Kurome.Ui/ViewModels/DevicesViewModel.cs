using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using DynamicData;
using Kurome.Fbs.Ipc;
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

    private readonly SourceCache<DeviceState, string> _devices = new(d => d.Id!);
    
    private readonly ReadOnlyObservableCollection<DeviceState> _activeDevices;
    public ReadOnlyObservableCollection<DeviceState> ActiveDevices => _activeDevices;
    [Reactive] public DeviceState SelectedDevice { get; set; }

    public DevicesViewModel(INavigationService navigationService, PipeService pipeService)
    {
        ReadOnlyObservableCollection<DeviceState> devices;
        _navigationService = navigationService;
        _pipeService = pipeService;
        _devices.Connect()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out _activeDevices)
            .Subscribe();
        _pipeService.IpcEventStreamObservable
            .ObserveOn(NewThreadScheduler.Default)
            .Subscribe(ipcPacket =>
            {
                switch (ipcPacket.Component!.Value.Kind)
                {
                    case Component.ItemKind.DeviceState:
                    {
                        _devices.Edit(l => l.AddOrUpdate(ipcPacket.Component!.Value.DeviceState!));
                        break;
                    }
                    case Component.ItemKind.DeviceStateList:
                    {
                        _devices.Edit(l => l.AddOrUpdate(ipcPacket.Component!.Value.DeviceStateList.States!));
                        break;
                    }
                    case Component.ItemKind.PairEvent:
                    {
                        _devices.Edit(l => l.AddOrUpdate(ipcPacket.Component!.Value.PairEvent.DeviceState!));
                        break;
                    }
                }
                 
            }, e => { }, () => { });
        
    }

    public void OnDeviceClicked(DeviceState device)
    {
        SelectedDevice = device;
        _navigationService.NavigateWithHierarchy(typeof(DeviceDetails));
    }
}