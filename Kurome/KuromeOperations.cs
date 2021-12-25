using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using DokanNet;
using kurome;
using FileAccess = DokanNet.FileAccess;

namespace Kurome
{
    public class KuromeOperations : IDokanOperations
    {
        private readonly Device _device;

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
            //Access to these directories is prohibited in newer version of Android.
            //Android\obb can be accessed with REQUEST_INSTALL_PACKAGES Android permission.
            //TODO: Find workaround/ask for root/ask permission (for obb)/etc.
            if (fileName is "\\Android\\data" or "\\Android\\obb")
                return DokanResult.AccessDenied;
            var fileType = _device.GetFileType(fileName);
            var pathExists = fileType is ResultType.ResultFileIsFile or ResultType.ResultFileIsDirectory;
            if (info.IsDirectory)
            {
                switch (mode)
                {
                    case FileMode.Open:
                        if (!pathExists)
                            return DokanResult.FileNotFound;
                        else if (fileType == ResultType.ResultFileIsFile)
                            return DokanResult.NotADirectory;
                        break;

                    case FileMode.CreateNew:
                        if (pathExists)
                            return DokanResult.FileExists;
                        Console.WriteLine("Called CreateDirectory");
                        _device.CreateDirectory(fileName);
                        break;
                }
            }
            else
            {
                switch (mode)
                {
                    case FileMode.Open:
                        if (fileType != ResultType.ResultFileNotFound && fileType != ResultType.ResultPathNotFound)
                        {
                            if (fileType == ResultType.ResultFileIsDirectory)
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
                        if (fileType == ResultType.ResultFileIsFile)
                            return DokanResult.FileExists;
                        else if (fileType == ResultType.ResultPathNotFound)
                            return DokanResult.PathNotFound;
                        _device.CreateEmptyFile(fileName);
                        break;
                    case FileMode.Create:
                        if (fileType == ResultType.ResultPathNotFound)
                            return DokanResult.PathNotFound;
                        if (pathExists)
                            return DokanResult.AlreadyExists;
                        _device.CreateEmptyFile(fileName);
                        break;
                    case FileMode.OpenOrCreate:
                        if (pathExists)
                            return DokanResult.AlreadyExists;
                        if (fileType == ResultType.ResultPathNotFound)
                            return DokanResult.PathNotFound;
                        _device.CreateEmptyFile(fileName);
                        break;
                    case FileMode.Truncate:
                        if (!pathExists)
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
            var size = (info.Context as FileNode? ?? default).Length;
            if (offset >= size)
            {
                bytesRead = 0;
                return DokanResult.Success;
            }

            var bytesToRead = (offset + buffer.Length) > size ? (size - offset) : buffer.Length;
            bytesRead = _device.ReceiveFileBuffer(buffer, fileName, offset, (int) bytesToRead, size);
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
            return DokanResult.Success;
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
        {
            var fileNode = _device.GetFileInfo(fileName);
            fileInfo = new FileInformation
            {
                FileName = fileNode.Filename,
                Attributes = fileNode.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal,
                LastAccessTime = DateTimeOffset.FromUnixTimeMilliseconds(fileNode.LastAccessTime).LocalDateTime,
                LastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(fileNode.LastWriteTime).LocalDateTime,
                CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(fileNode.CreationTime).LocalDateTime,
                Length = fileNode.IsDirectory ? 0 : fileNode.Length
            };
            return DokanResult.Success;
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            files = null;
            var packet = _device.GetFileNodes(fileName);
            files = new List<FileInformation>();
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
            return DokanResult.Success;
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime,
            DateTime? lastWriteTime,
            IDokanFileInfo info)
        {
            var cTime = creationTime?.ToFileTimeUtc();
            var laTime = lastAccessTime?.ToFileTimeUtc();
            var lwTime = lastWriteTime?.ToFileTimeUtc();
            _device.SetFileTime(fileName, cTime ?? 0, laTime ?? 0, lwTime ?? 0);
            return DokanResult.Success;
        }

        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {
            var type = _device.GetFileType(fileName);
            if (type == ResultType.ResultFileNotFound)
                return DokanResult.FileNotFound;
            else if (type == ResultType.ResultFileIsDirectory)
                return DokanResult.AccessDenied;
            return DokanResult.Success;
        }

        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            var type = _device.GetFileType(fileName);
            if (type == ResultType.ResultFileNotFound)
                return DokanResult.FileNotFound;
            else if (type == ResultType.ResultFileIsFile)
                return DokanResult.AccessDenied;
            return DokanResult.Success;
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            var fileType = _device.GetFileType(newName);
            var fileExists = fileType != ResultType.ResultFileNotFound;
            if (!fileExists)
            {
                _device.Rename(oldName, newName);
                return DokanResult.Success;
            }
            else if (replace)
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
            var spaces = _device.GetSpace();
            var total = spaces.TotalBytes;
            long free = spaces.FreeBytes;
            totalNumberOfBytes = total;
            freeBytesAvailable = free;
            totalNumberOfFreeBytes = free;
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