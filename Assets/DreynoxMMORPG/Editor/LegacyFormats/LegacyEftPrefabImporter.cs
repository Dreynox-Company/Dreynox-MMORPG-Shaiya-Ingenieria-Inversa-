using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Vfx;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public static class LegacyEftPrefabImporter
    {
        private const string CanonicalEffectRoot =
            "DATA_Español/effect";

        private const string OutputRoot =
            "Assets/DreynoxMMORPG/LocalLegacyGenerated/Effects";

        private static readonly Dictionary<string, GameObject> SessionCache =
            new Dictionary<string, GameObject>(
                StringComparer.OrdinalIgnoreCase);

        private sealed class TextureSet
        {
            public Material Material;
            public Sprite[] Sprites =
                Array.Empty<Sprite>();
        }

        public static string ResolveCanonicalPath(
            CanonicalClientCorpus corpus,
            string effectFileName)
        {
            if (corpus == null)
                throw new ArgumentNullException(nameof(corpus));

            string normalized =
                NormalizeEffectName(effectFileName);

            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            string effectRoot =
                LegacyUiAssetImporter.ResolveCaseInsensitive(
                    corpus.RootPath,
                    CanonicalEffectRoot);

            if (!Directory.Exists(effectRoot))
                return null;

            return ResolveEffectLibrary(
                effectRoot,
                normalized);
        }

        public static LegacyEftFile ParseCanonical(
            CanonicalClientCorpus corpus,
            string effectFileName)
        {
            string path =
                ResolveCanonicalPath(
                    corpus,
                    effectFileName);

            return path == null
                ? null
                : LegacyEftParser.Parse(path);
        }

        public static GameObject Import(
            CanonicalClientCorpus corpus,
            string effectFileName,
            bool forceReimport = false)
        {
            if (corpus == null)
                throw new ArgumentNullException(nameof(corpus));

            string normalized =
                NormalizeEffectName(effectFileName);

            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            string cacheKey =
                normalized.ToLowerInvariant();

            if (!forceReimport &&
                SessionCache.TryGetValue(
                    cacheKey,
                    out GameObject cached) &&
                cached != null)
            {
                return cached;
            }

            string effectRoot =
                LegacyUiAssetImporter.ResolveCaseInsensitive(
                    corpus.RootPath,
                    CanonicalEffectRoot);

            if (!Directory.Exists(effectRoot))
                return null;

            string eftPath =
                ResolveEffectLibrary(
                    effectRoot,
                    normalized);

            if (eftPath == null)
                return null;

            LegacyEftFile eft =
                LegacyEftParser.Parse(eftPath);

            string safeName =
                Sanitize(
                    Path.GetFileNameWithoutExtension(
                        eftPath));

            string assetRoot =
                OutputRoot + "/" + safeName;

            string prefabPath =
                assetRoot + "/Prefabs/" +
                safeName + ".prefab";

            if (!forceReimport)
            {
                GameObject existing =
                    Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.LoadAssetAtPath<GameObject>(
                        prefabPath);

                if (existing != null)
                {
                    SessionCache[cacheKey] =
                        existing;

                    return existing;
                }
            }

            EnsureFolder(assetRoot);
            EnsureFolder(assetRoot + "/Meshes");
            EnsureFolder(assetRoot + "/Clips");
            EnsureFolder(assetRoot + "/SourceTextures");
            EnsureFolder(assetRoot + "/TextureAtlases");
            EnsureFolder(assetRoot + "/Materials");
            EnsureFolder(assetRoot + "/Prefabs");

            LegacyVertexEffectClip[] meshClips =
                new LegacyVertexEffectClip[
                    eft.MeshNames.Count];

            string[] meshTextureNames =
                new string[
                    eft.MeshNames.Count];

            for (int meshIndex = 0;
                 meshIndex <
                 eft.MeshNames.Count;
                 meshIndex++)
            {
                string meshName =
                    eft.MeshNames[meshIndex];

                string meshPath =
                    ResolveEffectResource(
                        effectRoot,
                        "3de",
                        meshName);

                if (meshPath == null)
                {
                    throw new FileNotFoundException(
                        "EFT references missing 3DE '" +
                        meshName + "'.",
                        meshName);
                }

                Legacy3deFile source =
                    Legacy3deParser.Parse(
                        meshPath);

                meshTextureNames[meshIndex] =
                    source.TextureName;

                Mesh mesh =
                    BuildMesh(
                        safeName,
                        meshIndex,
                        source);

                string meshAssetPath =
                    assetRoot + "/Meshes/" +
                    meshIndex.ToString("D3") +
                    "_" +
                    Sanitize(
                        Path.GetFileNameWithoutExtension(
                            meshName)) +
                    ".asset";

                Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.DeleteAsset(
                    meshAssetPath);

                Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.CreateAsset(
                    mesh,
                    meshAssetPath);

                LegacyVertexEffectClip clip =
                    ScriptableObject.CreateInstance<
                        LegacyVertexEffectClip>();

                clip.name =
                    safeName + "_Mesh_" +
                    meshIndex.ToString("D3");

                clip.Configure(
                    mesh,
                    source.MaxKeyframe,
                    30f,
                    ConvertFrames(source));

                string clipPath =
                    assetRoot + "/Clips/" +
                    meshIndex.ToString("D3") +
                    ".asset";

                Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.DeleteAsset(
                    clipPath);

                Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.CreateAsset(
                    clip,
                    clipPath);

                meshClips[meshIndex] =
                    clip;
            }

            GameObject root =
                new GameObject(
                    "EFT_" + safeName);

            try
            {
                LegacyEftParticleEmitter[] emitters =
                    new LegacyEftParticleEmitter[
                        eft.Effects.Count];

                for (int effectIndex = 0;
                     effectIndex <
                     eft.Effects.Count;
                     effectIndex++)
                {
                    LegacyEftEffect sourceEffect =
                        eft.Effects[
                            effectIndex];

                    LegacyVertexEffectClip meshClip =
                        ResolveMeshClip(
                            meshClips,
                            sourceEffect.MeshIndex);

                    bool usesRenderableMesh =
                        meshClip != null &&
                        !sourceEffect.MotionPathEnabled;

                    string fallbackTexture =
                        sourceEffect.MeshIndex >= 0 &&
                        sourceEffect.MeshIndex <
                        meshTextureNames.Length
                            ? meshTextureNames[
                                sourceEffect.MeshIndex]
                            : string.Empty;

                    TextureSet textureSet =
                        BuildTextureSet(
                            effectRoot,
                            assetRoot,
                            safeName,
                            eft,
                            sourceEffect,
                            effectIndex,
                            fallbackTexture);

                    GameObject child =
                        new GameObject(
                            "Emitter_" +
                            effectIndex.ToString("D3") +
                            "_" +
                            Sanitize(
                                sourceEffect.Name));

                    child.transform.SetParent(
                        root.transform,
                        false);

                    ParticleSystem system =
                        child.AddComponent<
                            ParticleSystem>();

                    ParticleSystemRenderer renderer =
                        child.GetComponent<
                            ParticleSystemRenderer>();

                    ConfigureRenderer(
                        renderer,
                        sourceEffect,
                        meshClip,
                        textureSet.Material);

                    ConfigureTextureAnimation(
                        system,
                        sourceEffect,
                        textureSet.Sprites);

                    LegacyEftParticleEmitter emitter =
                        child.AddComponent<
                            LegacyEftParticleEmitter>();

                    emitter.Configure(
                        sourceEffect.Loop,
                        sourceEffect.VelocityRandomX,
                        sourceEffect.VelocityRandomY,
                        sourceEffect.VelocityRandomZ,
                        sourceEffect.VelocityMode,
                        sourceEffect.EmitRateMin,
                        sourceEffect.EmitRateMax,
                        sourceEffect.LifeMin,
                        sourceEffect.LifeMax,
                        sourceEffect.EmitterDuration,
                        sourceEffect.SwirlSpeed,
                        ConvertEffectVector(
                            sourceEffect.EmitPositionSpread,
                            usesRenderableMesh),
                        ConvertEffectVector(
                            sourceEffect.Acceleration,
                            usesRenderableMesh),
                        ConvertEffectVector(
                            sourceEffect.EmitOrigin,
                            usesRenderableMesh),
                        ConvertEffectVector(
                            sourceEffect.VelocityMin,
                            usesRenderableMesh),
                        ConvertEffectVector(
                            sourceEffect.VelocityMax,
                            usesRenderableMesh),
                        sourceEffect.GravityEnabled,
                        sourceEffect.AttractEnabled,
                        ConvertEffectVector(
                            sourceEffect.AttractPoint,
                            usesRenderableMesh),
                        sourceEffect.AttractStrength,
                        sourceEffect.AngularVelocityRandom,
                        sourceEffect.RotationEnabled,
                        sourceEffect.AngularVelocity,
                        sourceEffect.RotationAxis,
                        sourceEffect.InitialRotationAxis,
                        sourceEffect.InitialRotationMinDegrees,
                        sourceEffect.InitialRotationMaxDegrees,
                        sourceEffect.MotionPathEnabled,
                        meshClip,
                        ConvertColorKeys(
                            sourceEffect.ColorFrames),
                        ConvertVelocityScaleKeys(
                            sourceEffect.VelocityScaleFrames),
                        ConvertScaleKeys(
                            sourceEffect.ScaleFrames));

                    emitters[effectIndex] =
                        emitter;

                    child.SetActive(false);
                }

                LegacyEftSequenceDefinition[] sequences =
                    BuildSequences(
                        eft,
                        emitters);

                LegacyEftSequencePlayer sequencePlayer =
                    root.AddComponent<
                        LegacyEftSequencePlayer>();

                sequencePlayer.Configure(
                    emitters,
                    sequences);

                Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.DeleteAsset(
                    prefabPath);

                GameObject prefab =
                    Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.SaveAsPrefabAsset(
                        root,
                        prefabPath);

                Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.SaveAssets();
                Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.Refresh();

                SessionCache[cacheKey] =
                    prefab;

                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    root);
            }
        }

        public static void ClearSessionCache()
        {
            SessionCache.Clear();
        }

        private static string ResolveEffectLibrary(
            string effectRoot,
            string normalized)
        {
            string direct =
                ResolveEffectResource(
                    effectRoot,
                    string.Empty,
                    normalized);

            if (direct != null)
                return direct;

            if (Path.HasExtension(normalized))
                return null;

            foreach (string extension in
                     new[]
                     {
                         ".eft",
                         ".ef2",
                         ".ef3"
                     })
            {
                string candidate =
                    ResolveEffectResource(
                        effectRoot,
                        string.Empty,
                        normalized +
                        extension);

                if (candidate != null)
                    return candidate;
            }

            return null;
        }

        private static string ResolveEffectTextureResource(
            string effectRoot,
            string fileName)
        {
            string direct =
                ResolveEffectResource(
                    effectRoot,
                    "dds",
                    fileName);

            if (direct != null)
                return direct;

            string extension =
                Path.GetExtension(
                    fileName);

            if (!string.Equals(
                    extension,
                    ".dds",
                    StringComparison.OrdinalIgnoreCase))
            {
                string ddsName =
                    Path.GetFileNameWithoutExtension(
                        fileName) +
                    ".dds";

                direct =
                    ResolveEffectResource(
                        effectRoot,
                        "dds",
                        ddsName);

                if (direct != null)
                    return direct;
            }

            return null;
        }

        private static string ResolveEffectResource(
            string effectRoot,
            string conventionalDirectory,
            string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            if (!string.IsNullOrWhiteSpace(
                    conventionalDirectory))
            {
                string direct =
                    LegacyUiAssetImporter
                        .ResolveCaseInsensitive(
                            effectRoot,
                            conventionalDirectory +
                            "/" +
                            fileName);

                if (File.Exists(direct))
                    return direct;
            }

            string rootDirect =
                LegacyUiAssetImporter
                    .ResolveCaseInsensitive(
                        effectRoot,
                        fileName);

            if (File.Exists(rootDirect))
                return rootDirect;

            return CanonicalResourceIndex
                .FindUnique(
                    effectRoot,
                    fileName);
        }

        private static LegacyVertexEffectClip ResolveMeshClip(
            IReadOnlyList<LegacyVertexEffectClip> clips,
            int index)
        {
            if (index < 0 ||
                index >= clips.Count)
                return null;

            return clips[index];
        }

        private static TextureSet BuildTextureSet(
            string effectRoot,
            string assetRoot,
            string effectLibraryName,
            LegacyEftFile library,
            LegacyEftEffect effect,
            int effectIndex,
            string fallbackTextureName)
        {
            List<string> textureNames =
                new List<string>();

            for (int i = 0;
                 i < effect.TextureIds.Count;
                 i++)
            {
                int textureId =
                    effect.TextureIds[i];

                if (textureId < 0 ||
                    textureId >=
                    library.TextureNames.Count)
                {
                    throw new InvalidDataException(
                        "EFT component " +
                        effectIndex +
                        " references texture " +
                        textureId +
                        " outside 0.." +
                        (library.TextureNames.Count - 1) +
                        ".");
                }

                textureNames.Add(
                    library.TextureNames[
                        textureId]);
            }

            if (textureNames.Count == 0 &&
                !string.IsNullOrWhiteSpace(
                    fallbackTextureName))
            {
                textureNames.Add(
                    fallbackTextureName);
            }

            var sourceTextures =
                new List<Texture2D>();

            var sourceLabels =
                new List<string>();

            for (int i = 0;
                 i < textureNames.Count;
                 i++)
            {
                string source =
                    ResolveEffectTextureResource(
                        effectRoot,
                        textureNames[i]);

                if (source == null)
                    continue;

                string destination =
                    assetRoot +
                    "/SourceTextures/" +
                    effectIndex.ToString("D3") +
                    "_" +
                    i.ToString("D2") +
                    "_" +
                    Path.GetFileName(source)
                        .ToLowerInvariant();

                string absolute =
                    Path.GetFullPath(
                        destination);

                string directory =
                    Path.GetDirectoryName(
                        absolute);

                if (!string.IsNullOrWhiteSpace(
                        directory))
                {
                    Directory.CreateDirectory(
                        directory);
                }

                File.Copy(
                    source,
                    absolute,
                    true);

                AssetDatabase.ImportAsset(
                    destination,
                    ImportAssetOptions
                        .ForceSynchronousImport);

                TextureImporter importer =
                    AssetImporter.GetAtPath(
                        destination)
                    as TextureImporter;

                if (importer != null)
                {
                    importer.textureType =
                        TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    importer.isReadable = true;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency =
                        true;
                    importer.wrapMode =
                        TextureWrapMode.Mirror;
                    importer.filterMode =
                        FilterMode.Bilinear;

                    importer.SaveAndReimport();
                }

                Texture2D texture =
                    AssetDatabase
                        .LoadAssetAtPath<Texture2D>(
                            destination);

                if (texture != null)
                {
                    sourceTextures.Add(
                        texture);

                    sourceLabels.Add(
                        Path.GetFileNameWithoutExtension(
                            source));
                }
            }

            Texture2D atlas = null;
            Sprite[] sprites =
                Array.Empty<Sprite>();

            string atlasPath =
                assetRoot +
                "/TextureAtlases/Effect_" +
                effectIndex.ToString("D3") +
                ".asset";

            Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.DeleteAsset(
                atlasPath);

            if (sourceTextures.Count > 0)
            {
                atlas =
                    new Texture2D(
                        4,
                        4,
                        TextureFormat.RGBA32,
                        true,
                        false)
                    {
                        name =
                            effectLibraryName +
                            "_Atlas_" +
                            effectIndex.ToString("D3"),
                        wrapMode =
                            TextureWrapMode.Mirror,
                        filterMode =
                            FilterMode.Bilinear,
                        anisoLevel = 1
                    };

                Rect[] rects =
                    atlas.PackTextures(
                        sourceTextures.ToArray(),
                        2,
                        4096,
                        false);

                atlas.Apply(
                    true,
                    false);

                Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.CreateAsset(
                    atlas,
                    atlasPath);

                sprites =
                    new Sprite[
                        rects.Length];

                for (int i = 0;
                     i < rects.Length;
                     i++)
                {
                    Rect normalized =
                        rects[i];

                    Rect pixels =
                        new Rect(
                            normalized.x *
                            atlas.width,
                            normalized.y *
                            atlas.height,
                            normalized.width *
                            atlas.width,
                            normalized.height *
                            atlas.height);

                    Sprite sprite =
                        Sprite.Create(
                            atlas,
                            pixels,
                            new Vector2(
                                0.5f,
                                0.5f),
                            100f,
                            0,
                            SpriteMeshType.FullRect);

                    sprite.name =
                        sourceLabels[i];

                    Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.AddObjectToAsset(
                        sprite,
                        atlas);

                    sprites[i] =
                        sprite;
                }

                EditorUtility.SetDirty(
                    atlas);

                Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.SaveAssets();
            }

            Material material =
                CreateParticleMaterial(
                    effect,
                    atlas,
                    effectLibraryName,
                    effectIndex);

            string materialPath =
                assetRoot +
                "/Materials/Effect_" +
                effectIndex.ToString("D3") +
                ".mat";

            Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.DeleteAsset(
                materialPath);

            Dreynox.Mmorpg.Editor.Importing.LegacyAssetWriteBatch.CreateAsset(
                material,
                materialPath);

            return new TextureSet
            {
                Material = material,
                Sprites = sprites
            };
        }

        private static Material CreateParticleMaterial(
            LegacyEftEffect effect,
            Texture2D texture,
            string libraryName,
            int effectIndex)
        {
            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Particles/Unlit");

            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Universal Render Pipeline/Unlit");
            }

            if (shader == null)
                shader =
                    Shader.Find(
                        "Particles/Standard Unlit");

            if (shader == null)
                throw new InvalidOperationException(
                    "No particle-compatible shader is available.");

            Material material =
                new Material(shader)
                {
                    name =
                        libraryName +
                        "_Particle_" +
                        effectIndex.ToString("D3"),
                    renderQueue = 3000
                };

            if (texture != null)
            {
                if (material.HasProperty(
                        "_BaseMap"))
                {
                    material.SetTexture(
                        "_BaseMap",
                        texture);
                }

                if (material.HasProperty(
                        "_MainTex"))
                {
                    material.SetTexture(
                        "_MainTex",
                        texture);
                }
            }

            BlendMode source =
                MapBlend(
                    effect.SourceBlend,
                    sourceRole: true);

            BlendMode destination =
                MapBlend(
                    effect.DestinationBlend,
                    sourceRole: false);

            if (material.HasProperty(
                    "_Surface"))
                material.SetFloat(
                    "_Surface",
                    1f);

            if (material.HasProperty(
                    "_ZWrite"))
                material.SetFloat(
                    "_ZWrite",
                    0f);

            if (material.HasProperty(
                    "_SrcBlend"))
                material.SetFloat(
                    "_SrcBlend",
                    (float)source);

            if (material.HasProperty(
                    "_DstBlend"))
                material.SetFloat(
                    "_DstBlend",
                    (float)destination);

            material.SetOverrideTag(
                "RenderType",
                "Transparent");

            material.EnableKeyword(
                "_SURFACE_TYPE_TRANSPARENT");

            return material;
        }

        private static BlendMode MapBlend(
            int value,
            bool sourceRole)
        {
            switch (value)
            {
                case 0:
                    return BlendMode.Zero;
                case 1:
                    return BlendMode.One;
                case 2:
                    return BlendMode.SrcColor;
                case 3:
                    return BlendMode.OneMinusSrcColor;
                case 4:
                    return BlendMode.SrcAlpha;
                case 5:
                    return BlendMode.OneMinusSrcAlpha;
                case 6:
                    return BlendMode.DstAlpha;
                case 7:
                    return BlendMode.OneMinusDstAlpha;
                case 8:
                    return BlendMode.DstColor;
                case 9:
                    return BlendMode.OneMinusDstColor;
                case 10:
                    return BlendMode.SrcAlphaSaturate;
                default:
                    return sourceRole
                        ? BlendMode.SrcAlpha
                        : BlendMode.OneMinusSrcAlpha;
            }
        }

        private static void ConfigureRenderer(
            ParticleSystemRenderer renderer,
            LegacyEftEffect effect,
            LegacyVertexEffectClip meshClip,
            Material material)
        {
            renderer.sharedMaterial =
                material;

            renderer.shadowCastingMode =
                ShadowCastingMode.Off;

            renderer.receiveShadows =
                false;

            renderer.sortMode =
                ParticleSystemSortMode.Distance;

            bool usesMesh =
                meshClip != null &&
                meshClip.BaseMesh != null &&
                !effect.MotionPathEnabled;

            if (usesMesh)
            {
                renderer.renderMode =
                    ParticleSystemRenderMode.Mesh;

                renderer.mesh =
                    meshClip.BaseMesh;

                renderer.alignment =
                    ParticleSystemRenderSpace.Local;

                renderer.enableGPUInstancing =
                    true;

                return;
            }

            switch (effect.BaseAxis)
            {
                case 1:
                    renderer.renderMode =
                        ParticleSystemRenderMode
                            .HorizontalBillboard;
                    renderer.alignment =
                        ParticleSystemRenderSpace.World;
                    break;

                case 2:
                    renderer.renderMode =
                        ParticleSystemRenderMode
                            .VerticalBillboard;
                    renderer.alignment =
                        ParticleSystemRenderSpace.View;
                    break;

                case 3:
                    renderer.renderMode =
                        ParticleSystemRenderMode.Billboard;
                    renderer.alignment =
                        ParticleSystemRenderSpace.Local;
                    break;

                default:
                    renderer.renderMode =
                        ParticleSystemRenderMode.Billboard;
                    renderer.alignment =
                        ParticleSystemRenderSpace.View;
                    break;
            }
        }

        private static void ConfigureTextureAnimation(
            ParticleSystem system,
            LegacyEftEffect effect,
            IReadOnlyList<Sprite> sprites)
        {
            ParticleSystem.TextureSheetAnimationModule module =
                system.textureSheetAnimation;

            module.enabled =
                sprites != null &&
                sprites.Count > 1;

            if (!module.enabled)
                return;

            module.mode =
                ParticleSystemAnimationMode.Sprites;

            for (int i = 0;
                 i < sprites.Count;
                 i++)
            {
                if (sprites[i] != null)
                    module.AddSprite(
                        sprites[i]);
            }

            module.timeMode =
                ParticleSystemAnimationTimeMode.FPS;

            module.fps =
                1f /
                Mathf.Max(
                    0.033f,
                    Mathf.Abs(
                        effect.DelayPerFrame));

            module.cycleCount =
                effect.TextureLoop
                    ? 1000
                    : 1;
        }

        private static Vector3 ConvertEffectVector(
            Vector3 value,
            bool usesRenderableMesh)
        {
            Vector3 result =
                LegacyCoordinateBridge.Direction(
                    value);

            if (usesRenderableMesh)
            {
                result.x =
                    -result.x;

                result.z =
                    -result.z;
            }

            return result;
        }

        private static LegacyEftColorKey[]
            ConvertColorKeys(
                IReadOnlyList<LegacyEftColorFrame> source)
        {
            return source
                .Select(
                    value =>
                        new LegacyEftColorKey
                        {
                            color =
                                new Color(
                                    value.R,
                                    value.G,
                                    value.B,
                                    Mathf.Clamp01(
                                        value.A)),
                            time =
                                Mathf.Max(
                                    0f,
                                    value.Time)
                        })
                .OrderBy(value => value.time)
                .ToArray();
        }

        private static LegacyEftFloatKey[]
            ConvertVelocityScaleKeys(
                IReadOnlyList<LegacyEftFloatFrame> source)
        {
            return source
                .Select(
                    value =>
                        new LegacyEftFloatKey
                        {
                            value =
                                value.Value,
                            time =
                                Mathf.Max(
                                    0f,
                                    value.Time)
                        })
                .OrderBy(value => value.time)
                .ToArray();
        }

        private static LegacyEftScaleKey[]
            ConvertScaleKeys(
                IReadOnlyList<LegacyEftScaleFrame> source)
        {
            return source
                .Select(
                    value =>
                        new LegacyEftScaleKey
                        {
                            min =
                                value.Min,
                            max =
                                value.Max,
                            time =
                                Mathf.Max(
                                    0f,
                                    value.Time)
                        })
                .OrderBy(value => value.time)
                .ToArray();
        }

        private static Mesh BuildMesh(
            string effectName,
            int meshIndex,
            Legacy3deFile source)
        {
            int vertexCount =
                source.Vertices.Count;

            Vector3[] positions =
                new Vector3[
                    vertexCount];

            Vector2[] uvs =
                new Vector2[
                    vertexCount];

            for (int i = 0;
                 i < vertexCount;
                 i++)
            {
                positions[i] =
                    LegacyCoordinateBridge.Position(
                        source.Vertices[i]
                            .Position);

                uvs[i] =
                    source.Vertices[i]
                        .UV;
            }

            int[] triangles =
                new int[
                    source.Faces.Count * 3];

            for (int i = 0;
                 i < source.Faces.Count;
                 i++)
            {
                LegacyTriangle face =
                    source.Faces[i];

                triangles[
                    i * 3] =
                    face.A;

                triangles[
                    i * 3 + 1] =
                    face.C;

                triangles[
                    i * 3 + 2] =
                    face.B;
            }

            Mesh mesh =
                new Mesh
                {
                    name =
                        effectName +
                        "_3DE_" +
                        meshIndex.ToString("D3"),
                    indexFormat =
                        vertexCount > 65535
                            ? IndexFormat.UInt32
                            : IndexFormat.UInt16
                };

            mesh.vertices =
                positions;

            mesh.uv =
                uvs;

            mesh.triangles =
                triangles;

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private static LegacyVertexEffectFrame[]
            ConvertFrames(
                Legacy3deFile source)
        {
            LegacyVertexEffectFrame[] frames =
                new LegacyVertexEffectFrame[
                    source.Frames.Count];

            for (int frameIndex = 0;
                 frameIndex <
                 source.Frames.Count;
                 frameIndex++)
            {
                Legacy3deFrame sourceFrame =
                    source.Frames[
                        frameIndex];

                Vector3[] positions =
                    new Vector3[
                        sourceFrame
                            .Vertices.Count];

                Vector2[] uvs =
                    new Vector2[
                        sourceFrame
                            .Vertices.Count];

                for (int vertexIndex = 0;
                     vertexIndex <
                     sourceFrame
                         .Vertices.Count;
                     vertexIndex++)
                {
                    positions[vertexIndex] =
                        LegacyCoordinateBridge.Position(
                            sourceFrame
                                .Vertices[
                                    vertexIndex]
                                .Position);

                    uvs[vertexIndex] =
                        sourceFrame
                            .Vertices[
                                vertexIndex]
                            .UV;
                }

                frames[frameIndex] =
                    new LegacyVertexEffectFrame
                    {
                        keyframe =
                            sourceFrame.Keyframe,
                        positions =
                            positions,
                        uvs =
                            uvs
                    };
            }

            return frames;
        }

        private static LegacyEftSequenceDefinition[]
            BuildSequences(
                LegacyEftFile source,
                IReadOnlyList<LegacyEftParticleEmitter> emitters)
        {
            if (source.Sequences.Count == 0)
            {
                LegacyEftSequenceEvent[] events =
                    new LegacyEftSequenceEvent[
                        emitters.Count];

                float duration =
                    0f;

                for (int i = 0;
                     i < emitters.Count;
                     i++)
                {
                    events[i] =
                        new LegacyEftSequenceEvent
                        {
                            effectIndex = i,
                            time = 0f
                        };

                    if (emitters[i] != null)
                    {
                        duration =
                            Mathf.Max(
                                duration,
                                emitters[i]
                                    .EstimatedOneShotDuration);
                    }
                }

                return new[]
                {
                    new LegacyEftSequenceDefinition
                    {
                        name = "default",
                        events = events,
                        duration =
                            Mathf.Max(
                                0.05f,
                                duration)
                    }
                };
            }

            LegacyEftSequenceDefinition[] result =
                new LegacyEftSequenceDefinition[
                    source.Sequences.Count];

            for (int sequenceIndex = 0;
                 sequenceIndex <
                 source.Sequences.Count;
                 sequenceIndex++)
            {
                LegacyEftSequence sourceSequence =
                    source.Sequences[
                        sequenceIndex];

                LegacyEftSequenceEvent[] events =
                    new LegacyEftSequenceEvent[
                        sourceSequence
                            .Records.Count];

                float duration =
                    0f;

                for (int eventIndex = 0;
                     eventIndex <
                     sourceSequence
                         .Records.Count;
                     eventIndex++)
                {
                    LegacyEftSequenceRecord sourceEvent =
                        sourceSequence
                            .Records[
                                eventIndex];

                    if (sourceEvent.EffectId < 0 ||
                        sourceEvent.EffectId >=
                        emitters.Count)
                    {
                        throw new InvalidDataException(
                            "EFT sequence '" +
                            sourceSequence.Name +
                            "' references invalid effect " +
                            sourceEvent.EffectId +
                            ".");
                    }

                    float eventTime =
                        Mathf.Max(
                            0f,
                            sourceEvent.Time);

                    events[eventIndex] =
                        new LegacyEftSequenceEvent
                        {
                            effectIndex =
                                sourceEvent.EffectId,
                            time =
                                eventTime
                        };

                    LegacyEftParticleEmitter emitter =
                        emitters[
                            sourceEvent.EffectId];

                    duration =
                        Mathf.Max(
                            duration,
                            eventTime +
                            (emitter != null
                                ? emitter
                                    .EstimatedOneShotDuration
                                : 0f));
                }

                Array.Sort(
                    events,
                    (a, b) =>
                        a.time.CompareTo(
                            b.time));

                result[sequenceIndex] =
                    new LegacyEftSequenceDefinition
                    {
                        name =
                            string.IsNullOrWhiteSpace(
                                sourceSequence.Name)
                                ? "sequence_" +
                                  sequenceIndex
                                      .ToString("D3")
                                : sourceSequence.Name,
                        events = events,
                        duration =
                            Mathf.Max(
                                0.05f,
                                duration)
                    };
            }

            return result;
        }

        private static string NormalizeEffectName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized =
                value.Trim();

            if (string.Equals(
                    normalized,
                    "LOAD",
                    StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return normalized;
        }

        private static string Sanitize(
            string value)
        {
            string result =
                string.IsNullOrWhiteSpace(value)
                    ? "Unnamed"
                    : value.Trim();

            foreach (char invalid in
                     Path.GetInvalidFileNameChars())
            {
                result =
                    result.Replace(
                        invalid,
                        '_');
            }

            return result
                .Replace(' ', '_')
                .Replace('/', '_')
                .Replace('\\', '_');
        }

        private static void EnsureFolder(
            string path)
        {
            string[] parts =
                path.Split(
                    new[] { '/' },
                    StringSplitOptions
                        .RemoveEmptyEntries);

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

                if (!AssetDatabase
                    .IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[i]);
                }

                current =
                    next;
            }
        }
    }
}
