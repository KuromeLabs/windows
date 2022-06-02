using System.Net.Sockets;
using Application.Interfaces;
using MediatR;

namespace Application.Devices;

public class AcceptConnection
{
    public class Query : IRequest<ILink>
    {
        public TcpClient TcpClient { get; set; } = null!;
    }

    public class Handler : IRequestHandler<Query, ILink>
    {
        private readonly ILinkProvider<TcpClient> _linkProvider;

        public Handler(ILinkProvider<TcpClient> linkProvider)
        {
            _linkProvider = linkProvider;
        }

        public async Task<ILink> Handle(Query request, CancellationToken cancellationToken)
        {
            return await _linkProvider.CreateServerLinkAsync(request.TcpClient, cancellationToken);;
        }
    }
}