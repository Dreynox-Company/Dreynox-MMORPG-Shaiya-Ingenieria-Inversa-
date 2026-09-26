using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dreynox.Mmorpg.UI
{
    /// <summary>Actual instantiated UI measurements. Not a native screenshot comparison.</summary>
    public static class NativeHudContractEvidence
    {
        [Serializable] public sealed class Box
        {
            public string name;
            public Vector2 anchoredPosition,size;
            public string sourceTexture;
            public Rect uv;
        }
        [Serializable] public sealed class Snapshot
        {
            public string scope="instantiated-unity-ui-contracts-not-pixel-equivalence-to-native-client";
            public int width,height,visibleBars,logicalSlots;
            public bool pixelPerfect,arialAvailable,playerResourcesBound;
            public float canvasScale;
            public string canvasMode;
            public List<Box> rectangles=new List<Box>();
        }
        public static Snapshot Capture(NativeWorldHud hud)
        {
            if(hud==null||!hud.Ready||hud.Quickbar==null||!hud.Quickbar.Ready)
                throw new InvalidOperationException("The actual native-art HUD is not ready.");
            var root=hud.CanvasRoot;var canvas=root.GetComponent<Canvas>();var scaler=root.GetComponent<CanvasScaler>();
            if(!canvas.pixelPerfect||scaler.uiScaleMode!=CanvasScaler.ScaleMode.ConstantPixelSize||Mathf.Abs(canvas.scaleFactor-1)>.001f)
                throw new InvalidOperationException("HUD must retain original pixel scale rather than screen-relative stretching.");
            var value=new Snapshot{width=Screen.width,height=Screen.height,pixelPerfect=canvas.pixelPerfect,
                canvasMode=scaler.uiScaleMode.ToString(),canvasScale=canvas.scaleFactor,arialAvailable=NativeUiPrimitives.UsesNativeFontFamily,
                playerResourcesBound=hud.HasBoundPlayerResources,visibleBars=hud.Quickbar.Core.VisibleBars,logicalSlots=50};
            var status=root.Find("Native player status").GetComponent<RectTransform>();
            RequireSize(status,new Vector2(218,64));Add(value,status);
            for(int i=0;i<value.visibleBars;i++)
            {
                var bar=root.Find("Native quickbar "+i).GetComponent<RectTransform>();
                bool vertical=hud.Quickbar.Core.Vertical(i);var box=Core.NativeHudGeometry.Bar(vertical);
                RequireSize(bar,new Vector2(box.Width,box.Height));Add(value,bar);
                Add(value,bar.Find("Original slot frame").GetComponent<RectTransform>());
                for(int j=0;j<10;j++)
                {
                    var rect=bar.Find("Access "+(j+1)).GetComponent<RectTransform>();var cell=Core.NativeHudGeometry.Cell(j,vertical);
                    RequireSize(rect,new Vector2(32,32));
                    if(rect.anchoredPosition!=new Vector2(cell.X,-cell.Y))throw new InvalidOperationException("Native quickbar icon stride/origin differs from binary evidence.");
                    Add(value,rect);
                }
            }
            var quest=root.Find("NPC and quest dialog") as RectTransform;
            if(quest!=null&&quest.gameObject.activeInHierarchy)Add(value,quest);
            return value;
        }
        private static void RequireSize(RectTransform rect,Vector2 size)
        {
            if(rect.rect.size!=size)throw new InvalidOperationException("Incorrect authored widget size: "+rect.name);
        }
        private static void Add(Snapshot result,RectTransform rect)
        {
            var image=rect.GetComponent<RawImage>();
            result.rectangles.Add(new Box{name=rect.name,anchoredPosition=rect.anchoredPosition,size=rect.rect.size,
                sourceTexture=image!=null&&image.texture!=null?image.texture.name:"",uv=image!=null?image.uvRect:default});
        }
    }
}
