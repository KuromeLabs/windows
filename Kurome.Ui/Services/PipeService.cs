using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using FlatSharp;
using Kurome.Fbs.Ipc;
using Serilog;

namespace Kurome.Ui.Services;

public enum PipeConnectionState
{
    Connecting,
    Connected,
    Disconnected
}

public class PipeService
{
    private const string PipeName = "KuromePipe";
    private const int MaxPacketSize = 1024 * 1024;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);
    private const int ConnectTimeoutMs = 3000;

    private readonly ILogger _logger = Log.ForContext<PipeService>();
    private readonly Subject<IpcPacket> _packets = new();
    private readonly BehaviorSubject<PipeConnectionState> _connectionState =
        new(PipeConnectionState.Connecting);
    private readonly object _sendLock = new();
    private readonly object _stateLock = new();

    private NamedPipeClientStream? _pipe;
    private PipeConnectionState _currentState = PipeConnectionState.Connecting;

    public IObservable<IpcPacket> Packets => _packets.AsObservable();

    public IObservable<PipeConnectionState> ConnectionState => _connectionState.AsObservable();

    public bool IsConnected => _currentState == PipeConnectionState.Connected;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        SetState(PipeConnectionState.Connecting);

        while (!cancellationToken.IsCancellationRequested)
        {
            var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous);
            try
            {
                await pipe.ConnectAsync(ConnectTimeoutMs, cancellationToken);

                lock (_sendLock) _pipe = pipe;
                _logger.Information("Connected to the Kurome service");
                SetState(PipeConnectionState.Connected);

                await ReadLoopAsync(pipe, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (TimeoutException)
            {
            }
            catch (Exception e)
            {
                _logger.Debug(e, "Pipe connection dropped");
            }
            finally
            {
                lock (_sendLock)
                {
                    if (ReferenceEquals(_pipe, pipe)) _pipe = null;
                }

                await pipe.DisposeAsync();
                SetState(PipeConnectionState.Disconnected);
            }

            try
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        SetState(PipeConnectionState.Disconnected);
    }

    private void SetState(PipeConnectionState state)
    {
        lock (_stateLock)
        {
            if (_currentState == state) return;
            _logger.Information("Pipe state: {Previous} -> {State}", _currentState, state);
            _currentState = state;
        }

        UiDispatch.Post(() => _connectionState.OnNext(state));
    }

    private async Task ReadLoopAsync(PipeStream pipe, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        while (!cancellationToken.IsCancellationRequested && pipe.IsConnected)
        {
            await pipe.ReadExactlyAsync(lengthBuffer, cancellationToken);
            var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
            if (length is <= 0 or > MaxPacketSize)
                throw new InvalidDataException($"Refusing a {length} byte IPC packet");

            var buffer = new byte[length];
            await pipe.ReadExactlyAsync(buffer, cancellationToken);

            IpcPacket packet;
            try
            {
                packet = IpcPacket.Serializer.Parse(buffer);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Discarding an unparseable IPC packet");
                continue;
            }

            UiDispatch.Post(() => _packets.OnNext(packet));
        }
    }

    public void Send(IpcPacket ipcPacket)
    {
        lock (_sendLock)
        {
            var pipe = _pipe;
            if (pipe is not { IsConnected: true })
            {
                _logger.Debug("Dropping an IPC message: the service is not connected");
                return;
            }

            var buffer = ArrayPool<byte>.Shared.Rent(4 + IpcPacket.Serializer.GetMaxSize(ipcPacket));
            try
            {
                var length = IpcPacket.Serializer.Write(buffer.AsSpan()[4..], ipcPacket);
                BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan()[..4], length);
                pipe.Write(buffer, 0, length + 4);
                pipe.Flush();
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error while writing to the pipe");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
