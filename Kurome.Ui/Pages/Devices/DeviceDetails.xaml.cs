using System.Windows;
using Kurome.Ui.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace Kurome.Ui.Pages.Devices;

public partial class DeviceDetails : INavigableView<DeviceDetailsViewModel>
{
    public DeviceDetails(DeviceDetailsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }

    public DeviceDetailsViewModel ViewModel { get; }

    private void OnCopyIdClicked(object sender, RoutedEventArgs e) => ViewModel.CopyId();

    private async void OnUnpairClicked(object sender, RoutedEventArgs e) => await ViewModel.UnpairAsync();
}
