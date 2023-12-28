namespace Kurome.Core.Filesystem;

public interface IFileSystemHost
{
    public void Mount(string letter, Device device);
    public void Unmount(string letter);
}