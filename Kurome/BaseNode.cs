using System;
using System.IO;
using DokanNet;

namespace Kurome;

public abstract class BaseNode
{
    protected BaseNode(FileInformation fileInformation)
    {
        FileInformation = fileInformation;
    }

    public FileInformation FileInformation { get; protected set; }
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
    
    public void SetFileTime(DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime, Device device)
    {
        var fileInformation = FileInformation;
        fileInformation.CreationTime = creationTime;
        fileInformation.LastAccessTime = lastAccessTime;
        fileInformation.LastWriteTime = lastWriteTime;
        FileInformation = fileInformation;
        var cTime = creationTime == null ? 0 : ((DateTimeOffset) creationTime).ToUnixTimeMilliseconds();
        var laTime = lastAccessTime == null ? 0 : ((DateTimeOffset) lastAccessTime).ToUnixTimeMilliseconds();
        var lwTime = lastWriteTime == null ? 0 : ((DateTimeOffset) lastWriteTime).ToUnixTimeMilliseconds();
        device.SetFileTime(Fullname, cTime, laTime, lwTime);
    }

    public void Move(Device device, string newName, DirectoryNode destination)
    {
        var newFileInfo = FileInformation;
        newFileInfo.FileName = Path.GetFileName(newName);
        var newNode = Create(newFileInfo);
        if (destination._children == null)
            destination.GetChildrenNodes(device);
        else
        {
            destination._children.Add(newNode.Name, newNode);
            newNode.SetParent(destination);
        }
        Parent._children.Remove(Name);
        SetParent(null);
        device.Rename(Fullname, newName);
    }

    public void Delete(Device device)
    {
        Parent._children.Remove(Name);
        device.Delete(Fullname);
        SetParent(null);
    }
}