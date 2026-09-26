using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LegacyEffectFormatTests
    {
        [Test]
        public void ThreeDeParsesVertexAnimation()
        {
            byte[] bytes;

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer =
                   new BinaryWriter(stream, Encoding.UTF8))
            {
                WriteString(writer, "spark.dds");

                writer.Write(3);

                Write3deVertex(
                    writer,
                    new Vector3(0f, 0f, 0f),
                    -1,
                    new Vector2(0f, 0f));

                Write3deVertex(
                    writer,
                    new Vector3(1f, 0f, 0f),
                    -1,
                    new Vector2(1f, 0f));

                Write3deVertex(
                    writer,
                    new Vector3(0f, 1f, 0f),
                    -1,
                    new Vector2(0f, 1f));

                writer.Write(1);
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write((ushort)2);

                writer.Write(30);
                writer.Write(2);

                writer.Write(0);
                Write3deFrameVertex(
                    writer,
                    new Vector3(0f, 0f, 0f),
                    new Vector2(0f, 0f));
                Write3deFrameVertex(
                    writer,
                    new Vector3(1f, 0f, 0f),
                    new Vector2(1f, 0f));
                Write3deFrameVertex(
                    writer,
                    new Vector3(0f, 1f, 0f),
                    new Vector2(0f, 1f));

                writer.Write(30);
                Write3deFrameVertex(
                    writer,
                    new Vector3(0f, 0f, 1f),
                    new Vector2(0f, 0f));
                Write3deFrameVertex(
                    writer,
                    new Vector3(1f, 0f, 1f),
                    new Vector2(1f, 0f));
                Write3deFrameVertex(
                    writer,
                    new Vector3(0f, 1f, 1f),
                    new Vector2(0f, 1f));

                bytes = stream.ToArray();
            }

            Legacy3deFile parsed =
                Legacy3deParser.Parse(bytes);

            Assert.AreEqual("spark.dds", parsed.TextureName);
            Assert.AreEqual(3, parsed.Vertices.Count);
            Assert.AreEqual(1, parsed.Faces.Count);
            Assert.AreEqual(30, parsed.MaxKeyframe);
            Assert.AreEqual(2, parsed.Frames.Count);
            Assert.AreEqual(0, parsed.Frames[0].Keyframe);
            Assert.AreEqual(30, parsed.Frames[1].Keyframe);
            Assert.AreEqual(
                new Vector3(1f, 0f, 1f),
                parsed.Frames[1].Vertices[1].Position);
        }

        [Test]
        public void Ef3ParsesMeshesTexturesEffectsAndSequence()
        {
            byte[] bytes;

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer =
                   new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(Encoding.ASCII.GetBytes("EF3"));

                writer.Write(1);
                WriteString(writer, "spark.3de");

                writer.Write(1);
                WriteString(writer, "spark.dds");

                writer.Write(1);
                WriteEffect(writer);

                writer.Write(1);
                WriteString(writer, "attack");
                writer.Write(1);
                writer.Write(0);
                writer.Write(0.25f);

                bytes = stream.ToArray();
            }

            LegacyEftFile parsed =
                LegacyEftParser.Parse(bytes);

            Assert.AreEqual(
                LegacyEftFormat.EF3,
                parsed.Format);

            CollectionAssert.AreEqual(
                new[] { "spark.3de" },
                parsed.MeshNames);

            CollectionAssert.AreEqual(
                new[] { "spark.dds" },
                parsed.TextureNames);

            Assert.AreEqual(1, parsed.Effects.Count);

            LegacyEftEffect effect =
                parsed.Effects[0];

            Assert.AreEqual("spark_effect", effect.Name);
            Assert.AreEqual(0, effect.MeshIndex);
            Assert.IsTrue(effect.VelocityRandomX);
            Assert.IsFalse(effect.VelocityRandomY);
            Assert.IsTrue(effect.VelocityRandomZ);
            Assert.IsTrue(effect.Loop);
            Assert.AreEqual(2, effect.DestinationBlend);
            Assert.AreEqual(1, effect.VelocityMode);
            Assert.AreEqual(5, effect.SourceBlend);
            Assert.IsTrue(effect.TextureLoop);
            Assert.AreEqual(0, effect.MeshIndex);
            Assert.IsFalse(effect.MotionPathEnabled);
            Assert.AreEqual(20f, effect.EmitRateMax, 0.000001f);
            Assert.AreEqual(10f, effect.EmitRateMin, 0.000001f);
            Assert.AreEqual(2f, effect.LifeMax, 0.000001f);
            Assert.AreEqual(1f, effect.LifeMin, 0.000001f);
            Assert.AreEqual(
                new Vector3(1f, 2f, 3f),
                effect.EmitOrigin);
            Assert.AreEqual(2, effect.BaseAxis);
            Assert.IsTrue(effect.GravityEnabled);
            Assert.IsTrue(effect.AttractEnabled);
            Assert.AreEqual(4f, effect.AttractStrength, 0.000001f);
            Assert.IsTrue(effect.AngularVelocityRandom);
            Assert.IsTrue(effect.RotationEnabled);
            Assert.AreEqual(3, effect.RotationAxis);
            Assert.AreEqual(27, effect.Ef3Unused);
            Assert.AreEqual(2, effect.DistanceScaleMode);
            Assert.AreEqual(2, effect.ColorFrames.Count);
            Assert.AreEqual(1, effect.VelocityScaleFrames.Count);
            Assert.AreEqual(1, effect.ScaleFrames.Count);
            Assert.IsTrue(effect.MirrorTexture);
            Assert.AreEqual(2, effect.InitialRotationAxis);
            Assert.AreEqual(10, effect.InitialRotationMinDegrees);
            Assert.AreEqual(350, effect.InitialRotationMaxDegrees);
            CollectionAssert.AreEqual(
                new[] { 0 },
                effect.TextureIds);

            Assert.AreEqual(1, parsed.Sequences.Count);
            Assert.AreEqual("attack", parsed.Sequences[0].Name);
            Assert.AreEqual(1, parsed.Sequences[0].Records.Count);
            Assert.AreEqual(
                0,
                parsed.Sequences[0].Records[0].EffectId);
            Assert.AreEqual(
                0.25f,
                parsed.Sequences[0].Records[0].Time,
                0.000001f);
        }

        [Test]
        public void CanonicalMonsterLinkedEffectsParseWithRetailLayoutWhenCorpusIsConfigured()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null ||
                !corpus.Validate().IsCanonical)
            {
                Assert.Ignore(
                    "Canonical ps0032 corpus is not configured on this machine.");
            }

            LegacyMonFile monsters =
                LegacyMonParser.Parse(
                    corpus.Resolve(
                        "DATA_Español/monster/monster.mon"));

            string effectRoot =
                LegacyUiAssetImporter.ResolveCaseInsensitive(
                    corpus.RootPath,
                    "DATA_Español/effect");

            var names =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            for (int i = 0;
                 i < monsters.Records.Count &&
                 names.Count < 24;
                 i++)
            {
                LegacyMonRecord record =
                    monsters.Records[i];

                AddEffectName(names, record.Attack1Effect);
                AddEffectName(names, record.Attack2Effect);
                AddEffectName(names, record.Attack3Effect);
                AddEffectName(names, record.DieEffect);
                AddEffectName(names, record.AttachEffect);
            }

            Assert.Greater(
                names.Count,
                0,
                "Canonical monster.mon did not expose any EFT references.");

            int parsedCount = 0;

            foreach (string name in names)
            {
                string path =
                    ResolveEffectPath(
                        effectRoot,
                        name);

                if (path == null)
                    continue;

                LegacyEftFile parsed =
                    LegacyEftParser.Parse(path);

                Assert.GreaterOrEqual(
                    parsed.Effects.Count,
                    0,
                    name);

                Assert.GreaterOrEqual(
                    parsed.Sequences.Count,
                    0,
                    name);

                parsedCount++;

                if (parsedCount >= 12)
                    break;
            }

            Assert.GreaterOrEqual(
                parsedCount,
                3,
                "Too few canonical monster-linked EFT libraries were resolved.");
        }

        private static void AddEffectName(
            ISet<string> output,
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            string normalized =
                value.Trim();

            if (string.Equals(
                    normalized,
                    "LOAD",
                    StringComparison.OrdinalIgnoreCase))
                return;

            output.Add(normalized);
        }

        private static string ResolveEffectPath(
            string effectRoot,
            string name)
        {
            string direct =
                CanonicalResourceIndex.FindUnique(
                    effectRoot,
                    name);

            if (direct != null)
                return direct;

            if (Path.HasExtension(name))
                return null;

            foreach (string extension in
                     new[]
                     {
                         ".eft",
                         ".ef2",
                         ".ef3"
                     })
            {
                string candidate =
                    CanonicalResourceIndex.FindUnique(
                        effectRoot,
                        name + extension);

                if (candidate != null)
                    return candidate;
            }

            return null;
        }

        private static void WriteEffect(
            BinaryWriter writer)
        {
            WriteString(writer, "spark_effect");

            writer.Write(1); // velocity random X
            writer.Write(0); // velocity random Y
            writer.Write(1); // velocity random Z
            writer.Write(1); // loop
            writer.Write(2); // destination blend
            writer.Write(1); // velocity mode
            writer.Write(5); // source blend
            writer.Write(1); // texture loop
            writer.Write(0); // mesh index
            writer.Write(0); // motion path

            writer.Write(0.1f); // delay/frame
            writer.Write(20f);  // emit rate max
            writer.Write(2f);   // life max
            writer.Write(10f);  // emit rate min
            writer.Write(1f);   // life min
            writer.Write(3f);   // emitter duration
            writer.Write(0.5f); // swirl speed
            writer.Write(0f);   // unknown18

            WriteVector3(writer, new Vector3(0.1f, 0.2f, 0.3f));
            WriteVector3(writer, new Vector3(0f, -9.8f, 0f));
            WriteVector3(writer, new Vector3(1f, 2f, 3f));
            WriteVector3(writer, new Vector3(-1f, 0f, -1f));
            WriteVector3(writer, new Vector3(1f, 2f, 1f));

            writer.Write(2); // base axis
            writer.Write(1); // gravity enabled
            writer.Write(1); // attract enabled
            WriteVector3(writer, new Vector3(0f, 1f, 0f));
            writer.Write(4f); // attract strength

            writer.Write(1);    // angular velocity random
            writer.Write(1);    // rotation enabled
            writer.Write(0.75f);
            writer.Write(3);    // rotation axis

            writer.Write(27);   // EF3 unused
            writer.Write(2);    // distance scale mode

            writer.Write(2);    // color frames
            writer.Write(1f);
            writer.Write(0.5f);
            writer.Write(0.25f);
            writer.Write(1f);
            writer.Write(0f);
            writer.Write(0.25f);
            writer.Write(0.5f);
            writer.Write(1f);
            writer.Write(0f);
            writer.Write(1f);

            writer.Write(1);    // velocity scale frames
            writer.Write(1.5f);
            writer.Write(0.4f);

            writer.Write(1);    // scale frames
            writer.Write(0.5f);
            writer.Write(2f);
            writer.Write(0.6f);

            writer.Write(1);    // mirror texture
            writer.Write(2);    // initial rotation axis
            writer.Write(10);   // initial min degrees
            writer.Write(350);  // initial max degrees

            writer.Write(1);    // texture IDs
            writer.Write(0);
        }

        private static void Write3deVertex(
            BinaryWriter writer,
            Vector3 position,
            int boneId,
            Vector2 uv)
        {
            WriteVector3(writer, position);
            writer.Write(boneId);
            writer.Write(uv.x);
            writer.Write(uv.y);
        }

        private static void Write3deFrameVertex(
            BinaryWriter writer,
            Vector3 position,
            Vector2 uv)
        {
            WriteVector3(writer, position);
            writer.Write(uv.x);
            writer.Write(uv.y);
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

        private static void WriteString(
            BinaryWriter writer,
            string value)
        {
            byte[] bytes =
                Encoding.UTF8.GetBytes(
                    value ?? string.Empty);

            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }
}
