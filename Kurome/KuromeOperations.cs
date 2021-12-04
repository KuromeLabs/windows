using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.AccessControl;
using DokanNet;
using FileAccess = DokanNet.FileAccess;

namespace Kurome
{
    public class KuromeOperations : IDokanOperations
    {
        private readonly Device _device;
        private const long KiB = 1024;
        private const long MiB = 1024 * KiB;
        private const long GiB = 1024 * MiB;
        private const long TiB = 1024 * GiB;
        private readonly object _lock = new();

        public KuromeOperations(Device device)
        {
            _device = device;
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
            var fileType = _device.GetFileType(fileName);
            var fileExists = fileType != Packets.ResultFileNotFound;
            var isDirectory = fileType == Packets.ResultFileIsDirectory;
            if (info.IsDirectory)
            {
                switch (mode)
                {
                    case FileMode.Open:
                        if (!isDirectory)
                            return DokanResult.NotADirectory;
                        break;
                    case FileMode.CreateNew:
                        if (fileExists)
                            return DokanResult.FileExists;
                        _device.CreateDirectory(fileName);
                        break;
                }
            }
            else
            {
                switch (mode)
                {
                    case FileMode.Open:
                        if (fileExists)
                        {
                            if (isDirectory)
                            {
                                info.IsDirectory = true;
                                info.Context = new object();
                                return DokanResult.Success;
                            }
                        }
                        else
                            return DokanResult.FileNotFound;

                        break;
                    case FileMode.CreateNew:
                        if (fileExists)
                            return DokanResult.FileExists;
                        break;
                    case FileMode.Truncate:
                        if (!fileExists)
                            return DokanResult.FileNotFound;
                        break;
                }
            }

            return DokanResult.Success;
        }

        public void Cleanup(string fileName, IDokanFileInfo info)
        {
            info.Context = null;
            if (info.DeleteOnClose)
                _device.Delete(fileName);
        }

        public void CloseFile(string fileName, IDokanFileInfo info)
        {
            info.Context = null;
        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
        {
            info.Context ??= _device.GetFileInfo(fileName);
            var size = (info.Context as FileNode)!.Size;
            if (offset >= size)
            {
                bytesRead = 0;
                return DokanResult.Success;
            }
            var bytesToRead = (offset + buffer.Length) > size ? (size - offset) : buffer.Length;
            bytesRead = _device.ReceiveFileBuffer(ref buffer, fileName, offset, (int)bytesToRead, size);
            return DokanResult.Success;
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset,
            IDokanFileInfo info)
        {
            _device.WriteFileBuffer(buffer, fileName, offset);
            bytesWritten = buffer.Length;
            return DokanResult.Success;
        }

        public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
        {
            var fileNode = _device.GetFileInfo(fileName);
            fileInfo = new FileInformation
            {
                FileName = fileNode.FileName,
                Attributes = fileNode.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal,
                LastAccessTime = DateTime.Now,
                LastWriteTime = null,
                CreationTime = null,
                Length = fileNode.IsDirectory ? 0 : fileNode.Size
            };
            return DokanResult.Success;
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            files = null;
            var fileNodeList = _device.GetFileNodes(fileName);
            if (fileNodeList == null)
                return DokanResult.Unsuccessful;
            files = fileNodeList.Select(fileNode => new FileInformation
                {
                    FileName = fileNode.FileName,
                    Attributes = fileNode.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal,
                    LastAccessTime = DateTime.Now,
                    LastWriteTime = null,
                    CreationTime = null,
                    Length = fileNode.IsDirectory ? 0 : fileNode.Size
                })
                .ToList();
            return DokanResult.Success;
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files,
            IDokanFileInfo info)
        {
            files = null;
            return DokanResult.NotImplemented;
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime,
            DateTime? lastWriteTime,
            IDokanFileInfo info)
        {
            var cTime = creationTime?.ToFileTimeUtc();
            var laTime = lastAccessTime?.ToFileTimeUtc();
            var lwTime = lastWriteTime?.ToFileTimeUtc();
            _device.SetFileTime(fileName, cTime, laTime, lwTime);
            return DokanResult.Success;
        }

        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {
            var type = _device.GetFileType(fileName);
            if (type == Packets.ResultFileNotFound)
                return DokanResult.FileNotFound;
            else if (type == Packets.ResultFileIsDirectory)
                return DokanResult.AccessDenied;
            return DokanResult.Success;
        }

        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            var type = _device.GetFileType(fileName);
            if (type == Packets.ResultFileNotFound)
                return DokanResult.FileNotFound;
            else if (type == Packets.ResultFileIsFile)
                return DokanResult.AccessDenied;
            return DokanResult.Success;
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            var fileType = _device.GetFileType(newName);
            var fileExists = fileType != Packets.ResultFileNotFound;
            if (!fileExists)
            {
                _device.Rename(oldName, newName);
                return DokanResult.Success;
            } else if (replace)
            {
                if (info.IsDirectory) return DokanResult.AccessDenied;
                _device.Delete(newName);
                _device.Rename(oldName, newName);
                return DokanResult.Success;
            }

            return DokanResult.FileExists;
        }

        public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
        {
            _device.SetLength(fileName, length);
            return DokanResult.Success;
        }

        public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
        {
            _device.SetLength(fileName, length);
            return DokanResult.Success;
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
            totalNumberOfBytes = freeBytesAvailable = totalNumberOfFreeBytes = 0;
            string[] spaces = _device.GetSpace().Split(':');
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
            volumeLabel = _device.Name;
            features = FileSystemFeatures.UnicodeOnDisk | FileSystemFeatures.CasePreservedNames;
            fileSystemName = "Kurome";
            maximumComponentLength = 255;
            return DokanResult.Success;
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security,
            AccessControlSections sections,
            IDokanFileInfo info)
        {
            security = null;
            return DokanResult.NotImplemented;
            throw new NotImplementedException();
        }

        public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections,
            IDokanFileInfo info)
        {
            return DokanResult.NotImplemented;
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