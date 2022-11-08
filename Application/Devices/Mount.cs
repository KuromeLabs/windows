using Application.Core;
using Application.Interfaces;
using Application.MediatorExtensions;
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
        private readonly IDeviceAccessorRepository _deviceAccessorRepository;
        private readonly IZoneTree<string, Device> _zoneTree;

        public Handler(IDeviceAccessorRepository deviceAccessorRepository, IZoneTree<string, Device> zoneTree)
        {
            _deviceAccessorRepository = deviceAccessorRepository;
            _zoneTree = zoneTree;
        }

        public async Task<Result<Unit>> Handle(Command request, CancellationToken cancellationToken)
        {

            var b = _zoneTree.TryGet(request.Id.ToString(), out var device);
            // if (device == null) return Result<Unit>.Failure("Device not found in database. Is it paired?");
            var accessor = _deviceAccessorRepository.Get(request.Id.ToString());
            if (accessor == null) return Result<Unit>.Failure("Device accessor not found");
            accessor.Mount();
            return Result<Unit>.Success(Unit.Value);
        }

        public async ValueTask<Result<Unit>> InvokeAsync(Command request, CancellationToken cancellationToken = new CancellationToken())
        {
            var b = _zoneTree.TryGet(request.Id.ToString(), out var device);
            // if (device == null) return Result<Unit>.Failure("Device not found in database. Is it paired?");
            var accessor = _deviceAccessorRepository.Get(request.Id.ToString());
            if (accessor == null) return Result<Unit>.Failure("Device accessor not found");
            accessor.Mount();
            return Result<Unit>.Success(Unit.Value);
        }
    }
}