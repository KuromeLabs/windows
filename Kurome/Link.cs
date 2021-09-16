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
        private byte[] _writeBuffer = new byte[16384];
        private byte[] _readBuffer = new byte[16384];

        public Link(TcpClient client)
        {
            _client = client;
        }

        public void WritePrefixedFileBuffer(byte[] buffer, string fileName, long offset)
        {
            var fileNameOffsetBytes = Encoding.UTF8.GetBytes($"{fileName.Replace('\\', '/')}:{offset.ToString()}:");
            var size = buffer.Length + fileNameOffsetBytes.Length + 1;
            var sizeBytes = BitConverter.GetBytes(size);
            if (buffer.Length + fileNameOffsetBytes.Length + 5 > _writeBuffer.Length)
                Array.Resize(ref _writeBuffer, buffer.Length + fileNameOffsetBytes.Length + 5);
            Buffer.BlockCopy(sizeBytes, 0, _writeBuffer, 0, 4);
            _writeBuffer[4] = Packets.ActionWriteFileBuffer;
            Buffer.BlockCopy(fileNameOffsetBytes, 0, _writeBuffer, 5, fileNameOffsetBytes.Length);
            Buffer.BlockCopy(buffer, 0, _writeBuffer, 5 + fileNameOffsetBytes.Length, buffer.Length);
            _client.GetStream().Write(_writeBuffer, 0, size + 4);
        }
        public void WritePrefixed(byte[] buffer)
        {
            WriteToLinkBuffer(ref buffer);
            _client.GetStream().Write(_writeBuffer, 0, buffer.Length + 4);
        }
        public void WritePrefixed(byte buffer)
        {
            _client.GetStream().Write(BitConverter.GetBytes(1).Append(buffer).ToArray());
        }

        public byte[] ReadFullPrefixed(int timeout)
        {
            return ReadFullPrefixed(timeout, out _);
        }
        public byte[] ReadFullPrefixed(int timeout, out int size)
        {
            var sizeBuffer = new byte[4];
            var readPrefixTask = Task.Run(() =>
            {
                var bytesRead = 0;
                while (bytesRead != 4)
                    bytesRead += _client.GetStream().Read(sizeBuffer, 0 + bytesRead, 4 - bytesRead);
            });
            Task.WaitAny(readPrefixTask, Task.Delay(TimeSpan.FromSeconds(timeout)));
            if (!readPrefixTask.IsCompleted)
                throw new TimeoutException();
            size = BitConverter.ToInt32(sizeBuffer);
            var bytesRead = 0;
            if (_readBuffer.Length < size)
                Array.Resize(ref _readBuffer, size + 4);
            while (bytesRead != size)
            {
                var readTask = _client.GetStream().Read(_readBuffer, 0 + bytesRead, size - bytesRead);
                bytesRead += readTask;
            }

            if (_readBuffer[0] != 0x1f || _readBuffer[1] != 0x8b)
                return _readBuffer;
            else
                return Decompress(_readBuffer);
        }
        
        public string BufferToString(byte[] array, int size)
        {
            return Encoding.UTF8.GetString(array, 0, size);
        }

        private byte[] Decompress(byte[] compressedData)
        {
            Console.WriteLine("Decompressing. Compressed size: " + compressedData.Length);
            var outputStream = new MemoryStream();
            using var compressedStream = new MemoryStream(compressedData);
            using var sr = new GZipStream(compressedStream, CompressionMode.Decompress);
            sr.CopyTo(outputStream);
            outputStream.Position = 0;
            return outputStream.ToArray();
        }

        private void WriteToLinkBuffer(ref byte[] buffer)
        {
            if (buffer.Length + 4 > _writeBuffer.Length)
                Array.Resize(ref _writeBuffer, buffer.Length + 4);
            var sizeBytes = BitConverter.GetBytes(buffer.Length);
            Buffer.BlockCopy(sizeBytes, 0, _writeBuffer, 0, 4);
            Buffer.BlockCopy(buffer, 0, _writeBuffer, 4, buffer.Length);
        }

        public void Dispose()
        {
            IsDisposed = true;
            _client.Close();
            _client.Dispose();
        }
    }
}