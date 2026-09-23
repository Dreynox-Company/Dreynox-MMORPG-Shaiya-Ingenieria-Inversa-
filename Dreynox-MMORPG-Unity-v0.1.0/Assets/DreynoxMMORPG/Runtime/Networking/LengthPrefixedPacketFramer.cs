using System;
using System.Collections.Generic;

namespace Dreynox.Mmorpg.Networking
{
    public sealed class LengthPrefixedPacketFramer
    {
        private readonly List<byte> _buffer = new List<byte>(8192);
        private readonly int _maxFrameBytes;
        private readonly bool _lengthIncludesHeader;

        public LengthPrefixedPacketFramer(int maxFrameBytes = 65535, bool lengthIncludesHeader = true)
        {
            _maxFrameBytes = Math.Max(64, maxFrameBytes);
            _lengthIncludesHeader = lengthIncludesHeader;
        }

        public IEnumerable<byte[]> Push(byte[] bytes, int count)
        {
            if (bytes == null || count <= 0) yield break;
            count = Math.Min(count, bytes.Length);
            for (int i = 0; i < count; i++) _buffer.Add(bytes[i]);

            while (_buffer.Count >= 2)
            {
                int declared = _buffer[0] | (_buffer[1] << 8);
                int frameBytes = _lengthIncludesHeader ? declared : declared + 2;
                if (frameBytes < 2 || frameBytes > _maxFrameBytes)
                    throw new InvalidOperationException("Longitud TCP inválida: " + frameBytes);
                if (_buffer.Count < frameBytes) yield break;

                int payloadOffset = 2;
                int payloadLength = frameBytes - payloadOffset;
                byte[] payload = new byte[payloadLength];
                _buffer.CopyTo(payloadOffset, payload, 0, payloadLength);
                _buffer.RemoveRange(0, frameBytes);
                yield return payload;
            }
        }

        public static byte[] Frame(byte[] payload, bool lengthIncludesHeader = true)
        {
            payload = payload ?? Array.Empty<byte>();
            int declared = payload.Length + (lengthIncludesHeader ? 2 : 0);
            if (declared > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(payload));
            byte[] result = new byte[payload.Length + 2];
            result[0] = (byte)(declared & 0xFF);
            result[1] = (byte)((declared >> 8) & 0xFF);
            Buffer.BlockCopy(payload, 0, result, 2, payload.Length);
            return result;
        }
    }
}
