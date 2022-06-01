namespace Application.Interfaces;

public interface IIdentityProvider
{
    public string GetEnvironmentName();
    public string GetEnvironmentId();
}