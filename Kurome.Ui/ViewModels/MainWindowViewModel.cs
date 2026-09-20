using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Kurome.Ui.Pages.Devices;
using Kurome.Ui.Pages.Settings;
using Kurome.Ui.Services;
using ReactiveUI;
using Serilog;
using Wpf.Ui.Controls;

namespace Kurome.Ui.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    public MainWindowViewModel(KuromeClient client)
    {
        client.ConnectionState
            .Subscribe(OnConnectionStateChanged);

        client.ServiceInfoReceived
            .Subscribe(info => ServiceDescription =
                $"Kurome {info.Version} on {info.MachineName}, listening on port {info.TcpPort}");
    }

    public ICollection<object> MenuItems { get; } = new ObservableCollection<object>
    {
        new NavigationViewItem("Devices", SymbolRegular.Phone24, typeof(Devices)),
        new NavigationViewItemSeparator(),
        new NavigationViewItem("Settings", SymbolRegular.Settings24, typeof(SettingsPage))
    };

    public string ApplicationTitle => "Kurome";

    private string _serviceDescription = "";
    public string ServiceDescription
    {
        get => _serviceDescription;
        private set => this.RaiseAndSetIfChanged(ref _serviceDescription, value);
    }

    private string _serviceStatusTitle = "Connecting";
    public string ServiceStatusTitle
    {
        get => _serviceStatusTitle;
        private set => this.RaiseAndSetIfChanged(ref _serviceStatusTitle, value);
    }

    private string _serviceStatusMessage = "Looking for the Kurome background service…";
    public string ServiceStatusMessage
    {
        get => _serviceStatusMessage;
        private set => this.RaiseAndSetIfChanged(ref _serviceStatusMessage, value);
    }

    private PipeConnectionState _connectionState = PipeConnectionState.Connecting;
    public PipeConnectionState ConnectionState
    {
        get => _connectionState;
        private set => this.RaiseAndSetIfChanged(ref _connectionState, value);
    }

    private static readonly ILogger Logger = Log.ForContext<MainWindowViewModel>();

    private void OnConnectionStateChanged(PipeConnectionState state)
    {
        Logger.Information("Status banner received state {State}", state);

        ConnectionState = state;
        switch (state)
        {
            case PipeConnectionState.Connected:
                ServiceStatusTitle = "Connected";
                ServiceStatusMessage = "The Kurome service is running.";
                break;
            case PipeConnectionState.Connecting:
                ServiceStatusTitle = "Connecting";
                ServiceStatusMessage = "Looking for the Kurome background service\u2026";
                break;
            default:
                ServiceStatusTitle = "Service unavailable";
                ServiceStatusMessage =
                    "Devices cannot connect until the Kurome service is started.";
                break;
        }
    }
}
