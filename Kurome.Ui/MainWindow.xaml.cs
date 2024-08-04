using System.Windows;
using Kurome.Ui.Pages.Devices;
using Kurome.Ui.Services;
using Kurome.Ui.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Kurome.Ui;

public partial class MainWindow 
{
    private readonly DialogViewModel _dialogViewModel;
    private readonly PipeService _pipeService;

    public MainWindow(
        MainWindowViewModel viewModel,
        DialogViewModel dialogViewModel,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        IContentDialogService contentDialogService,
        PipeService pipeService
    )
    {
        _dialogViewModel = dialogViewModel;
        _pipeService = pipeService;
        // Appearance.SystemThemeWatcher.Watch(this);
        
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();

        navigationService.SetNavigationControl(RootNavigation);
        contentDialogService.SetDialogHost(RootContentDialog);
        RootNavigation.SetServiceProvider(serviceProvider);
    }

    public MainWindowViewModel ViewModel { get; }

    private bool _isUserClosedPane;

    private bool _isPaneOpenedOrClosedFromCode;

    private void OnNavigationSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not NavigationView navigationView)
        {
            return;
        }

        RootNavigation.HeaderVisibility = Visibility.Visible;
    }

    private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isUserClosedPane)
        {
            return;
        }

        _isPaneOpenedOrClosedFromCode = true;
        RootNavigation.IsPaneOpen = !(e.NewSize.Width <= 1200);
        _isPaneOpenedOrClosedFromCode = false;
    }

    private void NavigationView_OnPaneOpened(NavigationView sender, RoutedEventArgs args)
    {
        if (_isPaneOpenedOrClosedFromCode)
        {
            return;
        }

        _isUserClosedPane = false;
    }

    private void NavigationView_OnPaneClosed(NavigationView sender, RoutedEventArgs args)
    {
        if (_isPaneOpenedOrClosedFromCode)
        {
            return;
        }

        _isUserClosedPane = true;
    }

    private void NavigationView_OnNavigated(NavigationView sender, NavigatedEventArgs args)
    {
        if (args.Page.GetType() == typeof(Devices))
            _pipeService.RequestDeviceStateList();
    }
    
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow mainWindow)
        {
            return;
        }

        _ = mainWindow.RootNavigation.Navigate(typeof(Devices));
    }
}