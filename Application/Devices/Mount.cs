using Application.Core;
using Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence;

namespace Application.Devices;

public class Mount
{
    public class Command : IRequest<Result<Unit>>
    {
        public Guid Id { get; set; }
    }

    public class Handler : IRequestHandler<Command, Result<Unit>>
    {
        private readonly IDeviceAccessorRepository _deviceAccessorRepository;
        private readonly DataContext _dataContext;

        public Handler(IDeviceAccessorRepository deviceAccessorRepository, DataContext dataContext)
        {
            _deviceAccessorRepository = deviceAccessorRepository;
            _dataContext = dataContext;
        }

        public async Task<Result<Unit>> Handle(Command request, CancellationToken cancellationToken)
        {
            var device =
                await _dataContext.Devices.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            // if (device == null) return Result<Unit>.Failure("Device not found in database. Is it paired?");
            var accessor = _deviceAccessorRepository.Get(request.Id.ToString());
            if (accessor == null) return Result<Unit>.Failure("Device accessor not found");
            accessor.Mount();
            return Result<Unit>.Success(Unit.Value);
        }
    }
}