using System;
using System.Collections.Generic;
using System.IO;
using Dreynox.Mmorpg.LocalData;
using Dreynox.Mmorpg.ParityCore;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LocalDataPipelineTests
    {
        [Test]
        public void UnweightedTailMatricesDoNotRequireExtraAnimationBones()
        {
            var mesh = MeshFixture(72, 35);
            Assert.DoesNotThrow(() => LegacyRuntimeSkinnedBuilder.ValidateMeshForSkeleton(mesh, 36));
        }
        [Test]
        public void WeightedTailBoneIsRejectedRatherThanClampedOrSilentlyRemapped()
        {
            var mesh = MeshFixture(72, 40);
            Assert.Throws<InvalidDataException>(() => LegacyRuntimeSkinnedBuilder.ValidateMeshForSkeleton(mesh, 36));
        }
        [Test]
        public void BuilderUsesAniHierarchyAndPreservesEachPiecesAuthoredBindPose()
        {
            GameObject root = new GameObject("Fixture"); Mesh built = null;
            try
            {
                var reference = MeshFixture(40, 0);
                var animation = new LegacyAniFile();
                for (int i = 0; i < 36; i++) animation.Bones.Add(new LegacyAniBone { ParentBoneIndex = i - 1, AbsoluteBaseMatrix = Matrix4x4.identity });
                Transform[] bones = LegacyRuntimeSkinnedBuilder.BuildSkeleton(root.transform, reference, animation);
                Assert.AreEqual(36, bones.Length);
                var piece = MeshFixture(72, 0);
                piece.InverseBindMatrices[0] = Matrix4x4.Translate(new Vector3(1, 2, 3));
                built = LegacyRuntimeSkinnedBuilder.BuildMesh(piece, bones, root.transform, "piece");
                Assert.AreEqual(36, built.bindposes.Length);
                Assert.AreEqual(1f, built.bindposes[0].m03);
                Assert.AreEqual(2f, built.bindposes[0].m13);
                Assert.AreEqual(-3f, built.bindposes[0].m23);
            }
            finally { if (built != null) Object.DestroyImmediate(built); Object.DestroyImmediate(root); }
        }
        [Test]
        public void AniSamplerUsesStartKeyAndRetainsReflectionAndMissingChannels()
        {
            var points = new List<LegacyAniTranslationFrame>
            {
                new LegacyAniTranslationFrame { Frame = 10, Translation = new Vector3(0,0,2) },
                new LegacyAniTranslationFrame { Frame = 20, Translation = new Vector3(10,0,4) }
            };
            Assert.AreEqual(new Vector3(5,0,-3), LocalAniPlayer.PositionAt(points,15,Vector3.zero));
            Assert.AreEqual(new Vector3(0,0,-2), LocalAniPlayer.PositionAt(points,0,Vector3.zero));
            Assert.AreEqual(new Vector3(10,0,-4), LocalAniPlayer.PositionAt(points,100,Vector3.zero));
            Assert.AreEqual(Vector3.one, LocalAniPlayer.PositionAt(new List<LegacyAniTranslationFrame>(),0,Vector3.one));
        }
        [Test]
        public void Dxt3AlphaIsDecodedWithoutNativeGpuFormatSupport()
        {
            byte[] d = new byte[144];
            Write(d,0,0x20534444); Write(d,4,124); Write(d,12,4); Write(d,16,4); Write(d,76,32); Write(d,80,4); Write(d,84,0x33545844);
            for (int i=128;i<136;i++) d[i]=0x88;
            d[137]=0xf8;
            DecodedDds decoded=LegacyDdsDecoder.Decode(d);
            Assert.AreEqual(4,decoded.Width); Assert.AreEqual(255,decoded.Pixels[0]); Assert.AreEqual(136,decoded.Pixels[3]);
        }
        [Test]
        public void CanonicalAll16RigsAnd224MeshesUseValidAnimatedBoneIndicesWhenCorpusIsConfigured()
        {
            string path=Environment.GetEnvironmentVariable("DREYNOX_CORPUS_ROOT");
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) Assert.Ignore("Full local corpus required for 224-mesh gate.");
            var source=new LocalDataFolder(path);
            int meshes=0, differingTables=0;
            for (int rig=0;rig<16;rig++)
            {
                var identity=LegacyCharacterRigCore.ResolveNativeRigIndex(rig);
                var first=LegacyCharacterAssetCore.ResolvePreview(identity.Family,identity.Job,identity.Sex);
                var ani=LegacyAniParser.Parse(source.Read(first.SelectAnimation,default));
                var paths=new List<string>{first.UpperMesh,first.LowerMesh,first.HandMesh,first.FootMesh};
                for(int v=0;v<5;v++)
                {
                    var p=LegacyCharacterAssetCore.ResolvePreview(identity.Family,identity.Job,identity.Sex,v,v);
                    paths.Add(p.FaceMesh);paths.Add(p.HairMesh);
                }
                foreach(string meshPath in paths)
                {
                    var mesh=Legacy3dcParser.Parse(source.Read(meshPath,default));
                    LegacyRuntimeSkinnedBuilder.ValidateMeshForSkeleton(mesh,ani.Bones.Count);
                    if(mesh.InverseBindMatrices.Count!=ani.Bones.Count)differingTables++;
                    meshes++;
                }
            }
            Assert.AreEqual(224,meshes);Assert.AreEqual(9,differingTables);
        }
        private static Legacy3dcFile MeshFixture(int tableSize,int weightedIndex)
        {
            var result=new Legacy3dcFile();
            for(int i=0;i<tableSize;i++)result.InverseBindMatrices.Add(Matrix4x4.identity);
            for(int i=0;i<3;i++)result.Vertices.Add(new Legacy3dcVertex{Position=new Vector3(i%2,i/2,0), Normal=Vector3.forward, Weight1=1, Bone1=(byte)weightedIndex});
            result.Faces.Add(new LegacyTriangle{A=0,B=1,C=2});return result;
        }
        private static void Write(byte[] d,int o,uint value) { Array.Copy(BitConverter.GetBytes(value),0,d,o,4); }
    }
}
