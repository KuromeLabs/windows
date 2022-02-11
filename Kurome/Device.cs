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
        public string Name { get; set; }
        public string Id { get; set; }
        private readonly Link _link;
        private readonly Random _random = new();
        private readonly object _lock = new();
        public Device(Link link, char driveLetter)
        {
            _link = link;
            _driveLetter = driveLetter;
        }

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
                    Attributes = node.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal,
                    LastAccessTime = DateTimeOffset.FromUnixTimeMilliseconds(node.LastAccessTime).LocalDateTime,
                    LastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(node.LastWriteTime).LocalDateTime,
                    CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(node.CreationTime).LocalDateTime,
                    Length = node.Length
                });
            }
            return files;
        }

        public ResultType GetFileType(string fileName)
        {
            var id = _random.Next(int.MaxValue - 1) + 1;
            // Console.WriteLine("Called getfiletype id: " + id);
            var packetCompletionSource = new TaskCompletionSource<Packet>();
            _link.AddCompletionSource(id, packetCompletionSource);
            SendPacket(fileName, ActionType.ActionGetFileType, id: id);
            var packet = packetCompletionSource.Task.Result;
            return packet.Result;
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

        public FileNode GetFileInfo(string fileName)
        {
            var id = _random.Next(int.MaxValue - 1) + 1;
            var packetCompletionSource = new TaskCompletionSource<Packet>();
            _link.AddCompletionSource(id, packetCompletionSource);
            SendPacket(fileName, ActionType.ActionGetFileInfo, id: id);
            var packet = packetCompletionSource.Task.Result;

            var packetNode = packet.Nodes(0).GetValueOrDefault(new FileNode());
            return packetNode;
        }

        public void CreateEmptyFile(string fileName)
        {
            SendPacket(fileName, ActionType.ActionCreateFile);
        }

        private void SendPacket(string filename = "", ActionType action = ActionType.NoAction,
            string nodeName = "", long cTime = 0, long laTime = 0, long lwTime = 0,
            bool fileIsDirectory = false, long fileLength = 0, long rawOffset = 0, byte[] rawBuffer = null,
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

                var nodesOffset = new Offset<FileNode>[1];
                var nodeNameOffset = builder.CreateString(nodeName);
                nodesOffset[0] = FileNode.CreateFileNode(builder, nodeNameOffset, fileIsDirectory, fileLength, cTime,
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