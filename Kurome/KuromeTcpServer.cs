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
        private int _numOfConnectedClients;
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
                    _numOfConnectedClients++;
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
                var byteCount = await tcpClient.GetStream().ReadAsync(buffer, 0, buffer.Length);
                var request = Encoding.UTF8.GetString(buffer, 0, byteCount);
                
                
                //list drive letters starting from C and excluding those already in use
                var list = Enumerable.Range('C', 'Z' - 'C').Select(i => (char) i + ":")
                    .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
                char letter = list[_numOfConnectedClients - 1][0];
                var rfs = new KuromeVirtualDisk(tcpClient.GetStream(), request, letter);
                rfs.Mount(letter + ":\\");
                Console.WriteLine(@"Success");
            }
            catch (DokanException ex)
            {
                Console.WriteLine(@"Error: " + ex.Message);
            }
            _numOfConnectedClients--;
            tcpClient.Close();
            Console.WriteLine("Server TCP Connection closed");
        }
    }
}