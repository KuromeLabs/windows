using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using FlatSharp;
using kurome;
using Kurome;

namespace Kurome
{
    public class Link : IDisposable
    {
        public readonly struct LinkContext
        {
            public readonly Packet Packet;
            private readonly byte[] _buffer;
            public readonly int Id;

            public LinkContext(Packet packet, byte[] buffer, int id)
            {
                Packet = packet;
                _buffer = buffer;
                Id = id;
            }

            public void Dispose()
            {
                ArrayPool<byte>.Shared.Return(_buffer);
            }
        }

        private readonly X509Certificate2 _certificate = SslHelper.Certificate;
        private readonly TcpClient _client;

        private readonly ConcurrentDictionary<int, TaskCompletionSource<LinkContext>> _packetTasks = new();
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
                var context = await GetContextAsync();
                if (context == null)
                {
                    StopConnection();
                    break;
                }

                if (_packetTasks.ContainsKey(context.Value.Id))
                    _packetTasks[context.Value.Id].SetResult(context.Value);
                _packetTasks.TryRemove(context.Value.Id, out _);
            }
        }

        public void SendBuffer(ReadOnlySpan<byte> buffer, int length)
        {
            _stream.Write(buffer[..length]);
        }

        private async Task<LinkContext?> GetContextAsync()
        {
            var sizeBuffer = new byte[4];
            var bytesRead = 0;
            try
            {
                int current;
                while (bytesRead != 4)
                {
                    current = await _stream.ReadAsync(sizeBuffer.AsMemory(0 + bytesRead, 4 - bytesRead));
                    bytesRead += current;
                    if (current != 0) continue;
                    return null;
                }

                var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
                bytesRead = 0;
                var readBuffer = ArrayPool<byte>.Shared.Rent(size);
                while (bytesRead != size)
                {
                    current = await _stream.ReadAsync(readBuffer.AsMemory(0 + bytesRead, size - bytesRead));
                    bytesRead += current;
                    if (current != 0) continue;
                    return null;
                }

                var packet = Packet.Serializer.Parse(readBuffer);
                return new LinkContext(packet, readBuffer, packet.Id);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        public void AddCompletionSource(int id, TaskCompletionSource<LinkContext> source)
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