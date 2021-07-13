using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DokanNet;

namespace Kurome
{
    class Program
    {
        

        static void Main(string[] args)
        {
            KuromeUdpServer udpServer = new KuromeUdpServer();
            udpServer.PeriodicCastUdpInfo(TimeSpan.FromSeconds(5), CancellationToken.None);
            // var address = IPAddress.Parse(UDP_SUBNET);
            // var ipEndPoint = new IPEndPoint(address, PORT);
            // CastUdpInfo(address, ipEndPoint, GetLocalIpAddress());
            KuromeTcpServer tcpServer = new KuromeTcpServer();
            tcpServer.StartServer();
            Console.Read();
        }

        

        
    }
}