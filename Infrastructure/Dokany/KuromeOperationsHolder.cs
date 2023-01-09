using System.Collections.Concurrent;
using Application.Dokany;
using Application.Interfaces;
using DokanNet;

namespace Infrastructure.Dokany;

public class KuromeOperationsHolder : IKuromeOperationsHolder
{


    private readonly Dokan _dokan;
    private readonly ConcurrentDictionary<string, (IKuromeOperations, DokanInstance)> _kuromeOperations = new();
    public KuromeOperationsHolder()
    {
        _dokan = new Dokan(null);
    }


    public void Add(string id, IKuromeOperations kuromeOperations, DokanInstance dokanInstance)
    {
        _kuromeOperations.TryAdd(id, (kuromeOperations, dokanInstance));
    }

    public Dokan GetDokan()
    {
        return _dokan;
    }
    
}