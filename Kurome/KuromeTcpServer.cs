using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
            
            var buffer = new byte[4096];
            Console.WriteLine("[Server] Reading from client");
            while (true)
            {
                var networkStream = tcpClient.GetStream();
                if (!IsTcpClientAlive(tcpClient).Result) break;
                var byteCount = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                if (byteCount == 0) break;
                var request = Encoding.UTF8.GetString(buffer, 0, byteCount);
                Console.Write("[Server]: Client wrote {0}", request);
            }
            lock (_lock) ConnectedTcpClients.Remove(tcpClient);
            tcpClient.Close();
            Console.WriteLine("Client disconnected");
        }

        public async void Send(string message, int tcpClientIndex)
        {
            try
            {
                IsTcpClientAlive(ConnectedTcpClients[0]);
                var token = new CancellationTokenSource(1000);
                var messageBytes = Encoding.UTF8.GetBytes(message);
                var networkStream = ConnectedTcpClients[0].GetStream();
                await networkStream.WriteAsync(messageBytes, 0, messageBytes.Length, token.Token);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private async Task<bool> IsTcpClientAlive(TcpClient tcpClient)
        {
            var buffer = new byte[4096];
            var networkStream = tcpClient.GetStream();
            await networkStream.WriteAsync(Encoding.UTF8.GetBytes("are you alive"));
            var readTask = networkStream.ReadAsync(buffer, 0, buffer.Length);
            await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(10)));
            if (!readTask.IsCompleted)
            {
                Console.WriteLine("Connection timed out");
                return false;
            }
            var request = Encoding.UTF8.GetString(buffer, 0, readTask.Result);
            if (request != "yes")
            {
                Console.WriteLine("Connection attestation failed");
                return false;
            }
            Console.WriteLine("Connection attestation succeeded");
            return true;

        }
    }
}