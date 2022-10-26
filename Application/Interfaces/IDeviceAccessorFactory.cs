using Domain;

namespace Application.Interfaces;

public interface IDeviceAccessorFactory
{
    public IDeviceAccessor Create(ILink link, Device device);
}