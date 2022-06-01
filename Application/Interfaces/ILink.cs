namespace Application.Interfaces;

public interface ILink : IDisposable
{
    Task<byte[]?> ReceiveAsync(CancellationToken cancellationToken);
    void SendAsync(ReadOnlySpan<byte> buffer, int length);
}