using System;
using System.Collections.Generic;
using System.IO;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public enum LegacyNpcType : byte
    {
        Merchant = 1,
        GateKeeper = 2,
        Blacksmith = 3,
        PvPManager = 4,
        GamblingHouse = 5,
        Warehouse = 6,
        Normal = 7,
        Guard = 8,
        Animal = 9,
        Apprentice = 10,
        GuildMaster = 11,
        DeadNpc = 12,
        CombatCommander = 13
    }

    public struct LegacyNpcSaleItem
    {
        public byte Type;
        public byte TypeId;
    }

    public struct LegacyNpcGateTarget
    {
        public short MapId;
        public float X;
        public float Y;
        public float Z;
        public int Cost;
    }

    public sealed class LegacyNpcDefinition
    {
        public LegacyNpcType Type;
        public short TypeId;
        public int Model;
        public int MoveDistance;
        public int MoveSpeed;
        public int Faction;

        public byte? MerchantType;

        public List<LegacyNpcSaleItem> SaleItems { get; } =
            new List<LegacyNpcSaleItem>();

        public List<LegacyNpcGateTarget> GateTargets { get; } =
            new List<LegacyNpcGateTarget>();

        public List<short> InQuestIds { get; } =
            new List<short>();

        public List<short> OutQuestIds { get; } =
            new List<short>();
    }

    public sealed class LegacyNpcQuestHeaderFile
    {
        private readonly Dictionary<int, LegacyNpcDefinition> _definitions =
            new Dictionary<int, LegacyNpcDefinition>();

        public List<int> GroupCounts { get; } =
            new List<int>();

        public List<LegacyNpcDefinition> Definitions { get; } =
            new List<LegacyNpcDefinition>();

        public long HeaderBytesConsumed { get; internal set; }
        public long UnparsedTailBytes { get; internal set; }

        public bool TryGet(
            int npcType,
            int typeId,
            out LegacyNpcDefinition definition)
        {
            if (npcType < byte.MinValue ||
                npcType > byte.MaxValue ||
                typeId < short.MinValue ||
                typeId > short.MaxValue)
            {
                definition = null;
                return false;
            }

            return _definitions.TryGetValue(
                Key((byte)npcType, (short)typeId),
                out definition);
        }

        internal void Add(LegacyNpcDefinition definition)
        {
            int key =
                Key(
                    (byte)definition.Type,
                    definition.TypeId);

            if (_definitions.ContainsKey(key))
            {
                throw new InvalidDataException(
                    "NpcQuest contains duplicate NPC key " +
                    definition.Type + "/" +
                    definition.TypeId + ".");
            }

            _definitions.Add(key, definition);
            Definitions.Add(definition);
        }

        private static int Key(
            byte type,
            short id)
        {
            return (type << 16) |
                   (ushort)id;
        }
    }

    public static class LegacyNpcQuestHeaderParser
    {
        private const int GroupCount = 13;
        private const int MaxRecordsPerGroup = 100_000;
        private const int MaxQuestLinks = 100_000;
        private const int MaxMerchantItems = 10_000;

        public static LegacyNpcQuestHeaderFile ParseEncrypted(
            string path,
            bool validateChecksum = true)
        {
            LegacySDataDecryptionResult decrypted =
                LegacySDataDecryptor.Decrypt(
                    path,
                    validateChecksum);

            return ParsePlain(decrypted.Plaintext);
        }

        public static LegacyNpcQuestHeaderFile ParsePlain(
            byte[] plaintext)
        {
            if (plaintext == null)
                throw new ArgumentNullException(nameof(plaintext));

            using (MemoryStream stream =
                   new MemoryStream(plaintext, false))
            using (BinaryReader reader =
                   new BinaryReader(stream))
            {
                return ParsePlain(reader);
            }
        }

        private static LegacyNpcQuestHeaderFile ParsePlain(
            BinaryReader reader)
        {
            var result =
                new LegacyNpcQuestHeaderFile();

            for (int group = 1;
                 group <= GroupCount;
                 group++)
            {
                int count =
                    LegacyFormatPrimitives.ReadCount(
                        reader,
                        "NpcQuest group " + group,
                        MaxRecordsPerGroup);

                result.GroupCounts.Add(count);

                LegacyNpcType expectedType =
                    (LegacyNpcType)group;

                for (int i = 0; i < count; i++)
                {
                    LegacyNpcDefinition definition;

                    switch (expectedType)
                    {
                        case LegacyNpcType.Merchant:
                            definition =
                                ReadMerchant(reader);
                            break;

                        case LegacyNpcType.GateKeeper:
                            definition =
                                ReadGateKeeper(reader);
                            break;

                        default:
                            definition =
                                ReadStandard(reader);
                            break;
                    }

                    if (definition.Type != expectedType)
                    {
                        throw new InvalidDataException(
                            "NpcQuest group " + expectedType +
                            " record " + i +
                            " declared type " +
                            definition.Type + ".");
                    }

                    result.Add(definition);
                }
            }

            result.HeaderBytesConsumed =
                reader.BaseStream.Position;

            result.UnparsedTailBytes =
                reader.BaseStream.Length -
                reader.BaseStream.Position;

            if (result.UnparsedTailBytes < 0)
            {
                throw new InvalidDataException(
                    "NpcQuest parser advanced beyond payload.");
            }

            return result;
        }

        private static LegacyNpcDefinition ReadMerchant(
            BinaryReader reader)
        {
            LegacyNpcDefinition npc =
                ReadFirstSegment(reader);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                1);

            npc.MerchantType =
                reader.ReadByte();

            ReadSecondSegment(reader, npc);

            int itemCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "NpcQuest merchant item",
                    MaxMerchantItems);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked((long)itemCount * 2L));

            for (int i = 0; i < itemCount; i++)
            {
                npc.SaleItems.Add(
                    new LegacyNpcSaleItem
                    {
                        Type = reader.ReadByte(),
                        TypeId = reader.ReadByte()
                    });
            }

            ReadThirdSegment(reader, npc);
            return npc;
        }

        private static LegacyNpcDefinition ReadGateKeeper(
            BinaryReader reader)
        {
            LegacyNpcDefinition npc =
                ReadFirstSegment(reader);

            ReadSecondSegment(reader, npc);

            const int TargetCount = 3;

            for (int i = 0; i < TargetCount; i++)
            {
                LegacyFormatPrimitives.EnsureRemaining(
                    reader,
                    18);

                npc.GateTargets.Add(
                    new LegacyNpcGateTarget
                    {
                        MapId = reader.ReadInt16(),
                        X = LegacyFormatPrimitives.ReadFiniteSingle(reader),
                        Y = LegacyFormatPrimitives.ReadFiniteSingle(reader),
                        Z = LegacyFormatPrimitives.ReadFiniteSingle(reader),
                        Cost = reader.ReadInt32()
                    });
            }

            ReadThirdSegment(reader, npc);
            return npc;
        }

        private static LegacyNpcDefinition ReadStandard(
            BinaryReader reader)
        {
            LegacyNpcDefinition npc =
                ReadFirstSegment(reader);

            ReadSecondSegment(reader, npc);
            ReadThirdSegment(reader, npc);
            return npc;
        }

        private static LegacyNpcDefinition ReadFirstSegment(
            BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                3);

            return new LegacyNpcDefinition
            {
                Type =
                    (LegacyNpcType)reader.ReadByte(),
                TypeId =
                    reader.ReadInt16()
            };
        }

        private static void ReadSecondSegment(
            BinaryReader reader,
            LegacyNpcDefinition npc)
        {
            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                16);

            npc.Model =
                reader.ReadInt32();

            npc.MoveDistance =
                reader.ReadInt32();

            npc.MoveSpeed =
                reader.ReadInt32();

            npc.Faction =
                reader.ReadInt32();
        }

        private static void ReadThirdSegment(
            BinaryReader reader,
            LegacyNpcDefinition npc)
        {
            int inCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "NpcQuest in-quest link",
                    MaxQuestLinks);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked((long)inCount * 2L));

            for (int i = 0; i < inCount; i++)
                npc.InQuestIds.Add(
                    reader.ReadInt16());

            int outCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "NpcQuest out-quest link",
                    MaxQuestLinks);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked((long)outCount * 2L));

            for (int i = 0; i < outCount; i++)
                npc.OutQuestIds.Add(
                    reader.ReadInt16());
        }
    }
}
