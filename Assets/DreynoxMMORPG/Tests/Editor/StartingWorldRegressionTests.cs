using System;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.Editor.Quests;
using Dreynox.Mmorpg.Quests;
using Dreynox.Mmorpg.World;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class StartingWorldRegressionTests
    {
        [Test]
        public void CanonicalQuestTailAndSpanishTranslationsConsumeTheEntireCorpus()
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();
            if(corpus==null)Assert.Ignore("Requires original ps0032 DATA; absence is not a pass.");
            byte[] definitions=LegacySDataDecryptor.Decrypt(corpus.Resolve("DATA_Español/npc/npcquest.sdata"),true).Plaintext;
            byte[] text=LegacySDataDecryptor.Decrypt(corpus.Resolve("DATA_Español/npc/npcquesttrans_spain.sdata"),true).Plaintext;
            var headers=LegacyNpcQuestHeaderParser.ParsePlain(definitions);
            var tr=LegacyNpcQuestTranslationParser.ParsePlain(text,headers);
            var catalog=LegacyQuestCatalogParser.Parse(definitions,headers.HeaderBytesConsumed,text,tr.NpcTranslationBytesConsumed);
            Assert.AreEqual(4085,catalog.quests.Length);
            var first=catalog.quests.Single(q=>q.id==3400);
            Assert.AreEqual("Nuevos Comienzos",first.title);
            Assert.AreEqual(7,first.startNpcType);Assert.AreEqual(1081,first.startNpcId);
            Assert.AreEqual(2011,first.mob1);Assert.AreEqual(5,first.mobCount1);
            Assert.AreEqual(3000,first.rewards[0].money);Assert.AreEqual(5,first.rewards[0].experience);
            Assert.IsTrue(first.initial.Contains("zorros"));
            var journal=new QuestJournalCore(catalog);
            Assert.AreEqual("",journal.Availability(3400,first.StartNpcKey,new QuestPlayerContext(1,0,0,0,2)));
        }
        [Test]
        public void CanonicalMap1ContainsTheActualStartingNpcsAndFoxes()
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();if(corpus==null)Assert.Ignore("Requires canonical DATA.");
            var map=LegacySvmapParser.Parse(corpus.Resolve("DATA_Español/world/1.svmap"));
            Assert.AreEqual(11,map.Portals.Count);Assert.AreEqual(502,map.MonsterAreas.Count);Assert.AreEqual(1186,map.MonsterInstanceCount);
            Assert.IsTrue(map.Npcs.Any(n=>n.NpcType==7&&n.NpcId==1081));
            Assert.IsTrue(map.Npcs.Any(n=>n.NpcType==7&&n.NpcId==1167));
            var wld=LegacyWldTerrainParser.Parse(corpus.Resolve("DATA_Español/world/1.wld"));
            Assert.AreEqual(8,wld.Textures.Count);Assert.AreEqual(0,wld.UnparsedTailBytes);
            Assert.AreEqual(13880,wld.RawHeightAt(290,880));
        }
        [Test]
        public void QuestParserRejectsTruncatedLinksAndMismatchedTranslationCounts()
        {
            Assert.Throws<EndOfStreamException>(()=>LegacyQuestCatalogParser.Parse(new byte[12],0,new byte[4],0));
            var empty=new byte[65536*8+4];
            Assert.AreEqual(0,LegacyQuestCatalogParser.Parse(empty,0,new byte[4],0).quests.Length);
            var wrong=new byte[4];wrong[0]=1;
            Assert.Throws<InvalidDataException>(()=>LegacyQuestCatalogParser.Parse(empty,0,wrong,0));
        }
        [Test]
        public void QuestParserRejectsTailGarbageAndDuplicateIds()
        {
            var empty=new byte[65536*8+5];
            Assert.Throws<InvalidDataException>(()=>LegacyQuestCatalogParser.Parse(empty,0,new byte[4],0));
            var two=new byte[65536*8+4+2*LegacyQuestCatalogParser.RecordSize];two[65536*8]=2;
            var tr=new byte[4+2*12*4];tr[0]=2;
            Assert.Throws<InvalidDataException>(()=>LegacyQuestCatalogParser.Parse(two,0,tr,0));
        }
        [Test]
        public void GroundPlacementSelectsWalkablePlatformNearAuthoredHeight()
        {
            Vector3 origin=new Vector3(40000,10000,40000);
            var bodyObject=new GameObject("placement fixture");bodyObject.transform.position=origin;
            var body=bodyObject.AddComponent<CharacterController>();body.height=1.8f;body.center=Vector3.up*.9f;body.radius=.35f;
            var lower=new GameObject("terrain substitute");lower.transform.position=origin+Vector3.down*.5f;lower.AddComponent<BoxCollider>().size=new Vector3(20,1,20);
            var upper=new GameObject("authored raised floor");upper.transform.position=origin+Vector3.up*4.5f;upper.AddComponent<BoxCollider>().size=new Vector3(20,1,20);
            try
            {
                Physics.SyncTransforms();
                Assert.IsTrue(WorldGroundPlacement.TryFind(body,origin+Vector3.up*5,out var position,out var reason,horizontalRadius:0,verticalTolerance:7),reason);
                Assert.That(position.y-origin.y,Is.InRange(5.04f,5.2f));
                Assert.AreEqual(origin,body.transform.position,"Query must not mutate the player.");
            }
            finally{Object.DestroyImmediate(bodyObject);Object.DestroyImmediate(lower);Object.DestroyImmediate(upper);}
        }
        [Test]
        public void MissingGroundDoesNotTeleportTheCharacter()
        {
            var go=new GameObject("unsupported placement");go.transform.position=new Vector3(40000,20000,40000);
            var body=go.AddComponent<CharacterController>();Vector3 before=go.transform.position;
            try{Assert.IsFalse(WorldGroundPlacement.TryFind(body,before,out _,out _,horizontalRadius:0,verticalTolerance:2));Assert.AreEqual(before,go.transform.position);}
            finally{Object.DestroyImmediate(go);}
        }
        [Test]
        public void AtomicRewardCannotBeClaimedOnDiskFailureOrClaimedTwice()
        {
            var quest=Fixture();var journal=new QuestJournalCore(new LegacyQuestCatalogData{sourceSha256="fixture",quests=new[]{quest}});
            Assert.IsTrue(journal.Accept(3400,quest.StartNpcKey,new QuestPlayerContext(1,0,0,0,2),out _));
            for(int i=0;i<5;i++)Assert.IsTrue(journal.CreditMobDeath(i+1,2011,out _));
            Assert.IsFalse(journal.CreditMobDeath(5,2011,out _));
            journal.Persist=_=>false;Assert.IsFalse(journal.Deliver(3400,quest.EndNpcKey,0,out _));Assert.AreEqual(0,journal.Gold);
            journal.Persist=_=>true;Assert.IsTrue(journal.Deliver(3400,quest.EndNpcKey,0,out _));Assert.AreEqual(3000,journal.Gold);
            Assert.IsFalse(journal.Deliver(3400,quest.EndNpcKey,0,out _));Assert.AreEqual(3000,journal.Gold);
        }
        [Test]
        public void UnsupportedTutorialCannotBeAcceptedAsAnEmptyCompletedQuest()
        {
            var q=Fixture();q.endType=4;q.mobCount1=0;
            var core=new QuestJournalCore(new LegacyQuestCatalogData{quests=new[]{q}});
            Assert.IsFalse(core.Accept(3400,q.StartNpcKey,new QuestPlayerContext(1,0,0,0,2),out var reason));
            Assert.IsTrue(reason.Contains("tutorial"));Assert.AreEqual(0,core.Entries.Count);
        }
        private static LegacyQuestDefinition Fixture()=>new LegacyQuestDefinition{
            id=3400,minLevel=1,maxLevel=80,male=1,female=1,jobs=new byte[]{1,1,1,1,1,1},faction=2,
            startType=1,startNpcType=7,startNpcId=1081,endType=2,endNpcType=7,endNpcId=1081,mob1=2011,mobCount1=5,resultType=1,
            rewards=new[]{new LegacyQuestReward{money=3000,experience=5,items=Array.Empty<LegacyQuestItem>()}}};
    }
}
