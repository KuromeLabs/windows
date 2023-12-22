using Microsoft.Extensions.Hosting;

namespace Kurome.Ui.Services;

public class HostService : IHostedService
{
    private readonly MainWindow _window;

    public HostService(MainWindow window)
    {
        _window = window;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _window.Show();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}