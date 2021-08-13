using System.Collections.Concurrent;

namespace Kurome
{
    public class LinkPool
    {
        public LinkPool(Device device)
        {
            _remoteDevice = device;
        }
        private LinkProvider _linkProvider = new();
        private ConcurrentQueue<Link> _linkQueue = new();
        private Device _remoteDevice;

        public Link Get()
        {
            return _linkQueue.TryDequeue(out var client) ? client : _linkProvider.CreateLink(_remoteDevice.ControlLink);
        }

        public void Return(Link link)
        {
            _linkQueue.Enqueue(link);
        }
        
       
    }
}