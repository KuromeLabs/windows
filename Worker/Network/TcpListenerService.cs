using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kurome.Network;

public class TcpListenerService : BackgroundService
{
    private readonly ILogger<TcpListenerService> _logger;
    private readonly DeviceConnectionHandler _handler;

    public TcpListenerService(ILogger<TcpListenerService> logger, DeviceConnectionHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tcpListener = TcpListener.Create(33587);
        tcpListener.Start();
        _logger.LogInformation("Started TCP Listener on port {Port}", 33587);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var client = await tcpListener.AcceptTcpClientAsync(stoppingToken);
                _logger.LogInformation("Accepted connection from {Ip}", (client.Client.RemoteEndPoint as IPEndPoint));
                _handler.HandleServerConnection(client, stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogDebug("Exception at StartTcpListener: {@Exception}", e.ToString());
            }
        }
    }
}