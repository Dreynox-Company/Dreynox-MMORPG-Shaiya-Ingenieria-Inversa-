using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dreynox.Mmorpg.LocalData
{
    /// <summary>Shared native mesh construction for Editor baking and standalone local-folder loading.</summary>
    public static class LegacyRuntimeSkinnedBuilder
    {
        public static Transform[] BuildSkeleton(Transform actorRoot, Legacy3dcFile mesh, LegacyAniFile ani)
        {
            if (actorRoot == null) throw new ArgumentNullException(nameof(actorRoot));
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            if (ani == null) throw new ArgumentNullException(nameof(ani));
            int count = ani.Bones.Count;
            if (count == 0 || mesh.InverseBindMatrices.Count < count)
                throw new InvalidDataException("Mesh/ANI skeleton size mismatch.");
            ValidateMeshForSkeleton(mesh, count);
            var bones = new Transform[count];
            var bindWorld = new Matrix4x4[count];
            var skeleton = new GameObject("Skeleton");
            skeleton.transform.SetParent(actorRoot, false);
            for (int i = 0; i < count; i++)
            {
                if (Mathf.Abs(mesh.InverseBindMatrices[i].determinant) < 1e-8f)
                    throw new InvalidDataException("Singular inverse bind matrix at bone " + i);
                bindWorld[i] = LegacyCoordinateBridge.Matrix(mesh.InverseBindMatrices[i].inverse);
            }
            for (int i = 0; i < count; i++)
            {
                int parentIndex = ani.Bones[i].ParentBoneIndex;
                if (parentIndex < -1 || parentIndex >= i)
                    throw new InvalidDataException("ANI hierarchy must be parent-before-child. Bone " + i + " parent=" + parentIndex + ".");
                var bone = new GameObject("Bone_" + i.ToString("D3"));
                Transform parent = parentIndex < 0 ? skeleton.transform : bones[parentIndex];
                if (parent == null) throw new InvalidDataException("Missing parent for bone " + i);
                bone.transform.SetParent(parent, false);
                Matrix4x4 local = parentIndex < 0 ? bindWorld[i] : bindWorld[parentIndex].inverse * bindWorld[i];
                ApplyLocalMatrix(bone.transform, local);
                bones[i] = bone.transform;
            }
            return bones;
        }

        public static Mesh BuildMesh(Legacy3dcFile source, Transform[] bones, Transform actorRoot, string meshName)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (bones == null) throw new ArgumentNullException(nameof(bones));
            if (actorRoot == null) throw new ArgumentNullException(nameof(actorRoot));
            ValidateMeshForSkeleton(source, bones.Length);
            int count = source.Vertices.Count;
            var positions = new Vector3[count];
            var normals = new Vector3[count];
            var uvs = new Vector2[count];
            var weights = new BoneWeight[count];
            for (int i = 0; i < count; i++)
            {
                Legacy3dcVertex vertex = source.Vertices[i];
                positions[i] = LegacyCoordinateBridge.Position(vertex.Position);
                normals[i] = LegacyCoordinateBridge.Direction(vertex.Normal).normalized;
                uvs[i] = vertex.UV;
                weights[i] = BuildBoneWeight(vertex);
            }
            var triangles = new int[source.Faces.Count * 3];
            for (int i = 0; i < source.Faces.Count; i++)
            {
                LegacyTriangle face = source.Faces[i];
                triangles[i * 3] = face.A;
                triangles[i * 3 + 1] = face.C;
                triangles[i * 3 + 2] = face.B;
            }
            var bindPoses = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                // Keep every piece's authored inverse bind pose, not the torso's.
                bindPoses[i] = i < source.InverseBindMatrices.Count
                    ? LegacyCoordinateBridge.Matrix(source.InverseBindMatrices[i])
                    : bones[i].worldToLocalMatrix * actorRoot.localToWorldMatrix;
            }
            var mesh = new Mesh
            {
                name = string.IsNullOrWhiteSpace(meshName) ? "Legacy3DC" : meshName,
                indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.boneWeights = weights;
            mesh.bindposes = bindPoses;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void ApplyLocalMatrix(Transform transform, Matrix4x4 local)
        {
            transform.localPosition = new Vector3(local.m03, local.m13, local.m23);
            transform.localRotation = local.rotation.normalized;
            transform.localScale = new Vector3(
                new Vector3(local.m00, local.m10, local.m20).magnitude,
                new Vector3(local.m01, local.m11, local.m21).magnitude,
                new Vector3(local.m02, local.m12, local.m22).magnitude);
        }

        private static BoneWeight BuildBoneWeight(Legacy3dcVertex vertex)
        {
            var merged = new Dictionary<int, float>();
            AddWeight(merged, vertex.Bone1, vertex.Weight1);
            AddWeight(merged, vertex.Bone2, vertex.Weight2);
            AddWeight(merged, vertex.Bone3, vertex.Weight3);
            AddWeight(merged, vertex.Bone4, vertex.Weight4);
            List<KeyValuePair<int, float>> ordered = merged.Where(p => p.Value > 0.000001f)
                .OrderByDescending(p => p.Value).Take(4).ToList();
            if (ordered.Count == 0) throw new InvalidDataException("3DC vertex contains no effective bone weight.");
            float sum = ordered.Sum(p => p.Value);
            var result = new BoneWeight();
            for (int slot = 0; slot < ordered.Count; slot++)
            {
                int bone = ordered[slot].Key;
                float weight = ordered[slot].Value / sum;
                switch (slot)
                {
                    case 0: result.boneIndex0 = bone; result.weight0 = weight; break;
                    case 1: result.boneIndex1 = bone; result.weight1 = weight; break;
                    case 2: result.boneIndex2 = bone; result.weight2 = weight; break;
                    case 3: result.boneIndex3 = bone; result.weight3 = weight; break;
                }
            }
            return result;
        }
        private static void AddWeight(Dictionary<int, float> merged, int bone, float weight)
        {
            if (weight <= 0.000001f) return;
            merged.TryGetValue(bone, out float existing);
            merged[bone] = existing + weight;
        }
        public static void ValidateMeshForSkeleton(Legacy3dcFile source, int boneCount)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (boneCount < 1) throw new InvalidDataException("The animation has no skeleton.");
            for (int i = 0; i < source.Vertices.Count; i++)
            {
                var v = source.Vertices[i];
                ValidateInfluence(v.Bone1, v.Weight1, source.InverseBindMatrices.Count, boneCount, i);
                ValidateInfluence(v.Bone2, v.Weight2, source.InverseBindMatrices.Count, boneCount, i);
                ValidateInfluence(v.Bone3, v.Weight3, source.InverseBindMatrices.Count, boneCount, i);
                ValidateInfluence(v.Bone4, v.Weight4, source.InverseBindMatrices.Count, boneCount, i);
            }
        }
        private static void ValidateInfluence(int index, float weight, int sourceCount, int targetCount, int vertex)
        {
            if (float.IsNaN(weight) || float.IsInfinity(weight) || weight < 0f || weight > 1.001f)
                throw new InvalidDataException("Invalid weight at vertex " + vertex);
            if (weight <= 0.000001f) return;
            if (index >= sourceCount || index >= targetCount)
                throw new InvalidDataException("Weighted bone " + index + " at vertex " + vertex + " is not in the ANI skeleton.");
        }
    }
}
