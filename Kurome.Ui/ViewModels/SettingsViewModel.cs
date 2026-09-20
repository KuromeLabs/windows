using System.Reactive.Linq;
using System.Reflection;
using Kurome.Ui.Services;
using ReactiveUI;

namespace Kurome.Ui.ViewModels;

public class SettingsViewModel : ReactiveObject
{
    private readonly AppThemeService _themeService;

    public SettingsViewModel(AppThemeService themeService, KuromeClient client)
    {
        _themeService = themeService;
        _selectedTheme = themeService.Current;

        client.ConnectionState
            .Subscribe(state =>
            {
                IsServiceConnected = state == PipeConnectionState.Connected;
                ServiceStatus = state switch
                {
                    PipeConnectionState.Connected => "Running",
                    PipeConnectionState.Connecting => "Looking for the service…",
                    _ => "Not running"
                };

                if (state != PipeConnectionState.Connected)
                {
                    ServiceVersion = Unavailable;
                    ListeningPort = Unavailable;
                }
            });

        client.ServiceInfoReceived
            .Subscribe(info =>
            {
                ServiceVersion = string.IsNullOrEmpty(info.Version) ? Unavailable : info.Version!;
                MachineName = info.MachineName ?? Environment.MachineName;
                ListeningPort = info.TcpPort == 0 ? "Not listening" : info.TcpPort.ToString();
            });

        client.RequestServiceInfo();
    }

    private const string Unavailable = "Unavailable";

    public string AppVersion { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion?.Split('+')[0]
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "unknown";

    public IReadOnlyList<AppTheme> Themes { get; } = [AppTheme.System, AppTheme.Light, AppTheme.Dark];

    private AppTheme _selectedTheme;
    public AppTheme SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTheme, value);
            _themeService.Current = value;
        }
    }

    private bool _isServiceConnected;
    public bool IsServiceConnected
    {
        get => _isServiceConnected;
        private set => this.RaiseAndSetIfChanged(ref _isServiceConnected, value);
    }

    private string _serviceStatus = "Looking for the service…";
    public string ServiceStatus
    {
        get => _serviceStatus;
        private set => this.RaiseAndSetIfChanged(ref _serviceStatus, value);
    }

    private string _serviceVersion = Unavailable;
    public string ServiceVersion
    {
        get => _serviceVersion;
        private set => this.RaiseAndSetIfChanged(ref _serviceVersion, value);
    }

    private string _machineName = Environment.MachineName;
    public string MachineName
    {
        get => _machineName;
        private set => this.RaiseAndSetIfChanged(ref _machineName, value);
    }

    private string _listeningPort = Unavailable;
    public string ListeningPort
    {
        get => _listeningPort;
        private set => this.RaiseAndSetIfChanged(ref _listeningPort, value);
    }
}
