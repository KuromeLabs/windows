using DokanNet;

namespace Kurome;

public class FileNode : BaseNode
{
    public FileNode(FileInformation fileInformation) : base(fileInformation)
    {
    }
    
    public void SetLength(long length, Device device)
    {
        var fileInformation = FileInformation;
        fileInformation.Length = length;
        FileInformation = fileInformation;
        device.SetLength(Fullname, length);
    }
    
    public void Write(byte[] data, long offset, Device device)
    {
        var fileInformation = FileInformation;
        fileInformation.Length = offset + data.Length;
        FileInformation = fileInformation;
        device.WriteFileBuffer(data, Fullname, offset);
    }

    //we can cache this
    public int ReadFile(byte[] buffer, long offset, int bytesToRead, long fileSize, Device device)
    {
        var data = device.ReceiveFileBuffer(buffer, Fullname, offset, bytesToRead, fileSize);
        return data;
    }
}