using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FlatSharp;
using Kurome.Core.Interfaces;
using Kurome.Fbs.Device;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kurome.Network;

public class NetworkService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly ILogger<NetworkService> _logger;
    private readonly DeviceService _deviceService;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, UdpClient> _udpClients = new();
    private ushort _tcpListeningPort = 0;

    public NetworkService(IIdentityProvider identityProvider, ISecurityService<X509Certificate2> sslService,
        ILogger<NetworkService> logger,  DeviceService deviceService, IConfiguration configuration)
    {
        _identityProvider = identityProvider;
        _logger = logger;
        _deviceService = deviceService;
        _configuration = configuration;
    }

    private async void StartUdpListener()
    {
    }

    public async Task<int> StartTcpListener(CancellationToken cancellationToken)
    {
        
        _tcpListeningPort = ushort.Parse(_configuration["Connection:TcpListeningPort"]!);
        var tcpListener = TcpListener.Create(_tcpListeningPort);
        tcpListener.Start();
        _logger.LogInformation("Started TCP Listener on port {Port}", _tcpListeningPort);
        while (!cancellationToken.IsCancellationRequested)
            try
            {
                _logger.LogInformation("Waiting for incoming TCP connection");
                var client = await tcpListener.AcceptTcpClientAsync(cancellationToken);
                _logger.LogInformation("Accepted connection from {Ip}", client.Client.RemoteEndPoint as IPEndPoint);
                
                _deviceService.HandleIncomingTcp(client, cancellationToken);
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
                UpdateUdpClients();
                CastUdp();
                await Task.Delay(2000, cancellationToken);
            }
            catch (Exception e)
            {
                // ignored
            }
        }

        return 0;
    }

    private void CastUdp()
    {
        var id = _identityProvider.GetEnvironmentId();
        foreach (var (ip, udpClient) in _udpClients)
        {
            try
            {
                var component = new Component(new DeviceIdentityResponse
                {
                    FreeBytes = 0,
                    TotalBytes = 0,
                    Name = _identityProvider.GetEnvironmentName(),
                    Id = _identityProvider.GetEnvironmentId(),
                    LocalIp = ip,
                    Platform = Platform.Windows,
                    TcpListeningPort = _tcpListeningPort
                });
                var packet = new Packet { Component = component, Id = -1 };
                var size = Packet.Serializer.GetMaxSize(packet);
                var buffer = ArrayPool<byte>.Shared.Rent(size);
                var length = Packet.Serializer.Write(buffer, packet);
                udpClient.Send(buffer, length, new IPEndPoint(IPAddress.Parse("255.255.255.255"), 33586));
                ArrayPool<byte>.Shared.Return(buffer);
                _logger.LogInformation("UDP Broadcast of FlatBuffer identity packet of size: \"{0}\" to {1}", length, ip);
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to send UDP broadcast: {Error}", e.ToString());
                udpClient.Dispose();
                _udpClients.TryRemove(ip, out _);
            }
        }
    }

    private void UpdateUdpClients()
    {
        var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
        foreach (var networkInterface in networkInterfaces)
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up) continue;
            var properties = networkInterface.GetIPProperties();
            foreach (var address in properties.UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (IPAddress.IsLoopback(address.Address)) continue;
                var ip = address.Address.ToString();
                if (_udpClients.ContainsKey(ip)) continue;
                var client = new UdpClient(AddressFamily.InterNetwork);
                try
                {
                    client.Client.Bind(new IPEndPoint(address.Address, 33586));
                    _udpClients.TryAdd(ip, client);
                } catch (Exception e)
                {
                    _logger.LogError("Failed to bind UDP client to {Ip}: {Error}", ip, e.ToString());
                    client.Dispose();
                }
            }
        }
    }

    private async Task SendIdentity(TcpClient client, CancellationToken cancellationToken)
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
    
}