using System;
using System.Collections.Generic;
using System.IO;
using DokanNet;
using FlatBuffers;
using kurome;

namespace Kurome
{
    public class Device
    {
        public Link ControlLink { get; }
        public readonly char _driveLetter;
        public string Name { get; set; }
        public string Id { get; set; }
        private readonly LinkPool _pool;

        public Device(Link controlLink, char driveLetter)
        {
            ControlLink = controlLink;
            _driveLetter = driveLetter;
            _pool = new LinkPool(this);
        }

        public DeviceInfo GetSpace()
        {
            var packet = ExchangePacket(action: ActionType.ActionGetSpaceInfo);
            return packet.DeviceInfo!.Value;
        }

        public List<FileInformation> GetFileNodes(string fileName)
        {
            var packet = ExchangePacket(fileName, ActionType.ActionGetDirectory);
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
            var packet = ExchangePacket(fileName, ActionType.ActionGetFileType);
            return packet.Result;
        }

        public byte CreateDirectory(string fileName)
        {
            var packet = ExchangePacket(fileName, ActionType.ActionCreateDirectory);
            return (byte) packet.Result;
        }

        public byte Delete(string fileName)
        {
            var packet = ExchangePacket(fileName, ActionType.ActionDelete);
            return (byte) packet.Result;
        }

        public int ReceiveFileBuffer(byte[] buffer, string fileName, long offset, int bytesToRead, long fileSize)
        {
            var packet = ExchangePacket(fileName, ActionType.ActionReadFileBuffer, rawOffset: offset,
                rawLength: bytesToRead);
            var rSpan = new Span<byte>(buffer);
            packet.FileBuffer?.GetDataBytes().CopyTo(rSpan);
            return bytesToRead;
        }

        public byte WriteFileBuffer(byte[] buffer, string fileName, long offset)
        {
            var packet = ExchangePacket(fileName, ActionType.ActionWriteFileBuffer, rawOffset: offset,
                rawBuffer: buffer);
            Console.WriteLine("buffer size: " + buffer.Length);
            return (byte) packet.Result;
        }

        public byte Rename(string oldName, string newName)
        {
            var packet = ExchangePacket(oldName, ActionType.ActionRename, newName);
            return (byte) packet.Result;
        }

        public byte SetLength(string fileName, long length)
        {
            var packet = ExchangePacket(nodeName: fileName, action: ActionType.ActionSetFileTime, fileLength: length);
            return (byte) packet.Result;
        }

        public byte SetFileTime(string fileName, long cTime, long laTime, long lwTime)
        {
            var packet = ExchangePacket(nodeName: fileName, action: ActionType.ActionSetFileTime,
                cTime: cTime, laTime: laTime, lwTime: lwTime);
            return (byte) packet.Result;
        }

        public FileNode GetFileInfo(string fileName)
        {
            var packet = ExchangePacket(fileName, ActionType.ActionGetFileInfo);
            var packetNode = packet.Nodes(0).GetValueOrDefault(new FileNode());
            return packetNode;
        }

        public byte CreateEmptyFile(string fileName)
        {
            var packet = ExchangePacket(fileName, ActionType.ActionCreateFile);
            return (byte) packet.Result;
        }

        private Packet ExchangePacket(string filename = "", ActionType action = ActionType.NoAction,
            string nodeName = "", long cTime = 0, long laTime = 0, long lwTime = 0,
            bool fileIsDirectory = false, long fileLength = 0, long rawOffset = 0, byte[] rawBuffer = null,
            long rawLength = 0)
        {
            filename = filename.Replace('\\', '/');
            nodeName = nodeName.Replace('\\', '/');
            var link = _pool.Get();
            var builder = link.BufferBuilder;
            var byteVector = new VectorOffset(0);
            if (rawBuffer != null) byteVector = Raw.CreateDataVector(builder, rawBuffer);
            var raw = Raw.CreateRaw(builder, byteVector, rawOffset, rawLength);

            var nodesOffset = new Offset<FileNode>[1];
            var nodeNameOffset = builder.CreateString(nodeName);
            nodesOffset[0] = FileNode.CreateFileNode(builder, nodeNameOffset, fileIsDirectory, fileLength, cTime,
                laTime, lwTime);
            var nodesVector = Packet.CreateNodesVector(builder, nodesOffset);

            var path = builder.CreateString(filename);
            var packet = Packet.CreatePacket(builder, path, action, default, default, raw, nodesVector);
            builder.FinishSizePrefixed(packet.Value);
            link.SendBuffer(builder.DataBuffer);
            builder.Clear();
            var res = link.GetPacket();
            _pool.Return(link);
            return res;
        }
    }
}