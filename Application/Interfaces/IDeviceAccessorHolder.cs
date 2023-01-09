namespace Application.Interfaces;

public interface IDeviceAccessorHolder
{
    public void Add(string id, IDeviceAccessor deviceAccessor);
    public void Remove(string id);
    public IDeviceAccessor? Get(string id);
}