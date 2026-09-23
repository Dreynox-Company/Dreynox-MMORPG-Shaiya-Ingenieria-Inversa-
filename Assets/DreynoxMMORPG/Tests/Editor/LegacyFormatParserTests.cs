using System;
using System.IO;
using System.Text;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyFormatParserTests
    {
        [Test]
        public void ThreeDcEp6ParsesBonesVerticesAndFaces()
        {
            byte[] bytes = Build3dc(
                444,
                2,
                new[]
                {
                    new VertexFixture
                    {
                        Position = new Vector3(1f, 2f, 3f),
                        W1 = 0.6f,
                        W2 = 0.4f,
                        W3 = 0f,
                        B1 = 0,
                        B2 = 1,
                        B3 = 0,
                        Normal = Vector3.up,
                        UV = new Vector2(0.25f, 0.75f)
                    }
                },
                new[] { new ushort[] { 0, 0, 0 } });

            Legacy3dcFile parsed =
                Legacy3dcParser.Parse(bytes);

            Assert.AreEqual(444, parsed.Version);
            Assert.IsTrue(parsed.IsEp6);
            Assert.AreEqual(2, parsed.InverseBindMatrices.Count);
            Assert.AreEqual(1, parsed.Vertices.Count);
            Assert.AreEqual(1, parsed.Faces.Count);

            Legacy3dcVertex vertex = parsed.Vertices[0];
            Assert.AreEqual(new Vector3(1f, 2f, 3f), vertex.Position);
            Assert.AreEqual(0.6f, vertex.Weight1, 0.000001f);
            Assert.AreEqual(0.4f, vertex.Weight2, 0.000001f);
            Assert.AreEqual(0, vertex.Bone1);
            Assert.AreEqual(1, vertex.Bone2);
            Assert.AreEqual(new Vector2(0.25f, 0.75f), vertex.UV);
        }

        [Test]
        public void ThreeDcEp5DerivesComplementarySecondWeight()
        {
            byte[] bytes = Build3dc(
                0,
                2,
                new[]
                {
                    new VertexFixture
                    {
                        Position = Vector3.zero,
                        W1 = 0.8f,
                        B1 = 0,
                        B2 = 1,
                        Normal = Vector3.forward,
                        UV = Vector2.zero
                    }
                },
                Array.Empty<ushort[]>());

            Legacy3dcFile parsed =
                Legacy3dcParser.Parse(bytes);

            Assert.IsFalse(parsed.IsEp6);
            Assert.AreEqual(0.8f, parsed.Vertices[0].Weight1, 0.000001f);
            Assert.AreEqual(0.2f, parsed.Vertices[0].Weight2, 0.000001f);
        }

        [Test]
        public void AniV2ParsesHierarchyAndThirtyFpsKeyframes()
        {
            byte[] bytes = BuildAniV2();
            LegacyAniFile parsed =
                LegacyAniParser.Parse(bytes);

            Assert.IsTrue(parsed.IsV2);
            Assert.AreEqual(0u, parsed.StartKeyframe);
            Assert.AreEqual(30u, parsed.EndKeyframe);
            Assert.AreEqual(1f, parsed.DurationSeconds, 0.000001f);
            Assert.AreEqual(2, parsed.Bones.Count);
            Assert.AreEqual(-1, parsed.Bones[0].ParentBoneIndex);
            Assert.AreEqual(0, parsed.Bones[1].ParentBoneIndex);
            Assert.AreEqual(2, parsed.Bones[1].Rotations.Count);
            Assert.AreEqual(1, parsed.Bones[1].Translations.Count);
            Assert.AreEqual(30u, parsed.Bones[1].Rotations[1].Frame);
        }

        [Test]
        public void AniLegacyWithoutSignatureResetsToOffsetZero()
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write((uint)0);
                writer.Write((uint)15);
                writer.Write((ushort)1);
                writer.Write(-1);
                WriteIdentityMatrix(writer);
                writer.Write(0);
                writer.Write(0);

                LegacyAniFile parsed =
                    LegacyAniParser.Parse(stream.ToArray());

                Assert.IsFalse(parsed.IsV2);
                Assert.AreEqual(15u, parsed.EndKeyframe);
                Assert.AreEqual(1, parsed.Bones.Count);
            }
        }

        [Test]
        public void ThreeDcSkeletonlessCloakLayoutParsesWithoutBoneTable()
        {
            byte[] bytes = BuildSkeletonless3dc();
            Legacy3dcFile parsed = Legacy3dcParser.Parse(bytes);

            Assert.AreEqual(Legacy3dcLayout.Skeletonless, parsed.Layout);
            Assert.IsFalse(parsed.HasEmbeddedSkeleton);
            Assert.AreEqual(1, parsed.Vertices.Count);
            Assert.AreEqual(1, parsed.Faces.Count);
        }

        [Test]
        public void ThreeDcTexturePrefixedLayoutPreservesTextureName()
        {
            byte[] bytes = BuildTexturePrefixed3dc();
            Legacy3dcFile parsed = Legacy3dcParser.Parse(bytes);

            Assert.AreEqual(Legacy3dcLayout.TexturePrefixed, parsed.Layout);
            Assert.AreEqual("Mob_Rend_01.TGA", parsed.EmbeddedTextureName);
            Assert.AreEqual(1, parsed.InverseBindMatrices.Count);
            Assert.AreEqual(1, parsed.Vertices.Count);
        }

        [Test]
        public void ThreeDcConcatenatedSectionsRequireParseMany()
        {
            byte[] first = Build3dc(
                444,
                1,
                new[]
                {
                    new VertexFixture
                    {
                        Position = Vector3.zero,
                        W1 = 1f,
                        B1 = 0,
                        Normal = Vector3.up,
                        UV = Vector2.zero
                    }
                },
                Array.Empty<ushort[]>());

            byte[] second = Build3dc(
                0,
                1,
                new[]
                {
                    new VertexFixture
                    {
                        Position = Vector3.one,
                        W1 = 1f,
                        B1 = 0,
                        Normal = Vector3.up,
                        UV = Vector2.one
                    }
                },
                Array.Empty<ushort[]>());

            byte[] combined = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, combined, 0, first.Length);
            Buffer.BlockCopy(second, 0, combined, first.Length, second.Length);

            Assert.Throws<InvalidDataException>(
                () => Legacy3dcParser.Parse(combined));

            var parsed = Legacy3dcParser.ParseMany(combined);
            Assert.AreEqual(2, parsed.Count);
            Assert.AreEqual(444, parsed[0].Version);
            Assert.AreEqual(0, parsed[1].Version);
        }

        [Test]
        public void CanonicalPs0032SamplesMatchObservedStructureWhenCorpusIsConfigured()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null || !corpus.Validate().IsCanonical)
                Assert.Ignore("Canonical ps0032 corpus is not configured on this machine.");

            Assert3dc(
                corpus,
                "DATA_Español/character/human/3dc/co_humm_upper003.3dc",
                444,
                56,
                856,
                1048);

            Assert3dc(
                corpus,
                "DATA_Español/character/human/3dc/co_humm_hand003.3dc",
                0,
                56,
                270,
                264);

            AssertAni(
                corpus,
                "DATA_Español/character/human/ani6/humm_000_normal.ani",
                false,
                0,
                60,
                56);

            AssertAni(
                corpus,
                "DATA_Español/character/human/ani6/humm_001_walk.ani",
                false,
                0,
                28,
                56);
        }

        [Test]
        public void WldFldTerrainHeaderParsesHeightAndTextureData()
        {
            ushort[] rawHeights =
            {
                10000, 10001, 10002,
                10003, 10004, 10005,
                10006, 10007, 10008
            };

            byte[] bytes =
                BuildWldTerrain(
                    4,
                    rawHeights,
                    new byte[9],
                    "TestDetail.dds",
                    8f,
                    "Step.wav",
                    ".wtr");

            LegacyWldTerrainFile parsed =
                LegacyWldTerrainParser.Parse(bytes);

            Assert.AreEqual("FLD\0", parsed.Signature);
            Assert.AreEqual(4, parsed.MapSize);
            Assert.AreEqual(3, parsed.Resolution);
            Assert.AreEqual(9, parsed.RawHeights.Length);
            Assert.AreEqual(1, parsed.Textures.Count);
            Assert.AreEqual("TestDetail.dds", parsed.Textures[0].TextureName);
            Assert.AreEqual(8f, parsed.Textures[0].TileSize, 0.000001f);
            Assert.AreEqual("Step.wav", parsed.Textures[0].WalkSound);
            Assert.AreEqual(".wtr", parsed.InnerLayout);
            Assert.AreEqual(
                LegacyTerrainHeightCore.Decode(10004),
                parsed.WorldHeightAtSample(1, 1),
                0.000001);
        }

        [Test]
        public void CanonicalMapZeroWldAndSvmapMatchVerifiedCorpusWhenConfigured()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null || !corpus.Validate().IsCanonical)
                Assert.Ignore(
                    "Canonical ps0032 corpus is not configured on this machine.");

            LegacyWldTerrainFile wld =
                LegacyWldTerrainParser.Parse(
                    corpus.Resolve("DATA_Español/world/0.wld"));

            Assert.AreEqual(2048, wld.MapSize);
            Assert.AreEqual(1025, wld.Resolution);
            Assert.AreEqual(7, wld.Textures.Count);
            Assert.AreEqual(
                "A1_Grass_field.dds",
                wld.Textures[0].TextureName);
            Assert.AreEqual(8f, wld.Textures[0].TileSize, 0.000001f);

            int x =
                LegacyTerrainHeightCore.WorldToSampleIndex(
                    184.4588165283203,
                    wld.MapSize);

            int z =
                LegacyTerrainHeightCore.WorldToSampleIndex(
                    1826.923583984375,
                    wld.MapSize);

            Assert.AreEqual(11268, wld.RawHeightAt(x, z));
            Assert.AreEqual(
                25.36,
                wld.WorldHeightAtSample(x, z),
                0.0001);

            LegacySvmapFile svmap =
                LegacySvmapParser.Parse(
                    corpus.Resolve("DATA_Español/world/0.svmap"));

            Assert.AreEqual(2048, svmap.MapSize);
            Assert.AreEqual(11, svmap.Portals.Count);
            Assert.AreEqual(509, svmap.MonsterAreas.Count);
            Assert.AreEqual(1330, svmap.MonsterInstanceCount);
            Assert.AreEqual(150, svmap.Npcs.Count);
            Assert.AreEqual(198, svmap.NpcPositionCount);
        }

        private static void Assert3dc(
            CanonicalClientCorpus corpus,
            string relativePath,
            int version,
            int bones,
            int vertices,
            int faces)
        {
            Legacy3dcFile parsed =
                Legacy3dcParser.Parse(corpus.Resolve(relativePath));

            Assert.AreEqual(version, parsed.Version, relativePath);
            Assert.AreEqual(bones, parsed.InverseBindMatrices.Count, relativePath);
            Assert.AreEqual(vertices, parsed.Vertices.Count, relativePath);
            Assert.AreEqual(faces, parsed.Faces.Count, relativePath);
        }

        private static void AssertAni(
            CanonicalClientCorpus corpus,
            string relativePath,
            bool v2,
            uint start,
            uint end,
            int bones)
        {
            LegacyAniFile parsed =
                LegacyAniParser.Parse(corpus.Resolve(relativePath));

            Assert.AreEqual(v2, parsed.IsV2, relativePath);
            Assert.AreEqual(start, parsed.StartKeyframe, relativePath);
            Assert.AreEqual(end, parsed.EndKeyframe, relativePath);
            Assert.AreEqual(bones, parsed.Bones.Count, relativePath);
        }

        private static byte[] BuildWldTerrain(
            int mapSize,
            ushort[] heights,
            byte[] textureMap,
            string textureName,
            float tileSize,
            string walkSound,
            string innerLayout)
        {
            int resolution =
                LegacyTerrainHeightCore.ResolutionForMapSize(mapSize);

            int expected =
                resolution * resolution;

            if (heights.Length != expected ||
                textureMap.Length != expected)
            {
                throw new ArgumentException(
                    "Synthetic WLD sample arrays do not match map resolution.");
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("FLD\0"));
                writer.Write((uint)mapSize);

                for (int i = 0; i < heights.Length; i++)
                    writer.Write(heights[i]);

                writer.Write(textureMap);
                writer.Write(1);

                WriteFixedAscii256(writer, textureName);
                writer.Write(tileSize);
                WriteFixedAscii256(writer, walkSound);
                WriteFixedAscii256(writer, innerLayout);

                return stream.ToArray();
            }
        }

        private static void WriteFixedAscii256(
            BinaryWriter writer,
            string value)
        {
            byte[] output = new byte[256];
            byte[] source =
                Encoding.ASCII.GetBytes(value ?? string.Empty);

            int count =
                Math.Min(source.Length, output.Length - 1);

            Buffer.BlockCopy(
                source,
                0,
                output,
                0,
                count);

            writer.Write(output);
        }

        private static byte[] Build3dc(
            int version,
            int boneCount,
            VertexFixture[] vertices,
            ushort[][] faces)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(version);
                writer.Write(boneCount);

                for (int i = 0; i < boneCount; i++)
                    WriteIdentityMatrix(writer);

                writer.Write(vertices.Length);

                for (int i = 0; i < vertices.Length; i++)
                {
                    VertexFixture v = vertices[i];
                    WriteVector3(writer, v.Position);
                    writer.Write(v.W1);

                    if (version == 444)
                    {
                        writer.Write(v.W2);
                        writer.Write(v.W3);
                    }

                    writer.Write(v.B1);
                    writer.Write(v.B2);
                    writer.Write(v.B3);
                    writer.Write((byte)0);
                    WriteVector3(writer, v.Normal);
                    writer.Write(v.UV.x);
                    writer.Write(v.UV.y);
                }

                writer.Write(faces.Length);

                for (int i = 0; i < faces.Length; i++)
                {
                    writer.Write(faces[i][0]);
                    writer.Write(faces[i][1]);
                    writer.Write(faces[i][2]);
                }

                return stream.ToArray();
            }
        }

        private static byte[] BuildSkeletonless3dc()
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(0);
                writer.Write(1);

                WriteVector3(writer, new Vector3(0f, 1f, 0f));
                writer.Write(1f);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                WriteVector3(writer, Vector3.forward);
                writer.Write(0.5f);
                writer.Write(0.5f);

                writer.Write(1);
                writer.Write((ushort)0);
                writer.Write((ushort)0);
                writer.Write((ushort)0);

                return stream.ToArray();
            }
        }

        private static byte[] BuildTexturePrefixed3dc()
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                byte[] name =
                    Encoding.ASCII.GetBytes("Mob_Rend_01.TGA\0");

                writer.Write(name.Length);
                writer.Write(name);

                writer.Write(1);
                WriteIdentityMatrix(writer);

                writer.Write(1);
                WriteVector3(writer, Vector3.zero);
                writer.Write(1f);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((byte)0);
                WriteVector3(writer, Vector3.up);
                writer.Write(0f);
                writer.Write(0f);

                writer.Write(0);
                return stream.ToArray();
            }
        }

        private static byte[] BuildAniV2()
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("ANI_V2"));
                writer.Write((uint)0);
                writer.Write((uint)30);
                writer.Write((ushort)2);

                writer.Write(-1);
                WriteIdentityMatrix(writer);
                writer.Write(0);
                writer.Write(0);

                writer.Write(0);
                WriteIdentityMatrix(writer);
                writer.Write(2);

                writer.Write((uint)0);
                WriteQuaternion(writer, Quaternion.identity);

                writer.Write((uint)30);
                WriteQuaternion(
                    writer,
                    Quaternion.AngleAxis(45f, Vector3.up));

                writer.Write(1);
                writer.Write((uint)15);
                WriteVector3(writer, new Vector3(0f, 1f, 0f));

                return stream.ToArray();
            }
        }

        private static void WriteIdentityMatrix(BinaryWriter writer)
        {
            Matrix4x4 m = Matrix4x4.identity;

            writer.Write(m.m00);
            writer.Write(m.m10);
            writer.Write(m.m20);
            writer.Write(m.m30);

            writer.Write(m.m01);
            writer.Write(m.m11);
            writer.Write(m.m21);
            writer.Write(m.m31);

            writer.Write(m.m02);
            writer.Write(m.m12);
            writer.Write(m.m22);
            writer.Write(m.m32);

            writer.Write(m.m03);
            writer.Write(m.m13);
            writer.Write(m.m23);
            writer.Write(m.m33);
        }

        private static void WriteVector3(
            BinaryWriter writer,
            Vector3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        private static void WriteQuaternion(
            BinaryWriter writer,
            Quaternion value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
            writer.Write(value.w);
        }

        private struct VertexFixture
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 UV;
            public float W1;
            public float W2;
            public float W3;
            public byte B1;
            public byte B2;
            public byte B3;
        }
    }
}
