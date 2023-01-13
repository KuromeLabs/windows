using System.Security.AccessControl;
using Application.Dokany;
using Application.Interfaces;
using DokanNet;
using Domain.FileSystem;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using FileAccess = DokanNet.FileAccess;

namespace Infrastructure.Dokany;

public partial class KuromeOperations : IKuromeOperations
{
    private readonly FileSystemTree _cache;
    private readonly IDeviceAccessor _deviceAccessor;
    private readonly ILogger _logger;
    private readonly IMapper _mapper;

    public KuromeOperations(IDeviceAccessor deviceAccessor, ILogger<KuromeOperations> logger, string mountPoint,
        IMapper mapper)
    {
        _deviceAccessor = deviceAccessor;
        _logger = logger;
        _mapper = mapper;
        MountPoint = mountPoint;
        _cache = new FileSystemTree(deviceAccessor);
    }

    public string MountPoint { get; set; }

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
            return Trace(MountPoint, nameof(CreateFile), fileName, info, access, share, mode, options, attributes,
                DokanResult.AccessDenied);
        var node = _cache.GetNode(fileName);
        var nodeExists = node != null;
        var parentPath = Path.GetDirectoryName(fileName);
        var parentNode = parentPath == null ? null : _cache.GetNode(parentPath) as DirectoryNode;
        var parentNodeExists = parentNode != null;
        var nodeIsDirectory = nodeExists && node!.IsDirectory;
        if (info.IsDirectory)
            switch (mode)
            {
                case FileMode.Open:
                    if (!nodeExists)
                        return Trace(MountPoint, nameof(CreateFile), fileName, info, access, share, mode, options,
                            attributes, DokanResult.PathNotFound);
                    if (!nodeIsDirectory)
                        return Trace(MountPoint, nameof(CreateFile), fileName, info, access, share, mode, options,
                            attributes, DokanResult.NotADirectory);
                    break;

                case FileMode.CreateNew:
                    if (nodeExists)
                        return Trace(MountPoint, nameof(CreateFile), fileName, info, access, share, mode, options,
                            attributes, DokanResult.AlreadyExists);
                    _cache.CreateDirectoryChild(parentNode!, fileName);
                    break;
            }
        else
            switch (mode)
            {
                case FileMode.Open:
                    if (!nodeExists)
                        return Trace(MountPoint, nameof(CreateFile), fileName, info, access, share, mode, options,
                            attributes, DokanResult.FileNotFound);
                    if (nodeIsDirectory)
                    {
                        if ((access & FileAccess.Delete) == FileAccess.Delete &&
                            (access & FileAccess.Synchronize) != FileAccess.Synchronize)
                            return Trace(MountPoint, nameof(CreateFile), fileName, info, access, share, mode, options,
                                attributes, DokanResult.AccessDenied);
                        info.IsDirectory = true;
                        return Trace(MountPoint, nameof(CreateFile), fileName, info, access, share, mode, options,
                            attributes, DokanResult.Success);
                    }

                    break;
                case FileMode.CreateNew:
                    if (nodeExists)
                        return Trace(MountPoint, nameof(CreateFile), fileName, info, access, share, mode, options,
                            attributes, DokanResult.FileExists);
                    if (!parentNodeExists)
                        return Trace(MountPoint, nameof(CreateFile), fileName, info, access, share, mode, options,
                            attributes, DokanResult.PathNotFound);
                    _cache.CreateFileChild(parentNode!, fileName);
                    break;
                case FileMode.Create:
                    if (!parentNodeExists)
                        return Trace(MountPoint, nameof(CreateFile), fileName, info, access, share, mode, options,
                            attributes, DokanResult.PathNotFound);
                    if (nodeExists)
                        return Trace(MountPoint, nameof(CreateFile), fileName, info, access, share, mode, options,
                            attributes, DokanResult.AlreadyExists);
                    _cache.CreateFileChild(parentNode!, fileName);
                    break;
                case FileMode.OpenOrCreate:
                    if (nodeExists)
                        return Trace(MountPoint, nameof(CreateFile), fileName, info, access, share, mode, options,
                            attributes, DokanResult.AlreadyExists);
                    if (!parentNodeExists)
                        return Trace(MountPoint, nameof(CreateFile), fileName, info, access, share, mode, options,
                            attributes, DokanResult.PathNotFound);
                    _cache.CreateFileChild(parentNode!, fileName);
                    break;
                case FileMode.Truncate:
                    if (!nodeExists)
                        return Trace(MountPoint, nameof(CreateFile), fileName, info, access, share, mode, options,
                            attributes, DokanResult.FileNotFound);
                    break;
            }

        return Trace(MountPoint, nameof(CreateFile), fileName, info, access, share, mode, options, attributes,
            DokanResult.Success);
    }

    public void Cleanup(string fileName, IDokanFileInfo info)
    {
        info.Context = null;
        if (info.DeleteOnClose)
            _cache.Delete(_cache.GetNode(fileName)!);
        Trace(MountPoint, nameof(Cleanup), fileName, info, DokanResult.Success);
    }

    public void CloseFile(string fileName, IDokanFileInfo info)
    {
        info.Context = null;
    }

    public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
    {
        var node = _cache.GetNode(fileName) as FileNode;
        var size = node!.Length;
        if (offset >= size)
        {
            bytesRead = 0;
            return Trace(MountPoint, nameof(ReadFile), fileName, info, DokanResult.Success,
                "R:" + bytesRead, "O:" + offset);
        }

        var bytesToRead = offset + buffer.Length > size ? size - offset : buffer.Length;
        bytesRead = _cache.ReadFile(node, buffer, offset, (int)bytesToRead, size);
        return Trace(MountPoint, nameof(ReadFile), fileName, info, DokanResult.Success,
            "R:" + bytesRead, "O:" + offset);
    }

    public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset,
        IDokanFileInfo info)
    {
        var node = _cache.GetNode(fileName) as FileNode;
        _cache.Write(node!, buffer, offset);
        bytesWritten = buffer.Length;
        return Trace(MountPoint, nameof(WriteFile), fileName, info, DokanResult.Success,
            "W:" + bytesWritten, "O:" + offset);
    }

    public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
    {
        return DokanResult.Success;
    }

    public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
    {
        var fileNode = _cache.GetNode(fileName)!;
        fileInfo = _mapper.Map<FileInformation>(fileNode);
        return Trace(MountPoint, nameof(GetFileInformation), fileName, info, DokanResult.Success);
    }

    public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
    {
        files = null!;
        return Trace(MountPoint, nameof(FindFiles), fileName, info, DokanResult.NotImplemented);
    }

    public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files,
        IDokanFileInfo info)
    {
        var nodes = _cache.GetChildren((DirectoryNode)_cache.GetNode(fileName)!).ToList();
        files = new List<FileInformation>(nodes.Count);
        foreach (var node in nodes.Where(node =>
                     DokanHelper.DokanIsNameInExpression(searchPattern, node.Name, true)))
            files.Add(new FileInformation
            {
                Attributes = node.IsDirectory ? FileAttributes.Directory : FileAttributes.Normal,
                CreationTime = node.CreationTime,
                LastAccessTime = node.LastAccessTime,
                LastWriteTime = node.LastWriteTime,
                Length = node.Length,
                FileName = node.Name
            });
        return Trace(MountPoint, nameof(FindFilesWithPattern), fileName, info, DokanResult.Success);
    }

    public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
    {
        return Trace(MountPoint, nameof(SetFileAttributes), fileName, info, DokanResult.Success);
    }

    public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime,
        DateTime? lastWriteTime,
        IDokanFileInfo info)
    {
        var node = _cache.GetNode(fileName);
        _cache.SetFileTime(node!, creationTime, lastAccessTime, lastWriteTime);
        return Trace(MountPoint, nameof(SetFileTime), fileName, info, DokanResult.Success);
    }

    public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
    {
        var node = _cache.GetNode(fileName);
        if (node == null)
            return Trace(MountPoint, nameof(DeleteFile), fileName, info, DokanResult.FileNotFound);
        if (node.IsDirectory)
            return Trace(MountPoint, nameof(DeleteFile), fileName, info, DokanResult.AccessDenied);
        return Trace(MountPoint, nameof(DeleteFile), fileName, info, DokanResult.Success);
    }

    public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
    {
        var node = (DirectoryNode?)_cache.GetNode(fileName);
        if (node == null)
            return Trace(MountPoint, nameof(DeleteDirectory), fileName, info, DokanResult.FileNotFound);
        if (node.Children.Any())
            return Trace(MountPoint, nameof(DeleteDirectory), fileName, info, DokanResult.DirectoryNotEmpty);
        return Trace(MountPoint, nameof(DeleteDirectory), fileName, info, DokanResult.Success);
    }

    public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
    {
        var newNode = _cache.GetNode(newName);
        var oldNode = _cache.GetNode(oldName);
        var destination = _cache.GetNode(Path.GetDirectoryName(newName)!) as DirectoryNode;
        if (newNode == null)
        {
            if (destination == null)
                return Trace(MountPoint, nameof(MoveFile), oldName, info, DokanResult.PathNotFound);
            // oldNode!.Move(_deviceAccessor, newName, destination);
            _cache.Move(oldNode!, newName, destination);
            return Trace(MountPoint, nameof(MoveFile), oldName, info, DokanResult.Success);
        }

        if (replace)
        {
            if (info.IsDirectory) return DokanResult.AccessDenied;
            _cache.Delete(newNode);
            _cache.Move(oldNode!, newName, destination!);
            return Trace(MountPoint, nameof(MoveFile), oldName, info, DokanResult.Success);
        }

        return Trace(MountPoint, nameof(MoveFile), oldName, info, DokanResult.FileExists);
    }

    public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
    {
        var node = _cache.GetNode(fileName) as FileNode;
        _cache.SetLength(node!, length);
        return Trace(MountPoint, nameof(SetEndOfFile), fileName, info, DokanResult.Success);
    }

    public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
    {
        var node = _cache.GetNode(fileName) as FileNode;
        _cache.SetLength(node!, length);
        return Trace(MountPoint, nameof(SetAllocationSize), fileName, info, DokanResult.Success);
    }

    public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
    {
        return Trace(MountPoint, nameof(LockFile), fileName, info, DokanResult.Success);
    }

    public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
    {
        return Trace(MountPoint, nameof(UnlockFile), fileName, info, DokanResult.Success);
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
        return Trace(MountPoint, nameof(GetDiskFreeSpace), null, info, DokanResult.Success,
            "F:" + freeBytesAvailable,
            "T:" + totalNumberOfBytes);
    }

    public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features,
        out string fileSystemName,
        out uint maximumComponentLength, IDokanFileInfo info)
    {
        volumeLabel = _deviceAccessor.GetDevice().Name;
        features = FileSystemFeatures.UnicodeOnDisk | FileSystemFeatures.CasePreservedNames;
        fileSystemName = "Kurome";
        maximumComponentLength = 255;
        return Trace(MountPoint, nameof(GetVolumeInformation), null, info, DokanResult.Success);
    }

    public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity security,
        AccessControlSections sections,
        IDokanFileInfo info)
    {
        security = new FileSecurity();
        return Trace(MountPoint, nameof(GetFileSecurity), null, info, DokanResult.NotImplemented);
    }

    public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections,
        IDokanFileInfo info)
    {
        return Trace(MountPoint, nameof(SetFileSecurity), null, info, DokanResult.NotImplemented);
    }

    public NtStatus Mounted(string mountPoint, IDokanFileInfo info)
    {
        return Trace(MountPoint, nameof(Mounted), null, info, DokanResult.Success);
    }

    public NtStatus Unmounted(IDokanFileInfo info)
    {
        return Trace(MountPoint, nameof(Unmounted), null, info, DokanResult.Success);
    }

    public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
    {
        streams = Array.Empty<FileInformation>();
        return Trace(MountPoint, nameof(FindStreams), null, info, DokanResult.Success);
    }
}