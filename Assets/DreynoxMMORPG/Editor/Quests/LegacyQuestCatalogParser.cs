using System;
using System.Collections.Generic;
using System.IO;
using Dreynox.Mmorpg.Quests;

namespace Dreynox.Mmorpg.Editor.Quests
{
    /// <summary>ps0032 quest tail and separate translations. Not an SPK/archive reader.</summary>
    public static class LegacyQuestCatalogParser
    {
        public const int RecordSize = 287;
        private const int MaxRecords = 100000, MaxStringBytes = 131072;
        public static LegacyQuestCatalogData Parse(byte[] plain, long npcEnd, byte[] translations, long npcTranslationEnd)
        {
            if (plain == null || translations == null) throw new ArgumentNullException();
            using (var stream = new MemoryStream(plain, false))
            using (var reader = new BinaryReader(stream))
            using (var texts = new MemoryStream(translations, false))
            using (var tr = new BinaryReader(texts))
            {
                Seek(stream, npcEnd); Seek(texts, npcTranslationEnd);
                // 256 item types x 256 IDs; each has start/end quest-link lists.
                for (int i = 0; i < 65536; i++)
                    for (int j = 0; j < 2; j++)
                    { int count = Count(reader); Require(reader, checked((long)count * 2)); stream.Position += (long)count * 2; }
                int n = Count(reader);
                if (stream.Length - stream.Position != (long)n * RecordSize)
                    throw new InvalidDataException("Quest record stride is not the verified 287-byte ps0032 layout.");
                if (Count(tr) != n) throw new InvalidDataException("Quest translation count differs from definitions.");
                var catalog = new LegacyQuestCatalogData { quests = new LegacyQuestDefinition[n] };
                var ids = new HashSet<ushort>();
                for (int i = 0; i < n; i++)
                {
                    long begin = stream.Position;
                    var q = ReadRecord(reader);
                    if (!ids.Add(q.id)) throw new InvalidDataException("Duplicate quest ID " + q.id);
                    if (stream.Position - begin != RecordSize) throw new InvalidDataException("Quest stride mismatch.");
                    q.title = Text(tr); q.summary = Text(tr);
                    for (int r = 0; r < 6; r++) q.rewards[r].completion = Text(tr);
                    q.initial = Text(tr); q.window = Text(tr); q.reminder = Text(tr); q.alternate = Text(tr);
                    catalog.quests[i] = q;
                }
                if (stream.Position != stream.Length || texts.Position != texts.Length)
                    throw new InvalidDataException("Unconsumed quest data/translation tail; do not assume another episode's layout.");
                return catalog;
            }
        }
        private static LegacyQuestDefinition ReadRecord(BinaryReader r)
        {
            var q = new LegacyQuestDefinition();
            q.id=r.ReadUInt16(); q.minLevel=r.ReadUInt16(); q.maxLevel=r.ReadUInt16();
            q.faction=r.ReadByte(); q.mode=r.ReadByte(); q.male=r.ReadByte(); q.female=r.ReadByte();
            q.jobs=r.ReadBytes(6); q.hg=r.ReadUInt16(); q.vg=r.ReadInt16(); q.cg=r.ReadByte(); q.og=r.ReadByte(); q.ig=r.ReadByte();
            q.previousQuestId=r.ReadUInt16(); q.requireParty=r.ReadByte(); q.partyJobs=r.ReadBytes(6);
            q.minimumTime=r.ReadUInt32(); q.time=r.ReadUInt32(); q.tickStartTerm=r.ReadUInt32(); q.tickKeepTime=r.ReadUInt32(); q.tickReceiveCount=r.ReadUInt32();
            q.startType=r.ReadByte(); q.startNpcType=r.ReadByte(); q.startNpcId=r.ReadUInt16(); q.startItemType=r.ReadByte(); q.startItemId=r.ReadByte();
            q.requiredItems=Items(r);
            q.endType=r.ReadByte(); q.endNpcType=r.ReadByte(); q.endNpcId=r.ReadInt16(); q.farmItems=Items(r);
            q.pvpKills=r.ReadByte(); q.mob1=r.ReadUInt16(); q.mobCount1=r.ReadByte(); q.mob2=r.ReadUInt16(); q.mobCount2=r.ReadByte();
            q.resultType=r.ReadByte(); q.userSelect=r.ReadByte(); q.rewards=new LegacyQuestReward[6];
            for (int i=0;i<6;i++) q.rewards[i]=new LegacyQuestReward {
                needMobId=r.ReadUInt16(), needMobCount=r.ReadByte(), needItemId=r.ReadByte(), needItemCount=r.ReadByte(),
                needTime=r.ReadUInt32(), needHG=r.ReadUInt16(), needVG=r.ReadInt16(), needOG=r.ReadByte(),
                experience=r.ReadUInt32(), money=r.ReadUInt32(), items=Items(r), nextQuestId=r.ReadUInt16() };
            return q;
        }
        private static LegacyQuestItem[] Items(BinaryReader r)
        {
            var values=new LegacyQuestItem[3];
            for(int i=0;i<3;i++) values[i]=new LegacyQuestItem { type=r.ReadByte(),typeId=r.ReadByte(),count=r.ReadByte() };
            return values;
        }
        private static int Count(BinaryReader r)
        { Require(r,4); int n=r.ReadInt32(); if(n<0||n>MaxRecords) throw new InvalidDataException("Invalid quest count."); return n; }
        private static string Text(BinaryReader r)
        {
            Require(r,4);int n=r.ReadInt32();
            if(n<0||n>MaxStringBytes)throw new InvalidDataException("Invalid quest string length.");
            Require(r,n); byte[] bytes=r.ReadBytes(n);char[] chars=new char[n];
            const string windows="€\u0081‚ƒ„…†‡ˆ‰Š‹Œ\u008dŽ\u008f\u0090‘’“”•–—˜™š›œ\u009džŸ";
            for(int i=0;i<n;i++)chars[i]=bytes[i]>=128&&bytes[i]<160?windows[bytes[i]-128]:(char)bytes[i];
            return new string(chars).TrimEnd('\0'); // Preserve native wording; UI formatting is separate.
        }
        private static void Seek(Stream s,long p)
        { if(p<0||p>s.Length)throw new InvalidDataException("Invalid NPC tail offset.");s.Position=p; }
        private static void Require(BinaryReader r,long n)
        { if(n<0||n>r.BaseStream.Length-r.BaseStream.Position)throw new EndOfStreamException("Truncated quest data."); }
    }
}
