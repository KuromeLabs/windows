using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            if (info.IsDirectory)
            {
                switch (mode)
                {
                    case FileMode.Open:
                        if (fileType != "directory")
                            return DokanResult.NotADirectory;
                        else if (fileType == "doesnotexist")
                            return DokanResult.FileNotFound;
                        break;
                    case FileMode.CreateNew:
                        if (fileType == "file")
                            return DokanResult.FileExists;
                        else if (fileType == "directory")
                            return DokanResult.AlreadyExists;
                        _device.CreateDirectory(fileName);
                        break;
                }
            }
            else
            {
                switch (mode)
                {
                    case FileMode.Open:
                        if (fileType == "doesnotexist")
                            return DokanResult.FileNotFound;
                        else if (fileType == "directory")
                            info.IsDirectory = true;
                        info.Context = new object();
                        return DokanResult.Success;
                    case FileMode.CreateNew:
                        if (fileType != "doesnotexist")
                            return DokanResult.FileExists;
                        break;
                    case FileMode.Truncate:
                        if (fileType == "doesnotexist")
                            return DokanResult.FileNotFound;
                        break;
                }
            }

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
                CreationTime = null,
                LastAccessTime = DateTime.Now,
                LastWriteTime = null,
                Attributes = info.IsDirectory ? FileAttributes.Directory : FileAttributes.NotContentIndexed,
                FileName = fileName[(fileName.LastIndexOf('\\')+1)..],
                Length = 0
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
            fileSystemName = "Generic hierarchical";
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