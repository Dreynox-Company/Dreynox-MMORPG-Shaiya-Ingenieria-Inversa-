using System;
using System.Collections.Generic;
using System.Text;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.UI
{
    /// <summary>Live world UI using original artwork. No fabricated quest completion or server stats.</summary>
    public sealed class NativeWorldHud : MonoBehaviour
    {
        [SerializeField] private Texture2D playerFrame, classIcon, actionFrame, talkFrame, minimap, targetFrame, targetBar;
        private ShaiyaClientActor actor;
        private ShaiyaCombatInteraction combat;
        private LegacyNpcInteractionRuntime interaction;
        private LegacyNpcSpawnStreamer npcs;
        private LegacyMonsterSpawnStreamer mobs;
        private Camera cameraView;
        private RectTransform canvasRoot, worldLabelRoot, dialogue;
        private Text targetText, targetHealthText, feedbackText, welcomeText, npcNameText, playerText;
        private RectTransform targetPanel;
        private Image targetFill;
        private Sprite healthSprite;
        private NativeWorldSession session;
        private LegacyNpcRuntimeDescriptor selectedNpc;
        [SerializeField] private NativeRadarSkin radarSkin;
        private NativeRadarView radar;
        private readonly List<Text> labels = new List<Text>();

        private float nextUpdate;
        private int visibleLabels;
        public int DialoguesOpened { get; private set; }
        public NativeRadarView Radar => radar;
        public void SetRadarSkin(NativeRadarSkin value) { radarSkin = value; }
        public void SetTargetArtwork(Texture2D frame,Texture2D bar) { targetFrame=frame;targetBar=bar; }
        public RectTransform CanvasRoot => canvasRoot;
        public bool HasQuestPanel { get; set; }
        public Func<int,string> QuestMarker { get; set; }
        public event Action<LegacyNpcRuntimeDescriptor> DialogueOpened;
        public event Action DialogueClosed;
        public void ShowMessage(string message) { if(feedbackText!=null) feedbackText.text=message??string.Empty; }
        public int VisibleNpcNames { get; private set; }
        public bool Ready { get; private set; }
        public LegacyNpcRuntimeDescriptor SelectedNpc => selectedNpc;
        public void SetArtwork(Texture2D frame, Texture2D icon, Texture2D slots, Texture2D talk, Texture2D map)
        { playerFrame=frame; classIcon=icon; actionFrame=slots; talkFrame=talk; minimap=map; }

        private void Start()
        {
            actor=FindFirstObjectByType<ShaiyaClientActor>();
            combat=FindFirstObjectByType<ShaiyaCombatInteraction>();
            interaction=FindFirstObjectByType<LegacyNpcInteractionRuntime>();
            npcs=FindFirstObjectByType<LegacyNpcSpawnStreamer>();
            mobs=FindFirstObjectByType<LegacyMonsterSpawnStreamer>();
            cameraView=Camera.main;
            if(actor==null || combat==null || interaction==null || cameraView==null || playerFrame==null || minimap==null || radarSkin==null || targetFrame==null || targetBar==null)
            { Debug.LogError("Native world HUD missing required bindings or original artwork.");return; }
            session=FindFirstObjectByType<NativeWorldSession>();
            BuildUi();
            combat.HitApplied+=OnHit;
            Ready=true;
        }
        private void OnDisable() { CloseDialogue(); }
        private void OnDestroy() { Dreynox.Mmorpg.Interaction.WorldInputGate.Set(this,false); if(combat!=null) combat.HitApplied-=OnHit; if(healthSprite!=null)Destroy(healthSprite); }
        private void OnHit(ShaiyaCombatTarget target,int damage)
        { feedbackText.text=target.IsAlive?"Daño: "+damage:"Objetivo derrotado"; }
        private void Update()
        {
            if(!Ready) return;
            if(Input.GetKeyDown(KeyCode.Escape)) CloseDialogue();
            bool overUi=Dreynox.Mmorpg.Interaction.WorldInputGate.IsBlocked || (EventSystem.current!=null && EventSystem.current.IsPointerOverGameObject());
            if(!overUi && Input.GetMouseButtonDown(0))
            {
                Ray ray=cameraView.ScreenPointToRay(Input.mousePosition);
                if(Physics.Raycast(ray,out RaycastHit hit,100f,~0,QueryTriggerInteraction.Ignore))
                {
                    var npc=hit.collider.GetComponentInParent<LegacyNpcRuntimeDescriptor>();
                    if(npc!=null) TryTalk(npc);
                }
            }
            if(!overUi && Input.GetKeyDown(KeyCode.F))
            {
                LegacyNpcRuntimeDescriptor closest=null;float distance=25f;
                if(npcs!=null) foreach(var spawn in npcs.Spawns)
                {
                    if(spawn.activeInstance==null)continue;
                    float sqr=(spawn.activeInstance.transform.position-actor.transform.position).sqrMagnitude;
                    if(sqr>=distance)continue;
                    closest=spawn.activeInstance.GetComponent<LegacyNpcRuntimeDescriptor>();distance=sqr;
                }
                if(closest!=null)TryTalk(closest);
                else feedbackText.text="Acércate a un NPC y pulsa F o haz clic sobre él.";
            }
            if(selectedNpc!=null && (!selectedNpc.gameObject.activeInHierarchy ||
                (selectedNpc.transform.position-actor.transform.position).sqrMagnitude>36f))CloseDialogue();
            if(Time.unscaledTime<nextUpdate)return;
            nextUpdate=Time.unscaledTime+0.1f;
            UpdateWorldUi();
        }
        public bool TryTalk(LegacyNpcRuntimeDescriptor npc)
        {
            if(!Ready || npc==null || !npc.gameObject.activeInHierarchy)return false;
            Vector3 origin=actor.transform.position+Vector3.up;
            Vector3 destination=npc.transform.position+Vector3.up;
            if((destination-origin).sqrMagnitude>25f)
            {feedbackText.text="Acércate para hablar con "+npc.DisplayName;return false;}
            Vector3 line=destination-origin;
            if(line.sqrMagnitude>0.0001f)
            foreach(var hit in Physics.RaycastAll(origin,line.normalized,line.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(actor.transform) && !hit.transform.IsChildOf(npc.transform))
                {feedbackText.text="La conversación está obstruida.";return false;}
            if(!interaction.Open(npc))return false;
            selectedNpc=npc;DialoguesOpened++;
            npcNameText.text=npc.DisplayName;
            var text=new StringBuilder(npc.WelcomeMessage);
            if(!HasQuestPanel && npc.InQuestIds.Count+npc.OutQuestIds.Count>0)
                text.Append("\n\nEste NPC está vinculado a misiones originales. La disponibilidad y recompensas aún requieren integración del catálogo de misiones.");
            welcomeText.text=text.ToString();
            dialogue.gameObject.SetActive(!HasQuestPanel);
            Dreynox.Mmorpg.Interaction.WorldInputGate.Set(this,true);
            DialogueOpened?.Invoke(npc);
            feedbackText.text="Conversando con "+npc.DisplayName;
            return true;
        }
        public void CloseDialogue()
        {
            if(interaction!=null)interaction.Close();
            selectedNpc=null;
            if(dialogue!=null)dialogue.gameObject.SetActive(false);
            Dreynox.Mmorpg.Interaction.WorldInputGate.Set(this,false);
            DialogueClosed?.Invoke();
        }
        private void UpdateWorldUi()
        {
            playerText.text="Dreynox · Mapa "+(session!=null?session.MapId:1)+"\n"+
                "X "+actor.transform.position.x.ToString("F1")+"   Z "+actor.transform.position.z.ToString("F1");
            var target=combat.SelectedTarget;
            bool valid=target!=null && target.gameObject.activeInHierarchy;
            targetPanel.gameObject.SetActive(valid);
            targetText.text=valid?DisplayTarget(target):"";
            targetHealthText.text=valid?target.Health+" / "+target.MaxHealth:"";
            targetFill.fillAmount=valid?(float)target.Health/Mathf.Max(1,target.MaxHealth):0f;
            // Current starting-world terrain extent, not an assumption for every future map.
            Vector2 extent = Terrain.activeTerrain != null
                ? new Vector2(Terrain.activeTerrain.terrainData.size.x, Terrain.activeTerrain.terrainData.size.z)
                : new Vector2(2048, 2048);
            radar.BeginFrame(actor.transform.position, actor.transform.eulerAngles.y, extent);
            visibleLabels=0;VisibleNpcNames=0;
            if(npcs!=null) foreach(var spawn in npcs.Spawns)
            {
                if(spawn.activeInstance==null)continue;
                Vector3 p=spawn.activeInstance.transform.position;
                string marker=QuestMarker!=null?QuestMarker((spawn.npcType<<16)|(ushort)spawn.typeId):string.Empty;
                if(Label(p,spawn.displayName+marker,new Color(0.4f,0.96f,1f)))VisibleNpcNames++;
                var descriptor=spawn.activeInstance.GetComponent<LegacyNpcRuntimeDescriptor>();
                radar.Add(p, ResolveNpcRadar(descriptor, marker));
            }
            if(mobs!=null)foreach(var spawn in mobs.Spawns)
            {
                if(spawn.activeInstance==null)continue;
                var life=spawn.activeInstance.GetComponent<ShaiyaCombatTarget>();
                if(life==null || !life.IsAlive)continue;
                Vector3 p=spawn.activeInstance.transform.position;
                Label(p,spawn.mobName+" · "+spawn.level,new Color(1f,0.7f,0.35f));
                radar.Add(p,NativeRadarKind.Monster);
            }
            for(int i=visibleLabels;i<labels.Count;i++)labels[i].gameObject.SetActive(false);
            radar.EndFrame();
        }
        private string DisplayTarget(ShaiyaCombatTarget target)
        {
            if(mobs!=null)foreach(var spawn in mobs.Spawns)
                if(spawn.targetId==target.TargetId)return spawn.mobName+" · Nivel "+spawn.level;
            return "Objetivo";
        }
        private bool Label(Vector3 position,string text,Color color)
        {
            if(visibleLabels>=80 || string.IsNullOrWhiteSpace(text) || (position-actor.transform.position).sqrMagnitude>65f*65f)return false;
            Vector3 screen=cameraView.WorldToScreenPoint(position+Vector3.up*2.1f);
            if(screen.z<=0 || screen.x<0 || screen.y<0 || screen.x>Screen.width || screen.y>Screen.height)return false;
            if(visibleLabels==labels.Count)
            {
                Text label=TextElement("World label",worldLabelRoot,"",13,TextAnchor.MiddleCenter);
                label.rectTransform.sizeDelta=new Vector2(280,25);
                labels.Add(label);
            }
            Text item=labels[visibleLabels++];item.gameObject.SetActive(true);item.text=text;item.color=color;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(worldLabelRoot,screen,null,out Vector2 point);
            item.rectTransform.anchoredPosition=point;
            return true;
        }
        public static NativeRadarKind ResolveNpcRadar(LegacyNpcRuntimeDescriptor npc,string questMarker)
        {
            if(!string.IsNullOrEmpty(questMarker))
            {
                if(questMarker.Contains("?"))return NativeRadarKind.QuestReady;
                if(questMarker.Contains("!"))return NativeRadarKind.QuestAvailable;
            }
            if(npc==null)return NativeRadarKind.Npc;
            switch(npc.NpcType)
            {
                case 2:return NativeRadarKind.Gatekeeper;
                case 3:return NativeRadarKind.Blacksmith;
                case 6:return NativeRadarKind.Warehouse;
                case 1:return NativeRadarKind.Merchant;
                default:return NativeRadarKind.Npc;
            }
        }
        private void BuildUi()
        {
            if(EventSystem.current==null)new GameObject("World UI Events",typeof(EventSystem),typeof(StandaloneInputModule));
            var go=new GameObject("World HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            go.transform.SetParent(transform,false);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1024,768);scaler.matchWidthOrHeight=0.5f;
            canvasRoot=go.GetComponent<RectTransform>();
            var labelGo=new GameObject("World names layer",typeof(RectTransform),typeof(Canvas));
            worldLabelRoot=labelGo.GetComponent<RectTransform>();worldLabelRoot.SetParent(canvasRoot,false);
            worldLabelRoot.anchorMin=Vector2.zero;worldLabelRoot.anchorMax=Vector2.one;
            worldLabelRoot.offsetMin=worldLabelRoot.offsetMax=Vector2.zero;
            var nameCanvas=labelGo.GetComponent<Canvas>();nameCanvas.overrideSorting=true;nameCanvas.sortingOrder=-1;

            Artwork("Original player frame",canvasRoot,playerFrame,new Rect(0,0,218,64),new Vector2(0,1),new Vector2(218,64),new Vector2(113,-36));
            Artwork("Class",canvasRoot,classIcon,new Rect(0,20,41,44),new Vector2(0,1),new Vector2(41,44),new Vector2(34.5f,-36));
            playerText=TextElement("Player",canvasRoot,"Dreynox",12,TextAnchor.MiddleLeft);
            Place(playerText.rectTransform,new Vector2(0,1),new Vector2(158,50),new Vector2(158,-43));
            Artwork("Original quickbar",canvasRoot,actionFrame,new Rect(0,10,448,54),new Vector2(0.5f,1),new Vector2(448,54),new Vector2(0,-32));
            for(int i=0;i<4;i++)
            {
                int index=i;
                ButtonElement(canvasRoot,(i+1).ToString(),new Vector2(0.5f,1),new Vector2(37,37),new Vector2(-178+i*39,-33),()=>combat.TryAttackSelected(new[]{95,130,180,240}[index]),false);
            }
            targetPanel=Rect("Target",canvasRoot,new Vector2(0.5f,1),new Vector2(193,38),new Vector2(0,-94));
            Artwork("Original target frame",targetPanel,targetFrame,new Rect(0,26,193,38),Vector2.one*0.5f,new Vector2(193,38),Vector2.zero);
            var fillRect=Rect("Target health",targetPanel,new Vector2(0,1),new Vector2(150,8),new Vector2(112,-29));
            targetFill=fillRect.gameObject.AddComponent<Image>();targetFill.color=Color.white;targetFill.raycastTarget=false;
            healthSprite=Sprite.Create(targetBar,new Rect(0,0,150,8),Vector2.one*0.5f,100,0,SpriteMeshType.FullRect);
            targetFill.sprite=healthSprite;targetFill.type=Image.Type.Filled;targetFill.fillMethod=Image.FillMethod.Horizontal;
            targetText=TextElement("Target name",targetPanel,"",12,TextAnchor.MiddleCenter);
            Place(targetText.rectTransform,new Vector2(0,1),new Vector2(150,18),new Vector2(112,-11));
            targetHealthText=TextElement("Actual target health",targetPanel,"",10,TextAnchor.MiddleCenter);
            Place(targetHealthText.rectTransform,new Vector2(0,1),new Vector2(150,12),new Vector2(112,-29));
            targetPanel.gameObject.SetActive(false);
            var radarRoot=Rect("Native radar",canvasRoot,new Vector2(1,1),new Vector2(202,226),new Vector2(-107,-120));
            radar=radarRoot.gameObject.AddComponent<NativeRadarView>();radar.Build(radarRoot,radarSkin,minimap);
            feedbackText=TextElement("Action feedback",canvasRoot,"WASD · Shift: correr · Clic: objetivo · 1–4: atacar · F: hablar · Esc: cerrar",13,TextAnchor.MiddleLeft);
            Place(feedbackText.rectTransform,new Vector2(0,0),new Vector2(780,38),new Vector2(402,30));
            dialogue=Rect("NPC dialogue",canvasRoot,Vector2.one*0.5f,new Vector2(400,330),new Vector2(-140,-15));
            var panel=dialogue.gameObject.AddComponent<Image>();panel.color=new Color(0.06f,0.05f,0.04f,0.97f);
            Artwork("Original dialogue border",dialogue,talkFrame,new Rect(1,282,342,229),Vector2.one*0.5f,new Vector2(400,330),Vector2.zero);
            npcNameText=TextElement("NPC name",dialogue,"",20,TextAnchor.MiddleLeft);Place(npcNameText.rectTransform,new Vector2(0.5f,1),new Vector2(360,42),new Vector2(0,-35));
            npcNameText.color=new Color(1f,0.88f,0.52f);
            welcomeText=TextElement("Original greeting",dialogue,"",16,TextAnchor.UpperLeft);Place(welcomeText.rectTransform,Vector2.one*0.5f,new Vector2(350,205),new Vector2(0,0));
            ButtonElement(dialogue,"Cerrar",new Vector2(0.5f,0),new Vector2(130,33),new Vector2(0,30),CloseDialogue,true);
            dialogue.gameObject.SetActive(false);
        }
        private static RawImage Artwork(string name,RectTransform parent,Texture2D texture,Rect pixels,Vector2 anchor,Vector2 size,Vector2 offset)
        {
            var image=Rect(name,parent,anchor,size,offset).gameObject.AddComponent<RawImage>();image.texture=texture;image.raycastTarget=false;
            if(texture!=null)image.uvRect=new Rect(pixels.x/texture.width,pixels.y/texture.height,pixels.width/texture.width,pixels.height/texture.height);
            return image;
        }
        private static RectTransform Rect(string name,RectTransform parent,Vector2 anchor,Vector2 size,Vector2 offset)
        {
            var value=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();value.SetParent(parent,false);Place(value,anchor,size,offset);return value;
        }
        private static void Place(RectTransform rect,Vector2 anchor,Vector2 size,Vector2 offset)
        {rect.anchorMin=rect.anchorMax=anchor;rect.pivot=Vector2.one*0.5f;rect.sizeDelta=size;rect.anchoredPosition=offset;}
        private static Text TextElement(string name,RectTransform parent,string text,int size,TextAnchor alignment)
        {
            var value=Rect(name,parent,Vector2.one*0.5f,new Vector2(200,35),Vector2.zero).gameObject.AddComponent<Text>();
            value.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");value.text=text;value.fontSize=size;value.color=Color.white;
            value.alignment=alignment;value.supportRichText=false;value.raycastTarget=false;
            value.gameObject.AddComponent<Shadow>().effectDistance=new Vector2(1,-1);
            return value;
        }
        private static void ButtonElement(RectTransform parent,string label,Vector2 anchor,Vector2 size,Vector2 offset,Action action,bool background)
        {
            var root=Rect(label,parent,anchor,size,offset);var image=root.gameObject.AddComponent<Image>();image.color=background?new Color(0.2f,0.14f,0.08f,0.9f):new Color(0,0,0,0.04f);
            var button=root.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>action());
            var text=TextElement("Label",root,label,14,TextAnchor.MiddleCenter);text.rectTransform.sizeDelta=size;
        }
    }
}
