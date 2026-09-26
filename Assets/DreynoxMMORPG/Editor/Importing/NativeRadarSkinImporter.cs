using System;
using System.Collections.Generic;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.UI;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.Importing
{
    public static class NativeRadarSkinImporter
    {
        private const string Root = "Assets/DreynoxMMORPG/LocalLegacyGenerated/NativeHud/Radar";
        // Original resources referenced by ps0032's radar initialization (0x599490),
        // not solid UI rectangles with approximate tints.
        private static readonly string[] Names =
        {
            "arrow.tga", "npcarrow.tga", "minimap_icon_monster.tga", "minimap_icon_pc.tga",
            "minimap_icon_party.tga", "radar_enemy.tga", "radar_queststart.tga", "radar_questend.tga",
            "radar_questlow.tga", "map_filter_icon_portal.tga", "map_filter_icon_warehouse.tga",
            "map_filter_icon_blacksmith.tga", "minimap_icon_miscellaneous.tga", "minimap_icon_weapon.tga",
            "minimap_icon_armour.tga", "minimap_icon_event.tga"
        };
        public static NativeRadarSkin Import(CanonicalClientCorpus corpus)
        {
            if (corpus == null) throw new ArgumentNullException(nameof(corpus));
            Directory.CreateDirectory(Root);
            var entries = new NativeRadarIcon[Names.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                string source = "DATA_Español/interface/rader/" + Names[i];
                Texture2D texture = ImportTexture(corpus.Resolve(source), Root + "/" + Names[i], true);
                string spritePath = Root + "/" + Path.GetFileNameWithoutExtension(Names[i]) + ".asset";
                entries[i] = new NativeRadarIcon { kind = (NativeRadarKind)i, source = source,
                    sprite = CreateSprite(texture, new Rect(0, 0, texture.width, texture.height), spritePath) };
            }
            Texture2D frame = ImportTexture(corpus.Resolve("DATA_Español/interface/rader/main_map.tga"), Root + "/main_map.tga", false);
            Texture2D buttons = ImportTexture(corpus.Resolve("DATA_Español/interface/rader/button/main_map_button.tga"), Root + "/main_map_button.tga", false);
            var plus = CreateSprite(buttons, new Rect(0, 0, 16, 16), Root + "/zoom-in.asset");
            var minus = CreateSprite(buttons, new Rect(0, 16, 16, 16), Root + "/zoom-out.asset");
            string path = Root + "/NativeRadarSkin.asset";
            var skin = AssetDatabase.LoadAssetAtPath<NativeRadarSkin>(path);
            if (skin == null) { skin = ScriptableObject.CreateInstance<NativeRadarSkin>(); AssetDatabase.CreateAsset(skin, path); }
            skin.Configure(frame, plus, minus, entries); EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            return skin;
        }
        public static Texture2D ImportTexture(string source, string destination, bool readable)
        {
            // Small UI textures stay uncompressed: do not destroy the one-pixel
            // outline or premultiply/dilate the native RGBA colors.
            File.Copy(source, destination, true);
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(destination) as TextureImporter;
            if (importer == null) throw new InvalidDataException("Native UI import failed: " + source);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = false;
            importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = readable; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(destination);
        }
        private static Sprite CreateSprite(Texture2D texture, Rect topLeftPixels, string path)
        {
            Rect rect = new Rect(topLeftPixels.x, texture.height - topLeftPixels.yMax, topLeftPixels.width, topLeftPixels.height);
            var value = Sprite.Create(texture, rect, Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
            value.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(value, path); return value;
        }
    }
}
