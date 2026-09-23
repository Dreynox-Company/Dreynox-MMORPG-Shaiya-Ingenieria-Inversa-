using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Vfx;
using Dreynox.Mmorpg.World;
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

            string eftPath =
                CanonicalResourceIndex.FindUnique(
                    effectRoot,
                    normalized);

            if (eftPath == null)
            {
                string withExtension =
                    Path.HasExtension(normalized)
                        ? normalized
                        : normalized + ".eft";

                eftPath =
                    CanonicalResourceIndex.FindUnique(
                        effectRoot,
                        withExtension);
            }

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
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        prefabPath);

                if (existing != null)
                {
                    SessionCache[cacheKey] = existing;
                    return existing;
                }
            }

            EnsureFolder(assetRoot);
            EnsureFolder(assetRoot + "/Meshes");
            EnsureFolder(assetRoot + "/Clips");
            EnsureFolder(assetRoot + "/Textures");
            EnsureFolder(assetRoot + "/Materials");
            EnsureFolder(assetRoot + "/Prefabs");

            LegacyVertexEffectClip[] meshClips =
                new LegacyVertexEffectClip[
                    eft.MeshNames.Count];

            Material[] meshMaterials =
                new Material[
                    eft.MeshNames.Count];

            for (int meshIndex = 0;
                 meshIndex < eft.MeshNames.Count;
                 meshIndex++)
            {
                string meshName =
                    eft.MeshNames[meshIndex];

                string meshPath =
                    CanonicalResourceIndex.FindUnique(
                        effectRoot,
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

                AssetDatabase.DeleteAsset(
                    meshAssetPath);

                AssetDatabase.CreateAsset(
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

                AssetDatabase.DeleteAsset(
                    clipPath);

                AssetDatabase.CreateAsset(
                    clip,
                    clipPath);

                meshClips[meshIndex] = clip;

                string textureName =
                    ResolveTextureName(
                        eft,
                        source,
                        meshIndex);

                meshMaterials[meshIndex] =
                    ImportTransparentMaterial(
                        effectRoot,
                        textureName,
                        assetRoot,
                        meshIndex);
            }

            GameObject root =
                new GameObject(
                    "EFT_" + safeName);

            try
            {
                LegacyEftEffectPlayer[] players =
                    new LegacyEftEffectPlayer[
                        eft.Effects.Count];

                for (int effectIndex = 0;
                     effectIndex < eft.Effects.Count;
                     effectIndex++)
                {
                    LegacyEftEffect sourceEffect =
                        eft.Effects[effectIndex];

                    GameObject child =
                        new GameObject(
                            "Effect_" +
                            effectIndex.ToString("D3") +
                            "_" +
                            Sanitize(sourceEffect.Name));

                    child.transform.SetParent(
                        root.transform,
                        false);

                    MeshRenderer renderer = null;
                    LegacyVertexEffectPlayer vertexPlayer =
                        null;

                    if (sourceEffect.MeshIndex >= 0 &&
                        sourceEffect.MeshIndex <
                        meshClips.Length)
                    {
                        MeshFilter filter =
                            child.AddComponent<MeshFilter>();

                        renderer =
                            child.AddComponent<MeshRenderer>();

                        LegacyVertexEffectClip clip =
                            meshClips[
                                sourceEffect.MeshIndex];

                        filter.sharedMesh =
                            clip.BaseMesh;

                        renderer.sharedMaterial =
                            meshMaterials[
                                sourceEffect.MeshIndex];

                        vertexPlayer =
                            child.AddComponent<
                                LegacyVertexEffectPlayer>();

                        vertexPlayer.Configure(
                            clip,
                            shouldLoop: false);
                    }

                    LegacyEftEffectPlayer player =
                        child.AddComponent<
                            LegacyEftEffectPlayer>();

                    LegacyEftRotationKey[] rotations =
                        sourceEffect.Rotations
                            .Select(
                                value =>
                                    new LegacyEftRotationKey
                                    {
                                        rotation =
                                            LegacyCoordinateBridge
                                                .Rotation(
                                                    value.Rotation),
                                        time =
                                            Mathf.Max(
                                                0f,
                                                value.Time)
                                    })
                            .OrderBy(value => value.time)
                            .ToArray();

                    LegacyEftOpacityKey[] opacity =
                        sourceEffect.OpacityFrames
                            .Select(
                                value =>
                                    new LegacyEftOpacityKey
                                    {
                                        opacity =
                                            Mathf.Clamp01(
                                                value.Opacity),
                                        time =
                                            Mathf.Max(
                                                0f,
                                                value.Time)
                                    })
                            .OrderBy(value => value.time)
                            .ToArray();

                    float duration =
                        ResolveEffectDuration(
                            sourceEffect,
                            sourceEffect.MeshIndex >= 0 &&
                            sourceEffect.MeshIndex <
                            meshClips.Length
                                ? meshClips[
                                    sourceEffect.MeshIndex]
                                : null);

                    player.Configure(
                        vertexPlayer,
                        renderer,
                        LegacyCoordinateBridge.Position(
                            sourceEffect.Position),
                        Quaternion.identity,
                        rotations,
                        opacity,
                        duration,
                        shouldLoop: false);

                    players[effectIndex] =
                        player;

                    child.SetActive(false);
                }

                LegacyEftSequenceDefinition[] sequences =
                    BuildSequences(
                        eft,
                        players);

                LegacyEftSequencePlayer sequencePlayer =
                    root.AddComponent<
                        LegacyEftSequencePlayer>();

                sequencePlayer.Configure(
                    players,
                    sequences);

                AssetDatabase.DeleteAsset(
                    prefabPath);

                GameObject prefab =
                    PrefabUtility.SaveAsPrefabAsset(
                        root,
                        prefabPath);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                SessionCache[cacheKey] = prefab;
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

        private static Mesh BuildMesh(
            string effectName,
            int meshIndex,
            Legacy3deFile source)
        {
            int vertexCount =
                source.Vertices.Count;

            Vector3[] positions =
                new Vector3[vertexCount];

            Vector2[] uvs =
                new Vector2[vertexCount];

            for (int i = 0;
                 i < vertexCount;
                 i++)
            {
                positions[i] =
                    LegacyCoordinateBridge.Position(
                        source.Vertices[i].Position);

                uvs[i] =
                    source.Vertices[i].UV;
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

                triangles[i * 3] =
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

            mesh.vertices = positions;
            mesh.uv = uvs;
            mesh.triangles = triangles;
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
                 frameIndex < source.Frames.Count;
                 frameIndex++)
            {
                Legacy3deFrame sourceFrame =
                    source.Frames[frameIndex];

                Vector3[] positions =
                    new Vector3[
                        sourceFrame.Vertices.Count];

                Vector2[] uvs =
                    new Vector2[
                        sourceFrame.Vertices.Count];

                for (int vertexIndex = 0;
                     vertexIndex <
                     sourceFrame.Vertices.Count;
                     vertexIndex++)
                {
                    positions[vertexIndex] =
                        LegacyCoordinateBridge.Position(
                            sourceFrame
                                .Vertices[vertexIndex]
                                .Position);

                    uvs[vertexIndex] =
                        sourceFrame
                            .Vertices[vertexIndex]
                            .UV;
                }

                frames[frameIndex] =
                    new LegacyVertexEffectFrame
                    {
                        keyframe =
                            sourceFrame.Keyframe,
                        positions = positions,
                        uvs = uvs
                    };
            }

            return frames;
        }

        private static string ResolveTextureName(
            LegacyEftFile eft,
            Legacy3deFile mesh,
            int meshIndex)
        {
            if (!string.IsNullOrWhiteSpace(
                    mesh.TextureName))
            {
                return mesh.TextureName;
            }

            if (meshIndex >= 0 &&
                meshIndex <
                eft.TextureNames.Count)
            {
                return eft.TextureNames[
                    meshIndex];
            }

            return eft.TextureNames.Count > 0
                ? eft.TextureNames[0]
                : string.Empty;
        }

        private static Material
            ImportTransparentMaterial(
                string effectRoot,
                string textureName,
                string assetRoot,
                int meshIndex)
        {
            Texture2D texture = null;

            if (!string.IsNullOrWhiteSpace(
                    textureName))
            {
                string source =
                    CanonicalResourceIndex.FindUnique(
                        effectRoot,
                        textureName);

                if (source != null)
                {
                    string assetPath =
                        assetRoot +
                        "/Textures/" +
                        meshIndex.ToString("D3") +
                        "_" +
                        Path.GetFileName(source)
                            .ToLowerInvariant();

                    string absolute =
                        Path.GetFullPath(
                            assetPath);

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
                        assetPath,
                        ImportAssetOptions
                            .ForceSynchronousImport);

                    TextureImporter importer =
                        AssetImporter.GetAtPath(
                            assetPath)
                        as TextureImporter;

                    if (importer != null)
                    {
                        importer.sRGBTexture = true;
                        importer.mipmapEnabled = true;
                        importer.alphaIsTransparency =
                            true;
                        importer.wrapMode =
                            TextureWrapMode.Clamp;
                        importer.filterMode =
                            FilterMode.Bilinear;

                        importer.SaveAndReimport();
                    }

                    texture =
                        AssetDatabase
                            .LoadAssetAtPath<Texture2D>(
                                assetPath);
                }
            }

            Shader shader =
                Shader.Find(
                    "Universal Render Pipeline/Unlit");

            if (shader == null)
                shader =
                    Shader.Find(
                        "Unlit/Transparent");

            if (shader == null)
                throw new InvalidOperationException(
                    "No transparent shader is available.");

            Material material =
                new Material(shader)
                {
                    name =
                        "EffectMaterial_" +
                        meshIndex.ToString("D3"),
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
                else
                {
                    material.mainTexture =
                        texture;
                }
            }

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

            material.EnableKeyword(
                "_SURFACE_TYPE_TRANSPARENT");

            string materialPath =
                assetRoot +
                "/Materials/" +
                meshIndex.ToString("D3") +
                ".mat";

            AssetDatabase.DeleteAsset(
                materialPath);

            AssetDatabase.CreateAsset(
                material,
                materialPath);

            return material;
        }

        private static float ResolveEffectDuration(
            LegacyEftEffect effect,
            LegacyVertexEffectClip clip)
        {
            float duration =
                clip != null
                    ? clip.DurationSeconds
                    : 0f;

            for (int i = 0;
                 i < effect.Rotations.Count;
                 i++)
            {
                duration =
                    Mathf.Max(
                        duration,
                        effect.Rotations[i].Time);
            }

            for (int i = 0;
                 i < effect.OpacityFrames.Count;
                 i++)
            {
                duration =
                    Mathf.Max(
                        duration,
                        effect.OpacityFrames[i].Time);
            }

            for (int i = 0;
                 i < effect.Sub3.Count;
                 i++)
            {
                duration =
                    Mathf.Max(
                        duration,
                        effect.Sub3[i].Time);
            }

            return Mathf.Max(
                0.05f,
                duration);
        }

        private static LegacyEftSequenceDefinition[]
            BuildSequences(
                LegacyEftFile source,
                IReadOnlyList<
                    LegacyEftEffectPlayer> effects)
        {
            if (source.Sequences.Count == 0)
            {
                LegacyEftSequenceEvent[] events =
                    new LegacyEftSequenceEvent[
                        effects.Count];

                float duration = 0f;

                for (int i = 0;
                     i < effects.Count;
                     i++)
                {
                    events[i] =
                        new LegacyEftSequenceEvent
                        {
                            effectIndex = i,
                            time = 0f
                        };

                    if (effects[i] != null)
                    {
                        duration =
                            Mathf.Max(
                                duration,
                                effects[i].Duration);
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
                        sourceSequence.Records.Count];

                float duration = 0f;

                for (int eventIndex = 0;
                     eventIndex <
                     sourceSequence.Records.Count;
                     eventIndex++)
                {
                    LegacyEftSequenceRecord sourceEvent =
                        sourceSequence.Records[
                            eventIndex];

                    if (sourceEvent.EffectId < 0 ||
                        sourceEvent.EffectId >=
                        effects.Count)
                    {
                        throw new InvalidDataException(
                            "EFT sequence '" +
                            sourceSequence.Name +
                            "' references invalid effect " +
                            sourceEvent.EffectId + ".");
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

                    LegacyEftEffectPlayer player =
                        effects[
                            sourceEvent.EffectId];

                    duration =
                        Mathf.Max(
                            duration,
                            eventTime +
                            (player != null
                                ? player.Duration
                                : 0f));
                }

                Array.Sort(
                    events,
                    (a, b) =>
                        a.time.CompareTo(b.time));

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
            string normalized =
                LegacyMonEntityDescriptor
                    .NormalizeResourceName(value);

            if (string.IsNullOrWhiteSpace(
                    normalized))
                return string.Empty;

            return normalized.Trim();
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

            string current = parts[0];

            for (int i = 1;
                 i < parts.Length;
                 i++)
            {
                string next =
                    current + "/" +
                    parts[i];

                if (!AssetDatabase
                    .IsValidFolder(next))
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
