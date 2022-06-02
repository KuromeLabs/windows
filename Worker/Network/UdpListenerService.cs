using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kurome.Network;

public class UdpListenerService : BackgroundService
{
    private readonly ILogger<UdpListenerService> _logger;
    private readonly DeviceConnectionHandler _handler;

    public UdpListenerService(ILogger<UdpListenerService> logger, DeviceConnectionHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting UDP Listener");
        var udpSocket = new UdpClient(33588);

        while (!stoppingToken.IsCancellationRequested)
        {
            var receivedBytes = (await udpSocket.ReceiveAsync(CancellationToken.None)).Buffer;
            var message = Encoding.Default.GetString(receivedBytes);
            _logger.LogInformation("Received UDP: {Message}", message);
            var ip = message.Split(':')[1];
            var id = Guid.Parse(message.Split(':')[3]);
            var name = message.Split(':')[2];
            
            _handler.HandleClientConnection(name, id, ip, 33587, stoppingToken);
        }
    }
}