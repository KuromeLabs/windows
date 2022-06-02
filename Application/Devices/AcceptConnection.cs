using System.Net.Sockets;
using Application.Core;
using Application.Interfaces;
using MediatR;

namespace Application.Devices;

public class AcceptConnection
{
    public class Query : IRequest<Result<ILink>>
    {
        public TcpClient TcpClient { get; set; } = null!;
    }

    public class Handler : IRequestHandler<Query, Result<ILink>>
    {
        private readonly ILinkProvider<TcpClient> _linkProvider;

        public Handler(ILinkProvider<TcpClient> linkProvider)
        {
            _linkProvider = linkProvider;
        }

        public async Task<Result<ILink>> Handle(Query request, CancellationToken cancellationToken)
        {
            try
            {
                var link = await _linkProvider.CreateServerLinkAsync(request.TcpClient, cancellationToken);
                return Result<ILink>.Success(link);
            }
            catch (Exception e)
            {
                return Result<ILink>.Failure(e.Message);
            }
        }
    }
}