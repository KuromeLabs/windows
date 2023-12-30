using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kurome.Core;
using Kurome.Core.Devices;
using Kurome.Core.Interfaces;
using Kurome.Core.Network;
using FlatSharp;
using Kurome.Core.Filesystem;
using Kurome.Fbs;
using Microsoft.Extensions.Logging;

namespace Kurome.Network;

public class LinkProvider
{
    private readonly IIdentityProvider _identityProvider;
    private readonly ISecurityService<X509Certificate2> _sslService;
    private readonly ILogger<LinkProvider> _logger;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IFileSystemHost _fileSystemHost;

    public LinkProvider(IIdentityProvider identityProvider, ISecurityService<X509Certificate2> sslService,
        ILogger<LinkProvider> logger, IDeviceRepository deviceRepository, IFileSystemHost fileSystemHost)
    {
        _identityProvider = identityProvider;
        _sslService = sslService;
        _logger = logger;
        _deviceRepository = deviceRepository;
        _fileSystemHost = fileSystemHost;
    }

    private async void StartUdpListener()
    {
    }

    public async Task<int> StartTcpListener(CancellationToken cancellationToken)
    {
        var tcpListener = TcpListener.Create(33587);
        tcpListener.Start();
        _logger.LogInformation("Started TCP Listener on port {Port}", 33587);
        while (!cancellationToken.IsCancellationRequested)
            try
            {
                var client = await tcpListener.AcceptTcpClientAsync(cancellationToken);
                _logger.LogInformation("Accepted connection from {Ip}", client.Client.RemoteEndPoint as IPEndPoint);
                HandleServerConnection(client, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogDebug("Exception at StartTcpListener: {@Exception}", e.ToString());
            }

        return 0;
    }

    public async Task<int> StartUdpCaster(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                CastUdp(GetLocalIpAddresses());
                await Task.Delay(2000, cancellationToken);
            }
            catch (Exception e)
            {
                // ignored
            }
        }

        return 0;
    }

    private void CastUdp(IEnumerable<string> addresses)
    {
        var id = _identityProvider.GetEnvironmentId();
        foreach (var ip in addresses)
        {
            var udpClient = new UdpClient(AddressFamily.InterNetwork);
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Parse(ip), 33586));
            var message = "kurome:" + ip + ":" + _identityProvider.GetEnvironmentName() + ":" + id;
            var data = Encoding.Default.GetBytes(message);
            // _logger.LogInformation("UDP Broadcast: \"{0}\" to {1}", message, ip);
            udpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Parse("255.255.255.255"), 33586));
            udpClient.Close();
        }
    }

    private static IEnumerable<string> GetLocalIpAddresses()
    {
        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
        return (from network in networkInterfaces
            where network.OperationalStatus == OperationalStatus.Up
            select network.GetIPProperties()
            into properties
            from address in properties.UnicastAddresses
            where address.Address.AddressFamily == AddressFamily.InterNetwork
            where !IPAddress.IsLoopback(address.Address)
            select address.Address.ToString()).ToList();
    }

    private async void SendIdentity(TcpClient client, CancellationToken cancellationToken)
    {
        var identity = $"{_identityProvider.GetEnvironmentName()}:{_identityProvider.GetEnvironmentId()}";
        var identityBytes = Encoding.UTF8.GetBytes(identity);
        var size = identityBytes.Length;
        var bytes = new byte[size + 4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan()[..4], size);
        identityBytes.CopyTo(bytes.AsSpan()[4..]);
        await client.GetStream().WriteAsync(bytes, cancellationToken);
    }


    public async void HandleClientConnection(string name, Guid id, string ip, int port,
        CancellationToken cancellationToken)
    {
        // var stream = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
        // await stream.AuthenticateAsServerAsync(_sslService.GetSecurityContext(), true, SslProtocols.None, true);
        // var link = new Link(stream);
        //
        // if (result.ResultStatus == Result<ILink>.Status.Success)
        //     await _monitor.InvokeAsync(new Monitor.Query(name, id, result.Value!), cancellationToken);
        //
        // var mountResult = await _mount.InvokeAsync(new Mount.Command(id), cancellationToken);
        //
        // if (mountResult.ResultStatus == Result<Unit>.Status.Failure)
        //     _logger.LogError("{Error}", mountResult.Error);
    }

    public async void HandleServerConnection(TcpClient client, CancellationToken cancellationToken)
    {
        var info = await ReadIdentityAsync(client, cancellationToken);
        if (info == null)
        {
            _logger.LogError("Failed to read device identity from incoming connection");
            return;
        }

        var id = info.Item1;
        var name = info.Item2;
        var existingDevice = _deviceRepository.GetActiveDevices().FirstOrDefault(x => x.Id == id);
        if (existingDevice != null)
        {
            _logger.LogInformation("Device {Name} ({Id}) is already active, reconnecting", name, id);
            existingDevice.Dispose();
            _deviceRepository.RemoveActiveDevice(existingDevice);
        }
        Link? result = null;
        try
        {
            var stream = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
            await stream.AuthenticateAsServerAsync(_sslService.GetSecurityContext(), true, SslProtocols.None, true);
            stream.ReadTimeout = 10000;
            result = new Link(stream);
            
        }
        catch (Exception e)
        {
            _logger.LogError($"{e}");
            return;
        }

        _logger.LogInformation("Link established with {Name} ({Id})", info.Item2, id);

        var device = new Device(id, name);
        device.Connect(result);
        _deviceRepository.AddActiveDevice(device);
        
        EventHandler<bool>? handler = null;
        handler = (sender, isConnected) =>
        {
            _logger.LogInformation($"Link isConnected event: {isConnected}");
            if (isConnected)
            {
                _deviceRepository.AddActiveDevice(device);
            }
            else
            {
                result.IsConnectedChanged -= handler;
                _deviceRepository.RemoveActiveDevice(device);
                device.Dispose();
            }
        };
        result.IsConnectedChanged += handler;
        result.Start(cancellationToken);
        device.Mount(_fileSystemHost);
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
            _logger.LogError("{@Exception}", e.ToString());
            return null;
        }
    }
    
}