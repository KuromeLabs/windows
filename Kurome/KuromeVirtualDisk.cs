using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Caching;
using System.Security.AccessControl;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DokanNet;
using FileAccess = DokanNet.FileAccess;

namespace Kurome
{
    public class KuromeVirtualDisk : IDokanOperations
    {
        private NetworkStream _networkStream;
        private string _deviceName;
        private char DriveLetter;
        private readonly object _lock = new();
        private const long KiB = 1024;
        private const long MiB = 1024 * KiB;
        private const long GiB = 1024 * MiB;
        private const long TiB = 1024 * GiB;
        private MemoryCache cache = MemoryCache.Default;


        public KuromeVirtualDisk(NetworkStream networkStream, string deviceName, char driveLetter)
        {
            _networkStream = networkStream;
            _deviceName = deviceName;
            DriveLetter = driveLetter;

        }

        public NtStatus CreateFile(
            string fileName,
            FileAccess access,
            FileShare share,
            FileMode mode,
            FileOptions options,
            FileAttributes attributes,
            IDokanFileInfo info)
        {
            if (info.IsDirectory && mode == FileMode.CreateNew)
                return DokanResult.AccessDenied;
            return DokanResult.Success;
        }

        public void Cleanup(string fileName, IDokanFileInfo info)
        {
            //throw new NotImplementedException();
        }

        public void CloseFile(string fileName, IDokanFileInfo info)
        {
            //throw new NotImplementedException();
        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset,
            IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
        {
            //TODO: implement
            fileInfo = new FileInformation
            {
            };
            return DokanResult.Unsuccessful;
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files,
            IDokanFileInfo info)
        {
            files = null;
            var request = "request:info:directory:" + fileName.Replace('\\', '/');
            var response = cache.Get(request) as string;
            if (response == null)
            {
                response = SendReceiveTcpWithTimeout(request, 15);
                CacheItemPolicy policy = new() {AbsoluteExpiration = DateTimeOffset.Now.AddSeconds(10)};
                cache.Add(request, response, policy);
            }
            Console.WriteLine("request:info:directory:" + fileName.Replace('\\', '/'));
            Console.WriteLine(response);
            var fileNodeList = JsonSerializer.Deserialize<List<FileNode>>(response);
            if (fileNodeList == null)
                return DokanResult.Unsuccessful;
            files = fileNodeList.Select(fileNode => new FileInformation
                {
                    FileName = fileNode.FileName,
                    Attributes = fileNode.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal,
                    LastAccessTime = DateTime.Now,
                    LastWriteTime = DateTime.Now,
                    CreationTime = DateTime.Now,
                    Length = fileNode.Size
                })
                .ToList();

            return DokanResult.Success;
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime,
            DateTime? lastWriteTime,
            IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus GetDiskFreeSpace(
            out long freeBytesAvailable,
            out long totalNumberOfBytes,
            out long totalNumberOfFreeBytes,
            IDokanFileInfo info)
        {
            var request = "request:info:space";
            var response = cache.Get(request) as string;
            if (response == null)
            {
                response = SendReceiveTcpWithTimeout(request, 15);
                CacheItemPolicy policy = new() {AbsoluteExpiration = DateTimeOffset.Now.AddSeconds(10)};
                cache.Add(request, response, policy);
            }
            totalNumberOfBytes = freeBytesAvailable = totalNumberOfFreeBytes = 0;
            Console.WriteLine(response);
            string[] spaces = response.Split(':');
            long totalSizeGb = long.Parse(spaces[0]);
            long freeSpaceGb = long.Parse(spaces[1]);
            totalNumberOfBytes = totalSizeGb;
            freeBytesAvailable = freeSpaceGb;
            totalNumberOfFreeBytes = freeSpaceGb;
            return DokanResult.Success;
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features,
            out string fileSystemName,
            out uint maximumComponentLength, IDokanFileInfo info)
        {
            volumeLabel = _deviceName;
            features = FileSystemFeatures.UnicodeOnDisk | FileSystemFeatures.CasePreservedNames;
            fileSystemName = "Generic hierarchical";
            maximumComponentLength = 255;
            return DokanResult.Success;
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security,
            AccessControlSections sections,
            IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections,
            IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus Mounted(IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus Unmounted(IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        private string SendReceiveTcpWithTimeout(string message, int timeout)
        {
            lock (_lock) //Maybe use a queue
            {
                try
                {
                    _networkStream.Write(BitConverter.GetBytes(message.Length)
                        .Concat(Encoding.UTF8.GetBytes(message)).ToArray());
                    var sizeBuffer = new byte[4];
                    var readPrefixTask = _networkStream.ReadAsync(sizeBuffer, 0, 4);
                    Task.WaitAny(readPrefixTask, Task.Delay(TimeSpan.FromSeconds(timeout)));
                    var size = BitConverter.ToInt32(sizeBuffer);
                    int bytesRead = 0;

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
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                Console.WriteLine("Client disconnected");
                Dokan.Unmount(DriveLetter);
                return null;
            }
        }


        private static byte[] Decompress(byte[] compressedData)
        {
            var outputStream = new MemoryStream();
            using var compressedStream = new MemoryStream(compressedData);
            using GZipStream sr = new GZipStream(compressedStream, CompressionMode.Decompress);
            sr.CopyTo(outputStream);
            outputStream.Position = 0;
            return outputStream.ToArray();
        }
    }
}