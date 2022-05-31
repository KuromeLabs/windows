using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Application.Interfaces;
using Kurome;

namespace Infrastructure.Network;

public class LinkProvider : ILinkProvider
{
    public async Task<ILink> CreateLinkAsync(string connectionInfo, CancellationToken cancellationToken)
    {
        var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Parse(connectionInfo.Split(':')[0]), 33587, cancellationToken);
        SendIdentity(client);
        var stream = new SslStream(client.GetStream(), true, (_, _, _, _) => true);
        await stream.AuthenticateAsClientAsync("Kurome", null, SslProtocols.None, false);
        return new Link(stream);
    }
    
    private void SendIdentity(TcpClient client)
    {
        var identity = $"{IdentityProvider.GetMachineName()}:{IdentityProvider.GetGuid()}";
        var identityBytes = Encoding.UTF8.GetBytes(identity);
        var size = identityBytes.Length;
        var bytes = new byte[size + 4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan()[..4], size);
        identityBytes.CopyTo(bytes.AsSpan()[4..]);
        client.GetStream().Write(bytes);
    }
}