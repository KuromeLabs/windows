using System.Collections.ObjectModel;
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

    private FileSystemTree _cache;

    protected int Trace(string driveLetter, string method, string? fileName, FileInfo info, int result,
        params object[]? parameters)

    {
        var extraParameters = parameters != null && parameters.Length > 0
            ? " " + string.Join(", ", parameters.Select(x => x.ToString()))
            : string.Empty;
        Log.Debug("{0} {1}[\"{2}\" | {3}{4}] -> {5}", driveLetter, method, fileName, info.ToString(),
            extraParameters, result.ToString());
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
        Log.Error(ex.ToString());
        return STATUS_SUCCESS;
    }

    public override int Init(object host)
    {
        Log.Information("Init");
        return STATUS_SUCCESS;
    }

    public override int Mounted(object host)
    {
        Log.Information("Mounted");
        return STATUS_SUCCESS;
    }

    public override void Unmounted(object host)
    {
        Log.Information("Unmounted");
        base.Unmounted(host);
    }

    public override int GetVolumeInfo([UnscopedRef] out VolumeInfo volumeInfo)
    {
        Log.Information("GetVolumeInfo");
        _device.GetSpace(out var total, out var free);
        volumeInfo = default;
        volumeInfo.TotalSize = (ulong)total;
        volumeInfo.FreeSize = (ulong)free;
        volumeInfo.SetVolumeLabel(VolumeLabel);
        return STATUS_SUCCESS;
    }

    public override int SetVolumeLabel(string volumeLabel, [UnscopedRef] out VolumeInfo volumeInfo)
    {
        Log.Information("SetVolumeLabel");
        VolumeLabel = volumeLabel;
        return GetVolumeInfo(out volumeInfo);
    }

    public override int GetSecurityByName(string fileName, [UnscopedRef] out uint fileAttributes,
        ref byte[] securityDescriptor)
    {
        Log.Information($"GetSecurityByName {fileName}");
        var node = _cache.GetNode(fileName);
        if (node == null)
        {
            fileAttributes = (uint)FileAttributes.Normal;
            return STATUS_OBJECT_NAME_NOT_FOUND;
        }

        fileAttributes = (uint)(node is { IsDirectory: true } ? FileAttributes.Directory : FileAttributes.Normal);
        return STATUS_SUCCESS;
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
        }

        Log.Information($"Open {fileName}, Path: {node?.FullName}, Size: {node?.Length}");
        // Log.Information(node.FullName);
        return STATUS_SUCCESS;
    }


    public override int GetFileInfo(object fileNode, object fileDesc, [UnscopedRef] out FileInfo fileInfo)
    {
        
        var node = (BaseNode)fileNode;
        Log.Information($"GetFileInfo Path:{node.FullName}");
        fileInfo = node.ToFileInfo();
        return STATUS_SUCCESS;
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
        Log.Information("SetBasicInfo");
        fileInfo = node.ToFileInfo();
        return STATUS_SUCCESS;
    }

    public override int SetFileSize(object fileNode, object fileDesc, ulong newSize, bool setAllocationSize,
        [UnscopedRef] out FileInfo fileInfo)
    {
        Log.Information($"SetFileSize Path:{((BaseNode) fileNode).FullName} newSize:{newSize} setAllocationSize:{setAllocationSize}");
        var node = (FileNode)fileNode;
        if (!setAllocationSize || newSize < (ulong)node.Length)
        {
            _cache.SetLength(node, (long)newSize);
        }

        fileInfo = node.ToFileInfo();
        return STATUS_SUCCESS;
    }

    public override int GetSecurity(object fileNode, object fileDesc, ref byte[] securityDescriptor)
    {
        Log.Information("GetSecurity");
        return STATUS_INVALID_DEVICE_REQUEST;
    }

    public override int SetSecurity(object fileNode, object fileDesc, AccessControlSections sections,
        byte[] securityDescriptor)
    {
        Log.Information("SetSecurity");
        return STATUS_INVALID_DEVICE_REQUEST;
    }

    public override int ReadDirectory(object fileNode, object fileDesc, string pattern, string marker, IntPtr buffer,
        uint length,
        [UnscopedRef] out uint bytesTransferred)
    {
        Log.Information("ReadDirectory");
        var directoryBuffer = new DirectoryBuffer();

        return BufferedReadDirectory(directoryBuffer, fileNode, fileDesc, pattern, marker, buffer, length,
            out bytesTransferred);
    }

    public override bool ReadDirectoryEntry(object fileNode, object fileDesc, string pattern, string? marker,
        ref object? context,
        [UnscopedRef] out string? fileName, [UnscopedRef] out FileInfo fileInfo)
    {
        fileName = default;
        fileInfo = default;
        var fileNode0 = (DirectoryNode)fileNode;
        Log.Information(
            $"Entering ReadDirectoryEntry, Marker: {marker}, Pattern: {pattern}, fileNode: {fileNode0.FullName}");
        var children = _cache.GetChildren(fileNode0);
        var childrenEnumerator = (IEnumerator<BaseNode>?)context;
        if (childrenEnumerator == null)
        {
            context = childrenEnumerator = children.GetEnumerator();
        }


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
        Log.Information($"ReadDirectoryEntry Child: {node.Name}, Path: {node.FullName}");
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
        return STATUS_SUCCESS;
    }

    public override int Write(object fileNode, object fileDesc, IntPtr buffer, ulong offset, uint length,
        bool writeToEndOfFile,
        bool constrainedIo, [UnscopedRef] out uint bytesTransferred, [UnscopedRef] out FileInfo fileInfo)
    {
        fileInfo = default;
        bytesTransferred = 0;
        var node = (FileNode)fileNode;
        if (node.Parent == null) return STATUS_SUCCESS;
        var fileSize = node.Length;


        var calculatedOffset = writeToEndOfFile ? fileSize : (long)offset;
        var bufferSize = constrainedIo ? Math.Min(fileSize - calculatedOffset, length) : length;
        var buffer0 = new byte[bufferSize];
        Log.Information(
            $"Write Path:{node.FullName}, currentFileSize:{node.Length} Offset:{offset}, calculatedOffset:{calculatedOffset} Length:{length}, bufferSize:{bufferSize} EOF:{writeToEndOfFile} constrainedIO:{constrainedIo}");
        Marshal.Copy(buffer, buffer0, 0, (int)bufferSize);
        _cache.Write(node, buffer0, calculatedOffset);
        bytesTransferred = (uint)bufferSize;
        fileInfo = node.ToFileInfo();
        return STATUS_SUCCESS;
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
        if (node != null) return STATUS_OBJECT_NAME_COLLISION;

        var parentPath = Path.GetDirectoryName(fileName);
        var parent = parentPath == null ? null : _cache.GetNode(parentPath) as DirectoryNode;
        if (parent == null) return STATUS_OBJECT_PATH_NOT_FOUND;
        if (0 != (createOptions & FILE_DIRECTORY_FILE))
        {
            Log.Information($"CreateDirectoryChild {fileName}");
            node = _cache.CreateDirectoryChild(parent, fileName);
        }
        else
        {
            Log.Information($"CreateFileChild {fileName}");
            node = _cache.CreateFileChild(parent, fileName);
        }

        fileNode = node;
        fileInfo = node.ToFileInfo();
        normalizedName = node.FullName;

        return STATUS_SUCCESS;
    }

    public override int Flush(object fileNode, object fileDesc, [UnscopedRef] out FileInfo fileInfo)
    {
        var node = (BaseNode)fileNode;
        fileInfo = node.ToFileInfo();
        return STATUS_SUCCESS;
    }

    public override int Overwrite(object fileNode, object fileDesc, uint fileAttributes, bool replaceFileAttributes,
        ulong allocationSize,
        [UnscopedRef] out FileInfo fileInfo)
    {
        var node = (BaseNode)fileNode;
        Log.Information($"Overwrite {node.FullName}");
        _cache.SetLength((FileNode)node, 0);
        fileInfo = node.ToFileInfo();
        return STATUS_SUCCESS;
    }

    public override int Rename(object fileNode, object fileDesc, string fileName, string newFileName,
        bool replaceIfExists)
    {
        Log.Information($"Rename - oldName:{fileName}, newName:{newFileName}, replaceIfExists:{replaceIfExists}");
        var newNode = _cache.GetNode(newFileName);
        var oldNode = _cache.GetNode(fileName);
        var destination = _cache.GetNode(Path.GetDirectoryName(newFileName)!) as DirectoryNode;
        if (newNode == null)
        {
            if (destination == null) return STATUS_OBJECT_PATH_NOT_FOUND;
            _cache.Move(oldNode, newFileName, destination);
            return STATUS_SUCCESS;
        }

        if (replaceIfExists)
        {
            if (newNode.IsDirectory) return STATUS_ACCESS_DENIED;
            _cache.Delete(newNode);
            _cache.Move(oldNode!, newFileName, destination!);
            return STATUS_SUCCESS;
        }

        return STATUS_OBJECT_NAME_EXISTS;
    }

    public override int CanDelete(object fileNode, object fileDesc, string fileName)
    {
        var node = (BaseNode)fileNode;
        var canDelete = node.IsDirectory && ((DirectoryNode)node).Children.Count > 0;
        if (canDelete)
            return STATUS_DIRECTORY_NOT_EMPTY;

        return STATUS_SUCCESS;
    }

    public override void Cleanup(object fileNode, object fileDesc, string fileName, uint flags)
    {
        var delete = 0 != (flags & CleanupDelete);
        if (delete) _cache.Delete((BaseNode)fileNode);

        Log.Information($"Cleanup fileName:{fileName}, delete:{delete}");
    }

    public override void Close(object? fileNode, object fileDesc)
    {
        var node = fileNode as BaseNode;
        
        Log.Information($"Close {node?.FullName}");
        fileNode = null;
    }
}