using Application.Core;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence;

namespace Application.Devices;

public class Get
{
    public class Query : IRequest<Result<Device>>
    {
        public Guid Id { get; set; }
    }

    public class Handler : IRequestHandler<Query, Result<Device>>
    {
        private readonly DataContext _context;

        public Handler(DataContext context)
        {
            _context = context;
        }

        public async Task<Result<Device>> Handle(Query request, CancellationToken cancellationToken)
        {
            var device = await _context.Devices.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            return device == null ? Result<Device>.Failure("Not found") : Result<Device>.Success(device);
        }
    }
}