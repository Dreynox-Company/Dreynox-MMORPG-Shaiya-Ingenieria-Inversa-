using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public sealed class Legacy3deFile
    {
        public string TextureName { get; internal set; } = string.Empty;
        public List<Legacy3deVertex> Vertices { get; } =
            new List<Legacy3deVertex>();
        public List<LegacyTriangle> Faces { get; } =
            new List<LegacyTriangle>();
        public int MaxKeyframe { get; internal set; }
        public List<Legacy3deFrame> Frames { get; } =
            new List<Legacy3deFrame>();
    }

    public struct Legacy3deVertex
    {
        public Vector3 Position;
        public int BoneId;
        public Vector2 UV;
    }

    public struct Legacy3deVertexFrame
    {
        public Vector3 Position;
        public Vector2 UV;
    }

    public sealed class Legacy3deFrame
    {
        public int Keyframe;
        public List<Legacy3deVertexFrame> Vertices { get; } =
            new List<Legacy3deVertexFrame>();
    }

    public static class Legacy3deParser
    {
        private const int MaxStringBytes = 4096;
        private const int MaxVertices = 2_000_000;
        private const int MaxFaces = 2_000_000;
        private const int MaxFrames = 100_000;

        public static Legacy3deFile Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException(
                    "3DE path is required.",
                    nameof(path));

            using (FileStream stream = File.OpenRead(path))
            using (BinaryReader reader =
                   new BinaryReader(stream, Encoding.UTF8))
                return Parse(reader);
        }

        public static Legacy3deFile Parse(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            using (MemoryStream stream =
                   new MemoryStream(bytes, false))
            using (BinaryReader reader =
                   new BinaryReader(stream, Encoding.UTF8))
                return Parse(reader);
        }

        private static Legacy3deFile Parse(
            BinaryReader reader)
        {
            var result =
                new Legacy3deFile
                {
                    TextureName =
                        ReadString(reader)
                };

            int vertexCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "3DE vertex",
                    MaxVertices);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked((long)vertexCount * 24L + 4L));

            for (int i = 0; i < vertexCount; i++)
            {
                result.Vertices.Add(
                    new Legacy3deVertex
                    {
                        Position =
                            LegacyFormatPrimitives
                                .ReadVector3(reader),
                        BoneId =
                            reader.ReadInt32(),
                        UV =
                            LegacyFormatPrimitives
                                .ReadVector2(reader)
                    });
            }

            int faceCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "3DE face",
                    MaxFaces);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked((long)faceCount * 6L + 8L));

            for (int i = 0; i < faceCount; i++)
            {
                LegacyTriangle face =
                    new LegacyTriangle
                    {
                        A = reader.ReadUInt16(),
                        B = reader.ReadUInt16(),
                        C = reader.ReadUInt16()
                    };

                if (face.A >= vertexCount ||
                    face.B >= vertexCount ||
                    face.C >= vertexCount)
                {
                    throw new InvalidDataException(
                        "3DE face " + i +
                        " references a vertex outside the mesh.");
                }

                result.Faces.Add(face);
            }

            result.MaxKeyframe =
                reader.ReadInt32();

            if (result.MaxKeyframe < 0)
            {
                throw new InvalidDataException(
                    "3DE max keyframe is negative.");
            }

            int frameCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "3DE frame",
                    MaxFrames);

            long frameBytes =
                checked(
                    (long)frameCount *
                    (4L + (long)vertexCount * 20L));

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                frameBytes);

            int previousKey = -1;

            for (int frameIndex = 0;
                 frameIndex < frameCount;
                 frameIndex++)
            {
                int key =
                    reader.ReadInt32();

                if (key < 0 ||
                    key > result.MaxKeyframe)
                {
                    throw new InvalidDataException(
                        "3DE frame " + frameIndex +
                        " has key " + key +
                        " outside 0.." +
                        result.MaxKeyframe + ".");
                }

                if (key < previousKey)
                {
                    throw new InvalidDataException(
                        "3DE frame keys are not sorted.");
                }

                previousKey = key;

                var frame =
                    new Legacy3deFrame
                    {
                        Keyframe = key
                    };

                for (int vertexIndex = 0;
                     vertexIndex < vertexCount;
                     vertexIndex++)
                {
                    frame.Vertices.Add(
                        new Legacy3deVertexFrame
                        {
                            Position =
                                LegacyFormatPrimitives
                                    .ReadVector3(reader),
                            UV =
                                LegacyFormatPrimitives
                                    .ReadVector2(reader)
                        });
                }

                result.Frames.Add(frame);
            }

            LegacyFormatPrimitives.EnsureFullyConsumed(
                reader,
                "3DE");

            return result;
        }

        private static string ReadString(
            BinaryReader reader)
        {
            int length =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "3DE string",
                    MaxStringBytes);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                length);

            if (length == 0)
                return string.Empty;

            return Encoding.UTF8
                .GetString(reader.ReadBytes(length))
                .TrimEnd('\0');
        }
    }
}
