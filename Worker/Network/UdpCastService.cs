using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Infrastructure.Devices;
using Microsoft.Extensions.Hosting;

namespace Kurome.Network;

public class UdpCastService : IHostedService
{
    private readonly IIdentityProvider _identityProvider;

    public UdpCastService(IIdentityProvider identityProvider)
    {
        _identityProvider = identityProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        CastUdp(GetLocalIpAddresses());
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    
    private void CastUdp(IEnumerable<string> addresses)
    {
        var id = _identityProvider.GetEnvironmentId();
        foreach (var ip in addresses)
        {
            var udpClient = new UdpClient(AddressFamily.InterNetwork);
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Parse(ip), 33586));
            udpClient.Ttl = 32;
            var message = "kurome:" + ip + ":" + _identityProvider.GetEnvironmentName() + ":" + id;
            var data = Encoding.Default.GetBytes(message);
            Console.WriteLine("Broadcast: \"{0}\" to {1}", message, ip);
            udpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Parse("255.255.255.255"), 33586));
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
}