using System;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering.Spk
{
    [Serializable]
    public sealed class SpkV3Header
    {
        public uint signature;
        public uint version;
        public ulong indexOffset;
        public ulong indexStoredBytes;
        public ulong indexDecodedBytes;
        public uint recordCount;
        public uint blockBytes;
        public byte[] indexNonce;
        public byte[] indexTag;
        public byte[] encryptedIndexSha256;
        public ulong auxiliaryOffset;
        public uint auxiliaryCount;

        public string VersionHex => "0x" + version.ToString("X8");
        public bool LooksObservedV3 => version == 0x00030000 && indexNonce != null && indexNonce.Length == 12 && indexTag != null && indexTag.Length == 16;
    }
}
