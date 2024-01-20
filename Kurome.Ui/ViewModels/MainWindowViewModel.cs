using System.Collections.ObjectModel;
using Kurome.Ui.Pages.Devices;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Wpf.Ui.Controls;

namespace Kurome.Ui.ViewModels;

public partial class MainWindowViewModel : ReactiveObject
{
    [Reactive] public ICollection<object> MenuItems { get; set; } = new ObservableCollection<object>
    {
        new NavigationViewItem("Devices", SymbolRegular.Phone24, typeof(Devices)),
        new NavigationViewItemSeparator(),
        new NavigationViewItem("Settings", SymbolRegular.Settings24, typeof(Devices))
    };
    
    [Reactive]
    public string ApplicationTitle { get; set; } = "Kurome";

    [Reactive] public string ServiceStatus { get; set; } = "";
}