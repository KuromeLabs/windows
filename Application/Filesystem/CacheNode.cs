using DokanNet;

namespace Application.Filesystem;

public class CacheNode
{
    public CacheNode? Parent { get; set; }
    public required string Name { get; set; }

    public string FullName => (Parent?.FullName ?? string.Empty) +
                              (Parent != null && Parent.Name != "\\" ? "\\" : string.Empty) + Name;

    public DateTime? CreationTime { get; set; } = DateTime.Now;
    public DateTime? LastAccessTime { get; set; } = DateTime.Now;
    public DateTime? LastWriteTime { get; set; } = DateTime.Now;
    public long Length { get; set; } = 0;
    public uint FileAttributes { get; set; }
    public readonly Dictionary<string, CacheNode> Children = new();

    public FileInformation ToFileInfo()
    {
        return new FileInformation
        {
            Attributes = (FileAttributes) FileAttributes,
            CreationTime = CreationTime,
            LastAccessTime = LastAccessTime,
            LastWriteTime = LastWriteTime,
            Length = Length,
            FileName = Name
        };
    }

    public override string ToString()
    {
        return $"Name: {Name}, Path: {FullName}, Length: {Length}, Attributes: {FileAttributes}";
    }
}