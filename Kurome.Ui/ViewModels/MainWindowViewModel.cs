using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Kurome.Ui.Pages.Devices;
using Wpf.Ui.Controls;

namespace Kurome.Ui.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private ICollection<object> _menuItems = new ObservableCollection<object>
    {
        new NavigationViewItem("Devices", SymbolRegular.Phone24, typeof(Devices)),
        new NavigationViewItemSeparator(),
        new NavigationViewItem("Settings", SymbolRegular.Settings24, typeof(Devices))
    };
    
    
}