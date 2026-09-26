using System;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacySmodAttributeTests
    {
        [Test]
        public void InvalidNormalIsDerivedOnlyFromItsOwnFaces()
        {
            var mesh = LegacySmodParser.Parse(Fixture(normalNaN:true)).Meshes[0];
            Assert.AreEqual(1,mesh.ReconstructedNormals);
            Assert.AreEqual(Vector3.forward,mesh.Vertices[0].Normal);
            Assert.AreEqual(Vector3.up,mesh.Vertices[1].Normal,"Valid authored normal must remain unchanged.");
            Assert.AreEqual(1,mesh.Faces.Count);
            Assert.AreEqual(4,mesh.Vertices.Count);
        }
        [Test]
        public void UnusedUvIsNeutralizedButReferencedInvalidUvStillFails()
        {
            var mesh=LegacySmodParser.Parse(Fixture(unusedUvNaN:true)).Meshes[0];
            Assert.AreEqual(1,mesh.UnreferencedUvDefaults);
            Assert.AreEqual(Vector2.zero,mesh.Vertices[3].UV);
            Assert.Throws<InvalidDataException>(()=>LegacySmodParser.Parse(Fixture(usedUvNaN:true)));
        }
        [Test]
        public void GeometryAndUnparsedDataRemainStrictWhileExistingZeroPaddingIsPreserved()
        {
            Assert.Throws<InvalidDataException>(()=>LegacySmodParser.Parse(Fixture(positionNaN:true)));
            // The shared legacy reader explicitly permits zero alignment padding.
            // Test unparsed data, rather than accidentally rejecting that existing contract.
            Assert.Throws<InvalidDataException>(()=>LegacySmodParser.Parse(Fixture().Concat(new byte[]{1}).ToArray()));
            Assert.DoesNotThrow(()=>LegacySmodParser.Parse(Fixture().Concat(new byte[]{0}).ToArray()));
        }
        [Test]
        public void CanonicalMap0AllReferencedStaticMeshesAreFiniteAfterBoundedRepairs()
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();
            if(corpus==null) Assert.Ignore("Requires canonical ps0032 DATA.");
            var wld=LegacyWldTerrainParser.Parse(corpus.Resolve("DATA_Español/world/0.wld"));
            var groups=new[]{wld.Buildings,wld.Shapes,wld.Trees,wld.Grass};
            string[] categories={"building","shape","tree","grass"};
            int files=0, rebuilt=0, inactive=0, unusedUv=0;
            for(int g=0;g<groups.Length;g++)
            foreach(int id in groups[g].Coordinates.Select(x=>x.Id).Distinct())
            {
                string path="DATA_Español/entity/"+categories[g]+"/"+groups[g].Names[id];
                var parsed=LegacySmodParser.Parse(corpus.Resolve(path));
                files++;
                foreach(var mesh in parsed.Meshes)
                {
                    rebuilt+=mesh.ReconstructedNormals; inactive+=mesh.InactiveNormalDefaults; unusedUv+=mesh.UnreferencedUvDefaults;
                    foreach(var v in mesh.Vertices)
                    {
                        Assert.IsTrue(Finite(v.Position.x)&&Finite(v.Position.y)&&Finite(v.Position.z),path);
                        Assert.IsTrue(Finite(v.Normal.x)&&Finite(v.Normal.y)&&Finite(v.Normal.z),path);
                        Assert.IsTrue(Finite(v.UV.x)&&Finite(v.UV.y),path);
                    }
                }
            }
            Assert.AreEqual(197,files);
            Assert.AreEqual(33,rebuilt+inactive,"Observed invalid normal vertices in canonical Map0.");
            Assert.AreEqual(4,unusedUv,"All four corrupt UV slots are unreferenced by triangles.");
        }
        private static bool Finite(float x){return !float.IsNaN(x)&&!float.IsInfinity(x);}
        private static byte[] Fixture(bool normalNaN=false,bool unusedUvNaN=false,bool usedUvNaN=false,bool positionNaN=false)
        {
            using(var stream=new MemoryStream())
            using(var writer=new BinaryWriter(stream))
            {
                for(int i=0;i<10;i++)writer.Write(0f);
                writer.Write(1);writer.Write(1);writer.Write((byte)'t');writer.Write(4);
                Vector3[] p={Vector3.zero,Vector3.right,Vector3.up,Vector3.one};
                for(int i=0;i<4;i++)
                {
                    writer.Write(i==0&&positionNaN?float.NaN:p[i].x);writer.Write(p[i].y);writer.Write(p[i].z);
                    writer.Write(i==0&&normalNaN?float.NaN:0f);writer.Write(1f);writer.Write(0f);writer.Write(-1);
                    writer.Write((i==0&&usedUvNaN)||(i==3&&unusedUvNaN)?float.NaN:0f);writer.Write(0f);
                }
                writer.Write(1);writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)2);
                for(int i=0;i<6;i++)writer.Write(0f);
                writer.Write(0);return stream.ToArray();
            }
        }
    }
}
