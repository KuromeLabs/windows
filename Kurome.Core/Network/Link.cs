using System.Net.Security;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Serilog;

namespace Kurome.Core.Network;

public class Link : IDisposable
{
    private readonly SslStream _stream;
    private bool Disposed { get; set; } = false;
    private readonly ILogger _logger = Serilog.Log.ForContext(typeof(Link));

    private readonly Subject<bool> _isConnected = new();
    public IObservable<bool> IsConnected => _isConnected.AsObservable();

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
        _logger.Information("Disposing Link");
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
            _isConnected.OnCompleted();
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
            _isConnected.OnCompleted();
            return 0;
        }
    }

    public void Send(ReadOnlySpan<byte> data, int length)
    {
        _stream.Write(data[..length]);
    }
    

    public async void Start(CancellationToken cancellationToken)
    {
        _isConnected.OnNext(true);
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