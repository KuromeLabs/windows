using System.Buffers;
using System.Buffers.Binary;
using System.Net.Security;
using Application.Interfaces;

namespace Infrastructure.Network;

public class Link : ILink
{
    public readonly SslStream _stream;

    public Link(SslStream stream)
    {
        _stream = stream;
    }

    public void Dispose()
    {
        _stream.Close();
        _stream.Dispose();
    }

    public async Task<byte[]?> ReceiveAsync(CancellationToken cancellationToken)
    {
        var sizeBuffer = new byte[4];
        var bytesRead = 0;
        try
        {
            int current;
            while (bytesRead != 4)
            {
                current = await _stream.ReadAsync(sizeBuffer.AsMemory(0 + bytesRead, 4 - bytesRead), cancellationToken);
                bytesRead += current;
                if (current != 0) continue;
                return null;
            }

            var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
            bytesRead = 0;
            var readBuffer = ArrayPool<byte>.Shared.Rent(size);
            while (bytesRead != size)
            {
                current = await _stream.ReadAsync(readBuffer.AsMemory(0 + bytesRead, size - bytesRead), cancellationToken);
                bytesRead += current;
                if (current != 0) continue;
                return null;
            }

            return readBuffer;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }

    public void SendAsync(ReadOnlySpan<byte> data, int length)
    {
        _stream.Write(data[..length]);
    }
}