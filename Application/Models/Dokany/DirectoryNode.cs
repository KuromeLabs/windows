using Application.Interfaces;
using Domain;

namespace Application.Models.Dokany;

public class DirectoryNode : BaseNode
{
    public Dictionary<string, BaseNode> Children = new();

    public DirectoryNode(KuromeInformation fileInformation) : base(fileInformation)
    {
    }

    //If children cache is null, get this node's children synchronously
    //Otherwise return the cache and update it in the background TODO: limit cache updates
    public IEnumerable<BaseNode> GetChildrenNodes(IDeviceAccessor deviceAccessor)
    {
        if (Children.Values.Count == 0)
            UpdateChildrenNodes(deviceAccessor);
        // else
        //     Task.Run(() => UpdateChildren(device));
        return Children.Values;
    }

    private void UpdateChildrenNodes(IDeviceAccessor deviceAccessor)
    {
        Children = deviceAccessor.GetFileNodes(Fullname).Select(Create).ToDictionary(x => x.Name);
        foreach (var node in Children.Values)
            node.SetParent(this);
    }

    public BaseNode? GetChild(IDeviceAccessor deviceAccessor, string name)
    {
        GetChildrenNodes(deviceAccessor);
        return Children.TryGetValue(name, out var node) ? node : null;
    }

    public void CreateFileChild(IDeviceAccessor deviceAccessor, string fileName)
    {
        var node = Create(new KuromeInformation
        {
            FileName = Path.GetFileName(fileName),
            IsDirectory = false,
            CreationTime = DateTime.Now,
            LastWriteTime = DateTime.Now,
            LastAccessTime = DateTime.Now,
            Length = 0
        }) as FileNode;
        Children.Add(Path.GetFileName(fileName), node!);
        node.SetParent(this);
        deviceAccessor.CreateEmptyFile(fileName);
    }

    public void CreateDirectoryChild(IDeviceAccessor deviceAccessor, string directoryName)
    {
        var node = Create(new KuromeInformation
        {
            FileName = Path.GetFileName(directoryName),
            IsDirectory = true,
            CreationTime = DateTime.Now,
            LastWriteTime = DateTime.Now,
            LastAccessTime = DateTime.Now,
            Length = 0
        }) as DirectoryNode;
        Children.Add(node!.Name, node);
        node.SetParent(this);
        deviceAccessor.CreateDirectory(directoryName);
    }
}