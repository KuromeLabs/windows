using Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Serilog;

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
        private readonly ILinkProvider _provider;
        private readonly ILogger<Handler> _logger;

        public Handler(ILinkProvider provider, ILogger<Handler> logger)
        {
            _provider = provider;
            _logger = logger;
        }

        public async Task<ILink> Handle(Query request, CancellationToken cancellationToken)
        {
            var link = await _provider.CreateLinkAsync($"{request.Ip}:{request.Port}", cancellationToken);
            _logger.LogInformation("Connected to {0}", request.Ip);
            return link;
        }
    }
}