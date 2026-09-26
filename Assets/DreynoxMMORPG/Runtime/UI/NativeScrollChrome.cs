using UnityEngine;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.UI
{
    /// <summary>Scroll controls from the original atlas, bound to the actual ScrollRect.</summary>
    public sealed class NativeScrollChrome : MonoBehaviour
    {
        private ScrollRect scroll;
        private Scrollbar scrollbar;
        private Button up,down;
        public void Build(RectTransform root,ScrollRect value,NativeHudSkin skin)
        {
            scroll=value;float x=root.rect.width-13,height=root.rect.height;
            up=NativeUiPrimitives.Button("Scroll up",root,skin.scrollUp,new Vector2(x,0),new Vector2(13,19),()=>Step(1));
            down=NativeUiPrimitives.Button("Scroll down",root,skin.scrollDown,new Vector2(x,height-19),new Vector2(13,19),()=>Step(-1));
            var track=NativeUiPrimitives.Rect("Scroll track",root,new Vector2(x+2,19),new Vector2(9,Mathf.Max(9,height-38)));
            track.gameObject.AddComponent<Image>().color=new Color(0,0,0,.25f);
            scrollbar=track.gameObject.AddComponent<Scrollbar>();scrollbar.direction=Scrollbar.Direction.BottomToTop;
            var handle=NativeUiPrimitives.Rect("Original thumb",track,Vector2.zero,new Vector2(9,20));
            var middle=handle.gameObject.AddComponent<Image>();middle.sprite=skin.scrollMiddle;middle.type=Image.Type.Simple;
            var top=NativeUiPrimitives.Rect("Thumb top",handle,Vector2.zero,new Vector2(9,4));top.gameObject.AddComponent<Image>().sprite=skin.scrollTop;
            var bottom=NativeUiPrimitives.Rect("Thumb bottom",handle,Vector2.zero,new Vector2(9,4));bottom.anchorMin=bottom.anchorMax=bottom.pivot=Vector2.zero;
            bottom.gameObject.AddComponent<Image>().sprite=skin.scrollBottom;
            top.GetComponent<Image>().raycastTarget=false;bottom.GetComponent<Image>().raycastTarget=false;
            scrollbar.targetGraphic=middle;scrollbar.handleRect=handle;scrollbar.navigation=new Navigation{mode=Navigation.Mode.None};
            scroll.verticalScrollbar=scrollbar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;
        }
        private void Step(int direction)
        {
            float hidden=scroll.content.rect.height-scroll.viewport.rect.height;
            if(hidden>0)scroll.verticalNormalizedPosition=Mathf.Clamp01(scroll.verticalNormalizedPosition+direction*36f/hidden);
        }
        private void LateUpdate()
        {
            if(scroll==null)return;
            bool overflow=scroll.content.rect.height>scroll.viewport.rect.height+.5f;
            scrollbar.gameObject.SetActive(overflow);up.interactable=overflow&&scroll.verticalNormalizedPosition<.999f;
            down.interactable=overflow&&scroll.verticalNormalizedPosition>.001f;
        }
    }
}
