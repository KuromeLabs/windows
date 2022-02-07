using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FlatBuffers;
using kurome;

namespace Kurome
{
    public sealed class LinkProvider
    {
        private static readonly Lazy<LinkProvider> Lazy = new(() => new LinkProvider());
        private readonly TcpListener _controlListener = TcpListener.Create(33587);
        public static LinkProvider Instance => Lazy.Value;
        private readonly object _lock = new();
        private readonly string UdpSubnet = "235.132.20.12";
        private readonly int Port = 33586;
        private string _id;
        private FlatBufferBuilder _builder = new(1024);

        private IEnumerable<string> _localIpAddresses = Array.Empty<string>();

        private Dictionary<string, UdpClient> _udpDictionary = new();

        public void InitializeUdpListener()
        {
            _id = IdentityProvider.GetGuid();
            _localIpAddresses = GetLocalIpAddress();
            foreach (var ip in _localIpAddresses)
            {
                var udpClient = new UdpClient(AddressFamily.InterNetwork);
                udpClient.JoinMulticastGroup(IPAddress.Parse(UdpSubnet));
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Parse(ip), Port));
                udpClient.Ttl = 32;
                _udpDictionary.Add(ip, udpClient);
            }
        }

        public Link CreateLink(Link controlLink)
        {
            lock (_lock)
            {
                var listener = TcpListener.Create(33588);
                listener.Start();
                var packet = Packet.CreatePacket(_builder, action: ActionType.ActionCreateLink);
                _builder.FinishSizePrefixed(packet.Value);
                controlLink.SendBuffer(_builder.DataBuffer);
                _builder.Clear();
                // controlLink.WritePrefixed(Packets.ActionCreateNewLink);
                var client = listener.AcceptTcpClient();
                listener.Stop();
                return new Link(client);
            }
        }

        public async void StartCasting(TimeSpan interval, CancellationToken token)
        {
            var address = IPAddress.Parse(UdpSubnet);
            var ipEndPoint = new IPEndPoint(address, Port);
            _id = IdentityProvider.GetGuid();
            // using var udpClient = new UdpClient();
            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    _localIpAddresses = GetLocalIpAddress();
                    await Task.Delay(TimeSpan.FromSeconds(10), token);
                }
            }, token);
            while (!token.IsCancellationRequested)
            {
                CastUdpInfo(address, ipEndPoint);
                await Task.Delay(interval, token);
            }
        }

        public void StartListening()
        {
            _controlListener.Start();
        }

        public async Task<Link> GetIncomingLink()
        {
            return new Link(await _controlListener.AcceptTcpClientAsync());
        }

        public async void CastUdpInfo(IPAddress address, IPEndPoint endpoint)
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

        public IEnumerable<string> GetLocalIpAddress()
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