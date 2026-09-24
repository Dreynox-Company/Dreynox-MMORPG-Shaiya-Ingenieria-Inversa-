using System;
using System.IO;
using System.Text;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyDunWldTests
    {
        [Test]
        public void SyntheticDungeonWorldSkipsFieldTerrainAndReadsCommonTail()
        {
            LegacyWldTerrainFile wld =
                LegacyWldTerrainParser.Parse(
                    BuildSyntheticDun());

            Assert.AreEqual(
                "DUN\0",
                wld.Signature);

            Assert.AreEqual(
                0,
                wld.MapSize);

            Assert.AreEqual(
                0,
                wld.Resolution);

            Assert.AreEqual(
                0,
                wld.RawHeights.Length);

            Assert.AreEqual(
                0,
                wld.TextureMap.Length);

            Assert.AreEqual(
                0,
                wld.Textures.Count);

            Assert.AreEqual(
                "fixture.dg",
                wld.InnerLayout);

            Assert.AreEqual(
                1,
                wld.Buildings.Names.Count);

            Assert.AreEqual(
                "fixture.smod",
                wld.Buildings.Names[0]);

            Assert.AreEqual(
                1,
                wld.Buildings.Coordinates.Count);

            Assert.AreEqual(
                "fixture.eft",
                wld.EffectName);

            Assert.AreEqual(
                1,
                wld.Effects.Count);

            Assert.AreEqual(
                string.Empty,
                wld.SkyName);

            Assert.AreEqual(
                string.Empty,
                wld.CloudsName1);

            Assert.AreEqual(
                string.Empty,
                wld.CloudsName2);

            Assert.AreEqual(
                0,
                wld.UnparsedTailBytes);
        }

        [Test]
        public void CanonicalLoginDungeonWorldMatchesPs0032WhenCorpusIsConfigured()
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

            LegacyWldTerrainFile wld =
                LegacyWldTerrainParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/world/Login.wld"));

            Assert.AreEqual(
                "DUN\0",
                wld.Signature);

            Assert.AreEqual(
                "DUN_LOGIN.dg",
                wld.InnerLayout);

            CollectionAssert.Contains(
                wld.Buildings.Names,
                "Dragon.SMOD");

            CollectionAssert.Contains(
                wld.Buildings.Names,
                "Starlighting.SMOD");

            CollectionAssert.Contains(
                wld.Buildings.Names,
                "login_A.SMOD");

            Assert.AreEqual(
                "login.EFT",
                wld.EffectName);

            Assert.AreEqual(
                2,
                wld.Shapes.Names.Count);

            Assert.AreEqual(
                2,
                wld.Shapes.Coordinates.Count);

            CollectionAssert.Contains(
                wld.Shapes.Names,
                "Dragon.SMOD");

            CollectionAssert.Contains(
                wld.Shapes.Names,
                "Starlighting.SMOD");

            Assert.AreEqual(
                1,
                wld.Grass.Names.Count);

            Assert.AreEqual(
                "login_A.SMOD",
                wld.Grass.Names[0]);

            Assert.AreEqual(
                1,
                wld.Grass.Coordinates.Count);

            Assert.AreEqual(
                16,
                wld.Effects.Count);

            Assert.IsTrue(
                wld.MapSize == 0 &&
                wld.Resolution == 0 &&
                wld.RawHeights.Length == 0 &&
                wld.TextureMap.Length == 0);

            Assert.IsTrue(
                string.IsNullOrEmpty(
                    wld.SkyName));

            Assert.AreEqual(
                new Vector3(
                    128f,
                    128f,
                    128f),
                wld.Point3);

            Assert.AreEqual(
                150f,
                wld.Unknown5,
                0.000001f);

            Assert.AreEqual(
                200f,
                wld.Unknown6,
                0.000001f);
        }

        private static byte[] BuildSyntheticDun()
        {
            using (MemoryStream stream =
                   new MemoryStream())
            using (BinaryWriter writer =
                   new BinaryWriter(
                       stream,
                       Encoding.ASCII))
            {
                writer.Write(
                    Encoding.ASCII
                        .GetBytes(
                            "DUN\0"));

                WriteFixed(
                    writer,
                    "fixture.dg");

                WriteNameCoordinateGroup(
                    writer,
                    "fixture.smod");

                for (int group = 0;
                     group < 6;
                     group++)
                {
                    WriteEmptyNameCoordinateGroup(
                        writer);
                }

                writer.Write(0);
                writer.Write(0);

                WriteFixed(
                    writer,
                    "fixture.eft");

                writer.Write(1);

                WriteVector3(
                    writer,
                    new Vector3(
                        1f,
                        2f,
                        3f));

                WriteVector3(
                    writer,
                    Vector3.forward);

                WriteVector3(
                    writer,
                    Vector3.up);

                writer.Write(0);

                writer.Write(0);
                writer.Write(0);
                writer.Write(0);

                WriteEmptyNameCoordinateGroup(
                    writer);

                // Music names, music zones, sound names, zones,
                // positional sounds, restricted boxes, portals, spawns,
                // named areas and NPC logical entries.
                for (int counter = 0;
                     counter < 10;
                     counter++)
                {
                    writer.Write(0);
                }

                WriteVector3(
                    writer,
                    new Vector3(
                        1f,
                        1f,
                        1f));

                WriteVector3(
                    writer,
                    new Vector3(
                        0.5f,
                        0.5f,
                        0.5f));

                WriteVector3(
                    writer,
                    new Vector3(
                        0.1f,
                        0.2f,
                        0.3f));

                writer.Write(10f);
                writer.Write(100f);

                return stream.ToArray();
            }
        }

        private static void WriteNameCoordinateGroup(
            BinaryWriter writer,
            string name)
        {
            writer.Write(1);
            WriteFixed(
                writer,
                name);

            writer.Write(1);
            writer.Write(0);

            WriteVector3(
                writer,
                new Vector3(
                    1f,
                    2f,
                    3f));

            WriteVector3(
                writer,
                Vector3.forward);

            WriteVector3(
                writer,
                Vector3.up);
        }

        private static void WriteEmptyNameCoordinateGroup(
            BinaryWriter writer)
        {
            writer.Write(0);
            writer.Write(0);
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
