using Kurome.Ui.ViewModels;
using Wpf.Ui.Abstractions.Controls;

namespace Kurome.Ui.Pages.Settings;

public partial class SettingsPage : INavigableView<SettingsViewModel>
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }
}
