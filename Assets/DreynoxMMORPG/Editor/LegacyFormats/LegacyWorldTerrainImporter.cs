using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.App;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Gameplay.CameraSystem;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Parity;
using Dreynox.Mmorpg.ParityCore;
using Dreynox.Mmorpg.UI;
using Dreynox.Mmorpg.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public static class LegacyWorldTerrainImporter
    {
        private const int MapId = 0;

        private const string OutputRoot =
            "Assets/DreynoxMMORPG/LocalLegacyGenerated/World/Map000";

        private const string CharacterPrefabPath =
            "Assets/DreynoxMMORPG/LocalLegacyGenerated/" +
            "Characters/HumanMale003/Prefabs/" +
            "HumanMale003_Canonical.prefab";

        public const string ScenePath =
            "Assets/DreynoxMMORPG/Game/Scenes/Generated/" +
            "CanonicalMap000World.unity";

        [MenuItem(
            "Dreynox MMORPG/Client Parity/" +
            "Build Canonical Map 0 World")]
        public static void BuildCanonicalMap0()
        {
            CanonicalClientCorpus corpus =
                CanonicalClientCorpus.FromStoredRoot();

            if (corpus == null ||
                !corpus.Validate().IsCanonical)
            {
                throw new InvalidOperationException(
                    "Configure the canonical ps0032 corpus first.");
            }

            string wldPath =
                ResolveCaseInsensitive(
                    corpus.RootPath,
                    "DATA_Español/world/0.wld");

            string svmapPath =
                ResolveCaseInsensitive(
                    corpus.RootPath,
                    "DATA_Español/world/0.svmap");

            LegacyWldTerrainFile wld =
                LegacyWldTerrainParser.Parse(wldPath);

            LegacySvmapFile svmap =
                LegacySvmapParser.Parse(svmapPath);

            ValidateMapZeroBaseline(wld, svmap);

            EnsureFolder(OutputRoot);
            EnsureFolder(OutputRoot + "/Textures");
            EnsureFolder(OutputRoot + "/TerrainLayers");
            EnsureFolder(OutputRoot + "/TerrainData");
            EnsureFolder(
                "Assets/DreynoxMMORPG/Game/Scenes/Generated");

            TerrainLayer[] layers =
                ImportTerrainLayers(corpus, wld);

            TerrainData terrainData =
                BuildTerrainData(wld, layers);

            string terrainDataPath =
                OutputRoot + "/TerrainData/Map000.asset";

            AssetDatabase.DeleteAsset(terrainDataPath);
            AssetDatabase.CreateAsset(
                terrainData,
                terrainDataPath);

            LegacyCharacterImporter.ImportCanonicalHumanMale003();

            Scene scene =
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);

            GameObject runtime =
                new GameObject("DreynoxRuntime");

            runtime.AddComponent<DreynoxBootstrap>();
            runtime.AddComponent<ParityScreenshotCapture>();

            GameObject terrainObject =
                Terrain.CreateTerrainGameObject(terrainData);

            terrainObject.name = "Map000_Terrain";
            terrainObject.transform.position =
                new Vector3(
                    0f,
                    (float)LegacyTerrainHeightCore.WorldYOffset,
                    0f);

            Terrain terrain =
                terrainObject.GetComponent<Terrain>();

            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 1200f;

            LegacySmodPrefabImporter.ClearSessionCache();

            GameObject staticRoot =
                new GameObject("WLD_StaticGeometry");

            int buildingInstances =
                CreateStaticGroup(
                    corpus,
                    staticRoot.transform,
                    "Buildings",
                    "building",
                    wld.Buildings,
                    alphaClip: false);

            int shapeInstances =
                CreateStaticGroup(
                    corpus,
                    staticRoot.transform,
                    "Shapes",
                    "shape",
                    wld.Shapes,
                    alphaClip: false);

            int treeInstances =
                CreateStaticGroup(
                    corpus,
                    staticRoot.transform,
                    "Trees",
                    "tree",
                    wld.Trees,
                    alphaClip: true);

            GameObject markerRoot =
                new GameObject("SVMAP_Metadata");

            CreatePortalMarkers(
                markerRoot.transform,
                svmap);

            CreateNpcMarkers(
                markerRoot.transform,
                svmap);

            CreateMonsterAreaMarkers(
                markerRoot.transform,
                svmap);

            CreateSpawnMarkers(
                markerRoot.transform,
                svmap);

            GameObject actor =
                InstantiateCanonicalActor();

            Vector3 spawn =
                ResolveAllianceSpawn(
                    svmap,
                    terrain);

            actor.transform.position = spawn;
            actor.name = "Player_HumanMale003";

            ShaiyaClientActor clientActor =
                actor.GetComponent<ShaiyaClientActor>();

            if (clientActor == null)
                throw new InvalidOperationException(
                    "Canonical character prefab has no ShaiyaClientActor.");

            GameObject cameraObject =
                new GameObject("Main Camera");

            cameraObject.tag = "MainCamera";

            Camera camera =
                cameraObject.AddComponent<Camera>();

            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 3500f;

            cameraObject.AddComponent<AudioListener>();

            ShaiyaThirdPersonCamera cameraController =
                cameraObject.AddComponent<ShaiyaThirdPersonCamera>();

            cameraController.SetTarget(actor.transform);
            clientActor.SetCameraReference(
                cameraObject.transform);

            cameraObject.transform.position =
                actor.transform.position +
                new Vector3(0f, 3f, -6.5f);

            CreateLighting();

            GameObject hudObject =
                new GameObject("ParityHUD");

            ParityDebugHud hud =
                hudObject.AddComponent<ParityDebugHud>();

            hud.Bind(clientActor);

            EditorSceneManager.SaveScene(
                scene,
                ScenePath);

            Selection.activeObject = terrainObject;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "Dreynox MMORPG: canonical Map 0 generated from 0.wld + " +
                "0.svmap. Terrain " + wld.MapSize + "x" + wld.MapSize +
                ", " + wld.Resolution + " height samples, " +
                wld.Textures.Count + " terrain layers, " +
                svmap.Portals.Count + " portals, " +
                svmap.NpcPositionCount + " NPC positions, " +
                svmap.MonsterAreas.Count + " monster areas / " +
                svmap.MonsterInstanceCount + " monster instances, " +
                buildingInstances + " buildings, " +
                shapeInstances + " shapes and " +
                treeInstances + " trees.");
        }

        private static TerrainData BuildTerrainData(
            LegacyWldTerrainFile wld,
            TerrainLayer[] layers)
        {
            TerrainData data =
                new TerrainData
                {
                    name = "Map000_TerrainData",
                    heightmapResolution = wld.Resolution,
                    alphamapResolution = wld.Resolution - 1,
                    baseMapResolution = 1024,
                    size = new Vector3(
                        wld.MapSize,
                        (float)LegacyTerrainHeightCore.FullHeightRange,
                        wld.MapSize),
                    terrainLayers = layers
                };

            float[,] heights =
                new float[wld.Resolution, wld.Resolution];

            for (int z = 0; z < wld.Resolution; z++)
            for (int x = 0; x < wld.Resolution; x++)
            {
                heights[z, x] =
                    LegacyTerrainHeightCore.Normalize(
                        wld.RawHeightAt(x, z));
            }

            data.SetHeights(0, 0, heights);

            float[,,] alphamaps =
                BuildAlphamaps(wld);

            data.SetAlphamaps(
                0,
                0,
                alphamaps);

            return data;
        }

        private static float[,,] BuildAlphamaps(
            LegacyWldTerrainFile wld)
        {
            int resolution = wld.Resolution - 1;
            int layerCount = wld.Textures.Count;

            float[,,] alpha =
                new float[resolution, resolution, layerCount];

            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                byte a = wld.TextureIndexAt(x, z);
                byte b = wld.TextureIndexAt(x + 1, z);
                byte c = wld.TextureIndexAt(x, z + 1);
                byte d = wld.TextureIndexAt(x + 1, z + 1);

                AddTextureWeight(alpha, z, x, a, layerCount, 0.25f);
                AddTextureWeight(alpha, z, x, b, layerCount, 0.25f);
                AddTextureWeight(alpha, z, x, c, layerCount, 0.25f);
                AddTextureWeight(alpha, z, x, d, layerCount, 0.25f);

                float sum = 0f;
                for (int layer = 0; layer < layerCount; layer++)
                    sum += alpha[z, x, layer];

                if (sum <= 0.000001f)
                {
                    alpha[z, x, 0] = 1f;
                }
                else if (Mathf.Abs(sum - 1f) > 0.000001f)
                {
                    for (int layer = 0; layer < layerCount; layer++)
                        alpha[z, x, layer] /= sum;
                }
            }

            return alpha;
        }

        private static void AddTextureWeight(
            float[,,] alpha,
            int z,
            int x,
            byte index,
            int layerCount,
            float weight)
        {
            if (index == byte.MaxValue ||
                index >= layerCount)
                return;

            alpha[z, x, index] += weight;
        }

        private static TerrainLayer[] ImportTerrainLayers(
            CanonicalClientCorpus corpus,
            LegacyWldTerrainFile wld)
        {
            TerrainLayer[] layers =
                new TerrainLayer[wld.Textures.Count];

            for (int i = 0; i < wld.Textures.Count; i++)
            {
                LegacyWldTexture source = wld.Textures[i];

                string canonicalPath =
                    ResolveCaseInsensitive(
                        corpus.RootPath,
                        "DATA_Español/terrain/detail/" +
                        source.TextureName);

                if (!File.Exists(canonicalPath))
                    throw new FileNotFoundException(
                        "WLD terrain texture not found: " +
                        source.TextureName,
                        canonicalPath);

                string textureAssetPath =
                    OutputRoot + "/Textures/" +
                    Path.GetFileName(canonicalPath)
                        .ToLowerInvariant();

                string absoluteDestination =
                    Path.GetFullPath(textureAssetPath);

                Directory.CreateDirectory(
                    Path.GetDirectoryName(absoluteDestination));

                File.Copy(
                    canonicalPath,
                    absoluteDestination,
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
                    importer.textureCompression =
                        TextureImporterCompression.CompressedHQ;
                    importer.SaveAndReimport();
                }

                Texture2D texture =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(
                        textureAssetPath);

                TerrainLayer layer =
                    new TerrainLayer
                    {
                        name =
                            "Map000_" +
                            i.ToString("D2") +
                            "_" +
                            Path.GetFileNameWithoutExtension(
                                source.TextureName),
                        diffuseTexture = texture,
                        tileSize =
                            new Vector2(
                                source.TileSize,
                                source.TileSize)
                    };

                string layerPath =
                    OutputRoot +
                    "/TerrainLayers/Layer_" +
                    i.ToString("D2") +
                    ".terrainlayer";

                AssetDatabase.DeleteAsset(layerPath);
                AssetDatabase.CreateAsset(layer, layerPath);
                layers[i] = layer;
            }

            return layers;
        }

        private static void ValidateMapZeroBaseline(
            LegacyWldTerrainFile wld,
            LegacySvmapFile svmap)
        {
            if (wld.MapSize != 2048 ||
                wld.Resolution != 1025)
            {
                throw new InvalidDataException(
                    "Canonical Map 0 terrain dimensions changed.");
            }

            if (wld.Textures.Count != 7)
                throw new InvalidDataException(
                    "Canonical Map 0 must declare 7 terrain textures.");

            LegacyWorldPopulation expected =
                LegacyWorldPopulationCore.Get(MapId);

            if (svmap.Portals.Count != expected.Portals)
                throw new InvalidDataException(
                    "Map 0 portal count mismatch.");

            if (svmap.MonsterAreas.Count != expected.MobAreas)
                throw new InvalidDataException(
                    "Map 0 monster area count mismatch.");

            if (svmap.MonsterInstanceCount != expected.Mobs)
                throw new InvalidDataException(
                    "Map 0 monster instance count mismatch.");

            // Verified raw map sample → NPC world height evidence.
            int sampleX =
                LegacyTerrainHeightCore.WorldToSampleIndex(
                    184.4588165283203,
                    wld.MapSize);

            int sampleZ =
                LegacyTerrainHeightCore.WorldToSampleIndex(
                    1826.923583984375,
                    wld.MapSize);

            ushort raw =
                wld.RawHeightAt(sampleX, sampleZ);

            double decoded =
                LegacyTerrainHeightCore.Decode(raw);

            if (raw != 11268 ||
                Math.Abs(decoded - 25.36) > 0.0001)
            {
                throw new InvalidDataException(
                    "Map 0 terrain height calibration mismatch.");
            }
        }

        private static int CreateStaticGroup(
            CanonicalClientCorpus corpus,
            Transform worldRoot,
            string groupName,
            string category,
            LegacyWldNameCoordinateGroup group,
            bool alphaClip)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            GameObject container =
                new GameObject(groupName);

            container.transform.SetParent(
                worldRoot,
                false);

            GameObject[] prefabs =
                new GameObject[group.Names.Count];

            for (int i = 0; i < group.Names.Count; i++)
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
                        "SMOD import returned null for " +
                        category + "/" + group.Names[i] + ".");
                }
            }

            StaticEditorFlags staticFlags =
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
                    coordinate.Id >= prefabs.Length)
                {
                    throw new InvalidDataException(
                        groupName + " coordinate " + i +
                        " references invalid resource " +
                        coordinate.Id + ".");
                }

                GameObject instance =
                    PrefabUtility.InstantiatePrefab(
                        prefabs[coordinate.Id])
                    as GameObject;

                if (instance == null)
                {
                    throw new InvalidOperationException(
                        "Could not instantiate " +
                        group.Names[coordinate.Id] + ".");
                }

                instance.name =
                    Path.GetFileNameWithoutExtension(
                        group.Names[coordinate.Id]) +
                    "_" +
                    i.ToString("D4");

                instance.transform.SetParent(
                    container.transform,
                    false);

                instance.transform.position =
                    coordinate.Position;

                Vector3 forward =
                    coordinate.Forward.normalized;

                Vector3 up =
                    coordinate.Up.normalized;

                if (forward.sqrMagnitude < 0.999f ||
                    up.sqrMagnitude < 0.999f ||
                    Mathf.Abs(Vector3.Dot(forward, up)) > 0.01f)
                {
                    Vector3 right =
                        Vector3.Cross(up, forward).normalized;

                    if (right.sqrMagnitude < 0.999f)
                    {
                        throw new InvalidDataException(
                            groupName +
                            " coordinate " + i +
                            " has an unusable orientation basis.");
                    }

                    up =
                        Vector3.Cross(forward, right).normalized;
                }

                instance.transform.rotation =
                    Quaternion.LookRotation(
                        forward,
                        up);

                ApplyStaticFlagsRecursively(
                    instance,
                    staticFlags);
            }

            return group.Coordinates.Count;
        }

        private static void ApplyStaticFlagsRecursively(
            GameObject root,
            StaticEditorFlags flags)
        {
            if (root == null)
                return;

            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(
                    true);

            for (int i = 0; i < transforms.Length; i++)
            {
                GameObjectUtility.SetStaticEditorFlags(
                    transforms[i].gameObject,
                    flags);
            }
        }

        private static void CreatePortalMarkers(
            Transform root,
            LegacySvmapFile svmap)
        {
            GameObject group =
                new GameObject("Portals");

            group.transform.SetParent(root, false);

            for (int i = 0; i < svmap.Portals.Count; i++)
            {
                LegacySvmapPortal portal = svmap.Portals[i];

                GameObject go =
                    new GameObject(
                        "Portal_" +
                        i.ToString("D3") +
                        "_ToMap_" +
                        portal.TargetMapId);

                go.transform.SetParent(group.transform, false);
                go.transform.position = portal.Position;

                LegacyWorldMarker marker =
                    go.AddComponent<LegacyWorldMarker>();

                marker.Configure(
                    LegacyWorldMarkerKind.Portal,
                    MapId,
                    (int)portal.TargetMapId,
                    portal.FactionOrPortalId,
                    1,
                    0f,
                    Vector3.zero,
                    Vector3.zero);
            }
        }

        private static void CreateNpcMarkers(
            Transform root,
            LegacySvmapFile svmap)
        {
            GameObject group =
                new GameObject("NPCs");

            group.transform.SetParent(root, false);

            int ordinal = 0;

            for (int i = 0; i < svmap.Npcs.Count; i++)
            {
                LegacySvmapNpc npc = svmap.Npcs[i];

                for (int p = 0; p < npc.Positions.Count; p++)
                {
                    LegacySvmapNpcPosition position =
                        npc.Positions[p];

                    GameObject go =
                        new GameObject(
                            "NPC_" +
                            npc.NpcType +
                            "_" +
                            npc.NpcId +
                            "_" +
                            ordinal.ToString("D3"));

                    ordinal++;

                    go.transform.SetParent(group.transform, false);
                    go.transform.position = position.Position;
                    go.transform.rotation =
                        Quaternion.Euler(
                            0f,
                            position.Yaw * Mathf.Rad2Deg,
                            0f);

                    LegacyWorldMarker marker =
                        go.AddComponent<LegacyWorldMarker>();

                    marker.Configure(
                        LegacyWorldMarkerKind.Npc,
                        MapId,
                        npc.NpcType,
                        npc.NpcId,
                        1,
                        position.Yaw,
                        Vector3.zero,
                        Vector3.zero);
                }
            }
        }

        private static void CreateMonsterAreaMarkers(
            Transform root,
            LegacySvmapFile svmap)
        {
            GameObject group =
                new GameObject("MonsterAreas");

            group.transform.SetParent(root, false);

            for (int i = 0; i < svmap.MonsterAreas.Count; i++)
            {
                LegacySvmapMonsterArea area =
                    svmap.MonsterAreas[i];

                Vector3 center =
                    (area.Area.Lower + area.Area.Upper) * 0.5f;

                Vector3 size =
                    area.Area.Upper - area.Area.Lower;

                int count = 0;
                int firstMobId = 0;

                for (int m = 0; m < area.Monsters.Count; m++)
                {
                    if (m == 0)
                        firstMobId = (int)area.Monsters[m].MobId;

                    count += (int)area.Monsters[m].Count;
                }

                GameObject go =
                    new GameObject(
                        "MobArea_" +
                        i.ToString("D4"));

                go.transform.SetParent(group.transform, false);
                go.transform.position = center;

                LegacyWorldMarker marker =
                    go.AddComponent<LegacyWorldMarker>();

                marker.Configure(
                    LegacyWorldMarkerKind.MonsterArea,
                    MapId,
                    firstMobId,
                    area.Monsters.Count,
                    count,
                    0f,
                    size,
                    Vector3.zero);
            }
        }

        private static void CreateSpawnMarkers(
            Transform root,
            LegacySvmapFile svmap)
        {
            GameObject group =
                new GameObject("Spawns");

            group.transform.SetParent(root, false);

            for (int i = 0; i < svmap.Spawns.Count; i++)
            {
                LegacySvmapSpawnArea spawn =
                    svmap.Spawns[i];

                Vector3 center =
                    (spawn.Area.Lower + spawn.Area.Upper) * 0.5f;

                Vector3 size =
                    spawn.Area.Upper - spawn.Area.Lower;

                GameObject go =
                    new GameObject(
                        "Spawn_Faction_" +
                        spawn.Faction);

                go.transform.SetParent(group.transform, false);
                go.transform.position = center;

                LegacyWorldMarker marker =
                    go.AddComponent<LegacyWorldMarker>();

                marker.Configure(
                    LegacyWorldMarkerKind.Spawn,
                    MapId,
                    spawn.Faction,
                    spawn.Unknown1,
                    1,
                    0f,
                    size,
                    Vector3.zero);
            }
        }

        private static Vector3 ResolveAllianceSpawn(
            LegacySvmapFile svmap,
            Terrain terrain)
        {
            LegacySvmapSpawnArea? selected = null;

            for (int i = 0; i < svmap.Spawns.Count; i++)
            {
                if (svmap.Spawns[i].Faction == 1)
                {
                    selected = svmap.Spawns[i];
                    break;
                }
            }

            if (!selected.HasValue &&
                svmap.Spawns.Count > 0)
            {
                selected = svmap.Spawns[0];
            }

            Vector3 position =
                selected.HasValue
                    ? (selected.Value.Area.Lower +
                       selected.Value.Area.Upper) * 0.5f
                    : new Vector3(220f, 25f, 1838f);

            float terrainY =
                terrain.SampleHeight(position) +
                terrain.transform.position.y;

            position.y = terrainY + 0.05f;
            return position;
        }

        private static GameObject InstantiateCanonicalActor()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    CharacterPrefabPath);

            if (prefab == null)
                throw new InvalidOperationException(
                    "Canonical character prefab was not generated.");

            GameObject instance =
                PrefabUtility.InstantiatePrefab(prefab)
                as GameObject;

            if (instance == null)
                throw new InvalidOperationException(
                    "Unable to instantiate canonical character prefab.");

            return instance;
        }

        private static void CreateLighting()
        {
            GameObject sun =
                new GameObject("Sun");

            Light light =
                sun.AddComponent<Light>();

            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.shadows = LightShadows.Soft;

            sun.transform.rotation =
                Quaternion.Euler(
                    48f,
                    -32f,
                    0f);

            RenderSettings.ambientMode =
                UnityEngine.Rendering.AmbientMode.Trilight;

            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 450f;
            RenderSettings.fogEndDistance = 1500f;
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
                    AssetDatabase.CreateFolder(
                        current,
                        parts[i]);

                current = next;
            }
        }
    }
}
