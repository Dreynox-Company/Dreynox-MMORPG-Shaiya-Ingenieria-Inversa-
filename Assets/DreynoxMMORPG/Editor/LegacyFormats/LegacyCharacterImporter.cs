using System;
using System.Collections.Generic;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Gameplay.AnimationSystem;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Gameplay.Equipment;
using Dreynox.Mmorpg.LocalData;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    /// <summary>Canonical Human Fighter Male (humf), built from original assets.</summary>
    public static class LegacyCharacterImporter
    {
        // Keep the established path/entrypoint so scene builders retain their references.
        private const string OutputRoot = "Assets/DreynoxMMORPG/LocalLegacyGenerated/Characters/HumanMale003";
        private const string CharacterRoot = "DATA_Español/character/human/";
        internal readonly struct ClipSpec
        {
            public readonly string Semantic, File;
            public readonly bool Loop;
            public ClipSpec(string semantic, string file, bool loop)
            { Semantic = semantic; File = file; Loop = loop; }
        }
        internal static readonly ClipSpec[] Clips =
        {
            new ClipSpec("idle", "humf_000_normal.ani", true),
            new ClipSpec("walk", "humf_001_walk.ani", true),
            new ClipSpec("run", "humf_002_run.ani", true),
            new ClipSpec("backstep", "humf_003_bstep.ani", false),
            new ClipSpec("step_left", "humf_004_lstep.ani", false),
            new ClipSpec("step_right", "humf_005_rstep.ani", false),
            new ClipSpec("swim_idle", "humf_006_swnormal.ani", true),
            new ClipSpec("swim", "humf_007_swim.ani", true),
            new ClipSpec("jump", "humf_008_jump.ani", false),
            new ClipSpec("dead", "humf_009_die.ani", false),
            new ClipSpec("down", "humf_010_down.ani", false),
            new ClipSpec("stand_up", "humf_011_up.ani", false),
            new ClipSpec("sit", "humf_012_sit.ani", true),
            new ClipSpec("idle_1", "humf_016_idle1.ani", false),
            new ClipSpec("idle_2", "humf_017_idle2.ani", false),
            new ClipSpec("ladder", "humf_018_ladder.ani", true),
            new ClipSpec("select", "humf_019_select.ani", true),
            new ClipSpec("mount_idle", "humf_020_vehicle.ani", true),
            new ClipSpec("mount_run", "humf_020_veh_run.ani", true),
            new ClipSpec("combat_idle", "humf_034_onready.ani", true),
            new ClipSpec("attack_1", "humf_035_onattack01.ani", false),
            new ClipSpec("attack_2", "humf_036_onattack02.ani", false),
            new ClipSpec("attack_3", "humf_037_onattack03.ani", false),
            new ClipSpec("attack_4", "humf_038_onattack04.ani", false),
            new ClipSpec("damage", "humf_039_ondamage.ani", false),
            new ClipSpec("combat_move", "humf_040_onrun.ani", true),
            new ClipSpec("skill_001", "humf_100_skill001.ani", false),
            new ClipSpec("sleep", "humf_114_sleep001.ani", true)
        };

        [MenuItem("Dreynox MMORPG/Client Parity/Import Canonical Human Male 003")]
        public static void ImportCanonicalHumanMale003()
        {
            CanonicalClientCorpus corpus = CanonicalClientCorpus.FromStoredRoot();
            if (corpus == null || !corpus.Validate().IsCanonical)
                throw new InvalidOperationException("A verified ps0032 content corpus is required.");
            foreach (string sub in new[] { "Meshes", "Materials", "Textures", "Animations", "Prefabs" })
                EnsureFolder(OutputRoot + "/" + sub);

            LegacyAniFile reference = ReadClip(corpus, Clips[0]);
            Legacy3dcFile torso = Legacy3dcParser.Parse(corpus.Resolve(CharacterRoot + "3dc/co_humf_upper003.3dc"));
            LegacyRuntimeSkinnedBuilder.ValidateMeshForSkeleton(torso, reference.Bones.Count);
            // Preflight every requested ANI before generating any character assets.
            var bodyClips = new List<LegacyAniFile>(Clips.Length);
            foreach (ClipSpec spec in Clips)
            {
                LegacyAniFile source = ReadClip(corpus, spec);
                bodyClips.Add(LegacyAniRigBinding.BodyClip(source, reference));
                Debug.Log("ANI binding " + spec.File + ": body=" + reference.Bones.Count +
                    ", source=" + source.Bones.Count + ", unbound extra tracks=" +
                    (source.Bones.Count - reference.Bones.Count));
            }

            GameObject actor = new GameObject("HumanMale003_Canonical");
            try
            {
                var controller = actor.AddComponent<CharacterController>();
                controller.center = new Vector3(0, 0.9f, 0);
                controller.height = 1.8f;
                controller.radius = 0.35f;
                actor.AddComponent<Animator>().applyRootMotion = false;
                var animation = actor.AddComponent<SemanticAnimationPlayer>();
                var equipment = actor.AddComponent<EquipmentAttachmentController>();
                actor.AddComponent<ShaiyaClientActor>().ConfigureLegacyIdentity(0, 0, 0);
                Transform[] bones = LegacySkinnedAssetBuilder.BuildSkeleton(actor.transform, torso, reference);
                if (bones.Length > 4) bones[4].gameObject.AddComponent<AttachmentSocket>().Configure("Wings_Back");
                equipment.RebuildSocketCache();
                string[] labels = { "Upper", "Lower", "Hands", "Feet", "Face", "Hair" };
                string[] meshStems = { "co_humf_upper003", "co_humf_lower003", "co_humf_hand003",
                    "co_humf_foot003", "humf_face001", "humf_hair001" };
                string[] textureStems = { "co_humf_upper003", "co_humf_lower003", "co_humf_hand003",
                    "co_humf_foot003", "hum_face001", "hum_hair001" };
                for (int i = 0; i < labels.Length; i++)
                {
                    Legacy3dcFile source = Legacy3dcParser.Parse(corpus.Resolve(CharacterRoot + "3dc/" + meshStems[i] + ".3dc"));
                    Mesh mesh = LegacySkinnedAssetBuilder.BuildMesh(source, bones, actor.transform, labels[i]);
                    WriteAsset(mesh, OutputRoot + "/Meshes/" + labels[i] + ".asset");
                    Material material = LegacySkinnedAssetBuilder.ImportLitMaterial(
                        corpus.Resolve(CharacterRoot + "dds/" + textureStems[i] + ".dds"),
                        OutputRoot + "/Textures/" + textureStems[i] + ".dds",
                        OutputRoot + "/Materials/" + labels[i] + ".mat", labels[i], i == 5);
                    GameObject part = new GameObject(labels[i]);
                    part.transform.SetParent(actor.transform, false);
                    var renderer = part.AddComponent<SkinnedMeshRenderer>();
                    renderer.sharedMesh = mesh; renderer.sharedMaterial = material;
                    renderer.bones = bones; renderer.rootBone = bones[0];
                    renderer.localBounds = mesh.bounds; renderer.updateWhenOffscreen = false;
                }
                var entries = new List<AnimationStateCatalog.Entry>();
                for (int i = 0; i < Clips.Length; i++)
                {
                    ClipSpec spec = Clips[i];
                    AnimationClip clip = LegacySkinnedAssetBuilder.BuildAnimationClip(
                        spec.Semantic, bodyClips[i], actor.transform, bones, spec.Loop);
                    WriteAsset(clip, OutputRoot + "/Animations/" + spec.Semantic + ".anim");
                    entries.Add(new AnimationStateCatalog.Entry { semanticState = spec.Semantic, clip = clip, playbackSpeed = 1 });
                }
                var catalog = ScriptableObject.CreateInstance<AnimationStateCatalog>();
                catalog.ReplaceEntries(entries);
                WriteAsset(catalog, OutputRoot + "/Animations/HumanMale003_Catalog.asset");
                animation.Catalog = catalog;
                string prefabPath = OutputRoot + "/Prefabs/HumanMale003_Canonical.prefab";
                Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.SaveAsPrefabAsset(actor, prefabPath);
                Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.SaveAssets();
                Selection.activeObject = Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.LoadAssetAtPath<GameObject>(prefabPath);
                Debug.Log("DREYNOX_CHARACTER_BODY_IMPORT_OK bones=" + bones.Length + " parts=6 clips=" + entries.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
        }

        internal static LegacyAniFile ReadClip(CanonicalClientCorpus corpus, ClipSpec spec)
        { return LegacyAniParser.Parse(corpus.Resolve(CharacterRoot + "ani6/" + spec.File)); }

        private static void WriteAsset(UnityEngine.Object value, string path)
        {
            Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.DeleteAsset(path);
            Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.CreateAsset(value, path);
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
