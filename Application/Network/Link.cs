using System.Buffers.Binary;
using System.Net.Security;
using Serilog;

namespace Application.Network;

public class Link : IDisposable
{
    private readonly SslStream _stream;

    public event EventHandler<bool>? IsConnectedChanged;

    public Link(SslStream stream)
    {
        _stream = stream;
    }

    public void Dispose()
    {
        Log.Information("Disposing Link");
        if (IsConnectedChanged != null) IsConnectedChanged.Invoke(this, false);
        _stream.Close();
        _stream.Dispose();
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
            Dispose();
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
            Dispose();
            return 0;
        }
    }

    public void Send(ReadOnlySpan<byte> data, int length)
    {
        _stream.Write(data[..length]);
    }
    

    public async void Start(CancellationToken cancellationToken)
    {
        if (IsConnectedChanged != null) IsConnectedChanged.Invoke(this, true);
        // while (!cancellationToken.IsCancellationRequested)
        // {
        //     var sizeBuffer = new byte[4];
        //     await ReceiveAsync(sizeBuffer, 4, cancellationToken);
        //     var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
        //     var buffer = new byte[size];
        //     await ReceiveAsync(buffer, size, cancellationToken);
        //     
        // }
    }
}