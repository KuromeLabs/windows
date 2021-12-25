using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using FlatBuffers;
using kurome;

namespace Kurome
{
    public class Link: IDisposable
    {
        private readonly TcpClient _client;
        public bool IsDisposed = false;
        public readonly FlatBufferBuilder BufferBuilder = new(1024);
        private byte[] _readBuffer = new byte[16384];
        private readonly byte[] _sizeBuffer = new byte[4];

        public Link(TcpClient client)
        {
            _client = client;
        }

        public void WritePrefixed(byte buffer)
        {
            _client.GetStream().Write(BitConverter.GetBytes(1).Append(buffer).ToArray());
        }

        public byte[] ReadFullPrefixed(int timeout)
        {
            var readPrefixTask = ReadPrefix();
            Task.WaitAny(readPrefixTask, Task.Delay(TimeSpan.FromSeconds(timeout)));
            if (!readPrefixTask.IsCompleted)
                throw new TimeoutException();
            var size = BitConverter.ToInt32(_sizeBuffer);
            var bytesRead = 0;
            if (_readBuffer.Length < size)
                Array.Resize(ref _readBuffer, size + 4);
            while (bytesRead != size)
            {
                var readTask = _client.GetStream().Read(_readBuffer, 0 + bytesRead, size - bytesRead);
                bytesRead += readTask;
            }
            return _readBuffer;
        }

        private async Task ReadPrefix()
        {
            var bytesRead = 0;
            while (bytesRead != 4)
                bytesRead += await _client.GetStream().ReadAsync(_sizeBuffer, 0 + bytesRead, 4 - bytesRead);
        }


        public void SendBuffer(ByteBuffer buffer)
        {
            _client.GetStream().Write(buffer.ToSpan(buffer.Position, buffer.Length - buffer.Position));
        }

        public Packet GetPacket()
        {
            ReadFullPrefixed(15);
            var bb = new ByteBuffer(_readBuffer);
            var packet = Packet.GetRootAsPacket(bb);
            return packet;
        }
        public void Dispose()
        {
            IsDisposed = true;
            _client.Close();
            _client.Dispose();
        }
    }
}