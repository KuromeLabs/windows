using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kurome.Ui.Services;

public class HostService : IHostedService
{
    private readonly MainWindow _window;
    private readonly IServiceProvider _serviceProvider;

    public HostService(MainWindow window, IServiceProvider serviceProvider)
    {
        _window = window;
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
       _serviceProvider.GetRequiredService<PipeService>().StartAsync(cancellationToken);
        _window.Show();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}