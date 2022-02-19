using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DokanNet;
using FlatSharp;
using kurome;

namespace Kurome
{
    public class Device
    {
        public readonly char _driveLetter;
        private readonly Link _link;
        private readonly object _lock = new();
        private readonly Random _random = new();

        public Device(Link link, char driveLetter)
        {
            _link = link;
            _driveLetter = driveLetter;
        }

        public string Name { get; set; }
        public string Id { get; set; }

        public DeviceInfo GetSpace()
        {
            var id = _random.Next(int.MaxValue - 1) + 1;
            var packetCompletionSource = new TaskCompletionSource<Packet>();
            _link.AddCompletionSource(id, packetCompletionSource);
            SendPacket(action: ActionType.ActionGetSpaceInfo, id: id);
            var packet = packetCompletionSource.Task.Result;
            return packet.DeviceInfo!;
        }

        public List<FileInformation> GetFileNodes(string fileName)
        {
            var id = _random.Next(int.MaxValue - 1) + 1;
            var packetCompletionSource = new TaskCompletionSource<Packet>();
            _link.AddCompletionSource(id, packetCompletionSource);
            SendPacket(fileName, ActionType.ActionGetDirectory, id: id);
            var packet = packetCompletionSource.Task.Result;
            var files = new List<FileInformation>();
            for (var i = 0; i < packet.Nodes!.Count; i++)
            {
                var node = packet.Nodes![i];
                files.Add(new FileInformation
                {
                    FileName = node.Filename,
                    Attributes = node.FileType == FileType.Directory ? FileAttributes.Directory : FileAttributes.Normal,
                    LastAccessTime = DateTimeOffset.FromUnixTimeMilliseconds(node.LastAccessTime).LocalDateTime,
                    LastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(node.LastWriteTime).LocalDateTime,
                    CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(node.CreationTime).LocalDateTime,
                    Length = node.Length
                });
            }

            return files;
        }

        public FileInformation GetFileNode(string fileName)
        {
            var id = _random.Next(int.MaxValue - 1) + 1;
            var packetCompletionSource = new TaskCompletionSource<Packet>();
            _link.AddCompletionSource(id, packetCompletionSource);
            SendPacket(fileName, ActionType.ActionGetFileInfo, id: id);
            var packet = packetCompletionSource.Task.Result;

            var fileBuffer = packet.Nodes![0];
            return new FileInformation
            {
                FileName = fileBuffer.Filename,
                Attributes = fileBuffer.FileType == FileType.Directory
                    ? FileAttributes.Directory
                    : FileAttributes.Normal,
                LastAccessTime = DateTimeOffset.FromUnixTimeMilliseconds(fileBuffer.LastAccessTime).LocalDateTime,
                LastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(fileBuffer.LastWriteTime).LocalDateTime,
                CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(fileBuffer.CreationTime).LocalDateTime,
                Length = fileBuffer.Length
            };
        }

        public FileInformation GetRoot()
        {
            var result = GetFileNode("\\");
            result.FileName = "";
            return result;
        }

        public void CreateDirectory(string fileName)
        {
            SendPacket(fileName, ActionType.ActionCreateDirectory);
        }

        public void Delete(string fileName)
        {
            SendPacket(fileName, ActionType.ActionDelete);
        }

        public int ReceiveFileBuffer(byte[] buffer, string fileName, long offset, int bytesToRead, long fileSize)
        {
            var id = _random.Next(int.MaxValue - 1) + 1;
            var packetCompletionSource = new TaskCompletionSource<Packet>();
            _link.AddCompletionSource(id, packetCompletionSource);
            SendPacket(fileName, ActionType.ActionReadFileBuffer, rawOffset: offset,
                rawLength: bytesToRead, id: id);
            var packet = packetCompletionSource.Task.Result;
            packet.FileBuffer?.Data!.Value.CopyTo(buffer);
            return bytesToRead;
        }

        public void WriteFileBuffer(byte[] buffer, string fileName, long offset)
        {
            SendPacket(fileName, ActionType.ActionWriteFileBuffer, rawOffset: offset,
                rawBuffer: buffer, id: 0);
        }

        public void Rename(string oldName, string newName)
        {
            SendPacket(oldName, ActionType.ActionRename, newName);
        }

        public void SetLength(string fileName, long length)
        {
            SendPacket(nodeName: fileName, action: ActionType.ActionSetFileTime, fileLength: length);
        }

        public void SetFileTime(string fileName, long cTime, long laTime, long lwTime)
        {
            SendPacket(nodeName: fileName, action: ActionType.ActionSetFileTime,
                cTime: cTime, laTime: laTime, lwTime: lwTime);
        }

        public void CreateEmptyFile(string fileName)
        {
            SendPacket(fileName, ActionType.ActionCreateFile);
        }

        private void SendPacket(string filename = "", ActionType action = ActionType.NoAction,
            string nodeName = "", long cTime = 0, long laTime = 0, long lwTime = 0,
            FileType fileType = 0, long fileLength = 0, long rawOffset = 0, byte[] rawBuffer = null,
            int rawLength = 0, int id = 0)
        {
            filename = filename.Replace('\\', '/');
            nodeName = nodeName.Replace('\\', '/');
            var packet = new Packet
            {
                Action = action,
                Path = filename,
                FileBuffer = new Raw
                {
                    Data = rawBuffer,
                    Length = rawLength,
                    Offset = rawOffset
                },
                Nodes = new FileBuffer[]
                {
                    new()
                    {
                        CreationTime = cTime,
                        Filename = nodeName,
                        FileType = fileType,
                        LastAccessTime = laTime,
                        LastWriteTime = lwTime,
                        Length = fileLength
                    }
                },
                Id = id
            };
            var size = Packet.Serializer.GetMaxSize(packet);
            Span<byte> buffer = stackalloc byte[size];
            Packet.Serializer.Write(buffer, packet);
            _link.SendBuffer(buffer);
        }
    }
}