namespace Domain.FileSystem;

public abstract class BaseNode
{
    public DirectoryNode? Parent { get; set; }
    public required string Name { get; set; } 
    public string FullName => (Parent?.FullName ?? string.Empty) + Name + "\\";
    public DateTime? CreationTime { get; set; } = DateTime.Now;
    public DateTime? LastAccessTime { get; set; } = DateTime.Now;
    public DateTime? LastWriteTime { get; set; } = DateTime.Now;
    public abstract long Length { get; set; }
    public abstract bool IsDirectory { get; }
}