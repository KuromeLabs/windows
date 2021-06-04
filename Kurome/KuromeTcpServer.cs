using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using DokanNet;

namespace Kurome
{
    public class KuromeTcpServer
    {
        readonly object _lock = new(); // sync lock 
        private List<TcpClient> ConnectedTcpClients { get; } = new();

        public async void StartServer()
        {
            try
            {
                TcpListener tcpListener = TcpListener.Create(33587);
                tcpListener.Start();
                while (true)
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();
                    Console.WriteLine("[Server]: Client has connected");
                    lock (_lock) ConnectedTcpClients.Add(client);
                    HandleConnectionAsync(client);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("\n Press any key to continue...");
            Console.ReadKey();
        }

        private async void HandleConnectionAsync(TcpClient tcpClient)
        {
            try
            {
               var list = Enumerable.Range('C', 'Z' - 'C').Select(i => (char) i + ":")
                    .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
               var rfs = new KuromeVirtualDisk(tcpClient);
               await Task.Run(() => rfs.Mount(list[ConnectedTcpClients.Count - 1] + "\\", DokanOptions.DebugMode | DokanOptions.StderrOutput));
                Console.WriteLine(@"Success");
            }
            catch (DokanException ex)
            {
                Console.WriteLine(@"Error: " + ex.Message);
            }
            lock (_lock) ConnectedTcpClients.Remove(tcpClient);
            tcpClient.Close();
            Console.WriteLine("Client disconnected");
        }
    }
}