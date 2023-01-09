using Application.Core;
using Application.Dokany;
using Application.Interfaces;
using Application.MediatorExtensions;
using DokanNet;
using DokanNet.Logging;
using Domain;
using MessagePipe;
using Tenray.ZoneTree;

namespace Application.Devices;

public class Mount
{
    public struct Command
    {
        public Command(Guid id)
        {
            Id = id;
        }
        public Guid Id;
    }

    public class Handler : IAsyncRequestHandler<Command, Result<Unit>>
    {
        private readonly IDeviceAccessorHolder _deviceAccessorHolder;
        private readonly IZoneTree<string, Device> _zoneTree;
        private readonly IKuromeOperationsHolder _holder;
        private readonly IKuromeOperationsFactory _factory;

        public Handler(IDeviceAccessorHolder deviceAccessorHolder, IZoneTree<string, Device> zoneTree, IKuromeOperationsHolder holder, IKuromeOperationsFactory factory)
        {
            _deviceAccessorHolder = deviceAccessorHolder;
            _zoneTree = zoneTree;
            _holder = holder;
            _factory = factory;
        }

        public async ValueTask<Result<Unit>> InvokeAsync(Command request, CancellationToken cancellationToken = new())
        {
            var b = _zoneTree.TryGet(request.Id.ToString(), out var device);
            // if (device == null) return Result<Unit>.Failure("Device not found in database. Is it paired?");
            var accessor = _deviceAccessorHolder.Get(request.Id.ToString());
            if (accessor == null) return Result<Unit>.Failure("Device accessor not found");
            
            var driveLetters = Enumerable.Range('C', 'Z' - 'C' + 1).Select(i => (char)i + ":")
                .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
            var mountLetter = driveLetters[0];
            var rfs = _factory.Create(accessor, mountLetter);
            
            var builder = new DokanInstanceBuilder(_holder.GetDokan())
                .ConfigureLogger(() => new ConsoleLogger())
                .ConfigureOptions(options =>
                {
                    options.Options = DokanOptions.FixedDrive;
                    options.MountPoint = mountLetter + "\\";
                    options.SingleThread = false;
                });

            var dokanInstance = builder.Build(rfs);
            
            _holder.Add(request.Id.ToString(), rfs, dokanInstance);
            return Result<Unit>.Success(Unit.Value);
        }
    }
}