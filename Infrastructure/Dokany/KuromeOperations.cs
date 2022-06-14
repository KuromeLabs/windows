using System.Security.AccessControl;
using Application.Interfaces;
using Application.Models.Dokany;
using AutoMapper;
using DokanNet;
using FileAccess = DokanNet.FileAccess;

namespace Infrastructure.Dokany
{
    public class KuromeOperations : IKuromeOperations
    {
        private readonly IDeviceAccessor _deviceAccessor;
        private readonly IMapper _mapper;

        public KuromeOperations(IMapper mapper, IDeviceAccessor deviceAccessor)
        {
            _mapper = mapper;
            _deviceAccessor = deviceAccessor;
        }

        private DirectoryNode? _root;

        private BaseNode? GetNode(string name)
        {
            var result = _root ?? (BaseNode) (_root = new DirectoryNode(_deviceAccessor.GetRootNode()));
            var parts = new Queue<string>(name.Split('\\', StringSplitOptions.RemoveEmptyEntries));

            while (result != null && parts.Count > 0)
                result = (result as DirectoryNode)?.GetChild(_deviceAccessor, parts.Dequeue());

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
            if (fileName.StartsWith("\\Android\\data") || fileName.StartsWith("\\Android\\obb"))
                return DokanResult.AccessDenied;
            var node = GetNode(fileName);
            var nodeExists = node != null;
            var parentPath = Path.GetDirectoryName(fileName);
            var parentNode = parentPath == null ? null : GetNode(parentPath) as DirectoryNode;
            var parentNodeExists = parentNode != null;
            var nodeIsDirectory = nodeExists && node!.KuromeInformation.IsDirectory;
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
                        parentNode!.CreateDirectoryChild(_deviceAccessor, fileName);
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
                        parentNode!.CreateFileChild(_deviceAccessor, fileName);
                        break;
                    case FileMode.Create:
                        if (!parentNodeExists)
                            return DokanResult.PathNotFound;
                        if (nodeExists)
                            return DokanResult.AlreadyExists;
                        parentNode!.CreateFileChild(_deviceAccessor, fileName);
                        break;
                    case FileMode.OpenOrCreate:
                        if (nodeExists)
                            return DokanResult.AlreadyExists;
                        if (!parentNodeExists)
                            return DokanResult.PathNotFound;
                        parentNode!.CreateFileChild(_deviceAccessor, fileName);
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
                GetNode(fileName)!.Delete(_deviceAccessor);
        }

        public void CloseFile(string fileName, IDokanFileInfo info)
        {
            info.Context = null;
        }

        public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
        {
            var node = GetNode(fileName) as FileNode;
            var size = node!.KuromeInformation.Length;
            if (offset >= size)
            {
                bytesRead = 0;
                return DokanResult.Success;
            }

            var bytesToRead = (offset + buffer.Length) > size ? (size - offset) : buffer.Length;
            bytesRead = node.ReadFile(buffer, offset, (int) bytesToRead, size, _deviceAccessor);
            return DokanResult.Success;
        }

        public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset,
            IDokanFileInfo info)
        {
            var node = GetNode(fileName) as FileNode;
            node!.Write(buffer, offset, _deviceAccessor);
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
                FileName = fileNode!.Name,
                Attributes = (fileNode.KuromeInformation.IsDirectory
                    ? FileAttributes.Directory
                    : FileAttributes.Normal),
                LastAccessTime = fileNode.KuromeInformation.LastAccessTime,
                LastWriteTime = fileNode.KuromeInformation.LastWriteTime,
                CreationTime = fileNode.KuromeInformation.CreationTime,
                Length = fileNode.KuromeInformation.Length
            };
            return DokanResult.Success;
        }

        public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
        {
            var parent = GetNode(fileName) as DirectoryNode;
            var children = parent!.GetChildrenNodes(_deviceAccessor).ToList();
            files = children.Any()
                ? children.Select(x => new FileInformation
                    {
                        FileName = x.Name,
                        Attributes = (x.KuromeInformation.IsDirectory
                            ? FileAttributes.Directory
                            : FileAttributes.Normal),
                        LastAccessTime = x.KuromeInformation.LastAccessTime,
                        LastWriteTime = x.KuromeInformation.LastWriteTime,
                        CreationTime = x.KuromeInformation.CreationTime,
                        Length = x.KuromeInformation.Length
                    })
                    .ToList()
                : new List<FileInformation>();

            return DokanResult.Success;
        }

        public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files,
            IDokanFileInfo info)
        {
            var nodes = ((DirectoryNode) GetNode(fileName)!).GetChildrenNodes(_deviceAccessor).ToList();
            files = new List<FileInformation>(nodes.Count);
            foreach (var node in nodes.Where(node =>
                         DokanHelper.DokanIsNameInExpression(searchPattern, node.Name, true)))
                files.Add(_mapper.Map<FileInformation>(node.KuromeInformation));
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
            node!.SetFileTime(creationTime, lastAccessTime, lastWriteTime, _deviceAccessor);
            return DokanResult.Success;
        }

        public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
        {
            var node = GetNode(fileName);
            if (node == null)
                return DokanResult.FileNotFound;
            else if (node.KuromeInformation.IsDirectory)
                return DokanResult.AccessDenied;
            return DokanResult.Success;
        }

        public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
        {
            var node = GetNode(fileName);
            if (node == null)
                return DokanResult.FileNotFound;
            else if (!node.KuromeInformation.IsDirectory)
                return DokanResult.AccessDenied;
            return DokanResult.Success;
        }

        public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
        {
            var newNode = GetNode(newName);
            var oldNode = GetNode(oldName);
            var destination = GetNode(Path.GetDirectoryName(newName)!) as DirectoryNode;
            if (newNode == null)
            {
                if (destination == null) return DokanResult.PathNotFound;
                oldNode!.Move(_deviceAccessor, newName, destination);
                return DokanResult.Success;
            }
            else if (replace)
            {
                if (info.IsDirectory) return DokanResult.AccessDenied;
                newNode.Delete(_deviceAccessor);
                oldNode!.Move(_deviceAccessor, newName, destination!);
                return DokanResult.Success;
            }

            return DokanResult.FileExists;
        }

        public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
        {
            var node = GetNode(fileName) as FileNode;
            node!.SetLength(length, _deviceAccessor);
            return DokanResult.Success;
        }

        public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
        {
            var node = GetNode(fileName) as FileNode;
            node!.SetLength(length, _deviceAccessor);
            return DokanResult.Success;
        }

        public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
        {
            return DokanResult.Success;
        }

        public NtStatus GetDiskFreeSpace(
            out long freeBytesAvailable,
            out long totalNumberOfBytes,
            out long totalNumberOfFreeBytes,
            IDokanFileInfo info)
        {
            totalNumberOfBytes = freeBytesAvailable = totalNumberOfFreeBytes = 0;
            _deviceAccessor.GetSpace(out var total, out var free);
            totalNumberOfBytes = total;
            freeBytesAvailable = free;
            totalNumberOfFreeBytes = free;
            return DokanResult.Success;
        }

        public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features,
            out string fileSystemName,
            out uint maximumComponentLength, IDokanFileInfo info)
        {
            volumeLabel = _deviceAccessor.Get().Name;
            features = FileSystemFeatures.UnicodeOnDisk | FileSystemFeatures.CasePreservedNames;
            fileSystemName = "Kurome";
            maximumComponentLength = 255;
            return DokanResult.Success;
        }

        public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security,
            AccessControlSections sections,
            IDokanFileInfo info)
        {
            security = new FileSecurity();
            return DokanResult.NotImplemented;
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
            streams = Array.Empty<FileInformation>();
            return DokanResult.Success;
        }
    }
}