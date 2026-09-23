using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public sealed class LegacyWtrFile
    {
        public float TileSize { get; internal set; }
        public uint Unknown2 { get; internal set; }
        public int Unknown3 { get; internal set; }
        public List<string> FrameNames { get; } =
            new List<string>();
    }

    public static class LegacyWtrParser
    {
        private const int FixedStringBytes = 256;
        private const int MaxFrames = 256;

        public static LegacyWtrFile Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException(
                    "WTR path is required.",
                    nameof(path));

            using (FileStream stream = File.OpenRead(path))
            using (BinaryReader reader = new BinaryReader(stream))
                return Parse(reader);
        }

        public static LegacyWtrFile Parse(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            using (MemoryStream stream =
                   new MemoryStream(bytes, false))
            using (BinaryReader reader =
                   new BinaryReader(stream))
                return Parse(reader);
        }

        private static LegacyWtrFile Parse(
            BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                16);

            var result =
                new LegacyWtrFile
                {
                    TileSize =
                        LegacyFormatPrimitives
                            .ReadFiniteSingle(reader),
                    Unknown2 =
                        reader.ReadUInt32(),
                    Unknown3 =
                        reader.ReadInt32()
                };

            if (result.TileSize <= 0f ||
                result.TileSize > 100000f)
            {
                throw new InvalidDataException(
                    "WTR tile size is invalid: " +
                    result.TileSize + ".");
            }

            int frameCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "WTR frame",
                    MaxFrames);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked(
                    (long)frameCount *
                    FixedStringBytes));

            for (int i = 0;
                 i < frameCount;
                 i++)
            {
                string name =
                    ReadFixedString(reader);

                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new InvalidDataException(
                        "WTR frame " + i +
                        " has an empty texture name.");
                }

                result.FrameNames.Add(name);
            }

            LegacyFormatPrimitives.EnsureFullyConsumed(
                reader,
                "WTR");

            return result;
        }

        private static string ReadFixedString(
            BinaryReader reader)
        {
            byte[] bytes =
                reader.ReadBytes(
                    FixedStringBytes);

            if (bytes.Length !=
                FixedStringBytes)
            {
                throw new EndOfStreamException();
            }

            int length =
                Array.IndexOf(
                    bytes,
                    (byte)0);

            if (length < 0)
                length = bytes.Length;

            return Encoding.ASCII
                .GetString(
                    bytes,
                    0,
                    length)
                .Trim();
        }
    }
}
