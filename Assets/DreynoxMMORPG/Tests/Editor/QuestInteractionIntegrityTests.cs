using System;
using System.Reflection;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Interaction;
using Dreynox.Mmorpg.ParityCore;
using Dreynox.Mmorpg.Quests;
using Dreynox.Mmorpg.UI;
using Dreynox.Mmorpg.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class QuestInteractionIntegrityTests
    {
        private GameObject root;
        private NativeWorldHud hud;
        private QuestWorldPanel panel;
        private LegacyNpcRuntimeDescriptor npc;
        private ShaiyaClientActor actor;
        private QuestJournalCore journal;
        private Texture2D image;
        private Sprite paper;
        private static readonly Vector3 Origin=new Vector3(35000,18000,35000);
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Set(object value,string field,object item)=>value.GetType().GetField(field,Private).SetValue(value,item);
        private static T Get<T>(object value,string field)=>(T)value.GetType().GetField(field,Private).GetValue(value);
        private static void Call(object value,string method,params object[] args)=>value.GetType().GetMethod(method,Private).Invoke(value,args);
        [SetUp]
        public void Setup()
        {
            root=new GameObject("Quest integrity fixture",typeof(RectTransform),typeof(Canvas));
            var player=new GameObject("Local actor");player.transform.SetParent(root.transform);player.transform.position=Origin;
            actor=player.AddComponent<ShaiyaClientActor>();
            var who=new GameObject("Authored NPC");who.transform.SetParent(root.transform);who.transform.position=Origin+Vector3.forward*2;
            npc=who.AddComponent<LegacyNpcRuntimeDescriptor>();
            npc.Configure(7,1081,0,0,0,0,-1,"NPC de prueba","Saludo",NpcServiceKind.Quest,null,new[]{3400},new[]{3400},null);
            var definition=new LegacyQuestDefinition{id=3400,minLevel=1,maxLevel=80,faction=2,mode=1,male=1,female=1,
                jobs=new byte[]{1,1,1,1,1,1},startType=1,startNpcType=7,startNpcId=1081,endType=2,endNpcType=7,endNpcId=1081,
                mob1=2011,mobCount1=5,resultType=1,title="Misión de prueba",initial="Texto original",window="Objetivo",
                rewards=new[]{new LegacyQuestReward{experience=5,money=3000,completion="Trabajo completado."}}};
            journal=new QuestJournalCore(new LegacyQuestCatalogData{sourceSha256="fixture",quests=new[]{definition}});
            var runtime=root.AddComponent<QuestJournalRuntime>();runtime.Configure(null,actor,null,null);
            typeof(QuestJournalRuntime).GetProperty("Journal").SetValue(runtime,journal);
            hud=root.AddComponent<NativeWorldHud>();
            Set(hud,"actor",actor);Set(hud,"selectedNpc",npc);typeof(NativeWorldHud).GetProperty("Ready").SetValue(hud,true);
            panel=root.AddComponent<QuestWorldPanel>();
            image=new Texture2D(256,512);paper=Sprite.Create(image,new Rect(0,0,256,512),Vector2.one*.5f);
            panel.Configure(hud,runtime,paper);
            Set(panel,"canvasRoot",root.GetComponent<RectTransform>());Set(panel,"font",Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            Call(panel,"BuildDialog");Call(panel,"NpcOpened",npc);
            Assert.IsTrue(panel.SelectVisibleQuest(3400));
            Physics.SyncTransforms();
        }
        [TearDown]
        public void Cleanup()
        {
            if(hud!=null)WorldInputGate.Set(hud,false);
            if(panel!=null)WorldInputGate.Set(panel,false);
            if(root!=null)Object.DestroyImmediate(root);
            if(paper!=null)Object.DestroyImmediate(paper);
            if(image!=null)Object.DestroyImmediate(image);
            Physics.SyncTransforms();
        }
        [Test]
        public void AuthoredParchmentRatioAndOpaqueBackdropPreserveLegibility()
        {
            var modal=Get<RectTransform>(panel,"modal");
            var page=modal.Find("Original quest parchment").GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(256,512),page.sizeDelta);
            Assert.AreEqual(1f,modal.GetComponent<Image>().color.a);
            Assert.AreSame(paper,page.GetComponent<Image>().sprite);
            Assert.AreEqual(Color.white,Get<Text>(panel,"narrative").color);
            Assert.IsNotNull(Get<Text>(panel,"narrative").GetComponent<Shadow>());
            Assert.IsFalse(Get<Button>(panel,"rewardCycle").gameObject.activeSelf);
        }
        [Test]
        public void VisibleAcceptanceAndDeliveryLeaveNoStaleClaimButtonOrDuplicateReward()
        {
            Assert.IsTrue(panel.SubmitSelectedQuest(),panel.ActionFailure);
            for(int i=0;i<5;i++)Assert.IsTrue(journal.CreditMobDeath(i,2011,out _));
            Assert.IsTrue(panel.SelectVisibleQuest(3400));
            Assert.IsTrue(panel.SubmitSelectedQuest(),panel.ActionFailure);
            Assert.AreEqual(3000,journal.Gold);
            Assert.AreEqual("Trabajo completado.",Get<Text>(panel,"narrative").text);
            Assert.IsFalse(Get<Button>(panel,"action").gameObject.activeSelf);
            Assert.IsFalse(Get<Button>(panel,"rewardCycle").gameObject.activeSelf);
            StringAssert.Contains("Recompensa guardada",Get<Text>(panel,"rewardText").text);
            StringAssert.DoesNotContain("Completa los objetivos",Get<Text>(panel,"rewardText").text);
            Assert.IsFalse(panel.SubmitSelectedQuest());Assert.AreEqual(3000,journal.Gold);
        }
        [Test]
        public void MovingAwayAfterOpeningCannotAcceptBeforeTheNextHudUpdate()
        {
            actor.transform.position=Origin+Vector3.right*20;Physics.SyncTransforms();
            Assert.IsFalse(panel.SubmitSelectedQuest());Assert.IsEmpty(journal.Entries);
            Assert.IsFalse(Get<RectTransform>(panel,"modal").gameObject.activeSelf);
        }
        [Test]
        public void NewObstacleInvalidatesThePreviouslyOpenedConversation()
        {
            Assert.IsTrue(LocalNpcInteractionGuard.Validate(actor,npc,hud.SelectedNpc,hud.Ready,out _));
            var wall=new GameObject("Door closed after talk");wall.transform.SetParent(root.transform);
            wall.transform.position=Origin+Vector3.forward+Vector3.up;
            wall.AddComponent<BoxCollider>().size=new Vector3(3,3,.2f);Physics.SyncTransforms();
            Assert.IsFalse(panel.SubmitSelectedQuest());Assert.IsEmpty(journal.Entries);
        }
        [Test]
        public void DespawnedNpcCannotCommitItsOldUiAction()
        {
            npc.gameObject.SetActive(false);
            Assert.IsFalse(panel.SubmitSelectedQuest());Assert.IsEmpty(journal.Entries);
        }
        [Test]
        public void DisabledQuestPanelReleasesBothDialogueAndJournalInputOwners()
        {
            WorldInputGate.Set(hud,true);WorldInputGate.Set(panel,true);
            panel.enabled=false;Call(panel,"OnDisable");
            Assert.IsNull(hud.SelectedNpc);Assert.IsFalse(WorldInputGate.IsBlocked);
        }
        private void OpenActiveJournal()
        {
            Assert.IsTrue(panel.SubmitSelectedQuest(),panel.ActionFailure);
            Call(panel,"ToggleJournal");Assert.IsTrue(panel.SelectVisibleQuest(3400));
        }
        [Test]
        public void AbandonmentRequiresConfirmationAndNeverGrantsAReward()
        {
            OpenActiveJournal();
            Assert.IsFalse(panel.ConfirmAbandon());Assert.IsTrue(journal.Entries.ContainsKey(3400));
            Assert.IsTrue(panel.RequestAbandonSelectedQuest());Assert.IsTrue(journal.Entries.ContainsKey(3400));
            panel.CancelAbandon();Assert.IsFalse(panel.ConfirmAbandon());
            Assert.IsTrue(panel.RequestAbandonSelectedQuest());Assert.IsTrue(panel.ConfirmAbandon(),panel.ActionFailure);
            Assert.IsFalse(journal.Entries.ContainsKey(3400));Assert.AreEqual(0,journal.Gold);Assert.AreEqual(0,journal.Experience);
            Assert.IsFalse(panel.ConfirmAbandon());
        }
        [Test]
        public void FailedAbandonSaveLeavesTheQuestAndAllowsExplicitRetry()
        {
            OpenActiveJournal();Assert.IsTrue(panel.RequestAbandonSelectedQuest());
            journal.Persist=_=>false;
            Assert.IsFalse(panel.ConfirmAbandon());Assert.IsTrue(journal.Entries.ContainsKey(3400));
            journal.Persist=_=>true;
            Assert.IsTrue(panel.ConfirmAbandon(),panel.ActionFailure);Assert.IsFalse(journal.Entries.ContainsKey(3400));
        }
        [Test]
        public void ClosingTheJournalInvalidatesAnOldAbandonConfirmation()
        {
            OpenActiveJournal();Assert.IsTrue(panel.RequestAbandonSelectedQuest());
            Call(panel,"CloseAll");Assert.IsFalse(panel.ConfirmAbandon());Assert.IsTrue(journal.Entries.ContainsKey(3400));
        }
        [Test]
        public void AbandonCannotOperateFromNpcDialogOrOnRewardedQuest()
        {
            Assert.IsFalse(panel.RequestAbandonSelectedQuest());
            OpenActiveJournal();
            for(int i=0;i<5;i++)journal.CreditMobDeath(i,2011,out _);
            Assert.IsTrue(panel.RequestAbandonSelectedQuest());
            Assert.IsTrue(journal.Deliver(3400,npc.ServiceKey,0,out _));
            Assert.IsFalse(panel.ConfirmAbandon());Assert.AreEqual(3000,journal.Gold);
            Assert.AreEqual(JournalStage.Rewarded,journal.Entries[3400].stage);
        }
        [Test]
        public void EscapeCancelsOnlyTopConfirmationBeforeClosingJournal()
        {
            OpenActiveJournal();Assert.IsTrue(panel.RequestAbandonSelectedQuest());
            Assert.IsTrue(panel.ProcessEscape());
            Assert.IsFalse(Get<RectTransform>(panel,"abandonConfirmation").gameObject.activeSelf);
            Assert.IsTrue(Get<RectTransform>(panel,"modal").gameObject.activeSelf);
            Assert.IsTrue(journal.Entries.ContainsKey(3400));
            Assert.IsTrue(panel.ProcessEscape());
            Assert.IsFalse(Get<RectTransform>(panel,"modal").gameObject.activeSelf);
            Assert.IsFalse(panel.ProcessEscape());Assert.IsFalse(WorldInputGate.IsBlocked);
        }
        [Test]
        public void NativeDetailAndNpcSelectorUseTheirSeparateAuthoredDimensions()
        {
            Assert.AreEqual(new Vector2(256,512),Get<RectTransform>(panel,"modal").sizeDelta);
            Call(panel,"NpcOpened",npc);
            Assert.AreEqual(new Vector2(342,229),Get<RectTransform>(panel,"modal").sizeDelta);
            Assert.IsFalse(Get<RectTransform>(panel,"paperPage").gameObject.activeSelf);
            Assert.IsTrue(panel.SelectVisibleQuest(3400));
            Assert.AreEqual(new Vector2(256,512),Get<RectTransform>(panel,"modal").sizeDelta);
            Assert.AreEqual(12,Get<Text>(panel,"narrative").fontSize);
        }
        [Test]
        public void ReusedNpcInstanceCannotAcceptUsingItsOldConversationLifetime()
        {
            using(var pool=new NpcPoolTestLease(root.transform,actor.transform,npc))
            {
                npc=pool.Current;Set(hud,"selectedNpc",npc);Call(panel,"NpcOpened",npc);
                Assert.IsTrue(panel.SelectVisibleQuest(3400));uint before=npc.LifetimeGeneration;
                Assert.AreSame(npc,pool.Recycle(),"Exercise the production same-object pool checkout.");
                Assert.AreNotEqual(before,npc.LifetimeGeneration);
                Assert.IsFalse(panel.SubmitSelectedQuest());Assert.IsEmpty(journal.Entries);
            }
        }
    }
}
