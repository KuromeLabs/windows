using System.Net.Sockets;
using Application.Core;
using Application.Interfaces;
using MessagePipe;

namespace Application.Devices;

public class AcceptConnection
{
    public struct Query
    {
        public Query(TcpClient tcpClient)
        {
            TcpClient = tcpClient;
        }

        public readonly TcpClient TcpClient;
    }

    public class Handler : IAsyncRequestHandler<Query, Result<ILink>>
    {
        private readonly ILinkProvider<TcpClient> _linkProvider;

        public Handler(ILinkProvider<TcpClient> linkProvider)
        {
            _linkProvider = linkProvider;
        }

        public async ValueTask<Result<ILink>> InvokeAsync(Query request, CancellationToken cancellationToken = new())
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