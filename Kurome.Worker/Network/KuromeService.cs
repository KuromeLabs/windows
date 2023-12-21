using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Kurome.Network;

public class KuromeService : BackgroundService
{
    private readonly LinkProvider _linkProvider;
    private readonly IpcService _ipcService;

    public KuromeService(LinkProvider linkProvider, IpcService ipcService)
    {
        _linkProvider = linkProvider;
        _ipcService = ipcService;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tcpListenTask = _linkProvider.StartTcpListener(stoppingToken);
        var udpCastTask = _linkProvider.StartUdpCaster(stoppingToken);
        var ipcTask = _ipcService.StartAsync(stoppingToken);
        return Task.WhenAll(tcpListenTask, udpCastTask, ipcTask);
    }
}