using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Kurome
{
    public class Device
    {
        public Link ControlLink { get; }
        private readonly char _driveLetter;
        private string Name { get; set; }
        private string Id { get; set; }
        private const int Timeout = 10;
        private readonly LinkPool _pool;

        public Device(Link controlLink, char driveLetter)
        {
            ControlLink = controlLink;
            _driveLetter = driveLetter;
            _pool = new LinkPool(this);
        }

        public string GetDeviceName()
        {
            if (Name != null) return Name;
            var link = _pool.Get();
            link.WritePrefixed(Packets.ActionGetDeviceName);
            Name = link.BufferToString(link.ReadFullPrefixed(Timeout));
            _pool.Return(link);
            return Name;
        }

        public string GetDeviceId()
        {
            if (Id != null) return Id;
            var link = _pool.Get();
            link.WritePrefixed(Packets.ActionGetDeviceId);
            Id = link.BufferToString(link.ReadFullPrefixed(Timeout));
            _pool.Return(link);
            return Id;
        }

        public string GetSpace()
        {
            var link = _pool.Get();
            link.WritePrefixed(Packets.ActionGetSpaceInfo);
            var result = link.BufferToString(link.ReadFullPrefixed(Timeout));
            _pool.Return(link);
            return result;
        }

        public IEnumerable<FileNode> GetFileNodes(string fileName)
        {
            var link = _pool.Get();
            link.WritePrefixed(Encoding.UTF8.GetBytes(fileName.Replace('\\', '/'))
                .Prepend(Packets.ActonGetEnumerateDirectory)
                .ToArray());
            var result = JsonSerializer.Deserialize<IEnumerable<FileNode>>(
                link.BufferToString(link.ReadFullPrefixed(Timeout)));
            _pool.Return(link);
            return result;
        }

        public byte GetFileType(string fileName)
        {
            var link = _pool.Get();
            link.WritePrefixed(Encoding.UTF8.GetBytes(fileName.Replace('\\', '/'))
                .Prepend(Packets.ActionGetFileType)
                .ToArray());
            var result = link.ReadFullPrefixed(Timeout)[0];
            _pool.Return(link);
            return result;
        }

        public byte CreateDirectory(string fileName)
        {
            var link = _pool.Get();
            link.WritePrefixed(Encoding.UTF8.GetBytes(fileName.Replace('\\', '/'))
                .Prepend(Packets.ActionWriteDirectory)
                .ToArray());
            var result = link.ReadFullPrefixed(Timeout)[0];
            _pool.Return(link);
            return result;
        }

        public byte Delete(string fileName)
        {
            var link = _pool.Get();
            link.WritePrefixed(Encoding.UTF8.GetBytes(fileName.Replace('\\', '/'))
                .Prepend(Packets.ActionDelete)
                .ToArray());
            var result = link.ReadFullPrefixed(Timeout)[0];
            _pool.Return(link);
            return result;
        }

        public int ReceiveFileBuffer(ref byte[] buffer, string fileName, long offset, int bytesToRead, long fileSize)
        {
            var link = _pool.Get();
            link.WritePrefixed(Encoding.UTF8.GetBytes(fileName.Replace('\\', '/') + ':' + offset + ':' + bytesToRead)
                .Prepend(Packets.ActionSendToServer)
                .ToArray());
            link.ReadFullPrefixed(Timeout).CopyTo(buffer, 0);
            _pool.Return(link);
            return bytesToRead;
        }

        public byte WriteFileBuffer(byte[] buffer, string fileName, long offset)
        {
            var link = _pool.Get();
            link.WritePrefixed(Encoding.UTF8.GetBytes(fileName.Replace('\\', '/') + ':' + offset + ':')
                .Concat(buffer)
                .Prepend(Packets.ActionWriteFileBuffer)
                .ToArray());
            var result = link.ReadFullPrefixed(Timeout)[0];
            _pool.Return(link);
            return result;
        }
        public byte Rename(string oldName, string newName){
            var link = _pool.Get();
            link.WritePrefixed(Encoding.UTF8.GetBytes(oldName.Replace('\\', '/') + ':' + 
                                                      newName.Replace('\\', '/'))
                .Prepend(Packets.ActionRename)
                .ToArray());
            var result = link.ReadFullPrefixed(Timeout)[0];
            _pool.Return(link);
            return result;
        }

        public byte SetLength(string fileName, long length)
        {
            var link = _pool.Get();
            link.WritePrefixed(Encoding.UTF8.GetBytes(fileName.Replace('\\', '/') + ':' + length)
                .Prepend(Packets.ActionSetLength)
                .ToArray());
            var result = link.ReadFullPrefixed(Timeout)[0];
            _pool.Return(link);
            return result;
        }

        public FileNode GetFileInfo(string fileName)
        {
            var link = _pool.Get();
            link.WritePrefixed(Encoding.UTF8.GetBytes(fileName.Replace('\\', '/'))
                .Prepend(Packets.ActionGetFileInfo)
                .ToArray());
            var result = JsonSerializer.Deserialize<FileNode>(link.BufferToString(link.ReadFullPrefixed(Timeout)));
            _pool.Return(link);
            return result;
        }
    }
}