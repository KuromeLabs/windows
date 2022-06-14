using Application.Interfaces;
using Domain;

namespace Application.Models.Dokany;

public abstract class BaseNode
{
    protected BaseNode(KuromeInformation fileInformation)
    {
        KuromeInformation = fileInformation;
    }

    public KuromeInformation KuromeInformation { get; protected set; }
    private DirectoryNode? Parent { get; set; }
    public string Name => KuromeInformation.FileName;
    protected string Fullname => (Parent?.Fullname ?? string.Empty) + Name + "\\";

    protected static BaseNode Create(KuromeInformation information)
    {
        if (information.IsDirectory)
            return new DirectoryNode(information);
        else
            return new FileNode(information);
    }

    public void SetParent(DirectoryNode? parent)
    {
        Parent = parent;
    }

    public void SetFileTime(DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime,
        IDeviceAccessor deviceAccessor)
    {
        KuromeInformation.CreationTime = creationTime;
        KuromeInformation.LastAccessTime = lastAccessTime;
        KuromeInformation.LastWriteTime = lastWriteTime;
        var cTime = creationTime == null ? 0 : ((DateTimeOffset) creationTime).ToUnixTimeMilliseconds();
        var laTime = lastAccessTime == null ? 0 : ((DateTimeOffset) lastAccessTime).ToUnixTimeMilliseconds();
        var lwTime = lastWriteTime == null ? 0 : ((DateTimeOffset) lastWriteTime).ToUnixTimeMilliseconds();
        deviceAccessor.SetFileTime(Fullname, cTime, laTime, lwTime);
    }

    public void Move(IDeviceAccessor deviceAccessor, string newName, DirectoryNode destination)
    {
        deviceAccessor.Rename(Fullname, newName);
        Parent!.Children!.Remove(Name);
        KuromeInformation.FileName = Path.GetFileName(newName);
        var newNode = Create(KuromeInformation);
        if (destination.Children == null)
            destination.GetChildrenNodes(deviceAccessor);
        else
        {
            destination.Children.Add(newNode.Name, newNode);
            newNode.SetParent(destination);
        }

        SetParent(null);
    }

    public void Delete(IDeviceAccessor deviceAccessor)
    {
        Parent!.Children!.Remove(Name);
        deviceAccessor.Delete(Fullname);
        SetParent(null);
    }
}