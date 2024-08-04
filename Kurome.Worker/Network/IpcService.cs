using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipes;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlatSharp;
using Microsoft.Extensions.Logging;
using Kurome.Fbs.Ipc;

namespace Kurome.Network;

public class IpcService
{
    private readonly DeviceService _deviceService;
    private readonly ILogger<IpcService> _logger;
    private readonly object _lock = new();

    public IpcService(DeviceService deviceService, ILogger<IpcService> logger)
    {
        _deviceService = deviceService;
        _logger = logger;
    }

    private readonly NamedPipeServerStream _pipeServer = new("KuromePipe", PipeDirection.InOut, 1,
        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObserveDeviceEvents(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_pipeServer.IsConnected)
            {
                await _pipeServer.WaitForConnectionAsync(cancellationToken);
                _logger.LogInformation($"Incoming pipe connection");
            }

            try
            {
                var buffer = new byte[4];
                await _pipeServer.ReadExactlyAsync(buffer, cancellationToken);
                var length = BinaryPrimitives.ReadInt32LittleEndian(buffer);
                buffer = new byte[length];
                await _pipeServer.ReadExactlyAsync(buffer, cancellationToken);
                ProcessIncomingIpcPacket(buffer);
            }
            catch (Exception e)
            {
                _pipeServer.Disconnect();
                _logger.LogError(e, "Error while reading from pipe. If you closed the client, this is expected.");
                await Task.Delay(2000, cancellationToken);
            }
        }
    }

    private void ObserveDeviceEvents(CancellationToken cancellationToken)
    {
        _deviceService
            .IpcEventStreamObservable
            .ObserveOn(NewThreadScheduler.Default)
            .Subscribe(ipcPacket =>
            {
                if (_pipeServer.IsConnected) Send(ipcPacket);
            }, cancellationToken);
    }

    private void ProcessIncomingIpcPacket(byte[] message)
    {
        var ipcPacket = IpcPacket.Serializer.Parse(message);
        if (ipcPacket.Component == null) return;

        switch (ipcPacket.Component.Value.Kind)
        {
            case Component.ItemKind.PairEvent:
            {
                switch (ipcPacket.Component.Value.PairEvent.Value)
                {
                    case PairEventType.PairRequestAccept:
                    {
                        _deviceService.OnIncomingPairRequestAccepted(
                            Guid.Parse(ipcPacket.Component.Value.PairEvent.DeviceState!.Id!));
                        break;
                    }
                    case PairEventType.PairRequestReject:
                        _deviceService.OnIncomingPairRequestRejected(
                            Guid.Parse(ipcPacket.Component.Value.PairEvent.DeviceState!.Id!));
                        break;
                }

                break;
            }
            case Component.ItemKind.DeviceStateListRequest:
            {
                var packet = new IpcPacket
                    { Component = new DeviceStateList { States = _deviceService.GetCurrentDeviceStates() } };
                Send(packet);
                break;
            }
        }
    }

    private void Send(IpcPacket ipcPacket)
    {
        lock (_lock)
            try
            {
                var size = IpcPacket.Serializer.GetMaxSize(ipcPacket);
                var buffer = ArrayPool<byte>.Shared.Rent(4 + size);
                var length = IpcPacket.Serializer.Write(buffer.AsSpan()[4..], ipcPacket);
                BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan()[..4], length);
                _pipeServer.Write(buffer, 0, length + 4);
                ArrayPool<byte>.Shared.Return(buffer);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while writing to pipe. If you closed the client, this is expected.");
            }
    }
}