using System.Buffers.Binary;
using System.IO.Pipes;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;
using DynamicData;
using Kurome.Core.Devices;
using Kurome.Network;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Kurome.Ui.Services;

public class PipeService : IHostedService
{


    private NamedPipeClientStream _pipeClient = new(".", "KuromePipe", PipeDirection.InOut,
        PipeOptions.Asynchronous);


    private readonly ILogger _logger = Log.ForContext<PipeService>();

    public readonly SourceList<DeviceState> ActiveDevices = new();
    
    private readonly Subject<DeviceState?> _pairingDeviceState = new();
    public IObservable<DeviceState?> PairingDeviceState => _pairingDeviceState.AsObservable();

    public PipeService()
    {
    }
    
    public void AcceptPairingRequest(DeviceState deviceState)
    {
        var ipcPacket = new IpcPacket
        {
            PacketType = IpcPacket.Type.AcceptIncomingPairRequest,
            Data = JsonSerializer.Serialize(deviceState)
        };
        Send(ipcPacket, CancellationToken.None);
    }

    private async void Send(IpcPacket ipcPacket, CancellationToken cancellationToken)
    {
        try
        {
            if (!_pipeClient.IsConnected) return;
            var payload = JsonSerializer.Serialize(ipcPacket);
            var messageLength = Encoding.UTF8.GetByteCount(payload);
            var buffer = new byte[4 + messageLength];
            Encoding.UTF8.GetBytes(payload, buffer.AsSpan()[4..]);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan()[..4], messageLength);
            await _pipeClient.WriteAsync(buffer, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error while writing to pipe");
        }
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
                    var msg = Encoding.UTF8.GetString(buffer);
                    _logger.Information($"Received: {msg}");
                    ProcessMessage(msg);
                }
                catch (Exception e)
                {
                    await _pipeClient.DisposeAsync();
                    ActiveDevices.Clear();
                    _logger.Error(e, "Error while reading from pipe");
                }
            }
        }, stoppingToken);

        return Task.CompletedTask;
    }

    private void ProcessMessage(string message)
    {
        var ipcPacket = JsonSerializer.Deserialize<IpcPacket>(message);
        switch (ipcPacket!.PacketType)
        {
            case IpcPacket.Type.DeviceList:
                var devices = JsonSerializer.Deserialize<DeviceState[]>(ipcPacket.Data);
                ActiveDevices.Edit(action =>
                {
                    action.Clear();
                    action.AddRange(devices!);
                });
                break;
            case IpcPacket.Type.IncomingPairRequest:
            {
                var deviceState = JsonSerializer.Deserialize<DeviceState>(ipcPacket.Data);
                _pairingDeviceState.OnNext(deviceState);
                break;
            }
            case IpcPacket.Type.CancelIncomingPairRequest:
                _pairingDeviceState.OnNext(null);
                break;
        }
        
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}