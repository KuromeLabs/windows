using System;
using System.Net.Sockets;

namespace Kurome
{
    public sealed class LinkProvider
    {
        private static readonly Lazy<LinkProvider> Lazy = new(() => new LinkProvider());    
        public static LinkProvider Instance => Lazy.Value;
        private readonly object _lock = new();
        public Link CreateLink(Link controlLink)
        {
            lock (_lock)
            {
                var listener = TcpListener.Create(33588);
                listener.Start();
                controlLink.WritePrefixed(Packets.ActionCreateNewLink);
                var client = listener.AcceptTcpClient();
                listener.Stop();
                return new Link(client);
            }
        }
    }
}