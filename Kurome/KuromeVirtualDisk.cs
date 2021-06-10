using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
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
            var request = SendReceiveTcpWithTimeout("request:info:directory:" + fileName.Replace('\\', '/'), 15);
            Console.WriteLine("request:info:directory:" + fileName.Replace('\\', '/'));
            if (request == null) return DokanResult.Unsuccessful;
            Console.WriteLine(request);
            var fileInfos = JsonSerializer.Deserialize<List<FileData>>(request);
            if (fileInfos == null)
                return DokanResult.Unsuccessful;
            files = fileInfos.Select(fileData => new FileInformation
                {
                    FileName = fileData.fileName,
                    Attributes = fileData.isDirectory ? FileAttributes.Directory : FileAttributes.Normal,
                    LastAccessTime = DateTime.Now,
                    LastWriteTime = null,
                    CreationTime = null,
                    Length = fileData.size
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
            string request = SendReceiveTcpWithTimeout("request:info:space", 15);
            totalNumberOfBytes = freeBytesAvailable = totalNumberOfFreeBytes = 0;
            if (request == null) return DokanResult.Unsuccessful;
            Console.WriteLine(request);
            string[] spaces = request.Split(':');
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
                _networkStream.Write(Encoding.UTF8.GetBytes(message));
                var buffer = new byte[8192];
                var readTask = _networkStream.ReadAsync(buffer, 0, buffer.Length);
                Task.WaitAny(readTask, Task.Delay(TimeSpan.FromSeconds(timeout)));
                if (readTask.IsCompleted && readTask.Result != 0)
                {
                    if (buffer[0] != 0x1f || buffer[1] != 0x8b)
                        return Encoding.UTF8.GetString(buffer, 0, readTask.Result);
                    var decompressed = Decompress(buffer);
                    return Encoding.UTF8.GetString(decompressed, 0, decompressed.Length);
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

    public class FileData
    {
        public string fileName { get; set; }
        public bool isDirectory { get; set; }
        public long size { get; set; }
    }
}