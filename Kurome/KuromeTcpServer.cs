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
                //list drive letters starting from C and excluding those already in use
                var list = Enumerable.Range('C', 'Z' - 'C').Select(i => (char) i + ":")
                    .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
                var letter = list[_numOfConnectedClients - 1][0];
                var controlLink = new Link(tcpClient);
                var device = new Device(controlLink, letter);
                var rfs = new KuromeOperations(device);
                await Task.Run(() =>rfs.Mount(letter + ":\\", DokanOptions.FixedDrive, 4));
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