using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Interaction;
using Dreynox.Mmorpg.NativeContent;
using Dreynox.Mmorpg.Quests;
using Dreynox.Mmorpg.UI;
using Dreynox.Mmorpg.World;
using UnityEngine;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.Commerce
{
    /// <summary>Actual local purchase/sale after the original quest reward; never seeds extra money/items.</summary>
    public static class LocalMerchantQualification
    {
        [Serializable] private sealed class Evidence
        {
            public string scope="original-merchant-stock-local-base-gold-commerce-not-native-server-economy";
            public bool passed,scriptedInput=true,relocatedToMerchant=true,duplicateRejected,inventoryReflected,otherStateUnchanged,inputReleased;
            public string npcName,itemName,failure="";
            public int npcType,npcTypeId,stockIndex,itemKey,bought=2,sold=1,countBefore,countAfter,revisionBefore,revisionAfter;
            public long unitBuy,unitSell,goldBefore,goldAfter;
        }
        private sealed class Candidate
        {
            public LegacyNpcSpawnDefinition Spawn;
            public int StockIndex;
            public LocalMerchantOffer Offer;
        }
        public static IEnumerator Run(NativeWorldHud hud,QuestJournalRuntime journal,LegacyNpcSpawnStreamer npcs,
            Func<Vector3,bool> placeNear,string output,Func<string,IEnumerator> capture)
        {
            var evidence=new Evidence();var merchant=journal.GetComponent<LocalMerchantPanel>();
            var content=journal.GetComponent<NativeContentPanel>();
            var quests=journal.GetComponent<QuestWorldPanel>();
            try
            {
                if(merchant==null||!merchant.Ready||content==null||!content.Ready||quests==null)
                    throw new InvalidOperationException("Real merchant/catalog/quest UI bindings are absent.");
                var candidates=new List<Candidate>();
                foreach(var spawn in npcs.Spawns)
                {
                    if(spawn.npcType!=1||spawn.prefab==null||!NativeMerchantOfferFactory.SupportsMerchantType(spawn.merchantType))continue;
                    for(int index=0;index<spawn.saleItems.Length&&index<=255;index++)
                    {
                        var raw=spawn.saleItems[index];var offer=NativeMerchantOfferFactory.Resolve(content.Items,(raw.type<<8)|raw.typeId);
                        if(offer==null||offer.Restriction.Length>0||offer.Buy<=0||offer.Sell<=0||offer.Sell>offer.Buy||
                            offer.Buy>uint.MaxValue||offer.Buy>journal.Journal.Gold/2)continue;
                        if(!content.Items.TryItem(offer.ItemKey,out var entry)||!content.Items.TryIcon(entry,out _,out _))continue;
                        candidates.Add(new Candidate{Spawn=spawn,StockIndex=index,Offer=offer});break;
                    }
                }
                var ordered=candidates.OrderBy(c=>(c.Spawn.position-journal.Actor.transform.position).sqrMagnitude)
                    .ThenBy(c=>c.Spawn.typeId).ThenBy(c=>c.StockIndex).ToArray();
                Candidate chosen=null;LegacyNpcRuntimeDescriptor npc=null;
                // Each attempt still uses the actual world collision and conversation guard.
                foreach(var candidate in ordered.Take(12))
                {
                    if(!placeNear(candidate.Spawn.position))continue;
                    yield return new WaitForSeconds(1);
                    npc=candidate.Spawn.activeInstance!=null?candidate.Spawn.activeInstance.GetComponent<LegacyNpcRuntimeDescriptor>():null;
                    if(npc==null||!hud.TryTalk(npc))continue;
                    chosen=candidate;break;
                }
                if(chosen==null)throw new InvalidOperationException("No original Map1 gold merchant could be reached with earned quest funds.");
                var before=journal.Journal.Snapshot();evidence.goldBefore=before.gold;evidence.revisionBefore=before.revision;
                evidence.npcType=npc.NpcType;evidence.npcTypeId=npc.TypeId;evidence.npcName=npc.DisplayName;
                evidence.stockIndex=chosen.StockIndex;evidence.itemKey=chosen.Offer.ItemKey;evidence.itemName=chosen.Offer.Name;
                evidence.unitBuy=chosen.Offer.Buy;evidence.unitSell=chosen.Offer.Sell;
                journal.Journal.Inventory.TryGetValue(evidence.itemKey,out evidence.countBefore);
                var service=hud.CanvasRoot.GetComponentsInChildren<Button>()
                    .SingleOrDefault(b=>b.name=="Original merchant service action");
                if(service==null||!service.interactable)throw new InvalidOperationException("NPC merchant service is not visibly available.");
                service.onClick.Invoke();
                if(!merchant.IsOpen||merchant.CurrentNpc!=npc)throw new InvalidOperationException("Visible NPC service did not open its merchant.");
                yield return capture("06-original-merchant-stock");
                if(!merchant.SelectStock(evidence.stockIndex)||!merchant.SetQuantity(2)||merchant.Pending==null||merchant.Pending.Total!=evidence.unitBuy*2)
                    throw new InvalidOperationException("Original item cannot be quoted with its source base price.");
                yield return capture("06-original-merchant-buy-quote");
                var confirm=hud.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="Confirm merchant exchange");
                confirm.onClick.Invoke();
                if(journal.Journal.Gold!=evidence.goldBefore-2*evidence.unitBuy||
                    !journal.Journal.Inventory.TryGetValue(evidence.itemKey,out int purchased)||purchased!=evidence.countBefore+2)
                    throw new InvalidOperationException("Actual purchase button did not atomically exchange earned gold for owned items.");
                evidence.duplicateRejected=!merchant.ConfirmPending();
                merchant.ShowSelling(true);
                if(!merchant.SelectOwnedItem(evidence.itemKey)||!merchant.SetQuantity(1))throw new InvalidOperationException("Purchased item is not sellable through the live inventory.");
                yield return capture("07-original-merchant-sell-quote");
                hud.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="Confirm merchant exchange").onClick.Invoke();
                var after=journal.Journal.Snapshot();evidence.goldAfter=after.gold;evidence.revisionAfter=after.revision;
                journal.Journal.Inventory.TryGetValue(evidence.itemKey,out evidence.countAfter);
                if(after.gold!=before.gold-2*evidence.unitBuy+evidence.unitSell||evidence.countAfter!=evidence.countBefore+1||after.revision!=before.revision+2)
                    throw new InvalidOperationException("Sale or journal revision differs from the exact base-price transaction.");
                evidence.duplicateRejected&=!merchant.ConfirmPending();
                var expected=new Dictionary<int,int>();foreach(var item in before.items)expected.Add(item.key,item.count);
                expected[evidence.itemKey]=evidence.countBefore+1;
                evidence.otherStateUnchanged=after.experience==before.experience&&after.quests.Length==before.quests.Length&&
                    after.quests.All(q=>before.quests.Any(p=>p.id==q.id&&p.stage==q.stage&&p.kills1==q.kills1&&p.kills2==q.kills2))&&
                    after.items.Length==expected.Count&&after.items.All(i=>expected.TryGetValue(i.key,out int count)&&i.count==count);
                merchant.ProcessEscape();
                if(!content.OpenInventory())throw new InvalidOperationException("Inventory did not reopen after commerce.");
                evidence.inventoryReflected=content.VisibleInventoryTypes==expected.Count;
                yield return capture("08-inventory-after-real-local-trade");
                content.Close();hud.CloseDialogue();evidence.inputReleased=!WorldInputGate.IsBlocked;
                if(!evidence.duplicateRejected||!evidence.otherStateUnchanged||!evidence.inventoryReflected||!evidence.inputReleased)
                    throw new InvalidOperationException("Local merchant isolation, inventory integration or input ownership failed.");
                evidence.passed=true;
            }
            finally
            {
                if(merchant!=null)merchant.Close();if(content!=null)content.Close();hud.CloseDialogue();
                if(!evidence.passed)evidence.failure="Merchant qualification interrupted; parent report retains the original exception.";
                File.WriteAllText(Path.Combine(output,"local-merchant-qualification.json"),JsonUtility.ToJson(evidence,true));
            }
        }
    }
}
