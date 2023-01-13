using Application.Core;
using Application.Interfaces;
using Domain;
using MessagePipe;
using Tenray.ZoneTree;

namespace Application.Devices;

public class Monitor
{
    public struct Query
    {
        public readonly string Name;
        public readonly Guid Id;
        public readonly ILink Link;

        public Query(string name, Guid id, ILink link)
        {
            Name = name;
            Id = id;
            Link = link;
        }
    }

    public class Handler : IAsyncRequestHandler<Query, Result<IDeviceAccessor>>
    {
        private readonly IDeviceAccessorFactory _deviceAccessorFactory;
        private readonly IDeviceAccessorHolder _deviceAccessorHolder;
        private readonly IZoneTree<string, Device> _zoneTree;

        public Handler(IDeviceAccessorFactory deviceAccessorFactory, IDeviceAccessorHolder deviceAccessorHolder,
            IZoneTree<string, Device> zoneTree)
        {
            _deviceAccessorFactory = deviceAccessorFactory;
            _deviceAccessorHolder = deviceAccessorHolder;
            _zoneTree = zoneTree;
        }

        public async ValueTask<Result<IDeviceAccessor>> InvokeAsync(Query request,
            CancellationToken cancellationToken = new())
        {
            if (!_zoneTree.TryGet(request.Id.ToString(), out var device))
                device = new Device
                {
                    Id = request.Id,
                    Name = request.Name
                };

            var deviceAccessor = _deviceAccessorFactory.Create(request.Link, device);
            _deviceAccessorHolder.Add(device.Id.ToString(), deviceAccessor);
            return Result<IDeviceAccessor>.Success(deviceAccessor);
        }
    }
}