using System.Windows.Controls;
using System.Windows.Data;
using Kurome.Core;
using Kurome.Ui.ViewModels;

namespace Kurome.Ui.Pages.Devices;

public partial class Devices : Page
{
    public DevicesViewModel ViewModel { get; }

    public Devices(DevicesViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        BindingOperations.EnableCollectionSynchronization(viewModel.ActiveDevices, viewModel.ActiveDevicesLock);
        
        InitializeComponent();
    }
}