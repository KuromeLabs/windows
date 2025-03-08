using Kurome.Ui.ViewModels;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace Kurome.Ui.Pages.Devices;

public partial class DeviceDetails : INavigableView<DevicesViewModel>
{
    public DeviceDetails(DevicesViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }

    public DevicesViewModel ViewModel { get; }
}