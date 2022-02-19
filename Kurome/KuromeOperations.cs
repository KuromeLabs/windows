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

        public KuromeOperations(Device device)
        {
            _device = device;
        }

        private DirectoryNode root;

        private BaseNode GetNode(string name)
        {
            var result = root ?? (BaseNode) (root = new DirectoryNode(_device.GetRoot()));
            var parts = new Queue<string>(name.Split('\\', StringSplitOptions.RemoveEmptyEntries));

            while (result != null && parts.Count > 0)
                result = (result as DirectoryNode)?.GetChild(_device, parts.Dequeue());

            return result;
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
            var node = GetNode(fileName);
            var nodeExists = node != null;
            var parentPath = Path.GetDirectoryName(fileName);
            var parentNode = parentPath == null ? null : GetNode(parentPath) as DirectoryNode;
            var parentNodeExists = parentNode != null;
            var nodeIsDirectory = nodeExists && (node.FileInformation.Attributes & FileAttributes.Directory) != 0;
            if (info.IsDirectory)
            {
                switch (mode)
                {
                    case FileMode.Open:
                        if (!nodeExists)
                            return DokanResult.FileNotFound;
                        else if (!nodeIsDirectory)
                            return DokanResult.NotADirectory;
                        break;

                    case FileMode.CreateNew:
                        if (nodeExists)
                            return DokanResult.FileExists;
                        Console.WriteLine("Called CreateDirectory");
                        parentNode.CreateDirectoryChild(_device, fileName);
                        break;
                }
            }
            else
            {
                switch (mode)
                {
                    case FileMode.Open:
                        if (nodeExists)
                        {
                            if (nodeIsDirectory)
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
                        if (nodeExists)
                            return DokanResult.FileExists;
                        else if (!parentNodeExists)
                            return DokanResult.PathNotFound;
                        parentNode.CreateFileChild(_device, fileName);
                        break;
                    case FileMode.Create:
                        if (!parentNodeExists)
                            return DokanResult.PathNotFound;
                        if (nodeExists)
                            return DokanResult.AlreadyExists;
                        parentNode.CreateFileChild(_device, fileName);
                        break;
                    case FileMode.OpenOrCreate:
                        if (nodeExists)
                            return DokanResult.AlreadyExists;
                        if (!parentNodeExists)
                            return DokanResult.PathNotFound;
                        parentNode.CreateFileChild(_device, fileName);
                        break;
                    case FileMode.Truncate:
                        if (!nodeExists)
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
                GetNode(fileName).Delete(_device);
        }

        public void CloseFile(string fileName, IDokanFileInfo info)
        {
            info.Context = null;
        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
        {
            var node = GetNode(fileName);
            var size = node.FileInformation.Length;
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
            var node = GetNode(fileName) as FileNode;
            node.Write(buffer, offset, _device);
            bytesWritten = buffer.Length;
            return DokanResult.Success;
        }

        public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
        {
            var fileNode = GetNode(fileName);
            fileInfo = new FileInformation
            {
                FileName = fileNode.Name,
                Attributes = (fileNode.FileInformation.Attributes & FileAttributes.Directory) != 0
                    ? FileAttributes.Directory
                    : FileAttributes.Normal,
                LastAccessTime = fileNode.FileInformation.LastAccessTime,
                LastWriteTime = fileNode.FileInformation.LastWriteTime,
                CreationTime = fileNode.FileInformation.CreationTime,
                Length = fileNode.FileInformation.Length
            };
            return DokanResult.Success;
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            var parent = GetNode(fileName) as DirectoryNode;
            var children = parent.GetChildrenNodes(_device).ToList();
            files = children.Any()
                ? children.Select(x => new FileInformation()
                {
                    FileName = x.Name,
                    Attributes = (x.FileInformation.Attributes & FileAttributes.Directory) != 0
                        ? FileAttributes.Directory
                        : FileAttributes.Normal,
                    LastAccessTime = x.FileInformation.LastAccessTime,
                    LastWriteTime = x.FileInformation.LastWriteTime,
                    CreationTime = x.FileInformation.CreationTime,
                    Length = x.FileInformation.Length
                }).ToList()
                : new List<FileInformation>();

            return DokanResult.Success;
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files,
            IDokanFileInfo info)
        {
            var nodes = ((DirectoryNode) GetNode(fileName)).GetChildrenNodes(_device).ToList();
            files = new List<FileInformation>(nodes.Count);
            foreach (var node in nodes.Where(node =>
                         DokanHelper.DokanIsNameInExpression(searchPattern, node.Name, true)))
                files.Add(node.FileInformation);
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
            var node = GetNode(fileName);
            node.SetFileTime(creationTime, lastAccessTime, lastWriteTime, _device);
            return DokanResult.Success;
        }

        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {
            var node = GetNode(fileName);
            if (node == null)
                return DokanResult.FileNotFound;
            else if ((node.FileInformation.Attributes & FileAttributes.Directory) != 0)
                return DokanResult.AccessDenied;
            return DokanResult.Success;
        }

        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            var node = GetNode(fileName);
            if (node == null)
                return DokanResult.FileNotFound;
            else if ((node.FileInformation.Attributes & FileAttributes.Directory) == 0)
                return DokanResult.AccessDenied;
            return DokanResult.Success;
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            var newNode = GetNode(newName);
            var oldNode = GetNode(oldName);
            var destination = GetNode(Path.GetDirectoryName(newName)) as DirectoryNode;
            if (newNode == null)
            {
                if (destination == null) return DokanResult.PathNotFound;
                oldNode.Move(_device, newName, destination);
                return DokanResult.Success;
            }
            else if (replace)
            {
                if (info.IsDirectory) return DokanResult.AccessDenied;
                newNode.Delete(_device);
                oldNode.Move(_device, newName, destination);
                return DokanResult.Success;
            }

            return DokanResult.FileExists;
        }

        public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
        {
            var node = GetNode(fileName) as FileNode;
            node.SetLength(length, _device);
            return DokanResult.Success;
        }

        public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
        {
            var node = GetNode(fileName) as FileNode;
            node.SetLength(length, _device);
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
            return DokanResult.Success;
        }

        public NtStatus Unmounted(IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
        {
            throw new NotImplementedException();
        }
    }
}