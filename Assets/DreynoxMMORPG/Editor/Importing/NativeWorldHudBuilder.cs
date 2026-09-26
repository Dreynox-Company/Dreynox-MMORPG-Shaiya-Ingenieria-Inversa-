using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.UI;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.Importing
{
    public static class NativeWorldHudBuilder
    {
        public static NativeWorldHud Create(CanonicalClientCorpus corpus)
        {
            const string root="Assets/DreynoxMMORPG/LocalLegacyGenerated/NativeHud";
            Directory.CreateDirectory(root);
            string[] paths={"statusminibar/player_bar_bg.tga","statusminibar/class_attack_fighter.tga",
                "slot/main_1.tga","npctalk/talk1_bg.tga","minimap/minimap_1.tga",
                "statusminibar/enemy_bar_bg.tga","statusminibar/enemy_bar.tga"};
            var textures=new Texture2D[paths.Length];
            for(int i=0;i<paths.Length;i++)
            {
                string source=corpus.Resolve("DATA_Español/interface/"+paths[i]);
                string asset=root+"/"+Path.GetFileName(source);
                File.Copy(source,asset,true);
                AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(asset);
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
                importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;
                importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                textures[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(asset);
            }
            var hud=new GameObject("Native World Interface").AddComponent<NativeWorldHud>();
            hud.SetArtwork(textures[0],textures[1],textures[2],textures[3],textures[4]);
            hud.SetRadarSkin(NativeRadarSkinImporter.Import(corpus));
            hud.SetTargetArtwork(textures[5],textures[6]);
            return hud;
        }
    }
}
