using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.LocalData;
using Dreynox.Mmorpg.World;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyPackedSkinTests
    {
        [Test]
        public void FortyByteVerticesPreserveUnusedPackedIndices()
        {
            var mesh = Legacy3dcParser.Parse(Fixture(false, 0.25f, 0, 0, 255));
            var v = mesh.Vertices[0];
            Assert.AreEqual(255, v.Unknown); Assert.AreEqual(2, v.Bone3);
            Assert.AreEqual(0.75f, v.Weight2); Assert.AreEqual(0, v.Weight3); Assert.AreEqual(0, v.Weight4);
            Assert.DoesNotThrow(() => LegacyRuntimeSkinnedBuilder.ValidateMeshForSkeleton(mesh, 2));
        }
        [Test]
        public void Ep6RetainsTheComplementaryFourthInfluenceInUnity()
        {
            var source = Legacy3dcParser.Parse(Fixture(true, 0.1f, 0.2f, 0.3f, 3));
            Assert.AreEqual(0.4f, source.Vertices[0].Weight4, 0.000001f);
            Assert.Throws<InvalidDataException>(() => LegacyRuntimeSkinnedBuilder.ValidateMeshForSkeleton(source, 3));
            var root = new GameObject("four-weight-fixture"); Mesh mesh = null;
            try
            {
                var bones = new Transform[4];
                for (int i=0; i<4; i++) { bones[i]=new GameObject("b"+i).transform; bones[i].SetParent(root.transform,false); }
                mesh = LegacyRuntimeSkinnedBuilder.BuildMesh(source,bones,root.transform,"four weights");
                BoneWeight weight = mesh.boneWeights[0];
                Assert.AreEqual(3,weight.boneIndex0); Assert.AreEqual(0.4f,weight.weight0,0.000001f);
                Assert.AreEqual(1f,weight.weight0+weight.weight1+weight.weight2+weight.weight3,0.000001f);
            }
            finally { if(mesh!=null)Object.DestroyImmediate(mesh);Object.DestroyImmediate(root); }
        }
        [Test]
        public void ImpossibleWeightsAndWeightedOutOfRangeIndicesStillFail()
        {
            Assert.Throws<InvalidDataException>(()=>Legacy3dcParser.Parse(Fixture(true,0.8f,0.3f,0,3)));
            Assert.Throws<InvalidDataException>(()=>Legacy3dcParser.Parse(Fixture(true,0.1f,0.2f,0.3f,255)));
            Assert.Throws<InvalidDataException>(()=>Legacy3dcParser.Parse(Fixture(false,1.1f,0,0,0)));
        }
        [TestCase("mob_boar_01.3dc")]
        [TestCase("mob_boar_02.3dc")]
        public void CanonicalBoarDoesNotTreatPackedMatrixIndicesAsASignature(string name)
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();
            if(corpus==null)Assert.Ignore("Requires original ps0032 DATA.");
            var mesh=Legacy3dcParser.Parse(corpus.Resolve("DATA_Español/monster/3dc/"+name));
            Assert.AreEqual(30,mesh.InverseBindMatrices.Count);
            Assert.AreEqual(845,mesh.Vertices.Count);Assert.AreEqual(958,mesh.Faces.Count);
            var v=mesh.Vertices[12];
            Assert.AreEqual(14,v.Bone1);Assert.AreEqual(4,v.Bone2);Assert.AreEqual(5,v.Bone3);Assert.AreEqual(9,v.Unknown);
            Assert.AreEqual(0.083333328f,v.Weight1,0.0000001f);Assert.AreEqual(0,v.Weight4);
        }
        [Test]
        public void CanonicalEp6DragonIncludesFourthBoneRatherThanRenormalizingThree()
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();
            if(corpus==null)Assert.Ignore("Requires original ps0032 DATA.");
            var mesh=Legacy3dcParser.Parse(corpus.Resolve("DATA_Español/monster/3dc/china_8yx_mob_long.3DC"));
            Assert.AreEqual(444,mesh.Version);
            var v=mesh.Vertices[160];Assert.AreEqual(17,v.Bone4);
            Assert.AreEqual(0.561500065f,v.Weight4,0.000001f);
            Assert.DoesNotThrow(()=>LegacyRuntimeSkinnedBuilder.ValidateMeshForSkeleton(mesh,56));
        }
        [Test]
        public void EveryMap1MonsterAndNpcRigIsValidatedBeforeExpensiveWorldImport()
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();
            if(corpus==null)Assert.Ignore("Requires original ps0032 DATA.");
            var map=LegacySvmapParser.Parse(corpus.Resolve("DATA_Español/world/1.svmap"));
            var db=LegacyDbMonsterDataParser.ParseData(corpus.Resolve("DATA_Español/binarysdata/DBMonsterData.SData"));
            var monsters=LegacyMonParser.Parse(corpus.Resolve("DATA_Español/monster/monster.mon"));
            var ids=new SortedSet<int>();
            foreach(var spawn in map.MonsterAreas.SelectMany(a=>a.Monsters).Where(s=>s.Count>0))
            { Assert.IsTrue(db.TryGet(spawn.MobId,out var row));ids.Add(checked((int)row.Image)); }
            var errors=new List<string>();
            Assert.AreEqual(30,ids.Count);
            foreach(int id in ids)Check(corpus,LegacyMonCatalogKind.Monster,id,monsters,errors);
            TestContext.WriteLine("Map1 monster models: "+ids.Count);
            var definitions=LegacyNpcQuestHeaderParser.ParseEncrypted(corpus.Resolve("DATA_Español/npc/NpcQuest.SData"));
            var npcs=LegacyMonParser.Parse(corpus.Resolve("DATA_Español/npc/npc.mon"));
            ids.Clear(); int unresolved=0;
            foreach(var npc in map.Npcs)
            {
                if(definitions.TryGet(npc.NpcType,npc.NpcId,out var row)) ids.Add(row.Model);
                else
                {
                    // Already documented by the world importer: four Guard 8/169
                    // positions have no original NpcQuest definition. Never invent a model.
                    Assert.IsTrue(npc.NpcType==8&&npc.NpcId==169,"Unexpected unresolved NPC definition.");
                    unresolved+=npc.Positions.Count;
                }
            }
            Assert.AreEqual(4,unresolved,"Known source-unresolved guard positions changed.");
            Assert.AreEqual(54,ids.Count);
            foreach(int id in ids)Check(corpus,LegacyMonCatalogKind.Npc,id,npcs,errors);
            TestContext.WriteLine("Map1 NPC models: "+ids.Count+"; source-unresolved guard positions="+unresolved);
            Assert.IsEmpty(errors,string.Join("\n",errors));
        }
        private static void Check(CanonicalClientCorpus corpus,LegacyMonCatalogKind kind,int id,LegacyMonFile catalog,List<string> errors)
        {
            try { LegacyMonPrefabImporter.ValidateRigOnly(corpus,kind,catalog.Records[id]); }
            catch(Exception ex) { errors.Add(kind+"/"+id+" "+catalog.Records[id].Name+": "+ex); }
        }
        private static byte[] Fixture(bool ep6,float a,float b,float c,byte fourth)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(ep6?444:0);writer.Write(4);
                for(int bone=0;bone<4;bone++)for(int j=0;j<16;j++)writer.Write(j%5==0?1f:0f);
                writer.Write(3);
                for(int i=0;i<3;i++)
                {
                    writer.Write((float)(i%2));writer.Write((float)(i/2));writer.Write(0f);
                    writer.Write(a);if(ep6){writer.Write(b);writer.Write(c);}
                    writer.Write((byte)0);writer.Write((byte)1);writer.Write((byte)2);writer.Write(fourth);
                    writer.Write(0f);writer.Write(0f);writer.Write(1f);writer.Write(0f);writer.Write(0f);
                }
                writer.Write(1);writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)2);
                return stream.ToArray();
            }
        }
    }
}
