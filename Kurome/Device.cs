using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using DefaultNamespace;
using DokanNet;
using FlatSharp;
using kurome;
using Action = kurome.Action;

namespace Kurome
{
    public class Device
    {
        public char DriveLetter;
        private Link _link;
        private readonly object _lock = new();
        private readonly Random _random = new();
        public bool IsPaired = false;
        public delegate void PacketReceived(Packet packet);

        private PairingHandler _pairingHandler;

        public event PairingHandler.PairStatusDelegate OnPairStatus;

        public Device()
        {
        }

        public Device(Link link)
        {
            SetLink(link);
        }

        public void SetLink(Link link)
        {
            _link = link;
            _link.OnPacketReceived += OnPacketReceived;
            _pairingHandler = new PairingHandler(this);
            
        }

        private void OnPacketReceived(Packet packet)
        {
            if (packet.Action == Action.ActionPair)
            {
                _pairingHandler.PairPacketReceived(packet);
            }
        }
        public string Name { get; set; }
        public string Id { get; set; }

        public DeviceInfo GetSpace()
        {
            var response = SendPacketWithResponse(action: Action.ActionGetSpaceInfo);
            var deviceInfo = new DeviceInfo(response.Packet.DeviceInfo!);
            response.Dispose();
            return deviceInfo;
        }

        public List<FileInformation> GetFileNodes(string fileName)
        {
            var response = SendPacketWithResponse(fileName, Action.ActionGetDirectory);
            var files = new List<FileInformation>();
            for (var i = 0; i < response.Packet.Nodes!.Count; i++)
            {
                var node = response.Packet.Nodes![i];
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

            response.Dispose();
            return files;
        }

        private FileInformation GetFileNode(string fileName)
        {
            var response = SendPacketWithResponse(fileName, Action.ActionGetFileInfo);
            var file = response.Packet.Nodes![0];
            var fileInfo = new FileInformation
            {
                FileName = file.Filename,
                Attributes = file.FileType == FileType.Directory ? FileAttributes.Directory : FileAttributes.Normal,
                LastAccessTime = DateTimeOffset.FromUnixTimeMilliseconds(file.LastAccessTime).LocalDateTime,
                LastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(file.LastWriteTime).LocalDateTime,
                CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(file.CreationTime).LocalDateTime,
                Length = file.Length
            };
            response.Dispose();
            return fileInfo;
        }

        public FileInformation GetRoot()
        {
            var result = GetFileNode("\\");
            result.FileName = "";
            return result;
        }

        public void CreateDirectory(string fileName)
        {
            SendPacket(fileName, Action.ActionCreateDirectory);
        }

        public void Delete(string fileName)
        {
            SendPacket(fileName, Action.ActionDelete);
        }

        public int ReceiveFileBuffer(byte[] buffer, string fileName, long offset, int bytesToRead, long fileSize)
        {
            var response = SendPacketWithResponse(fileName, Action.ActionReadFileBuffer, rawOffset: offset,
                rawLength: bytesToRead);
            response.Packet.FileBuffer?.Data!.Value.CopyTo(buffer);
            response.Dispose();
            return bytesToRead;
        }

        public void WriteFileBuffer(byte[] buffer, string fileName, long offset)
        {
            SendPacket(fileName, Action.ActionWriteFileBuffer, rawOffset: offset, rawBuffer: buffer, id: 0);
        }

        public void Rename(string oldName, string newName)
        {
            SendPacket(oldName, Action.ActionRename, newName);
        }

        public void SetLength(string fileName, long length)
        {
            SendPacket(fileName, Action.ActionSetFileTime, fileLength: length);
        }

        public void SetFileTime(string fileName, long cTime, long laTime, long lwTime)
        {
            SendPacket(fileName, Action.ActionSetFileTime, cTime: cTime, laTime: laTime, lwTime: lwTime);
        }

        public void CreateEmptyFile(string fileName)
        {
            SendPacket(fileName, Action.ActionCreateFile);
        }

        public void SendPacket(string filename = "", Action action = Action.NoAction,
            string nodeName = "", long cTime = 0, long laTime = 0, long lwTime = 0, FileType fileType = 0,
            long fileLength = 0, long rawOffset = 0, byte[] rawBuffer = null, int rawLength = 0, int id = 0, PairEvent pair = 0)
        {
            lock (_lock)
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
                    Id = id,
                    Pair = pair,
                    DeviceInfo = new DeviceInfo
                    {
                        FreeBytes = 0,
                        Id = IdentityProvider.GetGuid(),
                        Name = IdentityProvider.GetMachineName(),
                        TotalBytes = 0
                    }
                };
                var size = Packet.Serializer.GetMaxSize(packet);
                var bytes = ArrayPool<byte>.Shared.Rent(size + 4);
                Span<byte> buffer = bytes;
                var length = Packet.Serializer.Write(buffer[4..], packet);
                BinaryPrimitives.WriteInt32LittleEndian(buffer[..4], length);
                _link.SendBuffer(buffer, length + 4);
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }

        private LinkContext SendPacketWithResponse(string filename = "", Action action = Action.NoAction,
            string nodeName = "", long cTime = 0, long laTime = 0, long lwTime = 0, FileType fileType = 0,
            long fileLength = 0, long rawOffset = 0, byte[] rawBuffer = null, int rawLength = 0)
        {
            lock (_lock)
            {
                var packetId = _random.Next(int.MaxValue - 1) + 1;
                var responseEvent = new ManualResetEventSlim(false);
                var context = new LinkContext(packetId, responseEvent);
                _link.AddLinkContextWait(packetId, context);
                SendPacket(filename, action, nodeName, cTime, laTime, lwTime, fileType, fileLength, rawOffset,
                    rawBuffer,
                    rawLength, packetId);
                responseEvent.Wait();
                return context;
            }
        }

        public void AcceptPairing()
        {
            SendPacket(action: Action.ActionPair, pair: PairEvent.Pair);
            OnPairStatus?.Invoke(PairingHandler.PairStatus.Paired, this);
        }
    }
}