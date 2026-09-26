using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Dreynox.Mmorpg.Interaction;
using Dreynox.Mmorpg.NativeContent;
using Dreynox.Mmorpg.Quests;
using Dreynox.Mmorpg.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class NativeCatalogIntegrationTests
    {
        private GameObject root;
        private NativeContentPanel panel;
        private QuestJournalCore journal;
        private NativeCatalogAsset items,skills;
        private readonly List<Object> assets=new List<Object>();
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static void Set(object value,string name,object field)=>value.GetType().GetField(name,Private).SetValue(value,field);
        private static T Get<T>(object value,string name)=>(T)value.GetType().GetField(name,Private).GetValue(value);
        private Texture2D Texture(int width,int height)
        {var value=new Texture2D(width,height,TextureFormat.RGBA32,false);assets.Add(value);return value;}
        private NativeCatalogAsset Fixture(bool isSkills)
        {
            string id=isSkills?"id":"itemtype",variant=isSkills?"skilllevel":"itemtypeid",name=isSkills?"name":"itemname";
            string[] fields={id,variant,"image","level","icon","unclassified"};
            int first=isSkills?804:1,second=isSkills?805:45;
            byte[] numeric;
            using(var stream=new MemoryStream())
            {
                using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
                {
                    writer.Write(new byte[128]);writer.Write(fields.Length);
                    foreach(string field in fields){writer.Write((byte)field.Length);writer.Write(Encoding.Unicode.GetBytes(field));}
                    writer.Write(2);
                    foreach(var row in new[]{new long[]{first,1,0,1,1,long.MinValue},new long[]{second,2,1001,9,101,long.MaxValue}})
                        foreach(long value in row)writer.Write(value);
                }
                numeric=stream.ToArray();
            }
            byte[] text;
            using(var stream=new MemoryStream())
            {
                using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
                {
                    writer.Write(new byte[128]);writer.Write(4);
                    foreach(string field in new[]{id,variant,name,"text"}){writer.Write((byte)field.Length);writer.Write(Encoding.Unicode.GetBytes(field));}
                    writer.Write(2);
                    writer.Write((long)first);writer.Write(1L);
                    foreach(string value in new[]{isSkills?"Técnica de prueba":"Espada de prueba","{c5}Descripción española{/c}\\nSin conceder objetos."})
                    {byte[] bytes=NativeWindows1252.Encode(value);writer.Write(bytes.Length);writer.Write(bytes);}
                    writer.Write((long)second);writer.Write(2L);
                    foreach(string value in new[]{"Árbol de prueba",""})
                    {byte[] bytes=NativeWindows1252.Encode(value);writer.Write(bytes.Length);writer.Write(bytes);}
                }
                text=stream.ToArray();
            }
            var source=new NativeDefinitionCatalog(NativeDataTable.ReadNumeric(numeric),NativeDataTable.ReadText(text),isSkills);
            var rows=new[]{new NativeCatalogEntry(source.Entries[0],fields,0,new NativeIconRegion("fixture",0,0),""),
                new NativeCatalogEntry(source.Entries[1],fields,-1,default,"Fixture intentionally has no second icon.")};
            var result=ScriptableObject.CreateInstance<NativeCatalogAsset>();assets.Add(result);
            result.Configure(isSkills,fields,rows,new[]{Texture(32,32)},new string('a',64),new string('b',64));return result;
        }
        [SetUp] public void Setup()
        {
            root=new GameObject("Content integration fixture",typeof(RectTransform),typeof(Canvas));
            root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            var canvas=root.GetComponent<RectTransform>();canvas.sizeDelta=new Vector2(1024,768);
            var hud=root.AddComponent<NativeWorldHud>();Set(hud,"canvasRoot",canvas);
            typeof(NativeWorldHud).GetProperty("Ready").SetValue(hud,true);
            var skin=ScriptableObject.CreateInstance<NativeHudSkin>();assets.Add(skin);
            var atlas=Texture(128,32);var states=new Sprite[4];
            for(int i=0;i<4;i++){states[i]=Sprite.Create(atlas,new Rect(32*i,0,32,32),Vector2.one*.5f);assets.Add(states[i]);}
            skin.command=skin.scrollUp=skin.scrollDown=states;
            skin.scrollTop=skin.scrollMiddle=skin.scrollBottom=states[0];hud.SetPresentationSkin(skin);
            var runtime=root.AddComponent<QuestJournalRuntime>();
            journal=new QuestJournalCore(new LegacyQuestCatalogData{quests=Array.Empty<LegacyQuestDefinition>()});
            typeof(QuestJournalRuntime).GetProperty("Journal").SetValue(runtime,journal);
            items=Fixture(false);skills=Fixture(true);
            runtime.SetItemCatalog(items);panel=root.AddComponent<NativeContentPanel>();
            panel.Configure(hud,runtime,items,skills,Texture(512,1024),Texture(512,1024));panel.Initialize();
        }
        [TearDown] public void Cleanup()
        {
            if(panel!=null)WorldInputGate.Set(panel,false);
            if(root!=null)Object.DestroyImmediate(root);
            foreach(var item in assets)if(item!=null)Object.DestroyImmediate(item);assets.Clear();
        }
        [Test] public void ConvertedCatalogKeepsSpanishAndEverySignedNumericField()
        {
            Assert.IsTrue(items.TryItem(257,out var entry));Assert.AreEqual("Espada de prueba",entry.Name);
            Assert.AreEqual(long.MinValue,items.Value(entry,"unclassified"));
            Assert.AreEqual("Descripción española\nSin conceder objetos.",entry.Description);
            Assert.AreEqual(6,items.FieldCount);Assert.AreEqual(2,items.Count);
            Assert.IsTrue(items.TryGet(45,2,out entry));Assert.AreEqual(long.MaxValue,items.Value(entry,"unclassified"));
            Assert.Throws<ArgumentException>(()=>skills.Value(entry,"level"));
        }
        [Test] public void MissingOriginalIconDoesNotBorrowAnUnrelatedTexture()
        {
            Assert.IsTrue(items.TryGet(45,2,out var entry));
            Assert.IsFalse(items.TryIcon(entry,out _,out _));Assert.IsNotEmpty(entry.IconIssue);
            Assert.IsTrue(items.TryItem(257,out entry));Assert.IsTrue(items.TryIcon(entry,out var image,out var pixels));
            Assert.AreEqual(new Rect(0,0,32,32),pixels);Assert.AreEqual(32,image.width);
        }
        [Test] public void EmptyInventoryDoesNotContainTheDeveloperCatalog()
        {
            Assert.IsTrue(panel.OpenInventory());Assert.AreEqual(0,panel.VisibleInventoryTypes);
            Assert.IsFalse(panel.InspectInventoryCell(0));Assert.AreEqual(0,journal.Inventory.Count);
            Assert.IsTrue(panel.OpenCatalog(true));Assert.AreEqual(2,panel.CatalogMatches);
            Assert.IsTrue(panel.InspectCatalogRow(0));Assert.AreEqual(804*256+1,panel.InspectedKey);
            Assert.AreEqual(0,journal.Inventory.Count);Assert.AreEqual(0,journal.Gold);Assert.AreEqual(0,journal.Experience);
        }
        [Test] public void RealJournalOwnershipFeedsTheViewWithoutCreatingAnotherInventory()
        {
            Assert.IsTrue(journal.ApplyInventoryTransaction(new Dictionary<int,int>{{257,7},{45*256+2,3}},out _));
            Assert.IsTrue(panel.OpenInventory());Assert.AreEqual(2,panel.VisibleInventoryTypes);
            Assert.IsTrue(panel.InspectInventoryCell(0));Assert.AreEqual(257,panel.InspectedKey);
            Assert.IsTrue(journal.ApplyInventoryTransaction(new Dictionary<int,int>{{257,-7}},out _));
            panel.RefreshInventory();Assert.AreEqual(1,panel.VisibleInventoryTypes);
            Assert.IsTrue(panel.InspectInventoryCell(0));Assert.AreEqual(45*256+2,panel.InspectedKey);
            Assert.AreEqual(3,journal.Inventory[45*256+2]);
        }
        [Test] public void CatalogSearchUsesOriginalNamesAndCannotGrantDefinitions()
        {
            Assert.IsTrue(panel.OpenCatalog(false));Assert.IsTrue(panel.SetCatalogQuery("arbol"));
            Assert.AreEqual(1,panel.CatalogMatches);Assert.IsTrue(panel.InspectCatalogRow(0));
            Assert.AreEqual(45*256+2,panel.InspectedKey);Assert.AreEqual(0,journal.Inventory.Count);
            Assert.IsFalse(panel.SelectCatalogPage(1));
            Assert.IsTrue(panel.SetCatalogQuery("inexistente"));Assert.AreEqual(0,panel.CatalogMatches);
            Assert.IsFalse(panel.InspectCatalogRow(0));Assert.IsFalse(panel.SetCatalogQuery(new string('x',129)));
        }
        [Test] public void OriginalWindowsAndInventoryCellsKeepPixelDimensions()
        {
            panel.OpenInventory();var window=Get<RectTransform>(panel,"inventoryWindow");
            Assert.AreEqual(new Vector2(284,564),window.rect.size);
            var frame=window.Find("Original artwork").GetComponent<RawImage>();
            Assert.AreEqual(new Rect(0,1-564f/1024,284f/512,564f/1024),frame.uvRect);
            for(int i=0;i<24;i++)
            {
                var slot=window.Find("Owned item "+i).GetComponent<RectTransform>();
                Assert.AreEqual(new Vector2(32,32),slot.rect.size);
                Assert.AreEqual(new Vector2(NativeInventoryProjection.X(i),-NativeInventoryProjection.Y(i)),slot.anchoredPosition);
            }
            panel.OpenCatalog(true);Assert.AreEqual(new Vector2(500,626),Get<RectTransform>(panel,"catalogWindow").rect.size);
        }
        [Test] public void CloseAndDisableReleaseOnlyThisInputOwner()
        {
            panel.OpenInventory();Assert.IsTrue(WorldInputGate.IsBlocked);
            panel.Close();Assert.IsFalse(WorldInputGate.IsBlocked);
            panel.OpenCatalog(false);Assert.IsTrue(WorldInputGate.IsBlocked);
            panel.enabled=false;typeof(NativeContentPanel).GetMethod("OnDisable",Private).Invoke(panel,null);
            Assert.IsFalse(WorldInputGate.IsBlocked);Assert.IsFalse(panel.IsCatalogOpen);
        }
        [TestCase(0,"icon_skill.tga",0,0)]
        [TestCase(1,"icon_skill.tga",0,0)]
        [TestCase(17,"icon_skill.tga",0,32)]
        [TestCase(1000,"icon_skill2.dds",0,0)]
        [TestCase(1001,"icon_skill2.dds",0,0)]
        [TestCase(2011,"icon_skill3.dds",320,0)]
        public void NativeSkillImageUsesRecoveredBankAndOneBasedCell(int value,string file,int x,int y)
        {
            Assert.IsTrue(NativeSkillIcon.TryResolve(value,out var icon));
            Assert.AreEqual(file,icon.File);Assert.AreEqual(x,icon.X);Assert.AreEqual(y,icon.Y);
        }
        [TestCase(-1)] [TestCase(257)] [TestCase(10000)] [TestCase(65536)]
        public void InvalidSkillImageDoesNotWrapToAnUnrelatedAtlas(int value)
        {Assert.IsFalse(NativeSkillIcon.TryResolve(value,out _));}
    }
}
