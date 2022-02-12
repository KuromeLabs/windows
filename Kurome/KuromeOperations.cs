using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            var node = _device.GetFileInfo(fileName);
            info.Context = node;
            var fileType = node.FileType;
            var pathExists = fileType is FileType.File or FileType.Directory;
            if (info.IsDirectory)
            {
                switch (mode)
                {
                    case FileMode.Open:
                        if (!pathExists)
                            return DokanResult.FileNotFound;
                        else if (fileType == FileType.File)
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
                        if (fileType != FileType.FileNotFound && fileType != FileType.PathNotFound)
                        {
                            if (fileType == FileType.Directory)
                            {
                                if ((access & FileAccess.Delete) == FileAccess.Delete &&
                                    (access & FileAccess.Synchronize) != FileAccess.Synchronize)
                                    return DokanResult.AccessDenied;
                                info.IsDirectory = true;
                                // info.Context = new object();
                                return DokanResult.Success;
                            }
                        }
                        else
                            return DokanResult.FileNotFound;

                        break;
                    case FileMode.CreateNew:
                        if (fileType == FileType.File)
                            return DokanResult.FileExists;
                        else if (fileType == FileType.PathNotFound)
                            return DokanResult.PathNotFound;
                        _device.CreateEmptyFile(fileName);
                        break;
                    case FileMode.Create:
                        if (fileType == FileType.PathNotFound)
                            return DokanResult.PathNotFound;
                        if (pathExists)
                            return DokanResult.AlreadyExists;
                        _device.CreateEmptyFile(fileName);
                        break;
                    case FileMode.OpenOrCreate:
                        if (pathExists)
                            return DokanResult.AlreadyExists;
                        if (fileType == FileType.PathNotFound)
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
            var fileNode = (info.Context as FileNode? ?? default);
            fileInfo = new FileInformation
            {
                FileName = fileNode.Filename,
                Attributes = fileNode.FileType == FileType.Directory ? FileAttributes.Directory : FileAttributes.Normal,
                LastAccessTime = DateTimeOffset.FromUnixTimeMilliseconds(fileNode.LastAccessTime).LocalDateTime,
                LastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(fileNode.LastWriteTime).LocalDateTime,
                CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(fileNode.CreationTime).LocalDateTime,
                Length = fileNode.FileType == FileType.Directory ? 0 : fileNode.Length
            };
            return DokanResult.Success;
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            files = _device.GetFileNodes(fileName);
            return DokanResult.Success;
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files,
            IDokanFileInfo info)
        {
            var nodes = _device.GetFileNodes(fileName);
            files = new List<FileInformation>(nodes.Count);
            foreach (var node in nodes.Where(node =>
                         DokanHelper.DokanIsNameInExpression(searchPattern, node.FileName, true)))
                files.Add(node);
            return DokanResult.Success;
        }

        public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime,
            DateTime? lastWriteTime,
            IDokanFileInfo info)
        {
            var cTime = creationTime == null ? 0 : ((DateTimeOffset) creationTime).ToUnixTimeMilliseconds();
            var laTime = lastAccessTime == null ? 0 : ((DateTimeOffset) lastAccessTime).ToUnixTimeMilliseconds();
            var lwTime = lastWriteTime == null ? 0 : ((DateTimeOffset) lastWriteTime).ToUnixTimeMilliseconds();
            _device.SetFileTime(fileName, cTime, laTime, lwTime);
            return DokanResult.Success;
        }

        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {
            var type = (info.Context as FileNode? ?? default).FileType;
            
            if (type == FileType.FileNotFound)
                return DokanResult.FileNotFound;
            else if (type == FileType.Directory)
                return DokanResult.AccessDenied;
            return DokanResult.Success;
        }

        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            var type = ((FileNode) info.Context).FileType;
            if (type == FileType.FileNotFound)
                return DokanResult.FileNotFound;
            else if (type == FileType.File)
                return DokanResult.AccessDenied;
            return DokanResult.Success;
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            var fileType = _device.GetFileInfo(newName).FileType;
            var fileExists = fileType != FileType.FileNotFound;
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
            var free = spaces.FreeBytes;
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

        public NtStatus Mounted(string mountPoint, IDokanFileInfo info)
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