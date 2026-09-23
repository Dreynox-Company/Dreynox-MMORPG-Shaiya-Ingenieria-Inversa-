using System;
using System.IO;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering.Spk
{
    [Serializable]
    public sealed class SpkIndexCryptoProfile
    {
        public int schema = 1;
        public string profileId;
        public string indexSha256;
        public string algorithm = "AES";
        public string chainingMode = "GCM";
        public string secretHex;

        public byte[] SecretBytes()
        {
            if (string.IsNullOrWhiteSpace(secretHex) || (secretHex.Length % 2) != 0) throw new InvalidDataException("secretHex inválido.");
            byte[] data = new byte[secretHex.Length / 2];
            for (int i = 0; i < data.Length; i++) data[i] = Convert.ToByte(secretHex.Substring(i * 2, 2), 16);
            if (data.Length != 16 && data.Length != 24 && data.Length != 32) throw new InvalidDataException("La clave AES debe tener 16, 24 o 32 bytes.");
            return data;
        }

        public static SpkIndexCryptoProfile Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Perfil SPK no encontrado.", path);
            SpkIndexCryptoProfile p = JsonUtility.FromJson<SpkIndexCryptoProfile>(File.ReadAllText(path));
            if (p == null) throw new InvalidDataException("JSON de perfil inválido.");
            p.SecretBytes();
            return p;
        }
    }
}
