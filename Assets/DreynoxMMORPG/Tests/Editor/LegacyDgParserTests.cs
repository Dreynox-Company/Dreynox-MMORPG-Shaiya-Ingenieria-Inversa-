using System;
using System.IO;
using System.Text;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyDgParserTests
    {
        [Test]
        public void SyntheticDgParsesGeometryUv2AndHierarchy()
        {
            LegacyDgFile file =
                LegacyDgParser.Parse(
                    BuildSyntheticDg());

            Assert.AreEqual(
                1,
                file.TextureNames.Count);

            Assert.AreEqual(
                "fixture.tga",
                file.TextureNames[0]);

            Assert.AreEqual(
                1,
                file.LightmapCount);

            Assert.AreEqual(
                1,
                file.NodeCount);

            Assert.AreEqual(
                1,
                file.MeshGroupCount);

            Assert.AreEqual(
                1,
                file.MeshCount);

            Assert.AreEqual(
                3,
                file.VertexCount);

            Assert.AreEqual(
                1,
                file.FaceCount);

            LegacyDgMesh mesh =
                file.RootNode
                    .MeshGroups[0]
                    .Meshes[0];

            Assert.AreEqual(
                0,
                mesh.LightmapIndex);

            Assert.AreEqual(
                new Vector2(
                    0.25f,
                    0.75f),
                mesh.Vertices[1]
                    .LightmapUV);

            Assert.AreEqual(
                -1,
                mesh.Vertices[1]
                    .BoneId);

            Assert.AreEqual(
                0,
                file.RootNode
                    .Children.Count);
        }

        [Test]
        public void CanonicalDunLoginDgMatchesVerifiedStructureWhenCorpusIsConfigured()
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

            string dungeonRoot =
                LegacyUiAssetImporter
                    .ResolveCaseInsensitive(
                        corpus.RootPath,
                        "DATA_Español/world/dungeon");

            string path =
                CanonicalResourceIndex
                    .FindUnique(
                        dungeonRoot,
                        "dun_login.dg");

            Assert.IsNotNull(
                path);

            LegacyDgFile file =
                LegacyDgParser.Parse(
                    path);

            Assert.AreEqual(
                31,
                file.TextureNames.Count);

            Assert.AreEqual(
                4,
                file.LightmapCount);

            Assert.AreEqual(
                23,
                file.NodeCount);

            Assert.AreEqual(
                129,
                file.MeshGroupCount);

            Assert.AreEqual(
                131,
                file.MeshCount);

            Assert.AreEqual(
                6574,
                file.VertexCount);

            Assert.AreEqual(
                3188,
                file.FaceCount);

            Assert.AreEqual(
                "L_Dun1_Top_005.tga",
                file.TextureNames[0]);

            Assert.AreEqual(
                "L_Dun1_Object_003.tga",
                file.TextureNames[30]);
        }

        private static byte[] BuildSyntheticDg()
        {
            using (MemoryStream stream =
                   new MemoryStream())
            using (BinaryWriter writer =
                   new BinaryWriter(
                       stream,
                       Encoding.ASCII))
            {
                WriteBounds(
                    writer,
                    new Vector3(-5f, -2f, -5f),
                    new Vector3(5f, 2f, 5f));

                writer.Write(1);
                WriteFixed(
                    writer,
                    "fixture.tga");

                writer.Write(1);
                writer.Write(1);

                WriteVector3(
                    writer,
                    Vector3.zero);

                WriteBounds(
                    writer,
                    new Vector3(-5f, -2f, -5f),
                    new Vector3(5f, 2f, 5f));

                WriteBounds(
                    writer,
                    new Vector3(-4f, -1f, -4f),
                    new Vector3(4f, 1f, 4f));

                writer.Write(1);
                writer.Write(0);
                writer.Write(1);

                writer.Write(0);
                writer.Write(3);

                WriteDgVertex(
                    writer,
                    new Vector3(0f, 0f, 0f),
                    Vector3.up,
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f));

                WriteDgVertex(
                    writer,
                    new Vector3(1f, 0f, 0f),
                    Vector3.up,
                    new Vector2(1f, 0f),
                    new Vector2(0.25f, 0.75f));

                WriteDgVertex(
                    writer,
                    new Vector3(0f, 0f, 1f),
                    Vector3.up,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f));

                writer.Write(1);
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write((ushort)2);

                writer.Write(
                    (int)LegacyDgCollisionType.Transparent);

                for (int i = 0;
                     i < 8;
                     i++)
                {
                    writer.Write(0);
                }

                return stream.ToArray();
            }
        }

        private static void WriteDgVertex(
            BinaryWriter writer,
            Vector3 position,
            Vector3 normal,
            Vector2 uv,
            Vector2 uv2)
        {
            WriteVector3(
                writer,
                position);

            WriteVector3(
                writer,
                normal);

            writer.Write(-1);
            writer.Write(uv.x);
            writer.Write(uv.y);
            writer.Write(uv2.x);
            writer.Write(uv2.y);
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

        private static void WriteFixed(
            BinaryWriter writer,
            string value)
        {
            byte[] target =
                new byte[256];

            byte[] source =
                Encoding.ASCII.GetBytes(
                    value ??
                    string.Empty);

            Buffer.BlockCopy(
                source,
                0,
                target,
                0,
                Math.Min(
                    source.Length,
                    target.Length - 1));

            writer.Write(target);
        }
    }
}
