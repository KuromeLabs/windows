using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kurome;

public class KuromeWorker : BackgroundService
{
    private readonly ILogger<KuromeWorker> _logger;

    public KuromeWorker(IServiceProvider serviceProvider, ILogger<KuromeWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(45000, stoppingToken);
        }
    }
}