using System;
using System.IO;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Spk;
using NUnit.Framework;

namespace Dreynox.Mmorpg.Tests
{
    public sealed class SpkV3HeaderParserTests
    {
        [Test]
        public void ParsesObservedHeaderLayout()
        {
            byte[] bytes = Hex("4cd37b9e00000300fd219ec70000000045462300000000008072490000000000dcc300000000040060d953597e6f0c1549fc6d2a36c52432fb0e93496921dfcd6ab2492ee6128bf3e38d999e793d3eb6c2073787ec37a776f30376c3a858f89cc23214685dd19ac700000000851a000000000000000000000000000000000000");
            using (MemoryStream stream = new MemoryStream(bytes))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                SpkV3Header h = SpkV3HeaderParser.Parse(reader);
                Assert.AreEqual(196608u, h.version);
                Assert.AreEqual(3349029373ul, h.indexOffset);
                Assert.AreEqual(2311749ul, h.indexStoredBytes);
                Assert.AreEqual(4813440ul, h.indexDecodedBytes);
                Assert.AreEqual(50140u, h.recordCount);
                Assert.AreEqual(262144u, h.blockBytes);
                Assert.AreEqual(3348812125ul, h.auxiliaryOffset);
                Assert.AreEqual(6789u, h.auxiliaryCount);
                Assert.IsTrue(h.LooksObservedV3);
            }
        }

        private static byte[] Hex(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
    }
}
