using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using DokanNet;
using Serilog;
using FileAccess = DokanNet.FileAccess;

namespace Kurome.Core.Filesystem;

public class KuromeFs : IDokanOperations, IDokanOperationsUnsafe
{
    private readonly bool _caseInsensitive;
    private readonly Device _device;
    private readonly uint _maximumComponentLength;
    public string VolumeLabel = "Kurome";
    private string _mountPoint = "";
    private readonly ILogger _logger = Log.ForContext(typeof(KuromeFs));

    private FileSystemTree? _cache;

    protected NtStatus Trace(string driveLetter, string method, string? fileName, CacheNode? info, NtStatus result,
        string extra = "")

    {
        _logger.Debug("{0} {1}[\"{2}\" | {3} {4}] -> {5}", driveLetter, method, fileName, info, extra, result);
        return result;
    }

    public KuromeFs(bool caseInsensitive, Device device, uint maximumComponentLength = 127)
    {
        _caseInsensitive = caseInsensitive;
        _device = device;
        _maximumComponentLength = maximumComponentLength;
        VolumeLabel = _device.Name;
    }

    string illegalCharacters = "*";

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
            return Trace(_mountPoint, nameof(CreateFile), fileName, null, DokanResult.AccessDenied,
                $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
        var node = _cache?.GetNode(fileName);

        info.Context = node;
        var nodeExists = node != null;
        var parentPath = Path.GetDirectoryName(fileName);
        var parentNode = parentPath == null ? null : _cache.GetNode(parentPath);
        var parentNodeExists = parentNode != null;
        var nodeIsDirectory = nodeExists && (node!.FileAttributes & (uint)FileAttributes.Directory) != 0;
        if (nodeIsDirectory) info.IsDirectory = true;
        if (info.IsDirectory)
            switch (mode)
            {
                case FileMode.Open:
                    if (!nodeExists)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.PathNotFound,
                            $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    if (!nodeIsDirectory)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.NotADirectory,
                            $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    break;

                case FileMode.CreateNew:
                    if (nodeExists)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.AlreadyExists,
                            $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    lock (parentNode!.NodeLock)
                    {
                        var newNode = _cache.CreateDirectoryChild(parentNode!, fileName);
                        newNode.FileAttributes |= (uint)(attributes & ~FileAttributes.Normal);
                        info.Context = newNode;
                    }
                    break;
            }
        else
            switch (mode)
            {
                case FileMode.Open:
                    if (!nodeExists)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.FileNotFound,
                            $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    if (nodeIsDirectory)
                    {
                        if ((access & FileAccess.Delete) == FileAccess.Delete &&
                            (access & FileAccess.Synchronize) != FileAccess.Synchronize)
                            return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.AccessDenied,
                                $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");

                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.Success,
                            $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    }

                    break;
                case FileMode.CreateNew:
                    if (nodeExists)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.FileExists,
                            $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    if (!parentNodeExists)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.PathNotFound,
                            $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    lock (parentNode!.NodeLock)
                    {
                        var newNode = _cache.CreateFileChild(parentNode!, fileName);
                        newNode.FileAttributes |= (uint)(attributes & ~FileAttributes.Normal);
                        info.Context = newNode;
                    }
                    break;
                case FileMode.Create:
                    if (!parentNodeExists)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.PathNotFound,
                            $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    if (nodeExists)
                    {
                        node!.FileAttributes = (uint)(attributes | FileAttributes.Archive & ~FileAttributes.Normal);
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.AlreadyExists,
                            $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    }
                    lock (parentNode!.NodeLock)
                    {
                        var newNode0 = _cache.CreateFileChild(parentNode!, fileName);
                        newNode0.FileAttributes |= (uint)(attributes & ~FileAttributes.Normal);
                        info.Context = newNode0;
                    }
                    break;
                case FileMode.OpenOrCreate:
                    if (nodeExists)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.AlreadyExists,
                            $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    if (!parentNodeExists)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.PathNotFound,
                            $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    lock (parentNode!.NodeLock)
                    {
                        var newNode1 = _cache.CreateFileChild(parentNode!, fileName);
                        newNode1.FileAttributes |= (uint)(attributes & ~FileAttributes.Normal);
                        info.Context = newNode1;
                    }
                    break;
                case FileMode.Truncate:
                    if (!nodeExists)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.FileNotFound,
                            $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    break;
            }

        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.Success,
            $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
    }

    public void Cleanup(string fileName, IDokanFileInfo info)
    {
        var node = info.Context as CacheNode;
        lock (node!.NodeLock)
        {
            if (info.DeleteOnClose && node != null && (!info.IsDirectory || node.Children.Count == 0))
                _cache.Delete(node);
        }

        Trace(_mountPoint, nameof(Cleanup), fileName, node, DokanResult.Success, $"deleteOnClose:{info.DeleteOnClose}");
    }

    public void CloseFile(string fileName, IDokanFileInfo info)
    {
        info.Context = null;
        Trace(_mountPoint, nameof(CloseFile), fileName, null, DokanResult.Success);
    }

    public NtStatus ReadFile(string fileName, byte[] buffer, [UnscopedRef] out int bytesRead, long offset,
        IDokanFileInfo info)
    {
        var node = info.Context as CacheNode;
        lock (node!.NodeLock)
        {
            var size = node!.Length;
            if (offset >= size)
            {
                bytesRead = 0;
                return Trace(_mountPoint, nameof(ReadFile), fileName, node, DokanResult.Success,
                    $"R:{bytesRead}, O:{offset}");
            }

            var bytesToRead = offset + buffer.Length > size ? size - offset : buffer.Length;
            // bytesRead = _device.ReceiveFileBuffer(buffer, fileName, offset, (int)bytesToRead);
            bytesRead = 0;
            return Trace(_mountPoint, nameof(ReadFile), fileName, node, DokanResult.Success,
                $"bytesToRead:{bytesToRead}, R:{bytesRead}, O:{offset}");
        }
    }

    public NtStatus WriteFile(string fileName, byte[] buffer, [UnscopedRef] out int bytesWritten, long offset,
        IDokanFileInfo info)
    {
        var node = info.Context as CacheNode;
        // _cache.Write(node!, buffer, offset);
        bytesWritten = buffer.Length;
        return Trace(_mountPoint, nameof(WriteFile), fileName, node, DokanResult.Success,
            $"W:{bytesWritten}, O:{offset}");
    }

    public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
    {
        return Trace(_mountPoint, nameof(FlushFileBuffers), null, info.Context as CacheNode, DokanResult.Success);
    }

    public NtStatus GetFileInformation(string fileName, [UnscopedRef] out FileInformation fileInfo, IDokanFileInfo info)
    {
        var node = info.Context as CacheNode;
        fileInfo = node!.ToFileInfo();
        return Trace(_mountPoint, nameof(GetFileInformation), fileName, node, DokanResult.Success);
    }

    public NtStatus FindFiles(string fileName, [UnscopedRef] out IList<FileInformation> files, IDokanFileInfo info)
    {
        files = null!;
        return Trace(_mountPoint, nameof(FindFiles), fileName, info.Context as CacheNode, DokanResult.NotImplemented);
    }

    public NtStatus FindFilesWithPattern(string fileName, string searchPattern,
        [UnscopedRef] out IList<FileInformation> files,
        IDokanFileInfo info)
    {
        var dirNode = info.Context as CacheNode;
        lock (dirNode!.NodeLock)
        {
            var nodes = _cache.GetChildren(dirNode).ToList();
            files = new List<FileInformation>(nodes.Count);
            foreach (var node in nodes.Where(node =>
                         DokanHelper.DokanIsNameInExpression(searchPattern, node.Name, true)))
                files.Add(node.ToFileInfo());
        }

        return Trace(_mountPoint, nameof(FindFilesWithPattern), fileName, dirNode, DokanResult.Success);
    }

    public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
    {
        var node = (info.Context as CacheNode)!;
        lock (node!.NodeLock)
        {
            _logger.Information($"Current attributes: {node.FileAttributes}, isDirectory: {info.IsDirectory}");
            if (info.IsDirectory)
            {
                attributes |= FileAttributes.Directory;
                attributes &= ~FileAttributes.Normal;
            }
            else attributes |= FileAttributes.Archive;

            if (attributes != 0)
                node.FileAttributes = (uint)attributes;
        }

        return Trace(_mountPoint, nameof(SetFileAttributes), null, node, DokanResult.Success,
            $"Attributes: {attributes}");
    }

    public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime,
        DateTime? lastWriteTime,
        IDokanFileInfo info)
    {
        var node = (info.Context as CacheNode)!;
        lock (node!.NodeLock)
        {
            _cache.SetFileAttributes(node, creationTime, lastAccessTime, lastWriteTime, node.FileAttributes);
        }

        return Trace(_mountPoint, nameof(SetFileTime), fileName, node, DokanResult.Success);
    }

    public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
    {
        var node = (info.Context as CacheNode)!;
        if (info.IsDirectory)
            return Trace(_mountPoint, nameof(DeleteFile), fileName, node, DokanResult.AccessDenied);

        return Trace(_mountPoint, nameof(DeleteFile), fileName, node, DokanResult.Success);
    }

    public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
    {
        var node = (info.Context as CacheNode)!;
        var result = DokanResult.Success;
        lock (node.NodeLock)
        {
            if (node.Children.Count != 0)
                result = DokanResult.DirectoryNotEmpty;
        }

        return Trace(_mountPoint, nameof(DeleteDirectory), fileName, node, result);
    }

    public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
    {
        var newNode = _cache.GetNode(newName);
        var oldNode = _cache.GetNode(oldName);
        lock (oldNode!.NodeLock)
        {
            var destination = _cache.GetNode(Path.GetDirectoryName(newName)!);
            if (newNode == null)
            {
                if (destination == null)
                    return Trace(_mountPoint, nameof(MoveFile), oldName, oldNode, DokanResult.PathNotFound);
                // oldNode!.Move(_deviceAccessor, newName, destination);
                _cache.Move(oldNode!, newName, destination);
                return Trace(_mountPoint, nameof(MoveFile), oldName, oldNode, DokanResult.Success);
            }

            if (replace)
            {
                if (info.IsDirectory) return DokanResult.AccessDenied;
                _cache.Delete(newNode);
                _cache.Move(oldNode!, newName, destination!);
                return Trace(_mountPoint, nameof(MoveFile), oldName, oldNode, DokanResult.Success);
            }

            return Trace(_mountPoint, nameof(MoveFile), oldName, oldNode, DokanResult.FileExists);
        }
    }

    public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
    {
        var node = (info.Context as CacheNode)!;
        lock (node!.NodeLock)
        {
            _cache.SetLength(node!, length);
            return Trace(_mountPoint, nameof(SetEndOfFile), fileName, node, DokanResult.Success);
        }
    }

    public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
    {
        var node = (info.Context as CacheNode)!;
        lock (node!.NodeLock)
        {
            if (length < node.Length)
                _cache.SetLength(node, length);
            return Trace(_mountPoint, nameof(SetAllocationSize), fileName, node, DokanResult.Success);
        }
    }

    public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
    {
        return DokanResult.NotImplemented;
    }

    public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
    {
        return DokanResult.NotImplemented;
    }

    public NtStatus GetDiskFreeSpace([UnscopedRef] out long freeBytesAvailable,
        [UnscopedRef] out long totalNumberOfBytes,
        [UnscopedRef] out long totalNumberOfFreeBytes, IDokanFileInfo info)
    {
        totalNumberOfBytes = 0;
        totalNumberOfFreeBytes = 0;
        freeBytesAvailable = 0;
        if (_device.GetSpace(out var total, out var free))
        {
            totalNumberOfBytes = total;
            freeBytesAvailable = free;
            totalNumberOfFreeBytes = free;
            return Trace(_mountPoint, nameof(GetDiskFreeSpace), null, null, DokanResult.Success,
                $"Label: {VolumeLabel}, Total: {total}, Free: {free}");
        }

        ;
        return Trace(_mountPoint, nameof(GetDiskFreeSpace), null, null, DokanResult.Unsuccessful,
            $"Label: {VolumeLabel}, Total: {total}, Free: {free}");
    }

    public NtStatus GetVolumeInformation([UnscopedRef] out string volumeLabel,
        [UnscopedRef] out FileSystemFeatures features,
        [UnscopedRef] out string fileSystemName, [UnscopedRef] out uint maximumComponentLength, IDokanFileInfo info)
    {
        volumeLabel = VolumeLabel;
        features = FileSystemFeatures.CasePreservedNames | FileSystemFeatures.CaseSensitiveSearch |
                   FileSystemFeatures.UnicodeOnDisk;
        fileSystemName = "Kurome";
        maximumComponentLength = _maximumComponentLength;
        return Trace(_mountPoint, nameof(GetVolumeInformation), null, null, DokanResult.Success);
    }

    public NtStatus GetFileSecurity(string fileName, [UnscopedRef] out FileSystemSecurity security,
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
        _mountPoint = mountPoint;
        var root = _device.GetRootNode();
        _cache = new FileSystemTree(root, _device);
        return Trace(_mountPoint, nameof(Mounted), null, null, DokanResult.Success);
    }

    public NtStatus Unmounted(IDokanFileInfo info)
    {
        return DokanResult.Success;
    }

    public NtStatus FindStreams(string fileName, [UnscopedRef] out IList<FileInformation> streams, IDokanFileInfo info)
    {
        streams = Array.Empty<FileInformation>();
        return DokanResult.NotImplemented;
    }

    public NtStatus ReadFile(string fileName, IntPtr buffer, uint bufferLength, [UnscopedRef] out int bytesRead,
        long offset,
        IDokanFileInfo info)
    {
        var node = info.Context as CacheNode;
        lock (node!.NodeLock)
        {
            var size = node!.Length;
            if (offset >= size)
            {
                bytesRead = 0;
                return Trace(_mountPoint, nameof(ReadFile), fileName, node, DokanResult.Success,
                    $"R:{bytesRead}, O:{offset}");
            }

            var bytesToRead = offset + bufferLength > size ? size - offset : bufferLength;
            bytesRead = _device.ReceiveFileBufferUnsafe(buffer, node.FullName, offset, (int)bytesToRead);
            return Trace(_mountPoint, nameof(ReadFile), fileName, node, DokanResult.Success,
                $"bytesToRead:{bytesToRead}, R:{bytesRead}, O:{offset}");
        }
    }

    public NtStatus WriteFile(string fileName, IntPtr buffer, uint bufferLength, [UnscopedRef] out int bytesWritten,
        long offset,
        IDokanFileInfo info)
    {
        var node = info.Context as CacheNode;
        lock (node!.NodeLock)
        {
            _device.WriteFileBufferUnsafe(buffer, fileName, offset, (int)bufferLength);
            if (offset + bufferLength > node.Length)
                node.Length = offset + bufferLength;
            bytesWritten = (int)bufferLength;
            return Trace(_mountPoint, nameof(WriteFile), fileName, node, DokanResult.Success,
                $"W:{bytesWritten}, O:{offset}");
        }
    }
}