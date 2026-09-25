using System;
using Dreynox.Mmorpg.Editor.Rendering;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
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
    public static class PlayableWorldBuild
    {
        public const string Output = "Builds/WindowsPlayableMap0";
        [MenuItem("Dreynox MMORPG/Build/Windows x64/Playable Map 0 qualification")]
        public static void RunBatch()
        {
            var corpus=CanonicalClientCorpus.FromStoredRoot();
            if (corpus==null || !corpus.Validate().IsCanonical) throw new BuildFailedException("Verified content anchors required.");
            WorldRenderPipelineSetup.EnsureConfigured();
            LegacyWorldTerrainImporter.BuildCanonicalMap0();
            EnhancedWorldPresentation.ApplyToCurrentWorld();
            AssetDatabase.SaveAssets();
            var actor=UnityEngine.Object.FindFirstObjectByType<ShaiyaClientActor>();
            var streamer=UnityEngine.Object.FindFirstObjectByType<LegacyMonsterSpawnStreamer>();
            var camera=Camera.main;
            if (actor==null || streamer==null || camera==null || Terrain.activeTerrain==null)
                throw new BuildFailedException("Incomplete world; refusing to substitute the synthetic Parity Lab.");
            if (!actor.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.sharedMesh!=null && r.sharedMaterial!=null))
                throw new BuildFailedException("Original skinned character is missing.");
            if (streamer.LogicalSpawnCount==0 || streamer.Spawns.Any(s=>s.prefab==null))
                throw new BuildFailedException("Missing real monster prefabs.");
            var root=new GameObject("WorldQualification");
            var combat=root.AddComponent<ShaiyaCombatInteraction>(); combat.Bind(actor,camera);
            root.AddComponent<WorldQualificationScenario>();
            EditorSceneManager.MarkSceneDirty(actor.gameObject.scene);
            EditorSceneManager.SaveScene(actor.gameObject.scene,LegacyWorldTerrainImporter.ScenePath);
            Directory.CreateDirectory(Output);
            PlayerSettings.companyName="Dreynox";PlayerSettings.productName="Dreynox Mmorpg - Map 0";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            BuildOptions flags=BuildOptions.Development|BuildOptions.CompressWithLz4HC;
            LocalDataBuildGuard.ValidateRequest(new[]{LegacyWorldTerrainImporter.ScenePath},flags,false,"map0-qualification");
            int logicalSpawns=streamer.LogicalSpawnCount;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{LegacyWorldTerrainImporter.ScenePath},target=BuildTarget.StandaloneWindows64,
                locationPathName=Path.GetFullPath(Output+"/DreynoxMmorpg-Map0.exe"),options=flags
            });
            if (report.summary.result!=BuildResult.Succeeded) throw new BuildFailedException("Map0 build failed: "+report.summary.result);
            // Hash the complete Player payload, not just Unity's generic launcher stub.
            using(var sha=SHA256.Create())
            using(var writer=new StreamWriter(Output+"/SHA256SUMS.txt"))
                foreach(string file in Directory.GetFiles(Output,"*",SearchOption.AllDirectories).OrderBy(x=>x,StringComparer.Ordinal))
                {
                    if(file.EndsWith("SHA256SUMS.txt",StringComparison.Ordinal)) continue;
                    using(var stream=File.OpenRead(file))
                        writer.WriteLine(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()+"  "+file.Substring(Output.Length+1).Replace('\\','/'));
                }
            File.WriteAllText(Output+"/LEEME.txt","Prueba de integracion local de Map 0 con recursos originales preconvertidos. No es el MMORPG completo ni paridad certificada.\nWASD: caminar. Shift: correr. Raton derecho: camara. Rueda: zoom. Clic en mob: seleccionar. 1..4: atacar en alcance.\nConserve toda la carpeta; DATA no es necesaria para ejecutar este Player.\n");
            Debug.Log("DREYNOX_PLAYABLE_MAP0_BUILD_OK spawns="+logicalSpawns);
        }
    }
}
