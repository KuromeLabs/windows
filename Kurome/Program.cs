﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Kurome
{
    class Program
    {
        public static readonly string UDP_SUBNET = "235.132.20.12";

        static void Main(string[] args)
        {
            var address = IPAddress.Parse(UDP_SUBNET);
            var ipEndPoint = new IPEndPoint(address, 33586);
            CastUdpInfo(address, ipEndPoint, GetLocalIpAddress());
            KuromeTcpServer server = new KuromeTcpServer();
            server.StartServer();
            while (true)
            {
                String message = Console.ReadLine();
                server.Send(message, 0);
            }
        }

        private static void CastUdpInfo(IPAddress address, IPEndPoint endpoint, IEnumerable<string> localIpAddresses)
        {
            foreach (var interfaceIp in localIpAddresses)
            {
                String message = "kurome:" + interfaceIp + ":" + Environment.MachineName;
                var data = Encoding.Default.GetBytes(message);
                using var udpClient = new UdpClient(AddressFamily.InterNetwork);
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Parse(interfaceIp), 33586));
                udpClient.JoinMulticastGroup(address);
                udpClient.Ttl = 32;
                udpClient.Send(data, data.Length, endpoint);
                Console.WriteLine("Broadcast: \"{0}\" to {1}", message, interfaceIp);
                udpClient.Close();
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