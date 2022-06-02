namespace Application.Interfaces;

public interface ILinkProvider<in T>
{
    public Task<ILink> CreateClientLinkAsync(string connectionInfo, CancellationToken cancellationToken);
    public Task<ILink> CreateServerLinkAsync(T client, CancellationToken cancellationToken);
}