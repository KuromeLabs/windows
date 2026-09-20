using Kurome.Ui.ViewModels;
using Microsoft.Extensions.Hosting;

namespace Kurome.Ui.Services;

public class HostService : IHostedService
{
    private readonly MainWindow _window;
    private readonly PipeService _pipeService;
    private readonly AppThemeService _themeService;
    private readonly CancellationTokenSource _cts = new();

    public HostService(
        MainWindow window,
        PipeService pipeService,
        AppThemeService themeService,
        DeviceStore deviceStore,
        DialogViewModel dialogViewModel)
    {
        _window = window;
        _pipeService = pipeService;
        _themeService = themeService;
        _ = deviceStore;
        _ = dialogViewModel;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _themeService.Attach(_window);

        _ = Task.Run(() => _pipeService.RunAsync(_cts.Token), _cts.Token);

        _window.Show();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts.Cancel();
        _cts.Dispose();
        return Task.CompletedTask;
    }
}
