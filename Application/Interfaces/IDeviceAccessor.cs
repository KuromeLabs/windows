using Domain;

namespace Application.Interfaces;

public interface IDeviceAccessor : IDisposable
{
    public void Start(CancellationToken cancellationToken);
    public void SetLength(string fileName, long length);
    public Device Get();
    public void SetFileTime(string fileName, long cTime, long laTime, long lwTime);
    public void Rename(string fileName, string newFileName);
    public IEnumerable<KuromeInformation> GetFileNodes(string fileName);
    public KuromeInformation GetRootNode();
    public KuromeInformation GetFileNode(string fileName);
    public void GetSpace(out long total, out long free);
    public void CreateEmptyFile(string fileName);
    public void CreateDirectory(string directoryName);
    public int ReceiveFileBuffer(byte[] buffer, string fileName, long offset, int bytesToRead, long fileSize);
    public void WriteFileBuffer(byte[] buffer, string fileName, long offset);
    public void Delete(string fileName);
}