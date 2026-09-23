using System;
using System.Security.Cryptography;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering.Spk
{
    public static class AesGcmManaged
    {
        public static byte[] Decrypt(byte[] key, byte[] nonce, byte[] ciphertext, byte[] tag, byte[] aad = null)
        {
            if (key == null || (key.Length != 16 && key.Length != 24 && key.Length != 32)) throw new ArgumentException("Clave AES inválida.", nameof(key));
            if (nonce == null || nonce.Length == 0) throw new ArgumentException("Nonce vacío.", nameof(nonce));
            if (ciphertext == null) ciphertext = Array.Empty<byte>();
            if (tag == null || tag.Length < 12 || tag.Length > 16) throw new ArgumentException("Tag GCM inválido.", nameof(tag));
            if (aad == null) aad = Array.Empty<byte>();

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    byte[] h = EncryptBlock(encryptor, new byte[16]);
                    byte[] j0 = BuildJ0(h, nonce);
                    byte[] counter = (byte[])j0.Clone();
                    IncrementCounter32(counter);
                    byte[] plaintext = Gctr(encryptor, counter, ciphertext);

                    byte[] s = Ghash(h, aad, ciphertext);
                    byte[] calculatedTagBlock = Gctr(encryptor, j0, s);
                    if (!FixedTimeEqualsPrefix(calculatedTagBlock, tag))
                        throw new CryptographicException("Autenticación AES-GCM fallida: el tag no coincide.");
                    return plaintext;
                }
            }
        }

        private static byte[] BuildJ0(byte[] h, byte[] nonce)
        {
            if (nonce.Length == 12)
            {
                byte[] j0 = new byte[16];
                Buffer.BlockCopy(nonce, 0, j0, 0, 12);
                j0[15] = 1;
                return j0;
            }

            int padded = ((nonce.Length + 15) / 16) * 16;
            byte[] data = new byte[padded + 16];
            Buffer.BlockCopy(nonce, 0, data, 0, nonce.Length);
            WriteUInt64BigEndian(data, data.Length - 8, checked((ulong)nonce.Length * 8UL));
            return GhashBlocks(h, data);
        }

        private static byte[] Ghash(byte[] h, byte[] aad, byte[] cipher)
        {
            int aadPadded = ((aad.Length + 15) / 16) * 16;
            int cipherPadded = ((cipher.Length + 15) / 16) * 16;
            byte[] data = new byte[aadPadded + cipherPadded + 16];
            Buffer.BlockCopy(aad, 0, data, 0, aad.Length);
            Buffer.BlockCopy(cipher, 0, data, aadPadded, cipher.Length);
            int lenOffset = aadPadded + cipherPadded;
            WriteUInt64BigEndian(data, lenOffset, checked((ulong)aad.Length * 8UL));
            WriteUInt64BigEndian(data, lenOffset + 8, checked((ulong)cipher.Length * 8UL));
            return GhashBlocks(h, data);
        }

        private static byte[] GhashBlocks(byte[] h, byte[] blocks)
        {
            byte[] y = new byte[16];
            byte[] block = new byte[16];
            for (int offset = 0; offset < blocks.Length; offset += 16)
            {
                Array.Clear(block, 0, block.Length);
                int copy = Math.Min(16, blocks.Length - offset);
                Buffer.BlockCopy(blocks, offset, block, 0, copy);
                XorInPlace(y, block);
                y = MultiplyGf128(y, h);
            }
            return y;
        }

        private static byte[] MultiplyGf128(byte[] x, byte[] y)
        {
            byte[] z = new byte[16];
            byte[] v = (byte[])y.Clone();
            for (int i = 0; i < 128; i++)
            {
                int byteIndex = i / 8;
                int bitIndex = 7 - (i % 8);
                if (((x[byteIndex] >> bitIndex) & 1) != 0) XorInPlace(z, v);
                bool lsb = (v[15] & 1) != 0;
                ShiftRightOne(v);
                if (lsb) v[0] ^= 0xE1;
            }
            return z;
        }

        private static byte[] Gctr(ICryptoTransform encryptor, byte[] initialCounterBlock, byte[] input)
        {
            if (input.Length == 0) return Array.Empty<byte>();
            byte[] counter = (byte[])initialCounterBlock.Clone();
            byte[] output = new byte[input.Length];
            for (int offset = 0; offset < input.Length; offset += 16)
            {
                byte[] stream = EncryptBlock(encryptor, counter);
                int count = Math.Min(16, input.Length - offset);
                for (int i = 0; i < count; i++) output[offset + i] = (byte)(input[offset + i] ^ stream[i]);
                IncrementCounter32(counter);
            }
            return output;
        }

        private static byte[] EncryptBlock(ICryptoTransform encryptor, byte[] block)
        {
            byte[] output = new byte[16];
            int written = encryptor.TransformBlock(block, 0, 16, output, 0);
            if (written != 16) throw new CryptographicException("AES ECB no produjo 16 bytes.");
            return output;
        }

        private static void IncrementCounter32(byte[] counter)
        {
            for (int i = 15; i >= 12; i--)
            {
                counter[i]++;
                if (counter[i] != 0) break;
            }
        }

        private static void ShiftRightOne(byte[] value)
        {
            byte carry = 0;
            for (int i = 0; i < value.Length; i++)
            {
                byte nextCarry = (byte)(value[i] & 1);
                value[i] = (byte)((value[i] >> 1) | (carry << 7));
                carry = nextCarry;
            }
        }

        private static void XorInPlace(byte[] target, byte[] value)
        {
            for (int i = 0; i < 16; i++) target[i] ^= value[i];
        }

        private static bool FixedTimeEqualsPrefix(byte[] full, byte[] expected)
        {
            int diff = 0;
            for (int i = 0; i < expected.Length; i++) diff |= full[i] ^ expected[i];
            return diff == 0;
        }

        private static void WriteUInt64BigEndian(byte[] target, int offset, ulong value)
        {
            for (int i = 7; i >= 0; i--)
            {
                target[offset + i] = (byte)(value & 0xFF);
                value >>= 8;
            }
        }
    }
}
