using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using DynamicData;
using FlatSharp;
using Kurome.Core.Devices;
using Kurome.Core.Interfaces;
using Kurome.Core.Network;
using Kurome.Fbs.Device;
using Microsoft.Extensions.Logging;

namespace Kurome.Network;

public class DeviceService(
    ILogger<DeviceService> logger,
    ISecurityService<X509Certificate2> sslService,
    IDeviceRepository deviceRepository
)
{
    private readonly SourceCache<DeviceState, Guid> _deviceStates = new(t => t.Id);
    private readonly ConcurrentDictionary<Guid, DeviceHandler> _deviceHandlers = new();
    public IObservableCache<DeviceState, Guid> DeviceStates => _deviceStates.AsObservableCache();

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
        var hasExistingHandler = _deviceHandlers.TryRemove(id, out var existingDeviceHandler);
        if (hasExistingHandler && existingDeviceHandler!.IsConnected)
        {
            logger.LogInformation("Device {Name} ({Id}) is already active, disconnecting", name, id);
            existingDeviceHandler.Stop();
        }
        var device = deviceRepository.GetSavedDevice(id).Result;
        var deviceTrusted = false;
        Link? link;
        try
        {
            var stream = new SslStream(client.GetStream(), false, (sender, certificate, chain, policyErrors) =>
            {
                if (policyErrors == SslPolicyErrors.RemoteCertificateNameMismatch) return false;
                if (certificate == null) return false;
                if (device != null && certificate.Equals(device.Certificate))
                {
                    deviceTrusted = true;
                    return true;
                }
                device = new Device(id, name, (X509Certificate2)certificate);
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
        var deviceHandler = new DeviceHandler(link, id, name, deviceTrusted)
        {
            IsConnected = true
        };
        _deviceHandlers.TryAdd(id, deviceHandler);

        new Thread(() => link.Start(cancellationToken))
        {
            IsBackground = true,
        }.Start();
        deviceHandler.Start();
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

    public void OnIncomingPairRequestRejected(Guid id)
    {
        if (_deviceHandlers.TryGetValue(id, out var deviceHandler))
            deviceHandler.OnIncomingPairRequestRejected();
    }
    
    public void OnIncomingPairRequestAccepted(Guid id)
    {
        if (_deviceHandlers.TryGetValue(id, out var deviceHandler))
            deviceHandler.OnIncomingPairRequestAccepted();
    }
}

public sealed class DeviceState(string name, Guid id) : INotifyPropertyChanged
{
    public Guid Id { get; set; } = id;
    public string Name { get; set; } = name;

    public bool IsConnected;
    public PairState State { get; set; } = PairState.Unpaired;
    


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
}