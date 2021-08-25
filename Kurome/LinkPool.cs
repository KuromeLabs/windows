using System;
using System.Collections.Concurrent;

namespace Kurome
{
    public class LinkPool
    {
        private int _numOfLinks = 0;
        private readonly LinkProvider _linkProvider = LinkProvider.Instance;
        public LinkPool(Device device)
        {
            _remoteDevice = device;
        }
        private ConcurrentQueue<Link> _linkQueue = new();
        private Device _remoteDevice;

        public Link Get()
        {
            if (_linkQueue.TryDequeue(out var client))
            {
                Console.WriteLine($"LinkPool is returning an existing Link. Total: {_numOfLinks}");
                return client;
            }
            else
            {
                _numOfLinks++;
                Console.WriteLine($"LinkPool is creating a new Link. Total: {_numOfLinks}");
                return _linkProvider.CreateLink(_remoteDevice.ControlLink);
            }
        }

        public void Return(Link link)
        {
            _linkQueue.Enqueue(link);
        }
    }
}