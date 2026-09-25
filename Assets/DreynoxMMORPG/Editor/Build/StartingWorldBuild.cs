using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.Importing;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.Editor.Quests;
using Dreynox.Mmorpg.Editor.Rendering;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.Interaction;
using Dreynox.Mmorpg.Parity;
using Dreynox.Mmorpg.Quests;
using Dreynox.Mmorpg.UI;
using Dreynox.Mmorpg.World;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.Build
{
    public static class StartingWorldBuild
    {
        public const string Output = "Builds/WindowsStartingWorld";
        private const string StampPath = "Artifacts/StartingWorld/prepared.json";
        private const string HudPath = "Assets/DreynoxMMORPG/LocalLegacyGenerated/World/Map001/Interface";
        [Serializable] private sealed class Prepared
        {
            public int schema=1,mapId=1;
            public string sourceBaseline="a92c222169d07e23ff6b58db998f459f6dd47a36";
            public string sourceHash,dependencyHash,scene=LegacyWorldTerrainImporter.Map1ScenePath;
            public string generatedUtc;
        }
        [MenuItem("Dreynox MMORPG/Build/Starting world/1 Prepare real Map1 content")]
        public static void PrepareBatch()
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();
            if(corpus==null||!corpus.Validate().IsCanonical)throw new BuildFailedException("Verified DATA corpus is required.");
            if(!File.Exists(corpus.Resolve("DATA_Español/world/1.wld")))throw new BuildFailedException("Map1 is absent. No Map0/sandbox substitute permitted.");
            var total=System.Diagnostics.Stopwatch.StartNew();
            Stage("pipeline",WorldRenderPipelineSetup.EnsureConfigured);
            Stage("map1-models-world-collision",LegacyWorldTerrainImporter.BuildCanonicalMap1);
            Stage("enhanced-color-batch-atmosphere",EnhancedWorldPresentation.ApplyToCurrentWorld);
            var actor=UnityEngine.Object.FindFirstObjectByType<ShaiyaClientActor>();
            var monsters=UnityEngine.Object.FindFirstObjectByType<LegacyMonsterSpawnStreamer>();
            var npcs=UnityEngine.Object.FindFirstObjectByType<LegacyNpcSpawnStreamer>();
            var camera=Camera.main;
            if(actor==null||monsters==null||npcs==null||camera==null||Terrain.activeTerrain==null)
                throw new BuildFailedException("Incomplete real starting world.");
            if(monsters.LogicalSpawnCount!=1186||npcs.LogicalSpawnCount!=307)
                throw new BuildFailedException("Map1 instance population mismatch.");
            if(!npcs.Spawns.Any(n=>n.npcType==7&&n.typeId==1081)||!npcs.Spawns.Any(n=>n.npcType==7&&n.typeId==1167))
                throw new BuildFailedException("Ethan/Instructor original NPC definitions missing.");
            foreach(var debug in UnityEngine.Object.FindObjectsByType<ParityDebugHud>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(debug.gameObject);
            TextAsset catalog=null;Stage("quest-catalog-4085",()=>catalog=LegacyQuestCatalogImporter.Import(corpus));
            Stage("native-ui-textures",()=>ImportInterface(corpus));
            var game=new GameObject("Map1_LocalGameSession");
            var combat=game.AddComponent<ShaiyaCombatInteraction>();combat.Bind(actor,camera);
            var journal=game.AddComponent<QuestJournalRuntime>();journal.Configure(catalog,actor,combat,monsters);
            var hud=NativeWorldHudBuilder.Create(corpus);
            var questUi=game.AddComponent<QuestWorldPanel>();questUi.Configure(hud,journal,
                AssetDatabase.LoadAssetAtPath<Sprite>(HudPath+"/take.tga"));
            game.AddComponent<StartingWorldQualification>();
            EditorSceneManager.MarkSceneDirty(actor.gameObject.scene);
            EditorSceneManager.SaveScene(actor.gameObject.scene,LegacyWorldTerrainImporter.Map1ScenePath);
            AssetDatabase.SaveAssets();
            var stamp=new Prepared {sourceHash=SourceHash(),dependencyHash=AssetDatabase.GetAssetDependencyHash(LegacyWorldTerrainImporter.Map1ScenePath).ToString(),generatedUtc=DateTime.UtcNow.ToString("O")};
            Directory.CreateDirectory(Path.GetDirectoryName(StampPath));File.WriteAllText(StampPath,JsonUtility.ToJson(stamp,true));
            Debug.Log("DREYNOX_STARTING_WORLD_PREPARED map=1 npcPositions=307 mobs=1186 quests=4085 ms="+total.ElapsedMilliseconds);
        }
        [MenuItem("Dreynox MMORPG/Build/Starting world/2 Build previously prepared Map1 Player")]
        public static void BuildPreparedBatch()
        {
            if(!File.Exists(StampPath))throw new BuildFailedException("Run PrepareBatch first.");
            var prepared=JsonUtility.FromJson<Prepared>(File.ReadAllText(StampPath));
            if(prepared==null||prepared.schema!=1||prepared.mapId!=1||prepared.sourceHash!=SourceHash()||
               prepared.scene!=LegacyWorldTerrainImporter.Map1ScenePath||!File.Exists(prepared.scene)||
               prepared.dependencyHash!=AssetDatabase.GetAssetDependencyHash(prepared.scene).ToString())
                throw new BuildFailedException("Source or prepared content changed. Regenerate; stale assets must not pass.");
            WorldRenderPipelineSetup.EnsureConfigured();
            PlayerSettings.companyName="Dreynox";PlayerSettings.productName="Dreynox MMORPG - Inicio Map1";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.runInBackground=true;
            var flags=BuildOptions.Development|BuildOptions.CompressWithLz4HC;
            LocalDataBuildGuard.ValidateRequest(new[]{prepared.scene},flags,false,"starting-world-local-integration");
            Directory.CreateDirectory(Output);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{prepared.scene},target=BuildTarget.StandaloneWindows64,
                options=flags,locationPathName=Path.GetFullPath(Output+"/DreynoxMmorpg-Map1.exe")});
            if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Starting world Player failed: "+report.summary.result);
            File.WriteAllText(Output+"/LEEME.txt","Map 1 con contenido original preconvertido. No es la replica completa ni un cliente conectado al servidor nativo.\nWASD mover; Shift correr; RMB camara; rueda zoom; clic/F NPC; L diario; clic enemigo + 1 ataque local.\nLa mision 3400 usa sus objetivos y recompensas originales; las reglas especiales no implementadas se bloquean.\nEl dano local no reproduce todavia las formulas nativas; las recompensas no implican subida de nivel.\nDATA no es necesaria al ejecutar el Player. Conserve todos los archivos de esta carpeta.\n");
            File.Copy(StampPath,Output+"/source-and-content.json",true);
            var rows=new List<string>();
            using(var hash=SHA256.Create())foreach(string file in Directory.GetFiles(Output,"*",SearchOption.AllDirectories).OrderBy(x=>x,StringComparer.Ordinal))
            {
                if(file.EndsWith("SHA256SUMS.txt",StringComparison.Ordinal))continue;
                using(var stream=File.OpenRead(file))rows.Add(Hex(hash.ComputeHash(stream))+"  "+file.Substring(Output.Length+1).Replace('\\','/'));
            }
            File.WriteAllLines(Output+"/SHA256SUMS.txt",rows);
            Debug.Log("DREYNOX_STARTING_WORLD_BUILD_OK "+Output);
        }
        private static void ImportInterface(CanonicalClientCorpus corpus)
        {
            Directory.CreateDirectory(HudPath);
            var paths=new[]{("DATA_Español/interface/quest/take.tga",HudPath+"/take.tga")};
            foreach(var pair in paths)File.Copy(corpus.Resolve(pair.Item1),pair.Item2,true);
            AssetDatabase.StartAssetEditing();try{foreach(var pair in paths)AssetDatabase.ImportAsset(pair.Item2);}finally{AssetDatabase.StopAssetEditing();}
            foreach(var pair in paths)
            {
                var importer=AssetImporter.GetAtPath(pair.Item2) as TextureImporter;
                if(importer==null)throw new BuildFailedException("Original UI texture import failed: "+pair.Item1);
                importer.textureType=TextureImporterType.Sprite;
                importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.sRGBTexture=true;
                importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;
                importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
                importer.textureCompression=TextureImporterCompression.Uncompressed;AssetDatabase.WriteImportSettingsIfDirty(pair.Item2);
            }
            AssetDatabase.StartAssetEditing();try{foreach(var pair in paths)AssetDatabase.ImportAsset(pair.Item2);}finally{AssetDatabase.StopAssetEditing();}
        }
        private static void Stage(string label,Action work)
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();Debug.Log("DREYNOX_STAGE_BEGIN "+label);
            try{work();Debug.Log("DREYNOX_STAGE_END "+label+" ms="+watch.ElapsedMilliseconds);}
            catch(Exception ex){Debug.LogError("DREYNOX_STAGE_FAILED "+label+" ms="+watch.ElapsedMilliseconds+" "+ex.Message);throw;}
        }
        private static string SourceHash()
        {
            var text=new StringBuilder();
            using(var hash=SHA256.Create())foreach(string p in Directory.GetFiles("Assets/DreynoxMMORPG","*",SearchOption.AllDirectories)
                .Where(p=>p.EndsWith(".cs")||p.EndsWith(".shader")||p.EndsWith(".asmdef")).OrderBy(p=>p,StringComparer.Ordinal))
                text.Append(p.Replace('\\','/')).Append(':').Append(Hex(hash.ComputeHash(File.ReadAllBytes(p)))).Append('\n');
            text.Append(File.ReadAllText("ProjectSettings/ProjectVersion.txt")).Append(File.ReadAllText("Packages/manifest.json"));
            using(var hash=SHA256.Create())return Hex(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }
        private static string Hex(byte[] bytes)=>BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
    }
}
