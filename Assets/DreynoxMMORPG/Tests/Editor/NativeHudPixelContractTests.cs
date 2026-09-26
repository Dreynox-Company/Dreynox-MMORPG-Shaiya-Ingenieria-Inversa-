using System;
using System.Collections.Generic;
using Dreynox.Mmorpg.Interaction;
using Dreynox.Mmorpg.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class NativeHudPixelContractTests
    {
        private GameObject root;
        private RectTransform parent;
        private NativeQuickbarView view;
        private readonly List<Object> resources=new List<Object>();
        private Sprite[] states;
        [SetUp] public void Setup()
        {
            root=new GameObject("Native UI fixture",typeof(RectTransform),typeof(Canvas));
            root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            parent=root.GetComponent<RectTransform>();parent.sizeDelta=new Vector2(1024,768);
            var skin=ScriptableObject.CreateInstance<NativeHudSkin>();resources.Add(skin);
            skin.horizontalBar=Texture(512,64);skin.verticalBar=Texture(64,512);
            var art=Texture(128,128);states=new Sprite[4];
            for(int i=0;i<4;i++){states[i]=Sprite.Create(art,new Rect(32*i,0,32,32),Vector2.one*.5f);resources.Add(states[i]);}
            skin.attackIcon=skin.health=skin.mana=skin.stamina=skin.levelFrame=skin.scrollTop=skin.scrollMiddle=skin.scrollBottom=states[0];
            skin.previous=skin.next=skin.previousVertical=skin.nextVertical=skin.expand=skin.collapse=skin.rotate=skin.command=skin.scrollUp=skin.scrollDown=states;
            view=root.AddComponent<NativeQuickbarView>();view.Build(parent,skin,null,null,null,false);
        }
        private Texture2D Texture(int width,int height){var value=new Texture2D(width,height);resources.Add(value);return value;}
        private RectTransform Bar(int id)=>parent.Find("Native quickbar "+id).GetComponent<RectTransform>();
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(root);
            foreach(var resource in resources)Object.DestroyImmediate(resource);resources.Clear();
        }
        [Test] public void TenNativeCellsPreserveOriginalHorizontalAndVerticalCoordinates()
        {
            foreach(bool vertical in new[]{false,true})
            {
                if(vertical)view.Core.Rotate(0);view.Refresh();var bar=Bar(0);
                Assert.AreEqual(vertical?new Vector2(53,446):new Vector2(446,53),bar.rect.size);
                for(int i=0;i<10;i++)
                {
                    var cell=bar.Find("Access "+(i+1)).GetComponent<RectTransform>();
                    Assert.AreEqual(new Vector2(32,32),cell.rect.size);
                    Assert.AreEqual(vertical?new Vector2(7,-24-40*i):new Vector2(24+40*i,-7),cell.anchoredPosition);
                }
            }
        }
        [Test] public void ActualPageAndOrientationButtonsMutateTheirSharedCore()
        {
            var bar=Bar(0);var next=bar.Find("Next page").GetComponent<Button>();
            Assert.IsFalse(bar.Find("Previous page").GetComponent<Button>().interactable);
            for(int i=0;i<4;i++)next.onClick.Invoke();
            Assert.AreEqual(4,view.Core.Page(0));Assert.IsFalse(next.interactable);
            Assert.AreEqual("5",bar.Find("Page").GetComponent<Text>().text);
            bar.Find("Rotate bar").GetComponent<Button>().onClick.Invoke();Assert.IsTrue(view.Core.Vertical(0));
            bar.Find("Additional bar").GetComponent<Button>().onClick.Invoke();Assert.IsTrue(Bar(1).gameObject.activeSelf);
            bar.Find("Additional bar").GetComponent<Button>().onClick.Invoke();Assert.IsFalse(Bar(1).gameObject.activeSelf);
        }
        [Test] public void SourceCropDoesNotStretchThePowerOfTwoTextureAcrossTheWidget()
        {
            var frame=Bar(0).Find("Original slot frame").GetComponent<RawImage>();
            Assert.AreEqual(new Rect(0,1-53f/64,446f/512,53f/64),frame.uvRect);
            Assert.AreEqual(new Vector2(446,53),frame.rectTransform.rect.size);
        }
        [Test] public void EmptyNativeCellsHaveNoInventedAbilityAndDragCancellationReleasesInput()
        {
            var cells=Bar(0).GetComponentsInChildren<NativeQuickbarSlot>();Assert.AreEqual(10,cells.Length);
            Assert.IsTrue(cells[0].GetComponent<Button>().interactable);
            for(int i=1;i<10;i++)Assert.IsFalse(cells[i].GetComponent<Button>().interactable);
            view.BeginDrag(cells[0]);Assert.IsTrue(WorldInputGate.IsBlocked);
            view.CancelDrag();Assert.IsFalse(WorldInputGate.IsBlocked);Assert.IsNull(parent.Find("Dragged action"));
        }
        [Test] public void SpriteStatesAreBoundWithoutSelectableKeyboardFocusLeakingToGameplay()
        {
            var button=Bar(0).Find("Next page").GetComponent<Button>();
            Assert.AreEqual(Selectable.Transition.SpriteSwap,button.transition);
            Assert.AreSame(states[1],button.spriteState.highlightedSprite);Assert.AreSame(states[2],button.spriteState.pressedSprite);
            Assert.AreSame(states[3],button.spriteState.disabledSprite);Assert.AreEqual(Navigation.Mode.None,button.navigation.mode);
        }
        [Test] public void CanvasPolicyUsesPixelUnitsRatherThanReferenceResolutionMagnification()
        {
            var scaler=root.AddComponent<CanvasScaler>();var canvas=root.GetComponent<Canvas>();
            NativeUiPrimitives.PixelCanvas(canvas,scaler);
            Assert.IsTrue(canvas.pixelPerfect);Assert.AreEqual(CanvasScaler.ScaleMode.ConstantPixelSize,scaler.uiScaleMode);
            Assert.AreEqual(1,scaler.scaleFactor);Assert.IsNotNull(NativeUiPrimitives.Font);
        }
        [Test] public void ClampingKeepsRotatedBarInsideAnOddSizedViewport()
        {
            parent.sizeDelta=new Vector2(1001,731);var bar=Bar(0);
            view.Core.Rotate(0);view.Refresh();bar.anchoredPosition=new Vector2(9999,-9999);
            bar.GetComponent<NativeWindowDrag>().Clamp();
            Assert.AreEqual(new Vector2(948,-285),bar.anchoredPosition);
        }
    }
}
