using System;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FlatBuffers;
using kurome;

namespace Kurome
{
    public class Link : IDisposable
    {
        private readonly TcpClient _client;
        private readonly SslStream _stream;

        public bool IsDisposed = false;

        public readonly FlatBufferBuilder BufferBuilder = new(1024);

        private readonly ConcurrentDictionary<int, TaskCompletionSource<Packet>> _packetTasks = new();
        private readonly X509Certificate2 _certificate = SslHelper.Certificate;

        public Link(TcpClient client)
        {
            _client = client;
            _stream = new SslStream(client.GetStream(), false);
            _stream.AuthenticateAsServer(_certificate, false, SslProtocols.None, true);
        }

        public async void StartListeningAsync()
        {
            while (true)
            {
                var packet = await GetPacketAsync();
                if (_packetTasks.ContainsKey(packet.Id))
                    _packetTasks[packet.Id].SetResult(packet);
                _packetTasks.TryRemove(packet.Id, out _);
            }
        }

        private async Task<int> ReadPrefixAsync()
        {
            var sizeBuffer = new byte[4];
            var bytesRead = 0;
            var current = -1;
            while (bytesRead != 4 && current != 0)
            {
                current = await _stream.ReadAsync(sizeBuffer, 0 + bytesRead, 4 - bytesRead);
                bytesRead += current;
                if (current == 0) //TODO: Handle graceful disconnect
                    Console.WriteLine("Disconnected");
            }

            return BitConverter.ToInt32(sizeBuffer);
        }


        public void SendBuffer(ByteBuffer buffer)
        {
            _stream.Write(buffer.ToSpan(buffer.Position, buffer.Length - buffer.Position));
        }

        private async Task<Packet> GetPacketAsync()
        {
            var size = await ReadPrefixAsync();
            var bytesRead = 0;
            var readBuffer = new byte[size];
            while (bytesRead != size)
            {
                var readTask = await _stream.ReadAsync(readBuffer.AsMemory(0 + bytesRead, size - bytesRead));
                bytesRead += readTask;
            }

            var bb = new ByteBuffer(readBuffer);
            var packet = Packet.GetRootAsPacket(bb);
            return packet;
        }

        public void AddCompletionSource(int id, TaskCompletionSource<Packet> source)
        {
            _packetTasks.TryAdd(id, source);
        }

        public void Dispose()
        {
            IsDisposed = true;
            _client.Close();
            _client.Dispose();
        }
    }
}