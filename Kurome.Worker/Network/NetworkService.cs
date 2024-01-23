using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kurome.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Kurome.Network;

public class NetworkService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly ILogger<NetworkService> _logger;
    private readonly DeviceService _deviceService;
    private readonly ConcurrentDictionary<string, UdpClient> _udpClients = new();

    public NetworkService(IIdentityProvider identityProvider, ISecurityService<X509Certificate2> sslService,
        ILogger<NetworkService> logger,  DeviceService deviceService)
    {
        _identityProvider = identityProvider;
        _logger = logger;
        _deviceService = deviceService;
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
                _logger.LogInformation("Waiting for incoming TCP connection");
                var client = await tcpListener.AcceptTcpClientAsync(cancellationToken);
                _logger.LogInformation("Accepted connection from {Ip}", client.Client.RemoteEndPoint as IPEndPoint);
                _ = _deviceService.HandleIncomingTcp(client, cancellationToken);
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
            var message = "kurome:" + ip + ":" + _identityProvider.GetEnvironmentName() + ":" + id;
            var data = Encoding.Default.GetBytes(message);
            try
            {
                udpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Parse("255.255.255.255"), 33586));
                // _logger.LogInformation("UDP Broadcast: \"{0}\" to {1}", message, ip);
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