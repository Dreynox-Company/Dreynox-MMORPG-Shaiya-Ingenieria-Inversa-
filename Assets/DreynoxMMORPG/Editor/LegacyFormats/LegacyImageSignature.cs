using System;
using System.IO;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    /// <summary>Legacy filenames do not determine encoding. Never change source bytes.</summary>
    internal static class LegacyImageSignature
    {
        public static string ImportedFileName(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) throw new ArgumentException("Image source is required.", nameof(sourcePath));
            byte[] header;
            using (var stream = File.OpenRead(sourcePath))
            using (var reader = new BinaryReader(stream)) header = reader.ReadBytes(128);
            return Path.ChangeExtension(Path.GetFileName(sourcePath), DetectExtension(header)).ToLowerInvariant();
        }
        public static string DetectExtension(byte[] header)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (header.Length >= 128 && header[0] == 'D' && header[1] == 'D' && header[2] == 'S' && header[3] == ' ' && UInt32(header,4) == 124)
                return ".dds";
            if (header.Length >= 54 && header[0] == 'B' && header[1] == 'M' && UInt32(header,14) >= 40 &&
                UInt32(header,18) > 0 && UInt32(header,22) != 0 && header[26] == 1 && header[27] == 0)
                return ".bmp";
            throw new InvalidDataException("Unsupported or truncated legacy image signature; filename extension is not trusted.");
        }
        private static uint UInt32(byte[] b, int p)
        {
            return (uint)b[p] | ((uint)b[p+1] << 8) | ((uint)b[p+2] << 16) | ((uint)b[p+3] << 24);
        }
    }
}
