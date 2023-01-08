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
        try
        {
            await _stream.ReadExactlyAsync(buffer, 0, size, cancellationToken);
            return size;
        }
        catch (Exception e)
        {
            Log.Debug("Exception at Link: {@Exception}", e.ToString());
            return 0;
        }
    }

    public int Receive(byte[] buffer, int size)
    {
        try
        {
            _stream.ReadExactly(buffer, 0, size);
            return size;
        }
        catch (Exception e)
        {
            Log.Debug("Exception at Link: {@Exception}", e.ToString());
            return 0;
        }
    }

    public void Send(ReadOnlySpan<byte> data, int length)
    {
        _stream.Write(data[..length]);
    }
}