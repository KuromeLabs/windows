namespace Application.Interfaces;

public interface ILinkProvider
{
    public Task<ILink> CreateLinkAsync(string connectionInfo, CancellationToken cancellationToken);
}