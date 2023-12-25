using System.Buffers.Binary;
using DokanNet;
using DokanNet.Logging;
using FlatSharp;
using Kurome.Core.Filesystem;
using Kurome.Core.flatbuffers;
using Kurome.Core.Network;
using Kurome.Fbs;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Kurome.Core;

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
    private DokanInstance? _fsHost;
    private Dokan? _dokan;
    private ILogger _logger = Log.ForContext(typeof(Device));
    private readonly object _lock = new();
    private long _totalSpace = -1;
    private long _freeSpace = -1;
    private long _lastSpaceUpdate = 0;

    public void Connect(Link link)
    {
        _link = link;
    }

    public void Mount()
    {
        _logger.Information($"Mounting device {Id}");
        _dokan = new Dokan(null);
        var fs = new KuromeFs(false, this);
        var builder = new DokanInstanceBuilder(_dokan)
            .ConfigureLogger(() => new NullLogger())
            .ConfigureOptions(options =>
            {
                options.Options = DokanOptions.FixedDrive;
                options.MountPoint = "E:" + "\\";
                options.SingleThread = false;
            });

        
        _fsHost = builder.Build(fs);
    }

    public void Dispose()
    {
        _logger.Information($"Unmounting device {Id}");
        _dokan?.Unmount('E');
        _fsHost?.WaitForFileSystemClosed(5000);
        _logger.Information($"Disposing device {Id}");
        _link?.Dispose();
        _link = null;
        _fsHost = null;
        _dokan = null;
        GC.SuppressFinalize(this);
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

    public IEnumerable<CacheNode>? GetFileNodes(string fileName)
    {
        var response = SendQuery(FlatBufferHelper.GetDirectoryQuery(SanitizeName(fileName)));
        if (response == null || !FlatBufferHelper.TryGetFileResponseNode(response, out var result)) return null;
        return result!.Children!.Select(x => new CacheNode
        {
            Name = x.Attributes!.Name!,
            Length = x.Attributes.Length,
            CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(x.Attributes.CreationTime).LocalDateTime,
            LastAccessTime = DateTimeOffset.FromUnixTimeMilliseconds(x.Attributes!.LastAccessTime).LocalDateTime,
            LastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(x.Attributes!.LastWriteTime).LocalDateTime,
            FileAttributes = x.Attributes.ExtraAttributes
        });
    }

    public CacheNode? GetRootNode()
    {
        try
        {
            var response = SendQuery(FlatBufferHelper.GetDirectoryQuery("/"));
            if (response == null || !FlatBufferHelper.TryGetFileResponseNode(response, out var file)) return null;
            return new CacheNode
            {
                Name = "\\",
                Length = file!.Attributes!.Length,
                CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(file.Attributes.CreationTime).LocalDateTime,
                LastAccessTime = DateTimeOffset.FromUnixTimeMilliseconds(file.Attributes!.LastAccessTime).LocalDateTime,
                LastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(file.Attributes!.LastWriteTime).LocalDateTime,
                FileAttributes = file.Attributes.ExtraAttributes
            };
        }
        catch (Exception e)
        {
            _logger.Error(e.ToString());
        }

        return null;
    }

    public bool GetSpace(out long total, out long free)
    {
        if (DateTimeOffset.Now.ToUnixTimeMilliseconds() - _lastSpaceUpdate > 15000)
        {
            _totalSpace = -1;
            _freeSpace = -1;
        }
        if (_totalSpace == -1 || _freeSpace == -1)
        {
            var response = SendQuery(FlatBufferHelper.DeviceInfoSpaceQuery());
            _lastSpaceUpdate = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (response == null || !FlatBufferHelper.TryGetDeviceInfo(response, out var deviceInfo))
            {
                total = 0;
                free = 0;
                return false;
            }
            _totalSpace = deviceInfo!.Space!.TotalBytes;
            _freeSpace = deviceInfo.Space.FreeBytes;
        }

        total = _totalSpace;
        free = _freeSpace;
        return true;
    }

    public void CreateEmptyFile(string fileName)
    {
        SendQuery(FlatBufferHelper.CreateFileCommand(SanitizeName(fileName)));
    }

    public void CreateDirectory(string directoryName)
    {
        SendQuery(FlatBufferHelper.CreateDirectoryCommand(SanitizeName(directoryName)));
    }

    public int ReceiveFileBuffer(byte[] buffer, string fileName, long offset, int bytesToRead)
    {
        var response = SendQuery(FlatBufferHelper.ReadFileQuery(SanitizeName(fileName), offset, bytesToRead));
        if (response == null || !FlatBufferHelper.TryGetFileResponseRaw(response, out var raw)) return 0;
        raw!.Data?.CopyTo(buffer);
        return raw.Data == null ? 0 : bytesToRead;
    }
    
    public unsafe int ReceiveFileBufferUnsafe(IntPtr buffer, string fileName, long offset, int bytesToRead)
    {
        var response = SendQuery(FlatBufferHelper.ReadFileQuery(SanitizeName(fileName), offset, bytesToRead));
        if (response == null || !FlatBufferHelper.TryGetFileResponseRaw(response, out var raw)) return 0;
        var memory = raw!.Data!.Value;
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
        _link?.Send(buffer, length + 4);
    }

    private Packet? ReadPacket()
    {
        var sizeBuffer = new byte[4];
        if (_link?.Receive(sizeBuffer, 4) <= 0) return null;
        var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
        var buffer = new byte[size];
        if (_link?.Receive(buffer, size) <= 0) return null;
        return Packet.Serializer.Parse(buffer);
    }

    private Packet? SendQuery(Component component)
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