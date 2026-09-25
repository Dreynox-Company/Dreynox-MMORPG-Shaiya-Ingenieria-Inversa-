using System;
using System.IO;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Binary;
using Dreynox.Mmorpg.LocalData;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.Rendering
{
    /// <summary>
    /// DDS uses IHVImageFormatImporter, not TextureImporter. Decode the authored
    /// base mip losslessly to PNG so sRGB, mip generation and filtering are explicit.
    /// Only color textures use this path. Lightmaps/normal maps remain linear.
    /// </summary>
    public static class LegacyColorTextureImporter
    {
        public static Texture2D Import(string source, string assetPath,
            TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            if (!File.Exists(source)) throw new FileNotFoundException("Missing original color texture.", source);
            bool dds = string.Equals(Path.GetExtension(source), ".dds", StringComparison.OrdinalIgnoreCase);
            if (dds) assetPath = Path.ChangeExtension(assetPath, ".png");
            assetPath = assetPath.Replace('\\', '/');
            string full = Path.GetFullPath(assetPath);
            string assets = Path.GetFullPath("Assets") + Path.DirectorySeparatorChar;
            if (!full.StartsWith(assets, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Converted texture must stay inside project Assets.");
            string stamp = "DREYNOX_COLOR_V1:" + FileFingerprint.Sha256(source);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null || importer.userData != stamp || !File.Exists(full))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                if (dds) File.WriteAllBytes(full, DecodeDdsToPng(File.ReadAllBytes(source)));
                else if (!string.Equals(Path.GetFullPath(source), full, StringComparison.OrdinalIgnoreCase))
                    File.Copy(source, full, true);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            }
            if (importer == null)
                throw new InvalidDataException("Expected configurable color TextureImporter for " + assetPath);
            if (importer.userData != stamp || !importer.sRGBTexture || !importer.mipmapEnabled ||
                importer.filterMode != FilterMode.Trilinear || importer.anisoLevel != 8 ||
                importer.wrapMode != wrap || importer.alphaIsTransparency || importer.isReadable ||
                importer.textureCompression != TextureImporterCompression.CompressedHQ)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 4096;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                // Preserve transparent RGB and alpha; no color dilation of source pixels.
                importer.alphaIsTransparency = false;
                importer.wrapMode = wrap;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.isReadable = false;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.userData = stamp;
                importer.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null) throw new InvalidDataException("Color texture failed import: " + assetPath);
            return texture;
        }

        public static byte[] DecodeDdsToPng(byte[] source)
        {
            DecodedDds data = LegacyDdsDecoder.Decode(source);
            // Decoder already returns bottom row first. Do NOT flip again.
            var texture = new Texture2D(data.Width, data.Height, TextureFormat.RGBA32, false, true);
            try
            {
                texture.LoadRawTextureData(data.Pixels);
                texture.Apply(false, false);
                return texture.EncodeToPNG();
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
