namespace Application.Filesystem;

public class FileNode : BaseNode
{
    public override long Length { get; set; }

    public override bool IsDirectory => false;
}