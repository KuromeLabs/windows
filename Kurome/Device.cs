using System.Collections.Generic;
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
            link.WritePrefixed(Packets.ActionGetDeviceName, "");
            Name = link.BufferToString(link.ReadFullPrefixed(Timeout));
            _pool.Return(link);
            return Name;
        }

        public string GetDeviceId()
        {
            if (Id != null) return Id;
            var link = _pool.Get();
            link.WritePrefixed(Packets.ActionGetDeviceId, "");
            Id = link.BufferToString(link.ReadFullPrefixed(Timeout));
            _pool.Return(link);
            return Id;
        }

        public string GetSpace()
        {
            var link = _pool.Get();
            link.WritePrefixed(Packets.ActionGetSpaceInfo, "");
            var result = link.BufferToString(link.ReadFullPrefixed(Timeout));
            _pool.Return(link);
            return result;
        }

        public IEnumerable<FileNode> GetFileNodes(string fileName)
        {
            var link = _pool.Get();
            link.WritePrefixed(Packets.ActonGetEnumerateDirectory, fileName.Replace('\\', '/'));
            var result = JsonSerializer.Deserialize<IEnumerable<FileNode>>(
                link.BufferToString(link.ReadFullPrefixed(Timeout)));
            _pool.Return(link);
            return result;
        }

        public byte GetFileType(string fileName)
        {
            var link = _pool.Get();
            link.WritePrefixed((Packets.ActionGetFileType), fileName.Replace('\\', '/'));
            var result = link.ReadFullPrefixed(Timeout)[0];
            _pool.Return(link);
            return result;
        }

        public byte CreateDirectory(string fileName)
        {
            var link = _pool.Get();
            link.WritePrefixed(Packets.ActionWriteDirectory, fileName.Replace('\\', '/'));
            var result = link.ReadFullPrefixed(Timeout)[0];
            _pool.Return(link);
            return result;
        }

        public byte Delete(string fileName)
        {
            var link = _pool.Get();
            link.WritePrefixed(Packets.ActionDelete, fileName.Replace('\\', '/'));
            var result = link.ReadFullPrefixed(Timeout)[0];
            _pool.Return(link);
            return result;
        }

        public int ReceiveFileBuffer(ref byte[] buffer, string fileName, long offset, int bytesToRead, long fileSize)
        {
            var link = _pool.Get();
            link.WritePrefixed(Packets.ActionSendToServer,
                fileName.Replace('\\', '/') + ':' + offset + ':' + bytesToRead);
            link.ReadFullPrefixed(Timeout).CopyTo(buffer, 0);
            _pool.Return(link);
            return bytesToRead;
        }

        public FileNode GetFileInfo(string filename)
        {
            var link = _pool.Get();
            link.WritePrefixed(Packets.ActionGetFileInfo, filename.Replace('\\', '/'));
            var result = JsonSerializer.Deserialize<FileNode>(link.BufferToString(link.ReadFullPrefixed(Timeout)));
            _pool.Return(link);
            return result;
        }
    }
}