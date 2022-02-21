using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FlatSharp;
using kurome;

namespace Kurome
{public class LinkContext
    {
        public Packet Packet;
        public byte[] Buffer;
        public ManualResetEventSlim ResponseEvent;

        public LinkContext(int id, ManualResetEventSlim responseEvent)
        {
            ResponseEvent = responseEvent;
            Packet = null;
            Buffer = null;
        }

        public void Dispose()
        {
            ResponseEvent.Dispose();
            ArrayPool<byte>.Shared.Return(Buffer);
        }
    }
    public class Link : IDisposable
    {

        private readonly X509Certificate2 _certificate = SslHelper.Certificate;
        private readonly TcpClient _client;

        private readonly ConcurrentDictionary<int, LinkContext> _linkContexts = new();
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
                var buffer = await GetBufferAsync();
                if (buffer == null)
                {
                    StopConnection();
                    break;
                }

                var packet = Packet.Serializer.Parse(buffer);
                if (_linkContexts.ContainsKey(packet.Id))
                {
                    _linkContexts[packet.Id].Packet = packet;
                    _linkContexts[packet.Id].Buffer = buffer;
                    _linkContexts[packet.Id].ResponseEvent.Set();
                    _linkContexts.TryRemove(packet.Id, out _);
                }
            }
        }

        public void SendBuffer(ReadOnlySpan<byte> buffer, int length)
        {
            _stream.Write(buffer[..length]);
        }

        private async Task<byte[]?> GetBufferAsync()
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

                return readBuffer;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        public void AddLinkContextWait(int id, LinkContext source)
        {
            _linkContexts.TryAdd(id, source);
        }

        private void StopConnection()
        {
            Console.WriteLine("Disconnected");
            Dispose();
            foreach (var (key, value) in _linkContexts)
                value.Dispose();

            _linkContexts.Clear();
            OnLinkDisconnected?.Invoke(this);
        }
    }
}