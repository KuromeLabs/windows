using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Application.Core;
using Application.Devices;
using Application.Interfaces;
using Domain;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kurome.Network;

public class UdpListenerWorker : BackgroundService
{
    private readonly ILogger<UdpListenerWorker> _logger;
    private readonly IMediator _mediator;
    private readonly IDeviceAccessorFactory _deviceAccessorFactory;

    public UdpListenerWorker(ILogger<UdpListenerWorker> logger, IMediator mediator,
        IDeviceAccessorFactory deviceAccessorFactory)
    {
        _logger = logger;
        _mediator = mediator;
        _deviceAccessorFactory = deviceAccessorFactory;
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

            var link = await _mediator.Send(new Connect.Query {Ip = message.Split(':')[1], Port = 33587},
                stoppingToken);

            var query = await _mediator.Send(new Get.Query {Id = Guid.Parse(message.Split(':')[3])}, stoppingToken);
            var device = query.Value;
            if (query.ResultStatus == Result<Device>.Status.Failure)
            {
                device = new Device
                {
                    Id = Guid.Parse(message.Split(':')[3]),
                    Name = message.Split(':')[2],
                };
            }

            var monitor = _deviceAccessorFactory.Create(link, device!);
            monitor.Start(stoppingToken);

            await _mediator.Send(new Mount.Command {Id = Guid.Parse(message.Split(':')[3])}, stoppingToken);
        }
    }
}