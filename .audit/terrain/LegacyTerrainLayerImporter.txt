using System;
using System.Collections.Generic;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.Importing;
using Dreynox.Mmorpg.Editor.Rendering;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.LegacyFormats
{
    public static class LegacyTerrainLayerImporter
    {
        public static string ResolveTexture(string root, string authoredName)
        {
            if (string.IsNullOrWhiteSpace(authoredName))
                throw new InvalidDataException("An authored terrain texture name is required.");
            string relative = "DATA_Español/terrain/detail/" + authoredName;
            string exact = CanonicalCorpusPaths.Resolve(root, relative);
            if (File.Exists(exact)) return exact;
            // Original WLD may name a TGA whose shipped counterpart is DDS.
            // Resolve only that identical basename; never substitute another texture.
            if (string.Equals(Path.GetExtension(authoredName), ".tga", StringComparison.OrdinalIgnoreCase))
            {
                string dds = CanonicalCorpusPaths.Resolve(root, Path.ChangeExtension(relative, ".dds"));
                if (File.Exists(dds)) return dds;
            }
            throw new FileNotFoundException("Original WLD terrain texture is absent: " + authoredName, exact);
        }
        public static TerrainLayer[] Import(CanonicalClientCorpus corpus, LegacyWldTerrainFile wld,
            string outputRoot, int mapId)
        {
            if (corpus == null) throw new ArgumentNullException(nameof(corpus));
            if (wld == null || wld.Textures.Count == 0) throw new InvalidDataException("No authored terrain layers.");
            var requests = new List<LegacyColorTextureImporter.Request>();
            var paths = new string[wld.Textures.Count];
            for (int i = 0; i < wld.Textures.Count; i++)
            {
                LegacyWldTexture layer = wld.Textures[i];
                if (float.IsNaN(layer.TileSize) || float.IsInfinity(layer.TileSize) || layer.TileSize <= 0)
                    throw new InvalidDataException("Terrain layer UV scale is invalid at index " + i);
                string source = ResolveTexture(corpus.RootPath, layer.TextureName);
                string destination = outputRoot + "/Textures/" + Path.GetFileName(source).ToLowerInvariant();
                paths[i] = LegacyColorTextureImporter.Destination(source, destination);
                requests.Add(new LegacyColorTextureImporter.Request(source, destination, TextureWrapMode.Repeat));
                Debug.Log("DREYNOX_TERRAIN_TEXTURE map=" + mapId + " layer=" + i +
                    " authored=" + layer.TextureName + " source=" + Path.GetFileName(source));
            }
            LegacyColorTextureImporter.Prepare(requests);
            var result = new TerrainLayer[wld.Textures.Count];
            for (int i = 0; i < result.Length; i++)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[i]);
                if (texture == null) throw new InvalidDataException("Converted terrain texture missing: " + paths[i]);
                var source = wld.Textures[i];
                var layer = new TerrainLayer
                {
                    name = "Map" + mapId.ToString("D3") + "_" + i.ToString("D2") + "_" + Path.GetFileNameWithoutExtension(source.TextureName),
                    diffuseTexture = texture,
                    tileSize = new Vector2(source.TileSize, source.TileSize)
                };
                EnhancedWorldPresentation.ConfigureTerrainLayer(layer);
                string path = outputRoot + "/TerrainLayers/Layer_" + i.ToString("D2") + ".terrainlayer";
                LegacyAssetWriteBatch.DeleteAsset(path);
                LegacyAssetWriteBatch.CreateAsset(layer, path);
                result[i] = layer;
            }
            return result;
        }
    }
}
