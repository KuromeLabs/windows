using System.Windows.Controls;
using Kurome.Fbs.Ipc;
using Wpf.Ui.Controls;

namespace Kurome.Ui.Pages.Devices;

public partial class IncomingPairDialog : ContentDialog
{
    public DeviceState DeviceState { get; set; }

    public IncomingPairDialog(ContentPresenter contentPresenter, DeviceState deviceState) : base(contentPresenter)
    {
        DeviceState = deviceState;
        DataContext = this;
        InitializeComponent();
    }
}