namespace Domain.FileSystem;

public class DirectoryNode : BaseNode
{
    public readonly Dictionary<string, BaseNode> Children = new();
    public override long Length { get => 0; set { } }
    public override bool IsDirectory => true;
}