using System.Buffers.Binary;
using System.Text;
using Domain;
using Tenray.ZoneTree.Serializers;

namespace Persistence;

public class DeviceSerializer : ISerializer<Device>
{
    public Device Deserialize(byte[] bytes)
    {
        var i = IndexedRead(bytes, 0, out var name);
        IndexedRead(bytes, i, out var id);
        return new Device
        {
            Id = Guid.Parse(id),
            Name = name
        };
    }

    public byte[] Serialize(in Device entry)
    {
        var name = Encoding.UTF8.GetBytes(entry.Name);
        var id = Encoding.UTF8.GetBytes(entry.Id.ToString());
        var buffer = new byte[name.Length + id.Length + 8];
        var i = IndexedWrite(buffer, name, 0);
        IndexedWrite(buffer, id, i);
        return buffer;
    }

    private int IndexedWrite(byte[] buffer, byte[] data, int i)
    {
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan()[i..(i + 4)], data.Length);
        data.AsSpan().CopyTo(buffer.AsSpan()[(4 + i)..]);
        return 4 + i + data.Length;
    }

    private int IndexedRead(byte[] buffer, int i, out string s)
    {
        var size = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan()[i..(i + 4)]);
        s = Encoding.UTF8.GetString(buffer.AsSpan()[(i + 4)..(i + 4 + size)]);
        return i + 4 + size;
    }
}