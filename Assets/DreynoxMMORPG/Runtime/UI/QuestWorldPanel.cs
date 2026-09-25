using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Dreynox.Mmorpg.Interaction;
using Dreynox.Mmorpg.Quests;
using Dreynox.Mmorpg.World;
using UnityEngine;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.UI
{
    /// <summary>Quest panel layered onto NativeWorldHud; does not replace its original atlas HUD, targeting or minimap.</summary>
    public sealed class QuestWorldPanel : MonoBehaviour
    {
        [SerializeField] private NativeWorldHud hud;
        [SerializeField] private QuestJournalRuntime quests;
        [SerializeField] private Sprite questBackground;
        private RectTransform canvasRoot,modal,listRoot;
        private Text tracker,modalTitle,narrative,rewardText;
        private Button action;
        private Font font;
        private LegacyNpcRuntimeDescriptor npc;
        private int selectedQuest=-1,rewardIndex;
        private bool journalView;
        private QuestJournalCore subscribed;
        private readonly List<GameObject> optionObjects=new List<GameObject>();
        private readonly Dictionary<int,List<int>> byNpc=new Dictionary<int,List<int>>();
        public void Configure(NativeWorldHud worldHud,QuestJournalRuntime journal,Sprite parchment)
        {hud=worldHud;quests=journal;questBackground=parchment;}
        private IEnumerator Start()
        {
            if(hud==null||quests==null||questBackground==null)throw new InvalidOperationException("Quest UI missing bindings/artwork.");
            float until=Time.realtimeSinceStartup+10;
            while((!hud.Ready||!quests.Ready)&&Time.realtimeSinceStartup<until)yield return null;
            if(!hud.Ready||!quests.Ready)throw new InvalidOperationException("Native HUD or journal did not initialize: "+quests.Error);
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");canvasRoot=hud.CanvasRoot;
            var panel=Panel("Quest tracker",canvasRoot,new Vector2(1,1),new Vector2(-282,-310),new Vector2(265,215));
            tracker=Label(panel,"Misiones · L",14);Inset(tracker.rectTransform,10);
            var button=Button(canvasRoot,"L · Misiones",ToggleJournal);
            Box(button.GetComponent<RectTransform>(),new Vector2(1,0),new Vector2(-145,65),new Vector2(125,35));
            BuildDialog();hud.HasQuestPanel=true;
            hud.DialogueOpened+=NpcOpened;hud.DialogueClosed+=CloseDialog;
            subscribed=quests.Journal;subscribed.Changed+=RefreshQuests;
            var spawns=FindFirstObjectByType<LegacyNpcSpawnStreamer>();
            if(spawns!=null)foreach(var spawn in spawns.Spawns)
            {
                int key=(spawn.npcType<<16)|(ushort)spawn.typeId;
                if(!byNpc.TryGetValue(key,out var ids)){ids=new List<int>();byNpc.Add(key,ids);}
                foreach(int id in spawn.inQuestIds)if(!ids.Contains(id))ids.Add(id);
                foreach(int id in spawn.outQuestIds)if(!ids.Contains(id))ids.Add(id);
            }
            hud.QuestMarker=Marker;RefreshQuests();
        }
        private void Update()
        {
            if(canvasRoot==null)return;
            if(Input.GetKeyDown(KeyCode.L))ToggleJournal();
            if(Input.GetKeyDown(KeyCode.Escape))CloseAll();
        }
        private string Marker(int npcKey)
        {
            if(!quests.Ready||!byNpc.TryGetValue(npcKey,out var ids))return "";
            bool available=false;
            foreach(int id in ids)
            {
                if(quests.Journal.Entries.TryGetValue(id,out var e)&&e.stage==JournalStage.Ready&&quests.Journal.TryGet(id,out var q)&&q.EndNpcKey==npcKey)return " ?";
                // Display eligible source quests even when a special objective remains disabled.
                // The dialog exposes the missing behavior instead of silently hiding the native quest.
                if(quests.Journal.Eligibility(id,npcKey,quests.Player).Length==0)available=true;
            }
            return available?" !":"";
        }
        private void BuildDialog()
        {
            modal=Panel("NPC and quest dialog",canvasRoot,new Vector2(.5f,.5f),new Vector2(-405,320),new Vector2(810,650));
            var close=Button(modal,"×",CloseAll);Box(close.GetComponent<RectTransform>(),new Vector2(1,1),new Vector2(-40,-8),new Vector2(30,30));
            modalTitle=Label(modal,"",24);Box(modalTitle.rectTransform,new Vector2(0,1),new Vector2(20,-14),new Vector2(735,40));
            listRoot=Scroll(modal,new Vector2(14,-70),new Vector2(290,560));
            var page=Panel("Original quest parchment",modal,new Vector2(0,1),new Vector2(319,-70),new Vector2(475,560));
            var image=page.GetComponent<Image>();image.sprite=questBackground;image.color=Color.white;
            var story=Scroll(page,new Vector2(20,-20),new Vector2(435,365));
            narrative=Label(story,"",17);narrative.color=new Color(.10f,.065f,.02f);narrative.supportRichText=false;
            var fit=narrative.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var min=narrative.gameObject.AddComponent<LayoutElement>();min.minHeight=350;
            rewardText=Label(page,"",15);rewardText.color=new Color(.1f,.07f,.02f);rewardText.supportRichText=false;
            Box(rewardText.rectTransform,new Vector2(0,1),new Vector2(20,-394),new Vector2(435,92));
            action=Button(page,"Aceptar",QuestAction);Box(action.GetComponent<RectTransform>(),new Vector2(0,0),new Vector2(20,58),new Vector2(210,38));
            var cycle=Button(page,"Elegir recompensa →",CycleReward);Box(cycle.GetComponent<RectTransform>(),new Vector2(0,0),new Vector2(240,58),new Vector2(215,38));
            modal.gameObject.SetActive(false);
        }
        private void NpcOpened(LegacyNpcRuntimeDescriptor value)
        {npc=value;journalView=false;selectedQuest=-1;modal.gameObject.SetActive(true);modalTitle.text=value.DisplayName;RefreshOptions();narrative.text=Format(value.WelcomeMessage);action.interactable=false;rewardText.text="";}
        private void ToggleJournal()
        {
            if(journalView&&modal.gameObject.activeSelf){CloseAll();return;}
            hud.CloseDialogue();npc=null;journalView=true;selectedQuest=-1;WorldInputGate.Set(this,true);
            modal.gameObject.SetActive(true);modalTitle.text="Diario de misiones";RefreshOptions();narrative.text="Selecciona una misión activa.\nLas condiciones no integradas se muestran, pero no se completan artificialmente.";rewardText.text="";action.interactable=false;
        }
        private void RefreshQuests()
        {
            if(!quests.Ready)return;
            var text=new StringBuilder("MISIONES · L\n");
            foreach(var e in quests.Journal.Entries.Values)
                if(e.stage!=JournalStage.Rewarded&&quests.Journal.TryGet(e.id,out var q))text.Append('\n').Append(q.title).Append('\n').Append(Progress(q,e));
            text.Append("\n\nEXP registrada: ").Append(quests.Journal.Experience).Append(" · Oro: ").Append(quests.Journal.Gold);
            tracker.text=text.ToString();if(modal.gameObject.activeSelf){RefreshOptions();if(selectedQuest>=0)SelectQuest(selectedQuest);}
        }
        private void RefreshOptions()
        {
            foreach(var obj in optionObjects){obj.SetActive(false);Destroy(obj);}optionObjects.Clear();
            if(!quests.Ready)return;
            var ids=new HashSet<int>();
            if(npc!=null){foreach(int id in npc.InQuestIds)ids.Add(id);foreach(int id in npc.OutQuestIds)ids.Add(id);}
            else foreach(var e in quests.Journal.Entries.Values)if(e.stage!=JournalStage.Rewarded)ids.Add(e.id);
            var ordered=new List<int>(ids);ordered.Sort();
            foreach(int id in ordered)
            {
                if(!quests.Journal.TryGet(id,out var q))continue;
                if(quests.Journal.Entries.TryGetValue(id,out var p)&&p.stage==JournalStage.Rewarded)continue;
                if(p==null && npc!=null && quests.Journal.Eligibility(id,npc.ServiceKey,quests.Player).Length>0)continue;
                int captured=id;string suffix=p!=null?"\n"+Progress(q,p):"\nNivel "+q.minLevel+"–"+q.maxLevel;
                var button=Button(listRoot,q.title+suffix,()=>SelectQuest(captured));
                button.gameObject.AddComponent<LayoutElement>().preferredHeight=70;optionObjects.Add(button.gameObject);
            }
            if(npc!=null&&npc.Services!=0)
            {
                var label=Label(listRoot,"Servicios originales: "+npc.Services+"\nComercio, viaje y almacén aún no se ejecutan desde este panel.",14);
                label.gameObject.AddComponent<LayoutElement>().preferredHeight=95;optionObjects.Add(label.gameObject);
            }
        }
        private void SelectQuest(int id)
        {
            if(!quests.Ready||!quests.Journal.TryGet(id,out var q))return;
            selectedQuest=id;rewardIndex=Mathf.Clamp(rewardIndex,0,Mathf.Max(0,Choices(q)-1));
            quests.Journal.Entries.TryGetValue(id,out var entry);
            narrative.text=Format(entry!=null?q.reminder:q.initial)+"\n\n"+Format(q.window)+"\n"+Progress(q,entry);
            string reason;
            if(entry==null)reason=npc==null?"Acude al NPC de inicio.":quests.Journal.Availability(id,npc.ServiceKey,quests.Player);
            else reason=entry.stage!=JournalStage.Ready?"Completa los objetivos.":npc==null||q.EndNpcKey!=npc.ServiceKey?"Vuelve al NPC de entrega.":"";
            var reward=q.rewards[rewardIndex];
            var text=new StringBuilder("Recompensa ").Append(rewardIndex+1).Append('/').Append(Choices(q)).Append(": ").Append(reward.experience).Append(" EXP · ").Append(reward.money).Append(" oro");
            foreach(var item in reward.items)if(item.count>0)text.Append("\nObjeto ").Append(item.type).Append('/').Append(item.typeId).Append(" ×").Append(item.count);
            if(reason.Length>0)text.Append('\n').Append(reason);
            rewardText.text=text.ToString();action.interactable=reason.Length==0;
            action.GetComponentInChildren<Text>().text=entry==null?"Aceptar misión":"Entregar misión";
        }
        private void QuestAction()
        {
            if(npc==null||selectedQuest<0||!quests.Ready)return;
            string reason;bool accepted;
            bool hadEntry=quests.Journal.Entries.ContainsKey(selectedQuest);
            int id=selectedQuest,index=rewardIndex;
            if(!hadEntry)accepted=quests.Journal.Accept(id,npc.ServiceKey,quests.Player,out reason);
            else accepted=quests.Journal.Deliver(id,npc.ServiceKey,index,out reason);
            ShowHint(accepted?(hadEntry?"Misión entregada. Recompensa guardada.":"Misión aceptada y guardada."):reason);
            if(accepted&&hadEntry&&quests.Journal.TryGet(id,out var q))narrative.text=Format(q.rewards[index].completion);
            RefreshOptions();action.interactable=false;
        }
        private void CycleReward(){if(selectedQuest<0||!quests.Ready||!quests.Journal.TryGet(selectedQuest,out var q))return;rewardIndex=(rewardIndex+1)%Mathf.Max(1,Choices(q));SelectQuest(selectedQuest);}
        private static int Choices(LegacyQuestDefinition q)=>q.resultType==1?1:Mathf.Clamp(q.userSelect,1,q.rewards.Length);
        private string Progress(LegacyQuestDefinition q,QuestProgress e)
        {
            if(e!=null&&e.stage==JournalStage.Ready)return "Lista para entregar";
            var s=new StringBuilder();if(q.mobCount1>0)s.Append(quests.MobName(q.mob1)).Append(": ").Append(e!=null?e.kills1:0).Append('/').Append(q.mobCount1);
            if(q.mobCount2>0)s.Append("\n").Append(quests.MobName(q.mob2)).Append(": ").Append(e!=null?e.kills2:0).Append('/').Append(q.mobCount2);
            foreach(var item in q.farmItems)if(item.count>0)s.Append("\nObjeto ").Append(item.type).Append('/').Append(item.typeId).Append(" ×").Append(item.count);
            return s.ToString();
        }
        private static string Format(string text)
        {
            // Remove only the source's known color markup; preserve wording.
            return System.Text.RegularExpressions.Regex.Replace(text??"",@"\{/?c[0-9]*\}","")
                .Replace("\\n","\n").Replace("$n","\n");
        }
        private void CloseAll(){hud.CloseDialogue();CloseDialog();}
        private void CloseDialog(){if(modal!=null)modal.gameObject.SetActive(false);npc=null;journalView=false;WorldInputGate.Set(this,false);}
        private void ShowHint(string message){if(hud!=null)hud.ShowMessage(message);}
        private void OnDisable(){CloseDialog();}
        private void OnDestroy()
        {
            if(hud!=null){hud.DialogueOpened-=NpcOpened;hud.DialogueClosed-=CloseDialog;hud.QuestMarker=null;hud.HasQuestPanel=false;}
            if(subscribed!=null)subscribed.Changed-=RefreshQuests;
            WorldInputGate.Set(this,false);
        }
        private RectTransform Panel(string name,RectTransform parent,Vector2 anchor,Vector2 position,Vector2 size)
        {
            var rect=Rect(name,parent,anchor,position,size);var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.025f,.03f,.04f,.80f);return rect;
        }
        private static RectTransform Rect(string name,RectTransform parent,Vector2 anchor,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);Box(rect,anchor,position,size);return rect;
        }
        private static void Box(RectTransform rect,Vector2 anchor,Vector2 position,Vector2 size)
        {rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;}
        private static void Inset(RectTransform rect,float amount)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.one*amount;rect.offsetMax=-Vector2.one*amount;}
        private Text Label(RectTransform parent,string text,int size)
        {
            var rect=Rect("Label",parent,Vector2.zero,Vector2.zero,Vector2.zero);Inset(rect,0);
            var label=rect.gameObject.AddComponent<Text>();label.font=font;label.fontSize=size;label.color=Color.white;
            label.text=text;label.supportRichText=false;label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;return label;
        }
        private Button Button(RectTransform parent,string text,UnityEngine.Events.UnityAction callback)
        {
            var rect=Panel("Action "+text,parent,new Vector2(0,1),Vector2.zero,new Vector2(260,42));
            rect.GetComponent<Image>().color=new Color(.16f,.14f,.1f,.95f);var button=rect.gameObject.AddComponent<Button>();
            button.targetGraphic=rect.GetComponent<Image>();button.onClick.AddListener(callback);var label=Label(rect,text,16);label.alignment=TextAnchor.MiddleCenter;Inset(label.rectTransform,5);return button;
        }
        private RectTransform Scroll(RectTransform parent,Vector2 position,Vector2 size)
        {
            var root=Rect("Scroll",parent,new Vector2(0,1),position,size);root.gameObject.AddComponent<RectMask2D>();var hitArea=root.gameObject.AddComponent<Image>();hitArea.color=Color.clear;
            var scroll=root.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            var content=Rect("Content",root,new Vector2(0,1),Vector2.zero,new Vector2(size.x,0));
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.spacing=8;
            var fitter=content.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.content=content;scroll.viewport=root;return content;
        }
    }
}
