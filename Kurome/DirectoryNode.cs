using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DokanNet;

namespace Kurome;

public class DirectoryNode : BaseNode
{
    public Dictionary<string, BaseNode> _children;

    public DirectoryNode(FileInformation fileInformation) : base(fileInformation)
    {
    }

    //If children cache is null, get this node's children synchronously
    //Otherwise return the cache and update it in the background TODO: limit cache updates
    public IEnumerable<BaseNode> GetChildrenNodes(Device device)
    {
        if (_children == null)
            UpdateChildrenNodes(device);
        // else
        //     Task.Run(() => UpdateChildren(device));
        return _children.Values;
    }

    private void UpdateChildrenNodes(Device device)
    {
        _children = device.GetFileNodes(Fullname).Select(Create).ToDictionary(x => x.Name);
        foreach (var node in _children.Values)
            node.SetParent(this);
    }

    public BaseNode GetChild(Device device, string name)
    {
        GetChildrenNodes(device);
        return _children.TryGetValue(name, out var node) ? node : null;
    }
}