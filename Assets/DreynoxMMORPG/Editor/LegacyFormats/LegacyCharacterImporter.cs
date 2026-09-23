using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Gameplay.AnimationSystem;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Gameplay.Equipment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public static class LegacyCharacterImporter
    {
        private const string OutputRoot =
            "Assets/DreynoxMMORPG/LocalLegacyGenerated/Characters/HumanMale003";

        private sealed class PartSpec
        {
            public string Name;
            public string MeshPath;
            public string TexturePath;
            public bool AlphaClip;
        }

        private sealed class ClipSpec
        {
            public string Semantic;
            public string Path;
            public bool Loop;
        }

        private static readonly PartSpec[] Parts =
        {
            new PartSpec
            {
                Name = "Upper",
                MeshPath = "DATA_Español/character/human/3dc/co_humm_upper003.3dc",
                TexturePath = "DATA_Español/character/human/dds/co_humm_upper003.dds"
            },
            new PartSpec
            {
                Name = "Lower",
                MeshPath = "DATA_Español/character/human/3dc/co_humm_lower003.3dc",
                TexturePath = "DATA_Español/character/human/dds/co_humm_lower003.dds"
            },
            new PartSpec
            {
                Name = "Hands",
                MeshPath = "DATA_Español/character/human/3dc/co_humm_hand003.3dc",
                TexturePath = "DATA_Español/character/human/dds/co_humm_hand003.dds"
            },
            new PartSpec
            {
                Name = "Feet",
                MeshPath = "DATA_Español/character/human/3dc/co_humm_foot003.3dc",
                TexturePath = "DATA_Español/character/human/dds/co_humm_foot003.dds"
            },
            new PartSpec
            {
                Name = "Face",
                MeshPath = "DATA_Español/character/human/3dc/humm_face001.3dc",
                TexturePath = "DATA_Español/character/human/dds/hum_face001.dds"
            },
            new PartSpec
            {
                Name = "Hair",
                MeshPath = "DATA_Español/character/human/3dc/humm_hair001.3dc",
                TexturePath = "DATA_Español/character/human/dds/hum_hair001.dds",
                AlphaClip = true
            }
        };

        private static readonly ClipSpec[] Clips =
        {
            new ClipSpec
            {
                Semantic = "idle",
                Path = "DATA_Español/character/human/ani6/humm_000_normal.ani",
                Loop = true
            },
            new ClipSpec
            {
                Semantic = "walk",
                Path = "DATA_Español/character/human/ani6/humm_001_walk.ani",
                Loop = true
            },
            new ClipSpec
            {
                Semantic = "run",
                Path = "DATA_Español/character/human/ani6/humm_002_run.ani",
                Loop = true
            },
            new ClipSpec
            {
                Semantic = "jump",
                Path = "DATA_Español/character/human/ani6/humm_008_jump.ani",
                Loop = false
            }
        };

        [MenuItem(
            "Dreynox MMORPG/Client Parity/" +
            "Import Canonical Human Male 003")]
        public static void ImportCanonicalHumanMale003()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null)
                throw new InvalidOperationException(
                    "Select the canonical ps0032 corpus first.");

            CorpusValidationResult validation = corpus.Validate();
            if (!validation.IsCanonical)
                throw new InvalidOperationException(
                    "The selected corpus is not the canonical ps0032 baseline.");

            EnsureFolder(OutputRoot);
            EnsureFolder(OutputRoot + "/Meshes");
            EnsureFolder(OutputRoot + "/Materials");
            EnsureFolder(OutputRoot + "/Textures");
            EnsureFolder(OutputRoot + "/Animations");
            EnsureFolder(OutputRoot + "/Prefabs");

            LegacyAniFile referenceAni =
                LegacyAniParser.Parse(
                    ResolveCaseInsensitive(corpus.RootPath, Clips[0].Path));

            Legacy3dcFile referenceMesh =
                Legacy3dcParser.Parse(
                    ResolveCaseInsensitive(corpus.RootPath, Parts[0].MeshPath));

            if (referenceMesh.InverseBindMatrices.Count != referenceAni.Bones.Count)
                throw new InvalidDataException(
                    "Canonical mesh/ANI bone count mismatch.");

            GameObject actor = new GameObject("HumanMale003_Canonical");
            try
            {
                CharacterController characterController =
                    actor.AddComponent<CharacterController>();
                characterController.center = new Vector3(0f, 0.9f, 0f);
                characterController.height = 1.8f;
                characterController.radius = 0.35f;

                Animator animator = actor.AddComponent<Animator>();
                animator.applyRootMotion = false;

                SemanticAnimationPlayer semanticPlayer =
                    actor.AddComponent<SemanticAnimationPlayer>();

                EquipmentAttachmentController equipment =
                    actor.AddComponent<EquipmentAttachmentController>();

                ShaiyaClientActor clientActor =
                    actor.AddComponent<ShaiyaClientActor>();
                clientActor.ConfigureLegacyIdentity(0, 0, 0);

                Transform[] bones = BuildSkeleton(
                    actor.transform,
                    referenceMesh,
                    referenceAni);

                AddVerifiedSockets(bones);

                for (int i = 0; i < Parts.Length; i++)
                {
                    ImportPart(
                        corpus,
                        actor.transform,
                        bones,
                        Parts[i]);
                }

                AnimationStateCatalog catalog =
                    ImportAnimations(
                        corpus,
                        actor.transform,
                        bones,
                        referenceAni.Bones.Count);

                semanticPlayer.Catalog = catalog;

                string prefabPath =
                    OutputRoot + "/Prefabs/HumanMale003_Canonical.prefab";

                AssetDatabase.DeleteAsset(prefabPath);
                PrefabUtility.SaveAsPrefabAsset(actor, prefabPath);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Selection.activeObject =
                    AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                Debug.Log(
                    "Dreynox MMORPG: canonical Human Male 003 imported with " +
                    bones.Length + " bones, " + Parts.Length +
                    " skinned parts and " + Clips.Length + " ANI clips.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        private static Transform[] BuildSkeleton(
            Transform actorRoot,
            Legacy3dcFile mesh,
            LegacyAniFile ani)
        {
            int boneCount = mesh.InverseBindMatrices.Count;
            Transform[] bones = new Transform[boneCount];
            Matrix4x4[] bindWorld = new Matrix4x4[boneCount];

            GameObject skeletonObject = new GameObject("Skeleton");
            skeletonObject.transform.SetParent(actorRoot, false);

            for (int i = 0; i < boneCount; i++)
            {
                Matrix4x4 shaiyaWorld =
                    mesh.InverseBindMatrices[i].inverse;

                bindWorld[i] =
                    LegacyCoordinateBridge.Matrix(shaiyaWorld);
            }

            for (int i = 0; i < boneCount; i++)
            {
                int parentIndex = ani.Bones[i].ParentBoneIndex;

                GameObject boneObject =
                    new GameObject("Bone_" + i.ToString("D3"));

                Transform parent =
                    parentIndex < 0
                        ? skeletonObject.transform
                        : bones[parentIndex];

                if (parent == null)
                    throw new InvalidDataException(
                        "ANI hierarchy is not parent-before-child at bone " + i + ".");

                boneObject.transform.SetParent(parent, false);

                Matrix4x4 local =
                    parentIndex < 0
                        ? bindWorld[i]
                        : bindWorld[parentIndex].inverse * bindWorld[i];

                ApplyLocalMatrix(boneObject.transform, local);
                bones[i] = boneObject.transform;
            }

            return bones;
        }

        private static void AddVerifiedSockets(Transform[] bones)
        {
            if (bones.Length <= 4)
                return;

            AttachmentSocket wingSocket =
                bones[4].gameObject.GetComponent<AttachmentSocket>();

            if (wingSocket == null)
                wingSocket =
                    bones[4].gameObject.AddComponent<AttachmentSocket>();

            wingSocket.Configure("Wings_Back");
        }

        private static void ImportPart(
            CanonicalClientCorpus corpus,
            Transform actorRoot,
            Transform[] bones,
            PartSpec spec)
        {
            string meshSource =
                ResolveCaseInsensitive(corpus.RootPath, spec.MeshPath);

            Legacy3dcFile source =
                Legacy3dcParser.Parse(meshSource);

            if (source.InverseBindMatrices.Count != bones.Length)
                throw new InvalidDataException(
                    spec.Name + " has " +
                    source.InverseBindMatrices.Count +
                    " bones; expected " + bones.Length + ".");

            Mesh mesh = BuildMesh(source, bones, actorRoot);
            string meshPath =
                OutputRoot + "/Meshes/" + spec.Name + ".asset";

            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            Material material =
                ImportMaterial(corpus, spec);

            GameObject partObject =
                new GameObject(spec.Name);

            partObject.transform.SetParent(actorRoot, false);

            SkinnedMeshRenderer renderer =
                partObject.AddComponent<SkinnedMeshRenderer>();

            renderer.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.bones = bones;
            renderer.rootBone = bones[0];
            renderer.updateWhenOffscreen = false;
            renderer.localBounds = mesh.bounds;
        }

        private static Mesh BuildMesh(
            Legacy3dcFile source,
            Transform[] bones,
            Transform actorRoot)
        {
            int vertexCount = source.Vertices.Count;

            Vector3[] positions = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            BoneWeight[] weights = new BoneWeight[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                Legacy3dcVertex vertex = source.Vertices[i];

                positions[i] =
                    LegacyCoordinateBridge.Position(vertex.Position);

                normals[i] =
                    LegacyCoordinateBridge.Direction(vertex.Normal).normalized;

                uvs[i] = vertex.UV;
                weights[i] = BuildBoneWeight(vertex);
            }

            int[] triangles =
                new int[source.Faces.Count * 3];

            for (int i = 0; i < source.Faces.Count; i++)
            {
                LegacyTriangle face = source.Faces[i];

                // Z reflection changes handedness, therefore winding is reversed.
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
                name = "Legacy3DC",
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

        private static BoneWeight BuildBoneWeight(
            Legacy3dcVertex vertex)
        {
            Dictionary<int, float> merged =
                new Dictionary<int, float>();

            AddWeight(merged, vertex.Bone1, vertex.Weight1);
            AddWeight(merged, vertex.Bone2, vertex.Weight2);
            AddWeight(merged, vertex.Bone3, vertex.Weight3);

            List<KeyValuePair<int, float>> ordered =
                merged
                    .Where(pair => pair.Value > 0.000001f)
                    .OrderByDescending(pair => pair.Value)
                    .Take(4)
                    .ToList();

            if (ordered.Count == 0)
                throw new InvalidDataException(
                    "3DC vertex contains no effective bone weight.");

            float sum = ordered.Sum(pair => pair.Value);
            BoneWeight result = new BoneWeight();

            SetInfluence(ref result, 0, ordered, sum);
            SetInfluence(ref result, 1, ordered, sum);
            SetInfluence(ref result, 2, ordered, sum);
            SetInfluence(ref result, 3, ordered, sum);

            return result;
        }

        private static void AddWeight(
            Dictionary<int, float> merged,
            int bone,
            float weight)
        {
            if (weight <= 0.000001f)
                return;

            float current;
            merged.TryGetValue(bone, out current);
            merged[bone] = current + weight;
        }

        private static void SetInfluence(
            ref BoneWeight result,
            int slot,
            IReadOnlyList<KeyValuePair<int, float>> ordered,
            float sum)
        {
            if (slot >= ordered.Count)
                return;

            int index = ordered[slot].Key;
            float weight = ordered[slot].Value / sum;

            switch (slot)
            {
                case 0:
                    result.boneIndex0 = index;
                    result.weight0 = weight;
                    break;
                case 1:
                    result.boneIndex1 = index;
                    result.weight1 = weight;
                    break;
                case 2:
                    result.boneIndex2 = index;
                    result.weight2 = weight;
                    break;
                case 3:
                    result.boneIndex3 = index;
                    result.weight3 = weight;
                    break;
            }
        }

        private static Material ImportMaterial(
            CanonicalClientCorpus corpus,
            PartSpec spec)
        {
            string source =
                ResolveCaseInsensitive(corpus.RootPath, spec.TexturePath);

            if (!File.Exists(source))
                throw new FileNotFoundException(
                    "Canonical texture missing: " + spec.TexturePath,
                    source);

            string texturePath =
                OutputRoot + "/Textures/" +
                Path.GetFileName(source).ToLowerInvariant();

            string absolute =
                Path.GetFullPath(texturePath);

            Directory.CreateDirectory(
                Path.GetDirectoryName(absolute));

            File.Copy(source, absolute, true);
            AssetDatabase.ImportAsset(
                texturePath,
                ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer =
                AssetImporter.GetAtPath(texturePath) as TextureImporter;

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
                AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
                shader = Shader.Find("Standard");

            if (shader == null)
                throw new InvalidOperationException(
                    "No compatible lit shader is available.");

            Material material =
                new Material(shader)
                {
                    name = spec.Name + "_Material"
                };

            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            else
                material.mainTexture = texture;

            if (spec.AlphaClip &&
                material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.45f);
                material.EnableKeyword("_ALPHATEST_ON");
            }

            string materialPath =
                OutputRoot + "/Materials/" +
                spec.Name + ".mat";

            AssetDatabase.DeleteAsset(materialPath);
            AssetDatabase.CreateAsset(material, materialPath);

            return material;
        }

        private static AnimationStateCatalog ImportAnimations(
            CanonicalClientCorpus corpus,
            Transform actorRoot,
            Transform[] bones,
            int expectedBoneCount)
        {
            List<AnimationStateCatalog.Entry> entries =
                new List<AnimationStateCatalog.Entry>();

            for (int i = 0; i < Clips.Length; i++)
            {
                ClipSpec spec = Clips[i];

                LegacyAniFile source =
                    LegacyAniParser.Parse(
                        ResolveCaseInsensitive(
                            corpus.RootPath,
                            spec.Path));

                if (source.Bones.Count != expectedBoneCount)
                    throw new InvalidDataException(
                        spec.Semantic +
                        " ANI bone count does not match the mesh.");

                AnimationClip clip =
                    BuildAnimationClip(
                        spec.Semantic,
                        source,
                        actorRoot,
                        bones,
                        spec.Loop);

                string clipPath =
                    OutputRoot + "/Animations/" +
                    spec.Semantic + ".anim";

                AssetDatabase.DeleteAsset(clipPath);
                AssetDatabase.CreateAsset(clip, clipPath);

                entries.Add(
                    new AnimationStateCatalog.Entry
                    {
                        semanticState = spec.Semantic,
                        clip = clip,
                        playbackSpeed = 1f
                    });
            }

            AnimationStateCatalog catalog =
                ScriptableObject.CreateInstance<
                    AnimationStateCatalog>();

            catalog.ReplaceEntries(entries);

            string catalogPath =
                OutputRoot + "/Animations/" +
                "HumanMale003_Catalog.asset";

            AssetDatabase.DeleteAsset(catalogPath);
            AssetDatabase.CreateAsset(catalog, catalogPath);
            return catalog;
        }

        private static AnimationClip BuildAnimationClip(
            string semantic,
            LegacyAniFile source,
            Transform actorRoot,
            Transform[] bones,
            bool loop)
        {
            Matrix4x4[] absolute =
                new Matrix4x4[source.Bones.Count];

            for (int i = 0; i < source.Bones.Count; i++)
                absolute[i] =
                    LegacyCoordinateBridge.Matrix(
                        source.Bones[i].AbsoluteBaseMatrix);

            AnimationClip clip =
                new AnimationClip
                {
                    name = semantic,
                    frameRate = LegacyAniFile.FramesPerSecond
                };

            for (int i = 0; i < source.Bones.Count; i++)
            {
                LegacyAniBone sourceBone = source.Bones[i];
                int parent = sourceBone.ParentBoneIndex;

                Matrix4x4 local =
                    parent < 0
                        ? absolute[i]
                        : absolute[parent].inverse * absolute[i];

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
                AnimationUtility.GetAnimationClipSettings(clip);

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
                AddVectorKey(x, y, z, 0f, fallback);
            }
            else
            {
                for (int i = 0; i < bone.Translations.Count; i++)
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
                        frame.Frame / LegacyAniFile.FramesPerSecond,
                        value);
                }
            }

            SetCurve(
                clip,
                path,
                "m_LocalPosition.x",
                x);

            SetCurve(
                clip,
                path,
                "m_LocalPosition.y",
                y);

            SetCurve(
                clip,
                path,
                "m_LocalPosition.z",
                z);
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
                for (int i = 0; i < bone.Rotations.Count; i++)
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
                        frame.Frame / LegacyAniFile.FramesPerSecond,
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

        private static void ApplyLocalMatrix(
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

            Vector3 scale = new Vector3(
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

            transform.localScale = scale;
        }

        private static string ResolveCaseInsensitive(
            string root,
            string relativePath)
        {
            string current = root;
            string[] parts =
                relativePath
                    .Replace('\\', '/')
                    .Split(
                        new[] { '/' },
                        StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                if (!Directory.Exists(current))
                    return Path.Combine(current, parts[i]);

                string[] entries =
                    Directory.GetFileSystemEntries(current);

                string match = null;
                for (int j = 0; j < entries.Length; j++)
                {
                    if (string.Equals(
                            Path.GetFileName(entries[j]),
                            parts[i],
                            StringComparison.OrdinalIgnoreCase))
                    {
                        match = entries[j];
                        break;
                    }
                }

                current =
                    match ?? Path.Combine(current, parts[i]);
            }

            return current;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts =
                path.Split(
                    new[] { '/' },
                    StringSplitOptions.RemoveEmptyEntries);

            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next =
                    current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}
