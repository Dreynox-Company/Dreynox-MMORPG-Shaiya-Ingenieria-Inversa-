using System;
using UnityEngine;

namespace Dreynox.Mmorpg.Networking
{
    public sealed class NetworkPump : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxPacketsPerFrame = 1024;
        private NetworkTransportWorker _worker;
        public event Action<NetworkPacket> PacketReceived;
        public NetworkTransportWorker Worker => _worker;

        public void Connect(string host, int tcpPort, int udpPort)
        {
            Disconnect();
            _worker = new NetworkTransportWorker();
            _worker.StartAsync(host, tcpPort, udpPort);
        }

        public void Disconnect()
        {
            if (_worker == null) return;
            _worker.Dispose();
            _worker = null;
        }

        private void Update()
        {
            if (_worker == null) return;
            int count = 0;
            while (count < maxPacketsPerFrame && _worker.TryDequeue(out NetworkPacket packet))
            {
                PacketReceived?.Invoke(packet);
                count++;
            }
            if (_worker.LastError != null)
            {
                Debug.LogError("Network worker: " + _worker.LastError);
                Disconnect();
            }
        }

        private void OnDestroy() => Disconnect();
    }
}
