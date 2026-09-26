using System;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.Importing;
using Dreynox.Mmorpg.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class NativeRadarTests
    {
        [Test]
        public void OriginalMonsterIconRetainsColoredRoundCenterAndTransparentCorners()
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();if(corpus==null)Assert.Ignore("Requires original ps0032 DATA.");
            NativeRadarSkin skin=NativeRadarSkinImporter.Import(corpus);
            Sprite monster=skin.Get(NativeRadarKind.Monster);
            Assert.AreEqual(new Vector2(16,16),monster.rect.size);
            Assert.AreEqual(0,monster.texture.GetPixel(0,0).a);
            Assert.AreEqual(0,monster.texture.GetPixel(15,15).a);
            Color center=monster.texture.GetPixel(8,8);
            Assert.Greater(center.a,.9f);Assert.Greater(center.r,center.g);Assert.Greater(center.g,center.b);
            Assert.AreNotSame(monster,skin.Get(NativeRadarKind.Npc));
            Assert.AreNotSame(skin.Get(NativeRadarKind.QuestAvailable),skin.Get(NativeRadarKind.QuestReady));
            Assert.AreEqual(new Vector2(8,12),skin.Get(NativeRadarKind.Npc).rect.size);
        }
        [Test]
        public void RadarUsesActualSpriteWithoutTintingAndClipsAtViewport()
        {
            var frame=new Texture2D(256,256);var iconTexture=new Texture2D(16,16);
            var sprite=Sprite.Create(iconTexture,new Rect(0,0,16,16),Vector2.one*.5f);
            var skin=ScriptableObject.CreateInstance<NativeRadarSkin>();
            var go=new GameObject("Radar fixture",typeof(RectTransform));
            try
            {
                skin.Configure(frame,sprite,sprite,Enum.GetValues(typeof(NativeRadarKind)).Cast<NativeRadarKind>()
                    .Select(k=>new NativeRadarIcon{kind=k,sprite=sprite,source="fixture"}).ToArray());
                var view=go.AddComponent<NativeRadarView>();view.Build(go.GetComponent<RectTransform>(),skin,frame);
                view.BeginFrame(new Vector3(1024,0,1024),0,new Vector2(2048,2048));
                Assert.IsTrue(view.Add(new Vector3(1024,0,1024),NativeRadarKind.Monster));view.EndFrame();
                Assert.AreSame(sprite,view.Markers[0].sprite);
                Assert.AreEqual(Color.white,view.Markers[0].color);
                Assert.AreEqual(new Vector2(16,16),view.Markers[0].rectTransform.sizeDelta);
                Assert.IsNotNull(view.Viewport.GetComponent<RectMask2D>());
                Assert.Less(Vector2.Distance(new Vector2(95,95),view.Markers[0].rectTransform.anchoredPosition),0.0001f);
                Assert.IsFalse(view.Add(Vector3.zero,NativeRadarKind.Monster));
                view.BeginFrame(new Vector3(1024,0,1024),0,new Vector2(2048,2048));view.EndFrame();
                Assert.IsFalse(view.Markers[0].gameObject.activeSelf,"Pooled old markers disappear.");
                float previous=view.ViewFraction;view.Zoom(1);Assert.Less(view.ViewFraction,previous);
                for(int i=0;i<50;i++)view.Zoom(-1);Assert.AreEqual(1,view.ViewFraction);
                Assert.IsFalse(view.PlayerMarker.gameObject.activeSelf,"No stale marker coordinates after zoom.");
            }
            finally{Object.DestroyImmediate(go);Object.DestroyImmediate(skin);Object.DestroyImmediate(sprite);Object.DestroyImmediate(iconTexture);Object.DestroyImmediate(frame);}
        }
        [Test]
        public void MapProjectionAndCropShareAnExplicitCoordinateConvention()
        {
            var size=new Vector2(2048,2048);var view=new Vector2(190,190);
            Rect window=NativeRadarView.Window(Vector3.zero,size,.22f);Assert.AreEqual(Vector2.zero,window.position);
            Assert.IsTrue(NativeRadarView.Project(Vector3.zero,size,window,view,out Vector2 p));Assert.AreEqual(Vector2.zero,p);
            Assert.IsFalse(NativeRadarView.Project(new Vector3(float.NaN,0,0),size,window,view,out _));
            var image=new Texture2D(256,256);
            try
            {
                Rect rect=NativeRadarView.TopLeftUv(image,new Rect(0,0,202,226));
                Assert.AreEqual(30f/256,rect.y);Assert.AreEqual(202f/256,rect.width);
            }
            finally{Object.DestroyImmediate(image);}
        }
        [Test]
        public void QuestStateSelectsDistinctSourceIconsInsteadOfRecoloringSquare()
        {
            Assert.AreEqual(NativeRadarKind.QuestAvailable,NativeWorldHud.ResolveNpcRadar(null," !"));
            Assert.AreEqual(NativeRadarKind.QuestReady,NativeWorldHud.ResolveNpcRadar(null," ?"));
            Assert.AreEqual(NativeRadarKind.Npc,NativeWorldHud.ResolveNpcRadar(null,""));
            var missing=ScriptableObject.CreateInstance<NativeRadarSkin>();
            try{Assert.Throws<InvalidOperationException>(()=>missing.Get(NativeRadarKind.Monster));}
            finally{Object.DestroyImmediate(missing);}
        }
    }
}
