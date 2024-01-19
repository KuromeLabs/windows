using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Kurome.Core;
using Kurome.Ui.Pages.Devices;
using Wpf.Ui;

namespace Kurome.Ui.ViewModels;

public partial class DevicesViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    [ObservableProperty] private ICollection<Device> _activeDevices = new ObservableCollection<Device>();

    public readonly object ActiveDevicesLock = new();

    [ObservableProperty] private Device? _selectedDevice;

    public DevicesViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public void SetDevices(List<Device> devices)
    {
        lock (ActiveDevicesLock)
        {
            ActiveDevices.Clear();
            foreach (var device in devices)
            {
                ActiveDevices.Add(device);
            }
        }
    }

    public void OnDeviceClicked(Device device)
    {
        SelectedDevice = device;
        _navigationService.NavigateWithHierarchy(typeof(DeviceDetails));
    }
}