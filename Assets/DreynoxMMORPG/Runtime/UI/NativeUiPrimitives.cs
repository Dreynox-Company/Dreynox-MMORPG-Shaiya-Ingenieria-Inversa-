using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.UI
{
    public static class NativeUiPrimitives
    {
        private static Font font;
        public static bool UsesNativeFontFamily {get;private set;}
        public static Font Font
        {
            get
            {
                if(font!=null)return font;
                var installed=UnityEngine.Font.GetOSInstalledFontNames();
                UsesNativeFontFamily=Array.Exists(installed,n=>String.Equals(n,"Arial",StringComparison.OrdinalIgnoreCase));
                font=UsesNativeFontFamily?UnityEngine.Font.CreateDynamicFontFromOSFont("Arial",12):Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                // The fallback is explicit evidence, never labelled native Arial.
                return font;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){font=null;UsesNativeFontFamily=false;}
        public static bool PointerCaptured => EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject();
        public static bool TextEditing => EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject!=null&&
            EventSystem.current.currentSelectedGameObject.GetComponentInParent<InputField>()!=null;
        public static RectTransform Rect(string name,RectTransform parent,Vector2 position,Vector2 size)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.sizeDelta=size;
            rect.anchoredPosition=new Vector2(Mathf.Round(position.x),-Mathf.Round(position.y));return rect;
        }
        public static Text Text(string name,RectTransform parent,string text,int size,Vector2 at,Vector2 dimensions,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var value=Rect(name,parent,at,dimensions).gameObject.AddComponent<Text>();
            value.font=Font;value.fontSize=size;value.text=text;value.color=Color.white;value.alignment=align;
            value.supportRichText=false;value.raycastTarget=false;value.resizeTextForBestFit=false;
            value.horizontalOverflow=HorizontalWrapMode.Wrap;value.verticalOverflow=VerticalWrapMode.Truncate;
            var shadow=value.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.9f);shadow.effectDistance=new Vector2(1,-1);
            return value;
        }
        public static RawImage Art(string name,RectTransform parent,Texture2D texture,Rect source,Vector2 at)
        {
            var image=Rect(name,parent,at,source.size).gameObject.AddComponent<RawImage>();image.texture=texture;
            image.uvRect=NativeRadarView.TopLeftUv(texture,source);image.color=Color.white;image.raycastTarget=false;return image;
        }
        public static void Skin(Button button,Sprite[] states,bool sliced=false)
        {
            if(states==null||states.Length!=4||Array.Exists(states,s=>s==null))throw new ArgumentException("Four original button states required.");
            var image=button.GetComponent<Image>();image.sprite=states[0];image.color=Color.white;
            image.type=sliced?Image.Type.Sliced:Image.Type.Simple;button.targetGraphic=image;
            button.transition=Selectable.Transition.SpriteSwap;
            button.spriteState=new SpriteState{highlightedSprite=states[1],pressedSprite=states[2],disabledSprite=states[3],selectedSprite=states[1]};
            button.navigation=new Navigation{mode=Navigation.Mode.None};
        }
        public static Button Button(string name,RectTransform parent,Sprite[] states,Vector2 at,Vector2 size,Action callback)
        {
            var root=Rect(name,parent,at,size);root.gameObject.AddComponent<Image>();
            var button=root.gameObject.AddComponent<Button>();Skin(button,states);button.onClick.AddListener(()=>callback());return button;
        }
        public static void PixelCanvas(Canvas canvas,CanvasScaler scaler)
        {
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.pixelPerfect=true;
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;scaler.scaleFactor=1;scaler.referencePixelsPerUnit=100;
        }
    }
    /// <summary>A tooltip owns no input and disappears when its source is disabled.</summary>
    public sealed class NativeUiTooltip : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        public string Message="";
        private RectTransform popup;
        private float showAt;
        private bool hovering;
        public void OnPointerEnter(PointerEventData e){hovering=true;showAt=Time.unscaledTime+.35f;}
        public void OnPointerExit(PointerEventData e){Hide();}
        private void Update()
        {
            if(!hovering||Time.unscaledTime<showAt||String.IsNullOrEmpty(Message))return;
            if(popup==null)
            {
                var canvas=GetComponentInParent<Canvas>().rootCanvas;var parent=canvas.GetComponent<RectTransform>();
                popup=NativeUiPrimitives.Rect("Tooltip",parent,Vector2.zero,new Vector2(260,70));
                var image=popup.gameObject.AddComponent<Image>();image.color=new Color(.04f,.04f,.035f,.96f);image.raycastTarget=false;
                var text=NativeUiPrimitives.Text("Tooltip text",popup,Message,12,new Vector2(8,6),new Vector2(244,58));
                var nested=popup.gameObject.AddComponent<Canvas>();nested.overrideSorting=true;nested.sortingOrder=300;
            }
            var label=popup.GetComponentInChildren<Text>();label.text=Message;
            float height=Mathf.Clamp(Mathf.Ceil(label.preferredHeight)+12,30,Mathf.Max(30,Screen.height));
            popup.sizeDelta=new Vector2(260,height);label.rectTransform.sizeDelta=new Vector2(244,height-12);
            popup.anchoredPosition=new Vector2(Mathf.Clamp(Input.mousePosition.x+12,0,Mathf.Max(0,Screen.width-260)),
                -Mathf.Clamp(Screen.height-Input.mousePosition.y+16,0,Mathf.Max(0,Screen.height-height)));
        }
        private void Hide(){hovering=false;if(popup!=null){if(Application.isPlaying)Destroy(popup.gameObject);else DestroyImmediate(popup.gameObject);popup=null;}}
        private void OnDisable(){Hide();}
        private void OnDestroy(){Hide();}
    }
}
