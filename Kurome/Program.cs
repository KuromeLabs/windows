using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DokanNet;
using DokanNet.Logging;
using FlatBuffers;
using kurome;

namespace Kurome
{
    class Program
    {
        static LinkProvider linkProvider = LinkProvider.Instance;

        static async Task<int> Main(string[] args)
        {
            var mutex = new Mutex(false, "Global\\kurome-mutex");
            if (!mutex.WaitOne(0, false))
            {
                Console.Write("Kurome is already running.\nPress any key to exit...");
                Console.ReadKey();
                return 0;
            }

            SslHelper.InitializeSsl();

            linkProvider.StartListening();

            Console.WriteLine("TCP Listening started.");
            linkProvider.InitializeUdpListener();
            var address = IPAddress.Parse("235.132.20.12");
            var ipEndPoint = new IPEndPoint(address, 33586);
            linkProvider.CastUdpInfo(address, ipEndPoint);
            
            Console.WriteLine("UDP casting started.");
            while (true)
            {
                var controlLink = await linkProvider.GetIncomingLink();
                HandleLink(CancellationToken.None, controlLink);
            }
        }

        static void HandleLink(CancellationToken token, Link controlLink)
        {
            var numOfConnectedClients = 0;
            var result = controlLink.GetPacket();
            if (result.DeviceInfo == null) return;
            var name = result.DeviceInfo.Value.Name;
            var id = result.DeviceInfo.Value.Id;
            Console.WriteLine("Device name: " + name + ", id: " + id);
            if (result.Action == ActionType.ActionConnect)
            {
                Console.WriteLine("Device has connected.");
                numOfConnectedClients++;
                var list = Enumerable.Range('C', 'Z' - 'C').Select(i => (char)i + ":")
                    .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
                var letter = list[numOfConnectedClients - 1][0];
                var device = new Device(controlLink, letter);
                device.Name = name;
                device.Id = id;
                var rfs = new KuromeOperations(device);
                // controlLink.WritePrefixed(Packets.ResultActionSuccess);
                Dokan.Init();
                _ = Task.Run(() => rfs.Mount(letter + ":\\",
                    DokanOptions.FixedDrive , false, new ConsoleLogger("[Kurome] ")));
                Console.WriteLine("Device {0}:{1} has been mounted on {2}:\\ ", name, id, letter.ToString());
            }
            else if (result.Action == ActionType.ActionPair)
            {
                Console.WriteLine("Device {0} wants to Pair. \nRemote ID: {1} \nThis PC's ID: {2}", name, id,
                    IdentityProvider.GetGuid());
                Console.Write("Do you trust it? (y/n): ");
                var input = Console.ReadKey().KeyChar;
                while (input != 'y' && input != 'n')
                {
                    Console.WriteLine("\nWrong input");
                    input = Console.ReadKey().KeyChar;
                }

                if (input == 'y')
                {
                    var builder = new FlatBufferBuilder(4);
                    var packet = Packet.CreatePacket(builder, result: ResultType.ResultActionSuccess);
                    builder.FinishSizePrefixed(packet.Value);
                    controlLink.SendBuffer(builder.DataBuffer);
                    builder.Clear();
                    Console.WriteLine("\nPairing accepted.");
                }
            }
        }
    }
}