using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Kurome.Network;

public class KuromeService : BackgroundService
{
    private readonly NetworkService _networkService;
    private readonly IpcService _ipcService;

    public KuromeService(NetworkService networkService, IpcService ipcService)
    {
        _networkService = networkService;
        _ipcService = ipcService;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tcpListenTask = _networkService.StartTcpListener(stoppingToken);
        var udpCastTask = _networkService.StartUdpCaster(stoppingToken);
        var ipcTask = _ipcService.StartAsync(stoppingToken);
        return Task.WhenAll(tcpListenTask, udpCastTask, ipcTask);
    }
}