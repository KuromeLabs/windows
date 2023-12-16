using FileInfo = Fsp.Interop.FileInfo;

namespace Application.Filesystem;

public abstract class BaseNode
{
    public DirectoryNode? Parent { get; set; }
    public required string Name { get; set; }

    public string FullName => (Parent?.FullName ?? string.Empty) +
                              (Parent != null && Parent.Name != "\\" ? "\\" : string.Empty) + Name;

    public DateTime? CreationTime { get; set; } = DateTime.Now;
    public DateTime? LastAccessTime { get; set; } = DateTime.Now;
    public DateTime? LastWriteTime { get; set; } = DateTime.Now;
    public abstract long Length { get; set; }
    public abstract bool IsDirectory { get; }

    public FileInfo ToFileInfo()
    {
        return new FileInfo
        {
            CreationTime = (ulong)(CreationTime?.ToFileTimeUtc() ?? 0),
            LastAccessTime = (ulong)(LastAccessTime?.ToFileTimeUtc() ?? 0),
            LastWriteTime = (ulong)(LastWriteTime?.ToFileTimeUtc() ?? 0),
            FileAttributes = (uint)(IsDirectory ? FileAttributes.Directory : FileAttributes.Normal),
            FileSize = (ulong)Length
        };
    }
}