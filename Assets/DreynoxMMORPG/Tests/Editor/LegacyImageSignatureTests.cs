using System;
using System.IO;
using System.Security.Cryptography;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyImageSignatureTests
    {
        [Test]
        public void SignatureWinsOverLegacyFilenameAndSourceStaysUnchanged()
        {
            string path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".dds");
            byte[] bytes=Bitmap();
            try
            {
                File.WriteAllBytes(path,bytes);
                Assert.IsTrue(LegacyImageSignature.ImportedFileName(path).EndsWith(".bmp"));
                CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
            }
            finally { File.Delete(path); }
        }
        [Test]
        public void RealDdsKeepsItsEncodingAndMalformedHeadersAreNotGuessed()
        {
            var header=new byte[128];header[0]=(byte)'D';header[1]=(byte)'D';header[2]=(byte)'S';header[3]=32;header[4]=124;
            Assert.AreEqual(".dds",LegacyImageSignature.DetectExtension(header));
            Assert.Throws<InvalidDataException>(()=>LegacyImageSignature.DetectExtension(new byte[]{66,77}));
            Assert.Throws<InvalidDataException>(()=>LegacyImageSignature.DetectExtension(new byte[128]));
        }
        [Test]
        public void CopiedBitmapBytesImportAsAnActualUnityTexture()
        {
            string path="Assets/__DreynoxImageTest_"+Guid.NewGuid().ToString("N")+".bmp";
            try
            {
                File.WriteAllBytes(path,Bitmap());
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(texture);
                Assert.AreEqual(1,texture.width);Assert.AreEqual(1,texture.height);
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }
        [Test]
        public void AllFiveCanonicalFortressLightmapsAreBitmapsAndImportWithCorrectDimensions()
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();
            if(corpus==null) Assert.Ignore("Requires canonical ps0032 DATA.");
            for(int i=0;i<5;i++)
            {
                string source=corpus.Resolve("DATA_Español/world/dungeon/l_r1_fortress00/l_r1_fortress00_l"+i+".dds");
                byte[] original=File.ReadAllBytes(source);
                Assert.AreEqual(196662,original.Length);
                Assert.AreEqual("l_r1_fortress00_l"+i+".bmp",LegacyImageSignature.ImportedFileName(source));
                string target="Assets/__DreynoxLightmapTest_"+Guid.NewGuid().ToString("N")+".bmp";
                try
                {
                    File.WriteAllBytes(target,original);
                    AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
                    var importer=AssetImporter.GetAtPath(target) as TextureImporter;
                    Assert.IsNotNull(importer);
                    importer.textureType=TextureImporterType.Lightmap;
                    importer.sRGBTexture=false;importer.SaveAndReimport();
                    var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(target);
                    Assert.IsNotNull(texture);Assert.AreEqual(256,texture.width);Assert.AreEqual(256,texture.height);
                    CollectionAssert.AreEqual(original,File.ReadAllBytes(source));
                }
                finally { AssetDatabase.DeleteAsset(target); }
            }
        }
        private static byte[] Bitmap()
        {
            using(var stream=new MemoryStream())
            using(var writer=new BinaryWriter(stream))
            {
                writer.Write((byte)'B');writer.Write((byte)'M');writer.Write(58);writer.Write(0);writer.Write(54);
                writer.Write(40);writer.Write(1);writer.Write(1);writer.Write((ushort)1);writer.Write((ushort)24);
                writer.Write(0);writer.Write(4);writer.Write(0);writer.Write(0);writer.Write(0);writer.Write(0);
                writer.Write(new byte[]{0,128,255,0});return stream.ToArray();
            }
        }
    }
}
