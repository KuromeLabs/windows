using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DokanNet;

namespace Kurome
{
    public class Device
    {
        private readonly NetworkStream _networkStream;
        private readonly char _driveLetter;
        private readonly object _lock = new();
        public string Name { get; private set; }
        private const byte ActonGetEnumerateDirectory = 1;
        private const byte ActionGetSpaceInfo = 2;
        private const byte ActionGetFileType = 3;
        private const byte ActionWriteDirectory = 4;

        public Device(NetworkStream networkStream, char driveLetter)
        {
            _networkStream = networkStream;
            _driveLetter = driveLetter;
        }

        public void Initialize()
        {
            Name = ReadTcpWithTimeout(15);
        }

        public string GetSpace()
        {
            SendTcpPrefixed(ActionGetSpaceInfo, "");
            return ReadTcpWithTimeout(15);
        }

        public IEnumerable<FileNode> GetFileNodes(string fileName)
        {
            SendTcpPrefixed(ActonGetEnumerateDirectory,fileName.Replace('\\', '/'));
            return JsonSerializer.Deserialize<IEnumerable<FileNode>>(ReadTcpWithTimeout(15));
        }

        public string GetFileType(string fileName)
        {
            SendTcpPrefixed(ActionGetFileType, fileName.Replace('\\', '/'));
            return ReadTcpWithTimeout(15);
        }

        public string CreateDirectory(string fileName)
        {
            SendTcpPrefixed(ActionWriteDirectory,fileName.Replace('\\', '/'));
            return ReadTcpWithTimeout(15);
        }

        private void SendTcpPrefixed(byte action, string message)
        {
            lock (_lock)
            {
                _networkStream.Write(BitConverter.GetBytes(message.Length + 1)
                    .Concat(Encoding.UTF8.GetBytes(message).Prepend(action)).ToArray());
            }
        }

        private string ReadTcpWithTimeout(int timeout)
        {
            try
            {
                lock (_lock)
                {
                    var sizeBuffer = new byte[4];
                    var readPrefixTask = _networkStream.ReadAsync(sizeBuffer, 0, 4);
                    Task.WaitAny(readPrefixTask, Task.Delay(TimeSpan.FromSeconds(timeout)));

                    var size = BitConverter.ToInt32(sizeBuffer);
                    var bytesRead = 0;
                    var buffer = new byte[size];
                    while (bytesRead != size)
                    {
                        var readTask = _networkStream.Read(buffer, 0 + bytesRead, buffer.Length - bytesRead);
                        bytesRead += readTask;
                    }

                    if (buffer[0] != 0x1f || buffer[1] != 0x8b)
                        return Encoding.UTF8.GetString(buffer, 0, size);
                    var decompressed = Decompress(buffer);
                    return Encoding.UTF8.GetString(decompressed, 0, decompressed.Length);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Console.WriteLine("Client disconnected");
            Dokan.Unmount(_driveLetter);
            return null;
        }

        private static byte[] Decompress(byte[] compressedData)
        {
            var outputStream = new MemoryStream();
            using var compressedStream = new MemoryStream(compressedData);
            using var sr = new GZipStream(compressedStream, CompressionMode.Decompress);
            sr.CopyTo(outputStream);
            outputStream.Position = 0;
            return outputStream.ToArray();
        }
    }
}