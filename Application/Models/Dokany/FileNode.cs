using Application.Interfaces;
using Domain;

namespace Application.Models.Dokany;

public class FileNode : BaseNode
{
    public FileNode(KuromeInformation fileInformation) : base(fileInformation)
    {
    }

    public void SetLength(long length, IDeviceAccessor deviceAccessor)
    {
        KuromeInformation.Length = length;
        deviceAccessor.SetLength(Fullname, length);
    }

    public void Write(byte[] data, long offset, IDeviceAccessor deviceAccessor)
    {
        KuromeInformation.Length = offset + data.Length;
        deviceAccessor.WriteFileBuffer(data, Fullname, offset);
    }

    //we can cache this
    public int ReadFile(byte[] buffer, long offset, int bytesToRead, long fileSize, IDeviceAccessor deviceAccessor)
    {
        var data = deviceAccessor.ReceiveFileBuffer(buffer, Fullname, offset, bytesToRead, fileSize);
        return data;
    }
}