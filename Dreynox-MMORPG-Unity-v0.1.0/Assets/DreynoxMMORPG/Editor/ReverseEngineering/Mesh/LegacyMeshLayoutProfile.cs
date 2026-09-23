using UnityEngine;

namespace Dreynox.Mmorpg.Editor.ReverseEngineering.Meshes
{
    [CreateAssetMenu(menuName = "Dreynox MMORPG/Reverse Engineering/Legacy Mesh Layout", fileName = "LegacyMeshLayoutProfile")]
    public sealed class LegacyMeshLayoutProfile : ScriptableObject
    {
        [Header("Vertex data")]
        public long vertexCountOffset = -1;
        public int fixedVertexCount;
        public long vertexDataOffset;
        public int vertexStride = 12;
        public int positionXOffset;
        public int positionYOffset = 4;
        public int positionZOffset = 8;
        public float positionScale = 1f;
        public bool swapYAndZ;
        public bool negateX;
        public bool negateZ;

        [Header("UV data (optional)")]
        public bool hasUv;
        public int uvUOffset;
        public int uvVOffset = 4;
        public bool flipV = true;

        [Header("Index data")]
        public bool indexed = true;
        public long indexCountOffset = -1;
        public int fixedIndexCount;
        public long indexDataOffset;
        public bool indicesAre32Bit;

        public int ResolveVertexCount(System.IO.BinaryReader reader)
        {
            if (vertexCountOffset >= 0)
            {
                reader.BaseStream.Position = vertexCountOffset;
                return reader.ReadInt32();
            }
            return fixedVertexCount;
        }

        public int ResolveIndexCount(System.IO.BinaryReader reader)
        {
            if (!indexed) return 0;
            if (indexCountOffset >= 0)
            {
                reader.BaseStream.Position = indexCountOffset;
                return reader.ReadInt32();
            }
            return fixedIndexCount;
        }
    }
}
