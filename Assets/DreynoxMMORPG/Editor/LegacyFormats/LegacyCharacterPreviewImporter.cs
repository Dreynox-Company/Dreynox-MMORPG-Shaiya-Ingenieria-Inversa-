using System;
using System.Collections.Generic;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Gameplay.AnimationSystem;
using Dreynox.Mmorpg.Parity;
using Dreynox.Mmorpg.ParityCore;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public static class LegacyCharacterPreviewImporter
    {
        private const string OutputRoot =
            "Assets/DreynoxMMORPG/LocalLegacyGenerated/Characters/Preview";

        private static GameObject[] _sessionPrefabCache;

        private sealed class PartSpec
        {
            public string Name;
            public string MeshPath;
            public string TexturePath;
            public bool AlphaClip;
        }

        public static GameObject[] ImportAllCanonicalRigs()
        {
            if (_sessionPrefabCache != null &&
                _sessionPrefabCache.Length == 16)
            {
                bool valid = true;

                for (int i = 0;
                     i < _sessionPrefabCache.Length;
                     i++)
                {
                    if (_sessionPrefabCache[i] == null)
                    {
                        valid = false;
                        break;
                    }
                }

                if (valid)
                {
                    return
                        (GameObject[])
                        _sessionPrefabCache.Clone();
                }
            }

            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null ||
                !corpus.Validate().IsCanonical)
            {
                throw new InvalidOperationException(
                    "Configure the canonical ps0032 corpus before importing character previews.");
            }

            var result =
                new GameObject[16];

            for (int nativeRigIndex = 0;
                 nativeRigIndex < result.Length;
                 nativeRigIndex++)
            {
                LegacyCharacterRigSelection selection =
                    LegacyCharacterRigCore
                        .ResolveNativeRigIndex(
                            nativeRigIndex);

                result[nativeRigIndex] =
                    ImportCanonicalRig(
                        corpus,
                        selection,
                        faceIndex: 0,
                        hairIndex: 0,
                        setId: 3);
            }

            _sessionPrefabCache =
                (GameObject[])
                result.Clone();

            return result;
        }

        public static void ClearSessionCache()
        {
            _sessionPrefabCache =
                null;
        }

        public static GameObject ImportCanonicalRig(
            CanonicalClientCorpus corpus,
            LegacyCharacterRigSelection selection,
            int faceIndex,
            int hairIndex,
            int setId)
        {
            if (corpus == null)
                throw new ArgumentNullException(nameof(corpus));

            LegacyCharacterPreviewAssetPaths paths =
                LegacyCharacterAssetCore.ResolvePreview(
                    selection.Family,
                    selection.Job,
                    selection.Sex,
                    faceIndex,
                    hairIndex,
                    setId);

            string outputRoot =
                OutputRoot +
                "/" +
                selection.Prefix;

            EnsureFolder(OutputRoot);
            EnsureFolder(outputRoot);
            EnsureFolder(outputRoot + "/Meshes");
            EnsureFolder(outputRoot + "/Materials");
            EnsureFolder(outputRoot + "/Textures");
            EnsureFolder(outputRoot + "/Animations");
            EnsureFolder(outputRoot + "/Prefabs");

            string selectAniPath =
                ResolveCaseInsensitive(
                    corpus.RootPath,
                    paths.SelectAnimation);

            string upperMeshPath =
                ResolveCaseInsensitive(
                    corpus.RootPath,
                    paths.UpperMesh);

            if (!File.Exists(selectAniPath))
            {
                throw new FileNotFoundException(
                    "Character preview select ANI missing: " +
                    paths.SelectAnimation,
                    selectAniPath);
            }

            if (!File.Exists(upperMeshPath))
            {
                throw new FileNotFoundException(
                    "Character preview upper mesh missing: " +
                    paths.UpperMesh,
                    upperMeshPath);
            }

            LegacyAniFile selectAni =
                LegacyAniParser.Parse(
                    selectAniPath);

            Legacy3dcFile referenceMesh =
                Legacy3dcParser.Parse(
                    upperMeshPath);

            Dreynox.Mmorpg.LocalData.LegacyRuntimeSkinnedBuilder.ValidateMeshForSkeleton(
                referenceMesh, selectAni.Bones.Count);

            var actor =
                new GameObject(
                    selection.Prefix +
                    "_Preview");

            try
            {
                Animator animator =
                    actor.AddComponent<Animator>();

                animator.applyRootMotion =
                    false;

                SemanticAnimationPlayer player =
                    actor.AddComponent<
                        SemanticAnimationPlayer>();

                Transform[] bones =
                    LegacySkinnedAssetBuilder
                        .BuildSkeleton(
                            actor.transform,
                            referenceMesh,
                            selectAni);

                PartSpec[] bodyParts =
                    BuildBodyParts(
                        paths);

                for (int i = 0;
                     i < bodyParts.Length;
                     i++)
                {
                    ImportPart(
                        corpus,
                        actor.transform,
                        bones,
                        outputRoot,
                        bodyParts[i]);
                }

                var faceVariants =
                    new SkinnedMeshRenderer[5];

                var hairVariants =
                    new SkinnedMeshRenderer[5];

                for (int variant = 0;
                     variant < 5;
                     variant++)
                {
                    LegacyCharacterPreviewAssetPaths facePaths =
                        LegacyCharacterAssetCore.ResolvePreview(
                            selection.Family,
                            selection.Job,
                            selection.Sex,
                            faceIndex: variant,
                            hairIndex: 0,
                            setId: setId);

                    faceVariants[variant] =
                        ImportPart(
                            corpus,
                            actor.transform,
                            bones,
                            outputRoot,
                            new PartSpec
                            {
                                Name =
                                    "Face_" +
                                    (variant + 1)
                                        .ToString("D3"),
                                MeshPath =
                                    facePaths.FaceMesh,
                                TexturePath =
                                    facePaths.FaceTexture
                            });

                    LegacyCharacterPreviewAssetPaths hairPaths =
                        LegacyCharacterAssetCore.ResolvePreview(
                            selection.Family,
                            selection.Job,
                            selection.Sex,
                            faceIndex: 0,
                            hairIndex: variant,
                            setId: setId);

                    hairVariants[variant] =
                        ImportPart(
                            corpus,
                            actor.transform,
                            bones,
                            outputRoot,
                            new PartSpec
                            {
                                Name =
                                    "Hair_" +
                                    (variant + 1)
                                        .ToString("D3"),
                                MeshPath =
                                    hairPaths.HairMesh,
                                TexturePath =
                                    hairPaths.HairTexture,
                                AlphaClip =
                                    true
                            });
                }

                LegacyCharacterAppearanceVariants appearance =
                    actor.AddComponent<
                        LegacyCharacterAppearanceVariants>();

                appearance.Configure(
                    faceVariants,
                    hairVariants,
                    faceIndex,
                    hairIndex);

                AnimationClip selectClip =
                    LegacySkinnedAssetBuilder
                        .BuildAnimationClip(
                            "select",
                            selectAni,
                            actor.transform,
                            bones,
                            loop: true);

                string clipPath =
                    outputRoot +
                    "/Animations/select.anim";

                AssetDatabase.DeleteAsset(
                    clipPath);

                AssetDatabase.CreateAsset(
                    selectClip,
                    clipPath);

                AnimationStateCatalog catalog =
                    ScriptableObject.CreateInstance<
                        AnimationStateCatalog>();

                catalog.ReplaceEntries(
                    new[]
                    {
                        new AnimationStateCatalog.Entry
                        {
                            semanticState = "select",
                            clip = selectClip,
                            playbackSpeed = 1f
                        }
                    });

                string catalogPath =
                    outputRoot +
                    "/Animations/" +
                    selection.Prefix +
                    "_PreviewCatalog.asset";

                AssetDatabase.DeleteAsset(
                    catalogPath);

                AssetDatabase.CreateAsset(
                    catalog,
                    catalogPath);

                player.Catalog =
                    catalog;

                string prefabPath =
                    outputRoot +
                    "/Prefabs/" +
                    selection.Prefix +
                    "_Preview.prefab";

                AssetDatabase.DeleteAsset(
                    prefabPath);

                GameObject prefab =
                    PrefabUtility.SaveAsPrefabAsset(
                        actor,
                        prefabPath);

                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        "Could not save character preview prefab '" +
                        prefabPath +
                        "'.");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    actor);
            }
        }

        private static PartSpec[] BuildBodyParts(
            LegacyCharacterPreviewAssetPaths paths)
        {
            return new[]
            {
                new PartSpec
                {
                    Name = "Upper",
                    MeshPath = paths.UpperMesh,
                    TexturePath = paths.UpperTexture
                },
                new PartSpec
                {
                    Name = "Lower",
                    MeshPath = paths.LowerMesh,
                    TexturePath = paths.LowerTexture
                },
                new PartSpec
                {
                    Name = "Hands",
                    MeshPath = paths.HandMesh,
                    TexturePath = paths.HandTexture
                },
                new PartSpec
                {
                    Name = "Feet",
                    MeshPath = paths.FootMesh,
                    TexturePath = paths.FootTexture
                }
            };
        }

        private static SkinnedMeshRenderer ImportPart(
            CanonicalClientCorpus corpus,
            Transform actorRoot,
            Transform[] bones,
            string outputRoot,
            PartSpec spec)
        {
            string meshSource =
                ResolveCaseInsensitive(
                    corpus.RootPath,
                    spec.MeshPath);

            string textureSource =
                ResolveCaseInsensitive(
                    corpus.RootPath,
                    spec.TexturePath);

            if (!File.Exists(meshSource))
            {
                throw new FileNotFoundException(
                    "Character preview mesh missing: " +
                    spec.MeshPath,
                    meshSource);
            }

            if (!File.Exists(textureSource))
            {
                throw new FileNotFoundException(
                    "Character preview texture missing: " +
                    spec.TexturePath,
                    textureSource);
            }

            Legacy3dcFile source =
                Legacy3dcParser.Parse(
                    meshSource);

            Mesh mesh =
                LegacySkinnedAssetBuilder
                    .BuildMesh(
                        source,
                        bones,
                        actorRoot,
                        spec.Name +
                        "_Preview");

            string meshPath =
                outputRoot +
                "/Meshes/" +
                spec.Name +
                ".asset";

            AssetDatabase.DeleteAsset(
                meshPath);

            AssetDatabase.CreateAsset(
                mesh,
                meshPath);

            string textureAssetPath =
                outputRoot +
                "/Textures/" +
                spec.Name.ToLowerInvariant() +
                Path.GetExtension(textureSource)
                    .ToLowerInvariant();

            string materialAssetPath =
                outputRoot +
                "/Materials/" +
                spec.Name +
                ".mat";

            Material material =
                LegacySkinnedAssetBuilder
                    .ImportLitMaterial(
                        textureSource,
                        textureAssetPath,
                        materialAssetPath,
                        spec.Name +
                        "_PreviewMaterial",
                        spec.AlphaClip);

            GameObject partObject =
                new GameObject(
                    spec.Name);

            partObject.transform.SetParent(
                actorRoot,
                false);

            SkinnedMeshRenderer renderer =
                partObject.AddComponent<
                    SkinnedMeshRenderer>();

            renderer.sharedMesh =
                mesh;

            renderer.sharedMaterial =
                material;

            renderer.bones =
                bones;

            renderer.rootBone =
                bones[0];

            renderer.updateWhenOffscreen =
                false;

            renderer.localBounds =
                mesh.bounds;

            return renderer;
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

            for (int i = 0;
                 i < parts.Length;
                 i++)
            {
                if (!Directory.Exists(current))
                {
                    return Path.Combine(
                        current,
                        parts[i]);
                }

                string[] entries =
                    Directory.GetFileSystemEntries(
                        current);

                string match = null;

                for (int j = 0;
                     j < entries.Length;
                     j++)
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
                    match ??
                    Path.Combine(
                        current,
                        parts[i]);
            }

            return current;
        }

        private static void EnsureFolder(
            string path)
        {
            string[] parts =
                path.Split(
                    new[] { '/' },
                    StringSplitOptions.RemoveEmptyEntries);

            string current =
                parts[0];

            for (int i = 1;
                 i < parts.Length;
                 i++)
            {
                string next =
                    current +
                    "/" +
                    parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[i]);
                }

                current = next;
            }
        }
    }
}
