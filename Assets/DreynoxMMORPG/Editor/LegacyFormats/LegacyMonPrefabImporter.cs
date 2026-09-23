using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Gameplay.AnimationSystem;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.World;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public static class LegacyMonPrefabImporter
    {
        private sealed class CatalogSpec
        {
            public LegacyMonCatalogKind Kind;
            public string MonPath;
            public string ResourceRoot;
            public string OutputName;
        }

        private sealed class AnimationSpec
        {
            public string Semantic;
            public string FileName;
            public bool Loop;
        }

        private static readonly Dictionary<LegacyMonCatalogKind, CatalogSpec> Catalogs =
            new Dictionary<LegacyMonCatalogKind, CatalogSpec>
            {
                [LegacyMonCatalogKind.Monster] = new CatalogSpec
                {
                    Kind = LegacyMonCatalogKind.Monster,
                    MonPath = "DATA_Español/monster/monster.mon",
                    ResourceRoot = "DATA_Español/monster",
                    OutputName = "Monster"
                },
                [LegacyMonCatalogKind.Npc] = new CatalogSpec
                {
                    Kind = LegacyMonCatalogKind.Npc,
                    MonPath = "DATA_Español/npc/npc.mon",
                    ResourceRoot = "DATA_Español/npc",
                    OutputName = "Npc"
                },
                [LegacyMonCatalogKind.Wing] = new CatalogSpec
                {
                    Kind = LegacyMonCatalogKind.Wing,
                    MonPath = "DATA_Español/character/wing/wing.mon",
                    ResourceRoot = "DATA_Español/character/wing",
                    OutputName = "Wing"
                }
            };

        [MenuItem("Dreynox MMORPG/Client Parity/Entities/Import Monster 0 Ape")]
        public static void ImportMonsterZero()
        {
            Import(LegacyMonCatalogKind.Monster, 0);
        }

        [MenuItem("Dreynox MMORPG/Client Parity/Entities/Import NPC 0 Cow")]
        public static void ImportNpcZero()
        {
            Import(LegacyMonCatalogKind.Npc, 0);
        }

        [MenuItem("Dreynox MMORPG/Client Parity/Entities/Import Wing 0")]
        public static void ImportWingZero()
        {
            Import(LegacyMonCatalogKind.Wing, 0);
        }

        public static GameObject Import(
            LegacyMonCatalogKind kind,
            int recordIndex)
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null || !corpus.Validate().IsCanonical)
                throw new InvalidOperationException(
                    "Configure the canonical ps0032 corpus first.");

            CatalogSpec catalog;
            if (!Catalogs.TryGetValue(kind, out catalog))
                throw new ArgumentOutOfRangeException(nameof(kind));

            string monPath =
                LegacyUiAssetImporter.ResolveCaseInsensitive(
                    corpus.RootPath,
                    catalog.MonPath);

            LegacyMonFile mon =
                LegacyMonParser.Parse(monPath);

            if (recordIndex < 0 ||
                recordIndex >= mon.Records.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(recordIndex),
                    "MON record index is outside the catalog.");
            }

            LegacyMonRecord record =
                mon.Records[recordIndex];

            if (record.Objects.Count == 0)
            {
                throw new InvalidDataException(
                    "MON record " + recordIndex +
                    " contains no mesh objects.");
            }

            List<AnimationSpec> animationSpecs =
                BuildAnimationSpecs(record);

            AnimationSpec referenceSpec =
                animationSpecs.FirstOrDefault(
                    spec => ResourceExists(
                        corpus,
                        catalog.ResourceRoot + "/ani/" + spec.FileName));

            if (referenceSpec == null)
            {
                throw new FileNotFoundException(
                    "MON record " + record.Name +
                    " contains no usable ANI reference.");
            }

            string referenceAniPath =
                ResolveResource(
                    corpus,
                    catalog.ResourceRoot,
                    "ani",
                    referenceSpec.FileName);

            string referenceMeshPath =
                ResolveResource(
                    corpus,
                    catalog.ResourceRoot,
                    "3dc",
                    record.Objects[0].MeshName);

            LegacyAniFile referenceAni =
                LegacyAniParser.Parse(referenceAniPath);

            Legacy3dcFile referenceMesh =
                Legacy3dcParser.Parse(referenceMeshPath);

            if (referenceAni.Bones.Count !=
                referenceMesh.InverseBindMatrices.Count)
            {
                throw new InvalidDataException(
                    record.Name +
                    " reference ANI/3DC skeleton mismatch.");
            }

            string safeName =
                Sanitize(record.Name);

            string outputRoot =
                "Assets/DreynoxMMORPG/LocalLegacyGenerated/Entities/" +
                catalog.OutputName + "/" +
                recordIndex.ToString("D4") + "_" + safeName;

            EnsureFolder(outputRoot);
            EnsureFolder(outputRoot + "/Meshes");
            EnsureFolder(outputRoot + "/Textures");
            EnsureFolder(outputRoot + "/Materials");
            EnsureFolder(outputRoot + "/Animations");
            EnsureFolder(outputRoot + "/Audio");
            EnsureFolder(outputRoot + "/Prefabs");

            GameObject entity =
                new GameObject(
                    catalog.OutputName + "_" +
                    recordIndex.ToString("D4") +
                    "_" + safeName);

            try
            {
                Animator animator =
                    entity.AddComponent<Animator>();

                animator.applyRootMotion = false;

                SemanticAnimationPlayer player =
                    entity.AddComponent<SemanticAnimationPlayer>();

                Transform[] bones =
                    LegacySkinnedAssetBuilder.BuildSkeleton(
                        entity.transform,
                        referenceMesh,
                        referenceAni);

                ImportParts(
                    corpus,
                    catalog,
                    record,
                    entity.transform,
                    bones,
                    outputRoot);

                AnimationStateCatalog animationCatalog =
                    ImportAnimations(
                        corpus,
                        catalog,
                        record,
                        animationSpecs,
                        entity.transform,
                        bones,
                        outputRoot);

                player.Catalog = animationCatalog;

                LegacyMonEntityDescriptor descriptor =
                    entity.AddComponent<LegacyMonEntityDescriptor>();

                descriptor.Configure(
                    kind,
                    recordIndex,
                    record.Name,
                    record.Height,
                    record.Attack1Wav,
                    record.Attack2Wav,
                    record.Attack3Wav,
                    record.DeathWav,
                    record.Attack1Effect,
                    record.Attack2Effect,
                    record.Attack3Effect,
                    record.DieEffect,
                    record.AttachEffect,
                    record.Effects
                        .Select(
                            effect =>
                                new LegacyAttachedEffectDescriptor
                                {
                                    boneId = effect.BoneId,
                                    effectId = effect.EffectId
                                })
                        .ToArray());

                CapsuleCollider collider =
                    entity.AddComponent<CapsuleCollider>();

                float height =
                    Mathf.Max(0.4f, record.Height);

                collider.height = height;
                collider.radius =
                    Mathf.Clamp(height * 0.22f, 0.15f, 2.5f);

                collider.center =
                    new Vector3(
                        0f,
                        height * 0.5f,
                        0f);

                if (kind == LegacyMonCatalogKind.Monster)
                {
                    ShaiyaCombatTarget target =
                        entity.AddComponent<ShaiyaCombatTarget>();

                    target.Configure(
                        recordIndex + 1,
                        1000);

                    AudioClip attack1 =
                        ImportAudioClipOptional(
                            corpus,
                            catalog,
                            record.Attack1Wav,
                            outputRoot,
                            "attack_1");

                    AudioClip attack2 =
                        ImportAudioClipOptional(
                            corpus,
                            catalog,
                            record.Attack2Wav,
                            outputRoot,
                            "attack_2");

                    AudioClip attack3 =
                        ImportAudioClipOptional(
                            corpus,
                            catalog,
                            record.Attack3Wav,
                            outputRoot,
                            "attack_3");

                    AudioClip death =
                        ImportAudioClipOptional(
                            corpus,
                            catalog,
                            record.DeathWav,
                            outputRoot,
                            "death");

                    LegacyMonFeedbackController feedback =
                        entity.AddComponent<LegacyMonFeedbackController>();

                    feedback.Configure(
                        player,
                        target,
                        attack1,
                        attack2,
                        attack3,
                        death);
                }

                string prefabPath =
                    outputRoot + "/Prefabs/" +
                    safeName + ".prefab";

                AssetDatabase.DeleteAsset(prefabPath);

                GameObject prefab =
                    PrefabUtility.SaveAsPrefabAsset(
                        entity,
                        prefabPath);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entity);
            }
        }

        private static void ImportParts(
            CanonicalClientCorpus corpus,
            CatalogSpec catalog,
            LegacyMonRecord record,
            Transform entityRoot,
            Transform[] bones,
            string outputRoot)
        {
            for (int i = 0; i < record.Objects.Count; i++)
            {
                LegacyMonObject sourceObject =
                    record.Objects[i];

                string meshPath =
                    ResolveResource(
                        corpus,
                        catalog.ResourceRoot,
                        "3dc",
                        sourceObject.MeshName);

                string texturePath =
                    ResolveResource(
                        corpus,
                        catalog.ResourceRoot,
                        "dds",
                        sourceObject.TextureName);

                Legacy3dcFile source =
                    Legacy3dcParser.Parse(meshPath);

                Mesh mesh =
                    LegacySkinnedAssetBuilder.BuildMesh(
                        source,
                        bones,
                        entityRoot,
                        record.Name + "_Part_" + i);

                string meshAsset =
                    outputRoot + "/Meshes/Part_" +
                    i.ToString("D2") + ".asset";

                AssetDatabase.DeleteAsset(meshAsset);
                AssetDatabase.CreateAsset(mesh, meshAsset);

                string textureAsset =
                    outputRoot + "/Textures/" +
                    i.ToString("D2") + "_" +
                    Path.GetFileName(texturePath)
                        .ToLowerInvariant();

                string materialAsset =
                    outputRoot + "/Materials/Part_" +
                    i.ToString("D2") + ".mat";

                Material material =
                    LegacySkinnedAssetBuilder.ImportLitMaterial(
                        texturePath,
                        textureAsset,
                        materialAsset,
                        record.Name + "_Material_" + i,
                        alphaClip: true);

                GameObject part =
                    new GameObject(
                        "Part_" + i.ToString("D2") +
                        "_" +
                        Path.GetFileNameWithoutExtension(
                            sourceObject.MeshName));

                part.transform.SetParent(
                    entityRoot,
                    false);

                SkinnedMeshRenderer renderer =
                    part.AddComponent<SkinnedMeshRenderer>();

                renderer.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                renderer.bones = bones;
                renderer.rootBone = bones[0];
                renderer.updateWhenOffscreen = false;
                renderer.localBounds = mesh.bounds;
            }
        }

        private static AnimationStateCatalog ImportAnimations(
            CanonicalClientCorpus corpus,
            CatalogSpec catalog,
            LegacyMonRecord record,
            IReadOnlyList<AnimationSpec> specs,
            Transform entityRoot,
            Transform[] bones,
            string outputRoot)
        {
            List<AnimationStateCatalog.Entry> entries =
                new List<AnimationStateCatalog.Entry>();

            HashSet<string> importedFiles =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < specs.Count; i++)
            {
                AnimationSpec spec = specs[i];

                if (!IsResourceName(spec.FileName) ||
                    !importedFiles.Add(spec.FileName))
                    continue;

                string path =
                    ResolveResourceOptional(
                        corpus,
                        catalog.ResourceRoot,
                        "ani",
                        spec.FileName);

                if (path == null)
                    continue;

                LegacyAniFile ani =
                    LegacyAniParser.Parse(path);

                if (ani.Bones.Count != bones.Length)
                    continue;

                AnimationClip clip =
                    LegacySkinnedAssetBuilder.BuildAnimationClip(
                        spec.Semantic,
                        ani,
                        entityRoot,
                        bones,
                        spec.Loop);

                string assetPath =
                    outputRoot + "/Animations/" +
                    Sanitize(spec.Semantic) + ".anim";

                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.CreateAsset(clip, assetPath);

                entries.Add(
                    new AnimationStateCatalog.Entry
                    {
                        semanticState = spec.Semantic,
                        clip = clip,
                        playbackSpeed = 1f
                    });
            }

            AnimationStateCatalog animationCatalog =
                ScriptableObject.CreateInstance<
                    AnimationStateCatalog>();

            animationCatalog.ReplaceEntries(entries);

            string catalogPath =
                outputRoot +
                "/Animations/SemanticCatalog.asset";

            AssetDatabase.DeleteAsset(catalogPath);
            AssetDatabase.CreateAsset(
                animationCatalog,
                catalogPath);

            return animationCatalog;
        }

        private static List<AnimationSpec> BuildAnimationSpecs(
            LegacyMonRecord record)
        {
            return new List<AnimationSpec>
            {
                A("idle", record.IdleAnimation, true),
                A("breath", record.BreathAnimation, true),
                A("walk", record.WalkAnimation, true),
                A("run", record.RunAnimation, true),
                A("attack_1", record.JumpAttack1Animation, false),
                A("attack_2", record.Attack2Animation, false),
                A("attack_3", record.Attack3Animation, false),
                A("dead", record.DeathAnimation, false),
                A("damage", record.DamageAnimation, false)
            };
        }

        private static AnimationSpec A(
            string semantic,
            string file,
            bool loop)
        {
            return new AnimationSpec
            {
                Semantic = semantic,
                FileName = file,
                Loop = loop
            };
        }

        private static AudioClip ImportAudioClipOptional(
            CanonicalClientCorpus corpus,
            CatalogSpec catalog,
            string fileName,
            string outputRoot,
            string semantic)
        {
            if (!IsResourceName(fileName))
                return null;

            string source =
                ResolveResourceOptional(
                    corpus,
                    catalog.ResourceRoot,
                    "wav",
                    fileName);

            if (source == null)
                return null;

            string extension =
                Path.GetExtension(source);

            string destination =
                outputRoot + "/Audio/" +
                Sanitize(semantic) + "_" +
                Sanitize(
                    Path.GetFileNameWithoutExtension(source)) +
                extension.ToLowerInvariant();

            string absolute =
                Path.GetFullPath(destination);

            string directory =
                Path.GetDirectoryName(absolute);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.Copy(
                source,
                absolute,
                true);

            AssetDatabase.ImportAsset(
                destination,
                ImportAssetOptions.ForceSynchronousImport);

            AudioImporter importer =
                AssetImporter.GetAtPath(destination)
                as AudioImporter;

            if (importer != null)
            {
                AudioImporterSampleSettings settings =
                    importer.defaultSampleSettings;

                settings.loadType =
                    AudioClipLoadType.CompressedInMemory;

                settings.compressionFormat =
                    AudioCompressionFormat.Vorbis;

                settings.quality = 0.72f;

                importer.defaultSampleSettings = settings;
                importer.forceToMono = false;
                importer.preloadAudioData = true;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<AudioClip>(
                destination);
        }

        private static string ResolveResource(
            CanonicalClientCorpus corpus,
            string resourceRoot,
            string subdirectory,
            string fileName)
        {
            string path =
                ResolveResourceOptional(
                    corpus,
                    resourceRoot,
                    subdirectory,
                    fileName);

            if (path == null)
            {
                throw new FileNotFoundException(
                    "Legacy resource not found: " +
                    resourceRoot + "/" +
                    subdirectory + "/" +
                    fileName);
            }

            return path;
        }

        private static string ResolveResourceOptional(
            CanonicalClientCorpus corpus,
            string resourceRoot,
            string subdirectory,
            string fileName)
        {
            if (!IsResourceName(fileName))
                return null;

            string relative =
                resourceRoot + "/" +
                subdirectory + "/" +
                fileName;

            string path =
                LegacyUiAssetImporter.ResolveCaseInsensitive(
                    corpus.RootPath,
                    relative);

            return File.Exists(path)
                ? path
                : null;
        }

        private static bool ResourceExists(
            CanonicalClientCorpus corpus,
            string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return false;

            string path =
                LegacyUiAssetImporter.ResolveCaseInsensitive(
                    corpus.RootPath,
                    relativePath);

            return File.Exists(path);
        }

        private static bool IsResourceName(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   !string.Equals(
                       value.Trim(),
                       "LOAD",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string Sanitize(string value)
        {
            string result =
                string.IsNullOrWhiteSpace(value)
                    ? "Unnamed"
                    : value.Trim();

            foreach (char invalid in
                     Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalid, '_');
            }

            return result
                .Replace(' ', '_')
                .Replace('/', '_')
                .Replace('\\', '_');
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
                    AssetDatabase.CreateFolder(
                        current,
                        parts[i]);

                current = next;
            }
        }
    }
}
