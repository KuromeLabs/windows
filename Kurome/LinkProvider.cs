using System;
using System.Collections.Concurrent;
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
        private readonly string UdpSubnet = "255.255.255.255";
        private string _id;
        public bool Listening { get; set; }

        public ConcurrentDictionary<string, Link> ActiveLinks = new();

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
                    var client = await _tcpListener.AcceptTcpClientAsync();
                    var buffer = await GetBufferAsync(client.GetStream());
                    if (buffer == null) continue;
                    var packet = Packet.Serializer.Parse(buffer!);
                    var stream = new SslStream(client.GetStream(), false);
                    await stream.AuthenticateAsServerAsync(SslHelper.Certificate, false, SslProtocols.None, true);
                    var link = new Link(client, stream);
                    link.StartListeningAsync();
                    link.OnLinkDisconnected += OnDisconnect;
                    var info = new DeviceInfo(packet.DeviceInfo!);
                    link.DeviceId = info.Id;
                    link.DeviceName = info.Name;
                    ActiveLinks.TryAdd(link.DeviceId, link);
                    OnLinkConnected?.Invoke(link);
                    ArrayPool<byte>.Shared.Return(buffer);
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
            OnLinkDisconnected?.Invoke(link);
            if (ActiveLinks.ContainsKey(link.DeviceId)) ActiveLinks.Remove(link.DeviceId, out _);
        }
        
        private async Task<byte[]?> GetBufferAsync(NetworkStream stream)
        {
            var sizeBuffer = new byte[4];
            var bytesRead = 0;
            try
            {
                int current;
                while (bytesRead != 4)
                {
                    current = await stream.ReadAsync(sizeBuffer.AsMemory(0 + bytesRead, 4 - bytesRead));
                    bytesRead += current;
                    if (current != 0) continue;
                    return null;
                }

                var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
                bytesRead = 0;
                var readBuffer = ArrayPool<byte>.Shared.Rent(size);
                while (bytesRead != size)
                {
                    current = await stream.ReadAsync(readBuffer.AsMemory(0 + bytesRead, size - bytesRead));
                    bytesRead += current;
                    if (current != 0) continue;
                    return null;
                }

                return readBuffer;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }
    }
}