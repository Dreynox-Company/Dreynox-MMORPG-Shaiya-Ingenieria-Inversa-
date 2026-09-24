using System;
using System.IO;
using System.Text;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.World;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyVaniManiParserTests
    {
        [Test]
        public void SyntheticVaniParsesAllFramesAndConsumesFile()
        {
            LegacyVaniFile file =
                LegacyVaniParser.Parse(
                    BuildSyntheticVani());

            Assert.AreEqual(
                Vector3.zero,
                file.Center);

            Assert.AreEqual(
                1f,
                file.Radius,
                0.000001f);

            Assert.AreEqual(
                2,
                file.FrameCount);

            Assert.AreEqual(
                66,
                file.Unknown1);

            Assert.AreEqual(
                1,
                file.Meshes.Count);

            LegacyVaniMesh mesh =
                file.Meshes[0];

            Assert.AreEqual(
                "fixture.tga",
                mesh.TextureName);

            Assert.AreEqual(
                1,
                mesh.Faces.Count);

            Assert.AreEqual(
                3,
                mesh.Vertices.Count);

            Assert.AreEqual(
                2,
                mesh.Vertices[1]
                    .Frames.Count);

            Assert.AreEqual(
                -1,
                mesh.Vertices[1]
                    .Frames[1]
                    .BoneId);

            Assert.AreEqual(
                new Vector3(
                    1f,
                    1f,
                    0f),
                mesh.Vertices[1]
                    .Frames[1]
                    .Position);
        }

        [Test]
        public void SyntheticManiParsesRotationDescriptor()
        {
            LegacyManiFile file =
                LegacyManiParser.Parse(
                    BuildSyntheticMani());

            Assert.AreEqual(
                33,
                file.Version);

            Assert.IsTrue(
                file.RotationEnabled);

            Assert.AreEqual(
                Vector3.up,
                file.RotationAxis);

            Assert.AreEqual(
                0.25f,
                file.AnimationSpeed,
                0.000001f);

            Assert.AreEqual(
                500,
                file.Unknown13);
        }

        [Test]
        public void CanonicalMapZeroVaniSamplesMatchObservedStructureWhenCorpusIsConfigured()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus
                    .FromStoredRoot();

            if (corpus == null ||
                !corpus.Validate()
                    .IsCanonical)
            {
                Assert.Ignore(
                    "Canonical ps0032 corpus is not configured on this machine.");
            }

            LegacyVaniFile eagle =
                LegacyVaniParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/entity/vani/eagle.vani"));

            Assert.AreEqual(
                1,
                eagle.Meshes.Count);

            Assert.AreEqual(
                64,
                eagle.FrameCount);

            Assert.AreEqual(
                66,
                eagle.Unknown1);

            Assert.AreEqual(
                21,
                eagle.TotalVertexCount);

            Assert.AreEqual(
                22,
                eagle.TotalFaceCount);

            Assert.AreEqual(
                "eagle.tga",
                eagle.Meshes[0]
                    .TextureName);

            for (int vertex = 0;
                 vertex <
                    eagle.Meshes[0]
                        .Vertices.Count;
                 vertex++)
            {
                for (int frame = 0;
                     frame <
                        eagle.Meshes[0]
                            .Vertices[vertex]
                            .Frames.Count;
                     frame++)
                {
                    Assert.AreEqual(
                        -1,
                        eagle.Meshes[0]
                            .Vertices[vertex]
                            .Frames[frame]
                            .BoneId);
                }
            }

            LegacyVaniFile banner =
                LegacyVaniParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/entity/vani/F_FB_banner02.VANI"));

            Assert.AreEqual(
                3,
                banner.Meshes.Count);

            Assert.AreEqual(
                61,
                banner.FrameCount);

            Assert.AreEqual(
                33,
                banner.Unknown1);

            Assert.AreEqual(
                532,
                banner.TotalVertexCount);

            Assert.AreEqual(
                356,
                banner.TotalFaceCount);
        }

        [Test]
        public void CanonicalMapZeroManiSamplesMatchObservedRotationDescriptorsWhenCorpusIsConfigured()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus
                    .FromStoredRoot();

            if (corpus == null ||
                !corpus.Validate()
                    .IsCanonical)
            {
                Assert.Ignore(
                    "Canonical ps0032 corpus is not configured on this machine.");
            }

            LegacyManiFile raputa01 =
                LegacyManiParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/entity/mani/raputa01.mani"));

            LegacyManiFile raputa02 =
                LegacyManiParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/entity/mani/raputa02.mani"));

            LegacyManiFile raputa03 =
                LegacyManiParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/entity/mani/raputa03.mani"));

            LegacyManiFile raputa04 =
                LegacyManiParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/entity/mani/raputa04.mani"));

            LegacyManiFile[] samples =
            {
                raputa01,
                raputa02,
                raputa03,
                raputa04
            };

            for (int i = 0;
                 i < samples.Length;
                 i++)
            {
                Assert.AreEqual(
                    33,
                    samples[i].Version);

                Assert.AreEqual(
                    Vector3.up,
                    samples[i].RotationAxis);

                Assert.AreEqual(
                    500,
                    samples[i].Unknown13);
            }

            Assert.IsFalse(
                raputa01.RotationEnabled);

            Assert.AreEqual(
                0f,
                raputa01.AnimationSpeed,
                0.000001f);

            Assert.IsTrue(
                raputa02.RotationEnabled);

            Assert.AreEqual(
                0.02617994f,
                raputa02.AnimationSpeed,
                0.0000001f);

            Assert.IsTrue(
                raputa03.RotationEnabled);

            Assert.AreEqual(
                0.005235988f,
                raputa03.AnimationSpeed,
                0.0000001f);

            Assert.IsTrue(
                raputa04.RotationEnabled);

            Assert.AreEqual(
                0.031415924f,
                raputa04.AnimationSpeed,
                0.0000001f);

            Assert.AreEqual(
                45f,
                LegacyManiRotationRuntime.ResolveDegreesPerSecond(
                    raputa02.AnimationSpeed,
                    30f,
                    true),
                0.0001f);

            Assert.AreEqual(
                9f,
                LegacyManiRotationRuntime.ResolveDegreesPerSecond(
                    raputa03.AnimationSpeed,
                    30f,
                    true),
                0.0001f);

            Assert.AreEqual(
                54f,
                LegacyManiRotationRuntime.ResolveDegreesPerSecond(
                    raputa04.AnimationSpeed,
                    30f,
                    true),
                0.0001f);

            LegacyWldTerrainFile wld =
                LegacyWldTerrainParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/world/0.wld"));

            int rotatingPlacements = 0;

            for (int i = 0;
                 i < wld.MAniCoordinates.Count;
                 i++)
            {
                LegacyManiFile descriptor =
                    new[]
                    {
                        raputa01,
                        raputa02,
                        raputa03,
                        raputa04
                    }[
                        wld.MAniCoordinates[i]
                            .Id];

                if (descriptor.RotationEnabled)
                    rotatingPlacements++;
            }

            Assert.AreEqual(
                5,
                rotatingPlacements);
        }

        private static byte[] BuildSyntheticVani()
        {
            using (MemoryStream stream =
                   new MemoryStream())
            using (BinaryWriter writer =
                   new BinaryWriter(
                       stream,
                       Encoding.ASCII))
            {
                WriteVector3(
                    writer,
                    Vector3.zero);

                writer.Write(1f);

                WriteBounds(
                    writer,
                    -Vector3.one,
                    Vector3.one);

                writer.Write(1);
                writer.Write(2);
                writer.Write(66);

                WriteLengthPrefixed(
                    writer,
                    "fixture.tga");

                writer.Write(1);
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write((ushort)2);

                writer.Write(3);

                for (int frame = 0;
                     frame < 2;
                     frame++)
                {
                    for (int vertex = 0;
                         vertex < 3;
                         vertex++)
                    {
                        WriteVector3(
                            writer,
                            new Vector3(
                                vertex,
                                frame,
                                0f));

                        WriteVector3(
                            writer,
                            Vector3.up);

                        writer.Write(-1);
                        writer.Write(
                            vertex / 2f);
                        writer.Write(
                            frame);
                    }
                }

                WriteBounds(
                    writer,
                    -Vector3.one,
                    Vector3.one);

                writer.Write(0);

                return stream.ToArray();
            }
        }

        private static byte[] BuildSyntheticMani()
        {
            using (MemoryStream stream =
                   new MemoryStream())
            using (BinaryWriter writer =
                   new BinaryWriter(stream))
            {
                writer.Write(33);
                writer.Write(0);

                WriteVector3(
                    writer,
                    Vector3.right);

                writer.Write(0f);
                writer.Write(0f);
                writer.Write(1f);
                writer.Write(80);
                writer.Write(0);

                WriteVector3(
                    writer,
                    Vector3.up);

                writer.Write(0f);
                writer.Write(0f);
                writer.Write(1);

                WriteVector3(
                    writer,
                    Vector3.up);

                writer.Write(0.25f);
                writer.Write((short)0);
                writer.Write((short)0);

                WriteVector3(
                    writer,
                    Vector3.right);

                writer.Write(0f);
                writer.Write(1f);
                writer.Write(500);

                Assert.AreEqual(
                    LegacyManiParser.SerializedBytes,
                    stream.Length);

                return stream.ToArray();
            }
        }

        private static void WriteBounds(
            BinaryWriter writer,
            Vector3 lower,
            Vector3 upper)
        {
            WriteVector3(
                writer,
                lower);

            WriteVector3(
                writer,
                upper);
        }

        private static void WriteVector3(
            BinaryWriter writer,
            Vector3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        private static void WriteLengthPrefixed(
            BinaryWriter writer,
            string value)
        {
            byte[] bytes =
                Encoding.ASCII.GetBytes(
                    value);

            writer.Write(
                bytes.Length);

            writer.Write(
                bytes);
        }
    }
}
