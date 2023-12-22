using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Kurome.Core;

namespace Kurome.Ui.ViewModels;

public partial class DevicesViewModel : ObservableObject
{
    [ObservableProperty] private ICollection<Device> _activeDevices = new ObservableCollection<Device>();

    public readonly object ActiveDevicesLock = new();

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
}