using Microsoft.Extensions.Hosting;

namespace Kurome.Core.Interfaces;

public interface ISecurityService<T> : IHostedService
{
    public T GetSecurityContext();
}