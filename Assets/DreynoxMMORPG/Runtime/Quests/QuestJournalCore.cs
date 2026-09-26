using System;
using System.Collections.Generic;

namespace Dreynox.Mmorpg.Quests
{
    public enum JournalStage { Active, Ready, Rewarded }
    [Serializable] public sealed class QuestProgress
    {
        public int id, kills1, kills2;
        public JournalStage stage;
    }
    [Serializable] public sealed class QuestInventoryEntry { public int key, count; }
    [Serializable] public sealed class QuestJournalSave
    {
        public int schema = 1, revision;
        public string catalogHash = "";
        public long experience, gold;
        public QuestProgress[] quests = Array.Empty<QuestProgress>();
        public QuestInventoryEntry[] items = Array.Empty<QuestInventoryEntry>();
    }
    public readonly struct QuestPlayerContext
    {
        public readonly int Level, Family, Job, Sex, NativeMode;
        public QuestPlayerContext(int level,int family,int job,int sex,int nativeMode)
        { Level=level; Family=family; Job=job; Sex=sex; NativeMode=nativeMode; }
    }
    /// <summary>
    /// Local single-player integration, not server-authoritative game.exe rules.
    /// Basic NPC talk, kill and collection quests use authored IDs and rewards.
    /// Unqualified timed/PvP/tutorial/rank/party rules fail closed with a reason.
    /// </summary>
    public sealed class QuestJournalCore
    {
        private readonly Dictionary<int,LegacyQuestDefinition> catalog = new Dictionary<int,LegacyQuestDefinition>();
        private readonly Dictionary<int,QuestProgress> entries = new Dictionary<int,QuestProgress>();
        private readonly Dictionary<int,int> inventory = new Dictionary<int,int>();
        private readonly HashSet<long> creditedKills = new HashSet<long>();
        private readonly Queue<long> killOrder = new Queue<long>();
        private readonly string catalogHash;
        private int revision;
        private bool committing;
        private readonly HashSet<long> pendingCredits = new HashSet<long>();
        public long Experience {get;private set;}
        public long Gold {get;private set;}
        public int Revision => revision;
        public event Action Changed;
        public event Action<Exception> ObserverFailed;
        public int ObserverFailureCount { get; private set; }
        public IReadOnlyDictionary<int,QuestProgress> Entries => entries;
        public IReadOnlyDictionary<int,int> Inventory => inventory;
        // Must atomically persist the entire prospective snapshot. No mutation on failure.
        public Func<QuestJournalSave,bool> Persist {get;set;}
        public QuestJournalCore(LegacyQuestCatalogData data)
        {
            if(data==null)throw new ArgumentNullException(nameof(data));
            catalogHash=data.sourceSha256;
            foreach(var quest in data.quests)
                if(quest==null||catalog.ContainsKey(quest.id))throw new ArgumentException("Invalid/duplicate quest catalog.");
                else catalog.Add(quest.id,quest);
        }
        public bool TryGet(int id,out LegacyQuestDefinition quest)=>catalog.TryGetValue(id,out quest);
        public string Eligibility(int id,int npcKey,QuestPlayerContext player)
        {
            if(!catalog.TryGetValue(id,out var q))return "Definición de misión ausente.";
            if(entries.TryGetValue(id,out var e))return e.stage==JournalStage.Rewarded?"Misión ya entregada.":"Misión ya aceptada.";
            if(q.StartNpcKey!=npcKey||q.startType!=1)return "Esta misión no empieza con este NPC.";
            if(player.Level<q.minLevel||player.Level>q.maxLevel)return "Nivel no compatible con la misión.";
            if(player.Sex<0||player.Sex>1||(player.Sex==0?q.male:q.female)==0)return "Personaje no elegible.";
            if(player.Job<0||player.Job>=q.jobs.Length||q.jobs[player.Job]==0)return "Clase no elegible.";
            if(player.NativeMode<q.mode)return "Modo de dificultad no elegible.";
            // Local interpretation supported by the corpus distribution, not a native runtime eligibility trace.
            bool faction = q.faction==6 || q.faction==2&&(player.Family==0||player.Family==1) ||
                q.faction==5&&(player.Family==2||player.Family==3) || q.faction==player.Family+(player.Family>=2?1:0);
            if(!faction)return "Facción o raza no elegible.";
            if(q.previousQuestId!=0 && (!entries.TryGetValue(q.previousQuestId,out var before)||before.stage!=JournalStage.Rewarded))
                return "Primero completa la misión anterior.";
            return "";
        }
        public string Availability(int id,int npcKey,QuestPlayerContext player)
        {
            string reason=Eligibility(id,npcKey,player);if(reason.Length>0)return reason;
            var q=catalog[id];string unsupported=UnsupportedReason(q);if(unsupported.Length>0)return unsupported;
            if(!HasItems(q.requiredItems,inventory))return "Faltan objetos para iniciar la misión.";
            return "";
        }
        public static string UnsupportedReason(LegacyQuestDefinition q)
        {
            if(q.endType==0||q.endType>2)return "La condición especial/tutorial todavía requiere integración con el cliente nativo.";
            if(q.requireParty!=0||q.pvpKills!=0)return "Condición de grupo/PvP pendiente de integración.";
            if(q.time!=0||q.minimumTime!=0||q.tickStartTerm!=0||q.tickKeepTime!=0||q.tickReceiveCount!=0)
                return "Condición temporal o repetible pendiente de integración.";
            if(q.hg!=0||q.vg!=0||q.cg!=0||q.og!=0||q.ig!=0)return "Condición de rango pendiente de integración.";
            if(q.endNpcType==0)return "Destino de entrega todavía no vinculado.";
            if(q.resultType>1||q.resultType==0&&(q.userSelect==0||q.userSelect>6))return "Selección de recompensa no compatible.";
            foreach(var reward in q.rewards)
                if(reward.needMobId!=0||reward.needMobCount!=0||reward.needItemId!=0||reward.needItemCount!=0||
                   reward.needTime!=0||reward.needHG!=0||reward.needVG!=0||reward.needOG!=0)
                    return "Resultado condicional todavía no verificado.";
            return "";
        }
        public bool Accept(int id,int npcKey,QuestPlayerContext player,out string reason)
        {
            reason=Availability(id,npcKey,player);if(reason.Length>0)return false;
            var q=catalog[id];var copy=CloneEntries();var e=new QuestProgress{id=id,stage=JournalStage.Active};
            e.stage=Ready(q,e,inventory)?JournalStage.Ready:JournalStage.Active;copy.Add(id,e);
            return Commit(copy,new Dictionary<int,int>(inventory),Experience,Gold,out reason);
        }
        public bool Abandon(int id,out string reason)
        {
            reason="";if(!entries.TryGetValue(id,out var e)||e.stage==JournalStage.Rewarded){reason="Misión no activa.";return false;}
            var copy=CloneEntries();copy.Remove(id);return Commit(copy,new Dictionary<int,int>(inventory),Experience,Gold,out reason);
        }
        public bool CreditMobDeath(long lifetimeId,int mobId,out string reason)
        {
            reason="";
            if(creditedKills.Contains(lifetimeId)||!pendingCredits.Add(lifetimeId))return false;
            try
            {
                var copy=CloneEntries();bool changed=false;
                foreach(var e in copy.Values)
                {
                    if(e.stage!=JournalStage.Active)continue;var q=catalog[e.id];
                    if(q.mob1==mobId&&e.kills1<q.mobCount1){e.kills1++;changed=true;}
                    if(q.mob2==mobId&&e.kills2<q.mobCount2){e.kills2++;changed=true;}
                    if(Ready(q,e,inventory))e.stage=JournalStage.Ready;
                }
                if(!changed){RememberKill(lifetimeId);return false;}
                if(!Commit(copy,new Dictionary<int,int>(inventory),Experience,Gold,out reason))return false;
                RememberKill(lifetimeId);return true;
            }
            finally { pendingCredits.Remove(lifetimeId); }
        }
        private void RememberKill(long receipt)
        {
            if (!creditedKills.Add(receipt)) return;
            killOrder.Enqueue(receipt);
            // Bounded duplicate protection for delayed notifications of a pooled lifetime.
            if (killOrder.Count > 8192) creditedKills.Remove(killOrder.Dequeue());
        }
        public bool Deliver(int id,int npcKey,int rewardIndex,out string reason)
        {
            reason="";
            if(!entries.TryGetValue(id,out var entry)||entry.stage==JournalStage.Rewarded){reason="La misión no está activa.";return false;}
            var q=catalog[id];
            if(q.EndNpcKey!=npcKey){reason="Debes volver al NPC de entrega.";return false;}
            if(!Ready(q,entry,inventory)){reason="Aún faltan objetivos.";return false;}
            int choices=q.resultType==1?1:q.userSelect;
            if(rewardIndex<0||rewardIndex>=choices||rewardIndex>=q.rewards.Length){reason="Recompensa no válida.";return false;}
            var inv=new Dictionary<int,int>(inventory);
            foreach(var item in q.farmItems)if(item.count>0){inv[item.Key]-=item.count;if(inv[item.Key]==0)inv.Remove(item.Key);}
            var reward=q.rewards[rewardIndex];
            long experience,gold;
            try
            {
                experience=checked(Experience+reward.experience);gold=checked(Gold+reward.money);
                foreach(var item in reward.items)if(item.count>0){inv.TryGetValue(item.Key,out int count);inv[item.Key]=checked(count+item.count);}
            }
            catch(OverflowException){reason="La recompensa excede el almacenamiento permitido.";return false;}
            var copy=CloneEntries();copy[id].stage=JournalStage.Rewarded;
            foreach(var e in copy.Values)if(e.stage!=JournalStage.Rewarded)e.stage=Ready(catalog[e.id],e,inv)?JournalStage.Ready:JournalStage.Active;
            return Commit(copy,inv,experience,gold,out reason);
        }
        public bool ApplyInventoryTransaction(IReadOnlyDictionary<int,int> changes,out string reason)
        {
            reason="";var inv=new Dictionary<int,int>(inventory);
            try
            {
                foreach(var change in changes)
                {
                    if(change.Key<0||change.Key>65535){reason="Tipo de objeto inválido.";return false;}
                    inv.TryGetValue(change.Key,out int count);int next=checked(count+change.Value);
                    if(next<0){reason="Objetos insuficientes.";return false;}if(next==0)inv.Remove(change.Key);else inv[change.Key]=next;
                }
            }
            catch(OverflowException){reason="Cantidad inválida.";return false;}
            var copy=CloneEntries();foreach(var e in copy.Values)
                if(e.stage!=JournalStage.Rewarded)e.stage=Ready(catalog[e.id],e,inv)?JournalStage.Ready:JournalStage.Active;
            return Commit(copy,inv,Experience,Gold,out reason);
        }
        /// <summary>
        /// Atomic exchange for the local client only. Prices/authorization belong
        /// to the merchant adapter; all gold, items and quest readiness persist
        /// in the SAME journal snapshot. It is not a native server response.
        /// A quote is tied to a revision so a repeated/stale confirmation fails.
        /// </summary>
        public bool ExchangeLocalItem(int expectedRevision,int itemKey,int itemDelta,long goldDelta,out string reason)
        {
            reason="";
            if(committing){reason="Ya hay una transacción de diario en curso.";return false;}
            if(expectedRevision!=revision){reason="El inventario cambió. Revisa de nuevo la operación.";return false;}
            if(itemKey<257||itemKey>65535||(itemKey&255)==0||itemDelta==0||
                (itemDelta>0?goldDelta>=0:goldDelta<=0))
            {reason="Intercambio local no válido.";return false;}
            var inv=new Dictionary<int,int>(inventory);
            long nextGold;
            try
            {
                nextGold=checked(Gold+goldDelta);
                if(nextGold<0){reason="No tienes oro suficiente.";return false;}
                inv.TryGetValue(itemKey,out int current);
                int next=checked(current+itemDelta);
                if(next<0){reason="No tienes esa cantidad de objetos.";return false;}
                if(next==0)inv.Remove(itemKey);else inv[itemKey]=next;
            }
            catch(OverflowException){reason="El intercambio excede los límites permitidos.";return false;}
            var copy=CloneEntries();
            foreach(var entry in copy.Values)
                if(entry.stage!=JournalStage.Rewarded)
                    entry.stage=Ready(catalog[entry.id],entry,inv)?JournalStage.Ready:JournalStage.Active;
            return Commit(copy,inv,Experience,nextGold,out reason);
        }
        public QuestJournalSave Snapshot()=>Snapshot(entries,inventory,Experience,Gold,revision);
        public void Restore(QuestJournalSave data)
        {
            if(committing)throw new InvalidOperationException("No se puede restaurar durante una transacción.");
            if(data==null||data.schema!=1||data.catalogHash!=catalogHash||data.gold<0||data.experience<0||data.revision<0)
                throw new InvalidOperationException("La partida no corresponde al catálogo actual.");
            var inv=new Dictionary<int,int>();var copy=new Dictionary<int,QuestProgress>();
            foreach(var item in data.items){if(item.key<0||item.key>65535||item.count<=0||inv.ContainsKey(item.key))throw new InvalidOperationException("Inventario corrupto.");inv.Add(item.key,item.count);}
            foreach(var e in data.quests)
            {
                if(!catalog.TryGetValue(e.id,out var q)||copy.ContainsKey(e.id)||e.kills1<0||e.kills1>q.mobCount1||e.kills2<0||e.kills2>q.mobCount2||
                   !Enum.IsDefined(typeof(JournalStage),e.stage))throw new InvalidOperationException("Diario corrupto.");
                copy.Add(e.id,new QuestProgress{id=e.id,kills1=e.kills1,kills2=e.kills2,stage=e.stage==JournalStage.Rewarded?e.stage:Ready(q,e,inv)?JournalStage.Ready:JournalStage.Active});
            }
            entries.Clear();foreach(var e in copy)entries.Add(e.Key,e.Value);
            inventory.Clear();foreach(var e in inv)inventory.Add(e.Key,e.Value);
            Experience=data.experience;Gold=data.gold;revision=data.revision;creditedKills.Clear();killOrder.Clear();NotifyChanged();
        }
        private bool Commit(Dictionary<int,QuestProgress> next,Dictionary<int,int> inv,long experience,long gold,out string reason)
        {
            reason="";
            if(committing){reason="Ya hay una transacción de diario en curso.";return false;}
            committing=true;
            try
            {
                int rev;
                try
                {
                    rev=checked(revision+1);
                    if(Persist!=null&&!Persist(Snapshot(next,inv,experience,gold,rev)))
                    {reason="No se pudo guardar: no se aplicó ningún cambio.";return false;}
                }
                catch(Exception ex){reason="No se aplicó el cambio: "+ex.Message;return false;}
                entries.Clear();foreach(var e in next)entries.Add(e.Key,e.Value);
                inventory.Clear();foreach(var e in inv)inventory.Add(e.Key,e.Value);
                Experience=experience;Gold=gold;revision=rev;
            }
            finally { committing=false; }
            // UI failure cannot turn an already durable reward into a failed transaction.
            NotifyChanged();return true;
        }
        private void NotifyChanged()
        {
            Action callbacks=Changed;
            if(callbacks==null)return;
            foreach(Action handler in callbacks.GetInvocationList())
            {
                try { handler(); }
                catch(Exception ex)
                {
                    ObserverFailureCount++;
                    var reporters=ObserverFailed;
                    if(reporters==null)continue;
                    foreach(Action<Exception> report in reporters.GetInvocationList())
                        try { report(ex); } catch { /* Diagnostics cannot undo persisted state. */ }
                }
            }
        }
        private Dictionary<int,QuestProgress> CloneEntries()
        {var result=new Dictionary<int,QuestProgress>();foreach(var e in entries)result.Add(e.Key,new QuestProgress{id=e.Value.id,kills1=e.Value.kills1,kills2=e.Value.kills2,stage=e.Value.stage});return result;}
        private static bool HasItems(LegacyQuestItem[] items,IReadOnlyDictionary<int,int> inv)
        {
            var needed=new Dictionary<int,int>();foreach(var item in items)if(item.count>0){needed.TryGetValue(item.Key,out int count);needed[item.Key]=count+item.count;}
            foreach(var item in needed)if(!inv.TryGetValue(item.Key,out int count)||count<item.Value)return false;
            return true;
        }
        private static bool Ready(LegacyQuestDefinition q,QuestProgress e,IReadOnlyDictionary<int,int> inv)=>e.kills1>=q.mobCount1&&e.kills2>=q.mobCount2&&HasItems(q.farmItems,inv);
        private QuestJournalSave Snapshot(Dictionary<int,QuestProgress> data,Dictionary<int,int> inv,long xp,long gold,int rev)
        {
            var result=new QuestJournalSave{catalogHash=catalogHash,revision=rev,experience=xp,gold=gold,quests=new QuestProgress[data.Count],items=new QuestInventoryEntry[inv.Count]};
            int i=0;foreach(var q in data.Values)result.quests[i++]=new QuestProgress{id=q.id,kills1=q.kills1,kills2=q.kills2,stage=q.stage};
            i=0;foreach(var item in inv)result.items[i++]=new QuestInventoryEntry{key=item.Key,count=item.Value};return result;
        }
    }
}
