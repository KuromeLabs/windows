using System.Windows;
using Kurome.Ui.Models;
using Kurome.Ui.ViewModels;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace Kurome.Ui.Pages.Devices;

public partial class Devices : INavigableView<DevicesViewModel>
{
    public Devices(DevicesViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }

    public DevicesViewModel ViewModel { get; }

    private void OnDeviceSelected(object sender, RoutedEventArgs e)
    {
        if (sender is CardAction { DataContext: DeviceItem device })
            ViewModel.OnDeviceClicked(device);
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e) => ViewModel.Refresh();
}
