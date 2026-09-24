using System;
using System.Collections.Generic;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public sealed class LegacyWorldEnvironmentBuildResult
    {
        public string SkyName = string.Empty;
        public string CloudsName1 = string.Empty;
        public string CloudsName2 = string.Empty;
        public int ImportedMusicClips;
        public int MusicZones;
        public int ImportedSoundClips;
        public int PositionalSounds;
    }

    public static class LegacyWorldEnvironmentImporter
    {
        private const string OutputRoot =
            "Assets/DreynoxMMORPG/LocalLegacyGenerated/" +
            "World/Map000/Environment";

        public static LegacyWorldEnvironmentBuildResult Create(
            CanonicalClientCorpus corpus,
            LegacyWldTerrainFile wld,
            Transform observer)
        {
            if (corpus == null)
                throw new ArgumentNullException(
                    nameof(corpus));

            if (wld == null)
                throw new ArgumentNullException(
                    nameof(wld));

            if (observer == null)
                throw new ArgumentNullException(
                    nameof(observer));

            EnsureFolder(OutputRoot);
            EnsureFolder(OutputRoot + "/Sky");
            EnsureFolder(OutputRoot + "/Audio");
            EnsureFolder(OutputRoot + "/Audio/Music");
            EnsureFolder(OutputRoot + "/Audio/Sounds");

            Texture2D sky =
                ImportSkyTexture(
                    corpus,
                    wld.SkyName,
                    "sky");

            Texture2D cloud1 =
                ImportSkyTexture(
                    corpus,
                    wld.CloudsName1,
                    "cloud1");

            Texture2D cloud2 =
                ImportSkyTexture(
                    corpus,
                    wld.CloudsName2,
                    "cloud2");

            GameObject environment =
                new GameObject(
                    "WLD_Environment_Runtime");

            Renderer skyRenderer =
                CreateSkySphere(
                    environment.transform,
                    sky);

            Renderer cloudRenderer1 =
                CreateCloudLayer(
                    environment.transform,
                    "CloudLayer_1",
                    cloud1,
                    420f);

            Renderer cloudRenderer2 =
                CreateCloudLayer(
                    environment.transform,
                    "CloudLayer_2",
                    cloud2,
                    470f);

            LegacySkyCloudRuntime skyRuntime =
                environment.AddComponent<
                    LegacySkyCloudRuntime>();

            // WLD exposes the authored textures but no verified cloud scroll
            // cadence. Keep offsets static until game.exe capture calibrates it.
            skyRuntime.Configure(
                observer,
                skyRenderer,
                cloudRenderer1,
                cloudRenderer2,
                Vector2.zero,
                Vector2.zero,
                isScrollTimingCalibrated: false);

            Dictionary<int, AudioClip> music =
                ImportMusic(
                    corpus,
                    wld.MusicNames);

            Dictionary<int, AudioClip> sounds =
                ImportSounds(
                    corpus,
                    wld.SoundEffectNames);

            List<LegacyMusicZoneRuntimeDescriptor> zones =
                BuildMusicZones(
                    wld,
                    music);

            List<LegacyPositionalSoundRuntimeDescriptor> positional =
                BuildPositionalSounds(
                    wld,
                    sounds);

            LegacyWorldAudioRuntime audio =
                environment.AddComponent<
                    LegacyWorldAudioRuntime>();

            audio.Configure(
                observer,
                zones,
                positional);

            return new LegacyWorldEnvironmentBuildResult
            {
                SkyName =
                    wld.SkyName ?? string.Empty,
                CloudsName1 =
                    wld.CloudsName1 ?? string.Empty,
                CloudsName2 =
                    wld.CloudsName2 ?? string.Empty,
                ImportedMusicClips =
                    music.Count,
                MusicZones =
                    zones.Count,
                ImportedSoundClips =
                    sounds.Count,
                PositionalSounds =
                    positional.Count
            };
        }

        private static Texture2D ImportSkyTexture(
            CanonicalClientCorpus corpus,
            string authoredName,
            string label)
        {
            if (string.IsNullOrWhiteSpace(
                    authoredName))
                return null;

            string skyRoot =
                LegacyUiAssetImporter
                    .ResolveCaseInsensitive(
                        corpus.RootPath,
                        "DATA_Español/sky");

            string source =
                CanonicalResourceIndex
                    .FindUnique(
                        skyRoot,
                        Path.GetFileName(
                            authoredName));

            if (source == null ||
                !File.Exists(source))
            {
                throw new FileNotFoundException(
                    "Canonical " +
                    label +
                    " texture not found: " +
                    authoredName,
                    source);
            }

            string assetPath =
                OutputRoot +
                "/Sky/" +
                label +
                "_" +
                Path.GetFileName(source)
                    .ToLowerInvariant();

            CopyAsset(
                source,
                assetPath);

            TextureImporter importer =
                AssetImporter.GetAtPath(
                    assetPath)
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

            Texture2D texture =
                AssetDatabase.LoadAssetAtPath<
                    Texture2D>(
                        assetPath);

            if (texture == null)
            {
                throw new InvalidDataException(
                    "Unity did not import canonical " +
                    label +
                    " texture '" +
                    assetPath +
                    "'.");
            }

            return texture;
        }

        private static Dictionary<int, AudioClip> ImportMusic(
            CanonicalClientCorpus corpus,
            IReadOnlyList<string> names)
        {
            return ImportAudio(
                corpus,
                names,
                OutputRoot + "/Audio/Music",
                loadInBackground: true);
        }

        private static Dictionary<int, AudioClip> ImportSounds(
            CanonicalClientCorpus corpus,
            IReadOnlyList<string> names)
        {
            return ImportAudio(
                corpus,
                names,
                OutputRoot + "/Audio/Sounds",
                loadInBackground: false);
        }

        private static Dictionary<int, AudioClip> ImportAudio(
            CanonicalClientCorpus corpus,
            IReadOnlyList<string> names,
            string destinationRoot,
            bool loadInBackground)
        {
            var result =
                new Dictionary<int, AudioClip>();

            if (names == null ||
                names.Count == 0)
                return result;

            string soundRoot =
                LegacyUiAssetImporter
                    .ResolveCaseInsensitive(
                        corpus.RootPath,
                        "DATA_Español/sound");

            for (int i = 0;
                 i < names.Count;
                 i++)
            {
                string authored =
                    names[i];

                if (string.IsNullOrWhiteSpace(
                        authored))
                    continue;

                string source =
                    CanonicalResourceIndex
                        .FindUnique(
                            soundRoot,
                            Path.GetFileName(
                                authored));

                if (source == null ||
                    !File.Exists(source))
                {
                    throw new FileNotFoundException(
                        "Canonical WLD audio not found: " +
                        authored,
                        source);
                }

                string assetPath =
                    destinationRoot +
                    "/" +
                    i.ToString("D3") +
                    "_" +
                    Path.GetFileName(source)
                        .ToLowerInvariant();

                CopyAsset(
                    source,
                    assetPath);

                AudioImporter importer =
                    AssetImporter.GetAtPath(
                        assetPath)
                    as AudioImporter;

                if (importer != null)
                {
                    importer.forceToMono = false;
                    importer.loadInBackground =
                        loadInBackground;

                    AudioImporterSampleSettings settings =
                        importer.defaultSampleSettings;

                    settings.preloadAudioData =
                        !loadInBackground;

                    importer.defaultSampleSettings =
                        settings;

                    importer.SaveAndReimport();
                }

                AudioClip clip =
                    AssetDatabase.LoadAssetAtPath<
                        AudioClip>(
                            assetPath);

                if (clip == null)
                {
                    throw new InvalidDataException(
                        "Unity did not import canonical audio '" +
                        assetPath +
                        "'.");
                }

                result.Add(
                    i,
                    clip);
            }

            return result;
        }

        private static List<LegacyMusicZoneRuntimeDescriptor> BuildMusicZones(
            LegacyWldTerrainFile wld,
            IReadOnlyDictionary<int, AudioClip> clips)
        {
            var result =
                new List<
                    LegacyMusicZoneRuntimeDescriptor>(
                        wld.MusicZones.Count);

            for (int i = 0;
                 i < wld.MusicZones.Count;
                 i++)
            {
                LegacyWldMusicZone zone =
                    wld.MusicZones[i];

                AudioClip clip;
                if (!clips.TryGetValue(
                        zone.Id,
                        out clip))
                    continue;

                result.Add(
                    new LegacyMusicZoneRuntimeDescriptor
                    {
                        id =
                            zone.Id,
                        bounds =
                            ToBounds(
                                zone.Bounds),
                        radius =
                            Mathf.Max(
                                0f,
                                zone.Radius),
                        clip =
                            clip
                    });
            }

            return result;
        }

        private static List<LegacyPositionalSoundRuntimeDescriptor> BuildPositionalSounds(
            LegacyWldTerrainFile wld,
            IReadOnlyDictionary<int, AudioClip> clips)
        {
            var result =
                new List<
                    LegacyPositionalSoundRuntimeDescriptor>(
                        wld.SoundEffects.Count);

            for (int i = 0;
                 i < wld.SoundEffects.Count;
                 i++)
            {
                LegacyWldSoundEffect sound =
                    wld.SoundEffects[i];

                AudioClip clip;
                if (!clips.TryGetValue(
                        sound.Id,
                        out clip))
                    continue;

                result.Add(
                    new LegacyPositionalSoundRuntimeDescriptor
                    {
                        id =
                            sound.Id,
                        center =
                            sound.Center,
                        radius =
                            Mathf.Max(
                                0.1f,
                                sound.Radius),
                        clip =
                            clip
                    });
            }

            return result;
        }

        private static Renderer CreateSkySphere(
            Transform parent,
            Texture2D texture)
        {
            if (texture == null)
                return null;

            GameObject sphere =
                GameObject.CreatePrimitive(
                    PrimitiveType.Sphere);

            sphere.name =
                "LegacySky_" +
                texture.name;

            sphere.transform.SetParent(
                parent,
                false);

            sphere.transform.localScale =
                Vector3.one *
                3200f;

            Collider collider =
                sphere.GetComponent<Collider>();

            if (collider != null)
            {
                UnityEngine.Object
                    .DestroyImmediate(
                        collider);
            }

            MeshRenderer renderer =
                sphere.GetComponent<MeshRenderer>();

            renderer.sharedMaterial =
                CreateEnvironmentMaterial(
                    "Sky",
                    texture,
                    transparent: false,
                    cull:
                        CullMode.Front);

            renderer.shadowCastingMode =
                ShadowCastingMode.Off;
            renderer.receiveShadows =
                false;

            return renderer;
        }

        private static Renderer CreateCloudLayer(
            Transform parent,
            string name,
            Texture2D texture,
            float height)
        {
            if (texture == null)
                return null;

            GameObject plane =
                GameObject.CreatePrimitive(
                    PrimitiveType.Plane);

            plane.name = name;

            plane.transform.SetParent(
                parent,
                false);

            plane.transform.localScale =
                new Vector3(
                    360f,
                    1f,
                    360f);

            plane.transform.position =
                new Vector3(
                    0f,
                    height,
                    0f);

            Collider collider =
                plane.GetComponent<Collider>();

            if (collider != null)
            {
                UnityEngine.Object
                    .DestroyImmediate(
                        collider);
            }

            MeshRenderer renderer =
                plane.GetComponent<MeshRenderer>();

            renderer.sharedMaterial =
                CreateEnvironmentMaterial(
                    name,
                    texture,
                    transparent: true,
                    cull:
                        CullMode.Off);

            renderer.shadowCastingMode =
                ShadowCastingMode.Off;
            renderer.receiveShadows =
                false;

            return renderer;
        }

        private static Material CreateEnvironmentMaterial(
            string label,
            Texture2D texture,
            bool transparent,
            CullMode cull)
        {
            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Unlit");

            if (shader == null)
            {
                throw new InvalidOperationException(
                    "URP Unlit shader is unavailable.");
            }

            string path =
                OutputRoot +
                "/Sky/" +
                label +
                ".mat";

            AssetDatabase.DeleteAsset(path);

            Material material =
                new Material(shader)
                {
                    name =
                        "Map000_" +
                        label
                };

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

            if (material.HasProperty(
                    "_Cull"))
            {
                material.SetFloat(
                    "_Cull",
                    (float)cull);
            }

            if (transparent)
            {
                if (material.HasProperty(
                        "_Surface"))
                {
                    material.SetFloat(
                        "_Surface",
                        1f);
                }

                if (material.HasProperty(
                        "_ZWrite"))
                {
                    material.SetFloat(
                        "_ZWrite",
                        0f);
                }

                material.EnableKeyword(
                    "_SURFACE_TYPE_TRANSPARENT");

                material.renderQueue =
                    (int)RenderQueue.Transparent;
            }
            else
            {
                material.renderQueue =
                    (int)RenderQueue.Background;
            }

            AssetDatabase.CreateAsset(
                material,
                path);

            return material;
        }

        private static Bounds ToBounds(
            LegacyBounds source)
        {
            Vector3 min =
                Vector3.Min(
                    source.Lower,
                    source.Upper);

            Vector3 max =
                Vector3.Max(
                    source.Lower,
                    source.Upper);

            Bounds result =
                new Bounds();

            result.SetMinMax(
                min,
                max);

            return result;
        }

        private static void CopyAsset(
            string source,
            string assetPath)
        {
            string destination =
                Path.GetFullPath(
                    assetPath);

            string directory =
                Path.GetDirectoryName(
                    destination);

            if (!string.IsNullOrWhiteSpace(
                    directory))
            {
                Directory.CreateDirectory(
                    directory);
            }

            File.Copy(
                source,
                destination,
                true);

            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions
                    .ForceSynchronousImport);
        }

        private static void EnsureFolder(
            string assetPath)
        {
            string[] parts =
                assetPath.Split('/');

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
                        .IsValidFolder(
                            next))
                {
                    AssetDatabase
                        .CreateFolder(
                            current,
                            parts[i]);
                }

                current =
                    next;
            }
        }
    }
}
