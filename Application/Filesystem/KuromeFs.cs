using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using DokanNet;
using Serilog;
using FileAccess = DokanNet.FileAccess;

namespace Application.Filesystem;

public class KuromeFs : IDokanOperations, IDokanOperationsUnsafe
{
    private readonly bool _caseInsensitive;
    private readonly Device _device;
    private readonly uint _maximumComponentLength;
    public string VolumeLabel = "Kurome";
    private string _mountPoint = "";
    private readonly ILogger _logger = Log.ForContext(typeof(KuromeFs));

    private FileSystemTree _cache;

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
        var root = _device.GetRootNode();
        _cache = new FileSystemTree(root, _device);
    }

    // public override int ExceptionHandler(Exception ex)
    // {
    //     _logger.Error(ex.ToString());
    //     return STATUS_SUCCESS;
    // }
    //
    // public override int Init(object host)
    // {
    //     _host = (FileSystemHost)host;
    //     var root = _device.GetRootNode();
    //     _cache = new FileSystemTree(root, _device);
    //     return Trace(_mountPoint, nameof(Init), null, null, STATUS_SUCCESS);
    // }
    //
    // public override int Mounted(object host)
    // {
    //     _mountPoint = _host.MountPoint();
    //     return Trace(_mountPoint, nameof(Mounted), null, null, STATUS_SUCCESS);
    // }
    //
    // public override void Unmounted(object host)
    // {
    //     Trace(_mountPoint, nameof(Unmounted), null, null, STATUS_SUCCESS);
    // }
    //
    // public override int GetVolumeInfo([UnscopedRef] out VolumeInfo volumeInfo)
    // {
    //     _device.GetSpace(out var total, out var free);
    //     volumeInfo = default;
    //     volumeInfo.TotalSize = (ulong)total;
    //     volumeInfo.FreeSize = (ulong)free;
    //     volumeInfo.SetVolumeLabel(VolumeLabel);
    //     return Trace(_mountPoint, nameof(GetVolumeInfo), null, null, STATUS_SUCCESS,
    //         $"Label: {VolumeLabel}, Total: {total}, Free: {free}");
    // }
    //
    // public override int SetVolumeLabel(string volumeLabel, [UnscopedRef] out VolumeInfo volumeInfo)
    // {
    //     VolumeLabel = volumeLabel;
    //     Trace(_mountPoint, nameof(SetVolumeLabel), null, null, STATUS_SUCCESS, "Label: " + VolumeLabel);
    //     return GetVolumeInfo(out volumeInfo);
    // }
    //
    // public override int GetSecurityByName(string fileName, [UnscopedRef] out uint fileAttributes,
    //     ref byte[] securityDescriptor)
    // {
    //     var node = _cache.GetNode(fileName);
    //     if (node == null)
    //     {
    //         fileAttributes = (uint)FileAttributes.Normal;
    //         return STATUS_OBJECT_NAME_NOT_FOUND;
    //     }
    //     fileAttributes = node.FileAttributes;
    //     return Trace(_mountPoint, nameof(GetSecurityByName), fileName, node, STATUS_SUCCESS);
    // }
    //
    // public override int Open(string fileName, uint createOptions, uint grantedAccess,
    //     [UnscopedRef] out object? fileNode,
    //     [UnscopedRef] out object? fileDesc, [UnscopedRef] out FileInfo fileInfo,
    //     [UnscopedRef] out string? normalizedName)
    // {
    //     var node = _cache.GetNode(fileName);
    //     fileNode = default;
    //     fileDesc = null;
    //     fileInfo = default;
    //     normalizedName = default;
    //     if (node != null)
    //     {
    //         fileNode = node;
    //         normalizedName = node.FullName;
    //         fileInfo = node.ToFileInfo();
    //         return Trace(_mountPoint, nameof(Open), fileName, node, STATUS_SUCCESS);
    //     }
    //
    //     return Trace(_mountPoint, nameof(Open), fileName, node, STATUS_OBJECT_NAME_NOT_FOUND);
    // }
    //
    //
    // public override int GetFileInfo(object fileNode, object fileDesc, [UnscopedRef] out FileInfo fileInfo)
    // {
    //     var node = (CacheNode)fileNode;
    //     fileInfo = node.ToFileInfo();
    //     return Trace(_mountPoint, nameof(GetFileInfo), node.FullName, node, STATUS_SUCCESS);
    // }
    //
    // public override int SetBasicInfo(object fileNode, object fileDesc, uint fileAttributes, ulong creationTime,
    //     ulong lastAccessTime,
    //     ulong lastWriteTime, ulong changeTime, [UnscopedRef] out FileInfo fileInfo)
    // {
    //     var node = (CacheNode)fileNode;
    //     var newCreationTime = creationTime == 0 ? node.CreationTime : DateTime.FromFileTimeUtc((long)creationTime);
    //     var newLastAccessTime =
    //         lastAccessTime == 0 ? node.LastAccessTime : DateTime.FromFileTimeUtc((long)lastAccessTime);
    //     var newLastWriteTime = lastWriteTime == 0 ? node.LastWriteTime : DateTime.FromFileTimeUtc((long)lastWriteTime);
    //     var attributes = fileAttributes == unchecked((uint)(-1)) ? node.FileAttributes : fileAttributes;
    //     _cache.SetFileAttributes(node, newCreationTime, newLastAccessTime, newLastWriteTime, attributes);
    //     fileInfo = node.ToFileInfo();
    //     return Trace(_mountPoint, nameof(SetBasicInfo), null, node, STATUS_SUCCESS,
    //         $"Attributes: {fileAttributes}, CreationTime: {newCreationTime}, LastAccessTime: {newLastAccessTime}, LastWriteTime: {newLastWriteTime}");
    // }
    //
    // public override int SetFileSize(object fileNode, object fileDesc, ulong newSize, bool setAllocationSize,
    //     [UnscopedRef] out FileInfo fileInfo)
    // {
    //     var node = (CacheNode)fileNode;
    //     if (!setAllocationSize || newSize < (ulong)node.Length)
    //     {
    //         _cache.SetLength(node, (long)newSize);
    //     }
    //
    //     fileInfo = node.ToFileInfo();
    //     return Trace(_mountPoint, nameof(SetFileSize), null, node, STATUS_SUCCESS,
    //         $"NewSize: {newSize}, SetAllocationSize: {setAllocationSize}");
    // }
    //
    // public override int GetSecurity(object fileNode, object fileDesc, ref byte[] securityDescriptor)
    // {
    //     return Trace(_mountPoint, nameof(GetSecurity), null, (CacheNode)fileNode, STATUS_INVALID_DEVICE_REQUEST);
    // }
    //
    // public override int SetSecurity(object fileNode, object fileDesc, AccessControlSections sections,
    //     byte[] securityDescriptor)
    // {
    //     return Trace(_mountPoint, nameof(SetSecurity), null, (CacheNode)fileNode, STATUS_INVALID_DEVICE_REQUEST);
    // }
    //
    // public override int ReadDirectory(object fileNode, object fileDesc, string pattern, string marker, IntPtr buffer,
    //     uint length,
    //     [UnscopedRef] out uint bytesTransferred)
    // {
    //     var directoryBuffer = new DirectoryBuffer();
    //
    //     var result = BufferedReadDirectory(directoryBuffer, fileNode, fileDesc, pattern, marker, buffer, length,
    //         out bytesTransferred);
    //     return Trace(_mountPoint, nameof(ReadDirectory), null, (CacheNode)fileNode, result,
    //         $"Pattern: {pattern}, Marker: {marker}, Length: {length}, BytesTransferred: {bytesTransferred}");
    // }
    //
    // public override bool ReadDirectoryEntry(object fileNode, object fileDesc, string pattern, string? marker,
    //     ref object? context,
    //     [UnscopedRef] out string? fileName, [UnscopedRef] out FileInfo fileInfo)
    // {
    //     fileName = default;
    //     fileInfo = default;
    //     var fileNode0 = (CacheNode)fileNode;
    //     var children = _cache.GetChildren(fileNode0);
    //     var childrenEnumerator = (IEnumerator<CacheNode>?)context;
    //     if (childrenEnumerator == null)
    //     {
    //         context = childrenEnumerator = children.GetEnumerator();
    //     }
    //
    //     Trace(_mountPoint, nameof(ReadDirectoryEntry), null, fileNode0, -1,
    //         $"Pattern: {pattern}, Marker: {marker}");
    //     if (!childrenEnumerator.MoveNext())
    //     {
    //         childrenEnumerator.Dispose();
    //         return false;
    //     }
    //
    //     if (marker != null)
    //     {
    //         while (childrenEnumerator.Current.Name != marker)
    //         {
    //             if (!childrenEnumerator.MoveNext())
    //             {
    //                 childrenEnumerator.Dispose();
    //                 return false;
    //             }
    //         }
    //
    //         if (!childrenEnumerator.MoveNext())
    //         {
    //             childrenEnumerator.Dispose();
    //             return false;
    //         }
    //     }
    //
    //     var node = childrenEnumerator.Current;
    //     fileName = node.Name;
    //     fileInfo = node.ToFileInfo();
    //     return true;
    // }
    //
    // public override int Read(object fileNode, object fileDesc, IntPtr buffer, ulong offset, uint length,
    //     [UnscopedRef] out uint bytesTransferred)
    // {
    //     var node = (CacheNode)fileNode;
    //     var size = node.Length;
    //     var bytesToRead = (long)offset + length > size ? size - (long)offset : length;
    //     var buffer0 = new byte[length];
    //     bytesTransferred = (uint)_cache.ReadFile(node, buffer0, (long)offset, (int)bytesToRead, node.Length);
    //     Marshal.Copy(buffer0, 0, buffer, (int)bytesTransferred);
    //     return Trace(_mountPoint, nameof(Read), null, node, STATUS_SUCCESS,
    //         $"Offset: {offset}, Length: {length}, BytesTransferred: {bytesTransferred}");
    // }
    //
    // public override int Write(object fileNode, object fileDesc, IntPtr buffer, ulong offset, uint length,
    //     bool writeToEndOfFile,
    //     bool constrainedIo, [UnscopedRef] out uint bytesTransferred, [UnscopedRef] out FileInfo fileInfo)
    // {
    //     fileInfo = default;
    //     bytesTransferred = 0;
    //     var node = (CacheNode)fileNode;
    //     if (node.Parent == null) return STATUS_UNEXPECTED_IO_ERROR;
    //     var fileSize = node.Length;
    //
    //
    //     var calculatedOffset = writeToEndOfFile ? fileSize : (long)offset;
    //     var bufferSize = constrainedIo ? Math.Min(fileSize - calculatedOffset, length) : length;
    //     var buffer0 = new byte[bufferSize];
    //     Marshal.Copy(buffer, buffer0, 0, (int)bufferSize);
    //     _cache.Write(node, buffer0, calculatedOffset);
    //     bytesTransferred = (uint)bufferSize;
    //     fileInfo = node.ToFileInfo();
    //     return Trace(_mountPoint, nameof(Write), null, node, STATUS_SUCCESS,
    //         $"Offset: {offset}, Length: {length}, WriteToEndOfFile: {writeToEndOfFile}, ConstrainedIo: {constrainedIo}, BytesTransferred: {bytesTransferred}");
    // }
    //
    // public override int Create(string fileName, uint createOptions, uint grantedAccess, uint fileAttributes,
    //     byte[] securityDescriptor,
    //     ulong allocationSize, [UnscopedRef] out object? fileNode, [UnscopedRef] out object fileDesc,
    //     [UnscopedRef] out FileInfo fileInfo, [UnscopedRef] out string normalizedName)
    // {
    //     fileNode = null;
    //     fileDesc = null;
    //     fileInfo = default;
    //     normalizedName = null;
    //
    //     var node = _cache.GetNode(fileName);
    //     if (node != null) return Trace(_mountPoint, nameof(Create), fileName, node, STATUS_OBJECT_NAME_COLLISION);
    //
    //     var parentPath = Path.GetDirectoryName(fileName);
    //     var parent = parentPath == null ? null : _cache.GetNode(parentPath);
    //     if (parent == null) return Trace(_mountPoint, nameof(Create), fileName, node, STATUS_OBJECT_PATH_NOT_FOUND);
    //     if (0 != (createOptions & FILE_DIRECTORY_FILE))
    //         node = _cache.CreateDirectoryChild(parent, fileName);
    //     else
    //         node = _cache.CreateFileChild(parent, fileName);
    //
    //     node.FileAttributes = (node.FileAttributes & (uint)FileAttributes.Directory) != 0
    //         ? fileAttributes
    //         : fileAttributes | (uint)FileAttributes.Archive;
    //     fileNode = node;
    //     fileInfo = node.ToFileInfo();
    //     normalizedName = node.FullName;
    //
    //     return Trace(_mountPoint, nameof(Create), fileName, node, STATUS_SUCCESS,
    //         $"CreateOptions: {createOptions}, GrantedAccess: {grantedAccess}, FileAttributes: {fileAttributes}");
    // }
    //
    // public override int Flush(object? fileNode, object fileDesc, [UnscopedRef] out FileInfo fileInfo)
    // {
    //     var node = fileNode as CacheNode;
    //     if (node == null) fileInfo = default;
    //     else fileInfo = node.ToFileInfo();
    //     return Trace(_mountPoint, nameof(Flush), null, node, STATUS_SUCCESS);
    // }
    //
    // public override int Overwrite(object fileNode, object fileDesc, uint fileAttributes, bool replaceFileAttributes,
    //     ulong allocationSize,
    //     [UnscopedRef] out FileInfo fileInfo)
    // {
    //     var node = (CacheNode)fileNode;
    //     _cache.SetLength(node, 0);
    //     if (replaceFileAttributes)
    //         node.FileAttributes = fileAttributes | (uint)FileAttributes.Archive;
    //     else
    //         node.FileAttributes |= fileAttributes | (uint)FileAttributes.Archive;
    //     fileInfo = node.ToFileInfo();
    //     return Trace(_mountPoint, nameof(Overwrite), null, node, STATUS_SUCCESS,
    //         $"FileAttributes: {fileAttributes}, ReplaceFileAttributes: {replaceFileAttributes}, AllocationSize: {allocationSize}");
    // }
    //
    // public override int Rename(object fileNode, object fileDesc, string fileName, string newFileName,
    //     bool replaceIfExists)
    // {
    //     _logger.Information($"Rename - oldName:{fileName}, newName:{newFileName}, replaceIfExists:{replaceIfExists}");
    //     var newNode = _cache.GetNode(newFileName);
    //     var oldNode = (CacheNode)fileNode;
    //     var destination = _cache.GetNode(Path.GetDirectoryName(newFileName)!);
    //     if (newNode == null)
    //     {
    //         if (destination == null)
    //             return Trace(_mountPoint, nameof(Rename), fileName, oldNode, STATUS_OBJECT_PATH_NOT_FOUND);
    //         _cache.Move(oldNode, newFileName, destination);
    //         return Trace(_mountPoint, nameof(Rename), fileName, oldNode, STATUS_SUCCESS, $"NewNode: {newNode}");
    //     }
    //
    //     if (replaceIfExists)
    //     {
    //         var isDirectory = (newNode.FileAttributes & (int)FileAttributes.Directory) != 0;
    //         if (isDirectory)
    //             return Trace(_mountPoint, nameof(Rename), fileName, oldNode, STATUS_ACCESS_DENIED,
    //                 "NewNode is directory");
    //         _cache.Delete(newNode);
    //         _cache.Move(oldNode!, newFileName, destination!);
    //         return Trace(_mountPoint, nameof(Rename), fileName, oldNode, STATUS_SUCCESS,
    //             $"NewNode: {newNode}, Replaced");
    //     }
    //
    //     return Trace(_mountPoint, nameof(Rename), fileName, oldNode, STATUS_OBJECT_NAME_COLLISION);
    // }
    //
    // public override int CanDelete(object fileNode, object fileDesc, string fileName)
    // {
    //     var node = (CacheNode)fileNode;
    //     var isDirectory = (node.FileAttributes & (int)FileAttributes.Directory) != 0;
    //     var cantDelete = isDirectory && (node).Children.Count > 0;
    //     if (cantDelete)
    //         return Trace(_mountPoint, nameof(CanDelete), fileName, node, STATUS_DIRECTORY_NOT_EMPTY);
    //
    //     return Trace(_mountPoint, nameof(CanDelete), fileName, node, STATUS_SUCCESS);
    // }
    //
    // public override void Cleanup(object fileNode, object fileDesc, string fileName, uint flags)
    // {
    //     var delete = 0 != (flags & CleanupDelete);
    //     var node = (CacheNode)fileNode;
    //     var isDirectory = (node.FileAttributes & (int)FileAttributes.Directory) != 0;
    //     if (delete)
    //     {
    //         if (isDirectory)
    //         {
    //             if (node.Children.Count > 0)
    //             {
    //                 Trace(_mountPoint, nameof(Cleanup), fileName, node, -1,
    //                     $"Delete: {delete}, Directory not empty (not deleting)");
    //                 return;
    //             }
    //         }
    //
    //         _cache.Delete(node);
    //     }
    //
    //     Trace(_mountPoint, nameof(Cleanup), fileName, node, -1, $"Delete: {delete}");
    // }
    //
    // public override void Close(object? fileNode, object fileDesc)
    // {
    //     var node = fileNode as CacheNode;
    //
    //     Trace(_mountPoint, nameof(Close), null, node, -1);
    // }
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
        if (fileName.Split('\\').Any(x => Encoding.UTF8.GetByteCount(x) > _maximumComponentLength || x.Length > _maximumComponentLength))
        {
            _logger.Information($"For filename {fileName.Split('\\')[^1]} with size {fileName.Split('\\')[^1].Length} and byte count {Encoding.Default.GetByteCount(fileName.Split('\\')[^1])} returning invalid name");
            return DokanResult.InvalidName;
        };
        
        if (illegalCharacters.Any(fileName.Contains)) return DokanResult.InvalidName;
        //Access to these directories is prohibited in newer version of Android.
        //Android\obb can be accessed with REQUEST_INSTALL_PACKAGES Android permission.
        //TODO: Find workaround/ask for root/ask permission (for obb)/etc.
        if (fileName.StartsWith("\\Android\\data") || fileName.StartsWith("\\Android\\obb"))
            return Trace(_mountPoint, nameof(CreateFile), fileName, null, DokanResult.AccessDenied, $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
        var node = _cache.GetNode(fileName);
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
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.PathNotFound, $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    if (!nodeIsDirectory)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.NotADirectory, $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    break;

                case FileMode.CreateNew:
                    if (nodeExists)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.AlreadyExists, $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    var newNode = _cache.CreateDirectoryChild(parentNode!, fileName);
                    newNode.FileAttributes |= (uint) (attributes & ~FileAttributes.Normal);
                    info.Context = newNode;
                    break;
            }
        else
            switch (mode)
            {
                case FileMode.Open:
                    if (!nodeExists)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.FileNotFound, $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    if (nodeIsDirectory)
                    {
                        if ((access & FileAccess.Delete) == FileAccess.Delete &&
                            (access & FileAccess.Synchronize) != FileAccess.Synchronize)
                            return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.AccessDenied, $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                        
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.Success, $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    }

                    break;
                case FileMode.CreateNew:
                    if (nodeExists)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.FileExists, $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    if (!parentNodeExists)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.PathNotFound, $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    var newNode = _cache.CreateFileChild(parentNode!, fileName);
                    newNode.FileAttributes |= (uint) (attributes & ~FileAttributes.Normal);
                    info.Context = newNode;
                    break;
                case FileMode.Create:
                    if (!parentNodeExists)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.PathNotFound, $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    if (nodeExists)
                    {
                        node!.FileAttributes = (uint) (attributes | FileAttributes.Archive & ~FileAttributes.Normal);
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.AlreadyExists,
                            $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    }
                    var newNode0 = _cache.CreateFileChild(parentNode!, fileName);
                    newNode0.FileAttributes |= (uint) (attributes & ~FileAttributes.Normal);
                    info.Context = newNode0;
                    break;
                case FileMode.OpenOrCreate:
                    if (nodeExists)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.AlreadyExists, $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    if (!parentNodeExists)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.PathNotFound, $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    var newNode1 = _cache.CreateFileChild(parentNode!, fileName);
                    newNode1.FileAttributes |= (uint) (attributes & ~FileAttributes.Normal);
                    info.Context = newNode1;
                    break;
                case FileMode.Truncate:
                    if (!nodeExists)
                        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.FileNotFound, $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
                    break;
            }

        return Trace(_mountPoint, nameof(CreateFile), fileName, node, DokanResult.Success, $"Mode: {mode}, Attributes: {attributes}, Options: {options}, Share: {share}, Access: {access}");
    }

    public void Cleanup(string fileName, IDokanFileInfo info)
    {
        var node = info.Context as CacheNode;
        if (info.DeleteOnClose && node != null && (!info.IsDirectory || node.Children.Count == 0))
            _cache.Delete(node);
            
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
        var size = node!.Length;
        if (offset >= size)
        {
            bytesRead = 0;
            return Trace(_mountPoint, nameof(ReadFile), fileName, node, DokanResult.Success,
                $"R:{bytesRead}, O:{offset}");
        }

        var bytesToRead = offset + buffer.Length > size ? size - offset : buffer.Length;
        bytesRead = _cache.ReadFile(node, buffer, offset, (int)bytesToRead, size);
        return Trace(_mountPoint, nameof(ReadFile), fileName, node, DokanResult.Success,
            $"bytesToRead:{bytesToRead}, R:{bytesRead}, O:{offset}");
    }

    public NtStatus WriteFile(string fileName, byte[] buffer, [UnscopedRef] out int bytesWritten, long offset,
        IDokanFileInfo info)
    {
        var node = info.Context as CacheNode;
        _cache.Write(node!, buffer, offset);
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
        var nodes = _cache.GetChildren(dirNode!).ToList();
        files = new List<FileInformation>(nodes.Count);
        foreach (var node in nodes.Where(node =>
                     DokanHelper.DokanIsNameInExpression(searchPattern, node.Name, true)))
            files.Add(node.ToFileInfo());
        return Trace(_mountPoint, nameof(FindFilesWithPattern), fileName, dirNode, DokanResult.Success);
    }

    public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
    {
        var node = (info.Context as CacheNode)!;

        _logger.Information($"Current attributes: {node.FileAttributes}, isDirectory: {info.IsDirectory}");
        if (info.IsDirectory)
        {
            attributes |= FileAttributes.Directory;
            attributes &= ~FileAttributes.Normal;
        }
        else attributes |= FileAttributes.Archive;
        if (attributes != 0)
            node.FileAttributes = (uint) attributes;
        return Trace(_mountPoint, nameof(SetFileAttributes), null, node, DokanResult.Success,
            $"Attributes: {attributes}");
    }

    public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime,
        DateTime? lastWriteTime,
        IDokanFileInfo info)
    {
        var node = (info.Context as CacheNode)!;
        _cache.SetFileAttributes(node, creationTime, lastAccessTime, lastWriteTime, node.FileAttributes);
        return Trace(_mountPoint, nameof(SetFileTime), fileName, node, DokanResult.Success);
    }

    public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
    {
        var node = (info.Context as CacheNode)!;
        // if (node == null)
        //     return Trace(MountPoint, nameof(DeleteFile), fileName, info, DokanResult.FileNotFound);
        if (info.IsDirectory)
            return Trace(_mountPoint, nameof(DeleteFile), fileName, node, DokanResult.AccessDenied);
        return Trace(_mountPoint, nameof(DeleteFile), fileName, node, DokanResult.Success);
    }

    public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
    {
        var node = (info.Context as CacheNode)!;
        // if (node == null)
        //     return Trace(MountPoint, nameof(DeleteDirectory), fileName, info, DokanResult.FileNotFound);
        if (node.Children.Any())
            return Trace(_mountPoint, nameof(DeleteDirectory), fileName, node, DokanResult.DirectoryNotEmpty);
        return Trace(_mountPoint, nameof(DeleteDirectory), fileName, node, DokanResult.Success);
    }

    public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
    {
        var newNode = _cache.GetNode(newName);
        var oldNode = _cache.GetNode(oldName);
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

    public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
    {
        var node = (info.Context as CacheNode)!;
        _cache.SetLength(node!, length);
        return Trace(_mountPoint, nameof(SetEndOfFile), fileName, node, DokanResult.Success);
    }

    public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
    {
        var node = (info.Context as CacheNode)!;
        if (length < node.Length)
            _cache.SetLength(node, length);
        return Trace(_mountPoint, nameof(SetAllocationSize), fileName, node, DokanResult.Success);
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
        _device.GetSpace(out var total, out var free);
        totalNumberOfBytes = total;
        freeBytesAvailable = free;
        totalNumberOfFreeBytes = free;
        return Trace(_mountPoint, nameof(GetDiskFreeSpace), null, null, DokanResult.Success,
            $"Label: {VolumeLabel}, Total: {total}, Free: {free}");
    }

    public NtStatus GetVolumeInformation([UnscopedRef] out string volumeLabel,
        [UnscopedRef] out FileSystemFeatures features,
        [UnscopedRef] out string fileSystemName, [UnscopedRef] out uint maximumComponentLength, IDokanFileInfo info)
    {
        volumeLabel = VolumeLabel;
        features = FileSystemFeatures.CasePreservedNames | FileSystemFeatures.CaseSensitiveSearch |
                   FileSystemFeatures.UnicodeOnDisk | FileSystemFeatures.NamedStreams;
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

    public NtStatus ReadFile(string fileName, IntPtr buffer, uint bufferLength, [UnscopedRef] out int bytesRead, long offset,
        IDokanFileInfo info)
    {
        var node = info.Context as CacheNode;
        var size = node!.Length;
        if (offset >= size)
        {
            bytesRead = 0;
            return Trace(_mountPoint, nameof(ReadFile), fileName, node, DokanResult.Success,
                $"R:{bytesRead}, O:{offset}");
        }

        var bytesToRead = offset + bufferLength > size ? size - offset : bufferLength;
        bytesRead = _cache.ReadFileUnsafe(node, buffer, offset, (int)bytesToRead, size);
        return Trace(_mountPoint, nameof(ReadFile), fileName, node, DokanResult.Success,
            $"bytesToRead:{bytesToRead}, R:{bytesRead}, O:{offset}");
    }

    public NtStatus WriteFile(string fileName, IntPtr buffer, uint bufferLength, [UnscopedRef] out int bytesWritten, long offset,
        IDokanFileInfo info)
    {
        var bytes = new byte[bufferLength];
        Marshal.Copy(buffer, bytes, 0, (int)bufferLength);
        return WriteFile(fileName, bytes, out bytesWritten, offset, info);
    }
}