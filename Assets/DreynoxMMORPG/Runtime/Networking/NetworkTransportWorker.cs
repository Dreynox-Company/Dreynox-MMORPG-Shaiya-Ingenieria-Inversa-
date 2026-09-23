using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Dreynox.Mmorpg.Networking
{
    public sealed class NetworkTransportWorker : IDisposable
    {
        private readonly ConcurrentQueue<NetworkPacket> _incoming = new ConcurrentQueue<NetworkPacket>();
        private readonly ConcurrentQueue<byte[]> _tcpOutgoing = new ConcurrentQueue<byte[]>();
        private readonly SemaphoreSlim _tcpSignal = new SemaphoreSlim(0);
        private readonly LengthPrefixedPacketFramer _framer = new LengthPrefixedPacketFramer();
        private readonly IPacketTransform _transform;
        private CancellationTokenSource _cts;
        private TcpClient _tcp;
        private UdpClient _udp;
        private Task _worker;

        public NetworkTransportWorker(IPacketTransform transform = null)
        {
            _transform = transform ?? new IdentityPacketTransform();
        }

        public bool IsRunning => _worker != null && !_worker.IsCompleted;
        public Exception LastError { get; private set; }

        public Task StartAsync(string host, int tcpPort, int udpPort)
        {
            if (IsRunning) throw new InvalidOperationException("Network worker ya iniciado.");
            _cts = new CancellationTokenSource();
            _worker = Task.Run(() => RunAsync(host, tcpPort, udpPort, _cts.Token), _cts.Token);
            return Task.CompletedTask;
        }

        public bool TryDequeue(out NetworkPacket packet) => _incoming.TryDequeue(out packet);

        public void SendTcp(byte[] payload)
        {
            byte[] encoded = _transform.Encode(payload ?? Array.Empty<byte>());
            _tcpOutgoing.Enqueue(LengthPrefixedPacketFramer.Frame(encoded));
            _tcpSignal.Release();
        }

        public async Task StopAsync()
        {
            if (_cts == null) return;
            _cts.Cancel();
            try { _tcp?.Close(); } catch { }
            try { _udp?.Close(); } catch { }
            _tcpSignal.Release();
            if (_worker != null)
            {
                try { await _worker.ConfigureAwait(false); } catch { }
            }
            _worker = null;
            _cts.Dispose();
            _cts = null;
        }

        private async Task RunAsync(string host, int tcpPort, int udpPort, CancellationToken token)
        {
            try
            {
                _tcp = new TcpClient { NoDelay = true };
                await _tcp.ConnectAsync(host, tcpPort).ConfigureAwait(false);
                _udp = new UdpClient(udpPort);
                Task read = TcpReadLoopAsync(token);
                Task write = TcpWriteLoopAsync(token);
                Task udp = UdpReadLoopAsync(token);
                await Task.WhenAll(read, write, udp).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) when (token.IsCancellationRequested) { }
            catch (Exception ex) { LastError = ex; }
        }

        private async Task TcpReadLoopAsync(CancellationToken token)
        {
            NetworkStream stream = _tcp.GetStream();
            byte[] buffer = new byte[32768];
            while (!token.IsCancellationRequested)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if (read == 0) throw new SocketException((int)SocketError.ConnectionReset);
                foreach (byte[] frame in _framer.Push(buffer, read))
                    _incoming.Enqueue(new NetworkPacket(NetworkChannel.Tcp, _transform.Decode(frame), DateTime.UtcNow));
            }
        }

        private async Task TcpWriteLoopAsync(CancellationToken token)
        {
            NetworkStream stream = _tcp.GetStream();
            while (!token.IsCancellationRequested)
            {
                await _tcpSignal.WaitAsync(token).ConfigureAwait(false);
                while (_tcpOutgoing.TryDequeue(out byte[] frame))
                    await stream.WriteAsync(frame, 0, frame.Length, token).ConfigureAwait(false);
            }
        }

        private async Task UdpReadLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult result = await _udp.ReceiveAsync().ConfigureAwait(false);
                _incoming.Enqueue(new NetworkPacket(NetworkChannel.Udp, _transform.Decode(result.Buffer), DateTime.UtcNow));
            }
        }

        public void Dispose()
        {
            try { StopAsync().GetAwaiter().GetResult(); } catch { }
            _tcpSignal.Dispose();
        }
    }
}
