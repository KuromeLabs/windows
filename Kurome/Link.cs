using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Kurome
{
    public class Link: IDisposable
    {
        private readonly TcpClient _client;

        public Link(TcpClient client)
        {
            _client = client;
        }
        private void WritePrefixed(byte action, string message)
        {
            _client.GetStream().Write(BitConverter.GetBytes(message.Length + 1)
                    .Concat(Encoding.UTF8.GetBytes(message).Prepend(action)).ToArray());
        }
        private byte[] ReadFullPrefixed(int timeout)
        {
            var sizeBuffer = new byte[4];
            var readPrefixTask = _client.GetStream().ReadAsync(sizeBuffer, 0, 4);
            Task.WaitAny(readPrefixTask, Task.Delay(TimeSpan.FromSeconds(timeout)));
            if (!readPrefixTask.IsCompleted)
                throw new TimeoutException();
            var size = BitConverter.ToInt32(sizeBuffer);
            var bytesRead = 0;
            var buffer = new byte[size];
            while (bytesRead != size)
            {
                var readTask = _client.GetStream().Read(buffer, 0 + bytesRead, buffer.Length - bytesRead);
                bytesRead += readTask;
            }

            return buffer;
        }

        public void Dispose()
        {
            _client.Close();
            _client.Dispose();
        }
    }
}