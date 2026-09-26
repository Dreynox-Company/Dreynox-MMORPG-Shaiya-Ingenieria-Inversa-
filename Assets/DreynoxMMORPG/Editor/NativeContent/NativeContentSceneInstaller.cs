using System;
using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.NativeContent;
using Dreynox.Mmorpg.Quests;
using Dreynox.Mmorpg.UI;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.NativeContent
{
    public static class NativeContentSceneInstaller
    {
        public static void Install(CanonicalClientCorpus corpus,GameObject session,NativeWorldHud hud,QuestJournalRuntime journal)
        {
            if(session==null||hud==null||journal==null)throw new ArgumentException("Original world session is required.");
            if(session.GetComponent<NativeContentPanel>()!=null)throw new InvalidOperationException("Duplicate content UI installation.");
            NativeCatalogAsset[] catalogs=NativeCatalogImporter.Import(corpus);
            var inventory=ImportArtwork(corpus,"inventory/bg.tga","inventory.tga");
            var skills=ImportArtwork(corpus,"skill/skill.tga","skills.tga");
            journal.SetItemCatalog(catalogs[0]);
            session.AddComponent<NativeContentPanel>().Configure(hud,journal,catalogs[0],catalogs[1],inventory,skills);
            var market=ImportArtwork(corpus,"basicshop/market.tga","merchant-market.tga");
            var marketSell=ImportArtwork(corpus,"basicshop/market_sell.tga","merchant-sell.tga");
            var questPanel=session.GetComponent<QuestWorldPanel>();
            if(questPanel==null)throw new InvalidOperationException("Quest/NPC service dispatcher is absent.");
            session.AddComponent<Dreynox.Mmorpg.Commerce.LocalMerchantPanel>().Configure(hud,questPanel,journal,catalogs[0],market,marketSell);
            Debug.Log("DREYNOX_NATIVE_CATALOGS_INSTALLED itemRows="+catalogs[0].Count+" skillRanks="+catalogs[1].Count+
                "; Editor-converted definitions, no runtime SData decryption or ownership grants.");
        }
        private static Texture2D ImportArtwork(CanonicalClientCorpus corpus,string sourceName,string outputName)
        {
            string output=NativeCatalogImporter.Root+"/"+outputName;
            File.Copy(corpus.Resolve("DATA_Español/interface/"+sourceName),output,true);
            AssetDatabase.ImportAsset(output,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(output) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("Original interface import failed: "+sourceName);
            importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;importer.sRGBTexture=true;
            importer.alphaIsTransparency=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(output);
        }
    }
}
