using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DokanNet;
using DokanNet.Logging;
using FlatSharp;
using kurome;

namespace Kurome;

public class KuromeDaemon
{
    private int _numOfConnectedClients;

    public delegate void LinkConnected(string name, string id, Link link);

    public delegate void LinkDisconnected(string id, Link link);

    private readonly ConcurrentDictionary<string, Device> _devices = new();
    private readonly LinkProvider _linkProvider = new();

    public void Start()
    {
        LoadDevices();
        _linkProvider.Listening = true;
        _linkProvider.OnLinkConnected += OnLinkConnected;
        _linkProvider.OnLinkDisconnected += OnLinkDisconnected;
        SslHelper.InitializeSsl();
        _linkProvider.Initialize();
    }


    private void OnLinkConnected(string name, string id, Link link)
    {
        _numOfConnectedClients++;
        Console.WriteLine("Device connected name: " + name + ", id: " + id);

        var driveLetters = Enumerable.Range('C', 'Z' - 'C').Select(i => (char) i + ":")
            .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
        var letter = driveLetters[_numOfConnectedClients - 1][0];
        var device = new Device(link, letter);
        device.Name = name;
        device.Id = id;
        _devices.TryAdd(id, device);
        var rfs = new KuromeOperations(device);
        Dokan.Init();
        Task.Run(() => rfs.Mount(letter + ":\\",
            DokanOptions.FixedDrive, false, new ConsoleLogger("[Kurome] ")));
        Console.WriteLine("Device {0}:{1} has been mounted on {2}:\\ ", name, id, letter.ToString());
    }

    private void LoadDevices()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Kurome"
        );
        var file = Path.Combine(dir, "devices");
        if (File.Exists(file))
        {
            using var fs = File.Open(file, FileMode.Open);
            var length = fs.Length;
            var buffer = new byte[326];
            fs.Read(buffer);
            var devices = DeviceInfoVector.Serializer.Parse(buffer);
            foreach (var d in devices.Vector!)
            {
                Console.WriteLine("Found saved device. Name: {0}, id: {1}", d.Name, d.Id);
                var device = new Device
                {
                    Name = d.Name,
                    Id = d.Id
                };
                _devices.TryAdd(d.Id, device);
            }
        }
        else
        {
            Console.WriteLine("No saved devices found.");
        }
    }

    private void SaveDevices()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Kurome"
        );
        var file = Path.Combine(dir, "devices");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var devices = new DeviceInfoVector();
        foreach (var device in _devices.Values)
        {
            devices.Vector!.Add(new DeviceInfo {Name = device.Name, Id = device.Id});
        }

        var buffer = new byte[DeviceInfoVector.Serializer.GetMaxSize(devices)];
        DeviceInfoVector.Serializer.Write(buffer, devices);
        using var fs = File.Open(file, FileMode.Create);
        fs.Write(buffer);
    }

    private void OnLinkDisconnected(string id, Link link)
    {
        Dokan.Unmount(_devices[id]._driveLetter);
    }
}