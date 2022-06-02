using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Kurome.Network;

public class TcpListenerService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly DeviceConnectionHandler _handler;

    public TcpListenerService(ILogger logger, DeviceConnectionHandler handler)
    {
        _logger = logger;
        _handler = handler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tcpListener = TcpListener.Create(33587);
        tcpListener.Start();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var client = await tcpListener.AcceptTcpClientAsync(stoppingToken);
                _logger.Information("Accepted connection from {Ip}", (client.Client.RemoteEndPoint as IPEndPoint));
                _handler.HandleServerConnection(client, stoppingToken);
            }
            catch (Exception e)
            {
                _logger.Debug("Exception at StartTcpListener: {@Exception}", e.ToString());
            }
        }
    }
}