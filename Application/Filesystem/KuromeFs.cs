using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using Fsp;
using Fsp.Interop;
using Serilog;
using FileInfo = Fsp.Interop.FileInfo;

namespace Application.Filesystem;

public class KuromeFs : FileSystemBase
{
    private readonly bool _caseInsensitive;
    private readonly Device _device;
    public string VolumeLabel = "Kurome";
    private string _mountPoint = "";
    private FileSystemHost _host = null!;
    private readonly ILogger _logger = Log.ForContext(typeof(KuromeFs));

    private FileSystemTree _cache;

    protected int Trace(string driveLetter, string method, string? fileName, BaseNode? info, int result,
        string extra = "")

    {
        _logger.Debug("{0} {1}[\"{2}\" | {3}{4}] -> {5}", driveLetter, method, fileName, info, extra, result);
        return result;
    }

    public KuromeFs(bool caseInsensitive, Device device)
    {
        _caseInsensitive = caseInsensitive;
        _device = device;
        _cache = new(new DirectoryNode
        {
            Name = "\\"
        }, _device);
        VolumeLabel = _device.Name;
    }

    public override int ExceptionHandler(Exception ex)
    {
        _logger.Error(ex.ToString());
        return STATUS_SUCCESS;
    }

    public override int Init(object host)
    {
        _host = (FileSystemHost)host;
        return Trace(_mountPoint, nameof(Init), null, null, STATUS_SUCCESS);
    }

    public override int Mounted(object host)
    {
        _mountPoint = _host.MountPoint();
        return Trace(_mountPoint, nameof(Mounted), null, null, STATUS_SUCCESS);
    }

    public override void Unmounted(object host)
    {
        Trace(_mountPoint, nameof(Unmounted), null, null, STATUS_SUCCESS);
    }

    public override int GetVolumeInfo([UnscopedRef] out VolumeInfo volumeInfo)
    {
        _device.GetSpace(out var total, out var free);
        volumeInfo = default;
        volumeInfo.TotalSize = (ulong)total;
        volumeInfo.FreeSize = (ulong)free;
        volumeInfo.SetVolumeLabel(VolumeLabel);
        return Trace(_mountPoint, nameof(GetVolumeInfo), null, null, STATUS_SUCCESS,
            $"Label: {VolumeLabel}, Total: {total}, Free: {free}");
    }

    public override int SetVolumeLabel(string volumeLabel, [UnscopedRef] out VolumeInfo volumeInfo)
    {
        VolumeLabel = volumeLabel;
        Trace(_mountPoint, nameof(SetVolumeLabel), null, null, STATUS_SUCCESS, "Label: " + VolumeLabel);
        return GetVolumeInfo(out volumeInfo);
    }

    public override int GetSecurityByName(string fileName, [UnscopedRef] out uint fileAttributes,
        ref byte[] securityDescriptor)
    {
        var node = _cache.GetNode(fileName);
        if (node == null)
        {
            fileAttributes = (uint)FileAttributes.Normal;
            return STATUS_OBJECT_NAME_NOT_FOUND;
        }

        fileAttributes = (uint)(node is { IsDirectory: true } ? FileAttributes.Directory : FileAttributes.Normal);
        return Trace(_mountPoint, nameof(GetSecurityByName), fileName, node, STATUS_SUCCESS);
    }

    public override int Open(string fileName, uint createOptions, uint grantedAccess,
        [UnscopedRef] out object? fileNode,
        [UnscopedRef] out object? fileDesc, [UnscopedRef] out FileInfo fileInfo,
        [UnscopedRef] out string? normalizedName)
    {
        var node = _cache.GetNode(fileName);
        fileNode = default;
        fileDesc = null;
        fileInfo = default;
        normalizedName = default;
        if (node != null)
        {
            fileNode = node;
            normalizedName = node.FullName;
            fileInfo = node.ToFileInfo();
            return Trace(_mountPoint, nameof(Open), fileName, node, STATUS_SUCCESS);
        }

        return Trace(_mountPoint, nameof(Open), fileName, node, STATUS_OBJECT_NAME_NOT_FOUND);
    }


    public override int GetFileInfo(object fileNode, object fileDesc, [UnscopedRef] out FileInfo fileInfo)
    {
        var node = (BaseNode)fileNode;
        fileInfo = node.ToFileInfo();
        return Trace(_mountPoint, nameof(GetFileInfo), node.FullName, node, STATUS_SUCCESS);
    }

    public override int SetBasicInfo(object fileNode, object fileDesc, uint fileAttributes, ulong creationTime,
        ulong lastAccessTime,
        ulong lastWriteTime, ulong changeTime, [UnscopedRef] out FileInfo fileInfo)
    {
        var node = (BaseNode)fileNode;
        var newCreationTime = creationTime == 0 ? node.CreationTime : DateTime.FromFileTimeUtc((long)creationTime);
        var newLastAccessTime =
            lastAccessTime == 0 ? node.LastAccessTime : DateTime.FromFileTimeUtc((long)lastAccessTime);
        var newLastWriteTime = lastWriteTime == 0 ? node.LastWriteTime : DateTime.FromFileTimeUtc((long)lastWriteTime);
        // TODO: file attributes
        _cache.SetFileTime(node, newCreationTime, newLastAccessTime, newLastWriteTime);
        fileInfo = node.ToFileInfo();
        return Trace(_mountPoint, nameof(SetBasicInfo), null, node, STATUS_SUCCESS,
            $"Attributes: {fileAttributes}, CreationTime: {newCreationTime}, LastAccessTime: {newLastAccessTime}, LastWriteTime: {newLastWriteTime}");
    }

    public override int SetFileSize(object fileNode, object fileDesc, ulong newSize, bool setAllocationSize,
        [UnscopedRef] out FileInfo fileInfo)
    {
        var node = (FileNode)fileNode;
        if (!setAllocationSize || newSize < (ulong)node.Length)
        {
            _cache.SetLength(node, (long)newSize);
        }

        fileInfo = node.ToFileInfo();
        return Trace(_mountPoint, nameof(SetFileSize), null, node, STATUS_SUCCESS,
            $"NewSize: {newSize}, SetAllocationSize: {setAllocationSize}");
    }

    public override int GetSecurity(object fileNode, object fileDesc, ref byte[] securityDescriptor)
    {
        return Trace(_mountPoint, nameof(GetSecurity), null, (BaseNode)fileNode, STATUS_INVALID_DEVICE_REQUEST);
    }

    public override int SetSecurity(object fileNode, object fileDesc, AccessControlSections sections,
        byte[] securityDescriptor)
    {
        return Trace(_mountPoint, nameof(SetSecurity), null, (BaseNode)fileNode, STATUS_INVALID_DEVICE_REQUEST);
    }

    public override int ReadDirectory(object fileNode, object fileDesc, string pattern, string marker, IntPtr buffer,
        uint length,
        [UnscopedRef] out uint bytesTransferred)
    {
        var directoryBuffer = new DirectoryBuffer();

        var result = BufferedReadDirectory(directoryBuffer, fileNode, fileDesc, pattern, marker, buffer, length,
            out bytesTransferred);
        return Trace(_mountPoint, nameof(ReadDirectory), null, (BaseNode)fileNode, result,
            $"Pattern: {pattern}, Marker: {marker}, Length: {length}, BytesTransferred: {bytesTransferred}");
    }

    public override bool ReadDirectoryEntry(object fileNode, object fileDesc, string pattern, string? marker,
        ref object? context,
        [UnscopedRef] out string? fileName, [UnscopedRef] out FileInfo fileInfo)
    {
        fileName = default;
        fileInfo = default;
        var fileNode0 = (DirectoryNode)fileNode;
        var children = _cache.GetChildren(fileNode0);
        var childrenEnumerator = (IEnumerator<BaseNode>?)context;
        if (childrenEnumerator == null)
        {
            context = childrenEnumerator = children.GetEnumerator();
        }

        Trace(_mountPoint, nameof(ReadDirectoryEntry), null, fileNode0, -1,
            $"Pattern: {pattern}, Marker: {marker}, Context: {context}");
        if (!childrenEnumerator.MoveNext())
        {
            childrenEnumerator.Dispose();
            return false;
        }

        if (marker != null)
        {
            while (childrenEnumerator.Current.Name != marker)
            {
                if (!childrenEnumerator.MoveNext())
                {
                    childrenEnumerator.Dispose();
                    return false;
                }
            }

            if (!childrenEnumerator.MoveNext())
            {
                childrenEnumerator.Dispose();
                return false;
            }
        }

        var node = childrenEnumerator.Current;
        fileName = node.Name;
        fileInfo = node.ToFileInfo();
        return true;
    }

    public override int Read(object fileNode, object fileDesc, IntPtr buffer, ulong offset, uint length,
        [UnscopedRef] out uint bytesTransferred)
    {
        var node = (FileNode)fileNode;
        var size = node.Length;
        var bytesToRead = (long)offset + length > size ? size - (long)offset : length;
        var buffer0 = new byte[length];
        bytesTransferred = (uint)_cache.ReadFile(node, buffer0, (long)offset, (int)bytesToRead, node.Length);
        Marshal.Copy(buffer0, 0, buffer, (int)bytesTransferred);
        return Trace(_mountPoint, nameof(Read), null, node, STATUS_SUCCESS,
            $"Offset: {offset}, Length: {length}, BytesTransferred: {bytesTransferred}");
    }

    public override int Write(object fileNode, object fileDesc, IntPtr buffer, ulong offset, uint length,
        bool writeToEndOfFile,
        bool constrainedIo, [UnscopedRef] out uint bytesTransferred, [UnscopedRef] out FileInfo fileInfo)
    {
        fileInfo = default;
        bytesTransferred = 0;
        var node = (FileNode)fileNode;
        if (node.Parent == null) return STATUS_UNEXPECTED_IO_ERROR;
        var fileSize = node.Length;


        var calculatedOffset = writeToEndOfFile ? fileSize : (long)offset;
        var bufferSize = constrainedIo ? Math.Min(fileSize - calculatedOffset, length) : length;
        var buffer0 = new byte[bufferSize];
        Marshal.Copy(buffer, buffer0, 0, (int)bufferSize);
        _cache.Write(node, buffer0, calculatedOffset);
        bytesTransferred = (uint)bufferSize;
        fileInfo = node.ToFileInfo();
        return Trace(_mountPoint, nameof(Write), null, node, STATUS_SUCCESS,
            $"Offset: {offset}, Length: {length}, WriteToEndOfFile: {writeToEndOfFile}, ConstrainedIo: {constrainedIo}, BytesTransferred: {bytesTransferred}");
    }

    public override int Create(string fileName, uint createOptions, uint grantedAccess, uint fileAttributes,
        byte[] securityDescriptor,
        ulong allocationSize, [UnscopedRef] out object? fileNode, [UnscopedRef] out object fileDesc,
        [UnscopedRef] out FileInfo fileInfo, [UnscopedRef] out string normalizedName)
    {
        fileNode = null;
        fileDesc = null;
        fileInfo = default;
        normalizedName = null;

        var node = _cache.GetNode(fileName);
        if (node != null) return Trace(_mountPoint, nameof(Create), fileName, node, STATUS_OBJECT_NAME_COLLISION);

        var parentPath = Path.GetDirectoryName(fileName);
        var parent = parentPath == null ? null : _cache.GetNode(parentPath) as DirectoryNode;
        if (parent == null) return Trace(_mountPoint, nameof(Create), fileName, node, STATUS_OBJECT_PATH_NOT_FOUND);
        if (0 != (createOptions & FILE_DIRECTORY_FILE))
            node = _cache.CreateDirectoryChild(parent, fileName);
        else
            node = _cache.CreateFileChild(parent, fileName);


        fileNode = node;
        fileInfo = node.ToFileInfo();
        normalizedName = node.FullName;

        return Trace(_mountPoint, nameof(Create), fileName, node, STATUS_SUCCESS,
            $"CreateOptions: {createOptions}, GrantedAccess: {grantedAccess}, FileAttributes: {fileAttributes}");
    }

    public override int Flush(object? fileNode, object fileDesc, [UnscopedRef] out FileInfo fileInfo)
    {
        var node = fileNode as BaseNode;
        if (node == null) fileInfo = default;
        else fileInfo = node.ToFileInfo();
        return Trace(_mountPoint, nameof(Flush), null, node, STATUS_SUCCESS);
    }

    public override int Overwrite(object fileNode, object fileDesc, uint fileAttributes, bool replaceFileAttributes,
        ulong allocationSize,
        [UnscopedRef] out FileInfo fileInfo)
    {
        var node = (BaseNode)fileNode;
        _cache.SetLength((FileNode)node, 0);
        fileInfo = node.ToFileInfo();
        return Trace(_mountPoint, nameof(Overwrite), null, node, STATUS_SUCCESS,
            $"FileAttributes: {fileAttributes}, ReplaceFileAttributes: {replaceFileAttributes}, AllocationSize: {allocationSize}");
    }

    public override int Rename(object fileNode, object fileDesc, string fileName, string newFileName,
        bool replaceIfExists)
    {
        _logger.Information($"Rename - oldName:{fileName}, newName:{newFileName}, replaceIfExists:{replaceIfExists}");
        var newNode = _cache.GetNode(newFileName);
        var oldNode = (BaseNode) fileNode;
        var destination = _cache.GetNode(Path.GetDirectoryName(newFileName)!) as DirectoryNode;
        if (newNode == null)
        {
            if (destination == null) return Trace(_mountPoint, nameof(Rename), fileName, oldNode, STATUS_OBJECT_PATH_NOT_FOUND);
            _cache.Move(oldNode, newFileName, destination);
            return Trace(_mountPoint, nameof(Rename), fileName, oldNode, STATUS_SUCCESS, $"NewNode: {newNode}");
        }

        if (replaceIfExists)
        {
            if (newNode.IsDirectory) return Trace(_mountPoint, nameof(Rename), fileName, oldNode, STATUS_ACCESS_DENIED, "NewNode is directory");
            _cache.Delete(newNode);
            _cache.Move(oldNode!, newFileName, destination!);
            return Trace(_mountPoint, nameof(Rename), fileName, oldNode, STATUS_SUCCESS, $"NewNode: {newNode}, Replaced");
        }

        return Trace(_mountPoint, nameof(Rename), fileName, oldNode, STATUS_OBJECT_NAME_COLLISION);
    }

    public override int CanDelete(object fileNode, object fileDesc, string fileName)
    {
        var node = (BaseNode)fileNode;
        var cantDelete = node.IsDirectory && ((DirectoryNode)node).Children.Count > 0;
        if (cantDelete)
            return Trace(_mountPoint, nameof(CanDelete), fileName, node, STATUS_DIRECTORY_NOT_EMPTY);

        return Trace(_mountPoint, nameof(CanDelete), fileName, node, STATUS_SUCCESS);
    }

    public override void Cleanup(object fileNode, object fileDesc, string fileName, uint flags)
    {
        var delete = 0 != (flags & CleanupDelete);
        var node = (BaseNode)fileNode;
        if (delete)
        {
            if (node.IsDirectory)
            {
                var dirNode = (DirectoryNode)node;
                if (dirNode.Children.Count > 0)
                {
                    Trace(_mountPoint, nameof(Cleanup), fileName, node, -1, $"Delete: {delete}, Directory not empty (not deleting)");
                    return;
                }
            }

            _cache.Delete(node);
        }

        Trace(_mountPoint, nameof(Cleanup), fileName, node, -1, $"Delete: {delete}");
    }

    public override void Close(object? fileNode, object fileDesc)
    {
        var node = fileNode as BaseNode;

        Trace(_mountPoint, nameof(Close), null, node, -1);
    }
}