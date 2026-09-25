using System;
using System.Collections;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Gameplay.CameraSystem;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.UI;
using Dreynox.Mmorpg.World;
using UnityEngine;

namespace Dreynox.Mmorpg.Parity
{
    /// <summary>Exercises the actual packaged Map0 Player, not mocks. Opt-in Development build only.</summary>
    public sealed class WorldQualificationScenario : MonoBehaviour
    {
        [Serializable] private sealed class Evidence
        {
            public string scope = "local-map0-integration-not-native-equivalence";
            public bool passed;
            public string failure;
            public int activeMobs, logicalMobs, targetId, initialHealth, finalHealth, hits;
            public float walkedDistance, forwardDot;
            public Vector3 worldStart, walkEnd, combatStart;
            public bool relocatedForCombat = true;
            public string note = "Combat starts near a real spawn for repeatable qualification. Damage/reach are local test defaults, not decoded server combat rules.";
        }
        private Evidence evidence = new Evidence();
        private string output;
        private ShaiyaClientActor actor;

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!Debug.isDebugBuild || Array.IndexOf(args, "--world-qualification") < 0) return;
            output = Path.Combine(Application.persistentDataPath, "WorldQualification");
            for (int i=0;i+1<args.Length;i++) if (args[i]=="--world-output") output=Path.GetFullPath(args[i+1]);
            Directory.CreateDirectory(output);
            StartCoroutine(Execute());
        }

        private IEnumerator Execute()
        {
            yield return new WaitForSecondsRealtime(3);
            actor = FindFirstObjectByType<ShaiyaClientActor>();
            var streamer = FindFirstObjectByType<LegacyMonsterSpawnStreamer>();
            var interaction = FindFirstObjectByType<ShaiyaCombatInteraction>();
            var camera = FindFirstObjectByType<ShaiyaThirdPersonCamera>();
            Terrain terrain = Terrain.activeTerrain;
            if (actor == null || streamer == null || interaction == null || camera == null || terrain == null)
            { Finish("Required real-world components are absent."); yield break; }
            if (!actor.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.sharedMesh != null && r.sharedMaterial != null))
            { Finish("No skinned original character present; synthetic actor is not accepted."); yield break; }
            evidence.logicalMobs = streamer.LogicalSpawnCount;
            evidence.worldStart = actor.transform.position;
            foreach (var hud in FindObjectsByType<ParityDebugHud>(FindObjectsSortMode.None)) hud.enabled=false;
            yield return Capture("01-world-start");

            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
            actor.SetExternalMovement(Vector2.up, false);
            yield return new WaitForSeconds(1.8f);
            actor.SetExternalMovement(Vector2.zero, false);
            yield return new WaitForSeconds(0.3f);
            evidence.walkEnd=actor.transform.position;
            Vector3 delta=Vector3.ProjectOnPlane(evidence.walkEnd-evidence.worldStart,Vector3.up);
            evidence.walkedDistance=delta.magnitude;
            evidence.forwardDot=delta.sqrMagnitude>0.001f?Vector3.Dot(delta.normalized,forward):0;
            yield return Capture("02-after-walking");
            if (evidence.walkedDistance<0.5f || evidence.forwardDot<0.75f)
            { Finish("Actual actor failed forward walking gate (may be blocked by environment; inspect capture)."); yield break; }

            var candidate = streamer.Spawns.Where(s=>s.prefab!=null && s.maxHealth>0)
                .OrderBy(s=>(s.position-actor.transform.position).sqrMagnitude).FirstOrDefault();
            if (candidate==null) { Finish("No real monster spawn definitions."); yield break; }
            // Scenario relocation is reported explicitly; it is not claimed as a matched native-camera scene.
            Vector3 start=candidate.position+Vector3.back*2.5f;
            start.y=terrain.SampleHeight(start)+terrain.transform.position.y+0.1f;
            var controller=actor.GetComponent<CharacterController>(); controller.enabled=false;
            actor.transform.position=start; controller.enabled=true;
            evidence.combatStart=start;
            camera.ConfigureView(0,18,6.5f);
            Physics.SyncTransforms(); streamer.EvaluateNow();
            yield return new WaitForSeconds(1);
            var target=candidate.activeInstance != null?candidate.activeInstance.GetComponent<ShaiyaCombatTarget>():null;
            if (target==null || !interaction.Select(target)) { Finish("Spawn could not be selected for combat."); yield break; }
            evidence.targetId=target.TargetId; evidence.initialHealth=target.Health; evidence.activeMobs=streamer.ActiveCount;
            yield return Capture("03-before-combat");
            float until=Time.realtimeSinceStartup+35;
            int prior=target.Health;
            while (target.IsAlive && Time.realtimeSinceStartup<until)
            {
                // Exercise the same public attack path used by keys 1..4. Never force HP=0.
                interaction.TryAttackSelected(95);
                yield return new WaitForSeconds(0.55f);
                if (target.Health<prior) { evidence.hits++;prior=target.Health; }
            }
            evidence.finalHealth=target.Health;
            yield return new WaitForSeconds(0.4f);
            yield return Capture("04-after-combat");
            if (target.IsAlive || evidence.hits==0) { Finish("Real-target damage/death gate failed; inspect reach, LOS and scene.");yield break; }
            Finish(null);
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D image=ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG()); }
            finally { Destroy(image); }
        }
        private void Finish(string failure)
        {
            if (actor!=null) { actor.SetExternalMovement(Vector2.zero,false);actor.ReleaseExternalMovement(); }
            evidence.failure=failure; evidence.passed=failure==null;
            File.WriteAllText(Path.Combine(output,"world-qualification.json"),JsonUtility.ToJson(evidence,true));
            if (evidence.passed) Debug.Log("DREYNOX_WORLD_QUALIFICATION_OK");else Debug.LogError("DREYNOX_WORLD_QUALIFICATION_FAILED: "+failure);
            Application.Quit(evidence.passed?0:2);
        }
    }
}
