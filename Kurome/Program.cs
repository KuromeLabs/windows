using System;
using System.Threading;

namespace Kurome
{
    public class Program
    {
        static void Main(string[] args)
        {
            var mutex = new Mutex(false, "Global\\kurome-mutex");
            if (!mutex.WaitOne(0, false))
            {
                Console.Write("Kurome is already running.\nPress any key to exit...");
                Console.ReadKey();
                return;
            }

            var daemon = new KuromeDaemon();
            daemon.Start();

            var handle = new ManualResetEventSlim(false);
            handle.Wait();
        }
    }
}