using Application.Interfaces;
using Domain.FileSystem;

namespace Application.Dokany;

public class FileSystemTree
{
    private readonly IDeviceAccessor _accessor;
    private readonly ReaderWriterLockSlim _lock = new();
    public DirectoryNode? Root;

    public FileSystemTree(IDeviceAccessor accessor)
    {
        _accessor = accessor;
    }

    public BaseNode? GetNode(string path)
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (Root == null)
            {
                _lock.EnterWriteLock();
                try
                {
                    Root = (DirectoryNode)_accessor.GetRootNode();
                    Root.Name = "";
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            var parts = new Queue<string>(path.Split('\\', StringSplitOptions.RemoveEmptyEntries));
            var result = (BaseNode)Root;
            while (result != null && parts.Count > 0 && result.IsDirectory)
                result = GetChild(parts.Dequeue(), (DirectoryNode)result);
            if (parts.Count > 0) result = null;
            return result;
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    private BaseNode? GetChild(string child, DirectoryNode node)
    {
        if (node.Children.Count == 0) UpdateChildren(node);
        return node.Children.TryGetValue(child, out var n) ? n : null;
    }

    public IEnumerable<BaseNode> GetChildren(DirectoryNode node)
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

    private void UpdateChildren(DirectoryNode node)
    {
        var nodes = _accessor.GetFileNodes(node.FullName);
        _lock.EnterWriteLock();
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

    public void CreateFileChild(DirectoryNode directory, string fileName)
    {
        _accessor.CreateEmptyFile(fileName);
        _lock.EnterWriteLock();
        try
        {
            var node = new FileNode { Name = Path.GetFileName(fileName) };
            directory.Children.Add(Path.GetFileName(fileName), node);
            node.Parent = directory;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void CreateDirectoryChild(DirectoryNode directory, string directoryName)
    {
        _accessor.CreateDirectory(directoryName);
        _lock.EnterWriteLock();
        try
        {
            var node = new DirectoryNode { Name = Path.GetFileName(directoryName) };
            directory.Children.Add(Path.GetFileName(directoryName), node);
            node.Parent = directory;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void SetLength(FileNode node, long length)
    {
        _accessor.SetLength(node.FullName, length);
        _lock.EnterWriteLock();
        try
        {
            node.Length = length;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Move(BaseNode oldNode, string newName, DirectoryNode destination)
    {
        _accessor.Rename(oldNode.FullName, newName);
        _lock.EnterWriteLock();
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

    public void Delete(BaseNode node)
    {
        _accessor.Delete(node.FullName);
        _lock.EnterWriteLock();
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

    public void SetFileTime(BaseNode node, DateTime? cTime, DateTime? laTime, DateTime? lwTime)
    {
        var ucTime = cTime == null ? 0 : ((DateTimeOffset)cTime).ToUnixTimeMilliseconds();
        var ulaTime = laTime == null ? 0 : ((DateTimeOffset)laTime).ToUnixTimeMilliseconds();
        var ulwTime = lwTime == null ? 0 : ((DateTimeOffset)lwTime).ToUnixTimeMilliseconds();
        _accessor.SetFileTime(node.FullName, ucTime, ulaTime, ulwTime);
        _lock.EnterWriteLock();
        try
        {
            node.CreationTime = cTime;
            node.LastAccessTime = laTime;
            node.LastWriteTime = lwTime;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Write(FileNode node, Memory<byte> data, long offset)
    {
        _lock.EnterWriteLock();
        try
        {
            if (node.Length < offset + data.Length)
                node.Length = offset + data.Length;
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        _accessor.WriteFileBuffer(data, node.FullName, offset);
    }

    //we can cache this
    public int ReadFile(FileNode node, byte[] buffer, long offset, int bytesToRead, long fileSize)
    {
        _lock.EnterReadLock();
        try
        {
            var data = _accessor.ReceiveFileBuffer(buffer, node.FullName, offset, bytesToRead, fileSize);
            return data;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}