using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DokanNet;
using FlatBuffers;
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
            return packet.DeviceInfo!.Value;
        }

        public List<FileInformation> GetFileNodes(string fileName)
        {
            var id = _random.Next(int.MaxValue - 1) + 1;
            var packetCompletionSource = new TaskCompletionSource<Packet>();
            _link.AddCompletionSource(id, packetCompletionSource);
            SendPacket(fileName, ActionType.ActionGetDirectory, id: id);
            var packet = packetCompletionSource.Task.Result;
            var files = new List<FileInformation>();
            for (var i = 0; i < packet.NodesLength; i++)
            {
                var node = packet.Nodes(i)!.Value;
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

            var fileBuffer = packet.Nodes(0)!.Value;
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
            var rSpan = new Span<byte>(buffer);
            packet.FileBuffer?.GetDataBytes().CopyTo(rSpan);
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

        public FileBuffer GetFileInfo(string fileName)
        {
            var id = _random.Next(int.MaxValue - 1) + 1;
            var packetCompletionSource = new TaskCompletionSource<Packet>();
            _link.AddCompletionSource(id, packetCompletionSource);
            SendPacket(fileName, ActionType.ActionGetFileInfo, id: id);
            var packet = packetCompletionSource.Task.Result;

            var packetNode = packet.Nodes(0).GetValueOrDefault(new FileBuffer());
            return packetNode;
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
            lock (_lock)
            {
                filename = filename.Replace('\\', '/');
                nodeName = nodeName.Replace('\\', '/');
                var builder = _link.BufferBuilder;
                var byteVector = new VectorOffset(0);
                if (rawBuffer != null) byteVector = Raw.CreateDataVector(builder, rawBuffer);
                var raw = Raw.CreateRaw(builder, byteVector, rawOffset, rawLength);

                var nodesOffset = new Offset<FileBuffer>[1];
                var nodeNameOffset = builder.CreateString(nodeName);
                nodesOffset[0] = FileBuffer.CreateFileBuffer(builder, nodeNameOffset, fileType, fileLength, cTime,
                    laTime, lwTime);
                var nodesVector = Packet.CreateNodesVector(builder, nodesOffset);

                var path = builder.CreateString(filename);
                var packet = Packet.CreatePacket(builder, path, action, default, default, raw, nodesVector, id);
                builder.FinishSizePrefixed(packet.Value);
                _link.SendBuffer(builder.DataBuffer);
                builder.Clear();
            }
        }
    }
}