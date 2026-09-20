using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FlatSharp;
using Microsoft.Extensions.Logging;
using Kurome.Fbs.Ipc;

namespace Kurome.Network;

public class IpcService
{
    private const string PipeName = "KuromePipe";

    private const int MaxPacketSize = 1024 * 1024;

    private readonly DeviceService _deviceService;
    private readonly NetworkService _networkService;
    private readonly ILogger<IpcService> _logger;

    private readonly object _lock = new();

    private NamedPipeServerStream? _pipeServer;

    public IpcService(DeviceService deviceService, NetworkService networkService, ILogger<IpcService> logger)
    {
        _deviceService = deviceService;
        _networkService = networkService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObserveDeviceEvents(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var server = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(cancellationToken);
                lock (_lock) _pipeServer = server;
                _logger.LogInformation("Desktop UI connected");

                try
                {
                    Send(new IpcPacket
                    {
                        Component = new DeviceStateList
                            { States = await _deviceService.GetCurrentDeviceStates() }
                    });
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Could not send the initial device list");
                }

                await ReadLoopAsync(server, cancellationToken);
                _logger.LogInformation("Desktop UI disconnected");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "IPC connection ended unexpectedly");
            }
            finally
            {
                lock (_lock)
                {
                    if (ReferenceEquals(_pipeServer, server)) _pipeServer = null;
                }

                await server.DisposeAsync();
            }
        }
    }

    private async Task ReadLoopAsync(PipeStream pipe, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        while (!cancellationToken.IsCancellationRequested && pipe.IsConnected)
        {
            await pipe.ReadExactlyAsync(lengthBuffer, cancellationToken);
            var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
            if (length is <= 0 or > MaxPacketSize)
                throw new InvalidDataException($"Refusing a {length} byte IPC packet");

            var buffer = new byte[length];
            await pipe.ReadExactlyAsync(buffer, cancellationToken);
            await ProcessIncomingIpcPacket(buffer);
        }
    }

    private void ObserveDeviceEvents(CancellationToken cancellationToken)
    {
        _deviceService
            .IpcEventStreamObservable
            .ObserveOn(NewThreadScheduler.Default)
            .Subscribe(Send, cancellationToken);
    }

    private async Task ProcessIncomingIpcPacket(byte[] message)
    {
        IpcPacket ipcPacket;
        try
        {
            ipcPacket = IpcPacket.Serializer.Parse(message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Discarding an unparseable IPC packet");
            return;
        }

        if (ipcPacket.Component == null) return;

        switch (ipcPacket.Component.Value.Kind)
        {
            case Component.ItemKind.PairEvent:
            {
                var pairEvent = ipcPacket.Component.Value.PairEvent;
                if (pairEvent.DeviceState?.Id == null) break;
                if (!Guid.TryParse(pairEvent.DeviceState.Id, out var id)) break;

                switch (pairEvent.Value)
                {
                    case PairEventType.PairRequestAccept:
                        _deviceService.OnIncomingPairRequestAccepted(id);
                        break;
                    case PairEventType.PairRequestReject:
                        _deviceService.OnIncomingPairRequestRejected(id);
                        break;
                    case PairEventType.Unpair:
                        await _deviceService.OnUnpairRequested(id);
                        break;
                }

                break;
            }
            case Component.ItemKind.DeviceStateListRequest:
            {
                var states = await _deviceService.GetCurrentDeviceStates();
                Send(new IpcPacket { Component = new DeviceStateList { States = states } });
                break;
            }
            case Component.ItemKind.ServiceInfoRequest:
            {
                Send(new IpcPacket { Component = BuildServiceInfo() });
                break;
            }
        }
    }

    private ServiceInfo BuildServiceInfo()
    {
        return new ServiceInfo
        {
            Version = AssemblyVersion(),
            MachineName = Environment.MachineName,
            TcpPort = _networkService.TcpListeningPort
        };
    }

    private static string AssemblyVersion()
    {
        var informational = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrEmpty(informational))
            return Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

        var plus = informational.IndexOf('+');
        return plus < 0 ? informational : informational[..plus];
    }

    private void Send(IpcPacket ipcPacket)
    {
        lock (_lock)
        {
            var pipe = _pipeServer;
            if (pipe is not { IsConnected: true }) return;

            var buffer = ArrayPool<byte>.Shared.Rent(4 + IpcPacket.Serializer.GetMaxSize(ipcPacket));
            try
            {
                var length = IpcPacket.Serializer.Write(buffer.AsSpan()[4..], ipcPacket);
                BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan()[..4], length);
                pipe.Write(buffer, 0, length + 4);
                pipe.Flush();
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error while writing to the pipe. If you closed the UI, this is expected.");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
