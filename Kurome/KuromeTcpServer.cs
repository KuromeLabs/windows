using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
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
                var buffer = new byte[4096];
                var byteCount = tcpClient.GetStream().Read(buffer, 0, buffer.Length);
                var request = Encoding.UTF8.GetString(buffer, 0, byteCount);
                
                
                //list drive letters starting from C and excluding those already in use
                var list = Enumerable.Range('C', 'Z' - 'C').Select(i => (char) i + ":")
                    .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
                char letter = list[ConnectedTcpClients.Count - 1][0];
                var rfs = new KuromeVirtualDisk(tcpClient, request, letter);
               await Task.Run(() => rfs.Mount(letter + ":\\", DokanOptions.DebugMode | DokanOptions.StderrOutput));
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