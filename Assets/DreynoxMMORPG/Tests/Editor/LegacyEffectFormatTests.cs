using System;
using System.IO;
using System.Text;
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
            Assert.AreEqual(
                new Vector3(1f, 2f, 3f),
                effect.Position);
            Assert.AreEqual(1, effect.Rotations.Count);
            Assert.AreEqual(0.5f, effect.Rotations[0].Time, 0.000001f);
            Assert.AreEqual(2, effect.OpacityFrames.Count);
            Assert.AreEqual(1, effect.Sub3.Count);
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

        private static void WriteEffect(
            BinaryWriter writer)
        {
            WriteString(writer, "spark_effect");

            for (int i = 1; i <= 8; i++)
                writer.Write(i);

            writer.Write(0);  // MeshIndex
            writer.Write(10);

            for (int i = 11; i <= 18; i++)
                writer.Write((float)i);

            WriteVector3(writer, new Vector3(0.1f, 0.2f, 0.3f));
            WriteVector3(writer, new Vector3(0.4f, 0.5f, 0.6f));
            WriteVector3(writer, new Vector3(1f, 2f, 3f));
            WriteVector3(writer, new Vector3(0.7f, 0.8f, 0.9f));
            WriteVector3(writer, new Vector3(1.1f, 1.2f, 1.3f));

            writer.Write(19);
            writer.Write(20);
            writer.Write(21);

            WriteVector3(writer, new Vector3(1.4f, 1.5f, 1.6f));

            writer.Write(22f);
            writer.Write(23);
            writer.Write(24);
            writer.Write(25f);
            writer.Write(26);

            writer.Write(27f);
            writer.Write(28f);

            writer.Write(1);
            WriteQuaternion(
                writer,
                Quaternion.identity);
            writer.Write(0.5f);

            writer.Write(2);
            writer.Write(1f);
            writer.Write(0f);
            writer.Write(0f);
            writer.Write(1f);

            writer.Write(1);
            writer.Write(3f);
            writer.Write(4f);
            writer.Write(0.75f);

            writer.Write(29);
            writer.Write(30);
            writer.Write(31);
            writer.Write(32);

            writer.Write(1);
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
