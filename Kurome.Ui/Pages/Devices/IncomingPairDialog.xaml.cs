using System.Windows.Controls;
using Kurome.Network;
using Kurome.Ui.ViewModels;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;

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