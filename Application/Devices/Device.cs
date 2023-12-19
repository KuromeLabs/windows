using System.Buffers.Binary;
using Application.Filesystem;
using Application.flatbuffers;
using Application.Network;
using DokanNet;
using DokanNet.Logging;
using FlatSharp;
using Kurome.Fbs;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Application;

public class Device : IDisposable
{
    public Device()
    {
    }

    public Device(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    private Link? _link { get; set; }
    private DokanInstance? fsHost;
    private readonly Dokan _dokan = new(null);
    private ILogger _logger = Log.ForContext(typeof(Device));
    private readonly object _lock = new();
    private long _totalSpace = -1;
    private long _freeSpace = -1;

    public void Connect(Link link)
    {
        _link = link;
    }

    public void Mount()
    {
        
        var fs = new KuromeFs(false, this);
        var builder = new DokanInstanceBuilder(_dokan)
            .ConfigureLogger(() => new NullLogger())
            .ConfigureOptions(options =>
            {
                options.Options = DokanOptions.FixedDrive;
                options.MountPoint = "E:" + "\\";
                options.SingleThread = false;
            });

        
        fsHost = builder.Build(fs);
    }

    public void Dispose()
    {
        _logger.Information($"Disposing device {Id}");
        _link?.Dispose();
        fsHost?.Unmount();
        _link = null;
    }

    public void SetLength(string fileName, long length)
    {
        SendQuery(FlatBufferHelper.SetLengthCommand(SanitizeName(fileName), length));
    }

    public void SetFileTime(string fileName, long cTime, long laTime, long lwTime)
    {
        SendQuery(FlatBufferHelper.SetFileTimeCommand(SanitizeName(fileName), cTime, laTime, lwTime));
    }

    public void Rename(string fileName, string newFileName)
    {
        SendQuery(FlatBufferHelper.RenameCommand(SanitizeName(fileName), SanitizeName(newFileName)));
    }

    public IEnumerable<CacheNode> GetFileNodes(string fileName)
    {
        var response = SendQuery(FlatBufferHelper.GetDirectoryQuery(SanitizeName(fileName)));
        FlatBufferHelper.TryGetFileResponseNode(response, out var result);

        return result!.Children!.Select(x => new CacheNode
        {
            Name = x.Attributes.Name,
            Length = x.Attributes.Length,
            CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(x.Attributes.CreationTime).LocalDateTime,
            LastAccessTime = DateTimeOffset.FromUnixTimeMilliseconds(x.Attributes!.LastAccessTime).LocalDateTime,
            LastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(x.Attributes!.LastWriteTime).LocalDateTime,
            FileAttributes = x.Attributes.ExtraAttributes
        });
    }

    public CacheNode GetRootNode()
    {
        try
        {
            var response = SendQuery(FlatBufferHelper.GetDirectoryQuery("/"));
            FlatBufferHelper.TryGetFileResponseNode(response, out var file);
            return new CacheNode
            {
                Name = "\\",
                Length = file.Attributes.Length,
                CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(file.Attributes.CreationTime).LocalDateTime,
                LastAccessTime = DateTimeOffset.FromUnixTimeMilliseconds(file.Attributes!.LastAccessTime).LocalDateTime,
                LastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(file.Attributes!.LastWriteTime).LocalDateTime,
                FileAttributes = file.Attributes.ExtraAttributes
            };
        }
        catch (Exception e)
        {
            _logger.Fatal(e.ToString());
        }

        throw new Exception("Failed to get root node");
    }

    public void GetSpace(out long total, out long free)
    {
        var response = SendQuery(FlatBufferHelper.DeviceInfoSpaceQuery());
        FlatBufferHelper.TryGetDeviceInfo(response, out var deviceInfo);
        total = deviceInfo!.Space!.TotalBytes;
        free = deviceInfo.Space.FreeBytes;
    }

    public void CreateEmptyFile(string fileName)
    {
        SendQuery(FlatBufferHelper.CreateFileCommand(SanitizeName(fileName)));
    }

    public void CreateDirectory(string directoryName)
    {
        SendQuery(FlatBufferHelper.CreateDirectoryCommand(SanitizeName(directoryName)));
    }

    public int ReceiveFileBuffer(byte[] buffer, string fileName, long offset, int bytesToRead, long fileSize)
    {
        var response = SendQuery(FlatBufferHelper.ReadFileQuery(SanitizeName(fileName), offset, bytesToRead));
        FlatBufferHelper.TryGetFileResponseRaw(response, out var raw);
        raw.Data?.CopyTo(buffer);
        return raw.Data == null ? 0 : bytesToRead;
    }
    
    public unsafe int ReceiveFileBufferUnsafe(IntPtr buffer, string fileName, long offset, int bytesToRead, long fileSize)
    {
        var response = SendQuery(FlatBufferHelper.ReadFileQuery(SanitizeName(fileName), offset, bytesToRead));
        FlatBufferHelper.TryGetFileResponseRaw(response, out var raw);
        var memory = raw.Data.Value;
        var bufferSpan = new Span<byte>(buffer.ToPointer(), bytesToRead);
        memory.Span.CopyTo(bufferSpan);
        return raw.Data == null ? 0 : bytesToRead;
    }

    public void WriteFileBuffer(Memory<byte> buffer, string fileName, long offset)
    {
        SendQuery(FlatBufferHelper.WriteFileCommand(buffer, SanitizeName(fileName), offset));
    }

    public void Delete(string fileName)
    {
        SendQuery(FlatBufferHelper.DeleteCommand(SanitizeName(fileName)));
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