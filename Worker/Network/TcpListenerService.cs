using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Application.Devices;
using FlatSharp;
using kurome;
using MediatR;
using Microsoft.Extensions.Hosting;
using Serilog;
using Monitor = Application.Devices.Monitor;

namespace Kurome.Network;

public class TcpListenerService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly IMediator _mediator;

    public TcpListenerService(ILogger logger, IMediator mediator)
    {
        _logger = logger;
        _mediator = mediator;
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
                var info = await ReadIdentityAsync(client, stoppingToken);
                var link = await _mediator.Send(new AcceptConnection.Query { TcpClient = client }, stoppingToken);
                await _mediator.Send(new Monitor.Query {Id = Guid.Parse(info.Id!), Link = link, Name = info.Name!}, stoppingToken);
                await _mediator.Send(new Mount.Command {Id = Guid.Parse(info.Id!)}, stoppingToken);
            }
            catch (Exception e)
            {
                _logger.Information("Exception at StartTcpListener: {@Exception}", e.ToString());
            }
        }
    }
    
    private async Task<DeviceInfo> ReadIdentityAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var sizeBuffer = new byte[4];
        var bytesRead = 0;
        try
        {
            int current;
            while (bytesRead != 4)
            {
                current = await client.GetStream().ReadAsync(sizeBuffer.AsMemory(0 + bytesRead, 4 - bytesRead), cancellationToken);
                bytesRead += current;
                if (current != 0) continue;
                return null;
            }

            var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
            bytesRead = 0;
            var readBuffer = ArrayPool<byte>.Shared.Rent(size);
            while (bytesRead != size)
            {
                current = await client.GetStream().ReadAsync(readBuffer.AsMemory(0 + bytesRead, size - bytesRead), cancellationToken);
                bytesRead += current;
                if (current != 0) continue;
                return null;
            }
            var info = new DeviceInfo(Packet.Serializer.Parse(readBuffer).DeviceInfo!);
            ArrayPool<byte>.Shared.Return(readBuffer);
            return info;
        }
        catch (Exception e)
        {
            Log.Error("{@Exception}", e.ToString());
            return null;
        }
    }
}