using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using Application.flatbuffers;
using Application.Interfaces;
using DokanNet;
using DokanNet.Logging;
using Domain;
using FlatSharp;
using Infrastructure.Dokany;
using Kurome.Fbs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Infrastructure.Devices;

public class DeviceAccessor : IDeviceAccessor
{
    private class NetworkQuery
    {
        public Packet? Packet;
        public byte[]? Buffer;
        public readonly ManualResetEventSlim ResponseEvent;

        public NetworkQuery(ManualResetEventSlim responseEvent)
        {
            ResponseEvent = responseEvent;
            Packet = null;
            Buffer = null;
        }

        public void Dispose()
        {
            ResponseEvent.Set();
            ResponseEvent.Dispose();
            if (Buffer != null) ArrayPool<byte>.Shared.Return(Buffer);
        }
    }

    private readonly ILink _link;
    private readonly IDeviceAccessorRepository _deviceAccessorRepository;
    private readonly Device _device;
    private readonly IIdentityProvider _identityProvider;
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly FlatBufferHelper _flatBufferHelper;
    private readonly ConcurrentDictionary<long, NetworkQuery> _contexts = new();
    private Dokan? _dokan;
    private DokanInstance? _dokanInstance;
    private string? _mountLetter;

    public DeviceAccessor(ILink link, IDeviceAccessorRepository deviceAccessorRepository,
        Device device, IIdentityProvider identityProvider, ILogger<DeviceAccessor> logger,
        IServiceProvider serviceProvider, FlatBufferHelper flatBufferHelper)
    {
        _link = link;
        _deviceAccessorRepository = deviceAccessorRepository;
        _device = device;
        _identityProvider = identityProvider;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _flatBufferHelper = flatBufferHelper;
    }

    public void Dispose()
    {
        _link.Dispose();
        foreach (var query in _contexts)
            query.Value.Dispose();
        _logger.LogInformation("Disposed DeviceAccessor for {DeviceName} - {DeviceId}", _device.Name,
            _device.Id.ToString());
        Unmount();
        _deviceAccessorRepository.Remove(_device.Id.ToString());
        GC.SuppressFinalize(this);
    }

    public async void Start(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var sizeBuffer = new byte[4];
            var bytesRead = await _link.ReceiveAsync(sizeBuffer, 4, cancellationToken);
            if (bytesRead == 0) break;
            var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
            var buffer = ArrayPool<byte>.Shared.Rent(size);
            bytesRead = await _link.ReceiveAsync(buffer, size, cancellationToken);
            if (bytesRead == 0) break;
            var packet = Packet.Serializer.Parse(buffer);
            if (_contexts.ContainsKey(packet.Id))
            {
                _contexts[packet.Id].Packet = packet;
                _contexts[packet.Id].Buffer = buffer;
                _contexts[packet.Id].ResponseEvent.Set();
                _logger.LogTrace("Received {BytesLength} bytes from: {DeviceName} - {DeviceId}",
                    bytesRead, _device.Name, _device.Id.ToString());
                _contexts.TryRemove(packet.Id, out _);
            }
            else ArrayPool<byte>.Shared.Return(buffer);
        }

        Dispose();
    }

    public void SetLength(string fileName, long length)
    {
        SendPacket(_flatBufferHelper.SetLengthCommand(SanitizeName(fileName), length));
    }

    public Device Get()
    {
        return _device;
    }

    public void SetFileTime(string fileName, long cTime, long laTime, long lwTime)
    {
        SendPacket(_flatBufferHelper.SetFileTimeCommand(SanitizeName(fileName), cTime, laTime, lwTime));
    }

    public void Rename(string fileName, string newFileName)
    {
        SendPacket(_flatBufferHelper.RenameCommand(SanitizeName(fileName), SanitizeName(newFileName)));
    }

    public IEnumerable<KuromeInformation> GetFileNodes(string fileName)
    {
        var response = SendQuery(_flatBufferHelper.GetDirectoryQuery(SanitizeName(fileName)));
        var files = new List<KuromeInformation>();
        _flatBufferHelper.TryGetFileResponseNode(response.Packet!, out var result);
        foreach (var node in result!.Children!)
        {
            var attr = node.Attributes!;
            var file = new KuromeInformation
            {
                FileName = attr.Name!,
                IsDirectory = attr.Type == FileType.Directory,
                LastAccessTime = DateTimeOffset.FromUnixTimeMilliseconds(attr.LastAccessTime).LocalDateTime,
                LastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(attr.LastWriteTime).LocalDateTime,
                CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(attr.CreationTime).LocalDateTime,
                Length = attr.Length
            };
            files.Add(file);
        }

        response.Dispose();
        return files;
    }

    public KuromeInformation GetRootNode()
    {
        var response = SendQuery(_flatBufferHelper.GetDirectoryQuery("/"));
        _flatBufferHelper.TryGetFileResponseNode(response.Packet!, out var file);
        var attrs = file!.Attributes!;
        var fileInfo = new KuromeInformation
        {
            FileName = "",
            IsDirectory = attrs.Type == FileType.Directory,
            LastAccessTime = DateTimeOffset.FromUnixTimeMilliseconds(attrs.LastAccessTime).LocalDateTime,
            LastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(attrs.LastWriteTime).LocalDateTime,
            CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(attrs.CreationTime).LocalDateTime,
            Length = attrs.Length
        };
        return fileInfo;
    }

    public void GetSpace(out long total, out long free)
    {
        var response = SendQuery(_flatBufferHelper.DeviceInfoSpaceQuery());
        _flatBufferHelper.TryGetDeviceInfo(response.Packet!, out var deviceInfo);
        total = deviceInfo!.Space!.TotalBytes;
        free = deviceInfo.Space.FreeBytes;
        response.Dispose();
    }

    public void CreateEmptyFile(string fileName)
    {
        SendPacket(_flatBufferHelper.CreateFileCommand(SanitizeName(fileName)));
    }

    public void CreateDirectory(string directoryName)
    {
        SendPacket(_flatBufferHelper.CreateDirectoryCommand(SanitizeName(directoryName)));
    }

    public int ReceiveFileBuffer(byte[] buffer, string fileName, long offset, int bytesToRead, long fileSize)
    {
        var response = SendQuery(_flatBufferHelper.ReadFileQuery(SanitizeName(fileName), offset, bytesToRead));
        _flatBufferHelper.TryGetFileResponseRaw(response.Packet, out var raw);
        raw.Data?.CopyTo(buffer);
        response.Dispose();
        return raw.Data == null ? 0 : bytesToRead;
    }

    public void WriteFileBuffer(Memory<byte> buffer, string fileName, long offset)
    {
        SendPacket(_flatBufferHelper.WriteFileCommand(buffer, SanitizeName(fileName), offset));
    }

    public void Delete(string fileName)
    {
        SendPacket(_flatBufferHelper.DeleteCommand(SanitizeName(fileName)));
    }

    public void Mount()
    {
        var driveLetters = Enumerable.Range('C', 'Z' - 'C' + 1).Select(i => (char)i + ":")
            .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
        _mountLetter = driveLetters[0];
        var dokanLogger = new ConsoleLogger();
        _dokan = new Dokan(dokanLogger);
        var rfs = ActivatorUtilities.CreateInstance<KuromeOperations>(_serviceProvider, this, _mountLetter);
        var builder = new DokanInstanceBuilder(_dokan)
            .ConfigureLogger(() => dokanLogger)
            .ConfigureOptions(options =>
            {
                options.Options = DokanOptions.FixedDrive;
                options.MountPoint = driveLetters[0] + "\\";
                options.SingleThread = false;
            });
        _dokanInstance = builder.Build(rfs);
        _logger.LogInformation("Mounted {DeviceName} - {DeviceId} on {DriveLetter}", _device.Name,
            _device.Id.ToString(), driveLetters[0]);
    }

    public void Unmount()
    {
        _dokan?.Unmount(_mountLetter![0]);
        _dokanInstance?.Dispose();
        _logger.LogInformation("Unmounted {DeviceName} - {DeviceId} on {DriveLetter}", _device.Name,
            _device.Id.ToString(),
            _mountLetter?[0]);
    }

    private readonly object _lock = new();

    private void SendPacket(Component component, long id = 0)
    {
        var packet = new Packet { Component = component, Id = id };
        var size = Packet.Serializer.GetMaxSize(packet);
        var bytes = ArrayPool<byte>.Shared.Rent(size + 4);
        Span<byte> buffer = bytes;
        var length = Packet.Serializer.Write(buffer[4..], packet);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[..4], length);
        lock (_lock) _link.SendAsync(buffer, length + 4);
        ArrayPool<byte>.Shared.Return(bytes);
    }

    private readonly Random _random = new();

    private NetworkQuery SendQuery(Component component)
    {
        var packetId = _random.NextInt64(long.MaxValue - 1) + 1;
        var responseEvent = new ManualResetEventSlim(false);
        var context = new NetworkQuery(responseEvent);
        _contexts.TryAdd(packetId, context);
        SendPacket(component, packetId);
        responseEvent.Wait();
        return context;
    }

    private string SanitizeName(string fileName)
    {
        return fileName.Replace("\\", "/");
    }
}