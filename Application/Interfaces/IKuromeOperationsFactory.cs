namespace Application.Interfaces;

public interface IKuromeOperationsFactory
{
    public IKuromeOperations Create(IDeviceAccessor deviceAccessor, string mountLetter);
}