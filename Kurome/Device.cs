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
        private readonly IPAddress ip;
        private readonly char _driveLetter;
        private readonly object _readLock = new();
        private readonly object _writeLock = new();
        public string Name { get; private set; }
        public const byte ResultActionSuccess = 1;
        private const byte ActonGetEnumerateDirectory = 1;
        private const byte ActionGetSpaceInfo = 2;
        private const byte ActionGetFileType = 3;
        private const byte ActionWriteDirectory = 4;
        public const byte ResultFileIsDirectory = 5;
        public const byte ResultFileIsFile = 6;
        public const byte ResultFileNotFound = 7;
        private const byte ActionDelete = 8;

        public Device(TcpClient tcpClient, char driveLetter)
        {
            _networkStream = tcpClient.GetStream();
            ip = ((IPEndPoint) tcpClient.Client.RemoteEndPoint)!.Address;
            _driveLetter = driveLetter;
        }

        public void Initialize()
        {
            Name = ByteArrayToDecompressedString(ReadTcpWithTimeout(15));
        }

        public string GetSpace()
        {
            SendTcpPrefixed(ActionGetSpaceInfo, "");
            return ByteArrayToDecompressedString(ReadTcpWithTimeout(15));
        }

        public IEnumerable<FileNode> GetFileNodes(string fileName)
        {
            SendTcpPrefixed(ActonGetEnumerateDirectory, fileName.Replace('\\', '/'));
            return JsonSerializer.Deserialize<IEnumerable<FileNode>>(ByteArrayToDecompressedString(ReadTcpWithTimeout(15)));
        }

        public byte GetFileType(string fileName)
        {
            SendTcpPrefixed(ActionGetFileType, fileName.Replace('\\', '/'));
            return ReadTcpWithTimeout(15)[0];
        }

        public byte CreateDirectory(string fileName)
        {
            SendTcpPrefixed(ActionWriteDirectory, fileName.Replace('\\', '/'));
            return ReadTcpWithTimeout(15)[0];
        }

        public byte Delete(string fileName)
        {
            SendTcpPrefixed(ActionDelete, fileName.Replace('\\','/'));
            return ReadTcpWithTimeout(15)[0];
        }

        private void SendTcpPrefixed(byte action, string message)
        {
            lock (_writeLock)
            {
                _networkStream.Write(BitConverter.GetBytes(message.Length + 1)
                    .Concat(Encoding.UTF8.GetBytes(message).Prepend(action)).ToArray());
            }
        }

        private byte[] ReadTcpWithTimeout(int timeout)
        {
            try
            {
                lock (_readLock)
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

                    return buffer;
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

        private static string ByteArrayToDecompressedString(byte[] array)
        {
            if (array[0] != 0x1f || array[1] != 0x8b)
                return Encoding.UTF8.GetString(array, 0, array.Length);
            var decompressed = Decompress(array);
            return Encoding.UTF8.GetString(decompressed, 0, decompressed.Length);
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