using System;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.Editor.NativeContent;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class NativeAtlasEncodingTests
    {
        [Test] public void RleAndRawTgaAreRecognizedAndMalformedPacketsFailBeforeImport()
        {
            var raw=new byte[18+12];raw[2]=2;raw[12]=2;raw[14]=2;raw[16]=24;
            Assert.AreEqual(".tga",LegacyImageSignature.DetectExtension(raw));LegacyImageSignature.ValidateTgaPayload(raw);
            Assert.Throws<EndOfStreamException>(()=>LegacyImageSignature.ValidateTgaPayload(raw.Take(raw.Length-1).ToArray()));
            var rle=new byte[22];Array.Copy(raw,rle,18);rle[2]=10;rle[18]=131;rle[19]=10;rle[20]=20;rle[21]=30;
            Assert.AreEqual(".tga",LegacyImageSignature.DetectExtension(rle));LegacyImageSignature.ValidateTgaPayload(rle);
            rle[18]=132;Assert.Throws<InvalidDataException>(()=>LegacyImageSignature.ValidateTgaPayload(rle));
            rle[18]=131;Assert.Throws<EndOfStreamException>(()=>LegacyImageSignature.ValidateTgaPayload(rle.Take(21).ToArray()));
            rle[1]=1;Assert.Throws<InvalidDataException>(()=>LegacyImageSignature.DetectExtension(rle));
            Assert.Throws<InvalidDataException>(()=>LegacyImageSignature.DetectExtension(new byte[128]));
        }
        [TestCase("11")] [TestCase("17")] [TestCase("18")] [TestCase("20")] [TestCase("21")]
        [TestCase("32")] [TestCase("33")] [TestCase("35")] [TestCase("36")]
        public void OriginalDdsNamedTgaImportsUnchangedWithCorrectDimensions(string name)
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();if(corpus==null)Assert.Ignore("Requires original DATA.");
            string source=corpus.Resolve("DATA_Español/interface/icon/"+name+".dds");byte[] bytes=File.ReadAllBytes(source);
            Assert.AreEqual(".tga",LegacyImageSignature.DetectExtension(bytes));LegacyImageSignature.ValidateTgaPayload(bytes);
            string target="Assets/__DreynoxNativeTga_"+Guid.NewGuid().ToString("N")+".tga";
            try
            {
                File.WriteAllBytes(target,bytes);AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(target);
                Assert.IsNotNull(texture);Assert.AreEqual(128,texture.width);Assert.AreEqual(512,texture.height);
                CollectionAssert.AreEqual(bytes,File.ReadAllBytes(source));
            }
            finally {AssetDatabase.DeleteAsset(target);}
        }
        [Test] public void WholeCanonicalCatalogConversionActuallyImportsBeforeExpensiveWorldPreparation()
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();if(corpus==null)Assert.Ignore("Requires original DATA.");
            var assets=NativeCatalogImporter.Import(corpus);
            Assert.AreEqual(28142,assets[0].Count);Assert.AreEqual(12060,assets[1].Count);
            assets[0].Validate();assets[1].Validate();
            Assert.IsTrue(assets[0].TryGet(11,1,out var item));Assert.IsTrue(assets[0].TryIcon(item,out var texture,out _));
            Assert.AreEqual(128,texture.width);Assert.AreEqual(512,texture.height);
            for(int i=0;i<assets[1].Count;i++)Assert.IsTrue(assets[1].TryIcon(assets[1].Entry(i),out _,out _));
        }
    }
}
