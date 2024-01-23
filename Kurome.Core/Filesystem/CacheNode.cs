using System.Collections.Concurrent;
using DokanNet;

namespace Kurome.Core.Filesystem;

public class CacheNode
{
    public CacheNode? Parent { get; set; }
    public required string Name { get; set; }

    public string FullName => (Parent?.FullName ?? string.Empty) +
                              (Parent != null && Parent.Name != "\\" ? "\\" : string.Empty) + Name;

    public DateTime CreationTime { get; set; } = DateTime.Now;
    public DateTime LastAccessTime { get; set; } = DateTime.Now;
    public DateTime LastWriteTime { get; set; } = DateTime.Now;
    public long Length { get; set; } = 0;
    public uint FileAttributes { get; set; }
    public bool ChildrenRefreshed = false;
    public DateTime LastChildrenRefresh = DateTime.Now;
    public readonly ConcurrentDictionary<string, CacheNode> Children = new();
    public readonly object NodeLock = new();

    public FileInformation ToFileInfo()
    {
        return new FileInformation
        {
            Attributes = (FileAttributes)FileAttributes,
            CreationTime = CreationTime,
            LastAccessTime = LastAccessTime,
            LastWriteTime = LastWriteTime,
            Length = Length,
            FileName = Name
        };
    }

    public void Update(CacheNode node)
    {
        Name = node.Name;
        CreationTime = node.CreationTime;
        LastAccessTime = node.LastAccessTime;
        LastWriteTime = node.LastWriteTime;
        Length = node.Length;
        FileAttributes = node.FileAttributes;
    }

    public override string ToString()
    {
        return $"Name: {Name}, Path: {FullName}, Length: {Length}, Attributes: {FileAttributes}";
    }
}