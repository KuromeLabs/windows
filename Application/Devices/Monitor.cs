using Application.Core;
using Application.Interfaces;
using Domain;
using MediatR;
using Tenray.ZoneTree;

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
        private readonly IZoneTree<string, Device> _zoneTree;

        public Handler(IDeviceAccessorFactory deviceAccessorFactory, IDeviceAccessorRepository deviceAccessorRepository, IZoneTree<string, Device> zoneTree)
        {
            _deviceAccessorFactory = deviceAccessorFactory;
            _deviceAccessorRepository = deviceAccessorRepository;
            _zoneTree = zoneTree;
        }

        public async Task<Result<IDeviceAccessor>> Handle(Query request, CancellationToken cancellationToken)
        {
            if (!_zoneTree.TryGet(request.Id.ToString(), out var device))
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