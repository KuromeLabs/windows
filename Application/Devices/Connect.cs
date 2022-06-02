using System.Net.Sockets;
using Application.Interfaces;
using MediatR;

namespace Application.Devices;

public class Connect
{
    public class Query : IRequest<ILink>
    {
        public string Ip { get; set; } = null!;
        public int Port { get; set; }
    }

    public class Handler : IRequestHandler<Query, ILink>
    {
        private readonly ILinkProvider<TcpClient>  _provider;

        public Handler(ILinkProvider<TcpClient> provider)
        {
            _provider = provider;
        }

        public async Task<ILink> Handle(Query request, CancellationToken cancellationToken)
        {
            return await _provider.CreateClientLinkAsync($"{request.Ip}:{request.Port}", cancellationToken);
        }
    }
}