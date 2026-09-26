using System.IO;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Binary;
using Dreynox.Mmorpg.Quests;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.Quests
{
    public static class LegacyQuestCatalogImporter
    {
        public static TextAsset Import(CanonicalClientCorpus corpus)
        {
            string defsPath=corpus.Resolve("DATA_Español/npc/npcquest.sdata");
            string textPath=corpus.Resolve("DATA_Español/npc/npcquesttrans_spain.sdata");
            byte[] data=LegacySDataDecryptor.Decrypt(defsPath,true).Plaintext;
            byte[] texts=LegacySDataDecryptor.Decrypt(textPath,true).Plaintext;
            var npc=LegacyNpcQuestHeaderParser.ParsePlain(data);
            var translations=LegacyNpcQuestTranslationParser.ParsePlain(texts,npc);
            var catalog=LegacyQuestCatalogParser.Parse(data,npc.HeaderBytesConsumed,texts,translations.NpcTranslationBytesConsumed);
            if(catalog.quests.Length!=4085)throw new InvalidDataException("Canonical quest count changed.");
            catalog.sourceSha256=FileFingerprint.Sha256(defsPath);catalog.translationSha256=FileFingerprint.Sha256(textPath);
            const string folder="Assets/DreynoxMMORPG/LocalLegacyGenerated/Quests";
            Directory.CreateDirectory(folder);
            string path=folder+"/Ps0032_Quests_Spanish.json";
            string json=JsonUtility.ToJson(catalog);
            if(!File.Exists(path)||File.ReadAllText(path)!=json) File.WriteAllText(path,json,new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if(asset==null)throw new InvalidDataException("Quest catalog asset import failed.");
            Debug.Log("DREYNOX_QUEST_CATALOG records="+catalog.quests.Length+" defsEOF=true translationEOF=true");
            return asset;
        }
    }
}
