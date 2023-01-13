using System.Net.Sockets;
using Application.Core;
using Application.Interfaces;
using MessagePipe;

namespace Application.Devices;

public class Connect
{
    public struct Query
    {
        public Query(string ip, int port)
        {
            Port = port;
            Ip = ip;
        }

        public string Ip;
        public int Port;
    }

    public class Handler : IAsyncRequestHandler<Query, Result<ILink>>
    {
        private readonly ILinkProvider<TcpClient> _provider;

        public Handler(ILinkProvider<TcpClient> provider)
        {
            _provider = provider;
        }

        public async ValueTask<Result<ILink>> InvokeAsync(Query request, CancellationToken cancellationToken = new())
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