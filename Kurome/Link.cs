using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Kurome
{
    public class Link: IDisposable
    {
        private readonly TcpClient _client;
        public bool IsDisposed = false;

        public Link(TcpClient client)
        {
            _client = client;
        }
        public void WritePrefixed(byte action, string message)
        {
            _client.GetStream().Write(BitConverter.GetBytes(message.Length + 1)
                    .Concat(Encoding.UTF8.GetBytes(message).Prepend(action)).ToArray());
        }
        public byte[] ReadFullPrefixed(int timeout)
        {
            var sizeBuffer = new byte[4];
            var readPrefixTask = _client.GetStream().ReadAsync(sizeBuffer, 0, 4);
            Task.WaitAny(readPrefixTask, Task.Delay(TimeSpan.FromSeconds(timeout)));
            if (!readPrefixTask.IsCompleted)
                throw new TimeoutException();
            var size = BitConverter.ToInt32(sizeBuffer);
            var bytesRead = 0;
            var buffer = new byte[size];
            while (bytesRead != size)
            {
                var readTask = _client.GetStream().Read(buffer, 0 + bytesRead, buffer.Length - bytesRead);
                bytesRead += readTask;
            }

            return buffer;
        }
        
        private string ByteArrayToDecompressedString(byte[] array)
        {
            if (array[0] != 0x1f || array[1] != 0x8b)
                return Encoding.UTF8.GetString(array, 0, array.Length);
            var decompressed = Decompress(array);
            return Encoding.UTF8.GetString(decompressed, 0, decompressed.Length);
        }

        private byte[] Decompress(byte[] compressedData)
        {
            var outputStream = new MemoryStream();
            using var compressedStream = new MemoryStream(compressedData);
            using var sr = new GZipStream(compressedStream, CompressionMode.Decompress);
            sr.CopyTo(outputStream);
            outputStream.Position = 0;
            return outputStream.ToArray();
        }

        public void Dispose()
        {
            IsDisposed = true;
            _client.Close();
            _client.Dispose();
        }
    }
}