using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Security;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using FlatSharp;
using Kurome.Core.Devices;
using Kurome.Core.Interfaces;
using Kurome.Core.Network;
using Kurome.Fbs.Device;
using Microsoft.Extensions.Logging;
using Component = Kurome.Fbs.Device.Component;

namespace Kurome.Network;

public class DeviceService(
    ILogger<DeviceService> logger,
    ISecurityService<X509Certificate2> sslService,
    IDeviceRepository deviceRepository,
    FileSystemService fileSystemService
)
{
    private readonly SourceCache<DeviceState, Guid> _deviceStates = new(t => t.Device.Id);
    public IObservableCache<DeviceState, Guid> DeviceStates => _deviceStates.AsObservableCache();

    public void HandleIncomingTcp(TcpClient client, CancellationToken cancellationToken)
    {
        var info = ReadIdentityAsync(client, cancellationToken).Result;
        if (info == null)
        {
            logger.LogError("Failed to read device identity from incoming connection");
            return;
        }

        var id = info.Item1;
        var name = info.Item2;
        logger.LogInformation("Checking existing active devices");
        var existingDeviceState = _deviceStates.Lookup(id);
        if (existingDeviceState.HasValue && existingDeviceState.Value.IsConnected)
        {
            logger.LogInformation("Device {Name} ({Id}) is already active, disconnecting", name, id);
            DisconnectDevice(id);
        }

        var deviceState = new DeviceState(new Device { Name = name, Id = id }, null, "Connecting...");
        deviceState.IsConnected = true;
        _deviceStates.AddOrUpdate(deviceState);
        var device = deviceRepository.GetSavedDevice(id).Result;

        Link? link;
        try
        {
            var stream = new SslStream(client.GetStream(), false, (sender, certificate, chain, policyErrors) =>
            {
                if (policyErrors == SslPolicyErrors.RemoteCertificateNameMismatch) return false;
                if (certificate == null) return false;
                if (device != null && certificate.Equals(device.Certificate))
                {
                    deviceState.UpdatePairState(DeviceState.PairState.Paired);
                    return true;
                }

                deviceState.UpdatePairState(DeviceState.PairState.Unpaired);
                device = new Device(id, name, (X509Certificate2)certificate);
                return true;
            });
            stream.AuthenticateAsServer(sslService.GetSecurityContext(), true, SslProtocols.None, true);
            link = new Link(stream);
        }
        catch (Exception e)
        {
            deviceState.UpdatePairState(DeviceState.PairState.Unpaired);
            logger.LogError($"{e}");
            return;
        }

        logger.LogInformation("Link established with {Name} ({Id})", info.Item2, id);
        deviceState.Device = device!;
        deviceState.Link = link;
        StartDeviceServices(deviceState);
        link.DataReceived
            .Where(x => Packet.Serializer.Parse(x.Data).Component?.Kind == Component.ItemKind.Pair)
            .ObserveOn(Scheduler.Default)
            .Subscribe(buffer =>
            {
                var pair = Packet.Serializer.Parse(buffer.Data).Component?.Pair!;
                HandleIncomingPairPacket(pair, device.Id);
            }, _ =>
            {
                logger.LogInformation("Link closed with {Name} ({Id})", device.Name, device.Id);
                DisconnectDevice(device.Id);
            }, () => { });

        new Thread(() => link.Start(cancellationToken))
        {
            IsBackground = true,
        }.Start();
    }


    public void OnIncomingPairRequestAccepted(Guid id)
    {
        var state = _deviceStates.Lookup(id);
        if (!state.HasValue) return;
        state.Value.IncomingPairTimer?.Dispose();
        if (state.Value.State != DeviceState.PairState.PairRequestedByPeer) return;
        if (deviceRepository.SaveDevice(state.Value.Device) == 0)
        {
            logger.LogError("Failed to save device {Name} ({Id})", state.Value.Device.Name, state.Value.Device.Id);
            return;
        }
        _deviceStates.AddOrUpdate(state.Value.UpdatePairState(DeviceState.PairState.Paired));
        var packet = new Packet { Component = new Component(new Pair { Value = true }), Id = -1 };
        var maxSize = Packet.Serializer.GetMaxSize(packet);
        var buffer = ArrayPool<byte>.Shared.Rent(maxSize + 4);
        var span = buffer.AsSpan();
        var length = Packet.Serializer.Write(span[4..], packet);
        BinaryPrimitives.WriteInt32LittleEndian(span[..4], length);
        state.Value.Link?.Send(buffer, length + 4);
        ArrayPool<byte>.Shared.Return(buffer);
        StartDeviceServices(state.Value);
    }

    private void HandleIncomingPairPacket(Pair pair, Guid id)
    {
        var state = _deviceStates.Lookup(id);
        if (!state.HasValue) return;
        if (pair.Value)
        {
            switch (state.Value.State)
            {
                case DeviceState.PairState.Paired:
                    //pair request but we are already paired, ignore
                    break;
                case DeviceState.PairState.PairRequested:
                    //we requested pair and it's accepted
                    break;
                case DeviceState.PairState.Unpaired:
                    //incoming pair request from peer

                    state.Value.IncomingPairTimer?.Dispose();
                    state.Value.IncomingPairTimer = new Timer(t =>
                    {
                        logger.LogInformation("Pair request timed out for {Id}", id);
                        if (!state.HasValue) return;
                        if (state.Value.State != DeviceState.PairState.PairRequestedByPeer) return;
                        _deviceStates.AddOrUpdate(state.Value.UpdatePairState(DeviceState.PairState.Unpaired));
                        state.Value.IncomingPairTimer?.Dispose();
                    }, null, 25000, Timeout.Infinite);
                    _deviceStates.AddOrUpdate(state.Value.UpdatePairState(DeviceState.PairState.PairRequestedByPeer));
                    break;
            }
        }
        else
        {
            switch (state.Value.State)
            {
                case DeviceState.PairState.Paired:
                    //unpair request
                    break;
                case DeviceState.PairState.PairRequested:
                    //we requested pair and it's rejected
                    break;
            }
        }
    }

    private void StartDeviceServices(DeviceState deviceState)
    {
        if (deviceState.State == DeviceState.PairState.Paired)
        {
            var deviceAccessor = new DeviceAccessor(deviceState.Link!, deviceState.Device);
            _deviceStates.AddOrUpdate(deviceState
                .UpdateStatus("Connected"));
            fileSystemService.MountToAvailableMountPoint(deviceAccessor);
        }
        else
        {
            _deviceStates.AddOrUpdate(deviceState
                .UpdateStatus("Connected (Not Trusted)"));
        }
    }


    private void DisconnectDevice(Guid id)
    {
        var state = _deviceStates.Lookup(id);
        if (!state.HasValue) return;
        fileSystemService.Unmount(state.Value.Device.Id);
        _deviceStates.Remove(id);
        state.Value.Dispose();
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
            var info = Packet.Serializer.Parse(readBuffer).Component?.DeviceQueryResponse;
            ArrayPool<byte>.Shared.Return(readBuffer);
            return new Tuple<Guid, string>(Guid.Parse(info!.Id!), info.Name!);
        }
        catch (Exception e)
        {
            logger.LogError("{@Exception}", e.ToString());
            return null;
        }
    }
}

public sealed class DeviceState(Device device, Link? link, string statusMessage)
    : INotifyPropertyChanged, IDisposable
{
    public Device Device { get; set; } = device;
    [JsonIgnore] public Link? Link { get; set; } = link;
    [JsonIgnore] public Timer? IncomingPairTimer { get; set; }
    public string StatusMessage { get; set; } = statusMessage;
    public bool IsConnected;
    public PairState State { get; set; } = PairState.Unpaired;

    public DeviceState UpdateStatus(string statusMessage)
    {
        StatusMessage = statusMessage;
        return this;
    }

    public DeviceState UpdatePairState(PairState state)
    {
        State = state;
        return this;
    }

    public enum PairState
    {
        Paired,
        Unpaired,
        PairRequested,
        PairRequestedByPeer
    }


    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public void Dispose()
    {
        Link?.Dispose();
    }
}