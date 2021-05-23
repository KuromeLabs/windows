using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Kurome
{
    public class KuromeTcpServer
    {
        object _lock = new(); // sync lock 
        List<Task> _connections = new();
        private List<TcpClient> _connectedTcpClients { get; set; } = new();
        public async void StartServer()
        {
            IPAddress ipAddress = IPAddress.Any;
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 33587);
            try
            {
                TcpListener tcpListener = TcpListener.Create(33587);
                tcpListener.Start();
                while (true)
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();
                    Console.WriteLine("[Server]: Client has connected");
                    _connectedTcpClients.Add(client);
                    var task = StartHandleConnectionAsync(client);
                    if (task.IsFaulted)
                        await task;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("\n Press any key to continue...");
            Console.ReadKey();
        }

        private async Task StartHandleConnectionAsync(TcpClient tcpClient)
        {
            // start the new connection task
            var connectionTask = HandleConnectionAsync(tcpClient);

            // add it to the list of pending task 
            lock (_lock)
                _connections.Add(connectionTask);

            // catch all errors of HandleConnectionAsync
            try
            {
                await connectionTask;
                // we may be on another thread after "await"
            }
            catch (Exception ex)
            {
                // log the error
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                // remove pending task
                lock (_lock) _connections.Remove(connectionTask);
                tcpClient.Close();
                _connectedTcpClients.Remove(tcpClient);
            }
        }

        private async Task HandleConnectionAsync(TcpClient tcpClient)
        {
            await Task.Yield();
            // continue asynchronously on another threads

            using (var networkStream = tcpClient.GetStream())
            {
                var buffer = new byte[4096];
                Console.WriteLine("[Server] Reading from client");
                while (true)
                {
                    var byteCount = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                    var request = Encoding.UTF8.GetString(buffer, 0, byteCount);
                    Console.WriteLine("[Server]: Client wrote {0}", request);
                }
            }
        }

        public async void Send(string message, int tcpClientIndex)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            await _connectedTcpClients[tcpClientIndex].GetStream().WriteAsync(messageBytes, 0, messageBytes.Length);
            await _connectedTcpClients[tcpClientIndex].GetStream().FlushAsync();
        }
    }
}