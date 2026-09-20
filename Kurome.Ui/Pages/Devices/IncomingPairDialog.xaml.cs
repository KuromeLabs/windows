using Kurome.Fbs.Ipc;
using Wpf.Ui.Controls;

namespace Kurome.Ui.Pages.Devices;

public partial class IncomingPairDialog : ContentDialog
{
    public DeviceState DeviceState { get; }

    public IncomingPairDialog(ContentDialogHost contentDialogHost, DeviceState deviceState)
        : base(contentDialogHost)
    {
        DeviceState = deviceState;
        DataContext = this;
        InitializeComponent();
    }
}
