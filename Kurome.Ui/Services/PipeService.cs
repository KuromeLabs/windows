using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipes;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using FlatSharp;
using Kurome.Fbs.Ipc;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Kurome.Ui.Services;

public class PipeService : IHostedService
{
    private NamedPipeClientStream _pipeClient = new(".", "KuromePipe", PipeDirection.InOut,
        PipeOptions.Asynchronous);


    private readonly ILogger _logger = Log.ForContext<PipeService>();
    private readonly Subject<IpcPacket> _ipcEventStream = new();
    private readonly object _lock = new();
    public IObservable<IpcPacket> IpcEventStreamObservable => _ipcEventStream.AsObservable();

    public void AcceptPairingRequest(DeviceState deviceState)
    {
        var ipcPacket = new IpcPacket
        {
            Component = new PairEvent { Value = PairEventType.PairRequestAccept, DeviceState = deviceState },
        };
        Send(ipcPacket);
    }

    public void RejectPairingRequest(DeviceState deviceState)
    {
        var ipcPacket = new IpcPacket
        {
            Component = new PairEvent { Value = PairEventType.PairRequestReject, DeviceState = deviceState },
        };
        Send(ipcPacket);
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
                _pipeClient.Write(buffer, 0, length + 4);
                ArrayPool<byte>.Shared.Return(buffer);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error while writing to pipe. If you closed the client, this is expected.");
            }
    }

    public void RequestDeviceStateList()
    {
        _logger.Information("Requesting Device State List");
        var packet = new IpcPacket { Component = new DeviceStateListRequest() };
        Send(packet);
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!_pipeClient.IsConnected)
                    {
                        _pipeClient = new NamedPipeClientStream(".", "KuromePipe", PipeDirection.InOut,
                            PipeOptions.Asynchronous);
                        await _pipeClient.ConnectAsync(stoppingToken);
                    }

                    var buffer = new byte[4];
                    await _pipeClient.ReadExactlyAsync(buffer, stoppingToken);
                    var length = BinaryPrimitives.ReadInt32LittleEndian(buffer);
                    buffer = new byte[length];
                    await _pipeClient.ReadExactlyAsync(buffer, stoppingToken);
                    var packet = IpcPacket.Serializer.Parse(buffer);
                    ProcessIncomingIpcPacket(packet);
                }
                catch (Exception e)
                {
                    await _pipeClient.DisposeAsync();
                    _logger.Error(e, "Error while reading from pipe");
                }
            }
        }, stoppingToken);

        return Task.CompletedTask;
    }

    private void ProcessIncomingIpcPacket(IpcPacket packet)
    {
        _ipcEventStream.OnNext(packet);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}