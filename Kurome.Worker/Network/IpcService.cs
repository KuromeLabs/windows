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
                var msg = Encoding.UTF8.GetString(buffer);
                ProcessMessage(msg);
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
        var devicesJson = JsonSerializer.Serialize(devices);
        _logger.LogInformation($"Sending active devices: {devicesJson}");
        var json = new IpcPacket { PacketType = IpcPacket.Type.DeviceList, Data = devicesJson };
        Send(json, cancellationToken);
    }

    private void ObserveDevices(CancellationToken cancellationToken)
    {
        _deviceService.DeviceStates
            .Connect()
            .ObserveOn(Scheduler.Default)
            .Bind(out var list)
            .Subscribe(_ =>
            {
                var data = JsonSerializer.Serialize(list);
                var ipcPacket = new IpcPacket { PacketType = IpcPacket.Type.DeviceList, Data = data };
                Send(ipcPacket, cancellationToken);
            }, cancellationToken);
        
        _deviceService.DeviceStates.Connect().WhenPropertyChanged(x => x.State)
            .ObserveOn(Scheduler.Default)
            .Subscribe(v =>
            {
                _logger.LogInformation($"Device {v.Sender.Device.Name} changed status to {v.Value}");
                switch (v.Value)
                {
                    case DeviceState.PairState.PairRequestedByPeer:
                    {
                        var pairRequestPacket = new IpcPacket
                        {
                            PacketType = IpcPacket.Type.IncomingPairRequest,
                            Data = JsonSerializer.Serialize(v.Sender)
                        };
                        Send(pairRequestPacket, cancellationToken);
                        break;
                    }
                    case DeviceState.PairState.Unpaired:
                        var cancelPairRequestPacket = new IpcPacket
                        {
                            PacketType = IpcPacket.Type.CancelIncomingPairRequest,
                            Data = JsonSerializer.Serialize(v.Sender)
                        };
                        Send(cancelPairRequestPacket, cancellationToken);
                        break;
                }
            });
    }
    
    private void ProcessMessage(string message)
    {
        var ipcPacket = JsonSerializer.Deserialize<IpcPacket>(message);
        switch (ipcPacket!.PacketType)
        {
            case IpcPacket.Type.AcceptIncomingPairRequest:
            {
                var deviceState = JsonSerializer.Deserialize<DeviceState>(ipcPacket.Data);
                _deviceService.OnIncomingPairRequestAccepted(deviceState!.Device.Id);
                break;
            }
            case IpcPacket.Type.RejectIncomingPairRequest:
                var deviceState1 = JsonSerializer.Deserialize<DeviceState>(ipcPacket.Data);
                _deviceService.OnIncomingPairRequestRejected(deviceState1!.Device.Id);
                break;
        }
        
    }

    private async void Send(IpcPacket ipcPacket, CancellationToken cancellationToken)
    {
        try
        {
            if (!_pipeServer.IsConnected) return;
            var payload = JsonSerializer.Serialize(ipcPacket);
            var messageLength = Encoding.UTF8.GetByteCount(payload);
            var buffer = new byte[4 + messageLength];
            Encoding.UTF8.GetBytes(payload, buffer.AsSpan()[4..]);
            BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan()[..4], messageLength);
            await _pipeServer.WriteAsync(buffer, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while writing to pipe");
        }
        
    }
    
    
}

public class IpcPacket
{
    public Type PacketType { get; set; }
    public string Data { get; set; } = string.Empty;
    public enum Type
    {
        DeviceList,
        IncomingPairRequest,
        CancelIncomingPairRequest,
        AcceptIncomingPairRequest,
        RejectIncomingPairRequest
    }
}