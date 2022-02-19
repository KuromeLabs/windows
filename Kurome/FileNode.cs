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
        device.WriteFileBuffer(data,Fullname,offset);
    }
    
}