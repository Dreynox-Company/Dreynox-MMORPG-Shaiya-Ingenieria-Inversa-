using System;
using Dreynox.Mmorpg.Interaction;
using Dreynox.Mmorpg.UI.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Dreynox.Mmorpg.UI
{
    /// <summary>Integer-pixel drag, bounded to the current viewport, no input leaks.</summary>
    public sealed class NativeWindowDrag : MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        [Serializable] public sealed class Placement {public int width,height,x,y;}
        [Serializable] public sealed class History {public int schema=1;public Placement[] places=Array.Empty<Placement>();}
        private RectTransform window,parent;
        private Vector2 offset,start;
        private bool dragging;
        private NativeUiLocalStore store;
        private string key;
        private History history=new History();
        private int lastWidth,lastHeight;
        private bool corrupt;
        public Action<string> Error;
        public void Configure(RectTransform target,string id,NativeUiLocalStore preferences)
        {
            window=target;parent=window.parent as RectTransform;store=preferences;key=id+".json";
            if(window.pivot!=new Vector2(0,1)||window.anchorMin!=new Vector2(0,1)||window.anchorMax!=new Vector2(0,1))
                throw new InvalidOperationException("Pixel window requires a top-left origin.");
            if(store!=null)
            {
                try
                {
                    string text=store.Read(key);
                    if(text!=null){var parsed=JsonUtility.FromJson<History>(text);Validate(parsed);history=parsed;}
                }
                catch(Exception ex){corrupt=true;Error?.Invoke("Posiciones no cargadas; el archivo se conservará: "+ex.Message);}
            }
            RestoreViewport();
        }
        private static void Validate(History value)
        {
            if(value==null||value.schema!=1||value.places==null||value.places.Length>16)throw new InvalidOperationException("Invalid UI position history.");
            var seen=new System.Collections.Generic.HashSet<long>();
            foreach(var p in value.places)
                if(p==null||p.width<1||p.height<1||p.width>32768||p.height>32768||p.x<0||p.y<0||p.x>p.width||p.y>p.height||
                    !seen.Add(((long)p.width<<32)|(uint)p.height))throw new InvalidOperationException("Invalid UI position.");
        }
        public void Clamp()
        {
            if(window==null||parent==null)return;
            int width=Mathf.Max(1,Mathf.RoundToInt(parent.rect.width)),height=Mathf.Max(1,Mathf.RoundToInt(parent.rect.height));
            window.anchoredPosition=new Vector2(NativeHudGeometry.ClampPixel(window.anchoredPosition.x,width,Mathf.Max(1,Mathf.CeilToInt(window.rect.width))),
                -NativeHudGeometry.ClampPixel(-window.anchoredPosition.y,height,Mathf.Max(1,Mathf.CeilToInt(window.rect.height))));
        }
        private void RestoreViewport()
        {
            if(parent==null)return;lastWidth=Mathf.Max(1,Mathf.RoundToInt(parent.rect.width));lastHeight=Mathf.Max(1,Mathf.RoundToInt(parent.rect.height));
            foreach(var p in history.places)if(p.width==lastWidth&&p.height==lastHeight){window.anchoredPosition=new Vector2(p.x,-p.y);break;}
            Clamp();
        }
        private void Update()
        {
            if(window==null||parent==null)return;
            if(dragging&&Input.GetKeyDown(KeyCode.Escape)){window.anchoredPosition=start;Release();}
            if(lastWidth!=Mathf.RoundToInt(parent.rect.width)||lastHeight!=Mathf.RoundToInt(parent.rect.height))
            {if(dragging)Release();RestoreViewport();}
        }
        public void OnBeginDrag(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left||window==null||!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,e.position,e.pressEventCamera,out Vector2 at))return;
            start=window.anchoredPosition;offset=start-(at-new Vector2(parent.rect.xMin,parent.rect.yMax));dragging=true;WorldInputGate.Set(this,true);
        }
        public void OnDrag(PointerEventData e)
        {
            if(!dragging)return;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,e.position,e.pressEventCamera,out Vector2 at))
            {window.anchoredPosition=at-new Vector2(parent.rect.xMin,parent.rect.yMax)+offset;Clamp();}
        }
        public void OnEndDrag(PointerEventData e)
        {
            if(!dragging)return;Release();Clamp();if(store==null||corrupt)return;
            var p=new Placement{width=lastWidth,height=lastHeight,x=Mathf.RoundToInt(window.anchoredPosition.x),y=Mathf.RoundToInt(-window.anchoredPosition.y)};
            var list=new System.Collections.Generic.List<Placement>();
            foreach(var old in history.places)if(old.width!=p.width||old.height!=p.height)list.Add(old);
            list.Add(p);if(list.Count>16)list.RemoveAt(0);var next=new History{places=list.ToArray()};
            try{Validate(next);store.Write(key,JsonUtility.ToJson(next));history=next;}
            catch(Exception ex){window.anchoredPosition=start;Clamp();Error?.Invoke("No se guardó la posición: "+ex.Message);}
        }
        private void Release(){dragging=false;WorldInputGate.Set(this,false);}
        private void Cancel(){if(dragging&&window!=null){window.anchoredPosition=start;Clamp();}Release();}
        private void OnDisable(){Cancel();}
        private void OnDestroy(){Cancel();}
    }
}
