using Application.Core;
using Application.Interfaces;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence;

namespace Application.Devices;

public class Monitor
{
    public class Query : IRequest<Result<IDeviceAccessor>>
    {
        public string Name { get; set; } = null!;
        public Guid Id;
        public ILink Link = null!;
    }

    public class Handler : IRequestHandler<Query, Result<IDeviceAccessor>>
    {
        private readonly IDeviceAccessorFactory _deviceAccessorFactory;
        private readonly IDeviceAccessorRepository _deviceAccessorRepository;
        private readonly DataContext _dataContext;

        public Handler(IDeviceAccessorFactory deviceAccessorFactory, IDeviceAccessorRepository deviceAccessorRepository,
            DataContext dataContext)
        {
            _deviceAccessorFactory = deviceAccessorFactory;
            _deviceAccessorRepository = deviceAccessorRepository;
            _dataContext = dataContext;
        }

        public async Task<Result<IDeviceAccessor>> Handle(Query request, CancellationToken cancellationToken)
        {
            var device = await _dataContext.Devices.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (device == null)
            {
                device = new Device
                {
                    Id = request.Id,
                    Name = request.Name,
                };
            }

            var deviceAccessor = _deviceAccessorFactory.Create(request.Link, device);
            _deviceAccessorRepository.Add(device.Id.ToString(), deviceAccessor);
            deviceAccessor.Start(cancellationToken);
            return Result<IDeviceAccessor>.Success(deviceAccessor);
        }
    }
}