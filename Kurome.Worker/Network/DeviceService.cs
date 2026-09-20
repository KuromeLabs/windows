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
using System.Threading.Tasks;
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
    IDeviceRepository deviceRepository,
    IIdentityProvider identityProvider
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

                var peerCertificate = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());

                if (device == null)
                {
                    device = new Device(id, name, peerCertificate);
                    activeCertificate = peerCertificate;
                    return true;
                }

                if (device.Certificate == null ||
                    !peerCertificate.GetRawCertData().SequenceEqual(device.Certificate.GetRawCertData()))
                    return false;

                deviceTrusted = true;
                activeCertificate = peerCertificate;
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
        var deviceHandle = new DeviceHandle(link, id, name, deviceTrusted, activeCertificate!, identityProvider);
        _ipcEventStream.OnNext(new IpcPacket { Component = deviceHandle.ToDeviceState() });
        _deviceHandles.TryAdd(id, deviceHandle);

        link.DataReceived
            .Where(x => Packet.Serializer.Parse(x.Data).Component?.Kind == Fbs.Device.Component.ItemKind.Pair)
            .ObserveOn(NewThreadScheduler.Default)
            .Subscribe(buffer =>
            {
                var pair = Packet.Serializer.Parse(buffer.Data).Component?.Pair!;
                HandleIncomingPairPacket(pair, deviceHandle);
            }, e =>
            {
                logger.LogDebug("Error in DataReceived subscriber: {0}", e);
                OnDeviceDisconnected(id, deviceHandle);
            }, () => OnDeviceDisconnected(id, deviceHandle), cancellationToken);
        deviceHandle.SubscribePlugins();
        new Thread(() => link.Start(cancellationToken))
        {
            IsBackground = true,
        }.Start();
        deviceHandle.ActivatePlugins();
    }

    private void HandleIncomingPairPacket(Pair pair, DeviceHandle deviceHandle)
    {
        if (pair.Value)
        {
            switch (deviceHandle.PairState)
            {
                case PairState.PairRequested:
                    //we requested pair and it's accepted
                    break;
                case PairState.Paired:
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
                    logger.LogInformation("Peer {Id} unpaired us", deviceHandle.Id);
                    deviceHandle.PairState = PairState.Unpaired;
                    deviceRepository.DeleteDevice(deviceHandle.Id).Wait();
                    deviceHandle.ReloadPlugins();
                    _ipcEventStream.OnNext(new IpcPacket { Component = deviceHandle.ToDeviceState() });
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

    private void SendPairPacket(DeviceHandle deviceHandle, bool value)
    {
        var packet = new Packet
            { Component = new Kurome.Fbs.Device.Component(new Pair { Value = value }), Id = -126 };
        var maxSize = Packet.Serializer.GetMaxSize(packet);
        var buffer = ArrayPool<byte>.Shared.Rent(maxSize + 4);
        try
        {
            var span = buffer.AsSpan();
            var length = Packet.Serializer.Write(span[4..], packet);
            BinaryPrimitives.WriteInt32LittleEndian(span[..4], length);
            deviceHandle.Link.Send(buffer, length + 4);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public void OnIncomingPairRequestRejected(Guid id)
    {
        if (!_deviceHandles.TryGetValue(id, out var deviceHandle)) return;
        deviceHandle.IncomingPairTimer?.Dispose();
        if (deviceHandle.PairState != PairState.PairRequestedByPeer) return;
        deviceHandle.PairState = PairState.Unpaired;
        _ipcEventStream.OnNext(new IpcPacket { Component = deviceHandle.ToDeviceState() });
        SendPairPacket(deviceHandle, false);
    }

    public void OnIncomingPairRequestAccepted(Guid id)
    {
        if (!_deviceHandles.TryGetValue(id, out var deviceHandle)) return;
        if (deviceHandle.PairState != PairState.PairRequestedByPeer) return;
        deviceHandle.PairState = PairState.Paired;
        deviceHandle.IncomingPairTimer?.Dispose();
        deviceRepository.SaveDevice(new Device(id, deviceHandle.Name, deviceHandle.Certificate));
        SendPairPacket(deviceHandle, true);
        deviceHandle.ReloadPlugins();
        _ipcEventStream.OnNext(new IpcPacket { Component = deviceHandle.ToDeviceState() });
    }

    public async Task OnUnpairRequested(Guid id)
    {
        var saved = await deviceRepository.GetSavedDevice(id);
        await deviceRepository.DeleteDevice(id);
        logger.LogInformation("Unpairing {Id}", id);

        if (_deviceHandles.TryGetValue(id, out var deviceHandle))
        {
            if (deviceHandle.PairState == PairState.Paired) SendPairPacket(deviceHandle, false);
            deviceHandle.IncomingPairTimer?.Dispose();
            deviceHandle.PairState = PairState.Unpaired;
            deviceHandle.ReloadPlugins();
            _ipcEventStream.OnNext(new IpcPacket { Component = deviceHandle.ToDeviceState() });
        }
        else if (saved != null)
        {
            _ipcEventStream.OnNext(new IpcPacket
            {
                Component = new DeviceState
                {
                    Id = id.ToString(), Name = saved.Name,
                    State = PairState.Unpaired, IsConnected = false
                }
            });
        }
    }

    private void OnDeviceDisconnected(Guid id, DeviceHandle deviceHandle)
    {
        if (deviceHandle.Disposed) return;
        logger.LogInformation("Device {Name} ({Id}) disconnected", deviceHandle.Name, id);
        var wasPaired = deviceHandle.PairState == PairState.Paired;
        var name = deviceHandle.Name;
        deviceHandle.Dispose();
        _deviceHandles.TryRemove(new KeyValuePair<Guid, DeviceHandle>(id, deviceHandle));

        _ipcEventStream.OnNext(new IpcPacket
        {
            Component = new DeviceState
            {
                Id = id.ToString(), Name = name,
                State = wasPaired ? PairState.Paired : PairState.Unpaired,
                IsConnected = false
            }
        });
    }

    public async Task<IList<DeviceState>> GetCurrentDeviceStates()
    {
        var states = _deviceHandles.Select(x => x.Value.ToDeviceState()).ToList();
        var liveIds = states.Select(x => x.Id).ToHashSet();
        var saved = await deviceRepository.GetSavedDevices();
        states.AddRange(saved
            .Where(d => !liveIds.Contains(d.Id.ToString()))
            .Select(d => new DeviceState
            {
                Id = d.Id.ToString(), Name = d.Name,
                State = PairState.Paired, IsConnected = false
            }));
        return states;
    }
}
