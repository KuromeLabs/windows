using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<long, NetworkQuery> _contexts = new();
    private Dokan? _dokan;
    private DokanInstance? _dokanInstance;
    private string? _mountLetter;

    public DeviceAccessor(ILink link, IDeviceAccessorRepository deviceAccessorRepository,
        Device device, IIdentityProvider identityProvider, ILogger<DeviceAccessor> logger, IServiceProvider serviceProvider)
    {
        _link = link;
        _deviceAccessorRepository = deviceAccessorRepository;
        _device = device;
        _identityProvider = identityProvider;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public void Dispose()
    {
        _link.Dispose();
        foreach (var query in _contexts)
            query.Value.Dispose();
        _logger.LogInformation("Disposed DeviceAccessor for {DeviceName} - {DeviceId}", _device.Name, _device.Id.ToString());
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
        SendPacket(new Component(new FileCommand {Command = new FileCommandType (new SetAttributes { Path = SanitizeName(fileName), Attributes = new Attributes { Length = length}})}));
    }

    public Device Get()
    {
        return _device;
    }

    public void SetFileTime(string fileName, long cTime, long laTime, long lwTime)
    {
        SendPacket(new Component(new FileCommand {Command = new FileCommandType (new SetAttributes { Path = SanitizeName(fileName), Attributes = new Attributes { CreationTime = cTime, LastAccessTime = laTime, LastWriteTime = lwTime}})}));
    }

    public void Rename(string fileName, string newFileName)
    {
        SendPacket(new Component(new FileCommand {Command = new FileCommandType (new Rename { OldPath = SanitizeName(fileName), NewPath = SanitizeName(newFileName)})}));
    }

    public IEnumerable<KuromeInformation> GetFileNodes(string fileName)
    {
        var response = SendQuery(new Component(new FileQuery { Type = FileQueryType.GetDirectory, Path = SanitizeName(fileName) }));
        var files = new List<KuromeInformation>();
        var result = response.Packet.Component.Value.FileResponse.Response.Value.Node.Children;
        foreach (var node in result!)
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
        var response = SendQuery(new Component(new FileQuery { Type = FileQueryType.GetDirectory, Path = "/" }));
        var file = response.Packet.Component.Value.FileResponse.Response.Value.Node.Attributes;
        var fileInfo = new KuromeInformation
        {
            FileName = "",
            IsDirectory = file.Type == FileType.Directory,
            LastAccessTime = DateTimeOffset.FromUnixTimeMilliseconds(file.LastAccessTime).LocalDateTime,
            LastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(file.LastWriteTime).LocalDateTime,
            CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(file.CreationTime).LocalDateTime,
            Length = file.Length
        };
        return fileInfo;
    }

    public void GetSpace(out long total, out long free)
    {
        var response = SendQuery(new Component(new DeviceQuery { Type = DeviceQueryType.GetSpace }));
        var deviceInfo = response.Packet!.Component!.Value.DeviceResponse!.Response!.Value.DeviceInfo.Space!;
        
        total = deviceInfo.TotalBytes;
        free = deviceInfo.FreeBytes;
        response.Dispose();
    }

    public void CreateEmptyFile(string fileName)
    {
        SendPacket(new Component(new FileCommand {Command = new FileCommandType (new Create { Path = SanitizeName(fileName), Type = FileType.File})}));
    }

    public void CreateDirectory(string directoryName)
    {
        SendPacket(new Component(new FileCommand {Command = new FileCommandType (new Create { Path = SanitizeName(directoryName), Type = FileType.Directory})}));
    }

    public int ReceiveFileBuffer(byte[] buffer, string fileName, long offset, int bytesToRead, long fileSize)
    {
        var response = SendQuery(new Component(new FileQuery { Type = FileQueryType.ReadFile, Path = SanitizeName(fileName), Length = bytesToRead, Offset = offset}));
        response.Packet!.Component!.Value.FileResponse!.Response!.Value.Raw.Data!.Value.CopyTo(buffer);
        response.Dispose();
        return bytesToRead;
    }

    public void WriteFileBuffer(Memory<byte> buffer, string fileName, long offset)
    {
        SendPacket(new Component(new FileCommand
        {
            Command = new FileCommandType(new Write
                {
                    Path = SanitizeName(fileName), Buffer = new Raw { Data = buffer, Length = buffer.Length, Offset = offset }
                })
        }));
    }

    public void Delete(string fileName)
    {
        SendPacket(new Component(new FileCommand { Command = new FileCommandType(new Delete { Path = SanitizeName(fileName) }) }));
    }

    public void Mount()
    {
        var driveLetters = Enumerable.Range('C', 'Z' - 'C' + 1).Select(i => (char)i + ":")
            .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
        _mountLetter = driveLetters[0];
        var dokanLogger = new NullLogger();
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
        _logger.LogInformation("Mounted {DeviceName} - {DeviceId} on {DriveLetter}", _device.Name, _device.Id.ToString(),
            driveLetters[0]);
    }

    public void Unmount()
    {
        _dokan?.Unmount(_mountLetter![0]);
        _dokanInstance?.Dispose();
        _logger.LogInformation("Unmounted {DeviceName} - {DeviceId} on {DriveLetter}", _device.Name, _device.Id.ToString(),
            _mountLetter?[0]);
    }

    private readonly object _lock = new();

    private void SendPacket(Component component, long id = 0)
    {
        var packet = new Packet { Component = component, Id = id};
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