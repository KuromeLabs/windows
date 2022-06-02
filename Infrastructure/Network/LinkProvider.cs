 using FlatSharp;
using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Application.Interfaces;
using kurome;

namespace Infrastructure.Network;

public class LinkProvider : ILinkProvider<TcpClient>
{
    private readonly IIdentityProvider _identityProvider;
    private readonly ISecurityService<X509Certificate2> _sslService;

    public LinkProvider(IIdentityProvider identityProvider, ISecurityService<X509Certificate2> sslService)
    {
        _identityProvider = identityProvider;
        _sslService = sslService;
    }

    public async Task<ILink> CreateClientLinkAsync(string connectionInfo, CancellationToken cancellationToken)
    {
        var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Parse(connectionInfo.Split(':')[0]), 33587, cancellationToken);
        SendIdentity(client, cancellationToken);
        var stream = new SslStream(client.GetStream(), true, (_, _, _, _) => true);
        await stream.AuthenticateAsClientAsync("Kurome", null, SslProtocols.None, false);
        return new Link(stream);
    }

    public async Task<ILink> CreateServerLinkAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var stream = new SslStream(client.GetStream(), false);
        await stream.AuthenticateAsServerAsync(_sslService.GetSecurityContext(), false, SslProtocols.None, true);
        return new Link(stream);
    }

    private async void SendIdentity(TcpClient client, CancellationToken cancellationToken)
    {
        var identity = $"{_identityProvider.GetEnvironmentName()}:{_identityProvider.GetEnvironmentId()}";
        var identityBytes = Encoding.UTF8.GetBytes(identity);
        var size = identityBytes.Length;
        var bytes = new byte[size + 4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan()[..4], size);
        identityBytes.CopyTo(bytes.AsSpan()[4..]);
        await client.GetStream().WriteAsync(bytes, cancellationToken);
    }

    
}