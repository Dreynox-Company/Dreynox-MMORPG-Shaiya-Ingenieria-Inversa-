using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Dreynox.Mmorpg.Interaction;
using Dreynox.Mmorpg.ParityCore;
using Dreynox.Mmorpg.Quests;
using Dreynox.Mmorpg.World;
using UnityEngine;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.UI
{
    /// <summary>Original quest content on the existing native HUD, not a second world HUD.</summary>
    public sealed class QuestWorldPanel : MonoBehaviour
    {
        [SerializeField] private NativeWorldHud hud;
        [SerializeField] private QuestJournalRuntime quests;
        [SerializeField] private Sprite questBackground;
        private RectTransform canvasRoot, modal, listRoot, paperPage, selectorBackdrop;
        private Button closeButton;
        private NativeWindowDrag windowDrag;
        private Text tracker, modalTitle, narrative, rewardText;
        private Button action, rewardCycle, abandon;
        private RectTransform abandonConfirmation;
        private Text abandonPrompt;
        private int pendingAbandon=-1;
        private Font font;
        private LegacyNpcRuntimeDescriptor npc;
        private int selectedQuest=-1, rewardIndex;
        private bool journalView;
        private QuestJournalCore subscribed;
        private readonly List<GameObject> optionObjects=new List<GameObject>();
        private readonly HashSet<int> visibleQuestIds=new HashSet<int>();
        private readonly Dictionary<int,List<int>> byNpc=new Dictionary<int,List<int>>();
        public string ActionFailure { get; private set; }="";
        public void Configure(NativeWorldHud worldHud, QuestJournalRuntime journal, Sprite parchment)
        { hud=worldHud; quests=journal; questBackground=parchment; }
        private IEnumerator Start()
        {
            if(hud==null||quests==null||questBackground==null)throw new InvalidOperationException("Quest UI missing bindings/artwork.");
            float until=Time.realtimeSinceStartup+10;
            while((!hud.Ready||!quests.Ready)&&Time.realtimeSinceStartup<until)yield return null;
            if(!hud.Ready||!quests.Ready)throw new InvalidOperationException("Native HUD or journal did not initialize: "+quests.Error);
            font=NativeUiPrimitives.Font;canvasRoot=hud.CanvasRoot;
            var panel=Panel("Quest tracker",canvasRoot,new Vector2(1,1),new Vector2(-250,-242),new Vector2(238,185));
            tracker=Label(panel,"Misiones · L",12);Inset(tracker.rectTransform,6);
            panel.GetComponent<Image>().color=new Color(0,0,0,.28f);
            var button=Button(canvasRoot,"L · Misiones",ToggleJournal);
            Box(button.GetComponent<RectTransform>(),new Vector2(1,0),new Vector2(-112,56),new Vector2(104,26));
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
            if(!NativeUiPrimitives.TextEditing&&Input.GetKeyDown(KeyCode.L))ToggleJournal();
            if(Input.GetKeyDown(KeyCode.Escape))ProcessEscape();
        }
        public bool ProcessEscape()
        {
            if(modal==null||!modal.gameObject.activeInHierarchy)return false;
            if(pendingAbandon>=0)CancelAbandon();else CloseAll();
            return true;
        }
        private string Marker(int npcKey)
        {
            if(!quests.Ready||!byNpc.TryGetValue(npcKey,out var ids))return "";
            bool available=false;
            foreach(int id in ids)
            {
                if(quests.Journal.Entries.TryGetValue(id,out var e)&&e.stage==JournalStage.Ready&&quests.Journal.TryGet(e.id,out var q)&&q.EndNpcKey==npcKey)return " ?";
                if(quests.Journal.Eligibility(id,npcKey,quests.Player).Length==0)available=true;
            }
            return available?" !":"";
        }
        private void BuildDialog()
        {
            // Native take.tga is a 256x512 window, not a page stretched inside a
            // 580x592 substitute. Selection and details are separate presentations.
            float x=Mathf.Max(0,canvasRoot.rect.width-474);
            modal=Panel("NPC and quest dialog",canvasRoot,new Vector2(0,1),new Vector2(x,-118),new Vector2(256,512));
            modal.GetComponent<Image>().color=new Color(.065f,.052f,.037f,1);
            var overlay=modal.gameObject.AddComponent<Canvas>();overlay.overrideSorting=true;overlay.sortingOrder=100;
            modal.gameObject.AddComponent<GraphicRaycaster>();
            selectorBackdrop=Rect("Original NPC selection",modal,new Vector2(0,1),Vector2.zero,new Vector2(342,229));
            if(hud!=null&&hud.TalkArtwork!=null)
                NativeUiPrimitives.Art("NPC selection artwork",selectorBackdrop,hud.TalkArtwork,new Rect(1,1,342,229),Vector2.zero);
            listRoot=Scroll(modal,new Vector2(16,-43),new Vector2(310,145));
            paperPage=Panel("Original quest parchment",modal,new Vector2(0,1),Vector2.zero,new Vector2(256,512));
            var image=paperPage.GetComponent<Image>();image.sprite=questBackground;image.color=Color.white;
            var story=Scroll(paperPage,new Vector2(21,-48),new Vector2(224,272));
            narrative=Label(story,"",12);ConfigurePaperText(narrative);
            var rewards=Scroll(paperPage,new Vector2(21,-343),new Vector2(224,94));
            rewardText=Label(rewards,"",12);ConfigurePaperText(rewardText);
            action=Button(paperPage,"Aceptar",QuestAction);
            Box(action.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(52,-470),new Vector2(68,26));
            rewardCycle=Button(paperPage,"Elegir recompensa →",CycleReward);
            Box(rewardCycle.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(21,-439),new Vector2(215,23));
            rewardCycle.gameObject.SetActive(false);
            abandon=Button(paperPage,"Abandonar",()=>{RequestAbandonSelectedQuest();});
            Box(abandon.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(33,-470),new Vector2(98,26));
            abandon.gameObject.SetActive(false);
            modalTitle=Label(modal,"",12);modalTitle.color=Color.yellow;
            closeButton=Button(modal,"Cancelar",CloseAll);
            abandonConfirmation=Panel("Confirmar abandono",modal,new Vector2(0,1),Vector2.zero,new Vector2(256,512));
            abandonConfirmation.GetComponent<Image>().color=new Color(.04f,.035f,.025f,1);
            abandonPrompt=Label(abandonConfirmation,"",13);abandonPrompt.alignment=TextAnchor.MiddleCenter;
            Box(abandonPrompt.rectTransform,new Vector2(0,1),new Vector2(18,-126),new Vector2(220,172));
            var keep=Button(abandonConfirmation,"Conservar",CancelAbandon);
            Box(keep.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(17,-335),new Vector2(105,28));
            var confirm=Button(abandonConfirmation,"Abandonar",()=>{ConfirmAbandon();});
            Box(confirm.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(135,-335),new Vector2(105,28));
            abandonConfirmation.gameObject.SetActive(false);
            // Only the title strip starts a window drag; scrolling and reward
            // buttons retain their own pointer ownership.
            var handle=Rect("Window drag handle",modal,new Vector2(0,1),Vector2.zero,new Vector2(232,30));
            handle.gameObject.AddComponent<Image>().color=Color.clear;
            windowDrag=handle.gameObject.AddComponent<NativeWindowDrag>();windowDrag.Error=ShowHint;
            windowDrag.Configure(modal,"quest-window",null);
            modalTitle.transform.SetAsLastSibling();closeButton.transform.SetAsLastSibling();
            ShowPresentation(true);modal.gameObject.SetActive(false);
        }
        private void ShowPresentation(bool detail)
        {
            paperPage.gameObject.SetActive(detail);selectorBackdrop.gameObject.SetActive(!detail);
            listRoot.parent.parent.gameObject.SetActive(!detail);
            modal.sizeDelta=detail?new Vector2(256,512):new Vector2(342,229);
            Box(modalTitle.rectTransform,new Vector2(0,1),detail?new Vector2(36,-12):new Vector2(18,-9),detail?new Vector2(199,27):new Vector2(306,26));
            Box(closeButton.GetComponent<RectTransform>(),new Vector2(0,1),detail?new Vector2(144,-470):new Vector2(259,-195),new Vector2(76,26));
            if(windowDrag!=null)windowDrag.Clamp();
        }
        private static void ConfigurePaperText(Text value)
        {
            value.color=Color.white;value.supportRichText=false;
            var shadow=value.gameObject.AddComponent<Shadow>();
            shadow.effectColor=new Color(0,0,0,.9f);shadow.effectDistance=new Vector2(1,-1);
        }
        private void NpcOpened(LegacyNpcRuntimeDescriptor value)
        {
            CancelAbandon();abandon.gameObject.SetActive(false);
            npc=value;journalView=false;selectedQuest=-1;ShowPresentation(false);modal.gameObject.SetActive(true);modalTitle.text=value.DisplayName;
            RefreshOptions();narrative.text=Format(value.WelcomeMessage);action.interactable=false;rewardText.text="";
            action.gameObject.SetActive(false);rewardCycle.gameObject.SetActive(false);
        }
        private void ToggleJournal()
        {
            if(journalView&&modal.gameObject.activeSelf){CloseAll();return;}
            CancelAbandon();abandon.gameObject.SetActive(false);
            hud.CloseDialogue();npc=null;journalView=true;selectedQuest=-1;WorldInputGate.Set(this,true);
            ShowPresentation(false);modal.gameObject.SetActive(true);modalTitle.text="Diario de misiones";RefreshOptions();
            narrative.text="Selecciona una misión activa.\nLas condiciones no integradas se muestran, pero no se completan artificialmente.";
            rewardText.text="";action.interactable=false;
            action.gameObject.SetActive(false);rewardCycle.gameObject.SetActive(false);
        }
        private void RefreshQuests()
        {
            if(!quests.Ready)return;
            var text=new StringBuilder("MISIONES · L\n");
            foreach(var e in quests.Journal.Entries.Values)
                if(e.stage!=JournalStage.Rewarded&&quests.Journal.TryGet(e.id,out var q))text.Append('\n').Append(q.title).Append('\n').Append(Progress(q,e));
            tracker.text=text.ToString();
            tracker.transform.parent.gameObject.SetActive(text.Length>16);
            if(modal.gameObject.activeSelf){RefreshOptions();if(selectedQuest>=0)SelectQuest(selectedQuest);}
        }
        private void RefreshOptions()
        {
            foreach(var obj in optionObjects){obj.SetActive(false);if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}optionObjects.Clear();visibleQuestIds.Clear();
            if(!quests.Ready)return;
            if(npc!=null&&!String.IsNullOrWhiteSpace(npc.WelcomeMessage))
            {
                var greeting=Label(listRoot,Format(npc.WelcomeMessage),12);optionObjects.Add(greeting.gameObject);
            }
            var ids=new HashSet<int>();
            if(npc!=null){foreach(int id in npc.InQuestIds)ids.Add(id);foreach(int id in npc.OutQuestIds)ids.Add(id);}
            else foreach(var e in quests.Journal.Entries.Values)if(e.stage!=JournalStage.Rewarded)ids.Add(e.id);
            var ordered=new List<int>(ids);ordered.Sort();
            foreach(int id in ordered)
            {
                if(!quests.Journal.TryGet(id,out var q))continue;
                if(quests.Journal.Entries.TryGetValue(id,out var p)&&p.stage==JournalStage.Rewarded)continue;
                if(p==null&&npc!=null&&quests.Journal.Eligibility(id,npc.ServiceKey,quests.Player).Length>0)continue;
                visibleQuestIds.Add(id);
                int captured=id;string suffix=p!=null?"\n"+Progress(q,p):"\nNivel "+q.minLevel+"–"+q.maxLevel;
                var button=Button(listRoot,q.title+suffix,()=>SelectQuest(captured));
                button.gameObject.AddComponent<LayoutElement>().preferredHeight=42;optionObjects.Add(button.gameObject);
            }
            if(visibleQuestIds.Count==0)
            {
                var empty=Label(listRoot,npc==null?"No tienes misiones activas.":"No hay misiones disponibles para este personaje.",12);
                optionObjects.Add(empty.gameObject);
            }
            if(npc!=null&&(npc.Services & ~NpcServiceKind.Quest)!=NpcServiceKind.None)
            {
                var label=Label(listRoot,"Otros servicios de este NPC todavía requieren integración. Las misiones disponibles se muestran arriba.",12);
                label.gameObject.AddComponent<LayoutElement>().preferredHeight=80;optionObjects.Add(label.gameObject);
            }
        }
        public bool SelectVisibleQuest(int id)
        {
            if(modal==null||!modal.gameObject.activeInHierarchy||!visibleQuestIds.Contains(id)||!quests.Ready)return false;
            SelectQuest(id);Canvas.ForceUpdateCanvases();return selectedQuest==id;
        }
        private void SelectQuest(int id)
        {
            if(!quests.Ready||!quests.Journal.TryGet(id,out var q))return;
            if(selectedQuest!=id)CancelAbandon();
            selectedQuest=id;ShowPresentation(true);modalTitle.text=q.title;
            rewardIndex=Mathf.Clamp(rewardIndex,0,Mathf.Max(0,Choices(q)-1));
            quests.Journal.Entries.TryGetValue(id,out var entry);
            if(entry!=null&&entry.stage==JournalStage.Rewarded){ShowCompletion(q,rewardIndex);return;}
            abandon.gameObject.SetActive(journalView&&entry!=null&&entry.stage!=JournalStage.Rewarded);
            action.gameObject.SetActive(!journalView);
            rewardCycle.gameObject.SetActive(!journalView&&Choices(q)>1);
            narrative.text=Format(entry!=null?q.reminder:q.initial)+"\n\n"+Format(q.window)+"\n"+Progress(q,entry);
            string reason;
            if(entry==null)reason=npc==null?"Acude al NPC de inicio.":quests.Journal.Availability(id,npc.ServiceKey,quests.Player);
            else reason=entry.stage!=JournalStage.Ready?"Completa los objetivos.":npc==null||q.EndNpcKey!=npc.ServiceKey?"Vuelve al NPC de entrega.":"";
            var reward=q.rewards[rewardIndex];
            var text=new StringBuilder("Recompensa ").Append(rewardIndex+1).Append('/').Append(Choices(q)).Append(": ").Append(reward.experience).Append(" EXP · ").Append(reward.money).Append(" oro");
            foreach(var item in reward.items)if(item.count>0)text.Append("\nObjeto ").Append(item.type).Append('/').Append(item.typeId).Append(" ×").Append(item.count);
            if(reason.Length>0)text.Append('\n').Append(reason);
            rewardText.text=text.ToString();action.interactable=reason.Length==0;
            action.GetComponentInChildren<Text>().text=entry==null?"Aceptar":"Entregar";
        }
        private void QuestAction(){SubmitSelectedQuest();}
        public bool SubmitSelectedQuest()
        {
            ActionFailure="La conversación o misión seleccionada no está disponible.";
            if(npc==null||modal==null||!modal.gameObject.activeInHierarchy||selectedQuest<0||!quests.Ready||
                !visibleQuestIds.Contains(selectedQuest)||!action.interactable)return false;
            string conversationFailure="La conversación ya no está activa.";
            if(hud==null||!LocalNpcInteractionGuard.Validate(quests.Actor,npc,hud.SelectedNpc,hud.Ready,out conversationFailure))
            {
                ActionFailure=conversationFailure??"La conversación ya no está activa.";
                CloseAll();ShowHint(ActionFailure);return false;
            }
            string reason;bool accepted;
            bool hadEntry=quests.Journal.Entries.ContainsKey(selectedQuest);
            int id=selectedQuest,index=rewardIndex;
            if(!hadEntry)accepted=quests.Journal.Accept(id,npc.ServiceKey,quests.Player,out reason);
            else accepted=quests.Journal.Deliver(id,npc.ServiceKey,index,out reason);
            ShowHint(accepted?(hadEntry?"Misión entregada. Recompensa guardada.":"Misión aceptada y guardada."):reason);
            ActionFailure=accepted?"":reason;
            RefreshOptions();action.interactable=false;
            if(accepted&&hadEntry&&quests.Journal.TryGet(id,out var q))ShowCompletion(q,index);
            return accepted;
        }
        private void ShowCompletion(LegacyQuestDefinition quest,int index)
        {
            CancelAbandon();abandon.gameObject.SetActive(false);
            var reward=quest.rewards[Mathf.Clamp(index,0,quest.rewards.Length-1)];
            narrative.text=Format(reward.completion);
            rewardText.text="Misión completada.\nRecompensa guardada: "+reward.experience+" EXP · "+reward.money+" oro";
            action.interactable=false;action.gameObject.SetActive(false);rewardCycle.gameObject.SetActive(false);
        }
        public bool RequestAbandonSelectedQuest()
        {
            ActionFailure="Selecciona una misión activa en el diario.";
            if(!journalView||modal==null||!modal.gameObject.activeInHierarchy||!quests.Ready||
                !visibleQuestIds.Contains(selectedQuest)||!quests.Journal.Entries.TryGetValue(selectedQuest,out var progress)||
                progress.stage==JournalStage.Rewarded||!quests.Journal.TryGet(selectedQuest,out var quest))return false;
            pendingAbandon=selectedQuest;
            abandonPrompt.text="¿Abandonar «"+quest.title+"»?\n\nSe perderá el progreso de esta misión. No se entregará ninguna recompensa.";
            abandonConfirmation.gameObject.SetActive(true);abandonConfirmation.SetAsLastSibling();
            ActionFailure="";return true;
        }
        public void CancelAbandon()
        {
            pendingAbandon=-1;
            if(abandonConfirmation!=null)abandonConfirmation.gameObject.SetActive(false);
        }
        public bool ConfirmAbandon()
        {
            ActionFailure="La confirmación ya no corresponde a una misión activa.";
            if(pendingAbandon<0||pendingAbandon!=selectedQuest||!journalView||!quests.Ready||
                abandonConfirmation==null||!abandonConfirmation.gameObject.activeInHierarchy||
                !quests.Journal.Entries.TryGetValue(pendingAbandon,out var progress)||progress.stage==JournalStage.Rewarded)
            {CancelAbandon();return false;}
            int id=pendingAbandon;
            // Local durability, not a forged native server response.
            if(!quests.Journal.Abandon(id,out string reason))
            {ActionFailure=reason;ShowHint(reason);return false;}
            CancelAbandon();selectedQuest=-1;RefreshOptions();ShowPresentation(false);modalTitle.text="Diario de misiones";
            action.gameObject.SetActive(false);rewardCycle.gameObject.SetActive(false);abandon.gameObject.SetActive(false);
            narrative.text="Misión abandonada. Puedes consultar al NPC para volver a aceptarla cuando cumplas sus requisitos.";
            rewardText.text="No se entregó ninguna recompensa.";
            ActionFailure="";ShowHint("Misión abandonada y guardada.");return true;
        }
        private void CycleReward()
        {
            if(selectedQuest<0||!quests.Ready||!quests.Journal.TryGet(selectedQuest,out var q))return;
            rewardIndex=(rewardIndex+1)%Mathf.Max(1,Choices(q));SelectQuest(selectedQuest);
        }
        private static int Choices(LegacyQuestDefinition q)=>q.resultType==1?1:Mathf.Clamp(q.userSelect,1,q.rewards.Length);
        private string Progress(LegacyQuestDefinition q,QuestProgress e)
        {
            if(e!=null&&e.stage==JournalStage.Ready)return "Lista para entregar";
            var s=new StringBuilder();
            if(q.mobCount1>0)s.Append(quests.MobName(q.mob1)).Append(": ").Append(e!=null?e.kills1:0).Append('/').Append(q.mobCount1);
            if(q.mobCount2>0)s.Append("\n").Append(quests.MobName(q.mob2)).Append(": ").Append(e!=null?e.kills2:0).Append('/').Append(q.mobCount2);
            foreach(var item in q.farmItems)if(item.count>0)s.Append("\nObjeto ").Append(item.type).Append('/').Append(item.typeId).Append(" ×").Append(item.count);
            return s.ToString();
        }
        private static string Format(string text)
        {
            return System.Text.RegularExpressions.Regex.Replace(text??"",@"\{/?c[0-9]*\}","")
                .Replace("\\n","\n").Replace("$n","\n");
        }
        private void CloseAll(){if(hud!=null)hud.CloseDialogue();CloseDialog();}
        private void CloseDialog()
        {CancelAbandon();if(modal!=null)modal.gameObject.SetActive(false);npc=null;journalView=false;WorldInputGate.Set(this,false);}
        private void ShowHint(string message){if(hud!=null)hud.ShowMessage(message);}
        private void OnDisable(){CloseAll();}
        private void OnDestroy()
        {
            if(hud!=null){hud.DialogueOpened-=NpcOpened;hud.DialogueClosed-=CloseDialog;hud.QuestMarker=null;hud.HasQuestPanel=false;}
            if(subscribed!=null)subscribed.Changed-=RefreshQuests;
            WorldInputGate.Set(this,false);
        }
        private RectTransform Panel(string name,RectTransform parent,Vector2 anchor,Vector2 position,Vector2 size)
        {
            var rect=Rect(name,parent,anchor,position,size);
            rect.gameObject.AddComponent<Image>().color=new Color(.025f,.03f,.04f,.80f);return rect;
        }
        private static RectTransform Rect(string name,RectTransform parent,Vector2 anchor,Vector2 position,Vector2 size)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);Box(rect,anchor,position,size);return rect;
        }
        private static void Box(RectTransform rect,Vector2 anchor,Vector2 position,Vector2 size)
        {rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;}
        private static void Inset(RectTransform rect,float amount)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.one*amount;rect.offsetMax=-Vector2.one*amount;}
        private Text Label(RectTransform parent,string text,int size)
        {
            var rect=Rect("Label",parent,Vector2.zero,Vector2.zero,Vector2.zero);Inset(rect,0);
            var label=rect.gameObject.AddComponent<Text>();label.font=font;label.fontSize=size;label.color=Color.white;
            label.text=text;label.supportRichText=false;label.raycastTarget=false;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;return label;
        }
        private Button Button(RectTransform parent,string text,UnityEngine.Events.UnityAction callback)
        {
            var rect=Panel("Action "+text,parent,new Vector2(0,1),Vector2.zero,new Vector2(260,42));
            rect.GetComponent<Image>().color=new Color(.16f,.14f,.1f,.95f);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.onClick.AddListener(callback);
            if(hud!=null&&hud.PresentationSkin!=null)NativeUiPrimitives.Skin(button,hud.PresentationSkin.command,true);
            var label=Label(rect,text,12);label.alignment=TextAnchor.MiddleCenter;Inset(label.rectTransform,5);return button;
        }
        private RectTransform Scroll(RectTransform parent,Vector2 position,Vector2 size)
        {
            var root=Rect("Scroll",parent,new Vector2(0,1),position,size);
            root.gameObject.AddComponent<Image>().color=Color.clear;
            var scroll=root.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity=24;
            var viewport=Rect("Viewport",root,new Vector2(0,1),Vector2.zero,new Vector2(size.x-18,size.y));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Content",viewport,new Vector2(0,1),Vector2.zero,new Vector2(size.x-18,0));
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.spacing=6;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.content=content;scroll.viewport=viewport;
            if(hud!=null&&hud.PresentationSkin!=null)root.gameObject.AddComponent<NativeScrollChrome>().Build(root,scroll,hud.PresentationSkin);
            return content;
        }
    }
}
