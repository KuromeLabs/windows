using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FlatSharp;
using kurome;
using Action = kurome.Action;

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
            StartUdpListener();
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

        private async void StartUdpListener()
        {
            Console.WriteLine("Starting UDP Listener");
            var udpSocket = new UdpClient(33588);
            while (true)
            {
                var receivedBytes = (await udpSocket.ReceiveAsync(CancellationToken.None)).Buffer;
                var message = Encoding.Default.GetString(receivedBytes);
                Console.WriteLine("Received UDP: \"{0}\"", message);
                DatagramPacketReceived(message);
                
            }
        }

        private async void DatagramPacketReceived(string message)
        {
            var split = message.Split(':');
            var ip = split[1];
            var name = split[2];
            var id = split[3];
            if (ActiveLinks.ContainsKey(id))
                return;
            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Parse(ip), 33587);
            Console.WriteLine("Connected to {0}", ip);

            SendIdentity(client);
            var stream = new SslStream(client.GetStream(), true,
                (_, _, _, _) => true);
            await stream.AuthenticateAsClientAsync("Kurome", null, SslProtocols.None, false);
            var link = new Link(client, stream);
            link.StartListeningAsync();
            link.OnLinkDisconnected += OnDisconnect;
            link.DeviceId = id;
            link.DeviceName = name;
            ActiveLinks.TryAdd(link.DeviceId, link);
            OnLinkConnected?.Invoke(link);
        }

        private void SendIdentity(TcpClient client)
        {
            var packet = new Packet
            {
                Action = Action.ActionConnect,
                DeviceInfo = new DeviceInfo
                {
                    Id = IdentityProvider.GetGuid(),
                    Name = IdentityProvider.GetMachineName(),
                }
            };
            var size = Packet.Serializer.GetMaxSize(packet);
            var bytes = ArrayPool<byte>.Shared.Rent(size + 4);
            Span<byte> buffer = bytes;
            var length = Packet.Serializer.Write(buffer[4..], packet);
            BinaryPrimitives.WriteInt32LittleEndian(buffer[..4], length);
            Console.WriteLine("Sending buffer of size {0}", length + 4);
            client.GetStream().Write(buffer[..(4+length)]);
            ArrayPool<byte>.Shared.Return(bytes);
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