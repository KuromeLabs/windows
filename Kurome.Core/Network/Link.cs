using System.Buffers;
using System.Buffers.Binary;
using System.Net.Security;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using FlatSharp;
using Kurome.Fbs;
using Serilog;

namespace Kurome.Core.Network;

public class Link : IDisposable
{
    private readonly SslStream _stream;
    private bool Disposed { get; set; } = false;
    private readonly ILogger _logger = Log.ForContext(typeof(Link));

    private readonly ReplaySubject<bool> _isConnected = new();
    public IObservable<bool> IsConnected => _isConnected.AsObservable();
    
    private readonly Subject<Buffer?> _dataReceived = new();
    public IObservable<Buffer?> DataReceived => _dataReceived.AsObservable();
    private readonly object _lock = new();
    
    public sealed class Buffer 
    {
        public byte[] Data { get; set; }
        public int Size { get; set; }
        public long Id { get; set; }
        
        public void Free()
        {
            ArrayPool<byte>.Shared.Return(Data);
        }
    }

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



    public void Send(ReadOnlySpan<byte> data, int length)
    {
        try
        {
            lock (_lock) _stream.Write(data[..length]);
        }
        catch (Exception e)
        {
            Log.Debug("Exception at Link: {@Exception}", e.ToString());
            _isConnected.OnCompleted();
        }
    }
    

    public async void Start(CancellationToken cancellationToken)
    {
        _isConnected.OnNext(true);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var sizeBuffer = new byte[4];
                if (await ReceiveAsync(sizeBuffer, 4, cancellationToken) == 0) break;
                var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
                var buffer = ArrayPool<byte>.Shared.Rent(size);
                if (await ReceiveAsync(buffer, size, cancellationToken) == 0) break;
                var id = Packet.Serializer.Parse(buffer).Id;
                _dataReceived.OnNext(new Buffer { Data = buffer, Size = size, Id = id });
            }
            catch (Exception e)
            {
                Log.Debug("Exception at Link: {@Exception}", e.ToString());
                break;
            }
        }
        _dataReceived.OnNext(null);
        _isConnected.OnCompleted();
    }
}