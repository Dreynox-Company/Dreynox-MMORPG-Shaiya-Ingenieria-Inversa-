using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public struct LegacyBounds
    {
        public Vector3 Lower;
        public Vector3 Upper;
    }

    public struct LegacySvmapPortal
    {
        public Vector3 Position;
        public int FactionOrPortalId;
        public ushort MinLevel;
        public ushort MaxLevel;
        public uint TargetMapId;
        public Vector3 TargetPosition;
    }

    public struct LegacySvmapNpcPosition
    {
        public Vector3 Position;
        public float Yaw;
    }

    public sealed class LegacySvmapNpc
    {
        public int NpcType;
        public int NpcId;
        public List<LegacySvmapNpcPosition> Positions { get; } =
            new List<LegacySvmapNpcPosition>();
    }

    public struct LegacySvmapMonsterSpawn
    {
        public uint MobId;
        public uint Count;
    }

    public sealed class LegacySvmapMonsterArea
    {
        public LegacyBounds Area;
        public List<LegacySvmapMonsterSpawn> Monsters { get; } =
            new List<LegacySvmapMonsterSpawn>();
    }

    public struct LegacySvmapSpawnArea
    {
        public int Unknown1;
        public int Faction;
        public int Unknown2;
        public LegacyBounds Area;
    }

    public struct LegacySvmapNamedArea
    {
        public LegacyBounds Area;
        public int NameIdentifier1;
        public int NameIdentifier2;
    }

    public sealed class LegacySvmapFile
    {
        public int MapSize;
        public byte[] MapMask = Array.Empty<byte>();
        public int CellSize;

        public List<Vector3> Ladders { get; } =
            new List<Vector3>();

        public List<LegacySvmapMonsterArea> MonsterAreas { get; } =
            new List<LegacySvmapMonsterArea>();

        public List<LegacySvmapNpc> Npcs { get; } =
            new List<LegacySvmapNpc>();

        public List<LegacySvmapPortal> Portals { get; } =
            new List<LegacySvmapPortal>();

        public List<LegacySvmapSpawnArea> Spawns { get; } =
            new List<LegacySvmapSpawnArea>();

        public List<LegacySvmapNamedArea> NamedAreas { get; } =
            new List<LegacySvmapNamedArea>();

        public int NpcPositionCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Npcs.Count; i++)
                    count += Npcs[i].Positions.Count;
                return count;
            }
        }

        public long MonsterInstanceCount
        {
            get
            {
                long count = 0;
                for (int i = 0; i < MonsterAreas.Count; i++)
                for (int j = 0; j < MonsterAreas[i].Monsters.Count; j++)
                    count += MonsterAreas[i].Monsters[j].Count;
                return count;
            }
        }

        public bool GetMapMaskBit(int x, int z)
        {
            if (x < 0 || z < 0 || x >= MapSize || z >= MapSize)
                throw new ArgumentOutOfRangeException();

            long index = (long)z * MapSize + x;
            int byteIndex = (int)(index >> 3);
            int bit = (int)(index & 7);
            return (MapMask[byteIndex] & (1 << bit)) != 0;
        }
    }

    public static class LegacySvmapParser
    {
        private const int MaxMapSize = 8192;
        private const int MaxListCount = 2_000_000;

        public static LegacySvmapFile Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("SVMAP path is required.", nameof(path));

            using (FileStream stream = File.OpenRead(path))
            using (BinaryReader reader = new BinaryReader(stream))
                return Parse(reader);
        }

        public static LegacySvmapFile Parse(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (BinaryReader reader = new BinaryReader(stream))
                return Parse(reader);
        }

        private static LegacySvmapFile Parse(BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(reader, 4);

            int mapSize = reader.ReadInt32();
            if (mapSize <= 0 || mapSize > MaxMapSize)
                throw new InvalidDataException(
                    "SVMAP map size is outside the supported range: " +
                    mapSize + ".");

            long maskBits = (long)mapSize * mapSize;
            long maskBytes = maskBits / 8L;

            if (maskBits % 8L != 0L ||
                maskBytes > int.MaxValue)
            {
                throw new InvalidDataException(
                    "SVMAP map mask length is invalid.");
            }

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                maskBytes + 4L);

            var result = new LegacySvmapFile
            {
                MapSize = mapSize,
                MapMask = reader.ReadBytes((int)maskBytes),
                CellSize = reader.ReadInt32()
            };

            ReadLadders(reader, result);
            ReadMonsterAreas(reader, result);
            ReadNpcs(reader, result);
            ReadPortals(reader, result);
            ReadSpawnAreas(reader, result);
            ReadNamedAreas(reader, result);

            LegacyFormatPrimitives.EnsureFullyConsumed(reader, "SVMAP");
            return result;
        }

        private static void ReadLadders(
            BinaryReader reader,
            LegacySvmapFile result)
        {
            int count = LegacyFormatPrimitives.ReadCount(
                reader,
                "SVMAP ladder",
                MaxListCount);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                (long)count * 12L);

            for (int i = 0; i < count; i++)
                result.Ladders.Add(
                    LegacyFormatPrimitives.ReadVector3(reader));
        }

        private static void ReadMonsterAreas(
            BinaryReader reader,
            LegacySvmapFile result)
        {
            int count = LegacyFormatPrimitives.ReadCount(
                reader,
                "SVMAP monster area",
                MaxListCount);

            for (int i = 0; i < count; i++)
            {
                var area = new LegacySvmapMonsterArea
                {
                    Area = ReadBounds(reader)
                };

                int monsters = LegacyFormatPrimitives.ReadCount(
                    reader,
                    "SVMAP monster spawn",
                    MaxListCount);

                LegacyFormatPrimitives.EnsureRemaining(
                    reader,
                    (long)monsters * 8L);

                for (int j = 0; j < monsters; j++)
                {
                    area.Monsters.Add(
                        new LegacySvmapMonsterSpawn
                        {
                            MobId = reader.ReadUInt32(),
                            Count = reader.ReadUInt32()
                        });
                }

                result.MonsterAreas.Add(area);
            }
        }

        private static void ReadNpcs(
            BinaryReader reader,
            LegacySvmapFile result)
        {
            int count = LegacyFormatPrimitives.ReadCount(
                reader,
                "SVMAP NPC definition",
                MaxListCount);

            for (int i = 0; i < count; i++)
            {
                LegacyFormatPrimitives.EnsureRemaining(reader, 12);

                var npc = new LegacySvmapNpc
                {
                    NpcType = reader.ReadInt32(),
                    NpcId = reader.ReadInt32()
                };

                int positions = LegacyFormatPrimitives.ReadCount(
                    reader,
                    "SVMAP NPC position",
                    MaxListCount);

                LegacyFormatPrimitives.EnsureRemaining(
                    reader,
                    (long)positions * 16L);

                for (int j = 0; j < positions; j++)
                {
                    npc.Positions.Add(
                        new LegacySvmapNpcPosition
                        {
                            Position =
                                LegacyFormatPrimitives.ReadVector3(reader),
                            Yaw =
                                LegacyFormatPrimitives.ReadFiniteSingle(reader)
                        });
                }

                result.Npcs.Add(npc);
            }
        }

        private static void ReadPortals(
            BinaryReader reader,
            LegacySvmapFile result)
        {
            int count = LegacyFormatPrimitives.ReadCount(
                reader,
                "SVMAP portal",
                MaxListCount);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                (long)count * 36L);

            for (int i = 0; i < count; i++)
            {
                result.Portals.Add(
                    new LegacySvmapPortal
                    {
                        Position =
                            LegacyFormatPrimitives.ReadVector3(reader),
                        FactionOrPortalId =
                            reader.ReadInt32(),
                        MinLevel =
                            reader.ReadUInt16(),
                        MaxLevel =
                            reader.ReadUInt16(),
                        TargetMapId =
                            reader.ReadUInt32(),
                        TargetPosition =
                            LegacyFormatPrimitives.ReadVector3(reader)
                    });
            }
        }

        private static void ReadSpawnAreas(
            BinaryReader reader,
            LegacySvmapFile result)
        {
            int count = LegacyFormatPrimitives.ReadCount(
                reader,
                "SVMAP spawn area",
                MaxListCount);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                (long)count * 36L);

            for (int i = 0; i < count; i++)
            {
                result.Spawns.Add(
                    new LegacySvmapSpawnArea
                    {
                        Unknown1 = reader.ReadInt32(),
                        Faction = reader.ReadInt32(),
                        Unknown2 = reader.ReadInt32(),
                        Area = ReadBounds(reader)
                    });
            }
        }

        private static void ReadNamedAreas(
            BinaryReader reader,
            LegacySvmapFile result)
        {
            int count = LegacyFormatPrimitives.ReadCount(
                reader,
                "SVMAP named area",
                MaxListCount);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                (long)count * 32L);

            for (int i = 0; i < count; i++)
            {
                result.NamedAreas.Add(
                    new LegacySvmapNamedArea
                    {
                        Area = ReadBounds(reader),
                        NameIdentifier1 = reader.ReadInt32(),
                        NameIdentifier2 = reader.ReadInt32()
                    });
            }
        }

        private static LegacyBounds ReadBounds(BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(reader, 24);

            return new LegacyBounds
            {
                Lower = LegacyFormatPrimitives.ReadVector3(reader),
                Upper = LegacyFormatPrimitives.ReadVector3(reader)
            };
        }
    }
}
