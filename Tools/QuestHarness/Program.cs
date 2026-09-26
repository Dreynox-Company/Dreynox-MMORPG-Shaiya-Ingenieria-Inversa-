using Dreynox.Mmorpg.Quests;
using Dreynox.Mmorpg.Editor.Quests;

int checks=0;
void Check(bool ok,string text){if(!ok)throw new Exception("FAIL: "+text);Console.WriteLine("PASS "+text);checks++;}
LegacyQuestDefinition Quest(int id)
{
    return new LegacyQuestDefinition{id=(ushort)id,minLevel=1,maxLevel=80,faction=2,mode=1,male=1,female=1,
        jobs=new byte[]{1,1,1,1,1,1},startType=1,startNpcType=7,startNpcId=1081,endType=2,endNpcType=7,endNpcId=1081,
        mob1=2011,mobCount1=5,resultType=1,rewards=new[]{new LegacyQuestReward{experience=5,money=3000,items=Array.Empty<LegacyQuestItem>()}}};
}
var q=Quest(3400);var catalog=new LegacyQuestCatalogData{sourceSha256="fixture-not-real-corpus",quests=new[]{q}};
var journal=new QuestJournalCore(catalog);var player=new QuestPlayerContext(1,0,0,0,2);int npc=q.StartNpcKey;
Check(!journal.Accept(q.id,0,player,out _),"wrong NPC cannot accept");
Check(journal.Accept(q.id,npc,player,out _),"authored fixture accepted");
Check(!journal.Accept(q.id,npc,player,out _),"accept cannot duplicate");
Check(!journal.Deliver(q.id,npc,0,out _),"early reward rejected");
Check(!journal.CreditMobDeath(1,2000,out _),"unrelated mob gives no progress");
for(int i=0;i<5;i++)Check(journal.CreditMobDeath(100+i,2011,out _),"unique kill "+i);
Check(!journal.CreditMobDeath(104,2011,out _),"duplicate death receipt rejected");
Check(journal.Entries[q.id].kills1==5&&journal.Entries[q.id].stage==JournalStage.Ready,"five kills produce ready state");
Check(!journal.Deliver(q.id,123,0,out _),"wrong turn-in NPC rejected");
Check(!journal.Deliver(q.id,npc,1,out _),"fixed reward cannot choose invalid index");
journal.Persist=_=>false;
Check(!journal.Deliver(q.id,npc,0,out _)&&journal.Gold==0&&journal.Entries[q.id].stage==JournalStage.Ready,"failed disk commit is atomic");
journal.Persist=_=>true;
Check(journal.Deliver(q.id,npc,0,out _)&&journal.Gold==3000&&journal.Experience==5,"correct reward committed once");
Check(!journal.Deliver(q.id,npc,0,out _)&&journal.Gold==3000,"duplicate reward rejected");
var restored=new QuestJournalCore(catalog);restored.Restore(journal.Snapshot());
Check(restored.Gold==3000&&restored.Entries[q.id].stage==JournalStage.Rewarded,"save/load keeps delivered state");
var invalid=journal.Snapshot();invalid.catalogHash="different";bool mismatch=false;
try{restored.Restore(invalid);}catch(InvalidOperationException){mismatch=true;}
Check(mismatch&&restored.Gold==3000,"foreign save rejected without mutation");
var tutorial=Quest(3781);tutorial.endType=4;tutorial.mobCount1=0;
var restricted=new QuestJournalCore(new LegacyQuestCatalogData{quests=new[]{tutorial}});
Check(!restricted.Accept(3781,tutorial.StartNpcKey,player,out _),"unqualified tutorial is not auto-completed");
var collect=Quest(3500);collect.mobCount1=0;collect.farmItems=new[]{new LegacyQuestItem{type=25,typeId=4,count=2}};
var gather=new QuestJournalCore(new LegacyQuestCatalogData{quests=new[]{collect}});
Check(gather.Accept(3500,npc,player,out _)&&gather.Entries[3500].stage==JournalStage.Active,"collection remains active without items");
Check(gather.ApplyInventoryTransaction(new Dictionary<int,int>{{(25<<8)|4,2}},out _)&&gather.Entries[3500].stage==JournalStage.Ready,"accepted inventory transaction advances collection");
Check(gather.Deliver(3500,npc,0,out _)&&!gather.Inventory.ContainsKey((25<<8)|4),"turn-in consumes required items atomically");
Check(!gather.ApplyInventoryTransaction(new Dictionary<int,int>{{(25<<8)|4,-1}},out _),"negative inventory rejected");
var durable=new QuestJournalCore(catalog);int notified=0,reported=0;
durable.Persist=_=>true;
durable.Changed+=()=>throw new InvalidOperationException("simulated broken UI");
durable.Changed+=()=>notified++;
durable.ObserverFailed+=_=>reported++;
Check(durable.Accept(3400,npc,player,out _)&&notified==1&&reported==1,"UI exception does not cancel persisted acceptance or other observers");
for(int i=0;i<5;i++)durable.CreditMobDeath(500+i,2011,out _);
Check(durable.Deliver(3400,npc,0,out _)&&durable.Gold==3000,"reward survives broken UI observer");
Check(!durable.Deliver(3400,npc,0,out _)&&durable.Gold==3000,"broken UI cannot make reward claimable again");
var reentrant=new QuestJournalCore(catalog);reentrant.Accept(3400,npc,player,out _);
bool nestedAccepted=true;
reentrant.Changed+=()=>nestedAccepted=reentrant.CreditMobDeath(600,2011,out _);
Check(reentrant.CreditMobDeath(600,2011,out _)&&!nestedAccepted&&reentrant.Entries[3400].kills1==1,"reentrant death callback is counted exactly once");
var diskFailure=new QuestJournalCore(catalog);diskFailure.Accept(3400,npc,player,out _);diskFailure.Persist=_=>false;
Check(!diskFailure.CreditMobDeath(700,2011,out _)&&diskFailure.Entries[3400].kills1==0,"failed persistence leaves kill uncredited");
diskFailure.Persist=_=>true;
Check(diskFailure.CreditMobDeath(700,2011,out _)&&diskFailure.Entries[3400].kills1==1,"same death can retry after failed persistence");
var writeReentry=new QuestJournalCore(catalog);bool nestedWrite=true;
writeReentry.Persist=snapshot=>{nestedWrite=writeReentry.ApplyInventoryTransaction(new Dictionary<int,int>{{1,1}},out _);return true;};
Check(writeReentry.Accept(3400,npc,player,out _)&&!nestedWrite&&writeReentry.Inventory.Count==0,"persistence callbacks cannot start a nested write");
if(args.Length==2)
{
    var parsed=LegacyQuestCatalogParser.Parse(File.ReadAllBytes(args[0]),98637,File.ReadAllBytes(args[1]),219081);
    Check(parsed.quests.Length==4085,"real corpus quest count");
    var source=parsed.quests.Single(x=>x.id==3400);
    Check(source.title=="Nuevos Comienzos"&&source.mob1==2011&&source.mobCount1==5&&source.rewards[0].money==3000,"real source fields and Spanish strings");
}
Console.WriteLine("QUEST HARNESS OK: "+checks+" checks. Fixtures do not certify native/server behavior.");
