using System.Security.Cryptography.X509Certificates;

namespace Kurome.Core.Devices;

public class Device
{

    public Device(Guid id, string name, X509Certificate2 certificate)
    {
        Id = id;
        Name = name;
        Certificate = certificate;
    }

    public Device()
    {
    }

    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public X509Certificate2? Certificate { get; set; }
}