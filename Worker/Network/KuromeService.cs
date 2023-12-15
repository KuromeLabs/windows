using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Kurome.Network;

public class KuromeService : BackgroundService
{
    private readonly LinkProvider _linkProvider;

    public KuromeService(LinkProvider linkProvider)
    {
        _linkProvider = linkProvider;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tcpListenTask = _linkProvider.StartTcpListener(stoppingToken);
        var udpCastTask = _linkProvider.StartUdpCaster(stoppingToken);
        return Task.WhenAll(tcpListenTask, udpCastTask);
    }
}