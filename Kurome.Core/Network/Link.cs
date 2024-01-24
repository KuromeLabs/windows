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
    
    private readonly Subject<Buffer> _dataReceived = new();
    public IObservable<Buffer> DataReceived => _dataReceived.AsObservable();
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
            _dataReceived.OnCompleted();
            Disposed = true;
        }
    }

    public void Dispose()
    {
        _logger.Information("Disposing Link");
        Dispose(true);
        GC.SuppressFinalize(this);
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
            _dataReceived.OnError(e);
        }
    }
    

    public void Start(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var sizeBuffer = new byte[4];
                _stream.ReadExactly(sizeBuffer, 0, 4);
                var size = BinaryPrimitives.ReadInt32LittleEndian(sizeBuffer);
                var buffer = ArrayPool<byte>.Shared.Rent(size);
                _stream.ReadExactly(buffer, 0, size);
                var id = Packet.Serializer.Parse(buffer).Id;
                _dataReceived.OnNext(new Buffer { Data = buffer, Size = size, Id = id });
            }
            catch (Exception e)
            {
                Log.Debug("Exception at Link: {@Exception}", e.ToString());
                _dataReceived.OnError(e);
                break;
            }
        }
    }

    public Buffer? GetBufferBlocking(long id, int ms)
    {
        try
        {
            return DataReceived
                .Where(x => x.Id == id)
                .Take(1)
                .Timeout(TimeSpan.FromMilliseconds(ms))
                .Wait();
        }
        catch (Exception e)
        {
            Log.Debug("Exception at Link: {@Exception}", e.ToString());
            _dataReceived.OnError(e);
            return null;
        }
        
    }
    
}