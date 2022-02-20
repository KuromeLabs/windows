using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using kurome;

namespace Kurome
{
    public class LinkProvider
    {
        private readonly TcpListener _tcpListener = TcpListener.Create(33587);
        private readonly int UdpPort = 33586;
        private readonly string UdpSubnet = "235.132.20.12";
        private string _id;
        public bool Listening { get; set; }

        public event KuromeDaemon.LinkConnected OnLinkConnected;
        public event KuromeDaemon.LinkDisconnected OnLinkDisconnected;


        public delegate void LinkDisconnected(Link link);


        public void Initialize()
        {
            var addresses = GetLocalIpAddresses();

            StartTcpListener();
            CastUdp(addresses);
        }

        private void CastUdp(IEnumerable<string> addresses)
        {
            _id = IdentityProvider.GetGuid();
            foreach (var ip in addresses)
            {
                var udpClient = new UdpClient(AddressFamily.InterNetwork);
                udpClient.JoinMulticastGroup(IPAddress.Parse(UdpSubnet));
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Parse(ip), UdpPort));
                udpClient.Ttl = 32;
                var message = "kurome:" + ip + ":" + IdentityProvider.GetMachineName() + ":" + _id;
                var data = Encoding.Default.GetBytes(message);
                Console.WriteLine("Broadcast: \"{0}\" to {1}", message, ip);
                udpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(UdpSubnet), UdpPort));
            }
        }

        private async void StartTcpListener()
        {
            _tcpListener.Start();
            while (Listening)
            {
                try
                {
                    var link = new Link(await _tcpListener.AcceptTcpClientAsync());
                    link.OnLinkDisconnected += OnDisconnect;
                    var contextCompletionSource = new TaskCompletionSource<Link.LinkContext>();
                    link.AddCompletionSource(0, contextCompletionSource);
                    link.StartListeningAsync();
                    var result = contextCompletionSource.Task.Result;
                    var info = new DeviceInfo(result.Packet.DeviceInfo!);
                    link.DeviceId = info.Id;
                    OnLinkConnected?.Invoke(info.Name, info.Id, link);
                    result.Dispose();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception at StartTcpListener: {0}", e);
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

        private void OnDisconnect(Link link)
        {
            Console.WriteLine("OnDisconnect called");
            OnLinkDisconnected?.Invoke(link.DeviceId, link);
        }
    }
}