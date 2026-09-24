using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public enum LegacyDgCollisionType
    {
        Transparent = 0,
        Collision = 1
    }

    public sealed class LegacyDgFile
    {
        public LegacyBounds BoundingBox;
        public List<string> TextureNames { get; } =
            new List<string>();
        public int LightmapCount;
        public LegacyDgNode RootNode;

        public int NodeCount { get; internal set; }
        public int MeshGroupCount { get; internal set; }
        public int MeshCount { get; internal set; }
        public int VertexCount { get; internal set; }
        public int FaceCount { get; internal set; }
        public int CollisionVertexCount { get; internal set; }
        public int CollisionFaceCount { get; internal set; }
    }

    public sealed class LegacyDgNode
    {
        public Vector3 Center;
        public LegacyBounds ViewBox;
        public LegacyBounds CollisionBox;
        public List<LegacyDgMeshGroup> MeshGroups { get; } =
            new List<LegacyDgMeshGroup>();
        public LegacyDgCollisionType CollisionType;
        public LegacyDgCollisionMesh CollisionMesh;
        public List<LegacyDgNode> Children { get; } =
            new List<LegacyDgNode>();
    }

    public sealed class LegacyDgMeshGroup
    {
        public int TextureIndex;
        public List<LegacyDgMesh> Meshes { get; } =
            new List<LegacyDgMesh>();
    }

    public sealed class LegacyDgMesh
    {
        public int LightmapIndex;
        public List<LegacyDgMeshVertex> Vertices { get; } =
            new List<LegacyDgMeshVertex>();
        public List<LegacyTriangle> Faces { get; } =
            new List<LegacyTriangle>();
    }

    public struct LegacyDgMeshVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public int BoneId;
        public Vector2 UV;
        public Vector2 LightmapUV;
    }

    public sealed class LegacyDgCollisionMesh
    {
        public List<Vector3> Vertices { get; } =
            new List<Vector3>();
        public List<LegacyTriangle> Faces { get; } =
            new List<LegacyTriangle>();
    }

    public static class LegacyDgParser
    {
        private const int FixedStringBytes = 256;
        private const int MaxTextures = 4096;
        private const int MaxLightmaps = 4096;
        private const int MaxNodes = 2_000_000;
        private const int MaxDepth = 32;
        private const int MaxMeshGroups = 2_000_000;
        private const int MaxMeshes = 2_000_000;
        private const int MaxVertices = 20_000_000;
        private const int MaxFaces = 20_000_000;

        public static LegacyDgFile Parse(
            string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException(
                    "DG path is required.",
                    nameof(path));

            using (FileStream stream =
                   File.OpenRead(path))
            using (BinaryReader reader =
                   new BinaryReader(stream))
            {
                return Parse(reader);
            }
        }

        public static LegacyDgFile Parse(
            byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(
                    nameof(bytes));

            using (MemoryStream stream =
                   new MemoryStream(bytes, false))
            using (BinaryReader reader =
                   new BinaryReader(stream))
            {
                return Parse(reader);
            }
        }

        private static LegacyDgFile Parse(
            BinaryReader reader)
        {
            var result =
                new LegacyDgFile
                {
                    BoundingBox =
                        ReadBounds(reader)
                };

            int textureCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "DG texture",
                    MaxTextures);

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked(
                    (long)textureCount *
                    FixedStringBytes +
                    8L));

            for (int i = 0;
                 i < textureCount;
                 i++)
            {
                string name =
                    ReadFixedAscii(reader);

                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new InvalidDataException(
                        "DG texture " +
                        i +
                        " has an empty name.");
                }

                result.TextureNames.Add(name);
            }

            result.LightmapCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "DG lightmap",
                    MaxLightmaps);

            int rootPresent =
                reader.ReadInt32();

            if (rootPresent < 0)
            {
                throw new InvalidDataException(
                    "DG root node flag is negative.");
            }

            int nodesRead = 0;

            if (rootPresent > 0)
            {
                result.RootNode =
                    ReadNode(
                        reader,
                        result,
                        0,
                        ref nodesRead);
            }

            LegacyFormatPrimitives
                .EnsureFullyConsumed(
                    reader,
                    "DG");

            if (result.RootNode == null)
            {
                throw new InvalidDataException(
                    "DG contains no root node.");
            }

            return result;
        }

        private static LegacyDgNode ReadNode(
            BinaryReader reader,
            LegacyDgFile file,
            int depth,
            ref int nodesRead)
        {
            if (depth > MaxDepth)
                throw new InvalidDataException(
                    "DG node hierarchy exceeds " +
                    MaxDepth +
                    " levels.");

            nodesRead++;

            if (nodesRead > MaxNodes)
                throw new InvalidDataException(
                    "DG node count exceeds " +
                    MaxNodes +
                    ".");

            file.NodeCount++;

            var node =
                new LegacyDgNode
                {
                    Center =
                        LegacyFormatPrimitives
                            .ReadVector3(reader),
                    ViewBox =
                        ReadBounds(reader),
                    CollisionBox =
                        ReadBounds(reader)
                };

            int groupCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "DG mesh group",
                    MaxMeshGroups);

            file.MeshGroupCount +=
                groupCount;

            for (int i = 0;
                 i < groupCount;
                 i++)
            {
                node.MeshGroups.Add(
                    ReadMeshGroup(
                        reader,
                        file));
            }

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                4);

            int collisionType =
                reader.ReadInt32();

            if (collisionType < 0 ||
                collisionType > 1)
            {
                throw new InvalidDataException(
                    "DG node collision type is invalid: " +
                    collisionType +
                    ".");
            }

            node.CollisionType =
                (LegacyDgCollisionType)
                collisionType;

            if (node.CollisionType ==
                LegacyDgCollisionType.Collision)
            {
                node.CollisionMesh =
                    ReadCollisionMesh(
                        reader,
                        file);
            }

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                8L * 4L);

            for (int child = 0;
                 child < 8;
                 child++)
            {
                int present =
                    reader.ReadInt32();

                if (present < 0)
                {
                    throw new InvalidDataException(
                        "DG child flag is negative.");
                }

                if (present > 0)
                {
                    node.Children.Add(
                        ReadNode(
                            reader,
                            file,
                            depth + 1,
                            ref nodesRead));
                }
            }

            return node;
        }

        private static LegacyDgMeshGroup ReadMeshGroup(
            BinaryReader reader,
            LegacyDgFile file)
        {
            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                8);

            int textureIndex =
                reader.ReadInt32();

            if (textureIndex < 0 ||
                textureIndex >=
                file.TextureNames.Count)
            {
                throw new InvalidDataException(
                    "DG mesh group texture index " +
                    textureIndex +
                    " is outside 0.." +
                    (file.TextureNames.Count - 1) +
                    ".");
            }

            int meshCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "DG mesh",
                    MaxMeshes);

            file.MeshCount +=
                meshCount;

            var group =
                new LegacyDgMeshGroup
                {
                    TextureIndex =
                        textureIndex
                };

            for (int i = 0;
                 i < meshCount;
                 i++)
            {
                group.Meshes.Add(
                    ReadMesh(
                        reader,
                        file));
            }

            return group;
        }

        private static LegacyDgMesh ReadMesh(
            BinaryReader reader,
            LegacyDgFile file)
        {
            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                8);

            int lightmapIndex =
                reader.ReadInt32();

            if (lightmapIndex < -1 ||
                lightmapIndex >=
                file.LightmapCount)
            {
                throw new InvalidDataException(
                    "DG mesh lightmap index " +
                    lightmapIndex +
                    " is invalid for " +
                    file.LightmapCount +
                    " lightmaps.");
            }

            int vertexCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "DG vertex",
                    MaxVertices);

            var mesh =
                new LegacyDgMesh
                {
                    LightmapIndex =
                        lightmapIndex
                };

            file.VertexCount +=
                vertexCount;

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked(
                    (long)vertexCount *
                    44L));

            for (int i = 0;
                 i < vertexCount;
                 i++)
            {
                mesh.Vertices.Add(
                    new LegacyDgMeshVertex
                    {
                        Position =
                            LegacyFormatPrimitives
                                .ReadVector3(reader),
                        Normal =
                            LegacyFormatPrimitives
                                .ReadVector3(reader),
                        BoneId =
                            reader.ReadInt32(),
                        UV =
                            LegacyFormatPrimitives
                                .ReadVector2(reader),
                        LightmapUV =
                            LegacyFormatPrimitives
                                .ReadVector2(reader)
                    });
            }

            int faceCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "DG face",
                    MaxFaces);

            file.FaceCount +=
                faceCount;

            ReadFaces(
                reader,
                faceCount,
                vertexCount,
                mesh.Faces,
                "DG");

            return mesh;
        }

        private static LegacyDgCollisionMesh ReadCollisionMesh(
            BinaryReader reader,
            LegacyDgFile file)
        {
            int vertexCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "DG collision vertex",
                    MaxVertices);

            file.CollisionVertexCount +=
                vertexCount;

            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked(
                    (long)vertexCount *
                    12L));

            var mesh =
                new LegacyDgCollisionMesh();

            for (int i = 0;
                 i < vertexCount;
                 i++)
            {
                mesh.Vertices.Add(
                    LegacyFormatPrimitives
                        .ReadVector3(reader));
            }

            int faceCount =
                LegacyFormatPrimitives.ReadCount(
                    reader,
                    "DG collision face",
                    MaxFaces);

            file.CollisionFaceCount +=
                faceCount;

            ReadFaces(
                reader,
                faceCount,
                vertexCount,
                mesh.Faces,
                "DG collision");

            return mesh;
        }

        private static void ReadFaces(
            BinaryReader reader,
            int faceCount,
            int vertexCount,
            ICollection<LegacyTriangle> target,
            string label)
        {
            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                checked(
                    (long)faceCount *
                    6L));

            for (int i = 0;
                 i < faceCount;
                 i++)
            {
                ushort a =
                    reader.ReadUInt16();

                ushort b =
                    reader.ReadUInt16();

                ushort c =
                    reader.ReadUInt16();

                if (a >= vertexCount ||
                    b >= vertexCount ||
                    c >= vertexCount)
                {
                    throw new InvalidDataException(
                        label +
                        " face " +
                        i +
                        " references vertex outside 0.." +
                        (vertexCount - 1) +
                        ".");
                }

                target.Add(
                    new LegacyTriangle
                    {
                        A = a,
                        B = b,
                        C = c
                    });
            }
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

        private static string ReadFixedAscii(
            BinaryReader reader)
        {
            LegacyFormatPrimitives.EnsureRemaining(
                reader,
                FixedStringBytes);

            byte[] bytes =
                reader.ReadBytes(
                    FixedStringBytes);

            int length =
                Array.IndexOf(
                    bytes,
                    (byte)0);

            if (length < 0)
                length =
                    bytes.Length;

            return Encoding.ASCII
                .GetString(
                    bytes,
                    0,
                    length)
                .Trim();
        }
    }
}
