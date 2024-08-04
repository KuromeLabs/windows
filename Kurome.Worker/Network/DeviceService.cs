using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using FlatSharp;
using Kurome.Core.Devices;
using Kurome.Core.Interfaces;
using Kurome.Core.Network;
using Kurome.Fbs.Device;
using Kurome.Fbs.Ipc;
using Microsoft.Extensions.Logging;

namespace Kurome.Network;

public class DeviceService(
    ILogger<DeviceService> logger,
    ISecurityService<X509Certificate2> sslService,
    IDeviceRepository deviceRepository
)
{
    private readonly ConcurrentDictionary<Guid, DeviceHandle> _deviceHandles = new();
    private readonly Subject<IpcPacket> _ipcEventStream = new();
    public IObservable<IpcPacket> IpcEventStreamObservable => _ipcEventStream.AsObservable();


    public void HandleIncomingTcp(TcpClient client, CancellationToken cancellationToken)
    {
        var info = ReadIdentity(client);
        if (info == null)
        {
            logger.LogError("Failed to read device identity from incoming connection");
            return;
        }

        var id = info.Item1;
        var name = info.Item2;
        logger.LogInformation("Checking existing active devices");
        var hasExistingHandler = _deviceHandles.TryRemove(id, out var existingDeviceHandle);
        if (hasExistingHandler)
        {
            logger.LogInformation("Device {Name} ({Id}) is already active, disconnecting", name, id);
            existingDeviceHandle!.Dispose();
            _ipcEventStream.OnNext(new IpcPacket { Component = existingDeviceHandle.ToDeviceState() });
        }

        var device = deviceRepository.GetSavedDevice(id).Result;
        var deviceTrusted = false;
        X509Certificate2? activeCertificate = null;
        Link? link;
        try
        {
            var stream = new SslStream(client.GetStream(), false, (sender, certificate, chain, policyErrors) =>
            {
                if (policyErrors == SslPolicyErrors.RemoteCertificateNameMismatch) return false;
                if (certificate == null) return false;
                if (device == null)
                {
                    device = new Device(id, name, (X509Certificate2)certificate);
                    activeCertificate = (X509Certificate2)certificate;
                    return true;
                }

                if (!certificate.Equals(device.Certificate)) return false;
                deviceTrusted = true;
                return true;
            });
            stream.AuthenticateAsServer(sslService.GetSecurityContext(), true, SslProtocols.None, true);
            link = new Link(stream);
        }
        catch (Exception e)
        {
            logger.LogError($"{e}");
            return;
        }

        logger.LogInformation("Link established with {Name} ({Id})", info.Item2, id);
        var deviceHandle = new DeviceHandle(link, id, name, deviceTrusted, activeCertificate!);
        _ipcEventStream.OnNext(new IpcPacket { Component = deviceHandle.ToDeviceState() });
        _deviceHandles.TryAdd(id, deviceHandle);

        link.DataReceived
            .Where(x => Packet.Serializer.Parse(x.Data).Component?.Kind == Kurome.Fbs.Device.Component.ItemKind.Pair)
            .ObserveOn(NewThreadScheduler.Default)
            .Subscribe(buffer =>
            {
                var pair = Packet.Serializer.Parse(buffer.Data).Component?.Pair!;
                HandleIncomingPairPacket(pair, deviceHandle);
            }, _ => deviceHandle.Dispose(), () => deviceHandle.Dispose(), cancellationToken);
        new Thread(() => link.Start(cancellationToken))
        {
            IsBackground = true,
        }.Start();
        if (deviceTrusted)
            deviceHandle.MountToAvailableMountPoint();
    }


    private void HandleIncomingPairPacket(Pair pair, DeviceHandle deviceHandle)
    {
        if (pair.Value)
        {
            switch (deviceHandle.PairState)
            {
                case PairState.Paired:
                    //pair request but we are already paired, ignore
                    break;
                case PairState.PairRequested:
                    //we requested pair and it's accepted
                    break;
                case PairState.Unpaired:
                    //incoming pair request from peer
                    deviceHandle.PairState = PairState.PairRequestedByPeer;
                    deviceHandle.IncomingPairTimer?.Dispose();
                    deviceHandle.IncomingPairTimer = new Timer(t =>
                    {
                        logger.LogInformation("Pair request timed out for {Id}", deviceHandle.Id);
                        if (deviceHandle.PairState != PairState.PairRequestedByPeer) return;
                        deviceHandle.PairState = PairState.Unpaired;
                        SendIpcPairEvent(PairEventType.PairRequestCancel, deviceHandle);
                        deviceHandle.IncomingPairTimer?.Dispose();
                    }, null, 25000, Timeout.Infinite);
                    SendIpcPairEvent(PairEventType.PairRequest, deviceHandle);
                    break;
            }
        }
        else
        {
            switch (deviceHandle.PairState)
            {
                case PairState.Paired:
                    //unpair request
                    break;
                case PairState.PairRequested:
                    //we requested pair and it's rejected
                    break;
            }
        }
    }


    private Tuple<Guid, string>? ReadIdentity(TcpClient client)
    {
        var sizeBuffer = new byte[4];
        try
        {
            client.GetStream().ReadExactly(sizeBuffer, 0, 4);
            var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
            var readBuffer = ArrayPool<byte>.Shared.Rent(size);
            client.GetStream().ReadExactly(readBuffer, 0, size);
            var info = Packet.Serializer.Parse(readBuffer).Component?.DeviceIdentityResponse;
            ArrayPool<byte>.Shared.Return(readBuffer);
            return new Tuple<Guid, string>(Guid.Parse(info!.Id!), info.Name!);
        }
        catch (Exception e)
        {
            logger.LogError("{@Exception}", e.ToString());
            return null;
        }
    }

    private void SendIpcPairEvent(PairEventType type, DeviceHandle handle)
    {
        _ipcEventStream.OnNext(new IpcPacket
        {
            Component = new PairEvent { DeviceState = handle.ToDeviceState(), Value = type }
        });
    }

    public void OnIncomingPairRequestRejected(Guid id)
    {
        if (_deviceHandles.TryGetValue(id, out var deviceHandle))
        {
            deviceHandle.IncomingPairTimer?.Dispose();
            if (deviceHandle.PairState != PairState.PairRequestedByPeer) return;
            deviceHandle.PairState = PairState.Unpaired;
            _ipcEventStream.OnNext(new IpcPacket { Component = deviceHandle.ToDeviceState() });
            var packet = new Packet
                { Component = new Kurome.Fbs.Device.Component(new Pair { Value = false }), Id = -1 };
            var maxSize = Packet.Serializer.GetMaxSize(packet);
            var buffer = ArrayPool<byte>.Shared.Rent(maxSize + 4);
            var span = buffer.AsSpan();
            var length = Packet.Serializer.Write(span[4..], packet);
            BinaryPrimitives.WriteInt32LittleEndian(span[..4], length);
            deviceHandle.Link.Send(buffer, length + 4);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void OnIncomingPairRequestAccepted(Guid id)
    {
        if (_deviceHandles.TryGetValue(id, out var deviceHandle))
        {
            if (deviceHandle.PairState != PairState.PairRequestedByPeer) return;
            deviceHandle.PairState = PairState.Paired;
            _ipcEventStream.OnNext(new IpcPacket { Component = deviceHandle.ToDeviceState() });
            deviceRepository.SaveDevice(new Device(id, deviceHandle.Name, deviceHandle.Certificate));
            var packet = new Packet { Component = new Kurome.Fbs.Device.Component(new Pair { Value = true }), Id = -1 };
            var maxSize = Packet.Serializer.GetMaxSize(packet);
            var buffer = ArrayPool<byte>.Shared.Rent(maxSize + 4);
            var span = buffer.AsSpan();
            var length = Packet.Serializer.Write(span[4..], packet);
            BinaryPrimitives.WriteInt32LittleEndian(span[..4], length);
            deviceHandle.Link.Send(buffer, length + 4);
            ArrayPool<byte>.Shared.Return(buffer);
            deviceHandle.MountToAvailableMountPoint();
        }
    }

    public IList<DeviceState> GetCurrentDeviceStates()
    {
        return _deviceHandles.Select(x => x.Value.ToDeviceState()).ToList();
    }
}