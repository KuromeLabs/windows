using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DokanNet;

namespace Kurome;

public class DirectoryNode : BaseNode
{
    private Dictionary<string, BaseNode> _children;

    public DirectoryNode(FileInformation fileInformation) : base(fileInformation)
    {
    }

    //If children cache is null, get this node's children synchronously
    //Otherwise return the cache and update it in the background
    public IEnumerable<BaseNode> GetChildren(Device device)
    {
        if (_children == null)
            UpdateChildren(device);
        else
            Task.Run(() => UpdateChildren(device));
        return _children.Values;
    }

    private void UpdateChildren(Device device)
    {
        _children = device.GetFileNodes(Fullname).Select(Create).ToDictionary(x => x.Name);
        foreach (var node in _children.Values)
            node.SetParent(this);
    }

    public BaseNode GetChild(Device device, string name)
    {
        GetChildren(device);
        return _children.TryGetValue(name, out var node) ? node : null;
    }
}