using System.Collections.Concurrent;

namespace Kurome.Core.Filesystem;

public class FileSystemTree
{
    public CacheNode Root;
    private readonly Device _device;
    private readonly ConcurrentDictionary<string, byte> _nodeChildrenUpdateOperations = new();


    public FileSystemTree(CacheNode root, Device device)
    {
        Root = root;
        _device = device;
    }

    public CacheNode? GetNode(string path)
    {
        var parts = new Queue<string>(path.Split('\\', StringSplitOptions.RemoveEmptyEntries));
        var result = Root;
        while (result != null && parts.Count > 0 && (result.FileAttributes & (uint)FileAttributes.Directory) != 0)
            result = GetChild(parts.Dequeue(), result);
        if (parts.Count > 0) result = null;
        return result;
    }

    private CacheNode? GetChild(string child, CacheNode node)
    {
        if (!node.ChildrenRefreshed)
            UpdateChildren(node);
        return node.Children.TryGetValue(child, out var n) ? n : null;
    }

    public IEnumerable<CacheNode> GetChildren(CacheNode node)
    {
        if (!node.ChildrenRefreshed)
            UpdateChildren(node);
        else
            Task.Run(() => UpdateChildren(node)).ConfigureAwait(false);
        
        return node.Children.Values;
    }

    private void UpdateChildren(CacheNode node)
    {
        if (_nodeChildrenUpdateOperations.ContainsKey(node.FullName) && 
            DateTime.Now - node.LastChildrenRefresh < TimeSpan.FromSeconds(5)) return;
        _nodeChildrenUpdateOperations.TryAdd(node.FullName, 0);
        node.LastChildrenRefresh = DateTime.Now;

        var nodes = _device.GetChildrenNodes(node);
        if (nodes == null)
        {
            node.Children.Clear();
            return;
        }

        foreach (var newNode in nodes)
        {
            if (!node.Children.TryGetValue(newNode.Key, out var existingNode))
                node.Children.TryAdd(newNode.Key, newNode.Value);
            else
            {
                lock (existingNode.NodeLock)
                {
                    existingNode.Update(newNode.Value);
                    existingNode.ChildrenRefreshed = false;
                }
            }
        }

        foreach (var oldNode in node.Children)
        {
            if (!nodes.ContainsKey(oldNode.Key))
                node.Children.TryRemove(oldNode.Key, out _);
        }
        _nodeChildrenUpdateOperations.TryRemove(node.FullName, out _);
        node.ChildrenRefreshed = true;
    }

    public CacheNode CreateFileChild(CacheNode directory, string fileName)
    {
        _device.CreateEmptyFile(fileName);
        var node = new CacheNode { Name = Path.GetFileName(fileName) };
        node.FileAttributes |= (uint)FileAttributes.Archive;
        directory.Children.TryAdd(Path.GetFileName(fileName), node);
        node.Parent = directory;
        return node;
    }

    public CacheNode CreateDirectoryChild(CacheNode directory, string directoryName)
    {
        _device.CreateDirectory(directoryName);
        var node = new CacheNode { Name = Path.GetFileName(directoryName) };
        node.FileAttributes |= (uint)FileAttributes.Directory;
        directory.Children.TryAdd(Path.GetFileName(directoryName), node);
        node.Parent = directory;
        return node;
    }

    public void SetLength(CacheNode node, long length)
    {
        _device.SetFileAttributes(node.FullName,
            ((DateTimeOffset)node.CreationTime).ToUnixTimeMilliseconds(),
            ((DateTimeOffset)node.LastAccessTime).ToUnixTimeMilliseconds(),
            ((DateTimeOffset)node.LastWriteTime).ToUnixTimeMilliseconds(),
            node.FileAttributes, length);
        node.Length = length;
    }

    public void Move(CacheNode oldNode, string newName, CacheNode destination)
    {
        _device.Rename(oldNode.FullName, newName);
        oldNode.Parent!.Children.TryRemove(oldNode.Name, out _);
        oldNode.Name = Path.GetFileName(newName);
        oldNode.Parent = destination;
        destination.Children.TryAdd(Path.GetFileName(newName), oldNode);
    }

    public void Delete(CacheNode node)
    {
        _device.Delete(node.FullName);
        node.Parent!.Children.TryRemove(node.Name, out _);
        node.Parent = null;
    }

    public void SetFileAttributes(CacheNode node, DateTime? cTime, DateTime? laTime, DateTime? lwTime, uint attributes)
    {
        var ucTime = cTime == null
            ? ((DateTimeOffset)node.CreationTime).ToUnixTimeMilliseconds()
            : ((DateTimeOffset)cTime.Value).ToUnixTimeMilliseconds();
        var ulaTime = laTime == null
            ? ((DateTimeOffset)node.LastAccessTime).ToUnixTimeMilliseconds()
            : ((DateTimeOffset)laTime.Value).ToUnixTimeMilliseconds();
        var ulwTime = lwTime == null
            ? ((DateTimeOffset)node.LastWriteTime).ToUnixTimeMilliseconds()
            : ((DateTimeOffset)lwTime.Value).ToUnixTimeMilliseconds();
        node.FileAttributes = attributes;
        _device.SetFileAttributes(node.FullName, ucTime, ulaTime, ulwTime, attributes, node.Length);
        node.CreationTime = cTime ?? node.CreationTime;
        node.LastAccessTime = laTime ?? node.CreationTime;
        node.LastWriteTime = lwTime ?? node.CreationTime;
    }
}