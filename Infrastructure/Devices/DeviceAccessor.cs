using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using Application.Interfaces;
using AutoMapper;
using DokanNet;
using DokanNet.Logging;
using Domain;
using FlatSharp;
using Infrastructure.Dokany;
using kurome;
using Serilog;
using Action = kurome.Action;

namespace Infrastructure.Devices;

public class DeviceAccessor : IDeviceAccessor
{
    private class NetworkQuery
    {
        public Packet? Packet;
        public byte[]? Buffer;
        public ManualResetEventSlim ResponseEvent;

        public NetworkQuery(int id, ManualResetEventSlim responseEvent)
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
    private readonly IDeviceAccessorFactory _deviceAccessorFactory;
    private readonly Device _device;
    private readonly IIdentityProvider _identityProvider;
    private readonly IMapper _mapper;
    private readonly ConcurrentDictionary<int, NetworkQuery> _contexts = new();
    private SemaphoreSlim? _mountSemaphore;
    private Dokan? _mountInstance;
    private string _mountLetter;

    public DeviceAccessor(ILink link, IDeviceAccessorFactory deviceAccessorFactory,
        Device device, IIdentityProvider identityProvider, IMapper mapper)
    {
        _link = link;
        _deviceAccessorFactory = deviceAccessorFactory;
        _device = device;
        _identityProvider = identityProvider;
        _mapper = mapper;
    }

    public void Dispose()
    {
        _deviceAccessorFactory.Unregister(_device.Id.ToString());
        _link.Dispose();
        Log.Information("DeviceAccessor Disposed");
        Unmount();
    }

    public async void Start(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var bytes = await _link.ReceiveAsync(cancellationToken);
            if (bytes == null) break;
            var packet = Packet.Serializer.Parse(bytes);
            if (_contexts.ContainsKey(packet.Id))
            {
                _contexts[packet.Id].Packet = packet;
                _contexts[packet.Id].Buffer = bytes;
                _contexts[packet.Id].ResponseEvent.Set();
                Log.Information("Received {BytesLength} bytes from: {DeviceName} - {DeviceId}",
                    bytes.Length, _device.Name, _device.Id.ToString());
                _contexts.TryRemove(packet.Id, out _);
            }
            else ArrayPool<byte>.Shared.Return(bytes);
        }

        Log.Information("DeviceAccessor Cancelled");
        Dispose();
    }

    public void SetLength(string fileName, long length)
    {
        SendCommand(fileName, Action.ActionSetFileTime, fileLength: length);
    }

    public Device Get()
    {
        return _device;
    }

    public void SetFileTime(string fileName, long cTime, long laTime, long lwTime)
    {
        SendCommand(fileName, Action.ActionSetFileTime, cTime: cTime, laTime: laTime, lwTime: lwTime);
    }

    public void Rename(string fileName, string newFileName)
    {
        SendCommand(fileName, Action.ActionRename, newFileName);
    }

    public IEnumerable<KuromeInformation> GetFileNodes(string fileName)
    {
        var query = SendQuery(fileName, Action.ActionGetDirectory);
        var files = new List<KuromeInformation>();
        for (var i = 0; i < query.Packet!.Nodes!.Count; i++)
        {
            var node = query.Packet.Nodes![i];
            var file = new KuromeInformation
            {
                FileName = node.Filename!,
                IsDirectory = node.FileType == FileType.Directory,
                LastAccessTime = DateTimeOffset.FromUnixTimeMilliseconds(node.LastAccessTime).LocalDateTime,
                LastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(node.LastWriteTime).LocalDateTime,
                CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(node.CreationTime).LocalDateTime,
                Length = node.Length
            };
            files.Add(file);
        }

        query.Dispose();
        return files;
    }

    public KuromeInformation GetRootNode()
    {
        var result = GetFileNode("\\");
        result.FileName = "";
        return result;
    }

    public KuromeInformation GetFileNode(string fileName)
    {
        var response = SendQuery(fileName, Action.ActionGetFileInfo);
        var file = response.Packet!.Nodes![0];
        var fileInfo = new KuromeInformation
        {
            FileName = file.Filename!,
            IsDirectory = file.FileType == FileType.Directory,
            LastAccessTime = DateTimeOffset.FromUnixTimeMilliseconds(file.LastAccessTime).LocalDateTime,
            LastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(file.LastWriteTime).LocalDateTime,
            CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(file.CreationTime).LocalDateTime,
            Length = file.Length
        };
        response.Dispose();
        return fileInfo;
    }

    public void GetSpace(out long total, out long free)
    {
        var response = SendQuery(action: Action.ActionGetSpaceInfo);
        var deviceInfo = response.Packet!.DeviceInfo!;
        total = deviceInfo.TotalBytes;
        free = deviceInfo.FreeBytes;
        response.Dispose();
    }

    public void CreateEmptyFile(string fileName)
    {
        SendCommand(fileName, Action.ActionCreateFile);
    }

    public void CreateDirectory(string directoryName)
    {
        SendCommand(directoryName, Action.ActionCreateDirectory);
    }

    public int ReceiveFileBuffer(byte[] buffer, string fileName, long offset, int bytesToRead, long fileSize)
    {
        var response = SendQuery(fileName, Action.ActionReadFileBuffer, rawOffset: offset,
            rawLength: bytesToRead);
        response.Packet!.FileBuffer?.Data!.Value.CopyTo(buffer);
        response.Dispose();
        return bytesToRead;
    }

    public void WriteFileBuffer(byte[] buffer, string fileName, long offset)
    {
        SendCommand(fileName, Action.ActionWriteFileBuffer, rawOffset: offset, rawBuffer: buffer, id: 0);
    }

    public void Delete(string fileName)
    {
        SendCommand(fileName, Action.ActionDelete);
    }

    public void Mount()
    {
        var driveLetters = Enumerable.Range('C', 'Z' - 'C' + 1).Select(i => (char) i + ":")
            .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
        _mountLetter = driveLetters[0];
        var dokanLogger = new ConsoleLogger("[Kurome] ");
        _mountInstance = new Dokan(dokanLogger);
        var rfs = new KuromeOperations(_mapper, this);
        Log.Information("Mounting {DeviceName} - {DeviceId} on letter {DriveLetter}", _device.Name, _device.Id.ToString(), driveLetters[0]);
        var builder = new DokanInstanceBuilder(_mountInstance)
            .ConfigureLogger(() => dokanLogger)
            .ConfigureOptions(options =>
            {
                options.Options = DokanOptions.FixedDrive;
                options.MountPoint = driveLetters[0] + "\\";
            });
        Task.Run(async () =>
        {
            _mountSemaphore = new SemaphoreSlim(0, 1);
            using var instance = builder.Build(rfs);
            await _mountSemaphore.WaitAsync();
        });
    }

    public void Unmount()
    {
        _mountInstance?.Unmount(_mountLetter[0]);
        _mountSemaphore?.Release();
    }

    private readonly object _lock = new();

    private void SendCommand(string filename = "", Action action = Action.NoAction,
        string nodeName = "", long cTime = 0, long laTime = 0, long lwTime = 0, FileType fileType = 0,
        long fileLength = 0, long rawOffset = 0, byte[]? rawBuffer = null, int rawLength = 0, int id = 0,
        PairEvent pair = 0)
    {
        lock (_lock)
        {
            filename = filename.Replace('\\', '/');
            nodeName = nodeName.Replace('\\', '/');
            var packet = new Packet
            {
                Action = action,
                Path = filename,
                FileBuffer = new Raw
                {
                    Data = rawBuffer,
                    Length = rawLength,
                    Offset = rawOffset
                },
                Nodes = new FileBuffer[]
                {
                    new()
                    {
                        CreationTime = cTime,
                        Filename = nodeName,
                        FileType = fileType,
                        LastAccessTime = laTime,
                        LastWriteTime = lwTime,
                        Length = fileLength
                    }
                },
                Id = id,
                Pair = pair,
                DeviceInfo = new DeviceInfo
                {
                    FreeBytes = 0,
                    Id = _identityProvider.GetEnvironmentId(),
                    Name = _identityProvider.GetEnvironmentName(),
                    TotalBytes = 0
                }
            };
            var size = Packet.Serializer.GetMaxSize(packet);
            var bytes = ArrayPool<byte>.Shared.Rent(size + 4);
            Span<byte> buffer = bytes;
            var length = Packet.Serializer.Write(buffer[4..], packet);
            BinaryPrimitives.WriteInt32LittleEndian(buffer[..4], length);
            _link.SendAsync(buffer, length + 4);
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    private readonly Random _random = new();

    private NetworkQuery SendQuery(string filename = "", Action action = Action.NoAction,
        string nodeName = "", long cTime = 0, long laTime = 0, long lwTime = 0, FileType fileType = 0,
        long fileLength = 0, long rawOffset = 0, byte[]? rawBuffer = null, int rawLength = 0, int id = 0,
        PairEvent pair = 0)
    {
        var packetId = _random.Next(int.MaxValue - 1) + 1;
        var responseEvent = new ManualResetEventSlim(false);
        var context = new NetworkQuery(packetId, responseEvent);
        _contexts.TryAdd(packetId, context);
        SendCommand(filename, action, nodeName, cTime, laTime, lwTime, fileType, fileLength, rawOffset,
            rawBuffer,
            rawLength, packetId);
        responseEvent.Wait();
        return context;
    }

    public void AcceptPairing()
    {
        SendCommand(action: Action.ActionPair, pair: PairEvent.Pair);
    }
}