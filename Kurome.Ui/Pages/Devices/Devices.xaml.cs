using System.Windows;
using Kurome.Core.Devices;
using Kurome.Ui.ViewModels;
using Wpf.Ui.Controls;

namespace Kurome.Ui.Pages.Devices;

public partial class Devices : INavigableView<DevicesViewModel>
{
    public DevicesViewModel ViewModel { get; }

    public Devices(DevicesViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
      
        InitializeComponent();
    }

    private void OnDeviceSelected(object sender, RoutedEventArgs e)
    {
        var cardAction = (CardAction)sender;
        var selectedDevice = (Device) cardAction.DataContext;
        if (selectedDevice != null)
            ViewModel.OnDeviceClicked(selectedDevice);
    }
}