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
using UnityEngine.Rendering;

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

            string dbMonsterDataPath =
                ResolveCaseInsensitive(
                    corpus.RootPath,
                    "DATA_Español/binarysdata/dbmonsterdata.sdata");

            string dbMonsterTextPath =
                ResolveCaseInsensitive(
                    corpus.RootPath,
                    "DATA_Español/binarysdata/dbmonstertext_spn.sdata");

            string monsterMonPath =
                ResolveCaseInsensitive(
                    corpus.RootPath,
                    "DATA_Español/monster/monster.mon");

            LegacyDbMonsterDataFile monsterData =
                LegacyDbMonsterDataParser.ParseData(
                    dbMonsterDataPath);

            LegacyDbMonsterTextFile monsterText =
                LegacyDbMonsterDataParser.ParseText(
                    dbMonsterTextPath);

            LegacyMonFile monsterModels =
                LegacyMonParser.Parse(monsterMonPath);

            if (monsterData.Records.Count != 5008 ||
                monsterText.Count != 5008 ||
                monsterModels.Records.Count != 863)
            {
                throw new InvalidDataException(
                    "Canonical monster catalogs changed. " +
                    "DBData=" + monsterData.Records.Count +
                    ", DBText=" + monsterText.Count +
                    ", MON=" + monsterModels.Records.Count + ".");
            }

            string npcQuestPath =
                ResolveCaseInsensitive(
                    corpus.RootPath,
                    "DATA_Español/npc/npcquest.sdata");

            string npcMonPath =
                ResolveCaseInsensitive(
                    corpus.RootPath,
                    "DATA_Español/npc/npc.mon");

            LegacyNpcQuestHeaderFile npcDefinitions =
                LegacyNpcQuestHeaderParser.ParseEncrypted(
                    npcQuestPath,
                    validateChecksum: true);

            string npcTranslationPath =
                ResolveCaseInsensitive(
                    corpus.RootPath,
                    "DATA_Español/npc/npcquesttrans_spain.sdata");

            LegacyNpcQuestTranslationHeaderFile npcTranslations =
                LegacyNpcQuestTranslationParser.Parse(
                    npcTranslationPath,
                    npcDefinitions);

            LegacyMonFile npcModels =
                LegacyMonParser.Parse(npcMonPath);

            if (npcDefinitions.Definitions.Count != 2394 ||
                npcTranslations.Translations.Count != 2394 ||
                npcTranslations.NpcTranslationBytesConsumed != 219081 ||
                npcTranslations.QuestTranslationCount != 4085 ||
                npcModels.Records.Count != 264)
            {
                throw new InvalidDataException(
                    "Canonical NPC catalogs changed. Definitions=" +
                    npcDefinitions.Definitions.Count +
                    ", translations=" +
                    npcTranslations.Translations.Count +
                    ", translationOffset=" +
                    npcTranslations.NpcTranslationBytesConsumed +
                    ", questTranslations=" +
                    npcTranslations.QuestTranslationCount +
                    ", MON=" + npcModels.Records.Count + ".");
            }

            ValidateMapZeroBaseline(wld, svmap);

            EnsureFolder(OutputRoot);
            EnsureFolder(OutputRoot + "/Textures");
            EnsureFolder(OutputRoot + "/TerrainLayers");
            EnsureFolder(OutputRoot + "/TerrainData");
            EnsureFolder(OutputRoot + "/Water");
            EnsureFolder(OutputRoot + "/Water/Frames");
            EnsureFolder(OutputRoot + "/Water/Materials");
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

            LightmapSettings.lightmapsMode =
                LightmapsMode.NonDirectional;

            LightmapSettings.lightmaps =
                Array.Empty<LightmapData>();

            GameObject runtime =
                new GameObject("DreynoxRuntime");

            runtime.AddComponent<DreynoxBootstrap>();
            runtime.AddComponent<ParityScreenshotCapture>();
            runtime.AddComponent<LegacyNpcInteractionRuntime>();

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

            LegacyWaterSurface waterSurface =
                CreateCanonicalWaterSurface(
                    corpus,
                    wld);

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

            int dungeonInstances =
                CreateDungeonInstances(
                    corpus,
                    staticRoot.transform,
                    wld.Dungeons);

            int rotatingManiInstances;

            int maniInstances =
                CreateManiInstances(
                    corpus,
                    staticRoot.transform,
                    wld,
                    out rotatingManiInstances);

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

            int uniqueMonsterModels;
            List<LegacyMonsterSpawnDefinition> monsterSpawns =
                BuildMonsterSpawnDefinitions(
                    corpus,
                    svmap,
                    terrain,
                    monsterData,
                    monsterText,
                    monsterModels,
                    out uniqueMonsterModels);

            GameObject monsterRuntime =
                new GameObject("SVMAP_Monsters_Runtime");

            LegacyMonsterSpawnStreamer monsterStreamer =
                monsterRuntime.AddComponent<LegacyMonsterSpawnStreamer>();

            monsterStreamer.Configure(
                actor.transform,
                monsterSpawns);

            int resolvedNpcDefinitions;
            int unresolvedNpcDefinitions;
            int uniqueNpcModels;

            List<LegacyNpcSpawnDefinition> npcSpawns =
                BuildNpcSpawnDefinitions(
                    corpus,
                    svmap,
                    npcDefinitions,
                    npcTranslations,
                    npcModels,
                    out resolvedNpcDefinitions,
                    out unresolvedNpcDefinitions,
                    out uniqueNpcModels);

            if (resolvedNpcDefinitions != 141 ||
                unresolvedNpcDefinitions != 9 ||
                npcSpawns.Count != 180 ||
                uniqueNpcModels != 41)
            {
                throw new InvalidDataException(
                    "Canonical Map 0 NPC resolution changed. " +
                    "resolvedDefs=" + resolvedNpcDefinitions +
                    ", unresolvedDefs=" + unresolvedNpcDefinitions +
                    ", positions=" + npcSpawns.Count +
                    ", models=" + uniqueNpcModels + ".");
            }

            GameObject npcRuntime =
                new GameObject("SVMAP_NPCs_Runtime");

            LegacyNpcSpawnStreamer npcStreamer =
                npcRuntime.AddComponent<LegacyNpcSpawnStreamer>();

            npcStreamer.Configure(
                actor.transform,
                npcSpawns);

            int worldEffectPlacements =
                CreateWorldEffectStreaming(
                    corpus,
                    wld,
                    actor.transform);

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

            LegacyWorldGrassRuntime grassRuntime =
                LegacyGrassBatchBuilder.Create(
                    corpus,
                    wld.Grass,
                    actor.transform,
                    camera);

            int grassInstances =
                grassRuntime != null
                    ? grassRuntime.LogicalPlacementCount
                    : 0;

            LegacyWorldVaniRuntime vaniRuntime =
                LegacyVaniBatchBuilder.Create(
                    corpus,
                    wld.VAni1,
                    wld.VAni2,
                    actor.transform,
                    camera);

            int vani1Instances =
                wld.VAni1.Coordinates.Count;

            int vani2Instances =
                wld.VAni2.Coordinates.Count;

            int vaniInstances =
                vaniRuntime != null
                    ? vaniRuntime.LogicalPlacementCount
                    : 0;

            if (vaniInstances !=
                vani1Instances +
                vani2Instances)
            {
                throw new InvalidDataException(
                    "Canonical Map 0 VANI placement count changed. " +
                    "runtime=" + vaniInstances +
                    ", VAni1=" + vani1Instances +
                    ", VAni2=" + vani2Instances + ".");
            }

            CreateLighting();

            LegacyWorldEnvironmentBuildResult environment =
                LegacyWorldEnvironmentImporter.Create(
                    corpus,
                    wld,
                    actor.transform);

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
                uniqueMonsterModels + " unique monster models, " +
                monsterSpawns.Count + " streamed monster definitions, " +
                resolvedNpcDefinitions + " resolved NPC definitions / " +
                npcSpawns.Count + " NPC positions / " +
                uniqueNpcModels + " NPC models, " +
                buildingInstances + " buildings, " +
                shapeInstances + " shapes, " +
                treeInstances + " trees, " +
                dungeonInstances + " DG dungeon placements, " +
                maniInstances + " MAni placements / " +
                rotatingManiInstances + " rotating, " +
                grassInstances + " GPU-instanced grass placements, " +
                vaniInstances + " VANI placements (" +
                vani1Instances + " group1 / " +
                vani2Instances + " group2), " +
                worldEffectPlacements + " WLD effect placements, water=" +
                (waterSurface != null
                    ? waterSurface.FrameCount + " WTR frames / tile " +
                      waterSurface.TileSize
                    : "none") +
                ", sky='" + environment.SkyName +
                "', clouds='" + environment.CloudsName1 +
                "'/'" + environment.CloudsName2 +
                "', music=" + environment.ImportedMusicClips +
                " clips / " + environment.MusicZones +
                " zones, ambient=" +
                environment.ImportedSoundClips +
                " clips / " + environment.PositionalSounds +
                " placements.");
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

        private static LegacyWaterSurface CreateCanonicalWaterSurface(
            CanonicalClientCorpus corpus,
            LegacyWldTerrainFile wld)
        {
            if (corpus == null)
                throw new ArgumentNullException(nameof(corpus));
            if (wld == null)
                throw new ArgumentNullException(nameof(wld));

            if (string.IsNullOrWhiteSpace(wld.InnerLayout) ||
                !string.Equals(
                    Path.GetExtension(wld.InnerLayout),
                    ".wtr",
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string waterRoot =
                ResolveCaseInsensitive(
                    corpus.RootPath,
                    "DATA_Español/entity/water");

            if (!Directory.Exists(waterRoot))
            {
                throw new DirectoryNotFoundException(
                    "Canonical water directory not found: " +
                    waterRoot);
            }

            string wtrPath =
                ResolveCaseInsensitive(
                    waterRoot,
                    Path.GetFileName(wld.InnerLayout));

            if (!File.Exists(wtrPath))
            {
                throw new FileNotFoundException(
                    "WLD water layout not found: " +
                    wld.InnerLayout,
                    wtrPath);
            }

            LegacyWtrFile water =
                LegacyWtrParser.Parse(wtrPath);

            var frames =
                new List<Texture2D>();

            var seenSources =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            for (int i = 0;
                 i < water.FrameNames.Count;
                 i++)
            {
                string authored =
                    water.FrameNames[i];

                string ddsName =
                    Path.GetFileNameWithoutExtension(
                        authored) +
                    ".dds";

                string source =
                    ResolveCaseInsensitive(
                        waterRoot,
                        ddsName);

                if (!File.Exists(source))
                {
                    source =
                        ResolveCaseInsensitive(
                            waterRoot,
                            Path.GetFileName(authored));
                }

                if (!File.Exists(source))
                {
                    throw new FileNotFoundException(
                        "WTR frame '" +
                        authored +
                        "' could not be resolved as '" +
                        ddsName +
                        "' or its authored file.",
                        source);
                }

                string normalized =
                    Path.GetFullPath(source);

                if (!seenSources.Add(normalized))
                    continue;

                string assetPath =
                    OutputRoot +
                    "/Water/Frames/" +
                    frames.Count.ToString("D3") +
                    "_" +
                    Path.GetFileName(source)
                        .ToLowerInvariant();

                string destination =
                    Path.GetFullPath(assetPath);

                Directory.CreateDirectory(
                    Path.GetDirectoryName(destination));

                File.Copy(
                    source,
                    destination,
                    true);

                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceSynchronousImport);

                TextureImporter importer =
                    AssetImporter.GetAtPath(assetPath)
                    as TextureImporter;

                if (importer != null)
                {
                    importer.textureType =
                        TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    importer.mipmapEnabled = true;
                    importer.alphaIsTransparency = true;
                    importer.wrapMode =
                        TextureWrapMode.Repeat;
                    importer.filterMode =
                        FilterMode.Bilinear;
                    importer.textureCompression =
                        TextureImporterCompression.CompressedHQ;
                    importer.SaveAndReimport();
                }

                Texture2D frame =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(
                        assetPath);

                if (frame == null)
                {
                    throw new InvalidDataException(
                        "Unity did not import WTR frame '" +
                        assetPath + "'.");
                }

                frames.Add(frame);
            }

            if (frames.Count == 0)
            {
                throw new InvalidDataException(
                    "Canonical WTR contains no resolvable water frames.");
            }

            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Unlit");

            if (shader == null)
                shader =
                    Shader.Find("Unlit/Transparent");

            if (shader == null)
                throw new InvalidOperationException(
                    "No Unity shader is available for WTR water.");

            Material material =
                new Material(shader)
                {
                    name = "Map000_Water_Material",
                    renderQueue = 3000
                };

            if (material.HasProperty("_BaseMap"))
                material.SetTexture(
                    "_BaseMap",
                    frames[0]);

            if (material.HasProperty("_MainTex"))
                material.SetTexture(
                    "_MainTex",
                    frames[0]);

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);

            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat(
                    "_SrcBlend",
                    (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat(
                    "_DstBlend",
                    (float)BlendMode.OneMinusSrcAlpha);
            }

            material.SetOverrideTag(
                "RenderType",
                "Transparent");

            material.EnableKeyword(
                "_SURFACE_TYPE_TRANSPARENT");

            string materialPath =
                OutputRoot +
                "/Water/Materials/Map000_Water.mat";

            AssetDatabase.DeleteAsset(
                materialPath);

            AssetDatabase.CreateAsset(
                material,
                materialPath);

            GameObject waterObject =
                GameObject.CreatePrimitive(
                    PrimitiveType.Plane);

            waterObject.name =
                "WLD_Water_" +
                Path.GetFileNameWithoutExtension(
                    wld.InnerLayout);

            Collider collider =
                waterObject.GetComponent<Collider>();

            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    collider);
            }

            waterObject.transform.position =
                new Vector3(
                    wld.MapSize * 0.5f,
                    0f,
                    wld.MapSize * 0.5f);

            waterObject.transform.localScale =
                new Vector3(
                    wld.MapSize / 10f,
                    1f,
                    wld.MapSize / 10f);

            MeshRenderer renderer =
                waterObject.GetComponent<MeshRenderer>();

            renderer.sharedMaterial =
                material;

            renderer.shadowCastingMode =
                ShadowCastingMode.Off;

            renderer.receiveShadows =
                false;

            LegacyWaterSurface surface =
                waterObject.AddComponent<
                    LegacyWaterSurface>();

            // WTR contains no verified playback frequency. Keep frame 0 until
            // parity capture against game.exe provides an observed cadence.
            surface.Configure(
                renderer,
                frames.ToArray(),
                water.TileSize,
                framesPerSecond: 0f,
                isTimingCalibrated: false);

            return surface;
        }

        private static int CreateWorldEffectStreaming(
            CanonicalClientCorpus corpus,
            LegacyWldTerrainFile wld,
            Transform observer)
        {
            if (corpus == null)
                throw new ArgumentNullException(nameof(corpus));
            if (wld == null)
                throw new ArgumentNullException(nameof(wld));
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            if (wld.Effects.Count == 0)
                return 0;

            if (string.IsNullOrWhiteSpace(wld.EffectName))
            {
                throw new InvalidDataException(
                    "WLD declares environmental effects but EffectName is empty.");
            }

            LegacyEftFile library =
                LegacyEftPrefabImporter.ParseCanonical(
                    corpus,
                    wld.EffectName);

            if (library == null)
            {
                throw new FileNotFoundException(
                    "WLD effect library could not be resolved: " +
                    wld.EffectName);
            }

            GameObject prefab =
                LegacyEftPrefabImporter.Import(
                    corpus,
                    wld.EffectName);

            if (prefab == null)
            {
                throw new InvalidDataException(
                    "WLD EFT importer returned null for '" +
                    wld.EffectName + "'.");
            }

            var placements =
                new List<LegacyWorldEffectPlacement>(
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
                        "WLD effect placement " +
                        i +
                        " references sequence " +
                        source.EffectId +
                        " but library '" +
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
                                "WLD effect placement",
                                i),
                        effectPrefab =
                            prefab
                    });
            }

            GameObject runtime =
                new GameObject(
                    "WLD_Effects_Runtime");

            LegacyWorldEffectStreamer streamer =
                runtime.AddComponent<
                    LegacyWorldEffectStreamer>();

            streamer.Configure(
                observer,
                placements);

            return placements.Count;
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
                    label +
                    " " +
                    ordinal +
                    " has a degenerate orientation basis.");
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
                        label +
                        " " +
                        ordinal +
                        " has an unusable orientation basis.");
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

            if (!string.Equals(
                    wld.InnerLayout,
                    "World.wtr",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Canonical Map 0 WTR changed. Expected World.wtr, got '" +
                    wld.InnerLayout + "'.");
            }

            if (!string.Equals(
                    wld.SkyName,
                    "sky_A2.bmp",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Canonical Map 0 sky changed. Expected sky_A2.bmp, got '" +
                    wld.SkyName + "'.");
            }

            if (wld.UnparsedTailBytes != 0)
            {
                throw new InvalidDataException(
                    "Canonical Map 0 WLD parser left " +
                    wld.UnparsedTailBytes +
                    " unknown trailing bytes.");
            }

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

        private static int CreateDungeonInstances(
            CanonicalClientCorpus corpus,
            Transform worldRoot,
            LegacyWldNameCoordinateGroup group)
        {
            if (group == null)
                throw new ArgumentNullException(
                    nameof(group));

            if (group.Coordinates.Count == 0)
                return 0;

            GameObject container =
                new GameObject(
                    "Dungeons");

            container.transform.SetParent(
                worldRoot,
                false);

            LegacyDgImportResult[] resources =
                new LegacyDgImportResult[
                    group.Names.Count];

            int[] lightmapOffsets =
                new int[
                    group.Names.Count];

            for (int i = 0;
                 i < group.Names.Count;
                 i++)
            {
                resources[i] =
                    LegacyDgPrefabImporter
                        .ImportCanonical(
                            corpus,
                            group.Names[i],
                            OutputRoot +
                            "/Dungeons/Resource_" +
                            i.ToString("D2"));

                if (resources[i] == null ||
                    resources[i].Prefab == null ||
                    resources[i].Source == null)
                {
                    throw new InvalidDataException(
                        "DG resource import failed: " +
                        group.Names[i] +
                        ".");
                }

                lightmapOffsets[i] =
                    LegacyDgPrefabImporter
                        .AppendSceneLightmaps(
                            resources[i]
                                .Lightmaps);
            }

            StaticEditorFlags flags =
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.NavigationStatic |
                StaticEditorFlags.ReflectionProbeStatic;

            for (int i = 0;
                 i < group.Coordinates.Count;
                 i++)
            {
                LegacyWldCoordinate coordinate =
                    group.Coordinates[i];

                if (coordinate.Id < 0 ||
                    coordinate.Id >=
                        resources.Length)
                {
                    throw new InvalidDataException(
                        "DG coordinate " +
                        i +
                        " references invalid resource " +
                        coordinate.Id +
                        ".");
                }

                LegacyDgImportResult resource =
                    resources[
                        coordinate.Id];

                GameObject instance =
                    PrefabUtility.InstantiatePrefab(
                        resource.Prefab)
                    as GameObject;

                if (instance == null)
                {
                    throw new InvalidOperationException(
                        "Could not instantiate DG resource " +
                        group.Names[
                            coordinate.Id] +
                        ".");
                }

                instance.name =
                    Path.GetFileNameWithoutExtension(
                        group.Names[
                            coordinate.Id]) +
                    "_" +
                    i.ToString("D3");

                instance.transform.SetParent(
                    container.transform,
                    false);

                instance.transform.position =
                    coordinate.Position;

                instance.transform.rotation =
                    RotationFromBasis(
                        coordinate.Forward,
                        coordinate.Up,
                        "WLD dungeon",
                        i);

                LegacyDgPrefabImporter
                    .OffsetInstanceLightmapIndices(
                        instance,
                        lightmapOffsets[
                            coordinate.Id],
                        resource.Source
                            .LightmapCount);

                ApplyStaticFlagsRecursively(
                    instance,
                    flags);
            }

            return group.Coordinates.Count;
        }

        private static int CreateManiInstances(
            CanonicalClientCorpus corpus,
            Transform worldRoot,
            LegacyWldTerrainFile wld,
            out int rotatingCount)
        {
            rotatingCount = 0;

            if (wld == null ||
                wld.MAniCoordinates.Count == 0)
                return 0;

            string maniRoot =
                ResolveCaseInsensitive(
                    corpus.RootPath,
                    "DATA_Español/entity/mani");

            GameObject container =
                new GameObject(
                    "MAni");

            container.transform.SetParent(
                worldRoot,
                false);

            var prefabCache =
                new Dictionary<int, GameObject>();

            var maniCache =
                new Dictionary<int, LegacyManiFile>();

            const float LegacyTicksPerSecondCandidate =
                30f;

            for (int i = 0;
                 i < wld.MAniCoordinates.Count;
                 i++)
            {
                LegacyWldManiCoordinate coordinate =
                    wld.MAniCoordinates[i];

                if (coordinate.WorldBuildingId < 0 ||
                    coordinate.WorldBuildingId >=
                        wld.Buildings.Names.Count)
                {
                    throw new InvalidDataException(
                        "MAni coordinate " +
                        i +
                        " references BuildingAsset " +
                        coordinate.WorldBuildingId +
                        " outside 0.." +
                        (wld.Buildings.Names.Count - 1) +
                        ".");
                }

                if (coordinate.Id < 0 ||
                    coordinate.Id >=
                        wld.MAniNames.Count)
                {
                    throw new InvalidDataException(
                        "MAni coordinate " +
                        i +
                        " references descriptor " +
                        coordinate.Id +
                        " outside 0.." +
                        (wld.MAniNames.Count - 1) +
                        ".");
                }

                GameObject prefab;

                if (!prefabCache.TryGetValue(
                        coordinate.WorldBuildingId,
                        out prefab))
                {
                    string buildingName =
                        wld.Buildings.Names[
                            coordinate.WorldBuildingId];

                    prefab =
                        LegacySmodPrefabImporter.Import(
                            corpus,
                            "building",
                            buildingName,
                            alphaClip: false);

                    if (prefab == null)
                    {
                        throw new InvalidDataException(
                            "MAni building SMOD import returned null for " +
                            buildingName + ".");
                    }

                    prefabCache.Add(
                        coordinate.WorldBuildingId,
                        prefab);
                }

                LegacyManiFile descriptor;

                if (!maniCache.TryGetValue(
                        coordinate.Id,
                        out descriptor))
                {
                    string maniName =
                        wld.MAniNames[
                            coordinate.Id];

                    string maniPath =
                        CanonicalResourceIndex.FindUnique(
                            maniRoot,
                            Path.GetFileName(
                                maniName));

                    if (maniPath == null ||
                        !File.Exists(
                            maniPath))
                    {
                        throw new FileNotFoundException(
                            "MAni descriptor was not found: " +
                            maniName,
                            maniPath);
                    }

                    descriptor =
                        LegacyManiParser.Parse(
                            maniPath);

                    maniCache.Add(
                        coordinate.Id,
                        descriptor);
                }

                GameObject instance =
                    PrefabUtility.InstantiatePrefab(
                        prefab)
                    as GameObject;

                if (instance == null)
                {
                    throw new InvalidOperationException(
                        "Could not instantiate MAni building " +
                        wld.Buildings.Names[
                            coordinate.WorldBuildingId] +
                        ".");
                }

                instance.name =
                    Path.GetFileNameWithoutExtension(
                        wld.Buildings.Names[
                            coordinate.WorldBuildingId]) +
                    "_MAni_" +
                    i.ToString("D3");

                instance.transform.SetParent(
                    container.transform,
                    false);

                instance.transform.position =
                    coordinate.Position;

                instance.transform.rotation =
                    RotationFromBasis(
                        coordinate.Forward,
                        coordinate.Up,
                        "WLD MAni",
                        i);

                LegacyManiRotationRuntime runtime =
                    instance.AddComponent<
                        LegacyManiRotationRuntime>();

                runtime.Configure(
                    descriptor.RotationEnabled,
                    descriptor.RotationAxis,
                    descriptor.AnimationSpeed,
                    LegacyTicksPerSecondCandidate,
                    calibrated: false);

                if (descriptor.RotationEnabled)
                    rotatingCount++;
            }

            return wld.MAniCoordinates.Count;
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

                if (portal.TargetMapId > int.MaxValue)
                {
                    throw new InvalidDataException(
                        "Portal " + i +
                        " target map id exceeds Int32.");
                }

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

                LegacyPortalRuntime portalRuntime =
                    go.AddComponent<LegacyPortalRuntime>();

                portalRuntime.Configure(
                    MapId,
                    portal.FactionOrPortalId,
                    portal.MinLevel,
                    portal.MaxLevel,
                    (int)portal.TargetMapId,
                    portal.TargetPosition);
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

        private static List<LegacyNpcSpawnDefinition>
            BuildNpcSpawnDefinitions(
                CanonicalClientCorpus corpus,
                LegacySvmapFile svmap,
                LegacyNpcQuestHeaderFile definitions,
                LegacyNpcQuestTranslationHeaderFile translations,
                LegacyMonFile npcModels,
                out int resolvedDefinitionCount,
                out int unresolvedDefinitionCount,
                out int uniqueModelCount)
        {
            if (corpus == null)
                throw new ArgumentNullException(nameof(corpus));
            if (svmap == null)
                throw new ArgumentNullException(nameof(svmap));
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));
            if (translations == null)
                throw new ArgumentNullException(nameof(translations));
            if (npcModels == null)
                throw new ArgumentNullException(nameof(npcModels));

            var prefabs =
                new Dictionary<int, GameObject>();

            var spawns =
                new List<LegacyNpcSpawnDefinition>();

            resolvedDefinitionCount = 0;
            unresolvedDefinitionCount = 0;

            for (int definitionIndex = 0;
                 definitionIndex < svmap.Npcs.Count;
                 definitionIndex++)
            {
                LegacySvmapNpc source =
                    svmap.Npcs[definitionIndex];

                LegacyNpcDefinition definition;
                if (!definitions.TryGet(
                        source.NpcType,
                        source.NpcId,
                        out definition))
                {
                    unresolvedDefinitionCount++;

                    // Map 0 contains nine known placeholder definitions using
                    // (0,0). Anything else is a real regression.
                    if (source.NpcType != 0 ||
                        source.NpcId != 0)
                    {
                        throw new InvalidDataException(
                            "Map 0 references unresolved NPC key " +
                            source.NpcType + "/" +
                            source.NpcId + ".");
                    }

                    continue;
                }

                resolvedDefinitionCount++;

                LegacyNpcTranslation translation;
                if (!translations.TryGet(
                        source.NpcType,
                        source.NpcId,
                        out translation))
                {
                    throw new InvalidDataException(
                        "Map 0 NPC " +
                        source.NpcType + "/" +
                        source.NpcId +
                        " has no Spanish translation.");
                }

                int modelIndex =
                    definition.Model;

                if (modelIndex < 0 ||
                    modelIndex >= npcModels.Records.Count)
                {
                    throw new InvalidDataException(
                        "NPC " + source.NpcType +
                        "/" + source.NpcId +
                        " resolves model " + modelIndex +
                        " outside npc.mon.");
                }

                GameObject prefab;
                if (!prefabs.TryGetValue(
                        modelIndex,
                        out prefab))
                {
                    prefab =
                        LegacyMonPrefabImporter.Import(
                            LegacyMonCatalogKind.Npc,
                            modelIndex);

                    if (prefab == null)
                    {
                        throw new InvalidDataException(
                            "NPC MON importer returned null for model " +
                            modelIndex + ".");
                    }

                    prefabs.Add(modelIndex, prefab);
                }

                LegacyNpcGateTargetRuntime[] gateTargets =
                    new LegacyNpcGateTargetRuntime[
                        definition.GateTargets.Count];

                for (int gateIndex = 0;
                     gateIndex < definition.GateTargets.Count;
                     gateIndex++)
                {
                    LegacyNpcGateTarget target =
                        definition.GateTargets[gateIndex];

                    string gateLabel =
                        gateIndex < translation.TeleportNames.Length
                            ? translation.TeleportNames[gateIndex]
                            : string.Empty;

                    gateTargets[gateIndex] =
                        new LegacyNpcGateTargetRuntime
                        {
                            mapId = target.MapId,
                            position =
                                new Vector3(
                                    target.X,
                                    target.Y,
                                    target.Z),
                            cost = target.Cost,
                            label = gateLabel
                        };
                }

                LegacyNpcSaleItemRuntime[] saleItems =
                    definition.SaleItems
                        .Select(
                            item =>
                                new LegacyNpcSaleItemRuntime
                                {
                                    type = item.Type,
                                    typeId = item.TypeId
                                })
                        .ToArray();

                int[] inQuestIds =
                    definition.InQuestIds
                        .Select(value => (int)value)
                        .ToArray();

                int[] outQuestIds =
                    definition.OutQuestIds
                        .Select(value => (int)value)
                        .ToArray();

                NpcServiceKind services =
                    NpcServiceResolverCore.Resolve(
                        source.NpcType,
                        inQuestIds.Length > 0 ||
                        outQuestIds.Length > 0);

                for (int positionIndex = 0;
                     positionIndex < source.Positions.Count;
                     positionIndex++)
                {
                    LegacySvmapNpcPosition position =
                        source.Positions[positionIndex];

                    spawns.Add(
                        new LegacyNpcSpawnDefinition
                        {
                            npcType = source.NpcType,
                            typeId = source.NpcId,
                            modelIndex = modelIndex,
                            faction = definition.Faction,
                            moveDistance = definition.MoveDistance,
                            moveSpeed = definition.MoveSpeed,
                            merchantType =
                                definition.MerchantType.HasValue
                                    ? definition.MerchantType.Value
                                    : -1,
                            displayName =
                                translation.Name,
                            welcomeMessage =
                                translation.WelcomeMessage,
                            services =
                                services,
                            saleItems =
                                saleItems,
                            inQuestIds =
                                inQuestIds,
                            outQuestIds =
                                outQuestIds,
                            position = position.Position,
                            yawDegrees =
                                position.Yaw * Mathf.Rad2Deg,
                            gateTargets = gateTargets,
                            prefab = prefab
                        });
                }
            }

            uniqueModelCount = prefabs.Count;
            return spawns;
        }

        private static List<LegacyMonsterSpawnDefinition>
            BuildMonsterSpawnDefinitions(
                CanonicalClientCorpus corpus,
                LegacySvmapFile svmap,
                Terrain terrain,
                LegacyDbMonsterDataFile monsterData,
                LegacyDbMonsterTextFile monsterText,
                LegacyMonFile monsterModels,
                out int uniqueModelCount)
        {
            if (corpus == null)
                throw new ArgumentNullException(nameof(corpus));
            if (svmap == null)
                throw new ArgumentNullException(nameof(svmap));
            if (terrain == null)
                throw new ArgumentNullException(nameof(terrain));
            if (monsterData == null)
                throw new ArgumentNullException(nameof(monsterData));
            if (monsterText == null)
                throw new ArgumentNullException(nameof(monsterText));
            if (monsterModels == null)
                throw new ArgumentNullException(nameof(monsterModels));

            var prefabs =
                new Dictionary<int, GameObject>();

            var definitions =
                new List<LegacyMonsterSpawnDefinition>(
                    (int)Math.Min(
                        int.MaxValue,
                        svmap.MonsterInstanceCount));

            int ordinal = 0;

            for (int areaIndex = 0;
                 areaIndex < svmap.MonsterAreas.Count;
                 areaIndex++)
            {
                LegacySvmapMonsterArea area =
                    svmap.MonsterAreas[areaIndex];

                float minX =
                    Mathf.Min(
                        area.Area.Lower.x,
                        area.Area.Upper.x);

                float maxX =
                    Mathf.Max(
                        area.Area.Lower.x,
                        area.Area.Upper.x);

                float minZ =
                    Mathf.Min(
                        area.Area.Lower.z,
                        area.Area.Upper.z);

                float maxZ =
                    Mathf.Max(
                        area.Area.Lower.z,
                        area.Area.Upper.z);

                for (int spawnTypeIndex = 0;
                     spawnTypeIndex < area.Monsters.Count;
                     spawnTypeIndex++)
                {
                    LegacySvmapMonsterSpawn spawn =
                        area.Monsters[spawnTypeIndex];

                    // Canonical Map 0 contains five MobId=0 placeholder rows,
                    // all with Count=0. They are list padding, not entities.
                    if (spawn.Count == 0)
                        continue;

                    LegacyDbMonsterDataRecord monster;
                    if (!monsterData.TryGet(
                            spawn.MobId,
                            out monster))
                    {
                        throw new InvalidDataException(
                            "Map 0 area " + areaIndex +
                            " references missing DBMonsterData id " +
                            spawn.MobId + ".");
                    }

                    int modelIndex =
                        CheckedInt32(
                            monster.Image,
                            "image",
                            monster.Id);

                    if (modelIndex < 0 ||
                        modelIndex >= monsterModels.Records.Count)
                    {
                        throw new InvalidDataException(
                            "MobId " + spawn.MobId +
                            " resolves image/model " + modelIndex +
                            " outside monster.mon.");
                    }

                    string monsterName;
                    if (!monsterText.TryGetName(
                            monster.Id,
                            out monsterName) ||
                        string.IsNullOrWhiteSpace(monsterName))
                    {
                        monsterName =
                            "Mob_" + monster.Id;
                    }

                    GameObject prefab;
                    if (!prefabs.TryGetValue(
                            modelIndex,
                            out prefab))
                    {
                        prefab =
                            LegacyMonPrefabImporter.Import(
                                LegacyMonCatalogKind.Monster,
                                modelIndex);

                        if (prefab == null)
                        {
                            throw new InvalidDataException(
                                "MON importer returned null for model " +
                                modelIndex +
                                " used by MobId " +
                                spawn.MobId + ".");
                        }

                        prefabs.Add(
                            modelIndex,
                            prefab);
                    }

                    for (uint instanceIndex = 0;
                         instanceIndex < spawn.Count;
                         instanceIndex++)
                    {
                        ordinal++;

                        float u =
                            (float)Halton(
                                ordinal,
                                2);

                        float v =
                            (float)Halton(
                                ordinal,
                                3);

                        float yaw =
                            (float)(
                                Halton(
                                    ordinal,
                                    5) *
                                360.0);

                        float x =
                            Mathf.Lerp(
                                minX,
                                maxX,
                                u);

                        float z =
                            Mathf.Lerp(
                                minZ,
                                maxZ,
                                v);

                        Vector3 position =
                            new Vector3(
                                x,
                                0f,
                                z);

                        position.y =
                            terrain.SampleHeight(position) +
                            terrain.transform.position.y +
                            0.02f;

                        definitions.Add(
                            new LegacyMonsterSpawnDefinition
                            {
                                mobId = spawn.MobId,
                                modelIndex = modelIndex,
                                targetId =
                                    1_000_000 + ordinal,
                                mobName =
                                    monsterName,
                                level =
                                    CheckedInt32(
                                        monster.Level,
                                        "level",
                                        monster.Id),
                                maxHealth =
                                    Math.Max(
                                        1,
                                        CheckedInt32(
                                            monster.Hp,
                                            "hp",
                                            monster.Id)),
                                ai =
                                    CheckedByte(
                                        monster.Ai,
                                        "ai",
                                        monster.Id),
                                element =
                                    CheckedByte(
                                        monster.Element,
                                        "attrib",
                                        monster.Id),
                                rawSize =
                                    CheckedByte(
                                        monster.Size,
                                        "size",
                                        monster.Id),
                                // DBMonsterData.Size is categorical in this
                                // ps0032 corpus (commonly 1/2), not a percent.
                                // monster.mon geometry supplies the native size.
                                scale = 1f,
                                position =
                                    position,
                                yawDegrees =
                                    yaw,
                                prefab =
                                    prefab
                            });
                    }
                }
            }

            if (definitions.Count !=
                svmap.MonsterInstanceCount)
            {
                throw new InvalidDataException(
                    "Map 0 monster definition count mismatch. Expected " +
                    svmap.MonsterInstanceCount +
                    ", generated " +
                    definitions.Count + ".");
            }

            uniqueModelCount = prefabs.Count;
            return definitions;
        }

        private static int CheckedInt32(
            long value,
            string field,
            long mobId)
        {
            if (value < int.MinValue ||
                value > int.MaxValue)
            {
                throw new InvalidDataException(
                    "DBMonsterData id " + mobId +
                    " field " + field +
                    " is outside Int32: " + value + ".");
            }

            return (int)value;
        }

        private static byte CheckedByte(
            long value,
            string field,
            long mobId)
        {
            if (value < byte.MinValue ||
                value > byte.MaxValue)
            {
                throw new InvalidDataException(
                    "DBMonsterData id " + mobId +
                    " field " + field +
                    " is outside byte range: " + value + ".");
            }

            return (byte)value;
        }

        private static double Halton(
            int index,
            int radix)
        {
            if (index <= 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (radix < 2)
                throw new ArgumentOutOfRangeException(nameof(radix));

            double fraction = 1.0;
            double result = 0.0;
            int value = index;

            while (value > 0)
            {
                fraction /= radix;
                result +=
                    fraction *
                    (value % radix);

                value /= radix;
            }

            return result;
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
