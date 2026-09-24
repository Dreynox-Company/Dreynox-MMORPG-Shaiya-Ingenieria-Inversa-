using System;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
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

            Vector3 previewAnchor =
                new Vector3(
                    (bounds.Lower.x +
                     bounds.Upper.x) *
                    0.5f,
                    bounds.Lower.y,
                    (bounds.Lower.z +
                     bounds.Upper.z) *
                    0.5f);

            return new LegacyDungeonPreviewBuildResult
            {
                Root = root,
                World = wld,
                Dungeon = dg,
                PreviewAnchor = previewAnchor,
                BuildingInstances = buildings,
                ShapeInstances = shapes,
                TreeInstances = trees,
                GrassInstances = grass
            };
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
