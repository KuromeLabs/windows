using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Application.Devices;
using Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monitor = Application.Devices.Monitor;

namespace Kurome.Network;

public class UdpListenerWorker : BackgroundService
{
    private readonly ILogger<UdpListenerWorker> _logger;
    private readonly IMediator _mediator;

    public UdpListenerWorker(ILogger<UdpListenerWorker> logger, IMediator mediator)
    {
        _logger = logger;
        _mediator = mediator;
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
            var id = Guid.Parse(message.Split(':')[3]);
            var name = message.Split(':')[2];
            var link = await _mediator.Send(new Connect.Query {Ip = message.Split(':')[1], Port = 33587},
                stoppingToken);
            
            await _mediator.Send(new Monitor.Query {Id = id, Link = link, Name = name}, stoppingToken);

            await _mediator.Send(new Mount.Command {Id = id}, stoppingToken);
        }
    }
}