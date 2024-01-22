using System.Buffers;
using System.Buffers.Binary;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using FlatSharp;
using Kurome.Core.Filesystem;
using Kurome.Core.Network;
using Kurome.Fbs;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Kurome.Core;

public class Device : IDisposable
{

    public Device(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Device()
    {
    }

    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    private Link? _link { get; set; }
    private readonly ILogger _logger = Log.ForContext(typeof(Device));
    private long _totalSpace = -1;
    private long _freeSpace = -1;
    private long _lastSpaceUpdate = 0;
    private bool Disposed { get; set; } = false;
    private long _packetId = 0;

    public void Connect(Link link)
    {
        _link = link;
    }
    

    protected virtual void Dispose(bool disposing)
    {
        if (Disposed) return;
        _logger.Information($"Disposing device {Id}");
        if (disposing)
        {
            _link?.Dispose();
        }

        _link = null;
        Disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public void SetFileAttributes(string fileName, long cTime, long laTime, long lwTime, uint attributes, long length)
    {
        SendPacket(new Component(new SetFileInfoCommand
        {
            Path = SanitizeName(fileName),
            CreationTime = cTime,
            LastAccessTime = laTime,
            LastWriteTime = lwTime,
            ExtraAttributes = attributes,
            Length = length
        }));
    }

    public void Rename(string fileName, string newFileName)
    {
        SendPacket(new Component(new RenameFileCommand
            { NewPath = SanitizeName(newFileName), OldPath = SanitizeName(fileName) }));
    }

    public Dictionary<string, CacheNode>? GetChildrenNodes(CacheNode parent)
    {
        Link.Buffer? data = null;
        var id = Interlocked.Increment(ref _packetId);
        SendPacket(new Component(new GetDirectoryQuery { Path = SanitizeName(parent.FullName) }), id);
        data = _link?.DataReceived
            .Where(x => x == null || x.Id == id)
            .Take(1)
            .Wait();

        if (data == null) return null;
        
        var response = Packet.Serializer.Parse(data.Data);
        if (response.Component?.Kind != Component.ItemKind.GetDirectoryResponse || response.Component.Value.GetDirectoryResponse.Node == null) {
            data.Free();
            return null;
        }
        var result = response.Component.Value.GetDirectoryResponse.Node!;
        var list = result.Children!.ToDictionary(x => x.Name!, x =>
            new CacheNode
            {
                Name = x.Name!,
                Length = x.Length,
                CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(x.CreationTime).LocalDateTime,
                LastAccessTime = DateTimeOffset.FromUnixTimeMilliseconds(x.LastAccessTime).LocalDateTime,
                LastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(x.LastWriteTime).LocalDateTime,
                FileAttributes = x.ExtraAttributes,
                Parent = parent
            });
        data.Free();
        return list;
    }

    public CacheNode? GetRootNode()
    {
        Link.Buffer? data = null;
        try
        {
            var id = Interlocked.Increment(ref _packetId);
            SendPacket(new Component(new GetDirectoryQuery { Path = "/" }), id);
            data = _link?.DataReceived
                .Where(x => x == null || x.Id == id)
                .Take(1)
                .Wait();

            if (data == null) return null;
            var response = Packet.Serializer.Parse(data.Data);
            if (response.Component?.Kind != Component.ItemKind.GetDirectoryResponse) {
                data.Free();
                return null;
            }
            var file = response.Component.Value.GetDirectoryResponse.Node;
            var root = new CacheNode
            {
                Name = "\\",
                Length = file!.Length,
                CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(file.CreationTime).LocalDateTime,
                LastAccessTime = DateTimeOffset.FromUnixTimeMilliseconds(file.LastAccessTime).LocalDateTime,
                LastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(file.LastWriteTime).LocalDateTime,
                FileAttributes = file.ExtraAttributes
            };
            data.Free();
            return root;
        }
        catch (Exception e)
        {
            _logger.Error(e.ToString());
        }
        data?.Free();
        return null;
    }

    public bool GetSpace(out long total, out long free)
    {
        total = 0;
        free = 0;
        if (DateTimeOffset.Now.ToUnixTimeMilliseconds() - _lastSpaceUpdate > 15000)
        {
            _totalSpace = -1;
            _freeSpace = -1;
        }

        if (_totalSpace == -1 || _freeSpace == -1)
        {
            Link.Buffer? data = null;
            var id = Interlocked.Increment(ref _packetId);
            SendPacket(new Component(new DeviceQuery()), id);
            data = _link?.DataReceived
                .Where(x => x == null || x.Id == id)
                .Take(1)
                .Wait();
            if (data == null) return false;
            
            var response = Packet.Serializer.Parse(data.Data);
            if (response.Component?.Kind != Component.ItemKind.DeviceQueryResponse) {
                data.Free();
                return false;
            }
            var deviceInfo = response.Component.Value.DeviceQueryResponse;
            _totalSpace = deviceInfo.TotalBytes;
            _freeSpace = deviceInfo.FreeBytes;
            _lastSpaceUpdate = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            data.Free();
        }

        total = _totalSpace;
        free = _freeSpace;
        return true;
    }

    public void CreateEmptyFile(string fileName, uint extraAttributes = (uint)FileAttributes.Archive)
    {
        SendPacket(new Component(new CreateFileCommand
            { Path = SanitizeName(fileName), ExtraAttributes = extraAttributes }));
    }

    public void CreateDirectory(string directoryName)
    {
        SendPacket(new Component(new CreateDirectoryCommand { Path = SanitizeName(directoryName) }));
    }

    public unsafe int ReceiveFileBufferUnsafe(IntPtr buffer, string fileName, long offset, int bytesToRead)
    {
        var readComponent = new Component(new ReadFileQuery
            { Path = SanitizeName(fileName), Offset = offset, Length = bytesToRead });
        Link.Buffer? data = null;
        var id = Interlocked.Increment(ref _packetId);
        SendPacket(readComponent, id);
        data = _link?.DataReceived
            .Where(x => x == null || x.Id == id)
            .Take(1)
            .Wait();
        
        if (data == null) return 0;
        var response = Packet.Serializer.Parse(data.Data);
        if (response.Component?.Kind != Component.ItemKind.ReadFileResponse ||
            response.Component?.ReadFileResponse.Data == null)
        {
            data.Free();
            return 0;
        }
        var memory = response.Component.Value.ReadFileResponse.Data!.Value;
        var bufferSpan = new Span<byte>(buffer.ToPointer(), bytesToRead);
        memory.Span.CopyTo(bufferSpan);
        data.Free();
        return bytesToRead;
    }

    public void WriteFileBufferUnsafe(IntPtr buffer, string fileName, long offset, int bytesToWrite)
    {
        var bytes = ArrayPool<byte>.Shared.Rent(bytesToWrite);
        Marshal.Copy(buffer, bytes, 0, bytesToWrite);
        SendPacket(new Component(new WriteFileCommand
            { Path = SanitizeName(fileName), Offset = offset, Data = bytes.AsMemory(0, bytesToWrite) }));
        ArrayPool<byte>.Shared.Return(bytes);
    }

    public void Delete(string fileName)
    {
        SendPacket(new Component(new DeleteFileCommand { Path = SanitizeName(fileName) }));
    }

    private void SendPacket(Component component, long id = 0)
    {
        var packet = new Packet { Component = component, Id = id };
        var size = Packet.Serializer.GetMaxSize(packet);
        var buffer = ArrayPool<byte>.Shared.Rent(4 + size);
        var length = Packet.Serializer.Write(buffer.AsSpan()[4..], packet);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan()[..4], length);
        _link?.Send(buffer, length + 4);
        ArrayPool<byte>.Shared.Return(buffer);
    }
    

    private string SanitizeName(string fileName)
    {
        return fileName.Replace("\\", "/");
    }
}