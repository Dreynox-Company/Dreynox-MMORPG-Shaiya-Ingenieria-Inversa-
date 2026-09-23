using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Dreynox.Mmorpg.ParityCore;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public sealed class LegacyWldTexture
    {
        public string TextureName = string.Empty;
        public float TileSize;
        public string WalkSound = string.Empty;
    }

    public struct LegacyWldCoordinate
    {
        public int Id;
        public Vector3 Position;
        public Vector3 Forward;
        public Vector3 Up;
    }

    public struct LegacyWldManiCoordinate
    {
        public int WorldBuildingId;
        public int Id;
        public Vector3 Position;
        public Vector3 Forward;
        public Vector3 Up;
    }

    public struct LegacyWldEffectPlacement
    {
        public Vector3 Position;
        public Vector3 Forward;
        public Vector3 Up;
        public int EffectId;
    }

    public struct LegacyWldMusicZone
    {
        public LegacyBounds Bounds;
        public float Radius;
        public int Id;
        public int Unknown;
    }

    public sealed class LegacyWldZone
    {
        public LegacyBounds Bounds;
        public List<int> Identifiers { get; } =
            new List<int>();
    }

    public struct LegacyWldSoundEffect
    {
        public int Id;
        public Vector3 Center;
        public float Radius;
    }

    public struct LegacyWldUnknownBox
    {
        public LegacyBounds Bounds;
        public float Radius;
    }

    public struct LegacyWldPortal
    {
        public LegacyBounds Bounds;
        public float Radius;
        public string Text1;
        public string Text2;
        public byte MapId;
        public short Faction;
        public byte Unknown;
        public Vector3 DestinationPosition;
    }

    public struct LegacyWldSpawn
    {
        public int Unknown1;
        public LegacyBounds Bounds;
        public float Radius;
        public int Faction;
        public int Unknown3;
    }

    public struct LegacyWldNamedArea
    {
        public LegacyBounds Bounds;
        public float Radius;
        public string Text1;
        public string Text2;
        public int Mode;
        public int Unknown;
    }

    public sealed class LegacyWldNpc
    {
        public int Type;
        public int TypeId;
        public Vector3 Position;
        public float Orientation;
        public List<Vector3> PatrolCoordinates { get; } =
            new List<Vector3>();
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
        public string Signature = string.Empty;
        public int MapSize;
        public int Resolution;
        public ushort[] RawHeights = Array.Empty<ushort>();
        public byte[] TextureMap = Array.Empty<byte>();
        public List<LegacyWldTexture> Textures { get; } =
            new List<LegacyWldTexture>();

        public string InnerLayout = string.Empty;

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

        public List<string> MAniNames { get; } =
            new List<string>();

        public List<LegacyWldManiCoordinate> MAniCoordinates { get; } =
            new List<LegacyWldManiCoordinate>();

        public string EffectName = string.Empty;

        public List<LegacyWldEffectPlacement> Effects { get; } =
            new List<LegacyWldEffectPlacement>();

        public int Unknown1;
        public int Unknown2;
        public int Unknown3;

        public LegacyWldNameCoordinateGroup Objects { get; } =
            new LegacyWldNameCoordinateGroup();

        public List<string> MusicNames { get; } =
            new List<string>();

        public List<LegacyWldMusicZone> MusicZones { get; } =
            new List<LegacyWldMusicZone>();

        public List<string> SoundEffectNames { get; } =
            new List<string>();

        public List<LegacyWldZone> Zones { get; } =
            new List<LegacyWldZone>();

        public List<LegacyWldSoundEffect> SoundEffects { get; } =
            new List<LegacyWldSoundEffect>();

        public List<LegacyWldUnknownBox> UnknownBoundingBoxes { get; } =
            new List<LegacyWldUnknownBox>();

        public List<LegacyWldPortal> Portals { get; } =
            new List<LegacyWldPortal>();

        public List<LegacyWldSpawn> Spawns { get; } =
            new List<LegacyWldSpawn>();

        public List<LegacyWldNamedArea> NamedAreas { get; } =
            new List<LegacyWldNamedArea>();

        public List<LegacyWldNpc> Npcs { get; } =
            new List<LegacyWldNpc>();

        public string SkyName = string.Empty;
        public string CloudsName1 = string.Empty;
        public string CloudsName2 = string.Empty;

        public Vector3 Point1;
        public Vector3 Point2;
        public Vector3 Point3;
        public float Unknown5;
        public float Unknown6;

        public long KnownBytesConsumed;
        public byte[] UnparsedTail = Array.Empty<byte>();

        public long UnparsedTailBytes =>
            UnparsedTail != null
                ? UnparsedTail.LongLength
                : 0;

        public ushort RawHeightAt(int x, int z)
        {
            if (x < 0 ||
                z < 0 ||
                x >= Resolution ||
                z >= Resolution)
                throw new ArgumentOutOfRangeException();

            return RawHeights[
                z * Resolution + x];
        }

        public byte TextureIndexAt(int x, int z)
        {
            if (x < 0 ||
                z < 0 ||
                x >= Resolution ||
                z >= Resolution)
                throw new ArgumentOutOfRangeException();

            return TextureMap[
                z * Resolution + x];
        }

        public double WorldHeightAtSample(
            int x,
            int z)
        {
            return LegacyTerrainHeightCore.Decode(
                RawHeightAt(x, z));
        }
    }

    public static class LegacyWldTerrainParser
    {
        private const int MaxTextures = 256;
        private const int MaxResourceNames = 100_000;
        private const int MaxCoordinates = 2_000_000;
        private const int MaxListCount = 2_000_000;
        private const int MaxZoneIdentifiers = 100_000;
        private const int MaxNpcLogicalEntries = 2_000_000;
        private const int FixedStringBytes = 256;

        public static LegacyWldTerrainFile Parse(
            string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException(
                    "WLD path is required.",
                    nameof(path));

            using (FileStream stream =
                   File.OpenRead(path))
            using (BinaryReader reader =
                   new BinaryReader(stream))
            {
                return Parse(reader);
            }
        }

        public static LegacyWldTerrainFile Parse(
            byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(
                    nameof(bytes));

            using (MemoryStream stream =
                   new MemoryStream(
                       bytes,
                       false))
            using (BinaryReader reader =
                   new BinaryReader(stream))
            {
                return Parse(reader);
            }
        }

        private static LegacyWldTerrainFile Parse(
            BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                8);

            string signature =
                Encoding.ASCII.GetString(
                    reader.ReadBytes(4));

            if (signature != "FLD\0")
            {
                if (signature == "DUN\0")
                {
                    throw new InvalidDataException(
                        "Dungeon WLD requires the DG world importer; " +
                        "this parser handles FLD field worlds.");
                }

                throw new InvalidDataException(
                    "Unsupported WLD signature: " +
                    EscapeSignature(signature) +
                    ".");
            }

            uint mapSizeRaw =
                reader.ReadUInt32();

            if (mapSizeRaw >
                int.MaxValue)
            {
                throw new InvalidDataException(
                    "WLD map size is too large.");
            }

            int mapSize =
                (int)mapSizeRaw;

            int resolution =
                LegacyTerrainHeightCore
                    .ResolutionForMapSize(
                        mapSize);

            int sampleCount =
                checked(
                    resolution *
                    resolution);

            long required =
                checked(
                    (long)sampleCount *
                    3L +
                    4L);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                required);

            ushort[] heights =
                new ushort[
                    sampleCount];

            for (int i = 0;
                 i < sampleCount;
                 i++)
            {
                heights[i] =
                    reader.ReadUInt16();
            }

            byte[] textureMap =
                reader.ReadBytes(
                    sampleCount);

            if (textureMap.Length !=
                sampleCount)
            {
                throw new EndOfStreamException(
                    "WLD texture map ended unexpectedly.");
            }

            int textureCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "WLD terrain texture",
                    MaxTextures);

            var result =
                new LegacyWldTerrainFile
                {
                    Signature = signature,
                    MapSize = mapSize,
                    Resolution = resolution,
                    RawHeights = heights,
                    TextureMap = textureMap
                };

            for (int i = 0;
                 i < textureCount;
                 i++)
            {
                LegacyFormatPrimitives.EnsureRemaining(
                    reader,
                    FixedStringBytes +
                    4L +
                    FixedStringBytes);

                string name =
                    ReadFixedAscii(reader);

                float tileSize =
                    LegacyFormatPrimitives
                        .ReadFiniteSingle(
                            reader);

                string walkSound =
                    ReadFixedAscii(reader);

                if (string.IsNullOrWhiteSpace(
                        name))
                {
                    throw new InvalidDataException(
                        "WLD terrain texture " +
                        i +
                        " has no file name.");
                }

                if (tileSize <= 0f ||
                    tileSize > 100000f)
                {
                    throw new InvalidDataException(
                        "WLD terrain texture " +
                        i +
                        " has invalid tile size " +
                        tileSize +
                        ".");
                }

                result.Textures.Add(
                    new LegacyWldTexture
                    {
                        TextureName =
                            name,
                        TileSize =
                            tileSize,
                        WalkSound =
                            walkSound
                    });
            }

            result.InnerLayout =
                ReadFixedAscii(reader);

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

            ReadNames(
                reader,
                result.MAniNames,
                "WLD MAni");

            int maniCoordinateCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "WLD MAni coordinate",
                    MaxCoordinates);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked(
                    (long)maniCoordinateCount *
                    44L));

            for (int i = 0;
                 i < maniCoordinateCount;
                 i++)
            {
                int worldBuildingId =
                    reader.ReadInt32();

                int id =
                    reader.ReadInt32();

                if (id < 0 ||
                    id >=
                    result.MAniNames.Count)
                {
                    throw new InvalidDataException(
                        "WLD MAni coordinate " +
                        i +
                        " references resource " +
                        id +
                        " but only " +
                        result.MAniNames.Count +
                        " names exist.");
                }

                Vector3 position =
                    LegacyFormatPrimitives
                        .ReadVector3(reader);

                Vector3 forward =
                    LegacyFormatPrimitives
                        .ReadVector3(reader);

                Vector3 up =
                    LegacyFormatPrimitives
                        .ReadVector3(reader);

                ValidateBasis(
                    forward,
                    up,
                    "WLD MAni",
                    i);

                result.MAniCoordinates.Add(
                    new LegacyWldManiCoordinate
                    {
                        WorldBuildingId =
                            worldBuildingId,
                        Id =
                            id,
                        Position =
                            position,
                        Forward =
                            forward,
                        Up =
                            up
                    });
            }

            result.EffectName =
                ReadFixedAscii(reader);

            int effectCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "WLD effect placement",
                    MaxListCount);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked(
                    (long)effectCount *
                    40L));

            for (int i = 0;
                 i < effectCount;
                 i++)
            {
                Vector3 position =
                    LegacyFormatPrimitives
                        .ReadVector3(reader);

                Vector3 forward =
                    LegacyFormatPrimitives
                        .ReadVector3(reader);

                Vector3 up =
                    LegacyFormatPrimitives
                        .ReadVector3(reader);

                ValidateBasis(
                    forward,
                    up,
                    "WLD effect placement",
                    i);

                LegacyWldEffectPlacement effect =
                    new LegacyWldEffectPlacement
                    {
                        Position =
                            position,
                        Forward =
                            forward,
                        Up =
                            up,
                        EffectId =
                            reader.ReadInt32()
                    };

                if (effect.EffectId < 0)
                {
                    throw new InvalidDataException(
                        "WLD effect placement " +
                        i +
                        " has negative sequence id " +
                        effect.EffectId +
                        ".");
                }

                result.Effects.Add(
                    effect);
            }

            if (result.Effects.Count > 0 &&
                string.IsNullOrWhiteSpace(
                    result.EffectName))
            {
                throw new InvalidDataException(
                    "WLD contains effect placements but has no EffectName library.");
            }

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                12);

            result.Unknown1 =
                reader.ReadInt32();

            result.Unknown2 =
                reader.ReadInt32();

            result.Unknown3 =
                reader.ReadInt32();

            ReadNameCoordinateGroup(
                reader,
                result.Objects,
                "WLD object");

            ReadNames(
                reader,
                result.MusicNames,
                "WLD music");

            int musicZoneCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "WLD music zone",
                    MaxListCount);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked(
                    (long)musicZoneCount *
                    36L));

            for (int i = 0;
                 i < musicZoneCount;
                 i++)
            {
                LegacyBounds bounds =
                    ReadBounds(reader);

                float radius =
                    LegacyFormatPrimitives
                        .ReadFiniteSingle(reader);

                int id =
                    reader.ReadInt32();

                int unknown =
                    reader.ReadInt32();

                if (id < 0 ||
                    id >=
                    result.MusicNames.Count)
                {
                    throw new InvalidDataException(
                        "WLD music zone " +
                        i +
                        " references music id " +
                        id +
                        " but only " +
                        result.MusicNames.Count +
                        " names exist.");
                }

                result.MusicZones.Add(
                    new LegacyWldMusicZone
                    {
                        Bounds = bounds,
                        Radius = radius,
                        Id = id,
                        Unknown = unknown
                    });
            }

            ReadNames(
                reader,
                result.SoundEffectNames,
                "WLD sound effect");

            int zoneCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "WLD zone",
                    MaxListCount);

            for (int i = 0;
                 i < zoneCount;
                 i++)
            {
                var zone =
                    new LegacyWldZone
                    {
                        Bounds =
                            ReadBounds(reader)
                    };

                int identifierCount =
                    LegacyFormatPrimitives.ReadCount(
                        reader,
                        "WLD zone identifier",
                        MaxZoneIdentifiers);

                LegacyFormatPrimitives.EnsureRemaining(
                    reader,
                    checked(
                        (long)identifierCount *
                        4L));

                for (int j = 0;
                     j < identifierCount;
                     j++)
                {
                    zone.Identifiers.Add(
                        reader.ReadInt32());
                }

                result.Zones.Add(
                    zone);
            }

            int soundEffectCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "WLD positional sound",
                    MaxListCount);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked(
                    (long)soundEffectCount *
                    20L));

            for (int i = 0;
                 i < soundEffectCount;
                 i++)
            {
                int id =
                    reader.ReadInt32();

                if (id < 0 ||
                    id >=
                    result.SoundEffectNames.Count)
                {
                    throw new InvalidDataException(
                        "WLD positional sound " +
                        i +
                        " references sound id " +
                        id +
                        " but only " +
                        result.SoundEffectNames.Count +
                        " names exist.");
                }

                result.SoundEffects.Add(
                    new LegacyWldSoundEffect
                    {
                        Id =
                            id,
                        Center =
                            LegacyFormatPrimitives
                                .ReadVector3(
                                    reader),
                        Radius =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(
                                    reader)
                    });
            }

            int unknownBoxCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "WLD unknown box",
                    MaxListCount);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked(
                    (long)unknownBoxCount *
                    28L));

            for (int i = 0;
                 i < unknownBoxCount;
                 i++)
            {
                result.UnknownBoundingBoxes.Add(
                    new LegacyWldUnknownBox
                    {
                        Bounds =
                            ReadBounds(reader),
                        Radius =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(
                                    reader)
                    });
            }

            int portalCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "WLD portal",
                    MaxListCount);

            for (int i = 0;
                 i < portalCount;
                 i++)
            {
                LegacyFormatPrimitives.EnsureRemaining(
                    reader,
                    556);

                result.Portals.Add(
                    new LegacyWldPortal
                    {
                        Bounds =
                            ReadBounds(reader),
                        Radius =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(
                                    reader),
                        Text1 =
                            ReadFixedAscii(
                                reader),
                        Text2 =
                            ReadFixedAscii(
                                reader),
                        MapId =
                            reader.ReadByte(),
                        Faction =
                            reader.ReadInt16(),
                        Unknown =
                            reader.ReadByte(),
                        DestinationPosition =
                            LegacyFormatPrimitives
                                .ReadVector3(
                                    reader)
                    });
            }

            int spawnCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "WLD spawn",
                    MaxListCount);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked(
                    (long)spawnCount *
                    40L));

            for (int i = 0;
                 i < spawnCount;
                 i++)
            {
                result.Spawns.Add(
                    new LegacyWldSpawn
                    {
                        Unknown1 =
                            reader.ReadInt32(),
                        Bounds =
                            ReadBounds(reader),
                        Radius =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(
                                    reader),
                        Faction =
                            reader.ReadInt32(),
                        Unknown3 =
                            reader.ReadInt32()
                    });
            }

            int namedAreaCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "WLD named area",
                    MaxListCount);

            for (int i = 0;
                 i < namedAreaCount;
                 i++)
            {
                LegacyFormatPrimitives.EnsureRemaining(
                    reader,
                    548);

                result.NamedAreas.Add(
                    new LegacyWldNamedArea
                    {
                        Bounds =
                            ReadBounds(reader),
                        Radius =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(
                                    reader),
                        Text1 =
                            ReadFixedAscii(
                                reader),
                        Text2 =
                            ReadFixedAscii(
                                reader),
                        Mode =
                            reader.ReadInt32(),
                        Unknown =
                            reader.ReadInt32()
                    });
            }

            int logicalNpcEntries =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "WLD NPC logical entry",
                    MaxNpcLogicalEntries);

            int remainingNpcEntries =
                logicalNpcEntries;

            while (remainingNpcEntries > 0)
            {
                LegacyFormatPrimitives.EnsureRemaining(
                    reader,
                    28);

                var npc =
                    new LegacyWldNpc
                    {
                        Type =
                            reader.ReadInt32(),
                        TypeId =
                            reader.ReadInt32(),
                        Position =
                            LegacyFormatPrimitives
                                .ReadVector3(
                                    reader),
                        Orientation =
                            LegacyFormatPrimitives
                                .ReadFiniteSingle(
                                    reader)
                    };

                int patrolCount =
                    LegacyFormatPrimitives.ReadCount(
                        reader,
                        "WLD NPC patrol coordinate",
                        MaxCoordinates);

                if (patrolCount + 1 >
                    remainingNpcEntries)
                {
                    throw new InvalidDataException(
                        "WLD NPC logical count is inconsistent: " +
                        "record requests " +
                        patrolCount +
                        " patrol coordinates with only " +
                        remainingNpcEntries +
                        " logical entries remaining.");
                }

                LegacyFormatPrimitives.EnsureRemaining(
                    reader,
                    checked(
                        (long)patrolCount *
                        12L));

                for (int i = 0;
                     i < patrolCount;
                     i++)
                {
                    npc.PatrolCoordinates.Add(
                        LegacyFormatPrimitives
                            .ReadVector3(
                                reader));
                }

                result.Npcs.Add(
                    npc);

                remainingNpcEntries -=
                    patrolCount + 1;
            }

            result.SkyName =
                ReadFixedAscii(reader);

            result.CloudsName1 =
                ReadFixedAscii(reader);

            result.CloudsName2 =
                ReadFixedAscii(reader);

            result.Point1 =
                LegacyFormatPrimitives
                    .ReadVector3(
                        reader);

            result.Point2 =
                LegacyFormatPrimitives
                    .ReadVector3(
                        reader);

            result.Point3 =
                LegacyFormatPrimitives
                    .ReadVector3(
                        reader);

            result.Unknown5 =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(
                        reader);

            result.Unknown6 =
                LegacyFormatPrimitives
                    .ReadFiniteSingle(
                        reader);

            result.KnownBytesConsumed =
                reader.BaseStream.Position;

            long tailBytes =
                reader.BaseStream.Length -
                reader.BaseStream.Position;

            if (tailBytes < 0 ||
                tailBytes > int.MaxValue)
            {
                throw new InvalidDataException(
                    "WLD tail size is invalid: " +
                    tailBytes + ".");
            }

            result.UnparsedTail =
                reader.ReadBytes(
                    (int)tailBytes);

            if (result.UnparsedTail.Length !=
                tailBytes)
            {
                throw new EndOfStreamException(
                    "WLD tail ended unexpectedly.");
            }

            ValidateTextureIndices(
                result);

            return result;
        }

        private static void ReadNameCoordinateGroup(
            BinaryReader reader,
            LegacyWldNameCoordinateGroup group,
            string label)
        {
            ReadNames(
                reader,
                group.Names,
                label);

            int coordinateCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    label + " coordinate",
                    MaxCoordinates);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked(
                    (long)coordinateCount *
                    40L));

            for (int i = 0;
                 i < coordinateCount;
                 i++)
            {
                int id =
                    reader.ReadInt32();

                if (id < 0 ||
                    id >=
                    group.Names.Count)
                {
                    throw new InvalidDataException(
                        label +
                        " coordinate " +
                        i +
                        " references resource id " +
                        id +
                        " but the name table contains " +
                        group.Names.Count +
                        " entries.");
                }

                Vector3 position =
                    LegacyFormatPrimitives
                        .ReadVector3(
                            reader);

                Vector3 forward =
                    LegacyFormatPrimitives
                        .ReadVector3(
                            reader);

                Vector3 up =
                    LegacyFormatPrimitives
                        .ReadVector3(
                            reader);

                ValidateBasis(
                    forward,
                    up,
                    label,
                    i);

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

        private static void ReadNames(
            BinaryReader reader,
            ICollection<string> names,
            string label)
        {
            int count =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    label + " name",
                    MaxResourceNames);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked(
                    (long)count *
                    FixedStringBytes));

            for (int i = 0;
                 i < count;
                 i++)
            {
                names.Add(
                    ReadFixedAscii(
                        reader));
            }
        }

        private static LegacyBounds ReadBounds(
            BinaryReader reader)
        {
            return new LegacyBounds
            {
                Lower =
                    LegacyFormatPrimitives
                        .ReadVector3(
                            reader),
                Upper =
                    LegacyFormatPrimitives
                        .ReadVector3(
                            reader)
            };
        }

        private static void ValidateBasis(
            Vector3 forward,
            Vector3 up,
            string label,
            int ordinal)
        {
            if (forward.sqrMagnitude <
                    0.000001f ||
                up.sqrMagnitude <
                    0.000001f)
            {
                throw new InvalidDataException(
                    label +
                    " coordinate " +
                    ordinal +
                    " has a degenerate orientation basis.");
            }
        }

        private static void ValidateTextureIndices(
            LegacyWldTerrainFile file)
        {
            for (int i = 0;
                 i <
                 file.TextureMap.Length;
                 i++)
            {
                byte value =
                    file.TextureMap[i];

                if (value ==
                    byte.MaxValue)
                    continue;

                if (value >=
                    file.Textures.Count)
                {
                    throw new InvalidDataException(
                        "WLD texture map references layer " +
                        value +
                        " but only " +
                        file.Textures.Count +
                        " layers are declared.");
                }
            }
        }

        private static string ReadFixedAscii(
            BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                FixedStringBytes);

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
                length =
                    bytes.Length;

            return Encoding.ASCII
                .GetString(
                    bytes,
                    0,
                    length)
                .Trim();
        }

        private static string EscapeSignature(
            string value)
        {
            StringBuilder builder =
                new StringBuilder();

            for (int i = 0;
                 i < value.Length;
                 i++)
            {
                char c =
                    value[i];

                if (c >= 32 &&
                    c <= 126)
                {
                    builder.Append(c);
                }
                else
                {
                    builder
                        .Append("\\x")
                        .Append(
                            ((int)c)
                            .ToString("X2"));
                }
            }

            return builder.ToString();
        }
    }
}
