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
        private readonly IDeviceAccessorFactory _deviceAccessorFactory;
        private readonly DataContext _dataContext;

        public Handler(IDeviceAccessorFactory deviceAccessorFactory, DataContext dataContext)
        {
            _deviceAccessorFactory = deviceAccessorFactory;
            _dataContext = dataContext;
        }

        public async Task<Result<Unit>> Handle(Command request, CancellationToken cancellationToken)
        {
            var device =
                await _dataContext.Devices.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            // if (device == null) return Result<Unit>.Failure("Device not found in database. Is it paired?");
            _deviceAccessorFactory.Mount(request.Id);
            return Result<Unit>.Success(Unit.Value);
        }
    }
}