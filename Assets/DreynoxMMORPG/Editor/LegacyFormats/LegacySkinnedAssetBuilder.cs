using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    internal static class LegacySkinnedAssetBuilder
    {
        public static Transform[] BuildSkeleton(
            Transform actorRoot,
            Legacy3dcFile mesh,
            LegacyAniFile ani)
        {
            if (actorRoot == null)
                throw new ArgumentNullException(nameof(actorRoot));
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));
            if (ani == null)
                throw new ArgumentNullException(nameof(ani));

            int boneCount = mesh.InverseBindMatrices.Count;

            if (boneCount == 0 ||
                boneCount != ani.Bones.Count)
            {
                throw new InvalidDataException(
                    "Mesh/ANI skeleton size mismatch.");
            }

            Transform[] bones = new Transform[boneCount];
            Matrix4x4[] bindWorld = new Matrix4x4[boneCount];

            GameObject skeletonObject =
                new GameObject("Skeleton");

            skeletonObject.transform.SetParent(
                actorRoot,
                false);

            for (int i = 0; i < boneCount; i++)
            {
                Matrix4x4 shaiyaWorld =
                    mesh.InverseBindMatrices[i].inverse;

                bindWorld[i] =
                    LegacyCoordinateBridge.Matrix(shaiyaWorld);
            }

            for (int i = 0; i < boneCount; i++)
            {
                int parentIndex =
                    ani.Bones[i].ParentBoneIndex;

                if (parentIndex >= i)
                {
                    throw new InvalidDataException(
                        "ANI hierarchy must be parent-before-child. Bone " +
                        i + " parent=" + parentIndex + ".");
                }

                GameObject boneObject =
                    new GameObject(
                        "Bone_" + i.ToString("D3"));

                Transform parent =
                    parentIndex < 0
                        ? skeletonObject.transform
                        : bones[parentIndex];

                if (parent == null)
                    throw new InvalidDataException(
                        "Missing parent for bone " + i + ".");

                boneObject.transform.SetParent(
                    parent,
                    false);

                Matrix4x4 local =
                    parentIndex < 0
                        ? bindWorld[i]
                        : bindWorld[parentIndex].inverse *
                          bindWorld[i];

                ApplyLocalMatrix(
                    boneObject.transform,
                    local);

                bones[i] = boneObject.transform;
            }

            return bones;
        }

        public static Mesh BuildMesh(
            Legacy3dcFile source,
            Transform[] bones,
            Transform actorRoot,
            string meshName)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (bones == null)
                throw new ArgumentNullException(nameof(bones));
            if (actorRoot == null)
                throw new ArgumentNullException(nameof(actorRoot));

            if (source.InverseBindMatrices.Count != bones.Length)
            {
                throw new InvalidDataException(
                    "3DC skeleton differs from the shared skeleton.");
            }

            int vertexCount = source.Vertices.Count;

            Vector3[] positions = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            BoneWeight[] weights = new BoneWeight[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                Legacy3dcVertex vertex = source.Vertices[i];

                positions[i] =
                    LegacyCoordinateBridge.Position(
                        vertex.Position);

                normals[i] =
                    LegacyCoordinateBridge.Direction(
                        vertex.Normal).normalized;

                uvs[i] = vertex.UV;
                weights[i] = BuildBoneWeight(vertex);
            }

            int[] triangles =
                new int[source.Faces.Count * 3];

            for (int i = 0; i < source.Faces.Count; i++)
            {
                LegacyTriangle face = source.Faces[i];

                triangles[i * 3] = face.A;
                triangles[i * 3 + 1] = face.C;
                triangles[i * 3 + 2] = face.B;
            }

            Matrix4x4[] bindPoses =
                new Matrix4x4[bones.Length];

            for (int i = 0; i < bones.Length; i++)
            {
                bindPoses[i] =
                    bones[i].worldToLocalMatrix *
                    actorRoot.localToWorldMatrix;
            }

            Mesh mesh = new Mesh
            {
                name = string.IsNullOrWhiteSpace(meshName)
                    ? "Legacy3DC"
                    : meshName,
                indexFormat =
                    vertexCount > 65535
                        ? IndexFormat.UInt32
                        : IndexFormat.UInt16
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

        public static AnimationClip BuildAnimationClip(
            string semantic,
            LegacyAniFile source,
            Transform actorRoot,
            Transform[] bones,
            bool loop)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (source.Bones.Count != bones.Length)
            {
                throw new InvalidDataException(
                    semantic + " ANI bone count does not match skeleton.");
            }

            Matrix4x4[] absolute =
                new Matrix4x4[source.Bones.Count];

            for (int i = 0; i < source.Bones.Count; i++)
            {
                absolute[i] =
                    LegacyCoordinateBridge.Matrix(
                        source.Bones[i].AbsoluteBaseMatrix);
            }

            AnimationClip clip =
                new AnimationClip
                {
                    name = semantic,
                    frameRate =
                        LegacyAniFile.FramesPerSecond
                };

            for (int i = 0; i < source.Bones.Count; i++)
            {
                LegacyAniBone sourceBone =
                    source.Bones[i];

                int parent =
                    sourceBone.ParentBoneIndex;

                Matrix4x4 local =
                    parent < 0
                        ? absolute[i]
                        : absolute[parent].inverse *
                          absolute[i];

                Vector3 defaultPosition =
                    new Vector3(
                        local.m03,
                        local.m13,
                        local.m23);

                Quaternion defaultRotation =
                    local.rotation.normalized;

                string path =
                    AnimationUtility.CalculateTransformPath(
                        bones[i],
                        actorRoot);

                WriteTranslationCurves(
                    clip,
                    path,
                    sourceBone,
                    defaultPosition);

                WriteRotationCurves(
                    clip,
                    path,
                    sourceBone,
                    defaultRotation);
            }

            clip.EnsureQuaternionContinuity();

            AnimationClipSettings settings =
                AnimationUtility.GetAnimationClipSettings(
                    clip);

            settings.loopTime = loop;
            settings.loopBlend = loop;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            settings.keepOriginalOrientation = true;

            AnimationUtility.SetAnimationClipSettings(
                clip,
                settings);

            return clip;
        }

        public static Material ImportLitMaterial(
            string textureSource,
            string textureAssetPath,
            string materialAssetPath,
            string materialName,
            bool alphaClip)
        {
            if (string.IsNullOrWhiteSpace(textureSource) ||
                !File.Exists(textureSource))
            {
                throw new FileNotFoundException(
                    "Legacy texture source not found.",
                    textureSource);
            }

            string absoluteTexture =
                Path.GetFullPath(textureAssetPath);

            string textureDirectory =
                Path.GetDirectoryName(absoluteTexture);

            if (!string.IsNullOrWhiteSpace(textureDirectory))
                Directory.CreateDirectory(textureDirectory);

            File.Copy(
                textureSource,
                absoluteTexture,
                true);

            AssetDatabase.ImportAsset(
                textureAssetPath,
                ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer =
                AssetImporter.GetAtPath(textureAssetPath)
                    as TextureImporter;

            if (importer != null)
            {
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaSource =
                    TextureImporterAlphaSource.FromInput;

                importer.SaveAndReimport();
            }

            Texture2D texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    textureAssetPath);

            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Lit");

            if (shader == null)
                shader = Shader.Find("Standard");

            if (shader == null)
                throw new InvalidOperationException(
                    "No compatible lit shader is available.");

            Material material =
                new Material(shader)
                {
                    name = materialName
                };

            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            else
                material.mainTexture = texture;

            if (alphaClip)
            {
                if (material.HasProperty("_AlphaClip"))
                    material.SetFloat("_AlphaClip", 1f);

                if (material.HasProperty("_Cutoff"))
                    material.SetFloat("_Cutoff", 0.45f);

                material.EnableKeyword("_ALPHATEST_ON");
            }

            AssetDatabase.DeleteAsset(materialAssetPath);
            AssetDatabase.CreateAsset(
                material,
                materialAssetPath);

            return material;
        }

        public static void ApplyLocalMatrix(
            Transform transform,
            Matrix4x4 local)
        {
            transform.localPosition =
                new Vector3(
                    local.m03,
                    local.m13,
                    local.m23);

            transform.localRotation =
                local.rotation.normalized;

            transform.localScale =
                new Vector3(
                    new Vector3(
                        local.m00,
                        local.m10,
                        local.m20).magnitude,
                    new Vector3(
                        local.m01,
                        local.m11,
                        local.m21).magnitude,
                    new Vector3(
                        local.m02,
                        local.m12,
                        local.m22).magnitude);
        }

        private static BoneWeight BuildBoneWeight(
            Legacy3dcVertex vertex)
        {
            Dictionary<int, float> merged =
                new Dictionary<int, float>();

            AddWeight(
                merged,
                vertex.Bone1,
                vertex.Weight1);

            AddWeight(
                merged,
                vertex.Bone2,
                vertex.Weight2);

            AddWeight(
                merged,
                vertex.Bone3,
                vertex.Weight3);

            List<KeyValuePair<int, float>> ordered =
                merged
                    .Where(pair => pair.Value > 0.000001f)
                    .OrderByDescending(pair => pair.Value)
                    .Take(4)
                    .ToList();

            if (ordered.Count == 0)
                throw new InvalidDataException(
                    "3DC vertex contains no effective bone weight.");

            float sum =
                ordered.Sum(pair => pair.Value);

            BoneWeight result = new BoneWeight();

            for (int slot = 0; slot < ordered.Count; slot++)
            {
                int bone = ordered[slot].Key;
                float weight =
                    ordered[slot].Value / sum;

                switch (slot)
                {
                    case 0:
                        result.boneIndex0 = bone;
                        result.weight0 = weight;
                        break;
                    case 1:
                        result.boneIndex1 = bone;
                        result.weight1 = weight;
                        break;
                    case 2:
                        result.boneIndex2 = bone;
                        result.weight2 = weight;
                        break;
                    case 3:
                        result.boneIndex3 = bone;
                        result.weight3 = weight;
                        break;
                }
            }

            return result;
        }

        private static void AddWeight(
            Dictionary<int, float> merged,
            int bone,
            float weight)
        {
            if (weight <= 0.000001f)
                return;

            float existing;
            merged.TryGetValue(bone, out existing);
            merged[bone] = existing + weight;
        }

        private static void WriteTranslationCurves(
            AnimationClip clip,
            string path,
            LegacyAniBone bone,
            Vector3 fallback)
        {
            List<Keyframe> x = new List<Keyframe>();
            List<Keyframe> y = new List<Keyframe>();
            List<Keyframe> z = new List<Keyframe>();

            if (bone.Translations.Count == 0)
            {
                AddVectorKey(
                    x,
                    y,
                    z,
                    0f,
                    fallback);
            }
            else
            {
                for (int i = 0;
                     i < bone.Translations.Count;
                     i++)
                {
                    LegacyAniTranslationFrame frame =
                        bone.Translations[i];

                    Vector3 value =
                        LegacyCoordinateBridge.Position(
                            frame.Translation);

                    AddVectorKey(
                        x,
                        y,
                        z,
                        frame.Frame /
                        LegacyAniFile.FramesPerSecond,
                        value);
                }
            }

            SetCurve(clip, path, "m_LocalPosition.x", x);
            SetCurve(clip, path, "m_LocalPosition.y", y);
            SetCurve(clip, path, "m_LocalPosition.z", z);
        }

        private static void WriteRotationCurves(
            AnimationClip clip,
            string path,
            LegacyAniBone bone,
            Quaternion fallback)
        {
            List<Keyframe> x = new List<Keyframe>();
            List<Keyframe> y = new List<Keyframe>();
            List<Keyframe> z = new List<Keyframe>();
            List<Keyframe> w = new List<Keyframe>();

            if (bone.Rotations.Count == 0)
            {
                AddQuaternionKey(
                    x,
                    y,
                    z,
                    w,
                    0f,
                    fallback);
            }
            else
            {
                for (int i = 0;
                     i < bone.Rotations.Count;
                     i++)
                {
                    LegacyAniRotationFrame frame =
                        bone.Rotations[i];

                    Quaternion value =
                        LegacyCoordinateBridge.Rotation(
                            frame.Rotation);

                    AddQuaternionKey(
                        x,
                        y,
                        z,
                        w,
                        frame.Frame /
                        LegacyAniFile.FramesPerSecond,
                        value);
                }
            }

            SetCurve(clip, path, "m_LocalRotation.x", x);
            SetCurve(clip, path, "m_LocalRotation.y", y);
            SetCurve(clip, path, "m_LocalRotation.z", z);
            SetCurve(clip, path, "m_LocalRotation.w", w);
        }

        private static void AddVectorKey(
            List<Keyframe> x,
            List<Keyframe> y,
            List<Keyframe> z,
            float time,
            Vector3 value)
        {
            x.Add(new Keyframe(time, value.x));
            y.Add(new Keyframe(time, value.y));
            z.Add(new Keyframe(time, value.z));
        }

        private static void AddQuaternionKey(
            List<Keyframe> x,
            List<Keyframe> y,
            List<Keyframe> z,
            List<Keyframe> w,
            float time,
            Quaternion value)
        {
            x.Add(new Keyframe(time, value.x));
            y.Add(new Keyframe(time, value.y));
            z.Add(new Keyframe(time, value.z));
            w.Add(new Keyframe(time, value.w));
        }

        private static void SetCurve(
            AnimationClip clip,
            string path,
            string property,
            List<Keyframe> keys)
        {
            AnimationCurve curve =
                new AnimationCurve(keys.ToArray());

            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(
                    curve,
                    i,
                    AnimationUtility.TangentMode.Linear);

                AnimationUtility.SetKeyRightTangentMode(
                    curve,
                    i,
                    AnimationUtility.TangentMode.Linear);
            }

            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(
                    path,
                    typeof(Transform),
                    property),
                curve);
        }
    }
}
