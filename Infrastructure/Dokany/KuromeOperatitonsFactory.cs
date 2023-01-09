using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Dokany;

public class KuromeOperatitonsFactory : IKuromeOperationsFactory
{
    private readonly IServiceProvider _serviceProvider;

    public KuromeOperatitonsFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IKuromeOperations Create(IDeviceAccessor deviceAccessor, string mountLetter)
    {
        return ActivatorUtilities.CreateInstance<KuromeOperations>(_serviceProvider, deviceAccessor, mountLetter);
    }
}