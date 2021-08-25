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
            var linkProvider = LinkProvider.Instance;
            linkProvider.StartCasting(TimeSpan.FromSeconds(1), CancellationToken.None);
            KuromeTcpServer tcpServer = new KuromeTcpServer();
            tcpServer.StartServer();
            Console.Read();
        }

        

        
    }
}