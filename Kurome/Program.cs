using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DokanNet;
using DokanNet.Logging;

namespace Kurome
{
    class Program
    {
        
        static LinkProvider linkProvider = LinkProvider.Instance;
        static void Main(string[] args)
        {
            linkProvider.StartListening();
            HandleLink(CancellationToken.None);
            Console.WriteLine("TCP Listening started");
            linkProvider.StartCasting(TimeSpan.FromSeconds(1), CancellationToken.None);
            Console.WriteLine("UDP casting started.");
            Console.Read();
        }

        static async void HandleLink(CancellationToken token)
        {
            var numOfConnectedClients = 0;
            while (!token.IsCancellationRequested)
            {
                var controlLink = await linkProvider.GetIncomingLink();
                Console.WriteLine("Device has connected.");
                numOfConnectedClients++;
                var list = Enumerable.Range('C', 'Z' - 'C').Select(i => (char)i + ":")
                    .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
                var letter = list[numOfConnectedClients - 1][0];
                var device = new Device(controlLink, letter);
                var rfs = new KuromeOperations(device);
                _ = Task.Run(() => rfs.Mount(letter + ":\\",
                    DokanOptions.FixedDrive |
                    DokanOptions.EnableFCBGC |
                    DokanOptions.MountManager, 8, new NullLogger()), token);
                Console.WriteLine("Device {0}:{1} has been mounted on {2}:\\ ", device.GetDeviceName(), device.GetDeviceId(), letter);

            }
        }

        
    }
}