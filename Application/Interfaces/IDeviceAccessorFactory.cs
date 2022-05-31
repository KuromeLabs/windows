using Domain;

namespace Application.Interfaces;

public interface IDeviceAccessorFactory
{
    public void Register(string id, IDeviceAccessor deviceAccessor);
    public void Unregister(string id);
    public IDeviceAccessor? Get(string id);
    public IDeviceAccessor Create(ILink link, Device device);
    public void Mount(Guid id);
}