using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DokanNet;
using DokanNet.Logging;
using FlatBuffers;
using kurome;

namespace Kurome;

public class KuromeDaemon
{
    private int _numOfConnectedClients;

    public delegate void LinkConnected(string name, string id, Link link);
    public delegate void LinkDisconnected(string id, Link link);

    private readonly ConcurrentDictionary<string, Device> _activeDevices = new();
    private readonly LinkProvider _linkProvider = new();

    public void Start()
    {
        _linkProvider.Listening = true;
        _linkProvider.OnLinkConnected += OnLinkConnected;
        _linkProvider.OnLinkDisconnected += OnLinkDisconnected;
        SslHelper.InitializeSsl();
        _linkProvider.Initialize();
    }


    private void OnLinkConnected(string name, string id, Link link)
    {
        _numOfConnectedClients++;
        var packetCompletionSource = new TaskCompletionSource<Packet>();
        link.AddCompletionSource(0, packetCompletionSource);
        Console.WriteLine("Device connected name: " + name + ", id: " + id);

        var driveLetters = Enumerable.Range('C', 'Z' - 'C').Select(i => (char) i + ":")
            .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
        var letter = driveLetters[_numOfConnectedClients - 1][0];
        var device = new Device(link, letter);
        device.Name = name;
        device.Id = id;
        _activeDevices.TryAdd(id, device);
        var rfs = new KuromeOperations(device);
        Dokan.Init();
        Task.Run(() => rfs.Mount(letter + ":\\",
            DokanOptions.FixedDrive, false, new ConsoleLogger("[Kurome] ")));
        Console.WriteLine("Device {0}:{1} has been mounted on {2}:\\ ", name, id, letter.ToString());
    }

    private void OnLinkDisconnected(string id, Link link)
    {
        Dokan.Unmount(_activeDevices[id]._driveLetter);
    }
}