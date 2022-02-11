﻿using System;
using System.IO;
using System.Linq;
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
        private static LinkProvider linkProvider = new();

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
            linkProvider.Initialize();
            
            while (true)
            {
                var link = await linkProvider.GetIncomingLink();
                HandleLink(link);
            }
        }

        static void HandleLink(Link controlLink)
        {
            var numOfConnectedClients = 0;
            var packetCompletionSource = new TaskCompletionSource<Packet>();
            controlLink.AddCompletionSource(0, packetCompletionSource);
            Task.Run(controlLink.StartListeningAsync);
            var result = packetCompletionSource.Task.Result;
            if (result.DeviceInfo == null) return;
            var name = result.DeviceInfo.Value.Name;
            var id = result.DeviceInfo.Value.Id;
            Console.WriteLine("Device name: " + name + ", id: " + id);

            if (result.Action == ActionType.ActionConnect)
            {
                Console.WriteLine("Device has connected.");
                numOfConnectedClients++;
                var list = Enumerable.Range('C', 'Z' - 'C').Select(i => (char) i + ":")
                    .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", ""))).ToList();
                var letter = list[numOfConnectedClients - 1][0];
                var device = new Device(controlLink, letter);
                device.Name = name;
                device.Id = id;

                var rfs = new KuromeOperations(device);
                // controlLink.WritePrefixed(Packets.ResultActionSuccess);
                Dokan.Init();
                _ = Task.Run(() => rfs.Mount(letter + ":\\",
                    DokanOptions.FixedDrive, false, new ConsoleLogger("[Kurome] ")));
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