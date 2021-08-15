using System.Net.Sockets;

namespace Kurome
{
    public class LinkProvider
    {
        private readonly object _lock = new();
        public Link CreateLink(Link controlLink)
        {
            lock (_lock)
            {
                var listener = TcpListener.Create(33588);
                listener.Start();
                controlLink.WritePrefixed(Packets.ActionCreateNewLink, "");
                var client = listener.AcceptTcpClient();
                listener.Stop();
                return new Link(client);
            }
        }
    }
}