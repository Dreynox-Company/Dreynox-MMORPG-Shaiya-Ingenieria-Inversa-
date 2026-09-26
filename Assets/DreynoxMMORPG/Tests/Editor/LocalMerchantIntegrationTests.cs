using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Dreynox.Mmorpg.Commerce;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Interaction;
using Dreynox.Mmorpg.NativeContent;
using Dreynox.Mmorpg.ParityCore;
using Dreynox.Mmorpg.Quests;
using Dreynox.Mmorpg.UI;
using Dreynox.Mmorpg.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LocalMerchantIntegrationTests
    {
        private GameObject root;
        private ShaiyaClientActor actor;
        private LegacyNpcRuntimeDescriptor npc;
        private NativeWorldHud hud;
        private QuestWorldPanel quests;
        private LocalMerchantPanel merchant;
        private QuestJournalCore wallet;
        private readonly List<Object> assets=new List<Object>();
        private static readonly Vector3 Origin=new Vector3(36000,18000,36000);
        private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Set(object value,string name,object data)=>value.GetType().GetField(name,Hidden).SetValue(value,data);
        private static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name,Hidden).GetValue(value);
        private static void Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Hidden).Invoke(value,args);
        private Texture2D Texture(int width,int height){var t=new Texture2D(width,height);assets.Add(t);return t;}
        private NativeCatalogAsset Catalog()
        {
            string[] fields={"itemtype","itemtypeid","level","buy","sell","buymethod","moneytype","duration","extduration","itemupgrade","spellbookdurability"};
            long[][] rows={new long[]{1,1,1,100,10,0,0,0,0,0,0},new long[]{25,1,0,130,25,0,0,0,0,0,0}};
            byte[] numbers,text;
            // Labelled fixture only; preserve the shared catalog's required schema.
            string[] complete=fields.Concat(new[]{"image","icon"}).ToArray();
            using(var m=new MemoryStream())
            {
                using(var w=new BinaryWriter(m,Encoding.UTF8,true))
                {
                    w.Write(new byte[128]);w.Write(complete.Length);
                    foreach(string f in complete){w.Write((byte)f.Length);w.Write(Encoding.Unicode.GetBytes(f));}
                    w.Write(rows.Length);foreach(var r in rows){foreach(long n in r)w.Write(n);w.Write(0L);w.Write(1L);}
                }
                numbers=m.ToArray();
            }
            using(var m=new MemoryStream())
            {
                using(var w=new BinaryWriter(m,Encoding.UTF8,true))
                {
                    w.Write(new byte[128]);w.Write(4);
                    foreach(string f in new[]{"itemtype","itemtypeid","itemname","text"}){w.Write((byte)f.Length);w.Write(Encoding.Unicode.GetBytes(f));}
                    w.Write(2);
                    for(int i=0;i<2;i++)
                    {
                        w.Write(rows[i][0]);w.Write(1L);
                        foreach(string t in new[]{i==0?"Espada de prueba":"Manzana de prueba","Fixture de comercio local, no fórmula nativa."})
                        {byte[] b=NativeWindows1252.Encode(t);w.Write(b.Length);w.Write(b);}
                    }
                }
                text=m.ToArray();
            }
            var defs=new NativeDefinitionCatalog(NativeDataTable.ReadNumeric(numbers),NativeDataTable.ReadText(text),false);
            var entries=defs.Entries.Select(d=>new NativeCatalogEntry(d,complete,0,new NativeIconRegion("fixture",0,0),"")).ToArray();
            var result=ScriptableObject.CreateInstance<NativeCatalogAsset>();assets.Add(result);
            result.Configure(false,complete,entries,new[]{Texture(32,32)},new string('a',64),new string('b',64));return result;
        }
        [SetUp] public void Setup()
        {
            root=new GameObject("Merchant integration fixture",typeof(RectTransform),typeof(Canvas));
            root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;root.GetComponent<RectTransform>().sizeDelta=new Vector2(1024,768);
            var a=new GameObject("Local actor");a.transform.SetParent(root.transform);a.transform.position=Origin;actor=a.AddComponent<ShaiyaClientActor>();
            var n=new GameObject("Original stock fixture");n.transform.SetParent(root.transform);n.transform.position=Origin+Vector3.forward*2;
            npc=n.AddComponent<LegacyNpcRuntimeDescriptor>();
            npc.Configure(1,10,0,0,0,0,0,"Comerciante de prueba","Saludo",NpcServiceResolverCore.Resolve(1,false),
                new[]{new LegacyNpcSaleItemRuntime{type=1,typeId=1},new LegacyNpcSaleItemRuntime{type=25,typeId=1}},null,null,null);
            wallet=new QuestJournalCore(new LegacyQuestCatalogData{sourceSha256="fixture",quests=Array.Empty<LegacyQuestDefinition>()});
            var state=wallet.Snapshot();state.gold=1000;wallet.Restore(state);
            var runtime=root.AddComponent<QuestJournalRuntime>();runtime.Configure(null,actor,null,null);typeof(QuestJournalRuntime).GetProperty("Journal").SetValue(runtime,wallet);
            var catalog=Catalog();runtime.SetItemCatalog(catalog);
            hud=root.AddComponent<NativeWorldHud>();Set(hud,"actor",actor);Set(hud,"selectedNpc",npc);Set(hud,"canvasRoot",root.GetComponent<RectTransform>());
            typeof(NativeWorldHud).GetProperty("Ready").SetValue(hud,true);
            var skin=ScriptableObject.CreateInstance<NativeHudSkin>();assets.Add(skin);var atlas=Texture(128,32);var states=new Sprite[4];
            for(int i=0;i<4;i++){states[i]=Sprite.Create(atlas,new Rect(32*i,0,32,32),Vector2.one*.5f);assets.Add(states[i]);}
            skin.command=skin.scrollUp=skin.scrollDown=states;skin.scrollTop=skin.scrollMiddle=skin.scrollBottom=states[0];hud.SetPresentationSkin(skin);
            var paper=Sprite.Create(Texture(256,512),new Rect(0,0,256,512),Vector2.one*.5f);assets.Add(paper);
            quests=root.AddComponent<QuestWorldPanel>();quests.Configure(hud,runtime,paper);
            Set(quests,"canvasRoot",root.GetComponent<RectTransform>());Set(quests,"font",NativeUiPrimitives.Font);Call(quests,"BuildDialog");
            merchant=root.AddComponent<LocalMerchantPanel>();merchant.Configure(hud,quests,runtime,catalog,Texture(256,512),Texture(256,512));merchant.Initialize();
            Call(quests,"NpcOpened",npc);Physics.SyncTransforms();
        }
        [TearDown] public void Cleanup()
        {
            if(merchant!=null)merchant.Close();if(hud!=null)hud.CloseDialogue();if(quests!=null)WorldInputGate.Set(quests,false);
            if(root!=null)Object.DestroyImmediate(root);foreach(var a in assets)if(a!=null)Object.DestroyImmediate(a);assets.Clear();Physics.SyncTransforms();
        }
        private void OpenFromButton()
        {
            var button=root.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Original merchant service action");
            button.onClick.Invoke();Assert.IsTrue(merchant.IsOpen,merchant.Failure);
        }
        [Test] public void ActualNpcServiceButtonOpensOnlyItsOriginalStock()
        {
            OpenFromButton();Assert.AreEqual(2,merchant.StockCount);Assert.AreEqual(2,merchant.VisibleCount);
            Assert.IsFalse(Get<RectTransform>(quests,"modal").gameObject.activeSelf);
            Assert.AreSame(npc,hud.SelectedNpc);Assert.IsTrue(WorldInputGate.IsBlocked);
        }
        [Test] public void ConfirmButtonsPurchaseAndSellAgainstTheSingleLiveWallet()
        {
            OpenFromButton();Assert.IsTrue(merchant.SelectStock(0));Assert.IsTrue(merchant.SetQuantity(2));
            Get<Button>(merchant,"confirm").onClick.Invoke();Assert.AreEqual(800,wallet.Gold);Assert.AreEqual(2,wallet.Inventory[257]);
            Assert.IsFalse(merchant.ConfirmPending());Assert.IsTrue(merchant.SelectOwnedItem(257));
            Assert.IsTrue(merchant.ConfirmPending());Assert.AreEqual(810,wallet.Gold);Assert.AreEqual(1,wallet.Inventory[257]);
        }
        [Test] public void CancellingTheQuoteRetainsTheShopButNeverSpends()
        {
            OpenFromButton();merchant.SelectStock(0);Assert.IsTrue(merchant.ProcessEscape());Assert.IsTrue(merchant.IsOpen);
            Assert.IsNull(merchant.Pending);Assert.AreEqual(1000,wallet.Gold);Assert.IsTrue(WorldInputGate.IsBlocked);
            Assert.IsTrue(merchant.ProcessEscape());Assert.IsFalse(merchant.IsOpen);Assert.IsFalse(WorldInputGate.IsBlocked);
        }
        [Test] public void ChangedInventoryCannotConfirmAnOldQuotation()
        {
            OpenFromButton();merchant.SelectStock(0);wallet.ApplyInventoryTransaction(new Dictionary<int,int>{{6401,1}},out _);
            Assert.IsNull(merchant.Pending);Assert.IsFalse(merchant.ConfirmPending());Assert.AreEqual(1000,wallet.Gold);
        }
        [Test] public void FailedPersistenceKeepsTheQuoteForExplicitRetryAndBothBalancesUnchanged()
        {
            OpenFromButton();merchant.SelectStock(0);wallet.Persist=_=>false;
            Assert.IsFalse(merchant.ConfirmPending());Assert.AreEqual(1000,wallet.Gold);Assert.IsEmpty(wallet.Inventory);
            wallet.Persist=_=>true;Assert.IsTrue(merchant.ConfirmPending(),merchant.Failure);Assert.AreEqual(900,wallet.Gold);
        }
        [Test] public void MovingAwayInvalidatesTradeAtClickTime()
        {
            OpenFromButton();merchant.SelectStock(0);actor.transform.position=Origin+Vector3.right*20;Physics.SyncTransforms();
            Assert.IsFalse(merchant.ConfirmPending());Assert.AreEqual(1000,wallet.Gold);Assert.IsEmpty(wallet.Inventory);Assert.IsFalse(merchant.IsOpen);
        }
        [Test] public void SamePooledNpcObjectCannotReuseTheOldConversationAfterRespawn()
        {
            OpenFromButton();WorldInputGate.Set(hud,true);merchant.SelectStock(0);uint before=npc.LifetimeGeneration;
            npc.gameObject.SetActive(false);npc.gameObject.SetActive(true);Assert.AreNotEqual(before,npc.LifetimeGeneration);
            Assert.IsFalse(merchant.ConfirmPending());Assert.AreEqual(1000,wallet.Gold);Assert.IsEmpty(wallet.Inventory);Assert.IsFalse(WorldInputGate.IsBlocked);
        }
        [Test] public void ANewWallBlocksPreviouslyOpenedMerchantActions()
        {
            OpenFromButton();merchant.SelectStock(0);
            var wall=new GameObject("Door");wall.transform.SetParent(root.transform);wall.transform.position=Origin+Vector3.forward+Vector3.up;
            wall.AddComponent<BoxCollider>().size=new Vector3(3,3,.2f);Physics.SyncTransforms();
            Assert.IsFalse(merchant.ConfirmPending());Assert.AreEqual(1000,wallet.Gold);
        }
        [Test] public void ClosingNpcDialogueReleasesMerchantModalAndPendingTrade()
        {
            OpenFromButton();merchant.SelectStock(0);hud.CloseDialogue();
            Assert.IsFalse(merchant.IsOpen);Assert.IsNull(merchant.Pending);Assert.IsFalse(WorldInputGate.IsBlocked);Assert.AreEqual(1000,wallet.Gold);
        }
        [Test] public void BuyingAndSellingUseTheirOwnUnstretchedSourceCellCoordinates()
        {
            OpenFromButton();var w=Get<RectTransform>(merchant,"window");
            Assert.AreEqual(new Vector2(256,355),w.rect.size);
            Assert.AreEqual(new Vector2(15,-71),w.Find("Merchant item 0").GetComponent<RectTransform>().anchoredPosition);
            merchant.ShowSelling(true);Assert.AreEqual(new Vector2(256,368),w.rect.size);
            Assert.AreEqual(new Vector2(18,-42),w.Find("Merchant item 0").GetComponent<RectTransform>().anchoredPosition);
            Assert.AreEqual(new Rect(0,1-368f/512,1,368f/512),Get<RawImage>(merchant,"frame").uvRect);
        }
    }
}
