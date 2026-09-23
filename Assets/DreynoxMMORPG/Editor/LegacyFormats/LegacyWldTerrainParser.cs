using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Dreynox.Mmorpg.ParityCore;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public sealed class LegacyWldTexture
    {
        public string TextureName;
        public float TileSize;
        public string WalkSound;
    }

    public struct LegacyWldCoordinate
    {
        public int Id;
        public UnityEngine.Vector3 Position;
        public UnityEngine.Vector3 Forward;
        public UnityEngine.Vector3 Up;
    }

    public sealed class LegacyWldNameCoordinateGroup
    {
        public List<string> Names { get; } =
            new List<string>();

        public List<LegacyWldCoordinate> Coordinates { get; } =
            new List<LegacyWldCoordinate>();
    }

    public sealed class LegacyWldTerrainFile
    {
        public string Signature;
        public int MapSize;
        public int Resolution;
        public ushort[] RawHeights = Array.Empty<ushort>();
        public byte[] TextureMap = Array.Empty<byte>();
        public List<LegacyWldTexture> Textures { get; } =
            new List<LegacyWldTexture>();
        public string InnerLayout;

        public LegacyWldNameCoordinateGroup Buildings { get; } =
            new LegacyWldNameCoordinateGroup();

        public LegacyWldNameCoordinateGroup Shapes { get; } =
            new LegacyWldNameCoordinateGroup();

        public LegacyWldNameCoordinateGroup Trees { get; } =
            new LegacyWldNameCoordinateGroup();

        public LegacyWldNameCoordinateGroup Grass { get; } =
            new LegacyWldNameCoordinateGroup();

        public LegacyWldNameCoordinateGroup VAni1 { get; } =
            new LegacyWldNameCoordinateGroup();

        public LegacyWldNameCoordinateGroup VAni2 { get; } =
            new LegacyWldNameCoordinateGroup();

        public LegacyWldNameCoordinateGroup Dungeons { get; } =
            new LegacyWldNameCoordinateGroup();

        public ushort RawHeightAt(int x, int z)
        {
            if (x < 0 || z < 0 || x >= Resolution || z >= Resolution)
                throw new ArgumentOutOfRangeException();

            return RawHeights[z * Resolution + x];
        }

        public byte TextureIndexAt(int x, int z)
        {
            if (x < 0 || z < 0 || x >= Resolution || z >= Resolution)
                throw new ArgumentOutOfRangeException();

            return TextureMap[z * Resolution + x];
        }

        public double WorldHeightAtSample(int x, int z)
        {
            return LegacyTerrainHeightCore.Decode(RawHeightAt(x, z));
        }
    }

    public static class LegacyWldTerrainParser
    {
        private const int MaxTextures = 256;
        private const int MaxResourceNames = 100000;
        private const int MaxCoordinates = 2000000;
        private const int FixedStringBytes = 256;

        public static LegacyWldTerrainFile Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException(
                    "WLD path is required.",
                    nameof(path));

            using (FileStream stream = File.OpenRead(path))
            using (BinaryReader reader = new BinaryReader(stream))
                return Parse(reader);
        }

        public static LegacyWldTerrainFile Parse(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (BinaryReader reader = new BinaryReader(stream))
                return Parse(reader);
        }

        private static LegacyWldTerrainFile Parse(BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(reader, 8);

            string signature =
                Encoding.ASCII.GetString(reader.ReadBytes(4));

            if (signature != "FLD\0")
            {
                if (signature == "DUN\0")
                    throw new InvalidDataException(
                        "Dungeon WLD does not contain FLD terrain height data.");

                throw new InvalidDataException(
                    "Unsupported WLD signature: " +
                    EscapeSignature(signature) + ".");
            }

            uint mapSizeRaw = reader.ReadUInt32();
            if (mapSizeRaw > int.MaxValue)
                throw new InvalidDataException("WLD map size is too large.");

            int mapSize = (int)mapSizeRaw;
            int resolution =
                LegacyTerrainHeightCore.ResolutionForMapSize(mapSize);

            long sampleCount =
                checked((long)resolution * resolution);

            long required =
                checked(sampleCount * 2L + sampleCount + 4L);

            LegacyFormatPrimitives.EnsureRemaining(reader, required);

            ushort[] heights =
                new ushort[sampleCount];

            for (long i = 0; i < sampleCount; i++)
                heights[i] = reader.ReadUInt16();

            byte[] textureMap =
                reader.ReadBytes((int)sampleCount);

            if (textureMap.Length != sampleCount)
                throw new EndOfStreamException(
                    "WLD texture map ended unexpectedly.");

            int textureCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "WLD terrain texture",
                    MaxTextures);

            var result = new LegacyWldTerrainFile
            {
                Signature = signature,
                MapSize = mapSize,
                Resolution = resolution,
                RawHeights = heights,
                TextureMap = textureMap
            };

            for (int i = 0; i < textureCount; i++)
            {
                LegacyFormatPrimitives.EnsureRemaining(
                    reader,
                    FixedStringBytes + 4L + FixedStringBytes);

                string name = ReadFixedAscii(reader);
                float tileSize =
                    LegacyFormatPrimitives.ReadFiniteSingle(reader);
                string walkSound = ReadFixedAscii(reader);

                if (string.IsNullOrWhiteSpace(name))
                    throw new InvalidDataException(
                        "WLD terrain texture " + i + " has no file name.");

                if (tileSize <= 0f || tileSize > 100000f)
                    throw new InvalidDataException(
                        "WLD terrain texture " + i +
                        " has invalid tile size " + tileSize + ".");

                result.Textures.Add(
                    new LegacyWldTexture
                    {
                        TextureName = name,
                        TileSize = tileSize,
                        WalkSound = walkSound
                    });
            }

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                FixedStringBytes);

            result.InnerLayout = ReadFixedAscii(reader);

            ReadNameCoordinateGroup(
                reader,
                result.Buildings,
                "WLD building");

            ReadNameCoordinateGroup(
                reader,
                result.Shapes,
                "WLD shape");

            ReadNameCoordinateGroup(
                reader,
                result.Trees,
                "WLD tree");

            ReadNameCoordinateGroup(
                reader,
                result.Grass,
                "WLD grass");

            ReadNameCoordinateGroup(
                reader,
                result.VAni1,
                "WLD VAni group 1");

            ReadNameCoordinateGroup(
                reader,
                result.VAni2,
                "WLD VAni group 2");

            ReadNameCoordinateGroup(
                reader,
                result.Dungeons,
                "WLD dungeon");

            ValidateTextureIndices(result);
            return result;
        }

        private static void ReadNameCoordinateGroup(
            BinaryReader reader,
            LegacyWldNameCoordinateGroup group,
            string label)
        {
            int nameCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    label + " name",
                    MaxResourceNames);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                (long)nameCount * FixedStringBytes + 4L);

            for (int i = 0; i < nameCount; i++)
                group.Names.Add(ReadFixedAscii(reader));

            int coordinateCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    label + " coordinate",
                    MaxCoordinates);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                (long)coordinateCount * 40L);

            for (int i = 0; i < coordinateCount; i++)
            {
                int id = reader.ReadInt32();

                if (id < 0 || id >= group.Names.Count)
                {
                    throw new InvalidDataException(
                        label + " coordinate " + i +
                        " references resource id " + id +
                        " but the name table contains " +
                        group.Names.Count + " entries.");
                }

                Vector3 position =
                    LegacyFormatPrimitives.ReadVector3(reader);

                Vector3 forward =
                    LegacyFormatPrimitives.ReadVector3(reader);

                Vector3 up =
                    LegacyFormatPrimitives.ReadVector3(reader);

                if (forward.sqrMagnitude < 0.000001f ||
                    up.sqrMagnitude < 0.000001f)
                {
                    throw new InvalidDataException(
                        label + " coordinate " + i +
                        " has a degenerate orientation basis.");
                }

                group.Coordinates.Add(
                    new LegacyWldCoordinate
                    {
                        Id = id,
                        Position = position,
                        Forward = forward,
                        Up = up
                    });
            }
        }

        private static void ValidateTextureIndices(
            LegacyWldTerrainFile file)
        {
            for (int i = 0; i < file.TextureMap.Length; i++)
            {
                byte value = file.TextureMap[i];

                // 255 is used by the canonical corpus as an unassigned cell.
                if (value == byte.MaxValue)
                    continue;

                if (value >= file.Textures.Count)
                {
                    throw new InvalidDataException(
                        "WLD texture map references layer " + value +
                        " but only " + file.Textures.Count +
                        " layers are declared.");
                }
            }
        }

        private static string ReadFixedAscii(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(FixedStringBytes);
            if (bytes.Length != FixedStringBytes)
                throw new EndOfStreamException();

            int length = Array.IndexOf(bytes, (byte)0);
            if (length < 0)
                length = bytes.Length;

            return Encoding.ASCII
                .GetString(bytes, 0, length)
                .Trim();
        }

        private static string EscapeSignature(string value)
        {
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 32 && c <= 126)
                    builder.Append(c);
                else
                    builder.Append("\\x").Append(((int)c).ToString("X2"));
            }

            return builder.ToString();
        }
    }
}
