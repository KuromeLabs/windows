using System;
using System.Collections.Generic;
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
        public NetworkStream FileStream { get; set; }
        private readonly char _driveLetter;
        private readonly object _readLock = new();
        private readonly object _writeLock = new();
        public string Name { get; private set; }

        public Device(TcpClient tcpClient, char driveLetter)
        {
            _networkStream = tcpClient.GetStream();
            _driveLetter = driveLetter;
        }

        public string GetDeviceName()
        {
            if (Name != null) return Name;
            SendTcpPrefixed(Packets.ActionGetDeviceName,"");
            Name = ByteArrayToDecompressedString(ReadFullStreamPrefixed(15));
            return Name;
        }

        public string GetSpace()
        {
            SendTcpPrefixed(Packets.ActionGetSpaceInfo, "");
            return ByteArrayToDecompressedString(ReadFullStreamPrefixed(15));
        }

        public IEnumerable<FileNode> GetFileNodes(string fileName)
        {
            SendTcpPrefixed(Packets.ActonGetEnumerateDirectory, fileName.Replace('\\', '/'));
            return JsonSerializer.Deserialize<IEnumerable<FileNode>>(
                ByteArrayToDecompressedString(ReadFullStreamPrefixed(15)));
        }

        public byte GetFileType(string fileName)
        {
            SendTcpPrefixed((Packets.ActionGetFileType), fileName.Replace('\\', '/'));
            return ReadFullStreamPrefixed(15)[0];
        }

        public byte CreateDirectory(string fileName)
        {
            SendTcpPrefixed(Packets.ActionWriteDirectory, fileName.Replace('\\', '/'));
            return ReadFullStreamPrefixed(15)[0];
        }

        public byte Delete(string fileName)
        {
            SendTcpPrefixed(Packets.ActionDelete, fileName.Replace('\\', '/'));
            return ReadFullStreamPrefixed(15)[0];
        }

        public NetworkStream ReceiveFileStream(string fileName, long offset, int size)
        {
            if (FileStream != null) return FileStream;
            var tcpListener = TcpListener.Create(33588);
            tcpListener.Start();
            SendTcpPrefixed(Packets.ActionSendToServer, fileName.Replace('\\', '/') + ':' + offset + ':' + size);
            var client = tcpListener.AcceptTcpClient();
            tcpListener.Stop();
            FileStream = client.GetStream();
            return FileStream;
        }

        public FileNode GetFileInfo(string filename)
        {
            SendTcpPrefixed(Packets.ActionGetFileInfo, filename.Replace('\\', '/'));
            return JsonSerializer.Deserialize<FileNode>(ByteArrayToDecompressedString(ReadFullStreamPrefixed(15)));
        }

        private void SendTcpPrefixed(byte action, string message)
        {
            lock (_writeLock)
            {
                _networkStream.Write(BitConverter.GetBytes(message.Length + 1)
                    .Concat(Encoding.UTF8.GetBytes(message).Prepend(action)).ToArray());
            }
        }

        private byte[] ReadFullStreamPrefixed(int timeout)
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