using System;
using System.IO;
using System.Text;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyWldFullParserTests
    {
        [Test]
        public void FullFieldWorldParsesEnvironmentSectionsAndPreservesTail()
        {
            byte[] bytes = BuildFullFieldFixture();
            LegacyWldTerrainFile file =
                LegacyWldTerrainParser.Parse(bytes);

            Assert.AreEqual("FLD\0", file.Signature);
            Assert.AreEqual(2, file.MapSize);
            Assert.AreEqual(2, file.Resolution);
            Assert.AreEqual(1, file.Textures.Count);
            Assert.AreEqual("fixture.wtr", file.InnerLayout);

            Assert.AreEqual(1, file.Buildings.Names.Count);
            Assert.AreEqual(1, file.Buildings.Coordinates.Count);
            Assert.AreEqual("building.smod", file.Buildings.Names[0]);

            Assert.AreEqual(1, file.MAniNames.Count);
            Assert.AreEqual(1, file.MAniCoordinates.Count);
            Assert.AreEqual(0, file.MAniCoordinates[0].WorldBuildingId);
            Assert.AreEqual(0, file.MAniCoordinates[0].Id);

            Assert.AreEqual("fixture.eft", file.EffectName);
            Assert.AreEqual(1, file.Effects.Count);
            Assert.AreEqual(2, file.Effects[0].EffectId);

            Assert.AreEqual(1, file.Objects.Names.Count);
            Assert.AreEqual(1, file.MusicNames.Count);
            Assert.AreEqual(1, file.MusicZones.Count);
            Assert.AreEqual(0, file.MusicZones[0].Id);

            Assert.AreEqual(1, file.SoundEffectNames.Count);
            Assert.AreEqual(1, file.Zones.Count);
            CollectionAssert.AreEqual(
                new[] { 11, 22 },
                file.Zones[0].Identifiers);
            Assert.AreEqual(1, file.SoundEffects.Count);
            Assert.AreEqual(0, file.SoundEffects[0].Id);
            Assert.AreEqual(1, file.UnknownBoundingBoxes.Count);

            Assert.AreEqual(1, file.Portals.Count);
            Assert.AreEqual(18, file.Portals[0].MapId);
            Assert.AreEqual(1, file.Portals[0].Faction);
            Assert.AreEqual(
                new Vector3(10f, 20f, 30f),
                file.Portals[0].DestinationPosition);

            Assert.AreEqual(1, file.Spawns.Count);
            Assert.AreEqual(1, file.Spawns[0].Faction);
            Assert.AreEqual(1, file.NamedAreas.Count);
            Assert.AreEqual("Fixture Area", file.NamedAreas[0].Text1);

            Assert.AreEqual(1, file.Npcs.Count);
            Assert.AreEqual(8, file.Npcs[0].Type);
            Assert.AreEqual(26, file.Npcs[0].TypeId);
            Assert.AreEqual(2, file.Npcs[0].PatrolCoordinates.Count);

            Assert.AreEqual("sky.3do", file.SkyName);
            Assert.AreEqual("cloud1.dds", file.CloudsName1);
            Assert.AreEqual("cloud2.dds", file.CloudsName2);

            Assert.AreEqual(
                new Vector3(1f, 2f, 3f),
                file.Point1);
            Assert.AreEqual(5.5f, file.Unknown5, 0.000001f);
            Assert.AreEqual(6.5f, file.Unknown6, 0.000001f);

            Assert.AreEqual(7, file.UnparsedTailBytes);
            CollectionAssert.AreEqual(
                new byte[]
                {
                    0x44, 0x52, 0x45,
                    0x59, 0x4E, 0x4F, 0x58
                },
                file.UnparsedTail);

            Assert.AreEqual(
                bytes.Length,
                file.KnownBytesConsumed +
                file.UnparsedTailBytes);
        }

        [Test]
        public void CanonicalMapZeroFullWldEnvironmentIsInternallyConsistentWhenCorpusIsConfigured()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null ||
                !corpus.Validate().IsCanonical)
            {
                Assert.Ignore(
                    "Canonical ps0032 corpus is not configured on this machine.");
            }

            LegacyWldTerrainFile wld =
                LegacyWldTerrainParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/world/0.wld"));

            Assert.AreEqual(2048, wld.MapSize);
            Assert.AreEqual(1025, wld.Resolution);
            Assert.AreEqual(7, wld.Textures.Count);

            Assert.Greater(
                wld.KnownBytesConsumed,
                0);

            if (wld.Effects.Count > 0)
            {
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(
                        wld.EffectName));

                LegacyEftFile effects =
                    LegacyEftPrefabImporter
                        .ParseCanonical(
                            corpus,
                            wld.EffectName);

                Assert.IsNotNull(
                    effects,
                    "Map 0 effect library '" +
                    wld.EffectName +
                    "' could not be resolved.");

                for (int i = 0;
                     i < wld.Effects.Count;
                     i++)
                {
                    Assert.GreaterOrEqual(
                        wld.Effects[i].EffectId,
                        0);

                    Assert.Less(
                        wld.Effects[i].EffectId,
                        effects.Sequences.Count,
                        "WLD effect placement " +
                        i +
                        " must index EFT sequences.");
                }
            }

            if (!string.IsNullOrWhiteSpace(
                    wld.InnerLayout) &&
                Path.GetExtension(
                    wld.InnerLayout)
                    .Equals(
                        ".wtr",
                        StringComparison.OrdinalIgnoreCase))
            {
                string waterRoot =
                    LegacyUiAssetImporter
                        .ResolveCaseInsensitive(
                            corpus.RootPath,
                            "DATA_Español/entity/water");

                string waterPath =
                    CanonicalResourceIndex
                        .FindUnique(
                            waterRoot,
                            Path.GetFileName(
                                wld.InnerLayout));

                Assert.IsNotNull(
                    waterPath,
                    "Map 0 WTR layout was not found: " +
                    wld.InnerLayout);

                LegacyWtrFile water =
                    LegacyWtrParser.Parse(
                        waterPath);

                Assert.Greater(
                    water.TileSize,
                    0f);

                Assert.Greater(
                    water.FrameNames.Count,
                    0);
            }

            Assert.AreEqual(
                "World.wtr",
                wld.InnerLayout);

            Assert.AreEqual(
                "world_r1.EFT",
                wld.EffectName);

            Assert.AreEqual(
                "sky_A2.bmp",
                wld.SkyName);

            Assert.AreEqual(
                "clouds01_B1.tga",
                wld.CloudsName1);

            Assert.AreEqual(
                "clouds02_B1.tga",
                wld.CloudsName2);

            CollectionAssert.Contains(
                wld.MusicNames,
                "bgm_town06.wav");

            CollectionAssert.Contains(
                wld.MusicNames,
                "bgm_title.wav");

            CollectionAssert.Contains(
                wld.MusicNames,
                "bgm_frt01.wav");

            CollectionAssert.Contains(
                wld.MusicNames,
                "bgm_frt02.wav");

            CollectionAssert.Contains(
                wld.MusicNames,
                "bgm_town07.wav");

            CollectionAssert.Contains(
                wld.SoundEffectNames,
                "bg_wind0001.wav");

            CollectionAssert.Contains(
                wld.SoundEffectNames,
                "bg_stream01.wav");

            CollectionAssert.Contains(
                wld.SoundEffectNames,
                "bg_wolf0001.wav");
        }

        [Test]
        public void WtrFixtureParsesFixedFrameNames()
        {
            byte[] bytes;

            using (MemoryStream stream =
                   new MemoryStream())
            using (BinaryWriter writer =
                   new BinaryWriter(stream))
            {
                writer.Write(64f);
                writer.Write((uint)7);
                writer.Write(9);
                writer.Write(2);

                WriteFixed(
                    writer,
                    "caust00.tga");

                WriteFixed(
                    writer,
                    "caust01.tga");

                bytes = stream.ToArray();
            }

            LegacyWtrFile water =
                LegacyWtrParser.Parse(bytes);

            Assert.AreEqual(
                64f,
                water.TileSize,
                0.000001f);

            Assert.AreEqual(
                7u,
                water.Unknown2);

            Assert.AreEqual(
                9,
                water.Unknown3);

            CollectionAssert.AreEqual(
                new[]
                {
                    "caust00.tga",
                    "caust01.tga"
                },
                water.FrameNames);
        }

        private static byte[] BuildFullFieldFixture()
        {
            using (MemoryStream stream =
                   new MemoryStream())
            using (BinaryWriter writer =
                   new BinaryWriter(
                       stream,
                       Encoding.ASCII))
            {
                writer.Write(
                    Encoding.ASCII.GetBytes(
                        "FLD\0"));

                writer.Write((uint)2);

                for (int i = 0; i < 4; i++)
                    writer.Write(
                        (ushort)(10000 + i));

                writer.Write(
                    new byte[]
                    {
                        0, 0, 0, 0
                    });

                writer.Write(1);
                WriteFixed(
                    writer,
                    "grass.dds");
                writer.Write(8f);
                WriteFixed(
                    writer,
                    "walk_grass.wav");

                WriteFixed(
                    writer,
                    "fixture.wtr");

                WriteNameCoordinateGroup(
                    writer,
                    "building.smod");

                WriteEmptyNameCoordinateGroup(
                    writer);
                WriteEmptyNameCoordinateGroup(
                    writer);
                WriteEmptyNameCoordinateGroup(
                    writer);
                WriteEmptyNameCoordinateGroup(
                    writer);
                WriteEmptyNameCoordinateGroup(
                    writer);
                WriteEmptyNameCoordinateGroup(
                    writer);

                writer.Write(1);
                WriteFixed(
                    writer,
                    "spin.mani");

                writer.Write(1);
                writer.Write(0);
                writer.Write(0);
                WriteVector3(
                    writer,
                    new Vector3(
                        4f,
                        5f,
                        6f));
                WriteVector3(
                    writer,
                    Vector3.forward);
                WriteVector3(
                    writer,
                    Vector3.up);

                WriteFixed(
                    writer,
                    "fixture.eft");

                writer.Write(1);
                WriteVector3(
                    writer,
                    new Vector3(
                        7f,
                        8f,
                        9f));
                WriteVector3(
                    writer,
                    Vector3.forward);
                WriteVector3(
                    writer,
                    Vector3.up);
                writer.Write(2);

                writer.Write(101);
                writer.Write(102);
                writer.Write(103);

                WriteNameCoordinateGroup(
                    writer,
                    "object.smod");

                writer.Write(1);
                WriteFixed(
                    writer,
                    "theme.ogg");

                writer.Write(1);
                WriteBounds(
                    writer,
                    Vector3.zero,
                    new Vector3(
                        100f,
                        50f,
                        100f));
                writer.Write(70f);
                writer.Write(0);
                writer.Write(0);

                writer.Write(1);
                WriteFixed(
                    writer,
                    "birds.wav");

                writer.Write(1);
                WriteBounds(
                    writer,
                    Vector3.zero,
                    new Vector3(
                        40f,
                        20f,
                        40f));
                writer.Write(2);
                writer.Write(11);
                writer.Write(22);

                writer.Write(1);
                writer.Write(0);
                WriteVector3(
                    writer,
                    new Vector3(
                        25f,
                        2f,
                        25f));
                writer.Write(30f);

                writer.Write(1);
                WriteBounds(
                    writer,
                    new Vector3(
                        1f,
                        2f,
                        3f),
                    new Vector3(
                        4f,
                        5f,
                        6f));
                writer.Write(3f);

                writer.Write(1);
                WriteBounds(
                    writer,
                    new Vector3(
                        10f,
                        0f,
                        10f),
                    new Vector3(
                        20f,
                        10f,
                        20f));
                writer.Write(8f);
                WriteFixed(
                    writer,
                    "Portal Fixture");
                WriteFixed(
                    writer,
                    string.Empty);
                writer.Write((byte)18);
                writer.Write((short)1);
                writer.Write((byte)0);
                WriteVector3(
                    writer,
                    new Vector3(
                        10f,
                        20f,
                        30f));

                writer.Write(1);
                writer.Write(1);
                WriteBounds(
                    writer,
                    new Vector3(
                        30f,
                        0f,
                        30f),
                    new Vector3(
                        40f,
                        10f,
                        40f));
                writer.Write(8f);
                writer.Write(1);
                writer.Write(0);

                writer.Write(1);
                WriteBounds(
                    writer,
                    new Vector3(
                        50f,
                        0f,
                        50f),
                    new Vector3(
                        60f,
                        10f,
                        60f));
                writer.Write(8f);
                WriteFixed(
                    writer,
                    "Fixture Area");
                WriteFixed(
                    writer,
                    "fixture_area.bmp");
                writer.Write(2);
                writer.Write(0);

                // NPC logical count = one NPC + two patrol coordinates.
                writer.Write(3);
                writer.Write(8);
                writer.Write(26);
                WriteVector3(
                    writer,
                    new Vector3(
                        70f,
                        5f,
                        70f));
                writer.Write(1.25f);
                writer.Write(2);
                WriteVector3(
                    writer,
                    new Vector3(
                        71f,
                        5f,
                        70f));
                WriteVector3(
                    writer,
                    new Vector3(
                        72f,
                        5f,
                        70f));

                WriteFixed(
                    writer,
                    "sky.3do");
                WriteFixed(
                    writer,
                    "cloud1.dds");
                WriteFixed(
                    writer,
                    "cloud2.dds");

                WriteVector3(
                    writer,
                    new Vector3(
                        1f,
                        2f,
                        3f));
                WriteVector3(
                    writer,
                    new Vector3(
                        4f,
                        5f,
                        6f));
                WriteVector3(
                    writer,
                    new Vector3(
                        7f,
                        8f,
                        9f));

                writer.Write(5.5f);
                writer.Write(6.5f);

                writer.Write(
                    new byte[]
                    {
                        0x44, 0x52, 0x45,
                        0x59, 0x4E, 0x4F, 0x58
                    });

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
            byte[] bytes =
                new byte[256];

            byte[] source =
                Encoding.ASCII.GetBytes(
                    value ??
                    string.Empty);

            int count =
                Math.Min(
                    bytes.Length - 1,
                    source.Length);

            Buffer.BlockCopy(
                source,
                0,
                bytes,
                0,
                count);

            writer.Write(bytes);
        }
    }
}
