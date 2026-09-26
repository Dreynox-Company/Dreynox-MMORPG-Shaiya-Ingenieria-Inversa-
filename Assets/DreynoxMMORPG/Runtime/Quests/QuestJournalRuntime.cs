using System;
using System.IO;
using System.Collections.Generic;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.World;
using UnityEngine;

namespace Dreynox.Mmorpg.Quests
{
    public sealed class QuestJournalRuntime : MonoBehaviour
    {
        [SerializeField] private TextAsset catalogAsset;
        [SerializeField] private ShaiyaClientActor actor;
        [SerializeField] private ShaiyaCombatInteraction combat;
        [SerializeField] private LegacyMonsterSpawnStreamer monsters;
        [SerializeField] private string localCharacterKey="ps0032-map1-human-fighter";
        [SerializeField] private Dreynox.Mmorpg.NativeContent.NativeCatalogAsset itemCatalog;
        public void SetItemCatalog(Dreynox.Mmorpg.NativeContent.NativeCatalogAsset value)
        {
            if(value==null||value.IsSkills)throw new ArgumentException("An item definition catalog is required.");
            itemCatalog=value;
        }
        public string ItemName(int key)=>itemCatalog!=null?itemCatalog.ItemName(key):"Objeto "+(key>>8)+"/"+(key&255);
        public QuestJournalCore Journal {get;private set;}
        public ShaiyaClientActor Actor => actor;
        public QuestPlayerContext Player => new QuestPlayerContext(1,actor!=null?actor.Family:0,actor!=null?actor.Job:0,actor!=null?actor.Sex:0,2);
        public string Error {get;private set;}="";
        public bool Ready => Journal!=null && Error.Length==0;
        private string path;
        private readonly Dictionary<int,string> mobNames=new Dictionary<int,string>();
        public string MobName(int id)=>mobNames.TryGetValue(id,out var name)?name:"Enemigo #"+id;
        public void Configure(TextAsset catalog,ShaiyaClientActor player,ShaiyaCombatInteraction interaction,LegacyMonsterSpawnStreamer stream)
        {catalogAsset=catalog;actor=player;combat=interaction;monsters=stream;}
        private void Start()
        {
            try
            {
                if(catalogAsset==null||actor==null||combat==null||monsters==null)throw new InvalidOperationException("Quest scene references are incomplete.");
                var data=JsonUtility.FromJson<LegacyQuestCatalogData>(catalogAsset.text);Journal=new QuestJournalCore(data);
                // This local save is separate from original offline-server databases.
                bool qualification=Array.IndexOf(Environment.GetCommandLineArgs(),"--starting-world-qualification")>=0;
                string key=qualification?"qualification-"+Guid.NewGuid().ToString("N"):localCharacterKey;
                path=Path.Combine(Application.persistentDataPath,"LocalQuestSaves",key+".json");
                if(File.Exists(path))Journal.Restore(JsonUtility.FromJson<QuestJournalSave>(File.ReadAllText(path)));
                foreach(var spawn in monsters.Spawns)if(!mobNames.ContainsKey((int)spawn.mobId))mobNames.Add((int)spawn.mobId,spawn.mobName);
                Journal.Persist=Save;Journal.ObserverFailed+=OnObserverFailed;combat.HitApplied+=OnHit;
            }
            catch(Exception ex){Error=ex.Message;Debug.LogException(ex);enabled=false;}
        }
        private void OnDestroy()
        {if(combat!=null)combat.HitApplied-=OnHit;if(Journal!=null)Journal.ObserverFailed-=OnObserverFailed;}
        private void OnObserverFailed(Exception error)
        {Debug.LogError("El diario ya guardó el cambio, pero un observador de interfaz falló: "+error);}
        private void OnHit(ShaiyaCombatTarget target,int damage)
        {
            if(target==null||target.IsAlive||damage<=0||Journal==null)return;
            foreach(var spawn in monsters.Spawns)
                if(spawn.targetId==target.TargetId && spawn.activeInstance==target.gameObject)
                {
                    long lifetime=((long)(uint)target.GetInstanceID()<<32)|(uint)target.Generation;
                    Journal.CreditMobDeath(lifetime,(int)spawn.mobId,out string reason);
                    if(reason.Length>0)Error=reason;return;
                }
        }
        private bool Save(QuestJournalSave value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                byte[] bytes=new System.Text.UTF8Encoding(false).GetBytes(JsonUtility.ToJson(value,true));
                using(var stream=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(File.Exists(path))File.Replace(tmp,path,path+".bak");else File.Move(tmp,path);
                return true;
            }
            finally{if(File.Exists(tmp))File.Delete(tmp);}
        }
    }
}
