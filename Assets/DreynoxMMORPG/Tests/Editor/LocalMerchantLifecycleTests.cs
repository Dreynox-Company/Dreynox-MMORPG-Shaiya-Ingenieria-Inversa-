using System;
using System.Linq;
using Dreynox.Mmorpg.Interaction;
using Dreynox.Mmorpg.ParityCore;
using Dreynox.Mmorpg.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.Tests.Editor
{
    // Reuses the real panel/journal/NPC setup in LocalMerchantIntegrationTests.
    // These callbacks are synchronous lifecycle changes, not simulated network ACKs.
    public sealed partial class LocalMerchantIntegrationTests
    {
        [Test]
        public void SavedPurchaseSurvivesAnObserverClosingTheConversation()
        {
            OpenFromButton();merchant.SelectStock(0);merchant.SetQuantity(2);
            wallet.Changed+=()=>hud.CloseDialogue();
            bool result=false;
            Assert.DoesNotThrow(()=>result=merchant.ConfirmPending());
            Assert.IsTrue(result,"An already durable purchase must not become a failed UI result.");
            Assert.AreEqual(800,wallet.Gold);Assert.AreEqual(2,wallet.Inventory[257]);
            Assert.IsFalse(merchant.IsOpen);Assert.IsNull(merchant.Pending);
            Assert.IsFalse(merchant.ConfirmPending());Assert.IsFalse(WorldInputGate.IsBlocked);
            Assert.AreEqual(0,wallet.ObserverFailureCount);
        }
        [TestCase(false)] [TestCase(true)]
        public void ClosingDuringPersistencePreservesTheExactCommitOutcome(bool saved)
        {
            OpenFromButton();merchant.SelectStock(0);
            wallet.Persist=_=>{hud.CloseDialogue();return saved;};
            bool result=!saved;
            Assert.DoesNotThrow(()=>result=merchant.ConfirmPending());
            Assert.AreEqual(saved,result);
            Assert.AreEqual(saved?900:1000,wallet.Gold);
            Assert.AreEqual(saved?1:0,wallet.Inventory.Count);
            Assert.IsFalse(merchant.IsOpen);Assert.IsNull(merchant.Pending);
            Assert.IsFalse(WorldInputGate.IsBlocked);
        }
        [Test]
        public void ObserversCannotReplaceTheMerchantWhileItsPaymentIsCommitting()
        {
            OpenFromButton();merchant.SelectStock(0);
            bool reopened=true,changedMode=true,changedSelection=true;
            wallet.Changed+=()=>
            {
                reopened=merchant.Open(npc);
                changedMode=merchant.ShowSelling(true);
                changedSelection=merchant.SelectStock(1);
            };
            Assert.IsTrue(merchant.ConfirmPending());
            Assert.IsFalse(reopened);Assert.IsFalse(changedMode);Assert.IsFalse(changedSelection);
            Assert.IsTrue(merchant.IsOpen);Assert.IsNull(merchant.Pending);
            Assert.AreEqual(900,wallet.Gold);Assert.AreEqual(1,wallet.Inventory[257]);
            Assert.AreEqual(0,wallet.ObserverFailureCount);
        }
        [TestCase(0)] [TestCase(-1)] [TestCase(256)] [TestCase(1000)]
        public void InvalidQuantityCannotLeaveAnEarlierPricePayable(int invalid)
        {
            OpenFromButton();Assert.IsTrue(merchant.SelectStock(0));Assert.IsNotNull(merchant.Pending);
            Assert.IsFalse(merchant.SetQuantity(invalid));Assert.IsNull(merchant.Pending);
            Assert.IsFalse(Get<Button>(merchant,"confirm").interactable);
            Assert.IsFalse(merchant.ConfirmPending());Assert.AreEqual(1000,wallet.Gold);
            Assert.IsEmpty(wallet.Inventory);
            Assert.IsTrue(merchant.SetQuantity(2));Assert.AreEqual(200,merchant.Pending.Total);
            Assert.IsTrue(merchant.ConfirmPending());Assert.AreEqual(800,wallet.Gold);
        }
        [Test]
        public void SelectingStockFromSellingModeRestoresItsOriginalBuyFrame()
        {
            OpenFromButton();merchant.ShowSelling(true);
            Assert.IsTrue(merchant.SelectStock(1));
            var window=Get<RectTransform>(merchant,"window");
            Assert.AreEqual(new Vector2(256,355),window.rect.size);
            Assert.AreEqual(new Vector2(15,-71),window.Find("Merchant item 0").GetComponent<RectTransform>().anchoredPosition);
            Assert.AreEqual(6401,merchant.Pending.ItemKey);
            Assert.AreEqual(130,merchant.Pending.Total);
        }
        [Test]
        public void SelectingOwnedItemFromBuyingModeUsesOriginalSellFrame()
        {
            wallet.ApplyInventoryTransaction(new System.Collections.Generic.Dictionary<int,int>{{257,1}},out _);
            OpenFromButton();Assert.IsTrue(merchant.SelectOwnedItem(257));
            Assert.AreEqual(new Vector2(256,368),Get<RectTransform>(merchant,"window").rect.size);
            Assert.AreEqual(10,merchant.Pending.Total);
        }
        [Test]
        public void SelectingAnAuthoredStockIndexOnAnotherPageDisplaysThatPage()
        {
            Set(npc,"saleItems",Enumerable.Repeat(new LegacyNpcSaleItemRuntime{type=1,typeId=1},33).ToArray());
            OpenFromButton();Assert.IsTrue(merchant.SelectStock(32));
            Assert.AreEqual("2 / 2",Get<Text>(merchant,"pageLabel").text);
            Assert.AreEqual(3,merchant.VisibleCount);
            Assert.AreEqual(257,merchant.Pending.ItemKey);
        }
        [Test]
        public void UnsupportedMerchantDoesNotClaimTheServiceOpened()
        {
            Set(npc,"merchantType",99);
            Assert.IsFalse(quests.RequestMerchant());Assert.IsFalse(merchant.IsOpen);
            Assert.IsNotEmpty(merchant.Failure);Assert.IsNotEmpty(quests.ActionFailure);
            Assert.IsTrue(Get<RectTransform>(quests,"modal").gameObject.activeSelf);
            Assert.AreSame(npc,hud.SelectedNpc);Assert.AreEqual(1000,wallet.Gold);
        }
        [Test]
        public void DisabledProviderDoesNotClaimTheServiceOpened()
        {
            merchant.enabled=false;
            Assert.IsFalse(quests.RequestMerchant());Assert.IsFalse(merchant.IsOpen);
            Assert.AreEqual(1000,wallet.Gold);Assert.IsEmpty(wallet.Inventory);
        }
        [Test]
        public void AmbiguousServiceProvidersAreRejectedBeforeAnyShopOpens()
        {
            bool secondCalled=false;
            quests.MerchantRequested+=_=>{secondCalled=true;return true;};
            Assert.IsFalse(quests.RequestMerchant());Assert.IsFalse(secondCalled);
            Assert.IsFalse(merchant.IsOpen);Assert.AreEqual(1000,wallet.Gold);
        }
        [Test]
        public void ANewConversationClosesOnlyThePreviousMerchantAndItsQuote()
        {
            OpenFromButton();merchant.SelectStock(0);
            var nextObject=new GameObject("New original NPC context");nextObject.transform.SetParent(root.transform);
            var nextNpc=nextObject.AddComponent<LegacyNpcRuntimeDescriptor>();
            nextNpc.Configure(7,1081,0,0,0,0,-1,"Otro NPC","Otro saludo",NpcServiceKind.Quest,null,null,null,null);
            // Raise the same subscribed public event with the HUD's new context.
            // The fixture does not execute NativeWorldHud.Start or invent world raycasts.
            Set(hud,"selectedNpc",nextNpc);
            Get<Action<LegacyNpcRuntimeDescriptor>>(hud,"DialogueOpened").Invoke(nextNpc);
            Call(quests,"NpcOpened",nextNpc);
            Assert.IsFalse(merchant.IsOpen);Assert.IsNull(merchant.Pending);
            Assert.AreSame(nextNpc,hud.SelectedNpc);
            Assert.IsTrue(Get<RectTransform>(quests,"modal").gameObject.activeSelf);
            Assert.AreEqual(1000,wallet.Gold);
        }
        [Test]
        public void MerchantClosurePreservesAnotherWindowsInputOwnership()
        {
            OpenFromButton();WorldInputGate.Set(quests,true);
            hud.CloseDialogue();Assert.IsFalse(merchant.IsOpen);
            Assert.IsTrue(WorldInputGate.IsBlocked,"This panel must not reset the global owner set.");
            WorldInputGate.Set(quests,false);Assert.IsFalse(WorldInputGate.IsBlocked);
        }
        [Test]
        public void ConfirmationRemainsCenteredWhenTheViewportChanges()
        {
            OpenFromButton();merchant.SelectStock(0);
            var backdrop=Get<RectTransform>(merchant,"confirmation");
            var box=backdrop.Find("Merchant confirmation").GetComponent<RectTransform>();
            Assert.AreEqual(Vector2.one*.5f,box.anchorMin);
            Assert.AreEqual(Vector2.one*.5f,box.anchorMax);
            Assert.AreEqual(Vector2.one*.5f,box.pivot);
            foreach(var size in new[]{new Vector2(1001,731),new Vector2(801,601)})
            {
                root.GetComponent<RectTransform>().sizeDelta=size;Canvas.ForceUpdateCanvases();
                Assert.Less(Vector3.Distance(box.TransformPoint(box.rect.center),backdrop.TransformPoint(backdrop.rect.center)),.001f);
            }
        }
    }
}
