using System.Net.Sockets;
using Application.Core;
using Application.Interfaces;
using MediatR;

namespace Application.Devices;

public class Connect
{
    public class Query : IRequest<Result<ILink>>
    {
        public string Ip { get; set; } = null!;
        public int Port { get; set; }
    }

    public class Handler : IRequestHandler<Query, Result<ILink>>
    {
        private readonly ILinkProvider<TcpClient>  _provider;

        public Handler(ILinkProvider<TcpClient> provider)
        {
            _provider = provider;
        }

        public async Task<Result<ILink>> Handle(Query request, CancellationToken cancellationToken)
        {
            try
            {
                var link = await _provider.CreateClientLinkAsync($"{request.Ip}:{request.Port}", cancellationToken);
                return Result<ILink>.Success(link);
            }
            catch (Exception e)
            {
                return Result<ILink>.Failure(e.Message);
            }
        }
    }
}