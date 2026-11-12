using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace CyberDefender.InputProviders
{
    /// <summary>
    /// Принимает UDP-датаграммы от Python-моста (ai_bridge) в фоновом потоке
    /// и отдаёт их главному потоку через потокобезопасную очередь.
    /// </summary>
    public sealed class UdpLineReceiver : IDisposable
    {
        private readonly UdpClient client;
        private readonly Thread thread;
        private readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();
        private volatile bool running;

        public UdpLineReceiver(int port)
        {
            client = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            running = true;
            thread = new Thread(ReceiveLoop) { IsBackground = true, Name = $"UdpLineReceiver:{port}" };
            thread.Start();
        }

        public bool TryDequeue(out string line) => queue.TryDequeue(out line);

        private void ReceiveLoop()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                try
                {
                    byte[] data = client.Receive(ref remote);
                    queue.Enqueue(Encoding.ASCII.GetString(data).Trim());
                }
                catch (SocketException)
                {
                    if (!running)
                        return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        public void Dispose()
        {
            running = false;
            client.Close();
        }
    }
}
