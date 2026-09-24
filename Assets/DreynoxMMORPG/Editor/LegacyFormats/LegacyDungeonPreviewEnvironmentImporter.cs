using System;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public sealed class LegacyDungeonPreviewBuildResult
    {
        public GameObject Root;
        public LegacyWldTerrainFile World;
        public LegacyDgImportResult Dungeon;
        public Vector3 PreviewAnchor;
        public string PreviewAnchorSource = string.Empty;
        public int BuildingInstances;
        public int ShapeInstances;
        public int TreeInstances;
        public int GrassInstances;
    }

    public static class LegacyDungeonPreviewEnvironmentImporter
    {
        private const string OutputRoot =
            "Assets/DreynoxMMORPG/LocalLegacyGenerated/" +
            "World/Dungeon/LoginPreview";

        public static LegacyDungeonPreviewBuildResult CreateCanonicalLogin(
            CanonicalClientCorpus corpus)
        {
            if (corpus == null)
                throw new ArgumentNullException(nameof(corpus));

            CorpusValidationResult validation =
                corpus.Validate();

            if (!validation.IsCanonical)
            {
                throw new InvalidOperationException(
                    "Canonical ps0032 corpus is required.");
            }

            string wldPath =
                corpus.Resolve(
                    "DATA_Español/world/Login.wld");

            LegacyWldTerrainFile wld =
                LegacyWldTerrainParser.Parse(
                    wldPath);

            if (!string.Equals(
                    wld.Signature,
                    "DUN\0",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Login.wld is not a DUN world.");
            }

            if (string.IsNullOrWhiteSpace(
                    wld.InnerLayout) ||
                !string.Equals(
                    Path.GetExtension(
                        wld.InnerLayout),
                    ".dg",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Login.wld does not reference a DG layout.");
            }

            LegacyDgImportResult dg =
                LegacyDgPrefabImporter
                    .ImportCanonical(
                        corpus,
                        wld.InnerLayout,
                        OutputRoot + "/DG");

            if (dg == null ||
                dg.Prefab == null ||
                dg.Source == null)
            {
                throw new InvalidDataException(
                    "Canonical login DG import returned no prefab.");
            }

            GameObject root =
                new GameObject(
                    "CanonicalLoginDungeon");

            GameObject dgInstance =
                PrefabUtility.InstantiatePrefab(
                    dg.Prefab)
                as GameObject;

            if (dgInstance == null)
            {
                UnityEngine.Object.DestroyImmediate(
                    root);

                throw new InvalidOperationException(
                    "Could not instantiate canonical login DG prefab.");
            }

            dgInstance.name =
                "DUN_LOGIN_Geometry";

            dgInstance.transform.SetParent(
                root.transform,
                false);

            LegacyDgPrefabImporter
                .ApplySceneLightmaps(
                    dg.Lightmaps);

            LegacySmodPrefabImporter
                .ClearSessionCache();

            int buildings =
                CreateStaticGroup(
                    corpus,
                    root.transform,
                    "Buildings",
                    "building",
                    wld.Buildings,
                    alphaClip: false);

            int shapes =
                CreateStaticGroup(
                    corpus,
                    root.transform,
                    "Shapes",
                    "shape",
                    wld.Shapes,
                    alphaClip: false);

            int trees =
                CreateStaticGroup(
                    corpus,
                    root.transform,
                    "Trees",
                    "tree",
                    wld.Trees,
                    alphaClip: true);

            int grass =
                CreateStaticGroup(
                    corpus,
                    root.transform,
                    "Grass",
                    "grass",
                    wld.Grass,
                    alphaClip: true);

            ApplyFog(
                wld);

            LegacyBounds bounds =
                dg.Source.BoundingBox;

            string anchorSource;
            Vector3 previewAnchor =
                ResolvePreviewAnchor(
                    wld,
                    bounds,
                    out anchorSource);

            return new LegacyDungeonPreviewBuildResult
            {
                Root = root,
                World = wld,
                Dungeon = dg,
                PreviewAnchor = previewAnchor,
                PreviewAnchorSource = anchorSource,
                BuildingInstances = buildings,
                ShapeInstances = shapes,
                TreeInstances = trees,
                GrassInstances = grass
            };
        }

        private static Vector3 ResolvePreviewAnchor(
            LegacyWldTerrainFile wld,
            LegacyBounds bounds,
            out string source)
        {
            Vector3 center =
                new Vector3(
                    (bounds.Lower.x +
                     bounds.Upper.x) *
                    0.5f,
                    bounds.Lower.y,
                    (bounds.Lower.z +
                     bounds.Upper.z) *
                    0.5f);

            LegacyWldEffectPlacement bestEffect =
                null;

            float bestEffectDistance =
                float.PositiveInfinity;

            for (int i = 0;
                 i < wld.Effects.Count;
                 i++)
            {
                LegacyWldEffectPlacement effect =
                    wld.Effects[i];

                if (effect.EffectId != 0)
                    continue;

                Vector2 delta =
                    new Vector2(
                        effect.Position.x -
                        center.x,
                        effect.Position.z -
                        center.z);

                float distance =
                    delta.sqrMagnitude;

                if (distance <
                    bestEffectDistance)
                {
                    bestEffectDistance =
                        distance;

                    bestEffect =
                        effect;
                }
            }

            if (bestEffect != null)
            {
                source =
                    "Login.wld effect sequence 0";

                return new Vector3(
                    bestEffect.Position.x,
                    bounds.Lower.y,
                    bestEffect.Position.z);
            }

            int starNameIndex =
                -1;

            for (int i = 0;
                 i < wld.Shapes.Names.Count;
                 i++)
            {
                if (string.Equals(
                        Path.GetFileName(
                            wld.Shapes.Names[i]),
                        "Starlighting.SMOD",
                        StringComparison.OrdinalIgnoreCase))
                {
                    starNameIndex =
                        i;

                    break;
                }
            }

            if (starNameIndex >= 0)
            {
                for (int i = 0;
                     i < wld.Shapes.Coordinates.Count;
                     i++)
                {
                    LegacyWldCoordinate coordinate =
                        wld.Shapes.Coordinates[i];

                    if (coordinate.Id !=
                        starNameIndex)
                        continue;

                    source =
                        "Login.wld Starlighting.SMOD";

                    return new Vector3(
                        coordinate.Position.x,
                        bounds.Lower.y,
                        coordinate.Position.z);
                }
            }

            source =
                "DG bounding-box center";

            return center;
        }

        public static int AttachRuntimeEffects(
            CanonicalClientCorpus corpus,
            LegacyDungeonPreviewBuildResult environment,
            Transform observer)
        {
            if (corpus == null)
                throw new ArgumentNullException(nameof(corpus));

            if (environment == null ||
                environment.World == null ||
                environment.Root == null)
            {
                throw new ArgumentNullException(
                    nameof(environment));
            }

            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            LegacyWldTerrainFile wld =
                environment.World;

            if (wld.Effects.Count == 0)
                return 0;

            if (string.IsNullOrWhiteSpace(
                    wld.EffectName))
            {
                throw new InvalidDataException(
                    "Login DUN contains EFT placements but no EffectName.");
            }

            LegacyEftFile library =
                LegacyEftPrefabImporter
                    .ParseCanonical(
                        corpus,
                        wld.EffectName);

            if (library == null)
            {
                throw new FileNotFoundException(
                    "Login DUN EFT library could not be resolved: " +
                    wld.EffectName);
            }

            GameObject prefab =
                LegacyEftPrefabImporter.Import(
                    corpus,
                    wld.EffectName);

            if (prefab == null)
            {
                throw new InvalidDataException(
                    "Login DUN EFT importer returned null for '" +
                    wld.EffectName +
                    "'.");
            }

            var placements =
                new System.Collections.Generic.List<
                    LegacyWorldEffectPlacement>(
                        wld.Effects.Count);

            for (int i = 0;
                 i < wld.Effects.Count;
                 i++)
            {
                LegacyWldEffectPlacement source =
                    wld.Effects[i];

                if (source.EffectId < 0 ||
                    source.EffectId >=
                    library.Sequences.Count)
                {
                    throw new InvalidDataException(
                        "Login DUN effect placement " +
                        i +
                        " references sequence " +
                        source.EffectId +
                        " but '" +
                        wld.EffectName +
                        "' contains " +
                        library.Sequences.Count +
                        " sequences.");
                }

                placements.Add(
                    new LegacyWorldEffectPlacement
                    {
                        sequenceIndex =
                            source.EffectId,
                        position =
                            source.Position,
                        rotation =
                            RotationFromBasis(
                                source.Forward,
                                source.Up,
                                "Effect",
                                i),
                        effectPrefab =
                            prefab
                    });
            }

            GameObject runtime =
                new GameObject(
                    "WLD_Login_Effects_Runtime");

            runtime.transform.SetParent(
                environment.Root.transform,
                false);

            LegacyWorldEffectStreamer streamer =
                runtime.AddComponent<
                    LegacyWorldEffectStreamer>();

            streamer.Configure(
                observer,
                placements);

            return placements.Count;
        }

        private static int CreateStaticGroup(
            CanonicalClientCorpus corpus,
            Transform parent,
            string label,
            string category,
            LegacyWldNameCoordinateGroup group,
            bool alphaClip)
        {
            if (group == null ||
                group.Coordinates.Count == 0)
                return 0;

            GameObject container =
                new GameObject(
                    "WLD_" + label);

            container.transform.SetParent(
                parent,
                false);

            GameObject[] prefabs =
                new GameObject[
                    group.Names.Count];

            for (int i = 0;
                 i < group.Names.Count;
                 i++)
            {
                prefabs[i] =
                    LegacySmodPrefabImporter.Import(
                        corpus,
                        category,
                        group.Names[i],
                        alphaClip);

                if (prefabs[i] == null)
                {
                    throw new InvalidDataException(
                        "DUN WLD " +
                        label +
                        " resource could not be imported: " +
                        group.Names[i] +
                        ".");
                }
            }

            StaticEditorFlags flags =
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ReflectionProbeStatic;

            for (int i = 0;
                 i < group.Coordinates.Count;
                 i++)
            {
                LegacyWldCoordinate coordinate =
                    group.Coordinates[i];

                if (coordinate.Id < 0 ||
                    coordinate.Id >=
                    prefabs.Length)
                {
                    throw new InvalidDataException(
                        "DUN WLD " +
                        label +
                        " coordinate " +
                        i +
                        " references invalid resource " +
                        coordinate.Id +
                        ".");
                }

                GameObject instance =
                    PrefabUtility.InstantiatePrefab(
                        prefabs[
                            coordinate.Id])
                    as GameObject;

                if (instance == null)
                {
                    throw new InvalidOperationException(
                        "Could not instantiate DUN WLD " +
                        label +
                        " resource " +
                        group.Names[
                            coordinate.Id] +
                        ".");
                }

                instance.name =
                    Path.GetFileNameWithoutExtension(
                        group.Names[
                            coordinate.Id]) +
                    "_" +
                    i.ToString("D4");

                instance.transform.SetParent(
                    container.transform,
                    false);

                instance.transform.position =
                    coordinate.Position;

                instance.transform.rotation =
                    RotationFromBasis(
                        coordinate.Forward,
                        coordinate.Up,
                        label,
                        i);

                ApplyStaticFlagsRecursively(
                    instance,
                    flags);
            }

            return group.Coordinates.Count;
        }

        private static Quaternion RotationFromBasis(
            Vector3 forward,
            Vector3 up,
            string label,
            int ordinal)
        {
            if (forward.sqrMagnitude <
                    0.000001f ||
                up.sqrMagnitude <
                    0.000001f)
            {
                throw new InvalidDataException(
                    "DUN WLD " +
                    label +
                    " coordinate " +
                    ordinal +
                    " has a degenerate basis.");
            }

            forward.Normalize();
            up.Normalize();

            if (Mathf.Abs(
                    Vector3.Dot(
                        forward,
                        up)) >
                0.01f)
            {
                Vector3 right =
                    Vector3.Cross(
                        up,
                        forward)
                    .normalized;

                if (right.sqrMagnitude <
                    0.999f)
                {
                    throw new InvalidDataException(
                        "DUN WLD " +
                        label +
                        " coordinate " +
                        ordinal +
                        " has an unusable basis.");
                }

                up =
                    Vector3.Cross(
                        forward,
                        right)
                    .normalized;
            }

            return Quaternion.LookRotation(
                forward,
                up);
        }

        private static void ApplyFog(
            LegacyWldTerrainFile wld)
        {
            if (wld == null)
                return;

            float start =
                wld.Unknown5;

            float end =
                wld.Unknown6;

            if (float.IsNaN(start) ||
                float.IsNaN(end) ||
                float.IsInfinity(start) ||
                float.IsInfinity(end) ||
                end <= start ||
                end <= 0f)
            {
                RenderSettings.fog =
                    false;

                return;
            }

            Vector3 fog =
                wld.Point3;

            RenderSettings.fog =
                true;

            RenderSettings.fogMode =
                FogMode.Linear;

            RenderSettings.fogStartDistance =
                Mathf.Max(
                    0f,
                    start);

            RenderSettings.fogEndDistance =
                end;

            RenderSettings.fogColor =
                new Color(
                    NormalizeLegacyColor(
                        fog.x),
                    NormalizeLegacyColor(
                        fog.y),
                    NormalizeLegacyColor(
                        fog.z),
                    1f);
        }

        private static float NormalizeLegacyColor(
            float value)
        {
            if (value > 1f)
                value /= 255f;

            return Mathf.Clamp01(
                value);
        }

        private static void ApplyStaticFlagsRecursively(
            GameObject root,
            StaticEditorFlags flags)
        {
            if (root == null)
                return;

            Transform[] transforms =
                root.GetComponentsInChildren<
                    Transform>(
                        true);

            for (int i = 0;
                 i < transforms.Length;
                 i++)
            {
                GameObjectUtility
                    .SetStaticEditorFlags(
                        transforms[i]
                            .gameObject,
                        flags);
            }
        }
    }
}
