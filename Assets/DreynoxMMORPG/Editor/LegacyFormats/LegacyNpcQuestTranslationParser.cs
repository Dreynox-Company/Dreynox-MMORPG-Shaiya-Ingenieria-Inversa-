using System;
using System.Collections.Generic;
using System.IO;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public sealed class LegacyNpcTranslation
    {
        public LegacyNpcType Type;
        public short TypeId;
        public string Name = string.Empty;
        public string WelcomeMessage = string.Empty;
        public string[] TeleportNames = Array.Empty<string>();
    }

    public sealed class LegacyNpcQuestTranslationHeaderFile
    {
        private readonly Dictionary<int, LegacyNpcTranslation> _byKey =
            new Dictionary<int, LegacyNpcTranslation>();

        public List<int> GroupCounts { get; } =
            new List<int>();

        public List<LegacyNpcTranslation> Translations { get; } =
            new List<LegacyNpcTranslation>();

        public long NpcTranslationBytesConsumed { get; internal set; }
        public int QuestTranslationCount { get; internal set; }
        public long QuestTranslationPayloadBytes { get; internal set; }

        public bool TryGet(
            int npcType,
            int typeId,
            out LegacyNpcTranslation translation)
        {
            if (npcType < byte.MinValue ||
                npcType > byte.MaxValue ||
                typeId < short.MinValue ||
                typeId > short.MaxValue)
            {
                translation = null;
                return false;
            }

            return _byKey.TryGetValue(
                Key((byte)npcType, (short)typeId),
                out translation);
        }

        internal void Add(
            LegacyNpcTranslation translation)
        {
            int key =
                Key(
                    (byte)translation.Type,
                    translation.TypeId);

            if (_byKey.ContainsKey(key))
            {
                throw new InvalidDataException(
                    "NpcQuest translation contains duplicate key " +
                    translation.Type + "/" +
                    translation.TypeId + ".");
            }

            _byKey.Add(key, translation);
            Translations.Add(translation);
        }

        private static int Key(
            byte type,
            short id)
        {
            return (type << 16) |
                   (ushort)id;
        }
    }

    public static class LegacyNpcQuestTranslationParser
    {
        private const int GroupCount = 13;
        private const int MaxRecordsPerGroup = 100_000;
        private const int MaxStringBytes = 128 * 1024;

        public static LegacyNpcQuestTranslationHeaderFile Parse(
            string path,
            LegacyNpcQuestHeaderFile definitions)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException(
                    "NpcQuest translation path is required.",
                    nameof(path));

            LegacySDataDecryptionResult data =
                LegacySDataDecryptor.Decrypt(
                    path,
                    validateChecksum: true);

            return ParsePlain(
                data.Plaintext,
                definitions);
        }

        public static LegacyNpcQuestTranslationHeaderFile ParsePlain(
            byte[] plaintext,
            LegacyNpcQuestHeaderFile definitions)
        {
            if (plaintext == null)
                throw new ArgumentNullException(nameof(plaintext));
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));

            using (MemoryStream stream =
                   new MemoryStream(plaintext, false))
            using (BinaryReader reader =
                   new BinaryReader(stream))
            {
                return ParsePlain(
                    reader,
                    definitions);
            }
        }

        private static LegacyNpcQuestTranslationHeaderFile ParsePlain(
            BinaryReader reader,
            LegacyNpcQuestHeaderFile definitions)
        {
            if (definitions.GroupCounts.Count != GroupCount)
            {
                throw new InvalidDataException(
                    "NpcQuest definitions do not contain the canonical 13 groups.");
            }

            var result =
                new LegacyNpcQuestTranslationHeaderFile();

            for (int group = 1;
                 group <= GroupCount;
                 group++)
            {
                int count =
                    LegacyFormatPrimitives.ReadCount(
                        reader,
                        "NpcQuest translation group " + group,
                        MaxRecordsPerGroup);

                result.GroupCounts.Add(count);

                int expected =
                    definitions.GroupCounts[group - 1];

                if (count != expected)
                {
                    throw new InvalidDataException(
                        "NpcQuest translation group " + group +
                        " count " + count +
                        " does not match definition count " +
                        expected + ".");
                }

                List<LegacyNpcDefinition> groupDefinitions =
                    DefinitionsForType(
                        definitions,
                        (LegacyNpcType)group);

                if (groupDefinitions.Count != count)
                {
                    throw new InvalidDataException(
                        "NpcQuest definition ordering changed for group " +
                        group + ".");
                }

                for (int index = 0;
                     index < count;
                     index++)
                {
                    LegacyNpcDefinition definition =
                        groupDefinitions[index];

                    var translation =
                        new LegacyNpcTranslation
                        {
                            Type = definition.Type,
                            TypeId = definition.TypeId,
                            Name = ReadString(reader),
                            WelcomeMessage = ReadString(reader)
                        };

                    if (translation.Type ==
                        LegacyNpcType.GateKeeper)
                    {
                        translation.TeleportNames =
                            new[]
                            {
                                ReadString(reader),
                                ReadString(reader),
                                ReadString(reader)
                            };
                    }

                    result.Add(translation);
                }
            }

            result.NpcTranslationBytesConsumed =
                reader.BaseStream.Position;

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                4);

            result.QuestTranslationCount =
                reader.ReadInt32();

            if (result.QuestTranslationCount < 0 ||
                result.QuestTranslationCount > 100_000)
            {
                throw new InvalidDataException(
                    "NpcQuest translation quest count is invalid: " +
                    result.QuestTranslationCount + ".");
            }

            result.QuestTranslationPayloadBytes =
                reader.BaseStream.Length -
                reader.BaseStream.Position;

            return result;
        }

        private static List<LegacyNpcDefinition> DefinitionsForType(
            LegacyNpcQuestHeaderFile definitions,
            LegacyNpcType type)
        {
            var result =
                new List<LegacyNpcDefinition>();

            for (int i = 0;
                 i < definitions.Definitions.Count;
                 i++)
            {
                LegacyNpcDefinition definition =
                    definitions.Definitions[i];

                if (definition.Type == type)
                    result.Add(definition);
            }

            return result;
        }

        private static string ReadString(
            BinaryReader reader)
        {
            int length =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "NpcQuest translation string",
                    MaxStringBytes);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                length);

            if (length == 0)
                return string.Empty;

            byte[] bytes =
                reader.ReadBytes(length);

            return DecodeWindows1252(bytes)
                .TrimEnd(' ', ' ');
        }

        private static string DecodeWindows1252(
            byte[] bytes)
        {
            char[] chars =
                new char[bytes.Length];

            for (int i = 0; i < bytes.Length; i++)
                chars[i] = DecodeByte(bytes[i]);

            return new string(chars);
        }

        private static char DecodeByte(byte value)
        {
            if (value < 0x80 ||
                value >= 0xA0)
                return (char)value;

            switch (value)
            {
                case 0x80: return '€';
                case 0x82: return '‚';
                case 0x83: return 'ƒ';
                case 0x84: return '„';
                case 0x85: return '…';
                case 0x86: return '†';
                case 0x87: return '‡';
                case 0x88: return 'ˆ';
                case 0x89: return '‰';
                case 0x8A: return 'Š';
                case 0x8B: return '‹';
                case 0x8C: return 'Œ';
                case 0x8E: return 'Ž';
                case 0x91: return '‘';
                case 0x92: return '’';
                case 0x93: return '“';
                case 0x94: return '”';
                case 0x95: return '•';
                case 0x96: return '–';
                case 0x97: return '—';
                case 0x98: return '˜';
                case 0x99: return '™';
                case 0x9A: return 'š';
                case 0x9B: return '›';
                case 0x9C: return 'œ';
                case 0x9E: return 'ž';
                case 0x9F: return 'Ÿ';
                default: return (char)value;
            }
        }
    }
}
