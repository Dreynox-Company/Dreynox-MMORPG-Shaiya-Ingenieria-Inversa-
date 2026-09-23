using System;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Spk;
using NUnit.Framework;

namespace Dreynox.Mmorpg.Tests
{
    public sealed class AesGcmManagedTests
    {
        [Test]
        public void DecryptsNistZeroBlockVector()
        {
            byte[] key = Hex("00000000000000000000000000000000");
            byte[] nonce = Hex("000000000000000000000000");
            byte[] cipher = Hex("0388dace60b6a392f328c2b971b2fe78");
            byte[] tag = Hex("ab6e47d42cec13bdf53a67b21257bddf");
            byte[] plain = AesGcmManaged.Decrypt(key, nonce, cipher, tag);
            CollectionAssert.AreEqual(new byte[16], plain);
        }

        [Test]
        public void RejectsInvalidTag()
        {
            byte[] key = new byte[16];
            byte[] nonce = new byte[12];
            byte[] tag = new byte[16];
            Assert.Throws<System.Security.Cryptography.CryptographicException>(() => AesGcmManaged.Decrypt(key, nonce, new byte[16], tag));
        }

        private static byte[] Hex(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
    }
}
