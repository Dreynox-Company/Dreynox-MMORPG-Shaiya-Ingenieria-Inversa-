using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Gameplay.AnimationSystem;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.World;
using Dreynox.Mmorpg.Vfx;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public static class LegacyMonPrefabImporter
    {
        private sealed class CatalogSpec
        {
            public LegacyMonCatalogKind Kind;
            public string MonPath, ResourceRoot, OutputName;
        }
        private sealed class AnimationSpec
        {
            public string Semantic, FileName;
            public bool Loop;
        }
        private static readonly Dictionary<string, LegacyMonAttachEffectCatalogAnalysis> AttachEffectAnalysisCache =
            new Dictionary<string, LegacyMonAttachEffectCatalogAnalysis>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<LegacyMonCatalogKind, CatalogSpec> Catalogs =
            new Dictionary<LegacyMonCatalogKind, CatalogSpec>
            {
                [LegacyMonCatalogKind.Monster] = new CatalogSpec { Kind = LegacyMonCatalogKind.Monster,
                    MonPath = "DATA_Español/monster/monster.mon", ResourceRoot = "DATA_Español/monster", OutputName = "Monster" },
                [LegacyMonCatalogKind.Npc] = new CatalogSpec { Kind = LegacyMonCatalogKind.Npc,
                    MonPath = "DATA_Español/npc/npc.mon", ResourceRoot = "DATA_Español/npc", OutputName = "Npc" },
                [LegacyMonCatalogKind.Wing] = new CatalogSpec { Kind = LegacyMonCatalogKind.Wing,
                    MonPath = "DATA_Español/character/wing/wing.mon", ResourceRoot = "DATA_Español/character/wing", OutputName = "Wing" }
            };

        [MenuItem("Dreynox MMORPG/Client Parity/Entities/Import Monster 0 Ape")]
        public static void ImportMonsterZero() { Import(LegacyMonCatalogKind.Monster, 0); }
        [MenuItem("Dreynox MMORPG/Client Parity/Entities/Import NPC 0 Cow")]
        public static void ImportNpcZero() { Import(LegacyMonCatalogKind.Npc, 0); }
        [MenuItem("Dreynox MMORPG/Client Parity/Entities/Import Wing 0")]
        public static void ImportWingZero() { Import(LegacyMonCatalogKind.Wing, 0); }

        public static GameObject Import(LegacyMonCatalogKind kind, int recordIndex)
        {
            var corpus = CanonicalClientCorpus.FromStoredRoot();
            if (corpus == null || !corpus.Validate().IsCanonical)
                throw new InvalidOperationException("Configure the canonical ps0032 corpus first.");
            if (!Catalogs.TryGetValue(kind, out CatalogSpec catalog)) throw new ArgumentOutOfRangeException(nameof(kind));
            var mon = LegacyMonParser.Parse(LegacyUiAssetImporter.ResolveCaseInsensitive(corpus.RootPath, catalog.MonPath));
            if (recordIndex < 0 || recordIndex >= mon.Records.Count) throw new ArgumentOutOfRangeException(nameof(recordIndex));
            var record = mon.Records[recordIndex];
            // Preflight every part and every named semantic before generating a prefab.
            var plan = ValidateRigOnly(corpus, kind, record);
            var attachAnalysis = ResolveAttachEffectAnalysis(corpus, catalog, mon);
            string safeName = Sanitize(record.Name);
            string outputRoot = "Assets/DreynoxMMORPG/LocalLegacyGenerated/Entities/" +
                catalog.OutputName + "/" + recordIndex.ToString("D4") + "_" + safeName;
            foreach (string sub in new[] { "Meshes", "Textures", "Materials", "Animations", "Audio", "Prefabs" })
                EnsureFolder(outputRoot + "/" + sub);
            var entity = new GameObject(catalog.OutputName + "_" + recordIndex.ToString("D4") + "_" + safeName);
            try
            {
                entity.AddComponent<Animator>().applyRootMotion = false;
                var player = entity.AddComponent<SemanticAnimationPlayer>();
                Transform[] bones = LegacySkinnedAssetBuilder.BuildSkeleton(entity.transform,
                    plan.Parts[plan.ReferencePart], plan.Reference);
                ImportParts(corpus, catalog, record, plan.Parts, entity.transform, bones, outputRoot);
                player.Catalog = ImportAnimations(plan.Clips, entity.transform, bones, outputRoot);
                entity.AddComponent<LegacyMonEntityDescriptor>().Configure(kind, recordIndex, record.Name, record.Height,
                    record.Attack1Wav, record.Attack2Wav, record.Attack3Wav, record.DeathWav,
                    record.Attack1Effect, record.Attack2Effect, record.Attack3Effect, record.DieEffect,
                    record.AttachEffect, record.Effects.Select(e => new LegacyAttachedEffectDescriptor {
                        boneId = e.BoneId, effectId = e.EffectId }).ToArray());
                ConfigureAttachedEffects(corpus, record, bones, entity, attachAnalysis);
                var collider = entity.AddComponent<CapsuleCollider>();
                float height = Mathf.Max(0.4f, record.Height);
                collider.height = height; collider.radius = Mathf.Clamp(height * 0.22f, 0.15f, 2.5f);
                collider.center = new Vector3(0, height * 0.5f, 0);
                if (kind == LegacyMonCatalogKind.Monster)
                {
                    var target = entity.AddComponent<ShaiyaCombatTarget>();
                    target.Configure(recordIndex + 1, 1000);
                    entity.AddComponent<LegacyMonFeedbackController>().Configure(player, target,
                        ImportAudioClipOptional(corpus, catalog, record.Attack1Wav, outputRoot, "attack_1"),
                        ImportAudioClipOptional(corpus, catalog, record.Attack2Wav, outputRoot, "attack_2"),
                        ImportAudioClipOptional(corpus, catalog, record.Attack3Wav, outputRoot, "attack_3"),
                        ImportAudioClipOptional(corpus, catalog, record.DeathWav, outputRoot, "death"),
                        LegacyEftPrefabImporter.Import(corpus, record.Attack1Effect),
                        LegacyEftPrefabImporter.Import(corpus, record.Attack2Effect),
                        LegacyEftPrefabImporter.Import(corpus, record.Attack3Effect),
                        LegacyEftPrefabImporter.Import(corpus, record.DieEffect));
                }
                string path = outputRoot + "/Prefabs/" + safeName + ".prefab";
                AssetDatabase.DeleteAsset(path);
                var prefab = PrefabUtility.SaveAsPrefabAsset(entity, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("DREYNOX_MON_IMPORT_OK " + kind + "/" + recordIndex + " " + record.Name +
                    " bones=" + bones.Length + " parts=" + plan.Parts.Length + " clips=" + plan.Clips.Count +
                    " extraTrackClips=" + plan.Clips.Count(c => c.ExtraTracks > 0) +
                    " reparentedClips=" + plan.Clips.Count(c => c.ReparentedBones > 0));
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(entity); }
        }

        internal static LegacyMonRigPlan ValidateRigOnly(CanonicalClientCorpus corpus,
            LegacyMonCatalogKind kind, LegacyMonRecord record)
        {
            var catalog = Catalogs[kind];
            var parts = new Legacy3dcFile[record.Objects.Count];
            for (int i = 0; i < parts.Length; i++)
            {
                string path = ResolveResource(corpus, catalog.ResourceRoot, "3dc", record.Objects[i].MeshName);
                parts[i] = Legacy3dcParser.ParseWithTopologyNormals(File.ReadAllBytes(path));
            }
            var parsed = new Dictionary<string, LegacyAniFile>(StringComparer.OrdinalIgnoreCase);
            var clips = new List<LegacyMonClipBinding>();
            foreach (var spec in BuildAnimationSpecs(record))
            {
                if (!IsResourceName(spec.FileName)) continue;
                if (!parsed.TryGetValue(spec.FileName, out LegacyAniFile ani))
                {
                    ani = LegacyAniParser.Parse(ResolveResource(corpus, catalog.ResourceRoot, "ani", spec.FileName));
                    parsed.Add(spec.FileName, ani);
                }
                // File caching is not semantic deduplication: idle/breath can share one file.
                clips.Add(new LegacyMonClipBinding { Semantic = spec.Semantic, FileName = spec.FileName,
                    Loop = spec.Loop, Source = ani });
            }
            try { return LegacyMonRigBinding.Prepare(parts, clips, record.Effects); }
            catch (InvalidDataException ex)
            { throw new InvalidDataException("MON '" + record.Name + "': " + ex.Message, ex); }
        }

        private static void ImportParts(CanonicalClientCorpus corpus, CatalogSpec catalog, LegacyMonRecord record,
            Legacy3dcFile[] parts, Transform root, Transform[] bones, string outputRoot)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                var item = record.Objects[i];
                string texturePath = ResolveResource(corpus, catalog.ResourceRoot, "dds", item.TextureName);
                var mesh = LegacySkinnedAssetBuilder.BuildMesh(parts[i], bones, root, record.Name + "_Part_" + i);
                string meshPath = outputRoot + "/Meshes/Part_" + i.ToString("D2") + ".asset";
                AssetDatabase.DeleteAsset(meshPath); AssetDatabase.CreateAsset(mesh, meshPath);
                var material = LegacySkinnedAssetBuilder.ImportLitMaterial(texturePath,
                    outputRoot + "/Textures/" + i.ToString("D2") + "_" + Path.GetFileName(texturePath).ToLowerInvariant(),
                    outputRoot + "/Materials/Part_" + i.ToString("D2") + ".mat", record.Name + "_Material_" + i, true);
                var part = new GameObject("Part_" + i.ToString("D2") + "_" + Path.GetFileNameWithoutExtension(item.MeshName));
                part.transform.SetParent(root, false);
                var renderer = part.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh; renderer.sharedMaterial = material;
                renderer.bones = bones; renderer.rootBone = bones[0];
                renderer.updateWhenOffscreen = false; renderer.localBounds = mesh.bounds;
            }
        }
        private static AnimationStateCatalog ImportAnimations(IReadOnlyList<LegacyMonClipBinding> specs,
            Transform root, Transform[] bones, string outputRoot)
        {
            var entries = new List<AnimationStateCatalog.Entry>();
            foreach (var spec in specs)
            {
                var clip = LegacySkinnedAssetBuilder.BuildAnimationClip(spec.Semantic, spec.Bound, root, bones, spec.Loop);
                string path = outputRoot + "/Animations/" + Sanitize(spec.Semantic) + ".anim";
                AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(clip, path);
                entries.Add(new AnimationStateCatalog.Entry { semanticState = spec.Semantic, clip = clip, playbackSpeed = 1 });
            }
            var catalog = ScriptableObject.CreateInstance<AnimationStateCatalog>();
            catalog.ReplaceEntries(entries);
            string catalogPath = outputRoot + "/Animations/SemanticCatalog.asset";
            AssetDatabase.DeleteAsset(catalogPath); AssetDatabase.CreateAsset(catalog, catalogPath);
            return catalog;
        }
        private static List<AnimationSpec> BuildAnimationSpecs(LegacyMonRecord r)
        {
            return new List<AnimationSpec> { A("idle",r.IdleAnimation,true), A("breath",r.BreathAnimation,true),
                A("walk",r.WalkAnimation,true), A("run",r.RunAnimation,true), A("attack_1",r.JumpAttack1Animation,false),
                A("attack_2",r.Attack2Animation,false), A("attack_3",r.Attack3Animation,false),
                A("dead",r.DeathAnimation,false), A("damage",r.DamageAnimation,false) };
        }
        private static AnimationSpec A(string semantic, string file, bool loop)
            => new AnimationSpec { Semantic = semantic, FileName = file, Loop = loop };

        private static LegacyMonAttachEffectCatalogAnalysis ResolveAttachEffectAnalysis(
            CanonicalClientCorpus corpus, CatalogSpec catalog, LegacyMonFile mon)
        {
            string key = corpus.RootPath + "|" + catalog.Kind;
            if (AttachEffectAnalysisCache.TryGetValue(key, out var cached)) return cached;
            var analysis = LegacyMonAttachEffectResolver.Analyze(corpus, mon);
            AttachEffectAnalysisCache.Add(key, analysis);
            return analysis;
        }
        private static void ConfigureAttachedEffects(CanonicalClientCorpus corpus, LegacyMonRecord record,
            IReadOnlyList<Transform> bones, GameObject entity, LegacyMonAttachEffectCatalogAnalysis analysis)
        {
            if (record == null || record.Effects.Count == 0) return;
            if (analysis == null || analysis.Mode == LegacyMonAttachEffectIndexMode.Ambiguous)
                throw new InvalidDataException("MON attach-effect mode remains ambiguous for '" + record.Name + "'.");
            string name = LegacyMonAttachEffectResolver.ResolveBindingLibraryName(record);
            var library = LegacyEftPrefabImporter.ParseCanonical(corpus, name);
            if (library == null) throw new FileNotFoundException("Attached EFT library missing for '" + record.Name + "': " + name);
            var invocation = LegacyMonAttachEffectResolver.ResolveInvocationKind(record, library, analysis.Mode);
            var prefab = LegacyEftPrefabImporter.Import(corpus, name);
            if (prefab == null) throw new InvalidDataException("Attached EFT importer returned null for '" + record.Name + "'.");
            var bindings = new LegacyAttachedEffectBinding[record.Effects.Count];
            for (int i = 0; i < bindings.Length; i++)
            {
                var effect = record.Effects[i];
                if (effect.BoneId < 0 || effect.BoneId >= bones.Count || bones[effect.BoneId] == null)
                    throw new InvalidDataException("MON '" + record.Name + "' attaches to invalid bone " + effect.BoneId);
                bindings[i] = new LegacyAttachedEffectBinding { bone = bones[effect.BoneId], effectPrefab = prefab,
                    effectIndex = effect.EffectId, invocationKind = invocation };
            }
            entity.AddComponent<LegacyAttachedEffectController>().Configure(bindings);
        }
        private static AudioClip ImportAudioClipOptional(CanonicalClientCorpus corpus, CatalogSpec catalog,
            string name, string outputRoot, string semantic)
        {
            string source = ResolveResourceOptional(corpus, catalog.ResourceRoot, "wav", name);
            if (source == null) return null;
            string path = outputRoot + "/Audio/" + Sanitize(semantic) + "_" +
                Sanitize(Path.GetFileNameWithoutExtension(source)) + Path.GetExtension(source).ToLowerInvariant();
            string absolute = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            File.Copy(source, absolute, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.72f; settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = false; importer.loadInBackground = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        private static string ResolveResource(CanonicalClientCorpus corpus, string root, string sub, string name)
        {
            return ResolveResourceOptional(corpus, root, sub, name) ??
                throw new FileNotFoundException("Legacy resource not found: " + root + "/" + sub + "/" + name);
        }
        private static string ResolveResourceOptional(CanonicalClientCorpus corpus, string root, string sub, string name)
        {
            if (!IsResourceName(name)) return null;
            string path = LegacyUiAssetImporter.ResolveCaseInsensitive(corpus.RootPath, root + "/" + sub + "/" + name);
            return File.Exists(path) ? path : null;
        }
        private static bool IsResourceName(string value)
            => !string.IsNullOrWhiteSpace(value) && !string.Equals(value.Trim(), "LOAD", StringComparison.OrdinalIgnoreCase);
        private static string Sanitize(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Unnamed" : value.Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) result = result.Replace(c, '_');
            return result.Replace(' ', '_').Replace('/', '_').Replace('\\', '_');
        }
        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/'); string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
