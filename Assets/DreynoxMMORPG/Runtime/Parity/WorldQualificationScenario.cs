using System;
using System.Collections;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Gameplay.AnimationSystem;
using Dreynox.Mmorpg.Gameplay.CameraSystem;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.UI;
using Dreynox.Mmorpg.World;
using UnityEngine;

namespace Dreynox.Mmorpg.Parity
{
    /// <summary>Exercises the packaged real Map0. Opt-in Development Player only.</summary>
    public sealed class WorldQualificationScenario : MonoBehaviour
    {
        [Serializable] private sealed class Evidence
        {
            public string scope = "local-map0-integration-not-native-equivalence";
            public bool passed;
            public string failure;
            public int activeMobs, logicalMobs, targetId, initialHealth, finalHealth, hits, attackAnimations;
            public string deathSemantic;
            public float walkedDistance, forwardDot, cameraFov;
            public Vector3 worldStart, walkEnd, combatStart, startCameraPosition, startCameraEuler;
            public bool relocatedForCombat = true;
            public string note = "Combat is staged near an authored spawn. Damage, reach and impact timing are local qualification values, not decoded server rules or a matched native capture.";
        }
        private Evidence evidence = new Evidence();
        private string output;
        private ShaiyaClientActor actor;
        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!Debug.isDebugBuild || Array.IndexOf(args,"--world-qualification")<0) return;
            output=Path.Combine(Application.persistentDataPath,"WorldQualification");
            for(int i=0;i+1<args.Length;i++) if(args[i]=="--world-output") output=Path.GetFullPath(args[i+1]);
            Directory.CreateDirectory(output);
            StartCoroutine(Execute());
        }
        private IEnumerator Execute()
        {
            yield return new WaitForSecondsRealtime(3);
            actor=FindFirstObjectByType<ShaiyaClientActor>();
            var streamer=FindFirstObjectByType<LegacyMonsterSpawnStreamer>();
            var interaction=FindFirstObjectByType<ShaiyaCombatInteraction>();
            var camera=FindFirstObjectByType<ShaiyaThirdPersonCamera>();
            Terrain terrain=Terrain.activeTerrain;
            if(actor==null || streamer==null || interaction==null || camera==null || terrain==null)
            { Finish("Required real-world components absent.");yield break; }
            if(!actor.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.sharedMesh!=null && r.sharedMaterial!=null))
            { Finish("Missing original skinned character; synthetic actor forbidden.");yield break; }
            var animator=actor.GetComponent<SemanticAnimationPlayer>();
            if(animator==null || animator.Catalog==null || !animator.Catalog.TryGet("attack_1",out AnimationClip attack))
            { Finish("Authored attack animation missing.");yield break; }
            evidence.logicalMobs=streamer.LogicalSpawnCount;
            evidence.worldStart=actor.transform.position;
            evidence.startCameraPosition=camera.transform.position;
            evidence.startCameraEuler=camera.transform.eulerAngles;
            evidence.cameraFov=Camera.main!=null?Camera.main.fieldOfView:0;
            foreach(var hud in FindObjectsByType<ParityDebugHud>(FindObjectsSortMode.None)) hud.enabled=false;
            yield return Capture("01-world-start");
            Vector3 forward=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
            actor.SetExternalMovement(Vector2.up,false);
            yield return new WaitForSeconds(1.8f);
            actor.SetExternalMovement(Vector2.zero,false);
            yield return new WaitForSeconds(0.3f);
            evidence.walkEnd=actor.transform.position;
            Vector3 delta=Vector3.ProjectOnPlane(evidence.walkEnd-evidence.worldStart,Vector3.up);
            evidence.walkedDistance=delta.magnitude;
            evidence.forwardDot=delta.sqrMagnitude>0.001f?Vector3.Dot(delta.normalized,forward):0;
            yield return Capture("02-after-walking");
            if(evidence.walkedDistance<0.5f || evidence.forwardDot<0.75f)
            { Finish("Actual forward walking failed; inspect environmental obstruction and capture.");yield break; }
            var candidate=streamer.Spawns.Where(s=>s.prefab!=null && s.maxHealth>0)
                .OrderBy(s=>(s.position-actor.transform.position).sqrMagnitude).FirstOrDefault();
            if(candidate==null){Finish("No real monster definitions.");yield break;}
            Vector3 start=candidate.position+Vector3.back*2.5f;
            start.y=terrain.SampleHeight(start)+terrain.transform.position.y+0.1f;
            var controller=actor.GetComponent<CharacterController>();controller.enabled=false;
            actor.transform.position=start;controller.enabled=true;
            evidence.combatStart=start;
            camera.ConfigureView(0,18,6.5f);
            Physics.SyncTransforms();streamer.EvaluateNow();
            yield return new WaitForSeconds(1);
            var target=candidate.activeInstance!=null?candidate.activeInstance.GetComponent<ShaiyaCombatTarget>():null;
            if(target==null || !interaction.Select(target)){Finish("Real spawn cannot be selected.");yield break;}
            evidence.targetId=target.TargetId; evidence.initialHealth=target.Health; evidence.activeMobs=streamer.ActiveCount;
            Vector3 facing=Vector3.ProjectOnPlane(target.transform.position-actor.transform.position,Vector3.up);
            if(facing.sqrMagnitude>0.0001f) actor.transform.rotation=Quaternion.LookRotation(facing.normalized);
            yield return Capture("03-before-combat");
            float until=Time.realtimeSinceStartup+35;
            int prior=target.Health;
            bool capturedAttack=false;
            while(target.IsAlive && Time.realtimeSinceStartup<until)
            {
                // Same public action path as keyboard attacks. Never set HP to zero directly.
                bool accepted=interaction.TryAttackSelected(95);
                if(accepted && !capturedAttack)
                {
                    yield return new WaitForSeconds(0.1f);
                    yield return Capture("03b-actual-attack-animation");
                    capturedAttack=true;
                    yield return new WaitForSeconds(0.45f);
                }
                else yield return new WaitForSeconds(0.55f);
                if(target.Health<prior){evidence.hits++;prior=target.Health;}
            }
            evidence.finalHealth=target.Health;
            evidence.attackAnimations=actor.AttackAnimationCount;
            var mobAnimation=target.GetComponent<SemanticAnimationPlayer>();
            evidence.deathSemantic=mobAnimation!=null?mobAnimation.ResolvedSemantic:"";
            yield return new WaitForSeconds(0.4f);
            yield return Capture("04-after-combat");
            if(target.IsAlive || evidence.hits==0){Finish("Actual damage/death gate failed.");yield break;}
            if(evidence.attackAnimations==0 || evidence.deathSemantic!="dead")
            { Finish("Health changed without exercising authored attack/death animation.");yield break; }
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
            if(actor!=null){actor.SetExternalMovement(Vector2.zero,false);actor.ReleaseExternalMovement();}
            evidence.failure=failure;evidence.passed=failure==null;
            File.WriteAllText(Path.Combine(output,"world-qualification.json"),JsonUtility.ToJson(evidence,true));
            if(evidence.passed) Debug.Log("DREYNOX_WORLD_QUALIFICATION_OK");
            else Debug.LogError("DREYNOX_WORLD_QUALIFICATION_FAILED: "+failure);
            Application.Quit(evidence.passed?0:2);
        }
    }
}
