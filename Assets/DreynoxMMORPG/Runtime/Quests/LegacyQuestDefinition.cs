using System;

namespace Dreynox.Mmorpg.Quests
{
    [Serializable] public sealed class LegacyQuestItem
    {
        public byte type, typeId, count;
        public int Key => (type << 8) | typeId;
    }
    [Serializable] public sealed class LegacyQuestReward
    {
        public ushort needMobId, needHG, nextQuestId;
        public byte needMobCount, needItemId, needItemCount, needOG;
        public short needVG;
        public uint needTime, experience, money;
        public LegacyQuestItem[] items = Array.Empty<LegacyQuestItem>();
        public string completion = "";
    }
    /// <summary>Lossless EP8/ps0032 record. Undecoded semantics stay explicit, not silently defaulted.</summary>
    [Serializable] public sealed class LegacyQuestDefinition
    {
        public ushort id, minLevel, maxLevel, hg, previousQuestId, startNpcId, mob1, mob2;
        public short vg, endNpcId;
        public byte faction, mode, male, female, cg, og, ig, requireParty;
        public byte startType, startNpcType, startItemType, startItemId, endType, endNpcType;
        public byte pvpKills, mobCount1, mobCount2, resultType, userSelect;
        public byte[] jobs = new byte[6], partyJobs = new byte[6];
        public uint minimumTime, time, tickStartTerm, tickKeepTime, tickReceiveCount;
        public LegacyQuestItem[] requiredItems = Array.Empty<LegacyQuestItem>();
        public LegacyQuestItem[] farmItems = Array.Empty<LegacyQuestItem>();
        public LegacyQuestReward[] rewards = Array.Empty<LegacyQuestReward>();
        public string title = "", summary = "", initial = "", window = "", reminder = "", alternate = "";
        public int StartNpcKey => (startNpcType << 16) | startNpcId;
        public int EndNpcKey => (endNpcType << 16) | (ushort)endNpcId;
    }
    [Serializable] public sealed class LegacyQuestCatalogData
    {
        public string sourceSha256 = "", translationSha256 = "";
        public LegacyQuestDefinition[] quests = Array.Empty<LegacyQuestDefinition>();
    }
}
