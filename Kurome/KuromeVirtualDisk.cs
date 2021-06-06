using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using DokanNet;
using FileAccess = DokanNet.FileAccess;

namespace Kurome
{
    public class KuromeVirtualDisk : IDokanOperations
    {
        private TcpClient _tcpClient { get; }
        private string _deviceName;
        private char DriveLetter;
        private const long KiB = 1024;
        private const long MiB = 1024 * KiB;
        private const long GiB = 1024 * MiB;
        private const long TiB = 1024 * GiB;


        public KuromeVirtualDisk(TcpClient tcpClient, string deviceName, char driveLetter)
        {
            _tcpClient = tcpClient;
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
            throw new NotImplementedException();
        }

        public void CloseFile(string fileName, IDokanFileInfo info)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files,
            IDokanFileInfo info)
        {
            throw new NotImplementedException();
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
            _tcpClient.GetStream().Write(Encoding.UTF8.GetBytes("request:info:space"));
            var buffer = new byte[4096];
            var readTask = _tcpClient.GetStream().ReadAsync(buffer, 0, buffer.Length);
            Task.WaitAny(readTask, Task.Delay(TimeSpan.FromSeconds(5)));
            if (!readTask.IsCompleted || readTask.Result == 0)
            {
                Console.WriteLine("Connection timed out or client disconnected");
                Dokan.Unmount(DriveLetter);
            }
            var request = Encoding.UTF8.GetString(buffer, 0, readTask.Result);
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
            features = FileSystemFeatures.UnicodeOnDisk  | FileSystemFeatures.CasePreservedNames;
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
    }
}