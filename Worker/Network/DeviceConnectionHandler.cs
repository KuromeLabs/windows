using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Application.Core;
using Application.Devices;
using Application.Interfaces;
using FlatSharp;
using kurome;
using MediatR;
using Microsoft.Extensions.Logging;
using Monitor = Application.Devices.Monitor;

namespace Kurome.Network;

public class DeviceConnectionHandler
{
    private readonly ILogger<DeviceConnectionHandler> _logger;
    private readonly IMediator _mediator;

    public DeviceConnectionHandler(ILogger<DeviceConnectionHandler> logger, IMediator mediator)
    {
        _logger = logger;
        _mediator = mediator;
    }

    public async void HandleClientConnection(string name, Guid id, string ip, int port,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new Connect.Query {Ip = ip, Port = port}, cancellationToken);

        if (result.ResultStatus == Result<ILink>.Status.Success)
            await _mediator.Send(new Monitor.Query {Id = id, Link = result.Value!, Name = name},
                cancellationToken);

        var mountResult = await _mediator.Send(new Mount.Command {Id = id}, cancellationToken);

        if (mountResult.ResultStatus == Result<Unit>.Status.Failure)
            _logger.LogError("{Error}", mountResult.Error);
    }

    public async void HandleServerConnection(TcpClient client, CancellationToken cancellationToken)
    {
        var info = await ReadIdentityAsync(client, cancellationToken);
        if (info == null)
        {
            _logger.LogError("Failed to read device identity from incoming connection");
            return;
        }

        var id = Guid.Parse(info.Id!);

        var result = await _mediator.Send(new AcceptConnection.Query {TcpClient = client}, cancellationToken);

        if (result.ResultStatus == Result<ILink>.Status.Success)
            await _mediator.Send(new Monitor.Query {Id = id, Link = result.Value!, Name = info.Name!},
                cancellationToken);

        var mountResult = await _mediator.Send(new Mount.Command {Id = id}, cancellationToken);

        if (mountResult.ResultStatus == Result<Unit>.Status.Failure)
            _logger.LogError("{Error}", mountResult.Error);
    }


    private async Task<DeviceInfo?> ReadIdentityAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var sizeBuffer = new byte[4];
        var bytesRead = 0;
        try
        {
            int current;
            while (bytesRead != 4)
            {
                current = await client.GetStream()
                    .ReadAsync(sizeBuffer.AsMemory(0 + bytesRead, 4 - bytesRead), cancellationToken);
                bytesRead += current;
                if (current != 0) continue;
                return null;
            }

            var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
            bytesRead = 0;
            var readBuffer = ArrayPool<byte>.Shared.Rent(size);
            while (bytesRead != size)
            {
                current = await client.GetStream()
                    .ReadAsync(readBuffer.AsMemory(0 + bytesRead, size - bytesRead), cancellationToken);
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
            _logger.LogError("{@Exception}", e.ToString());
            return null;
        }
    }
}