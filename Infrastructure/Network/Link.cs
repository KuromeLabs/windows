using System.Net.Security;
using Application.Interfaces;
using Serilog;

namespace Infrastructure.Network;

public class Link : ILink
{
    private readonly SslStream _stream;

    public Link(SslStream stream)
    {
        _stream = stream;
    }

    public void Dispose()
    {
        _stream.Close();
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }

    public async Task<int> ReceiveAsync(byte[] buffer, int size, CancellationToken cancellationToken)
    {
        var bytesRead = 0;
        try
        {
            while (bytesRead != size)
            {
                var current = await _stream.ReadAsync(buffer.AsMemory(0 + bytesRead, size - bytesRead), cancellationToken);
                bytesRead += current;
                if (current != 0) continue;
                return bytesRead;
            }
        }
        catch (Exception e)
        {
            Log.Debug("Exception at Link: {@Exception}", e.ToString());
            return 0;
        }
        return bytesRead;
    }

    public void SendAsync(ReadOnlySpan<byte> data, int length)
    {
        _stream.Write(data[..length]);
    }
}