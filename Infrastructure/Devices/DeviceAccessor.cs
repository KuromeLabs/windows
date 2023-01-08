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
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Infrastructure.Devices;

public class DeviceAccessor : IDeviceAccessor
{
    private readonly ILink _link;
    private readonly IDeviceAccessorRepository _deviceAccessorRepository;
    private readonly Device _device;
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly FlatBufferHelper _flatBufferHelper;
    private readonly IMapper _mapper;
    private Dokan? _dokan;
    private DokanInstance? _dokanInstance;
    private string? _mountLetter;
    private readonly object _lock = new();

    // TODO: MessagePipe experiments
    // private readonly IAsyncPublisher<long, Packet> _networkQueryPublisher;
    // private readonly IAsyncSubscriber<long, Packet> _networkQuerySubscriber;

    public DeviceAccessor(ILink link, IDeviceAccessorRepository deviceAccessorRepository,
        Device device, ILogger<DeviceAccessor> logger,
        IServiceProvider serviceProvider, FlatBufferHelper flatBufferHelper, IMapper mapper)
    {
        _link = link;
        _deviceAccessorRepository = deviceAccessorRepository;
        _device = device;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _flatBufferHelper = flatBufferHelper;
        _mapper = mapper;
    }

    public void Dispose()
    {
        _link.Dispose();
        _logger.LogInformation("Disposed DeviceAccessor for {DeviceName} - {DeviceId}", _device.Name,
            _device.Id.ToString());
        Unmount();
        _deviceAccessorRepository.Remove(_device.Id.ToString());
        GC.SuppressFinalize(this);
    }

    public async void Start(CancellationToken cancellationToken)
    {

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
        _flatBufferHelper.TryGetFileResponseNode(response, out var result);
        return _mapper.Map<IEnumerable<KuromeInformation>>(result!.Children!);
    }

    public KuromeInformation GetRootNode()
    {
        var response = SendQuery(_flatBufferHelper.GetDirectoryQuery("/"));
        _flatBufferHelper.TryGetFileResponseNode(response, out var file);
        return _mapper.Map<KuromeInformation>(file!);
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
        SendPacket(_flatBufferHelper.CreateFileCommand(SanitizeName(fileName)));
    }

    public void CreateDirectory(string directoryName)
    {
        SendPacket(_flatBufferHelper.CreateDirectoryCommand(SanitizeName(directoryName)));
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

    

    private void SendPacket(Component component, long id = 0)
    {
        var packet = new Packet { Component = component, Id = id };
        var size = Packet.Serializer.GetMaxSize(packet);
        Span<byte> buffer = new byte[4 + size];
        var length = Packet.Serializer.Write(buffer[4..], packet);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[..4], length);
        lock (_lock) _link.Send(buffer, length + 4);
    }

    private Packet ReadPacket()
    {
        lock (_lock)
        {
            
            var sizeBuffer = new byte[4];
            _link.Receive(sizeBuffer, 4);
            var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
            var buffer = new byte[size];
            _link.Receive(buffer, size);
            return Packet.Serializer.Parse(buffer);
        }
    }

    private Packet SendQuery(Component component)
    {
        SendPacket(component);
        return ReadPacket();
    }

    private string SanitizeName(string fileName)
    {
        return fileName.Replace("\\", "/");
    }
}