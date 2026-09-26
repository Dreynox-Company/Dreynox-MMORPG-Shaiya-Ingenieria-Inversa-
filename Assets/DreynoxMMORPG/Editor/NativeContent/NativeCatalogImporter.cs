using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.Editor.Rendering;
using Dreynox.Mmorpg.NativeContent;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.NativeContent
{
    public static class NativeCatalogImporter
    {
        public const string Root="Assets/DreynoxMMORPG/LocalLegacyGenerated/NativeContent";
        private static readonly string[] Names={"dbitemdata","dbitemtext_spn","dbskilldata","dbskilltext_spn"};
        private static readonly string[] SourceHashes={
            "cdb71e93b0f683b6db4adb9c45df7b1d95c481d3ab0d94092018217ad3afaa28",
            "f1b1e1129caf8ce95bff310a5bcadabb29fcbd674db0fa2e22819a30216b204b",
            "b7b99f7e681de8bc26d5cbe74e9ab45fbba0ab79491f8db51cde6732cb53218b",
            "f6570b0b80d56cd16d36e3c25e9fe39e69b52011c6b97918d8ac9f69e458754d"};
        private static readonly string[] PlainHashes={
            "7a2dffa9655b365e66997e65c0ec88ee44267337e251ddabc0bf8a14c0d8a0c9",
            SourceHashes[1],"beb148b8363b9147c8a7b2c494a0314c9ca9e78de566060852e876376a3b19d4",SourceHashes[3]};
        [Serializable] public sealed class Audit
        {
            public string scope="exact-original-tables-to-native-Unity-assets-not-complete-gameplay",failure="";
            public bool passed;
            public int itemRows,skillRanks,itemColumns,skillColumns,itemIcons,skillIcons,zeroItemIcons,missingItemIcons,missingSkillIcons;
            public List<string> exceptions=new List<string>();
            public string[] sources=(string[])SourceHashes.Clone(),plaintext=(string[])PlainHashes.Clone();
        }
        public static NativeDataTable[] ReadVerifiedTables(CanonicalClientCorpus corpus)
        {
            if(corpus==null)throw new ArgumentNullException(nameof(corpus));var tables=new NativeDataTable[4];
            for(int i=0;i<Names.Length;i++)
            {
                string path=corpus.Resolve("DATA_Español/binarysdata/"+Names[i]+".sdata");
                var info=new FileInfo(path);if(!info.Exists||info.Length>NativeDataTable.MaximumBytes)throw new InvalidDataException("Native table missing/oversized: "+Names[i]);
                byte[] raw=File.ReadAllBytes(path);
                if(Digest(raw)!=SourceHashes[i])throw new InvalidDataException("Different native table revision: "+Names[i]+". Qualify it before importing; no silent fallback.");
                // These two exact uploaded encrypted tables have stale embedded CRCs.
                // Exception is restricted by BOTH original-file and plaintext SHA256.
                // Unrecognized bytes never get a relaxed-checksum import.
                byte[] plain=LegacySDataDecryptor.Decrypt(raw,validateChecksum:false).Plaintext;
                if(Digest(plain)!=PlainHashes[i])throw new InvalidDataException("Native plaintext identity differs: "+Names[i]);
                tables[i]=(i&1)==0?NativeDataTable.ReadNumeric(plain):NativeDataTable.ReadText(plain);
                if(!plain.SequenceEqual(tables[i].ToPlaintext()))throw new InvalidDataException("Lossless native table roundtrip failed: "+Names[i]);
            }
            return tables;
        }
        public static NativeCatalogAsset[] Import(CanonicalClientCorpus corpus)
        {
            var audit=new Audit();Directory.CreateDirectory("Artifacts/StartingWorld");
            try
            {
                var tables=ReadVerifiedTables(corpus);
                var items=new NativeDefinitionCatalog(tables[0],tables[1],false);
                var skills=new NativeDefinitionCatalog(tables[2],tables[3],true);
                audit.itemRows=items.Count;audit.skillRanks=skills.Count;audit.itemColumns=tables[0].Columns.Count;audit.skillColumns=tables[2].Columns.Count;
                if(audit.itemRows!=28142||audit.skillRanks!=12060||audit.itemColumns!=70||audit.skillColumns!=101)
                    throw new InvalidDataException("Canonical full table coverage changed.");
                Directory.CreateDirectory(Root+"/Atlases");AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var textureCache=new Dictionary<string,Texture2D>(StringComparer.OrdinalIgnoreCase);
                var result=new[]{Build(corpus,items,tables[0],false,audit,textureCache),Build(corpus,skills,tables[2],true,audit,textureCache)};
                audit.passed=true;AssetDatabase.SaveAssets();return result;
            }
            catch(Exception ex){audit.failure=ex.ToString();throw;}
            finally{File.WriteAllText("Artifacts/StartingWorld/native-catalogs.json",JsonUtility.ToJson(audit,true));}
        }
        private static NativeCatalogAsset Build(CanonicalClientCorpus corpus,NativeDefinitionCatalog source,NativeDataTable table,bool skills,Audit audit,Dictionary<string,Texture2D> cache)
        {
            var rows=new List<NativeCatalogEntry>(source.Count);var textures=new List<Texture2D>();
            var indices=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in source.Entries)
            {
                NativeIconRegion icon;
                bool resolved=skills?NativeSkillIcon.TryResolve(entry.Value("image"),out icon):NativeItemIcon.TryResolve(entry.Key.Id,entry.Value("icon"),out icon);
                int atlas=-1;string issue="";
                if(!skills&&entry.Value("icon")==0){audit.zeroItemIcons++;issue="La fila original declara icon=0.";}
                else if(!resolved)issue="Regla de icono fuera del contrato recuperado.";
                else
                {
                    string path=corpus.Resolve("DATA_Español/interface/icon/"+icon.File);
                    if(!File.Exists(path))issue="Atlas original ausente: "+icon.File;
                    else
                    {
                        if(!cache.TryGetValue(icon.File,out Texture2D texture))
                        {
                            string asset=Root+"/Atlases/"+Path.ChangeExtension(icon.File,".png");
                            // Decode DDS losslessly once in Editor; TGA copies retain original RGBA.
                            if(Path.GetExtension(path).Equals(".dds",StringComparison.OrdinalIgnoreCase))File.WriteAllBytes(asset,LegacyColorTextureImporter.DecodeDdsToPng(File.ReadAllBytes(path)));
                            else{asset=Root+"/Atlases/"+icon.File;File.Copy(path,asset,true);}
                            AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceSynchronousImport);
                            var importer=(TextureImporter)AssetImporter.GetAtPath(asset);
                            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=false;
                            importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=false;
                            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
                            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.isReadable=false;importer.SaveAndReimport();
                            texture=AssetDatabase.LoadAssetAtPath<Texture2D>(asset);
                            if(texture==null)throw new InvalidDataException("Native atlas import failed: "+icon.File);
                            cache.Add(icon.File,texture);
                        }
                        if(!icon.Fits(texture.width,texture.height))issue="Recorte original fuera del atlas: "+icon.File+" "+icon.X+","+icon.Y;
                        else
                        {
                            if(!indices.TryGetValue(icon.File,out atlas)){atlas=textures.Count;textures.Add(texture);indices.Add(icon.File,atlas);}
                            if(skills)audit.skillIcons++;else audit.itemIcons++;
                        }
                    }
                }
                if(atlas<0&&!( !skills&&entry.Value("icon")==0))
                {
                    if(skills)audit.missingSkillIcons++;else audit.missingItemIcons++;
                    if(audit.exceptions.Count<100)audit.exceptions.Add((skills?"skill ":"item ")+entry.Key+": "+issue);
                }
                rows.Add(new NativeCatalogEntry(entry,table.Columns,atlas,icon,issue));
            }
            string destination=Root+(skills?"/Skills.asset":"/Items.asset");
            var assetObject=AssetDatabase.LoadAssetAtPath<NativeCatalogAsset>(destination);
            if(assetObject==null){assetObject=ScriptableObject.CreateInstance<NativeCatalogAsset>();AssetDatabase.CreateAsset(assetObject,destination);}
            int hashOffset=skills?2:0;
            assetObject.Configure(skills,table.Columns.ToArray(),rows.ToArray(),textures.ToArray(),SourceHashes[hashOffset],SourceHashes[hashOffset+1]);
            EditorUtility.SetDirty(assetObject);return assetObject;
        }
        private static string Digest(byte[] data)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
    }
}
