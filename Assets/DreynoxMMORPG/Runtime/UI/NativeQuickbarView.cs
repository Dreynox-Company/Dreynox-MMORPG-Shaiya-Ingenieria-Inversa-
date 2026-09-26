using System;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.Interaction;
using Dreynox.Mmorpg.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.UI
{
    public sealed class NativeQuickbarView : MonoBehaviour
    {
        private sealed class Bar
        {
            public RectTransform root;
            public RawImage frame;
            public Button previous,next,expand,rotate;
            public Text page;
            public NativeQuickbarSlot[] cells=new NativeQuickbarSlot[10];
            public NativeWindowDrag drag;
        }
        private readonly Bar[] bars=new Bar[3];
        private NativeHudSkin skin;
        private ShaiyaClientActor actor;
        private ShaiyaCombatInteraction combat;
        private RectTransform canvas;
        private Action<string> feedback;
        private int shownRevision=-1;
        private bool preferencesInvalid;
        private NativeUiLocalStore store;
        private Image ghost;
        private NativeQuickbarSlot dragging;
        public NativeQuickbarCore Core {get;private set;}
        public bool Ready => Core!=null&&bars[0]!=null;
        public void Build(RectTransform parent,NativeHudSkin artwork,ShaiyaClientActor player,ShaiyaCombatInteraction interaction,Action<string> message,bool loadPreferences=true)
        {
            if(Core!=null)throw new InvalidOperationException("Quickbar already built.");
            skin=artwork;skin.Validate();actor=player;combat=interaction;canvas=parent;feedback=message;
            Core=new NativeQuickbarCore();
            bool qualification=Array.IndexOf(Environment.GetCommandLineArgs(),"--starting-world-qualification")>=0;
            store=qualification||!loadPreferences?null:new NativeUiLocalStore(System.IO.Path.Combine(Application.persistentDataPath,"LocalUI","ps0032"));
            if(store!=null)
            {
                try {string text=store.Read("quickbar.json");if(text!=null)Core.Restore(JsonUtility.FromJson<QuickbarSave>(text));}
                catch(Exception ex){preferencesInvalid=true;feedback?.Invoke("La barra guardada no se cargó; el archivo permanece intacto: "+ex.Message);}
                Core.Persist=s=>{if(preferencesInvalid)return false;store.Write("quickbar.json",JsonUtility.ToJson(s));return true;};
            }
            for(int i=0;i<bars.Length;i++)
            {
                int id=i;var b=new Bar();bars[i]=b;
                b.root=NativeUiPrimitives.Rect("Native quickbar "+i,parent,new Vector2(Mathf.Floor(parent.rect.width/2)-223,54*i),new Vector2(446,53));
                var hit=b.root.gameObject.AddComponent<Image>();hit.color=Color.clear;
                b.frame=NativeUiPrimitives.Art("Original slot frame",b.root,skin.horizontalBar,new Rect(0,0,446,53),Vector2.zero);
                b.drag=b.root.gameObject.AddComponent<NativeWindowDrag>();b.drag.Error=message;b.drag.Configure(b.root,"quickbar-"+i,store);
                b.previous=NativeUiPrimitives.Button("Previous page",b.root,skin.previous,Vector2.zero,new Vector2(16,8),()=>ChangePage(id,-1));
                b.next=NativeUiPrimitives.Button("Next page",b.root,skin.next,Vector2.zero,new Vector2(16,8),()=>ChangePage(id,1));
                b.expand=NativeUiPrimitives.Button("Additional bar",b.root,skin.expand,Vector2.zero,new Vector2(16,16),()=>{Core.ToggleNextBar(id);Changed();});
                b.rotate=NativeUiPrimitives.Button("Rotate bar",b.root,skin.rotate,Vector2.zero,new Vector2(16,16),()=>{Core.Rotate(id);Changed();});
                b.page=NativeUiPrimitives.Text("Page",b.root,"1",12,Vector2.zero,new Vector2(16,16),TextAnchor.MiddleCenter);
                b.previous.gameObject.AddComponent<NativeUiTooltip>().Message="Página anterior";
                b.next.gameObject.AddComponent<NativeUiTooltip>().Message="Página siguiente";
                b.rotate.gameObject.AddComponent<NativeUiTooltip>().Message="Girar la barra horizontal / vertical";
                b.expand.gameObject.AddComponent<NativeUiTooltip>().Message="Mostrar u ocultar barra adicional";
                for(int j=0;j<10;j++)
                {
                    var cell=NativeUiPrimitives.Rect("Access "+(j+1),b.root,Vector2.zero,new Vector2(32,32));
                    b.cells[j]=cell.gameObject.AddComponent<NativeQuickbarSlot>();b.cells[j].Configure(this,i,j,skin.attackIcon);
                }
            }
            if(combat!=null)combat.NativeQuickbarOwnsInput=true;Refresh();
        }
        private void ChangePage(int bar,int direction){Core.SetPage(bar,Mathf.Clamp(Core.Page(bar)+direction,0,4));Changed();}
        private void Changed(){if(!String.IsNullOrEmpty(Core.Failure))feedback?.Invoke(Core.Failure);Refresh();}
        public void Refresh()
        {
            if(!Ready)return;
            for(int i=0;i<3;i++)
            {
                var b=bars[i];bool vertical=Core.Vertical(i);var geometry=NativeHudGeometry.Bar(vertical);
                b.root.gameObject.SetActive(i<Core.VisibleBars);b.root.sizeDelta=new Vector2(geometry.Width,geometry.Height);
                b.frame.texture=vertical?skin.verticalBar:skin.horizontalBar;
                b.frame.rectTransform.sizeDelta=b.root.sizeDelta;
                b.frame.uvRect=NativeRadarView.TopLeftUv(b.frame.texture,new Rect(0,0,geometry.Width,geometry.Height));
                NativeUiPrimitives.Skin(b.previous,vertical?skin.previousVertical:skin.previous);
                NativeUiPrimitives.Skin(b.next,vertical?skin.nextVertical:skin.next);
                Position(b.previous.GetComponent<RectTransform>(),vertical?new Vector2(7,6):new Vector2(6,8),vertical?new Vector2(8,16):new Vector2(16,8));
                Position(b.next.GetComponent<RectTransform>(),vertical?new Vector2(31,6):new Vector2(6,32),vertical?new Vector2(8,16):new Vector2(16,8));
                Position(b.page.rectTransform,vertical?new Vector2(16,6):new Vector2(6,16),new Vector2(16,16));
                b.previous.interactable=Core.Page(i)>0;b.next.interactable=Core.Page(i)<4;
                b.page.text=(Core.Page(i)+1).ToString();
                NativeUiPrimitives.Skin(b.expand,i==2||Core.VisibleBars>i+1?skin.collapse:skin.expand);
                Position(b.expand.GetComponent<RectTransform>(),vertical?new Vector2(25,421):new Vector2(421,5),new Vector2(16,16));
                Position(b.rotate.GetComponent<RectTransform>(),vertical?new Vector2(5,421):new Vector2(421,25),new Vector2(16,16));
                for(int j=0;j<10;j++)
                {
                    var cell=NativeHudGeometry.Cell(j,vertical);
                    Position(b.cells[j].GetComponent<RectTransform>(),new Vector2(cell.X,cell.Y),new Vector2(32,32));
                    b.cells[j].Refresh(Core.ActionAt(i,j)!=0);
                }
                b.drag.Clamp();
            }
            shownRevision=Core.Revision;
        }
        private static void Position(RectTransform rect,Vector2 at,Vector2 size){rect.anchoredPosition=new Vector2(at.x,-at.y);rect.sizeDelta=size;}
        private void Update()
        {
            if(!Ready)return;
            if(Core.Revision!=shownRevision)Refresh();
            if(dragging!=null&&Input.GetKeyDown(KeyCode.Escape))CancelDrag();
            bool blocked=WorldInputGate.IsBlocked||NativeUiPrimitives.TextEditing||!Application.isFocused;
            if(!blocked)
                for(int i=0;i<10;i++)
                    if(Input.GetKeyDown(i==9?KeyCode.Alpha0:(KeyCode)((int)KeyCode.Alpha1+i)))Activate(0,i);
            float remaining=actor!=null?(float)actor.Combat.AttackRemainingFraction:0;
            for(int i=0;i<Core.VisibleBars;i++)foreach(var cell in bars[i].cells)cell.SetCooldown(remaining);
        }
        public bool Activate(int bar,int slot)
        {
            if(!Ready)return false;
            bool result=Core.Activate(bar,slot,Time.frameCount,WorldInputGate.IsBlocked||NativeUiPrimitives.TextEditing,
                action=>combat!=null&&action==NativeQuickbarCore.BasicAttack&&combat.TryBasicAttack());
            if(!result&&Core.ActionAt(bar,slot)!=0&&combat!=null)feedback?.Invoke(combat.Feedback);
            return result;
        }
        public void BeginDrag(NativeQuickbarSlot source)
        {
            CancelDrag();if(Core.ActionAt(source.Bar,source.Slot)==0)return;
            dragging=source;WorldInputGate.Set(this,true);
            var rect=NativeUiPrimitives.Rect("Dragged action",canvas,Vector2.zero,new Vector2(32,32));
            var layer=rect.gameObject.AddComponent<Canvas>();layer.overrideSorting=true;layer.sortingOrder=250;
            ghost=rect.gameObject.AddComponent<Image>();ghost.sprite=skin.attackIcon;ghost.color=new Color(1,1,1,.8f);ghost.raycastTarget=false;
            MoveGhost();
        }
        public void MoveGhost()
        {if(ghost!=null)ghost.rectTransform.anchoredPosition=new Vector2(Input.mousePosition.x-16,-(Screen.height-Input.mousePosition.y-16));}
        public bool Drop(NativeQuickbarSlot source,int destinationBar,int destinationSlot)
        {
            if(source!=dragging||source.Owner!=this)return false;
            bool moved=Core.Move(source.SourcePage,source.Slot,Core.Page(destinationBar),destinationSlot,source.SourceRevision);
            Changed();return moved;
        }
        public void CancelDrag()
        {
            dragging=null;WorldInputGate.Set(this,false);
            if(ghost!=null){if(Application.isPlaying)Destroy(ghost.gameObject);else DestroyImmediate(ghost.gameObject);ghost=null;}
        }
        private void OnDisable(){CancelDrag();}
        private void OnDestroy(){CancelDrag();if(combat!=null)combat.NativeQuickbarOwnsInput=false;}
    }
}
