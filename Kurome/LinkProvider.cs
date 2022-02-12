using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Kurome
{
    public class LinkProvider : IObservable<Link>
    {
        private readonly TcpListener _tcpListener = TcpListener.Create(33587);
        private readonly string UdpSubnet = "235.132.20.12";
        private readonly int UdpPort = 33586;
        private string _id;

        private readonly List<IObserver<Link>> _observers = new();
        // private readonly Dictionary<string, UdpClient> _udpDictionary = new();


        public void Initialize()
        {
            var addresses = GetLocalIpAddresses();

            StartTcpListener();
            CastUdp(addresses);
            // CastUdpInfo(new IPEndPoint(IPAddress.Parse(UdpSubnet), UdpPort));
        }

        private void CastUdp(IEnumerable<string> addresses)
        {
            _id = IdentityProvider.GetGuid();
            foreach (var ip in addresses)
            {
                var udpClient = new UdpClient(AddressFamily.InterNetwork);
                udpClient.JoinMulticastGroup(IPAddress.Parse(UdpSubnet));
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Parse(ip), UdpPort));
                udpClient.Ttl = 32;
                var message = "kurome:" + ip + ":" + IdentityProvider.GetMachineName() + ":" + _id;
                var data = Encoding.Default.GetBytes(message);
                Console.WriteLine("Broadcast: \"{0}\" to {1}", message, ip);
                udpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(UdpSubnet), UdpPort));
            }
        }

        private async void StartTcpListener()
        {
            _tcpListener.Start();
            while (true)
            {
                var link = new Link(await _tcpListener.AcceptTcpClientAsync());
                OnLinkConnected(link);
            }
        }

        private void OnLinkConnected(Link link)
        {
            foreach (var observer in _observers)
            {
                observer.OnNext(link);
            }
        }
        

        private IEnumerable<string> GetLocalIpAddresses()
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            return (from network in networkInterfaces
                where network.OperationalStatus == OperationalStatus.Up
                select network.GetIPProperties()
                into properties
                from address in properties.UnicastAddresses
                where address.Address.AddressFamily == AddressFamily.InterNetwork
                where !IPAddress.IsLoopback(address.Address)
                select address.Address.ToString()).ToList();
        }

        public IDisposable Subscribe(IObserver<Link> observer)
        {
            if (!_observers.Contains(observer)) _observers.Add(observer);
            return new Unsubscriber<Link>(_observers, observer);
        }
        internal class Unsubscriber<Link> : IDisposable
        {
            private readonly List<IObserver<Link>> _observers;
            private readonly IObserver<Link> _observer;

            public Unsubscriber(List<IObserver<Link>> observers, IObserver<Link> observer)
            {
                _observers = observers;
                _observer = observer;
            }

            public void Dispose()
            {
                if (_observers.Contains(_observer)) _observers.Remove(_observer);
            }
        }
    }
}