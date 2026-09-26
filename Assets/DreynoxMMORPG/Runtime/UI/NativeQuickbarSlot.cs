using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.UI
{
    public sealed class NativeQuickbarSlot : MonoBehaviour,IPointerDownHandler,IBeginDragHandler,IDragHandler,IEndDragHandler,IDropHandler
    {
        public NativeQuickbarView Owner {get;private set;}
        public int Bar {get;private set;}
        public int Slot {get;private set;}
        public int SourcePage {get;private set;}
        public int SourceRevision {get;private set;}
        private Image icon,cooldown;
        private Button button;
        private NativeUiTooltip tooltip;
        private bool dragged,assigned;
        public void Configure(NativeQuickbarView owner,int bar,int slot,Sprite artwork)
        {
            Owner=owner;Bar=bar;Slot=slot;
            var hit=gameObject.AddComponent<Image>();hit.color=Color.clear;
            button=gameObject.AddComponent<Button>();button.targetGraphic=hit;
            button.navigation=new Navigation{mode=Navigation.Mode.None};
            var colors=button.colors;colors.normalColor=Color.clear;colors.highlightedColor=new Color(1,1,1,.18f);
            colors.pressedColor=new Color(1,1,1,.3f);colors.selectedColor=Color.clear;colors.disabledColor=Color.clear;button.colors=colors;
            button.onClick.AddListener(()=>{if(!dragged)Owner.Activate(Bar,Slot);});
            var root=GetComponent<RectTransform>();
            icon=NativeUiPrimitives.Rect("Original action icon",root,Vector2.zero,new Vector2(32,32)).gameObject.AddComponent<Image>();
            icon.sprite=artwork;icon.raycastTarget=false;
            cooldown=NativeUiPrimitives.Rect("Actual attack recovery",root,Vector2.zero,new Vector2(32,32)).gameObject.AddComponent<Image>();
            cooldown.sprite=artwork;cooldown.color=new Color(0,0,0,.65f);cooldown.raycastTarget=false;
            cooldown.type=Image.Type.Filled;cooldown.fillMethod=Image.FillMethod.Radial360;cooldown.fillOrigin=2;cooldown.fillClockwise=true;
            NativeUiPrimitives.Text("Key",root,bar==0?(slot==9?"0":(slot+1).ToString()):"",10,new Vector2(1,0),new Vector2(16,12));
            tooltip=gameObject.AddComponent<NativeUiTooltip>();
        }
        public void Refresh(bool hasAction)
        {
            assigned=hasAction;icon.enabled=hasAction;button.interactable=hasAction;
            tooltip.Message=hasAction?"Ataque básico\nClic o acceso numérico de la barra principal.\nArrastra para cambiar el acceso.":"Acceso vacío\nArrastra una acción desde otra casilla.";
            cooldown.enabled=false;
        }
        public void SetCooldown(float fraction){cooldown.enabled=assigned&&fraction>0;cooldown.fillAmount=Mathf.Clamp01(fraction);}
        public void OnPointerDown(PointerEventData e){dragged=false;}
        public void OnBeginDrag(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left||!assigned)return;
            dragged=true;SourcePage=Owner.Core.Page(Bar);SourceRevision=Owner.Core.Revision;Owner.BeginDrag(this);
        }
        public void OnDrag(PointerEventData e){if(dragged)Owner.MoveGhost();}
        public void OnEndDrag(PointerEventData e){if(dragged)Owner.CancelDrag();}
        public void OnDrop(PointerEventData e)
        {
            var source=e.pointerDrag!=null?e.pointerDrag.GetComponent<NativeQuickbarSlot>():null;
            if(source!=null&&source.Owner==Owner)Owner.Drop(source,Bar,Slot);
        }
        private void OnDisable(){if(dragged&&Owner!=null)Owner.CancelDrag();}
    }
}
