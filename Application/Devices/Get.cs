// using Application.Core;
// using Domain;
// using MediatR;
// using Tenray.ZoneTree;
//
// namespace Application.Devices;
//
// public class Get
// {
//     public class Query : IRequest<Result<Device>>
//     {
//         public Guid Id { get; set; }
//     }
//
//     public class Handler : IRequestHandler<Query, Result<Device>>
//     {
//         private readonly IZoneTree<string, Device> _zoneTree;
//
//         public Handler(IZoneTree<string, Device> zoneTree)
//         {
//             _zoneTree = zoneTree;
//         }
//
//         public async Task<Result<Device>> Handle(Query request, CancellationToken cancellationToken)
//         {
//             var b = _zoneTree.TryGet(request.Id.ToString(), out var device);
//             return !b ? Result<Device>.Failure("Not found") : Result<Device>.Success(device);
//         }
//     }
// }

