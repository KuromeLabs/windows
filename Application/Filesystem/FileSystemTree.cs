using Serilog;

namespace Application.Filesystem;

public class FileSystemTree
{
    private readonly ReaderWriterLockSlim _lock = new();
    public CacheNode Root;
    private readonly Device _device;

    public FileSystemTree(CacheNode root, Device device)
    {
        Root = root;
        _device = device;
    }

    public CacheNode? GetNode(string path)
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            var parts = new Queue<string>(path.Split('\\', StringSplitOptions.RemoveEmptyEntries));
            var result = (CacheNode)Root;
            while (result != null && parts.Count > 0 && (result.FileAttributes & (uint) FileAttributes.Directory) != 0)
                result = GetChild(parts.Dequeue(), (CacheNode)result);
            if (parts.Count > 0) result = null;
            return result;
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    private CacheNode? GetChild(string child, CacheNode node)
    {
        if (node.Children.Count == 0) UpdateChildren(node);
        return node.Children.TryGetValue(child, out var n) ? n : null;
    }

    public IEnumerable<CacheNode> GetChildren(CacheNode node)
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (node.Children.Count == 0) UpdateChildren(node);

            return node.Children.Values;
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    private void UpdateChildren(CacheNode node)
    {
        Log.Information("Attempting to get children from device with path {0}", node.FullName);
        Log.Information("Current Node's name: {0}", node.Name);
        _lock.EnterWriteLock();
        var nodes = _device.GetFileNodes(node.FullName);
        try
        {
            node.Children.Clear();
            foreach (var n in nodes)
            {
                n.Parent = node;
                node.Children.Add(n.Name, n);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public CacheNode CreateFileChild(CacheNode directory, string fileName)
    {
        _lock.EnterWriteLock();
        CacheNode node;
        try
        {
            _device.CreateEmptyFile(fileName);
            node = new CacheNode { Name = Path.GetFileName(fileName) };
            node.FileAttributes |= (uint)FileAttributes.Normal;
            directory.Children.Add(Path.GetFileName(fileName), node);
            node.Parent = directory;
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return node;
    }

    public CacheNode CreateDirectoryChild(CacheNode directory, string directoryName)
    {
        CacheNode node;
        _lock.EnterWriteLock();
        _device.CreateDirectory(directoryName);
        try
        {
            node = new CacheNode { Name = Path.GetFileName(directoryName) };
            node.FileAttributes |= (uint)FileAttributes.Directory;
            directory.Children.Add(Path.GetFileName(directoryName), node);
            node.Parent = directory;
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return node;
    }

    public void SetLength(CacheNode node, long length)
    {
        _lock.EnterWriteLock();
        _device.SetLength(node.FullName, length);
        try
        {
            node.Length = length;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Move(CacheNode oldNode, string newName, CacheNode destination)
    {
        _lock.EnterWriteLock();
        _device.Rename(oldNode.FullName, newName);
        try
        {
            oldNode.Parent!.Children.Remove(oldNode.Name);
            oldNode.Name = Path.GetFileName(newName);
            oldNode.Parent = destination;
            destination.Children.Add(Path.GetFileName(newName), oldNode);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Delete(CacheNode node)
    {
        _lock.EnterWriteLock();
        _device.Delete(node.FullName);
        try
        {
            node.Parent!.Children.Remove(node.Name);
            node.Parent = null;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void SetFileAttributes(CacheNode node, DateTime? cTime, DateTime? laTime, DateTime? lwTime, uint attributes)
    {
        var ucTime = cTime == null ? 0 : ((DateTimeOffset)cTime).ToUnixTimeMilliseconds();
        var ulaTime = laTime == null ? 0 : ((DateTimeOffset)laTime).ToUnixTimeMilliseconds();
        var ulwTime = lwTime == null ? 0 : ((DateTimeOffset)lwTime).ToUnixTimeMilliseconds();
        _lock.EnterWriteLock();
        
        try
        {
            node.FileAttributes = attributes;
            _device.SetFileTime(node.FullName, ucTime, ulaTime, ulwTime);
            node.CreationTime = cTime;
            node.LastAccessTime = laTime;
            node.LastWriteTime = lwTime;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Write(CacheNode node, Memory<byte> data, long offset)
    {
        _lock.EnterWriteLock();
        try
        {
            if (node.Length < offset + data.Length)
                node.Length = offset + data.Length;
            _device.WriteFileBuffer(data, node.FullName, offset);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    //we can cache this
    public int ReadFile(CacheNode node, byte[] buffer, long offset, int bytesToRead, long fileSize)
    {
        _lock.EnterReadLock();
        try
        {
            var data = _device.ReceiveFileBuffer(buffer, node.FullName, offset, bytesToRead, fileSize);
            return data;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}