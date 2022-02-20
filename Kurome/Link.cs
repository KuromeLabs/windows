using System;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FlatSharp;
using kurome;

namespace Kurome
{
    public class Link : IDisposable
    {
        private readonly X509Certificate2 _certificate = SslHelper.Certificate;
        private readonly TcpClient _client;

        private readonly ConcurrentDictionary<int, TaskCompletionSource<Packet>> _packetTasks = new();
        private readonly SslStream _stream;
        public string DeviceId;

        public bool IsDisposed = false;
        public event LinkProvider.LinkDisconnected OnLinkDisconnected;

        public Link(TcpClient client)
        {
            _client = client;
            _stream = new SslStream(client.GetStream(), false);
            _stream.AuthenticateAsServer(_certificate, false, SslProtocols.None, true);
        }

        public void Dispose()
        {
            IsDisposed = true;
            _client.Close();
            _client.Dispose();
        }

        public async void StartListeningAsync()
        {
            while (true)
            {
                var packet = await GetPacketAsync();
                if (packet == null)
                {
                    StopConnection();
                    break;
                }

                if (_packetTasks.ContainsKey(packet.Id))
                    _packetTasks[packet.Id].SetResult(packet);
                _packetTasks.TryRemove(packet.Id, out _);
            }
        }

        public void SendBuffer(ReadOnlySpan<byte> buffer, int length)
        {
            _stream.Write(buffer[..length]);;
        }

        private async Task<Packet?> GetPacketAsync()
        {
            var sizeBuffer = new byte[4];
            var bytesRead = 0;
            try
            {
                int current;
                while (bytesRead != 4)
                {
                    current = await _stream.ReadAsync(sizeBuffer, 0 + bytesRead, 4 - bytesRead);
                    bytesRead += current;
                    if (current != 0) continue;
                    return null;
                }

                var size = BitConverter.ToInt32(sizeBuffer);
                bytesRead = 0;
                var readBuffer = new byte[size];
                while (bytesRead != size)
                {
                    current = await _stream.ReadAsync(readBuffer.AsMemory(0 + bytesRead, size - bytesRead));
                    bytesRead += current;
                    if (current != 0) continue;
                    return null;
                }

                var packet = Packet.Serializer.Parse(readBuffer);
                return packet;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        public void AddCompletionSource(int id, TaskCompletionSource<Packet> source)
        {
            _packetTasks.TryAdd(id, source);
        }

        private void StopConnection()
        {
            Console.WriteLine("Disconnected");
            Dispose();
            foreach (var (key, value) in _packetTasks)
                value.SetCanceled();

            _packetTasks.Clear();
            OnLinkDisconnected?.Invoke(this);
        }
    }
}