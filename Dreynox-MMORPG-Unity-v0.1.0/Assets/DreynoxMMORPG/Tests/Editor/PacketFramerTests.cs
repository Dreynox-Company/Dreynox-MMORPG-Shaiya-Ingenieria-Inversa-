using System.Linq;
using Dreynox.Mmorpg.Networking;
using NUnit.Framework;

namespace Dreynox.Mmorpg.Tests
{
    public sealed class PacketFramerTests
    {
        [Test]
        public void ReassemblesSplitTcpFrame()
        {
            byte[] frame = LengthPrefixedPacketFramer.Frame(new byte[] { 1, 2, 3, 4 });
            LengthPrefixedPacketFramer parser = new LengthPrefixedPacketFramer();
            Assert.AreEqual(0, parser.Push(frame, 2).Count());
            byte[] tail = frame.Skip(2).ToArray();
            byte[][] decoded = parser.Push(tail, tail.Length).ToArray();
            Assert.AreEqual(1, decoded.Length);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, decoded[0]);
        }
    }
}
