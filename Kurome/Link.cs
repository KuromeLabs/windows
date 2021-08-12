using System.Collections.Generic;
using System.Net.Sockets;

namespace Kurome
{
    public class Link
    {
        private readonly object _queueLock = new();
        private Queue<TcpClient> _streamQueue = new();

        public TcpClient Get()
        {
            lock (_queueLock)
                return _streamQueue.Dequeue();
        }

        public void Return(TcpClient client)
        {
            lock (_queueLock)
                _streamQueue.Enqueue(client);
        }
    }
}