using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Kurome
{
    public class LinkProvider
    {
        private readonly TcpListener _tcpListener = TcpListener.Create(33587);
        private readonly string UdpSubnet = "235.132.20.12";
        private readonly int UdpPort = 33586;
        private string _id;
        private readonly Dictionary<string, UdpClient> _udpDictionary = new();


        public void Initialize()
        {
            var addresses = GetLocalIpAddresses();
            CreateUdpClients(addresses);
            StartListening();
            CastUdpInfo(new IPEndPoint(IPAddress.Parse(UdpSubnet), UdpPort));
        }

        private void CreateUdpClients(IEnumerable<string> addresses)
        {
            _id = IdentityProvider.GetGuid();
            foreach (var ip in addresses)
            {
                var udpClient = new UdpClient(AddressFamily.InterNetwork);
                udpClient.JoinMulticastGroup(IPAddress.Parse(UdpSubnet));
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Parse(ip), UdpPort));
                udpClient.Ttl = 32;
                _udpDictionary.Add(ip, udpClient);
            }
        }

        private void StartListening()
        {
            _tcpListener.Start();
        }

        public async Task<Link> GetIncomingLink()
        {
            return new Link(await _tcpListener.AcceptTcpClientAsync());
        }

        private async void CastUdpInfo(IPEndPoint endpoint)
        {
            foreach (var addr in _udpDictionary.Keys)
            {
                var message = "kurome:" + addr + ":" + IdentityProvider.GetMachineName() + ":" + _id;
                var data = Encoding.Default.GetBytes(message);
                try
                {
                    await _udpDictionary[addr].SendAsync(data, data.Length, endpoint);
                    // Console.WriteLine("Broadcast: \"{0}\" to {1}", message, addr);
                }
                catch (SocketException e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        private IEnumerable<string> GetLocalIpAddresses()
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
}