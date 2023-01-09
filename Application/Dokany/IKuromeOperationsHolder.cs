using Application.Interfaces;
using DokanNet;

namespace Application.Dokany;

public interface IKuromeOperationsHolder
{
    public void Add(string id, IKuromeOperations kuromeOperations, DokanInstance dokanInstance);
    public Dokan GetDokan();
}