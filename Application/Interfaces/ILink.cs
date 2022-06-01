namespace Application.Interfaces;

public interface ILink : IDisposable
{
    Task<int> ReceiveAsync(byte[] buffer, int size, CancellationToken cancellationToken);
    void SendAsync(ReadOnlySpan<byte> buffer, int length);
}