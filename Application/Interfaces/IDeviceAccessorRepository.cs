namespace Application.Interfaces;

public interface IDeviceAccessorRepository
{
    public void Add(string id, IDeviceAccessor deviceAccessor);
    public void Remove(string id);
    public IDeviceAccessor Get(string id);
}