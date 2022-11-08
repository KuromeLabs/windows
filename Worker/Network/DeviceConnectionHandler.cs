using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Application.Core;
using Application.Devices;
using Application.flatbuffers;
using Application.Interfaces;
using Application.MediatorExtensions;
using FlatSharp;
using Kurome.Fbs;
using MessagePipe;
using Microsoft.Extensions.Logging;
using Monitor = Application.Devices.Monitor;

namespace Kurome.Network;

public class DeviceConnectionHandler
{
    private readonly ILogger<DeviceConnectionHandler> _logger;
    private readonly FlatBufferHelper _flatBufferHelper;
    private readonly IAsyncRequestHandler<AcceptConnection.Query, Result<ILink>> _acceptConnection;
    private readonly IAsyncRequestHandler<Monitor.Query, Result<IDeviceAccessor>> _monitor;
    private readonly IAsyncRequestHandler<Connect.Query, Result<ILink>> _connect;
    private readonly IAsyncRequestHandler<Mount.Command, Result<Unit>> _mount;


    public DeviceConnectionHandler(ILogger<DeviceConnectionHandler> logger, FlatBufferHelper flatBufferHelper,
        IAsyncRequestHandler<AcceptConnection.Query, Result<ILink>> acceptConnection,
        IAsyncRequestHandler<Monitor.Query, Result<IDeviceAccessor>> monitor,
        IAsyncRequestHandler<Connect.Query, Result<ILink>> connect,
        IAsyncRequestHandler<Mount.Command, Result<Unit>> mount)
    {
        _logger = logger;
        _flatBufferHelper = flatBufferHelper;
        _acceptConnection = acceptConnection;
        _monitor = monitor;
        _connect = connect;
        _mount = mount;
    }

    public async void HandleClientConnection(string name, Guid id, string ip, int port,
        CancellationToken cancellationToken)
    {
        var result = await _connect.InvokeAsync(new Connect.Query(ip, port), cancellationToken);

        if (result.ResultStatus == Result<ILink>.Status.Success)
            await _monitor.InvokeAsync(new Monitor.Query(name, id, result.Value!), cancellationToken);

        var mountResult = await _mount.InvokeAsync(new Mount.Command(id: id), cancellationToken);

        if (mountResult.ResultStatus == Result<Unit>.Status.Failure)
            _logger.LogError("{Error}", mountResult.Error);
    }

    public async void HandleServerConnection(TcpClient client, CancellationToken cancellationToken)
    {
        var info = (await ReadIdentityAsync(client, cancellationToken));
        if (info == null)
        {
            _logger.LogError("Failed to read device identity from incoming connection");
            return;
        }

        var id = info.Item1;

        // var result = await _mediator.Send(new AcceptConnection.Query { TcpClient = client }, cancellationToken);
        var result = await _acceptConnection.InvokeAsync(new AcceptConnection.Query(client), cancellationToken);
        if (result.ResultStatus == Result<ILink>.Status.Success)
            await _monitor.InvokeAsync(new Monitor.Query(info.Item2, id, result.Value!),
                cancellationToken);
        else
            _logger.LogError("{Error}",result.Error);

        var mountResult = await _mount.InvokeAsync(new Mount.Command(id: id), cancellationToken);

        if (mountResult.ResultStatus == Result<Unit>.Status.Failure)
            _logger.LogError("{Error}", mountResult.Error);
    }


    private async Task<Tuple<Guid, string>?> ReadIdentityAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var sizeBuffer = new byte[4];
        try
        {
            await client.GetStream().ReadExactlyAsync(sizeBuffer, 0, 4, cancellationToken);
            var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
            var readBuffer = ArrayPool<byte>.Shared.Rent(size);
            await client.GetStream().ReadExactlyAsync(readBuffer, 0, size, cancellationToken);
            _flatBufferHelper.TryGetDeviceInfo(Packet.Serializer.Parse(readBuffer), out var info);
            var details = info!.Details!;
            ArrayPool<byte>.Shared.Return(readBuffer);
            return new Tuple<Guid, string>(Guid.Parse(details.Id!), details.Name!);
        }
        catch (Exception e)
        {
            _logger.LogError("{@Exception}", e.ToString());
            return null;
        }
    }
}