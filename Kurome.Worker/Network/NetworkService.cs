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
using Makaretu.Dns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kurome.Network;

public class NetworkService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly ILogger<NetworkService> _logger;
    private readonly DeviceService _deviceService;
    private readonly IConfiguration _configuration;
    private ushort _tcpListeningPort = 0;
    private ServiceDiscovery _serviceDiscovery;

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

    public void StartMdnsAdvertiser()
    {
        var service = new ServiceProfile(_identityProvider.GetEnvironmentId(), "_kurome._tcp", _tcpListeningPort);
        service.AddProperty("platform","Windows");
        service.AddProperty("name", _identityProvider.GetEnvironmentName());
        service.AddProperty("id", _identityProvider.GetEnvironmentId());
        
        _serviceDiscovery = new ServiceDiscovery();
        _serviceDiscovery.Advertise(service);
        _logger.LogDebug("mDNS Announce");
        _serviceDiscovery.Announce(service);
        _logger.LogDebug("mDNS Advertise");
    }

    public void StopMdnsAdvertiser()
    {
        _serviceDiscovery.Unadvertise();
        _logger.LogDebug("mDNS Goodbye");
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