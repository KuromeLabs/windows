using System;
using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kurome.Core.Devices;
using Microsoft.Extensions.Logging;

namespace Kurome.Network;

public class IpcService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly ILogger<IpcService> _logger;

    public IpcService(IDeviceRepository deviceRepository, ILogger<IpcService> logger)
    {
        _deviceRepository = deviceRepository;
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
                Console.WriteLine("Received: " + Encoding.UTF8.GetString(buffer));
            }
            catch (Exception e)
            {
                _pipeServer.Disconnect();
                Console.WriteLine(e);
                await Task.Delay(2000, cancellationToken);
            }
        }
    }

    private async void SendActiveDevices(CancellationToken cancellationToken)
    {
        var devices = _deviceRepository.GetActiveDevices();
        var json = JsonSerializer.Serialize(devices);
        await Send(json, cancellationToken);
    }

    private void ObserveDevices(CancellationToken cancellationToken)
    {
        _deviceRepository.DeviceAdded += (_, _) => SendActiveDevices(cancellationToken);
        _deviceRepository.DeviceRemoved += (_, _) => SendActiveDevices(cancellationToken);
    }

    public async Task Send(string message, CancellationToken cancellationToken)
    {
        if (!_pipeServer.IsConnected) return;
        var messageLength = Encoding.UTF8.GetByteCount(message);
        var buffer = new byte[4 + messageLength];
        Encoding.UTF8.GetBytes(message, buffer.AsSpan()[4..]);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan()[..4], messageLength);
        await _pipeServer.WriteAsync(buffer, cancellationToken);
    }
    
}