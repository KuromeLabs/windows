namespace Kurome.Core.Interfaces;

public interface IIdentityProvider
{
    public string GetEnvironmentName();
    public string GetEnvironmentId();
}