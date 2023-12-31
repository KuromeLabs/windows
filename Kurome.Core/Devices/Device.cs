using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
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
    private IFileSystemHost? _fileSystemHost;

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
    private readonly object _lock = new();
    private long _totalSpace = -1;
    private long _freeSpace = -1;
    private long _lastSpaceUpdate = 0;
    private bool Disposed { get; set; } = false;

    public void Connect(Link link)
    {
        _link = link;
    }

    public void Mount(IFileSystemHost fileSystemHost)
    {
        _fileSystemHost = fileSystemHost;
        _logger.Information($"Mounting device {Id}");
        _fileSystemHost.Mount("E", this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (Disposed) return;
        _logger.Information($"Disposing device {Id}");
        if (disposing)
        {
            _logger.Information($"Unmounting device {Id}");
            _fileSystemHost?.Unmount("E");
            _link?.Dispose();
            _fileSystemHost?.DisposeInstance("E");
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
        lock (_lock)
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
        lock (_lock)
            SendPacket(new Component(new RenameFileCommand
                { NewPath = SanitizeName(newFileName), OldPath = SanitizeName(fileName) }));
    }

    public Dictionary<string, CacheNode>? GetChildrenNodes(CacheNode parent)
    {
        byte[]? data;
        lock (_lock)
        {
            SendPacket(new Component(new GetDirectoryQuery { Path = SanitizeName(parent.FullName) }));
            data = ReadPacketRentedBuffer();
            if (data == null) return null;
        }
        var response = Packet.Serializer.Parse(data);
        if (response.Component?.Kind != Component.ItemKind.GetDirectoryResponse) return null;
        var result = response.Component.Value.GetDirectoryResponse.Node;
        if (result == null) return null;
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
        ArrayPool<byte>.Shared.Return(data);
        return list;
    }

    public CacheNode? GetRootNode()
    {
        
        try
        {
            byte[]? data;
            lock (_lock)
            {
                SendPacket(new Component(new GetDirectoryQuery { Path = "/" }));
                data = ReadPacketRentedBuffer();
                if (data == null) return null;
            }
            var response = Packet.Serializer.Parse(data);
            if (response.Component?.Kind != Component.ItemKind.GetDirectoryResponse) return null;
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
            ArrayPool<byte>.Shared.Return(data);
            return root;
        }
        catch (Exception e)
        {
            _logger.Error(e.ToString());
        }
        
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
            byte[]? data;
            lock (_lock)
            {
                SendPacket(new Component(new DeviceQuery()));
                data = ReadPacketRentedBuffer();
                if (data == null) return false;
            }
            var response = Packet.Serializer.Parse(data);
            if (response.Component?.Kind != Component.ItemKind.DeviceQueryResponse) return false;
            var deviceInfo = response.Component.Value.DeviceQueryResponse;
            _totalSpace = deviceInfo.TotalBytes;
            _freeSpace = deviceInfo.FreeBytes;
            _lastSpaceUpdate = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            ArrayPool<byte>.Shared.Return(data);
        }

        total = _totalSpace;
        free = _freeSpace;
        return true;
    }

    public void CreateEmptyFile(string fileName, uint extraAttributes = (uint)FileAttributes.Archive)
    {
        lock (_lock)
            SendPacket(new Component(new CreateFileCommand
                { Path = SanitizeName(fileName), ExtraAttributes = extraAttributes }));
    }

    public void CreateDirectory(string directoryName)
    {
        lock (_lock)
            SendPacket(new Component(new CreateDirectoryCommand { Path = SanitizeName(directoryName) }));
    }

    public unsafe int ReceiveFileBufferUnsafe(IntPtr buffer, string fileName, long offset, int bytesToRead)
    {
        var readComponent = new Component(new ReadFileQuery
            { Path = SanitizeName(fileName), Offset = offset, Length = bytesToRead });
        byte[]? data = null;
        lock (_lock)
        {
            SendPacket(readComponent);
            data = ReadPacketRentedBuffer();
        }
        if (data == null) return 0;
        var response = Packet.Serializer.Parse(data);
        if (response.Component?.Kind != Component.ItemKind.ReadFileResponse ||
            response.Component?.ReadFileResponse.Data == null) return 0;
        var memory = response.Component.Value.ReadFileResponse.Data!.Value;
        var bufferSpan = new Span<byte>(buffer.ToPointer(), bytesToRead);
        memory.Span.CopyTo(bufferSpan);
        ArrayPool<byte>.Shared.Return(data);
        return bytesToRead;
    }

    public void WriteFileBufferUnsafe(IntPtr buffer, string fileName, long offset, int bytesToWrite)
    {
        var bytes = ArrayPool<byte>.Shared.Rent(bytesToWrite);
        Marshal.Copy(buffer, bytes, 0, bytesToWrite);
        lock (_lock)
            SendPacket(new Component(new WriteFileCommand
                { Path = SanitizeName(fileName), Offset = offset, Data = bytes.AsMemory(0, bytesToWrite) }));
        ArrayPool<byte>.Shared.Return(bytes);
    }

    public void Delete(string fileName)
    {
        lock (_lock)
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

    private byte[]? ReadPacketRentedBuffer()
    {
        var sizeBuffer = new byte[4];
        if (_link?.Receive(sizeBuffer, 4) <= 0) return null;
        var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
        var buffer = ArrayPool<byte>.Shared.Rent(size);
        if (_link?.Receive(buffer, size) <= 0)
        {
            ArrayPool<byte>.Shared.Return(buffer);
            return null;
        }
        return buffer;
    }

    private string SanitizeName(string fileName)
    {
        return fileName.Replace("\\", "/");
    }
}