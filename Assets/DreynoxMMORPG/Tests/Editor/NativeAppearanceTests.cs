using System;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.LocalData;
using Dreynox.Mmorpg.ParityCore;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class NativeAppearanceTests
    {
        [Test]
        public void NativeHumanDefaultIsNotTheHardcodedCostume()
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();if(corpus==null)Assert.Ignore("Requires original ps0032 DATA.");
            var paths=LegacyDefaultAppearanceCore.Resolve(0,0,0,0,0,p=>File.ReadAllBytes(corpus.Resolve(p)));
            StringAssert.EndsWith("humf_torso001.3DC",paths.UpperMesh);
            StringAssert.EndsWith("humf_torso001.dds",paths.UpperTexture);
            StringAssert.EndsWith("humf_boots001.3DC",paths.FootMesh);
            StringAssert.EndsWith("hum_face001.dds",paths.FaceTexture);
            Assert.AreEqual(-1,paths.SetId);
        }
        [Test]
        public void All16NativeRigsResolveTheirOwnDefaultTablesAndVisibleFaceHairRows()
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();if(corpus==null)Assert.Ignore("Requires original ps0032 DATA.");
            int cases=0;
            var cache=new System.Collections.Generic.Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
            byte[] Read(string path)
            {
                if(!cache.TryGetValue(path,out byte[] value)){value=File.ReadAllBytes(corpus.Resolve(path));cache.Add(path,value);}
                return value;
            }
            for(int id=0;id<16;id++)
            {
                var rig=LegacyCharacterRigCore.ResolveNativeRigIndex(id);
                var ani=LegacyAniParser.Parse(corpus.Resolve("DATA_Español/character/"+rig.FamilyFolder+"/ani6/"+rig.Prefix+"_019_select.ani"));
                var checkedMeshes=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for(int face=0;face<5;face++)for(int hair=0;hair<5;hair++)
                {
                    var p=LegacyDefaultAppearanceCore.Resolve(rig.Family,rig.Job,rig.Sex,face,hair,Read);
                    string[] meshes={p.UpperMesh,p.LowerMesh,p.HandMesh,p.FootMesh,p.FaceMesh,p.HairMesh};
                    string[] textures={p.UpperTexture,p.LowerTexture,p.HandTexture,p.FootTexture,p.FaceTexture,p.HairTexture};
                    for(int i=0;i<6;i++)
                    {
                        Assert.IsTrue(File.Exists(corpus.Resolve(textures[i])),textures[i]);
                        if(checkedMeshes.Add(meshes[i]))
                        {
                            var mesh=Legacy3dcParser.Parse(corpus.Resolve(meshes[i]));
                            LegacyRuntimeSkinnedBuilder.ValidateMeshForSkeleton(mesh,ani.Bones.Count);
                        }
                    }
                    cases++;
                }
            }
            Assert.AreEqual(400,cases);Assert.AreEqual(96,cache.Count);
            // The extra misplaced viwm_upper.MLT in /elf is never used as a substitute.
        }
        [TestCase(0f,0f,0f,1f)]
        [TestCase(0.25f,0.75f,0.25f,0.25f)]
        [TestCase(1.2f,-0.5f,1.2f,1.5f)]
        public void TextureCoordinatesChangeOriginOnceWithoutClampingTiles(float u,float v,float expectedU,float expectedV)
        {
            Assert.AreEqual(new Vector2(expectedU,expectedV),LegacyTextureCoordinates.ToUnity(new Vector2(u,v)));
        }
        [Test]
        public void SharedSkinnedBuilderConvertsUvButDoesNotMutateOriginalData()
        {
            var root=new GameObject("UV fixture");Mesh output=null;
            try
            {
                var source=new Legacy3dcFile();source.InverseBindMatrices.Add(Matrix4x4.identity);
                source.Vertices.Add(new Legacy3dcVertex {Position=Vector3.zero,Normal=Vector3.up,UV=new Vector2(.25f,.25f),Weight1=1f});
                source.Vertices.Add(new Legacy3dcVertex {Position=Vector3.right,Normal=Vector3.up,UV=new Vector2(.75f,.25f),Weight1=1f});
                source.Vertices.Add(new Legacy3dcVertex {Position=Vector3.forward,Normal=Vector3.up,UV=new Vector2(.25f,.75f),Weight1=1f});
                source.Faces.Add(new LegacyTriangle {A=0,B=1,C=2});
                output=LegacyRuntimeSkinnedBuilder.BuildMesh(source,new[]{root.transform},root.transform,"UV test");
                Assert.AreEqual(new Vector2(.25f,.75f),output.uv[0]);
                Assert.AreEqual(new Vector2(.25f,.25f),source.Vertices[0].UV);
                CollectionAssert.AreEqual(new[]{0,2,1},output.triangles,"Existing basis/winding conversion stays intact.");
            }
            finally{if(output!=null)Object.DestroyImmediate(output);Object.DestroyImmediate(root);}
        }
        [Test]
        public void TopLeftAuthoredUvSamplesTheSameTexelAfterBottomFirstTextureUpload()
        {
            // Four deliberately different source texels, ordered as D3D rows (top first).
            Color[] native={Color.red,Color.green,Color.blue,Color.yellow};
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            try
            {
                texture.SetPixels(new[]{native[2],native[3],native[0],native[1]});texture.Apply();
                for(int row=0;row<2;row++)for(int column=0;column<2;column++)
                {
                    Vector2 uv=LegacyTextureCoordinates.ToUnity(new Vector2((column+.5f)/2,(row+.5f)/2));
                    Color sample=texture.GetPixel((int)(uv.x*2),(int)(uv.y*2));
                    Assert.AreEqual(native[row*2+column],sample);
                }
                Assert.AreNotEqual(native[0],texture.GetPixel(0,0),"Old unchanged UV samples the wrong vertical half.");
            }
            finally{Object.DestroyImmediate(texture);}
        }
        [Test]
        public void InvalidUvDoesNotEnterUnityMesh()
        {Assert.Throws<ArgumentOutOfRangeException>(()=>LegacyTextureCoordinates.ToUnity(new Vector2(float.NaN,0)));}
    }
}
