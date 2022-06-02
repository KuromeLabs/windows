using Microsoft.Extensions.Hosting;

namespace Application.Interfaces;

public interface ISecurityService<T> : IHostedService
{
    public T GetSecurityContext();
}