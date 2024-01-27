using System;
using System.Buffers.Binary;
using System.IO.Pipes;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using Microsoft.Extensions.Logging;

namespace Kurome.Network;

public class IpcService
{
    private readonly DeviceService _deviceService;
    private readonly ILogger<IpcService> _logger;

    public IpcService(DeviceService deviceService, ILogger<IpcService> logger)
    {
        _deviceService = deviceService;
        _logger = logger;
    }

    private readonly NamedPipeServerStream _pipeServer = new("KuromePipe", PipeDirection.InOut, 1, 
        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObserveDevices(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_pipeServer.IsConnected)
            {
                await _pipeServer.WaitForConnectionAsync(cancellationToken);
                _logger.LogInformation($"Incoming pipe connection");
                SendActiveDevices(cancellationToken);
            }

            try
            {
                var buffer = new byte[4];
                await _pipeServer.ReadExactlyAsync(buffer, cancellationToken);
                var length = BinaryPrimitives.ReadInt32LittleEndian(buffer);
                buffer = new byte[length];
                await _pipeServer.ReadExactlyAsync(buffer, cancellationToken);
                _logger.LogInformation($"Incoming message: {Encoding.UTF8.GetString(buffer)}");
            }
            catch (Exception e)
            {
                _pipeServer.Disconnect();
                _logger.LogError(e, "Error while reading from pipe. If you closed the client, this is expected.");
                await Task.Delay(2000, cancellationToken);
            }
        }
    }

    private void SendActiveDevices(CancellationToken cancellationToken)
    {
        var devices = _deviceService.DeviceStates.Items;
        var json = JsonSerializer.Serialize(devices);
        _logger.LogInformation($"Sending active devices: {json}");
        Send(json, cancellationToken);
    }

    private void ObserveDevices(CancellationToken cancellationToken)
    {
        _deviceService.DeviceStates
            .Connect()
            .ObserveOn(Scheduler.Default)
            .Bind(out var list)
            .Subscribe(_ => Send(JsonSerializer.Serialize(list), cancellationToken));

        _deviceService.DeviceStates.Connect().WhenPropertyChanged(x => x.Status).Subscribe(v =>
        {
            _logger.LogInformation($"Device {v.Sender.Device.Name} changed status to {v.Value}");
            //TODO: send pair request to UI
        });
    }

    private async void Send(string message, CancellationToken cancellationToken)
    {
        try
        {
            if (!_pipeServer.IsConnected) return;
            var messageLength = Encoding.UTF8.GetByteCount(message);
            var buffer = new byte[4 + messageLength];
            Encoding.UTF8.GetBytes(message, buffer.AsSpan()[4..]);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan()[..4], messageLength);
            await _pipeServer.WriteAsync(buffer, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while writing to pipe");
        }
        
    }
    
}