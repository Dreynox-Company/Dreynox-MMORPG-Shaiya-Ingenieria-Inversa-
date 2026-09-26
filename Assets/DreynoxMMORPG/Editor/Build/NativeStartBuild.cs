using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.Importing;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.Editor.Rendering;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.Parity;
using Dreynox.Mmorpg.World;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dreynox.Mmorpg.Editor.Build
{
    public static class NativeStartBuild
    {
        public const string Output = "Builds/WindowsNativeStart";
        [MenuItem("Dreynox MMORPG/Build/Windows x64/Native start Map1 with NPC interaction")]
        public static void RunBatch()
        {
            var total=System.Diagnostics.Stopwatch.StartNew();
            var corpus=CanonicalClientCorpus.FromStoredRoot();
            if(corpus==null || !corpus.Validate().IsCanonical)throw new BuildFailedException("Original ps0032 DATA required.");
            WorldRenderPipelineSetup.EnsureConfigured();
            Debug.Log("DREYNOX_NATIVE_START_STAGE world-import");
            LegacyWorldTerrainImporter.BuildCanonicalMap1();
            Debug.Log("DREYNOX_NATIVE_START_STAGE presentation seconds="+total.Elapsed.TotalSeconds);
            EnhancedWorldPresentation.ApplyToCurrentWorld();
            var actor=UnityEngine.Object.FindFirstObjectByType<ShaiyaClientActor>();
            var session=UnityEngine.Object.FindFirstObjectByType<NativeWorldSession>();
            var mobs=UnityEngine.Object.FindFirstObjectByType<LegacyMonsterSpawnStreamer>();
            var npcs=UnityEngine.Object.FindFirstObjectByType<LegacyNpcSpawnStreamer>();
            if(actor==null || session==null || session.MapId!=1 || mobs==null || npcs==null || Camera.main==null)
                throw new BuildFailedException("Map1 runtime incomplete; no synthetic fallback.");
            if(!actor.GetComponentsInChildren<SkinnedMeshRenderer>().Any(x=>x.sharedMesh!=null && x.sharedMaterial!=null))
                throw new BuildFailedException("Original player mesh is absent.");
            var runtime=new GameObject("Native world qualification");
            var combat=runtime.AddComponent<ShaiyaCombatInteraction>();combat.Bind(actor,Camera.main);
            runtime.AddComponent<WorldQualificationScenario>();
            NativeWorldHudBuilder.Create(corpus);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(actor.gameObject.scene);
            EditorSceneManager.SaveScene(actor.gameObject.scene,LegacyWorldTerrainImporter.Map1ScenePath);
            PlayerSettings.companyName="Dreynox";PlayerSettings.productName="Dreynox MMORPG - Inicio nativo";
            PlayerSettings.runInBackground=true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            var scenes=new[]{LegacyWorldTerrainImporter.Map1ScenePath};
            var flags=BuildOptions.Development|BuildOptions.CompressWithLz4HC;
            LocalDataBuildGuard.ValidateRequest(scenes,flags,false,"native-start-qualification");
            Directory.CreateDirectory(Output);
            Debug.Log("DREYNOX_NATIVE_START_STAGE player-build seconds="+total.Elapsed.TotalSeconds);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=scenes, target=BuildTarget.StandaloneWindows64, options=flags,
                locationPathName=Path.GetFullPath(Output+"/DreynoxMmorpg-NativeStart.exe")});
            if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Native-start build failed: "+report.summary.result);
            File.WriteAllText(Output+"/LEEME.txt",
                "Dreynox MMORPG - Map1, contenido original, presentacion Unity mejorada.\n"+
                "Version de integracion local, no replica completa certificada ni servidor multijugador.\n"+
                "WASD mover; Shift correr; boton derecho orbitar; rueda zoom; clic mob seleccionar; 1-4 ataques de prueba; clic NPC cercano o F conversar; Escape cerrar.\n"+
                "Interfaz con arte original y salud del objetivo. Dialogos NPC son originales. Misiones/recompensas y reglas de servidor no estan completas.\n"+
                "No requiere carpeta DATA: preserve la carpeta completa del Player.\n");
            File.WriteAllText(Output+"/build-evidence.json",JsonUtility.ToJson(new Manifest{
                commit=Environment.GetEnvironmentVariable("GITHUB_SHA")??"local",
                mapId=1, authoredStart=new Vector3(580,78,1760), runtime=Application.unityVersion,
                importAndBuildSeconds=total.Elapsed.TotalSeconds, utc=DateTime.UtcNow.ToString("O")},true));
            using(var sha=SHA256.Create())
            using(var writer=new StreamWriter(Output+"/SHA256SUMS.txt"))
                foreach(string file in Directory.GetFiles(Output,"*",SearchOption.AllDirectories).OrderBy(x=>x,StringComparer.Ordinal))
                {
                    if(file.EndsWith("SHA256SUMS.txt",StringComparison.Ordinal))continue;
                    using(var input=File.OpenRead(file))writer.WriteLine(BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant()+"  "+file.Substring(Output.Length+1).Replace('\\','/'));
                }
            Debug.Log("DREYNOX_NATIVE_START_BUILD_OK seconds="+total.Elapsed.TotalSeconds);
        }
        [Serializable]private sealed class Manifest
        {public string commit,runtime,utc;public int mapId;public Vector3 authoredStart;public double importAndBuildSeconds;}
    }
}
