using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kurome
{
    public class KuromeUdpServer
    {
        public static readonly string UDP_SUBNET = "235.132.20.12";
        public static readonly int PORT = 33586;
        private static string _id;

        public async void PeriodicCastUdpInfo(TimeSpan interval, CancellationToken token)
        {
            var address = IPAddress.Parse(UDP_SUBNET);
            var ipEndPoint = new IPEndPoint(address, PORT);
            _id = IdentityProvider.GetGuid();
            while (true)
            {
                CastUdpInfo(address, ipEndPoint);
                await Task.Delay(interval, token);
            }
        }

        private static async void CastUdpInfo(IPAddress address, IPEndPoint endpoint)
        {
            var localIpAddresses = GetLocalIpAddress();
            foreach (var interfaceIp in localIpAddresses)
            {
                var message = "kurome:" + interfaceIp + ":" + IdentityProvider.GetMachineName() + ":" + _id;
                var data = Encoding.Default.GetBytes(message);
                using var udpClient = new UdpClient(AddressFamily.InterNetwork);
                try
                {
                    udpClient.Client.Bind(new IPEndPoint(IPAddress.Parse(interfaceIp), PORT));
                    udpClient.JoinMulticastGroup(address);
                    udpClient.Ttl = 32;
                    await udpClient.SendAsync(data, data.Length, endpoint);
                    // Console.WriteLine("Broadcast: \"{0}\" to {1}", message, interfaceIp);
                    udpClient.Close();
                }
                catch (SocketException e)
                {
                    Console.WriteLine(e);
                }
            }
        }
        
        private static IEnumerable<string> GetLocalIpAddress()
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