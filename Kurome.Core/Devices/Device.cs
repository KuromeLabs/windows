namespace Kurome.Core.Devices;

public class Device
{

    public Device(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Device()
    {
    }

    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}