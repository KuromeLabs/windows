namespace Application.Interfaces;

public interface ILink : IDisposable
{
    Task<int> ReceiveAsync(byte[] buffer, int size, CancellationToken cancellationToken);
    int Receive(byte[] buffer, int size);
    void Send(ReadOnlySpan<byte> buffer, int length);
}