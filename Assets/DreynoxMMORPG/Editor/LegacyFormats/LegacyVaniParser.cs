using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public sealed class LegacyVaniFile
    {
        public Vector3 Center;
        public float Radius;
        public LegacyBounds BoundingBox;
        public int FrameCount;
        public int Unknown1;
        public List<LegacyVaniMesh> Meshes { get; } =
            new List<LegacyVaniMesh>();
        public LegacyBounds BoundingBox2;
        public int Unknown2;

        public int TotalVertexCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Meshes.Count; i++)
                    total += Meshes[i].Vertices.Count;
                return total;
            }
        }

        public int TotalFaceCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Meshes.Count; i++)
                    total += Meshes[i].Faces.Count;
                return total;
            }
        }
    }

    public sealed class LegacyVaniMesh
    {
        public string TextureName = string.Empty;
        public List<LegacyTriangle> Faces { get; } =
            new List<LegacyTriangle>();
        public List<LegacyVaniVertex> Vertices { get; } =
            new List<LegacyVaniVertex>();
    }

    public sealed class LegacyVaniVertex
    {
        public List<LegacyVaniVertexFrame> Frames { get; } =
            new List<LegacyVaniVertexFrame>();
    }

    public struct LegacyVaniVertexFrame
    {
        public Vector3 Position;
        public Vector3 Normal;
        public int BoneId;
        public Vector2 UV;
    }

    public static class LegacyVaniParser
    {
        private const int MaxMeshes = 4096;
        private const int MaxFrames = 10000;
        private const int MaxVerticesPerMesh = 2_000_000;
        private const int MaxFacesPerMesh = 5_000_000;
        private const int MaxTextureNameBytes = 1024;

        public static LegacyVaniFile Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException(
                    "VANI path is required.",
                    nameof(path));

            using (FileStream stream = File.OpenRead(path))
            using (BinaryReader reader = new BinaryReader(stream))
                return Parse(reader);
        }

        public static LegacyVaniFile Parse(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            using (MemoryStream stream = new MemoryStream(bytes, false))
            using (BinaryReader reader = new BinaryReader(stream))
                return Parse(reader);
        }

        private static LegacyVaniFile Parse(BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(reader, 52);

            var result = new LegacyVaniFile
            {
                Center = LegacyFormatPrimitives.ReadVector3(reader),
                Radius = LegacyFormatPrimitives.ReadFiniteSingle(reader),
                BoundingBox = ReadBounds(reader)
            };

            if (result.Radius < 0f)
                throw new InvalidDataException(
                    "VANI radius cannot be negative.");

            int meshCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "VANI mesh",
                    MaxMeshes);

            result.FrameCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "VANI frame",
                    MaxFrames);

            LegacyFormatPrimitives.EnsureRemaining(reader, 4);
            result.Unknown1 = reader.ReadInt32();

            if (result.FrameCount <= 0)
                throw new InvalidDataException(
                    "VANI must contain at least one frame.");

            for (int meshIndex = 0;
                 meshIndex < meshCount;
                 meshIndex++)
            {
                result.Meshes.Add(
                    ReadMesh(
                        reader,
                        result.FrameCount,
                        meshIndex));
            }

            result.BoundingBox2 =
                ReadBounds(reader);

            LegacyFormatPrimitives.EnsureRemaining(reader, 4);
            result.Unknown2 = reader.ReadInt32();

            LegacyFormatPrimitives.EnsureFullyConsumed(
                reader,
                "VANI");

            return result;
        }

        private static LegacyVaniMesh ReadMesh(
            BinaryReader reader,
            int frameCount,
            int meshIndex)
        {
            string textureName =
                ReadLengthPrefixedAscii(
                    reader,
                    "VANI mesh texture",
                    MaxTextureNameBytes);

            if (string.IsNullOrWhiteSpace(textureName))
                throw new InvalidDataException(
                    "VANI mesh " + meshIndex +
                    " has no texture name.");

            int faceCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "VANI face",
                    MaxFacesPerMesh);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked((long)faceCount * 6L + 4L));

            var mesh = new LegacyVaniMesh
            {
                TextureName = textureName
            };

            for (int i = 0; i < faceCount; i++)
            {
                mesh.Faces.Add(
                    new LegacyTriangle
                    {
                        A = reader.ReadUInt16(),
                        B = reader.ReadUInt16(),
                        C = reader.ReadUInt16()
                    });
            }

            int vertexCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "VANI vertex",
                    MaxVerticesPerMesh);

            long frameBytes =
                checked(
                    (long)frameCount *
                    vertexCount *
                    36L);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                frameBytes);

            for (int i = 0; i < vertexCount; i++)
                mesh.Vertices.Add(
                    new LegacyVaniVertex());

            for (int frame = 0;
                 frame < frameCount;
                 frame++)
            {
                for (int vertexIndex = 0;
                     vertexIndex < vertexCount;
                     vertexIndex++)
                {
                    LegacyVaniVertexFrame value =
                        new LegacyVaniVertexFrame
                        {
                            Position =
                                LegacyFormatPrimitives
                                    .ReadVector3(reader),
                            Normal =
                                LegacyFormatPrimitives
                                    .ReadVector3(reader)
                        };

                    LegacyFormatPrimitives.EnsureRemaining(
                        reader,
                        4);

                    value.BoneId =
                        reader.ReadInt32();

                    value.UV =
                        LegacyFormatPrimitives
                            .ReadVector2(reader);

                    mesh.Vertices[vertexIndex]
                        .Frames.Add(value);
                }
            }

            for (int i = 0; i < mesh.Faces.Count; i++)
            {
                LegacyTriangle face =
                    mesh.Faces[i];

                if (face.A >= vertexCount ||
                    face.B >= vertexCount ||
                    face.C >= vertexCount)
                {
                    throw new InvalidDataException(
                        "VANI mesh " + meshIndex +
                        " face " + i +
                        " references a vertex outside 0.." +
                        (vertexCount - 1) + ".");
                }
            }

            for (int i = 0;
                 i < mesh.Vertices.Count;
                 i++)
            {
                if (mesh.Vertices[i].Frames.Count !=
                    frameCount)
                {
                    throw new InvalidDataException(
                        "VANI mesh " + meshIndex +
                        " vertex " + i +
                        " frame count mismatch.");
                }
            }

            return mesh;
        }

        private static LegacyBounds ReadBounds(
            BinaryReader reader)
        {
            return new LegacyBounds
            {
                Lower =
                    LegacyFormatPrimitives
                        .ReadVector3(reader),
                Upper =
                    LegacyFormatPrimitives
                        .ReadVector3(reader)
            };
        }

        private static string ReadLengthPrefixedAscii(
            BinaryReader reader,
            string label,
            int maximum)
        {
            int length =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    label + " byte",
                    maximum);

            if (length == 0)
                return string.Empty;

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                length);

            byte[] bytes =
                reader.ReadBytes(length);

            int zero =
                Array.IndexOf(
                    bytes,
                    (byte)0);

            int count =
                zero >= 0
                    ? zero
                    : bytes.Length;

            return Encoding.ASCII
                .GetString(
                    bytes,
                    0,
                    count)
                .Trim();
        }
    }
}
