using System.IO;
using DokanNet;

namespace Kurome;

public abstract class BaseNode
{
    protected BaseNode(FileInformation fileInformation)
    {
        FileInformation = fileInformation;
    }

    public FileInformation FileInformation { get; private set; }
    protected DirectoryNode Parent { get; private set; }
    public string Name => FileInformation.FileName;
    public string Fullname => (Parent?.Fullname ?? string.Empty) + Name + "\\";

    public static BaseNode Create(FileInformation information)
    {
        if ((information.Attributes & FileAttributes.Directory) != 0)
            return new DirectoryNode(information);
        else
            return new FileNode(information);
    }

    public void SetParent(DirectoryNode parent)
    {
        Parent = parent;
    }
}