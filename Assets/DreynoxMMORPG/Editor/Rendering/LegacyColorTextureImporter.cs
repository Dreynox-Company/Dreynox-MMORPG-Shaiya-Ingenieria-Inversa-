using System;
using System.Collections.Generic;
using System.IO;
using Dreynox.Mmorpg.Editor.ReverseEngineering.Binary;
using Dreynox.Mmorpg.LocalData;
using UnityEditor;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.Rendering
{
    /// <summary>Two-phase import: generate files, import once, configure once, then permit asset loads.</summary>
    public static class LegacyColorTextureImporter
    {
        public readonly struct Request
        {
            public readonly string Source, AssetPath;
            public readonly TextureWrapMode Wrap;
            public Request(string source,string path,TextureWrapMode wrap=TextureWrapMode.Repeat)
            {Source=source;AssetPath=path;Wrap=wrap;}
        }
        private sealed class Pending
        {
            public string source,path,stamp;
            public TextureWrapMode wrap;
            public bool copy;
        }
        private static readonly HashSet<string> ready = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public static void ClearSessionCache() { ready.Clear(); }
        public static string Destination(string source,string path) =>
            (string.Equals(Path.GetExtension(source),".dds",StringComparison.OrdinalIgnoreCase)
                ? Path.ChangeExtension(path,".png") : path).Replace('\\','/');

        public static void Prepare(IEnumerable<Request> requests)
        {
            if(requests==null)throw new ArgumentNullException(nameof(requests));
            var watch=System.Diagnostics.Stopwatch.StartNew();
            var unique=new Dictionary<string,Pending>(StringComparer.OrdinalIgnoreCase);
            string assets=Path.GetFullPath("Assets")+Path.DirectorySeparatorChar;
            foreach(var request in requests)
            {
                string path=Destination(request.Source,request.AssetPath), full=Path.GetFullPath(path);
                if(!full.StartsWith(assets,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Converted texture must stay inside project Assets.");
                if(!File.Exists(request.Source))throw new FileNotFoundException("Original texture missing.",request.Source);
                if(unique.TryGetValue(path,out var previous))
                {
                    if(!string.Equals(Path.GetFullPath(previous.source),Path.GetFullPath(request.Source),StringComparison.OrdinalIgnoreCase)||previous.wrap!=request.Wrap)
                        throw new InvalidOperationException("Conflicting texture requests for "+path);
                    continue;
                }
                string key=path+"|"+request.Wrap;
                if(ready.Contains(key)&&AssetDatabase.LoadAssetAtPath<Texture2D>(path)!=null)continue;
                string stamp="DREYNOX_COLOR_V2:"+FileFingerprint.Sha256(request.Source);
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                bool copy=importer==null||importer.userData!=stamp||!File.Exists(full);
                unique.Add(path,new Pending{source=request.Source,path=path,stamp=stamp,wrap=request.Wrap,copy=copy});
                if(copy)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(full));
                    if(string.Equals(Path.GetExtension(request.Source),".dds",StringComparison.OrdinalIgnoreCase))
                        File.WriteAllBytes(full,DecodeDdsToPng(File.ReadAllBytes(request.Source)));
                    else if(!string.Equals(Path.GetFullPath(request.Source),full,StringComparison.OrdinalIgnoreCase))
                        File.Copy(request.Source,full,true);
                }
            }
            // No asset loads or SaveAndReimport calls while the database is paused.
            AssetDatabase.StartAssetEditing();
            try { foreach(var item in unique.Values)if(item.copy)AssetDatabase.ImportAsset(item.path); }
            finally { AssetDatabase.StopAssetEditing(); }
            var configured=new List<string>();
            foreach(var item in unique.Values)
            {
                var importer=AssetImporter.GetAtPath(item.path) as TextureImporter;
                if(importer==null)throw new InvalidDataException("Color TextureImporter missing for "+item.path);
                if(importer.userData==item.stamp && importer.sRGBTexture && importer.mipmapEnabled &&
                    importer.filterMode==FilterMode.Trilinear && importer.anisoLevel==8 &&
                    importer.wrapMode==item.wrap && !importer.alphaIsTransparency && !importer.isReadable &&
                    importer.textureCompression==TextureImporterCompression.CompressedHQ) continue;
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=true;
                importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
                importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=false;
                importer.wrapMode=item.wrap;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;
                importer.isReadable=false;importer.textureCompression=TextureImporterCompression.CompressedHQ;
                importer.userData=item.stamp;
                AssetDatabase.WriteImportSettingsIfDirty(item.path);configured.Add(item.path);
            }
            AssetDatabase.StartAssetEditing();
            try { foreach(string path in configured)AssetDatabase.ImportAsset(path); }
            finally { AssetDatabase.StopAssetEditing(); }
            foreach(var item in unique.Values)
            {
                if(AssetDatabase.LoadAssetAtPath<Texture2D>(item.path)==null)
                    throw new InvalidDataException("Texture did not finish importing: "+item.path);
                ready.Add(item.path+"|"+item.wrap);
            }
            Debug.Log("DREYNOX_COLOR_BATCH count="+unique.Count+" configured="+configured.Count+" ms="+watch.ElapsedMilliseconds);
        }
        public static Texture2D Import(string source,string assetPath,TextureWrapMode wrap=TextureWrapMode.Repeat)
        {
            Prepare(new[]{new Request(source,assetPath,wrap)});
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Destination(source,assetPath));
        }
        public static byte[] DecodeDdsToPng(byte[] source)
        {
            DecodedDds data=LegacyDdsDecoder.Decode(source);
            var texture=new Texture2D(data.Width,data.Height,TextureFormat.RGBA32,false,true);
            try {texture.LoadRawTextureData(data.Pixels);texture.Apply(false,false);return texture.EncodeToPNG();}
            finally {UnityEngine.Object.DestroyImmediate(texture);}
        }
    }
}
