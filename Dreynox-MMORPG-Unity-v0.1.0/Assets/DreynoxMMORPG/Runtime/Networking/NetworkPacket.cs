using System;

namespace Dreynox.Mmorpg.Networking
{
    public enum NetworkChannel { Tcp, Udp }

    public readonly struct NetworkPacket
    {
        public NetworkPacket(NetworkChannel channel, byte[] payload, DateTime receivedUtc)
        {
            Channel = channel;
            Payload = payload ?? Array.Empty<byte>();
            ReceivedUtc = receivedUtc;
        }
        public NetworkChannel Channel { get; }
        public byte[] Payload { get; }
        public DateTime ReceivedUtc { get; }
    }

    public interface IPacketTransform
    {
        byte[] Encode(byte[] payload);
        byte[] Decode(byte[] payload);
    }

    public sealed class IdentityPacketTransform : IPacketTransform
    {
        public byte[] Encode(byte[] payload) { return payload ?? Array.Empty<byte>(); }
        public byte[] Decode(byte[] payload) { return payload ?? Array.Empty<byte>(); }
    }
}
