using System.Collections.Concurrent;

namespace Kurome
{
    public class LinkPool
    {
        private ConcurrentBag<Link> _linkBag = new();
        private int _numOfLinks = 0;

        private Device _remoteDevice;

        // private readonly LinkProvider _linkProvider = LinkProvider.Instance;
        public LinkPool(Device device)
        {
            _remoteDevice = device;
        }

        // public Link Get()
        // {
        //     if (_linkBag.TryTake(out var link))
        //     {
        //         return link;
        //     }
        //     else
        //     {
        //         _numOfLinks++;
        //         return _linkProvider.CreateLink(_remoteDevice.Link);
        //         Console.WriteLine("LinkPool is creating a new Link. Number of links: " + _numOfLinks);
        //         return null;
        //     }
        //     
        // }

        public void Return(Link link)
        {
            _linkBag.Add(link);
        }
    }
}