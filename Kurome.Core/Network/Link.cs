using System.Net.Security;
using Serilog;

namespace Kurome.Core.Network;

public class Link : IDisposable
{
    private readonly SslStream _stream;
    private bool Disposed { get; set; } = false;

    public event EventHandler<bool>? IsConnectedChanged;

    public Link(SslStream stream)
    {
        _stream = stream;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (Disposed) return;
        if (disposing)
        {
            _stream.Dispose();
            Disposed = true;
        }
        
    }
    
    public void Dispose()
    {
        Dispose(true);
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
            IsConnectedChanged?.Invoke(this, false);
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
            IsConnectedChanged?.Invoke(this, false);
            return 0;
        }
    }

    public void Send(ReadOnlySpan<byte> data, int length)
    {
        _stream.Write(data[..length]);
    }
    

    public async void Start(CancellationToken cancellationToken)
    {
        IsConnectedChanged?.Invoke(this, true);
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