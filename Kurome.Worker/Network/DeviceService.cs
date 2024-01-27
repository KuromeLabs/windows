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

    public async Task HandleIncomingTcp(TcpClient client, CancellationToken cancellationToken)
    {
        await Task.Yield();
        var info = await ReadIdentityAsync(client, cancellationToken);
        if (info == null)
        {
            logger.LogError("Failed to read device identity from incoming connection");
            return;
        }

        var id = info.Item1;
        var name = info.Item2;
        logger.LogInformation("Checking existing active devices");
        var existingDeviceState = _deviceStates.Lookup(id);
        if (existingDeviceState.HasValue && existingDeviceState.Value.IsConnectedOrConnecting())
        {
            logger.LogInformation("Device {Name} ({Id}) is already active, disconnecting", name, id);
            DisconnectDevice(id);
            return;
        }

        var deviceState = new DeviceState(new Device { Name = name, Id = id },
            DeviceState.State.Connecting, "Connecting...");
        _deviceStates.AddOrUpdate(deviceState);
        var device = await deviceRepository.GetSavedDevice(id);
        var isDeviceTrusted = device != null;
        Link? link;
        try
        {
            var stream = new SslStream(client.GetStream(), false, (sender, certificate, chain, policyErrors) =>
            {
                if (policyErrors == SslPolicyErrors.RemoteCertificateNameMismatch) return false;
                if (certificate == null) return false;
                if (device != null && certificate.Equals(device.Certificate))
                {
                    isDeviceTrusted = true;
                    return true;
                }

                isDeviceTrusted = false;
                device = new Device(id, name, (X509Certificate2)certificate);
                return true;
            });
            await stream.AuthenticateAsServerAsync(sslService.GetSecurityContext(), true, SslProtocols.None, true);
            link = new Link(stream);
        }
        catch (Exception e)
        {
            isDeviceTrusted = false;
            logger.LogError($"{e}");
            return;
        }

        logger.LogInformation("Link established with {Name} ({Id})", info.Item2, id);
        SetDeviceServices(isDeviceTrusted, deviceState, link);
        link.DataReceived
            .Where(x => Packet.Serializer.Parse(x.Data).Component?.Kind == Component.ItemKind.Pair)
            .ObserveOn(Scheduler.Default)
            .Subscribe(buffer =>
            {
                var pair = Packet.Serializer.Parse(buffer.Data).Component?.Pair!;
                HandleIncomingPair(pair.Value, deviceState, link);
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

    private void SetDeviceServices(bool isDeviceTrusted, DeviceState deviceState, Link link)
    {
        if (isDeviceTrusted)
        {
            var deviceAccessor = new DeviceAccessor(link, deviceState.Device);
            _deviceStates.AddOrUpdate(deviceState.UpdateStatus(DeviceState.State.ConnectedTrusted, "Connected"));
            fileSystemService.MountToAvailableMountPoint(deviceAccessor);
        }
        else
        {
            _deviceStates.AddOrUpdate(deviceState.UpdateStatus(DeviceState.State.ConnectedUntrusted,
                "Connected (Not Trusted)"));
        }
    }

    private void HandleIncomingPair(bool paired, DeviceState deviceState, Link link)
    {
        if (paired)
        {
            //incoming pair request, for now accept automatically
            //TODO: implement properly
            deviceRepository.SaveDevice(deviceState.Device);
            SetDeviceServices(true, deviceState, link);
        }
    }

    private void DisconnectDevice(Guid id)
    {
        var state = _deviceStates.Lookup(id).Value;
        fileSystemService.Unmount(state.Device.Id);
        _deviceStates.AddOrUpdate(state.UpdateStatus(DeviceState.State.Disconnected, "Disconnected"));
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

public class DeviceState(Device device, DeviceState.State status, string statusMessage) : INotifyPropertyChanged
{
    public Device Device { get; set; } = device;
    public string StatusMessage { get; set; } = statusMessage;
    public State Status { get; set; } = status;

    public enum State
    {
        Disconnected,
        Connecting,
        ConnectedTrusted,
        ConnectedUntrusted
    }

    public bool IsConnectedOrConnecting()
    {
        return Status is State.ConnectedTrusted or State.ConnectedUntrusted or State.Connecting;
    }

    public DeviceState UpdateStatus(State status, string statusMessage)
    {
        Status = status;
        StatusMessage = statusMessage;
        return this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}