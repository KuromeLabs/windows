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
        private ConcurrentBag<Link> _linkBag = new();
        private Device _remoteDevice;

        public Link Get()
        {
            if (_linkBag.TryTake(out var link))
            {
                return link;
            }
            else
            {
                _numOfLinks++;
                return _linkProvider.CreateLink(_remoteDevice.ControlLink);
            }
        }

        public void Return(Link link)
        {
            _linkBag.Add(link);
        }
    }
}