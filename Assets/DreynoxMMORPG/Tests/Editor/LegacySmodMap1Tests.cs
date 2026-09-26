using System;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacySmodMap1Tests
    {
        [Test]
        public void UntexturedUvDoesNotChangeTopologyOrPermitTexturedCorruption()
        {
            var untextured=LegacySmodParser.Parse(Fixture(false));
            var mesh=untextured.Meshes[0];
            Assert.AreEqual(string.Empty,mesh.TextureName);
            Assert.AreEqual(1,mesh.UntexturedUvDefaults);
            Assert.AreEqual(0,mesh.UnreferencedUvDefaults);
            Assert.AreEqual(3,mesh.Vertices.Count);
            Assert.AreEqual(1,mesh.Faces.Count);
            Assert.AreEqual(Vector2.zero,mesh.Vertices[0].UV);
            Assert.Throws<InvalidDataException>(()=>LegacySmodParser.Parse(Fixture(true)));
        }
        [Test]
        public void CanonicalCronwellUntypedSubmeshDoesNotBlockMap1()
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();
            if(corpus==null) Assert.Ignore("Requires canonical ps0032 DATA.");
            var file=LegacySmodParser.Parse(corpus.Resolve("DATA_Español/entity/building/l_h2_cronwell01.smod"));
            Assert.AreEqual(11,file.Meshes.Count);
            var mesh=file.Meshes[4];
            Assert.AreEqual(string.Empty,mesh.TextureName);
            Assert.AreEqual(27,mesh.Vertices.Count);
            Assert.AreEqual(13,mesh.Faces.Count);
            Assert.AreEqual(4,mesh.UntexturedUvDefaults);
            Assert.Greater(file.CollisionMeshes.Count,0);
            Assert.AreEqual(0,file.Meshes.Where(m=>!string.IsNullOrWhiteSpace(m.TextureName)).Sum(m=>m.UntexturedUvDefaults));
        }
        [Test]
        public void CanonicalMap1EveryPlacedStaticResourceParsesBeforeBuilding()
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();
            if(corpus==null) Assert.Ignore("Requires canonical ps0032 DATA.");
            var wld=LegacyWldTerrainParser.Parse(corpus.Resolve("DATA_Español/world/1.wld"));
            var groups=new[]{wld.Buildings,wld.Shapes,wld.Trees,wld.Grass};
            string[] categories={"building","shape","tree","grass"};
            int files=0,unusedMaterialUv=0;
            for(int group=0;group<groups.Length;group++)
            foreach(int id in groups[group].Coordinates.Select(x=>x.Id).Distinct())
            {
                string path="DATA_Español/entity/"+categories[group]+"/"+groups[group].Names[id];
                var parsed=LegacySmodParser.Parse(corpus.Resolve(path));
                files++;
                foreach(var mesh in parsed.Meshes)
                {
                    unusedMaterialUv+=mesh.UntexturedUvDefaults;
                    foreach(var v in mesh.Vertices)
                    {
                        Assert.IsTrue(Finite(v.Position.x)&&Finite(v.Position.y)&&Finite(v.Position.z),path);
                        Assert.IsTrue(Finite(v.Normal.x)&&Finite(v.Normal.y)&&Finite(v.Normal.z),path);
                        Assert.IsTrue(Finite(v.UV.x)&&Finite(v.UV.y),path);
                    }
                }
            }
            Assert.Greater(files,0);
            Assert.AreEqual(4,unusedMaterialUv);
            TestContext.WriteLine("Map1 static resource preflight: "+files+" files; "+unusedMaterialUv+" untextured UV slots.");
        }
        private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        private static byte[] Fixture(bool textured)
        {
            using(var stream=new MemoryStream())
            using(var w=new BinaryWriter(stream))
            {
                for(int i=0;i<10;i++)w.Write(0f);
                w.Write(1);w.Write(textured?1:0);if(textured)w.Write((byte)'t');w.Write(3);
                Vector3[] points={Vector3.zero,Vector3.right,Vector3.up};
                for(int i=0;i<3;i++)
                {
                    w.Write(points[i].x);w.Write(points[i].y);w.Write(points[i].z);
                    w.Write(0f);w.Write(0f);w.Write(1f);w.Write(-1);
                    w.Write(i==0?float.NaN:0f);w.Write(0f);
                }
                w.Write(1);w.Write((ushort)0);w.Write((ushort)1);w.Write((ushort)2);
                for(int i=0;i<6;i++)w.Write(0f);
                w.Write(0);return stream.ToArray();
            }
        }
    }
}
