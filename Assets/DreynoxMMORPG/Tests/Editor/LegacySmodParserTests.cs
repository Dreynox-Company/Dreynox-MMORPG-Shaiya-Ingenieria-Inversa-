using System;
using System.IO;
using System.Text;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacySmodParserTests
    {
        [Test]
        public void SmodParsesRenderAndCollisionMeshes()
        {
            byte[] bytes = BuildSyntheticSmod();
            LegacySmodFile parsed =
                LegacySmodParser.Parse(bytes);

            Assert.AreEqual(
                new Vector3(1f, 2f, 3f),
                parsed.Center);

            Assert.AreEqual(
                4.5f,
                parsed.DistanceToCenter,
                0.000001f);

            Assert.AreEqual(1, parsed.Meshes.Count);
            Assert.AreEqual(
                "TestDiffuse.tga",
                parsed.Meshes[0].TextureName);

            Assert.AreEqual(
                3,
                parsed.Meshes[0].Vertices.Count);

            Assert.AreEqual(
                1,
                parsed.Meshes[0].Faces.Count);

            Assert.AreEqual(
                -1,
                parsed.Meshes[0].Vertices[0].BoneId);

            Assert.AreEqual(
                1,
                parsed.CollisionMeshes.Count);

            Assert.AreEqual(
                3,
                parsed.CollisionMeshes[0].Vertices.Count);

            Assert.AreEqual(
                1,
                parsed.CollisionMeshes[0].Faces.Count);
        }

        [Test]
        public void SmodRejectsOutOfRangeFace()
        {
            byte[] bytes =
                BuildSyntheticSmod(
                    invalidFace: true);

            Assert.Throws<InvalidDataException>(
                () => LegacySmodParser.Parse(bytes));
        }

        [Test]
        public void CanonicalMapZeroRockMatchesObservedSmodWhenConfigured()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null ||
                !corpus.Validate().IsCanonical)
            {
                Assert.Ignore(
                    "Canonical ps0032 corpus is not configured on this machine.");
            }

            LegacySmodFile parsed =
                LegacySmodParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/entity/building/R1_Rock_11.smod"));

            Assert.AreEqual(1, parsed.Meshes.Count);
            StringAssert.AreEqualIgnoringCase(
                "A1_bluff_03.tga",
                parsed.Meshes[0].TextureName);

            Assert.AreEqual(
                394,
                parsed.Meshes[0].Vertices.Count);

            Assert.AreEqual(
                274,
                parsed.Meshes[0].Faces.Count);

            Assert.AreEqual(
                1,
                parsed.CollisionMeshes.Count);

            Assert.AreEqual(
                98,
                parsed.CollisionMeshes[0].Vertices.Count);

            Assert.AreEqual(
                72,
                parsed.CollisionMeshes[0].Faces.Count);
        }

        private static byte[] BuildSyntheticSmod(
            bool invalidFace = false)
        {
            using (MemoryStream stream =
                   new MemoryStream())
            using (BinaryWriter writer =
                   new BinaryWriter(stream))
            {
                WriteVector3(
                    writer,
                    new Vector3(1f, 2f, 3f));

                writer.Write(4.5f);

                WriteBounds(
                    writer,
                    new Vector3(-2f, -1f, -3f),
                    new Vector3(2f, 5f, 3f));

                writer.Write(1);

                WriteLengthPrefixedAscii(
                    writer,
                    "TestDiffuse.tga");

                writer.Write(3);

                WriteSmodVertex(
                    writer,
                    new Vector3(0f, 0f, 0f),
                    Vector3.up,
                    new Vector2(0f, 0f));

                WriteSmodVertex(
                    writer,
                    new Vector3(1f, 0f, 0f),
                    Vector3.up,
                    new Vector2(1f, 0f));

                WriteSmodVertex(
                    writer,
                    new Vector3(0f, 0f, 1f),
                    Vector3.up,
                    new Vector2(0f, 1f));

                writer.Write(1);
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write(
                    invalidFace
                        ? (ushort)9
                        : (ushort)2);

                WriteBounds(
                    writer,
                    new Vector3(-2f, -1f, -3f),
                    new Vector3(2f, 5f, 3f));

                writer.Write(1);
                writer.Write(3);

                WriteVector3(
                    writer,
                    new Vector3(0f, 0f, 0f));

                WriteVector3(
                    writer,
                    new Vector3(1f, 0f, 0f));

                WriteVector3(
                    writer,
                    new Vector3(0f, 0f, 1f));

                writer.Write(1);
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write((ushort)2);

                return stream.ToArray();
            }
        }

        private static void WriteSmodVertex(
            BinaryWriter writer,
            Vector3 position,
            Vector3 normal,
            Vector2 uv)
        {
            WriteVector3(writer, position);
            WriteVector3(writer, normal);
            writer.Write(-1);
            writer.Write(uv.x);
            writer.Write(uv.y);
        }

        private static void WriteBounds(
            BinaryWriter writer,
            Vector3 lower,
            Vector3 upper)
        {
            WriteVector3(writer, lower);
            WriteVector3(writer, upper);
        }

        private static void WriteLengthPrefixedAscii(
            BinaryWriter writer,
            string value)
        {
            byte[] bytes =
                Encoding.ASCII.GetBytes(value);

            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static void WriteVector3(
            BinaryWriter writer,
            Vector3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }
    }
}
