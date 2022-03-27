using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DefaultNamespace;
using DokanNet;
using DokanNet.Logging;
using FlatSharp;
using kurome;
using Action = kurome.Action;

namespace Kurome;

public class KuromeDaemon
{
    private int _numOfConnectedClients;

    public delegate void LinkConnected(Link link);

    public delegate void LinkDisconnected(Link link);

    private readonly ConcurrentDictionary<string, Device> _devices = new();
    private readonly LinkProvider _linkProvider = new();

    public void Start()
    {
        SslHelper.InitializeSsl();
        LoadDevices();
        _linkProvider.Listening = true;
        _linkProvider.OnLinkConnected += OnLinkConnected;
        _linkProvider.OnLinkDisconnected += OnLinkDisconnected;
        _linkProvider.Initialize();
    }


    private void OnLinkConnected(Link link)
    {
        _numOfConnectedClients++;
        Console.WriteLine("Device connected name: " + link.DeviceName + ", id: " + link.DeviceId);

        var id = link.DeviceId;
        Device device;
        if (_devices.ContainsKey(id))
        {
            device = _devices[id];
        }
        else
            device = new Device();
        device.SetLink(link);


        device.Name = link.DeviceName;
        device.Id = link.DeviceId;
        device.OnPairStatus += OnPairStatus;
        if (device.IsPaired)
            MountDevice(device);
        
        
    }

    private void MountDevice(Device device)
    {
        var driveLetters = Enumerable.Range('C', 'Z' - 'C').Select(i => (char) i + ":")
            .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
        var letter = driveLetters[_numOfConnectedClients - 1][0];
        device.DriveLetter = letter;
        _devices.TryAdd(device.Id, device);
        var rfs = new KuromeOperations(device);
        Dokan.Init();
        Task.Run(() => rfs.Mount(letter + ":\\",
            DokanOptions.FixedDrive, false, new ConsoleLogger("[Kurome] ")));
        
        Console.WriteLine("Device {0}:{1} has been mounted on {2}:\\ ", device.Name, device.Id, letter.ToString());
        
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
                    Id = d.Id,
                    IsPaired = true
                };
                device.OnPairStatus += OnPairStatus;
                _devices.TryAdd(d.Id, device);
            }
        }
        else
        {
            Console.WriteLine("No saved devices found.");
        }
    }

    private void OnPairStatus(PairingHandler.PairStatus status, Device device)
    {
        switch (status)
        {
            case PairingHandler.PairStatus.Paired:
                Console.WriteLine("KuromeDaemon: Device paired");
                _devices[device.Id] = device;
                SaveDevices();
                MountDevice(device);
                break;
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
        devices.Vector = new List<DeviceInfo>();
        foreach (var device in _devices.Values)
        {
            Console.WriteLine("Saving device. Name: {0}, id: {1}", device.Name, device.Id);
            var info = new DeviceInfo
            {
                Name = device.Name,
                Id = device.Id,
                TotalBytes = 0,
                FreeBytes = 0
            };
            devices.Vector.Add(info);
        }

        var buffer = new byte[DeviceInfoVector.Serializer.GetMaxSize(devices)];
        DeviceInfoVector.Serializer.Write(buffer, devices);
        using var fs = File.Open(file, FileMode.Create);
        fs.Write(buffer);
    }

    private void OnLinkDisconnected(Link link)
    {
        Dokan.Unmount(_devices[link.DeviceId].DriveLetter);
        _numOfConnectedClients--;
    }
}