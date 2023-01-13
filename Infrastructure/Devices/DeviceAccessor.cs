using System.Buffers.Binary;
using Application.flatbuffers;
using Application.Interfaces;
using Domain;
using Domain.FileSystem;
using FlatSharp;
using Kurome.Fbs;
using MapsterMapper;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Devices;

public class DeviceAccessor : IDeviceAccessor
{
    private readonly Device _device;
    private readonly FlatBufferHelper _flatBufferHelper;
    private readonly ILink _link;
    private readonly object _lock = new();
    private readonly ILogger _logger;
    private readonly IMapper _mapper;

    // TODO: MessagePipe experiments
    // private readonly IAsyncPublisher<long, Packet> _networkQueryPublisher;
    // private readonly IAsyncSubscriber<long, Packet> _networkQuerySubscriber;

    public DeviceAccessor(ILink link,
        ILogger<DeviceAccessor> logger,
        FlatBufferHelper flatBufferHelper, IMapper mapper, Device device)
    {
        _link = link;
        _logger = logger;
        _flatBufferHelper = flatBufferHelper;
        _mapper = mapper;
        _device = device;
    }

    public void Dispose()
    {
        _link.Dispose();
        GC.SuppressFinalize(this);
    }

    public void SetLength(string fileName, long length)
    {
        SendQuery(_flatBufferHelper.SetLengthCommand(SanitizeName(fileName), length));
    }

    public void SetFileTime(string fileName, long cTime, long laTime, long lwTime)
    {
        SendQuery(_flatBufferHelper.SetFileTimeCommand(SanitizeName(fileName), cTime, laTime, lwTime));
    }

    public void Rename(string fileName, string newFileName)
    {
        SendQuery(_flatBufferHelper.RenameCommand(SanitizeName(fileName), SanitizeName(newFileName)));
    }

    public IEnumerable<BaseNode> GetFileNodes(string fileName)
    {
        var response = SendQuery(_flatBufferHelper.GetDirectoryQuery(SanitizeName(fileName)));
        _flatBufferHelper.TryGetFileResponseNode(response, out var result);
        return result!.Children!.Select(x => x.Attributes!.Type switch
        {
            FileType.File => _mapper.Map<FileNode>(x),
            _ => (BaseNode)_mapper.Map<DirectoryNode>(x)
        });
    }

    public BaseNode GetRootNode()
    {
        try
        {
            var response = SendQuery(_flatBufferHelper.GetDirectoryQuery("/"));
            _flatBufferHelper.TryGetFileResponseNode(response, out var file);
            return _mapper.Map<DirectoryNode>(file!);
        }
        catch (Exception e)
        {
            _logger.LogCritical(e.ToString());
        }

        throw new Exception("Failed to get root node");
    }

    public void GetSpace(out long total, out long free)
    {
        var response = SendQuery(_flatBufferHelper.DeviceInfoSpaceQuery());
        _flatBufferHelper.TryGetDeviceInfo(response, out var deviceInfo);
        total = deviceInfo!.Space!.TotalBytes;
        free = deviceInfo.Space.FreeBytes;
    }

    public void CreateEmptyFile(string fileName)
    {
        SendQuery(_flatBufferHelper.CreateFileCommand(SanitizeName(fileName)));
    }

    public void CreateDirectory(string directoryName)
    {
        SendQuery(_flatBufferHelper.CreateDirectoryCommand(SanitizeName(directoryName)));
    }

    public int ReceiveFileBuffer(byte[] buffer, string fileName, long offset, int bytesToRead, long fileSize)
    {
        var response = SendQuery(_flatBufferHelper.ReadFileQuery(SanitizeName(fileName), offset, bytesToRead));
        _flatBufferHelper.TryGetFileResponseRaw(response, out var raw);
        raw.Data?.CopyTo(buffer);
        return raw.Data == null ? 0 : bytesToRead;
    }

    public void WriteFileBuffer(Memory<byte> buffer, string fileName, long offset)
    {
        SendQuery(_flatBufferHelper.WriteFileCommand(buffer, SanitizeName(fileName), offset));
    }

    public void Delete(string fileName)
    {
        SendQuery(_flatBufferHelper.DeleteCommand(SanitizeName(fileName)));
    }

    public Device GetDevice()
    {
        return _device;
    }

    private void SendPacket(Component component, long id = 0)
    {
        var packet = new Packet { Component = component, Id = id };
        var size = Packet.Serializer.GetMaxSize(packet);
        Span<byte> buffer = new byte[4 + size];
        var length = Packet.Serializer.Write(buffer[4..], packet);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[..4], length);
        _link.Send(buffer, length + 4);
    }

    private Packet ReadPacket()
    {
        var sizeBuffer = new byte[4];
        _link.Receive(sizeBuffer, 4);
        var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
        var buffer = new byte[size];
        _link.Receive(buffer, size);
        return Packet.Serializer.Parse(buffer);
    }

    private Packet SendQuery(Component component)
    {
        lock (_lock)
        {
            SendPacket(component);
            return ReadPacket();
        }
    }

    private string SanitizeName(string fileName)
    {
        return fileName.Replace("\\", "/");
    }
}