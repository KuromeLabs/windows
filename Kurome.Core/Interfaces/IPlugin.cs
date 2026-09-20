namespace Kurome.Core;

public interface IPlugin: IDisposable
{
    public void Subscribe();
    
    public void Start();
}
