using Domain;
using Domain.FileSystem;

namespace Application.Interfaces;

public interface IDeviceAccessor : IDisposable
{
    public void SetLength(string fileName, long length);
    public void SetFileTime(string fileName, long cTime, long laTime, long lwTime);
    public void Rename(string fileName, string newFileName);
    public IEnumerable<BaseNode> GetFileNodes(string fileName);
    public BaseNode GetRootNode();
    public void GetSpace(out long total, out long free);
    public void CreateEmptyFile(string fileName);
    public void CreateDirectory(string directoryName);
    public int ReceiveFileBuffer(byte[] buffer, string fileName, long offset, int bytesToRead, long fileSize);
    public void WriteFileBuffer(Memory<byte> buffer, string fileName, long offset);
    public void Delete(string fileName);
    public Device GetDevice();
}