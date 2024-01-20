namespace Kurome.Core.Filesystem;

public interface IFileSystemHost
{
    public void Mount(string mountPoint, Device device);
    public void Unmount(string mountPoint);
}