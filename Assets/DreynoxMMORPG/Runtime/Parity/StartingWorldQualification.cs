using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Gameplay.AnimationSystem;
using Dreynox.Mmorpg.Gameplay.CameraSystem;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.Gameplay.Equipment;
using Dreynox.Mmorpg.UI;
using Dreynox.Mmorpg.Quests;
using Dreynox.Mmorpg.World;
using UnityEngine;

namespace Dreynox.Mmorpg.Parity
{
    /// <summary>Opt-in Development Player integration, not a native gameplay comparison.</summary>
    public sealed class StartingWorldQualification : MonoBehaviour
    {
        [Serializable] private sealed class Evidence
        {
            public string scope="map1-local-integration-not-native-equivalence", failure="";
            public bool passed, originalEthanOpened, questAccepted, questDelivered, inputIsScripted=true, combatRelocated=true;
            public int mapId=1, questId=3400, npcPositions, monsterInstances, kills, attackAnimations;
            public bool starterWeaponEquipped, terrainCollisionVerified, defaultAppearanceVerified, nativeRadarVerified;
            public string[] bodyMeshSources, bodyTextureSources;
            public int terrainProbes;
            public string starterWeaponResource="";
            public int starterWeaponVertices;
            public long rewardGold, rewardExperience;
            public float walkedDistance, directionDot, cameraYawBefore, cameraYawAfter;
            public Vector3 entry;
            public SkinnedPoseProbe.Difference walkSkin;
            public List<SkinnedPoseProbe.Difference> attackSkins=new List<SkinnedPoseProbe.Difference>();
            public List<SkinnedPoseProbe.Difference> deathSkins=new List<SkinnedPoseProbe.Difference>();
            public List<string> steps=new List<string>();
            public string limitations="Uses authored NPC/quest/mob IDs; damage=95 is local diagnostic, not native combat rules. Relocation and scripted input are explicit. No assertion of graphical/native equivalence.";
        }
        private Evidence evidence=new Evidence();
        private string output;
        private ShaiyaClientActor actor;
        private ShaiyaCombatInteraction combat;
        private NativeWorldHud npcInteraction;
        private QuestJournalRuntime journal;
        private LegacyMonsterSpawnStreamer monsters;
        private LegacyNpcSpawnStreamer npcs;
        private bool finished;
        private void Start()
        {
            string[] args=Environment.GetCommandLineArgs();
            if(!Debug.isDebugBuild||Array.IndexOf(args,"--starting-world-qualification")<0)return;
            output=Path.Combine(Application.persistentDataPath,"StartingWorldQualification");
            for(int i=0;i+1<args.Length;i++)if(args[i]=="--qualification-output")output=Path.GetFullPath(args[i+1]);
            Directory.CreateDirectory(output);StartCoroutine(Guarded(Execute()));
        }
        private IEnumerator Guarded(IEnumerator task)
        {
            var stack=new Stack<IEnumerator>();stack.Push(task);
            while(stack.Count>0)
            {
                object next=null;bool moved=false;Exception fault=null;
                try {moved=stack.Peek().MoveNext();if(moved)next=stack.Peek().Current;}
                catch(Exception ex){fault=ex;}
                if(fault!=null){Finish(fault.ToString());yield break;}
                if(!moved){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(next is IEnumerator nested){stack.Push(nested);continue;}
                yield return next;
            }
        }
        private IEnumerator Execute()
        {
            yield return new WaitForSecondsRealtime(3);
            actor=FindFirstObjectByType<ShaiyaClientActor>();combat=FindFirstObjectByType<ShaiyaCombatInteraction>();
            journal=FindFirstObjectByType<QuestJournalRuntime>();npcInteraction=FindFirstObjectByType<NativeWorldHud>();
            monsters=FindFirstObjectByType<LegacyMonsterSpawnStreamer>();npcs=FindFirstObjectByType<LegacyNpcSpawnStreamer>();
            var camera=FindFirstObjectByType<ShaiyaThirdPersonCamera>();
            if(actor==null||combat==null||journal==null||!journal.Ready||npcInteraction==null||monsters==null||npcs==null||camera==null)
            {Finish("Required real world/UI/quest components missing or journal initialization failed.");yield break;}
            evidence.entry=actor.transform.position;evidence.npcPositions=npcs.LogicalSpawnCount;evidence.monsterInstances=monsters.LogicalSpawnCount;
            var terrain=Terrain.activeTerrain;
            WorldTerrainCollision.Require(terrain);
            Physics.SyncTransforms();
            foreach(var fox in monsters.Spawns.Where(s=>s.mobId==2011&&s.prefab!=null))
            {
                if(!WorldTerrainCollision.TryProbe(terrain,fox.position,out RaycastHit support))
                {Finish("Original fox field has no terrain collider support at "+fox.targetId+" / "+fox.position);yield break;}
                float expected=terrain.SampleHeight(fox.position)+terrain.transform.position.y;
                if(Mathf.Abs(support.point.y-expected)>0.25f)
                {Finish("Terrain collision/render height mismatch at "+fox.targetId);yield break;}
                evidence.terrainProbes++;
            }
            if(evidence.terrainProbes==0){Finish("No authored fox areas to verify terrain support.");yield break;}
            evidence.terrainCollisionVerified=true;
            var appearance=actor.GetComponent<LegacyAppearanceEvidence>();
            if(appearance==null||appearance.Policy!="ML2-default-body-row0"||
                !appearance.Meshes[0].EndsWith("humf_torso001.3DC",StringComparison.OrdinalIgnoreCase))
            {Finish("Native starter body is absent or an unrelated costume replaced it.");yield break;}
            evidence.bodyMeshSources=appearance.Meshes;evidence.bodyTextureSources=appearance.Textures;
            evidence.defaultAppearanceVerified=true;
            if(npcInteraction.Radar==null||npcInteraction.Radar.PlayerMarker.sprite==null)
            {Finish("Original radar artwork is not bound in the running Player.");yield break;}
            evidence.nativeRadarVerified=true;
            if(Mathf.Abs(evidence.entry.x-580)>1||Mathf.Abs(evidence.entry.z-1760)>1||Mathf.Abs(evidence.entry.y-78)>3||evidence.npcPositions!=307||evidence.monsterInstances!=1186)
            {Finish("The native starting map or authored entry is not the expected Map1.");yield break;}
            if(!actor.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.sharedMesh!=null))
            {Finish("Original character mesh missing.");yield break;}
            var starter=actor.GetComponent<LocalStarterEquipment>();
            var attachments=actor.GetComponent<EquipmentAttachmentController>();
            evidence.starterWeaponEquipped=starter!=null&&starter.Equipped&&attachments!=null&&attachments.Has(EquipmentSlot.MainHand);
            if(!evidence.starterWeaponEquipped||!attachments.TryGetInstance(EquipmentSlot.MainHand,out var weapon)||
                !attachments.TryGetDefinition(EquipmentSlot.MainHand,out var weaponDefinition))
            {Finish("Original starter weapon was not equipped through the actor attachment system.");yield break;}
            var weaponMesh=weapon.GetComponentInChildren<MeshFilter>();
            evidence.starterWeaponVertices=weaponMesh!=null&&weaponMesh.sharedMesh!=null?weaponMesh.sharedMesh.vertexCount:0;
            evidence.starterWeaponResource=weaponDefinition.legacyResourceId;
            if(evidence.starterWeaponVertices!=169||weapon.transform.parent.name!="Bone_021")
            {Finish("Starter sword visual or original hand binding is missing.");yield break;}
            evidence.steps.Add("Original item1/1 resolved through DBItemData and IT2;169-vertex sword attached to authored HUMF hand.");
            yield return Capture("01-map1-entry-hud");
            // Exercise actual UI callbacks, not a standalone skin mockup.
            var nativeBar=npcInteraction.CanvasRoot.Find("Native quickbar 0");
            var nextPage=nativeBar.Find("Next page").GetComponent<UnityEngine.UI.Button>();
            for(int page=0;page<4;page++)nextPage.onClick.Invoke();
            if(npcInteraction.Quickbar.Core.Page(0)!=4)throw new InvalidOperationException("Native page arrows are not connected.");
            yield return Capture("01-native-bar-page-five");
            var previousPage=nativeBar.Find("Previous page").GetComponent<UnityEngine.UI.Button>();
            for(int page=0;page<4;page++)previousPage.onClick.Invoke();
            nativeBar.Find("Rotate bar").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            if(!npcInteraction.Quickbar.Core.Vertical(0))throw new InvalidOperationException("Native rotation is not connected.");
            yield return Capture("01-native-bar-vertical");
            nativeBar.Find("Rotate bar").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            nativeBar.Find("Additional bar").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return Capture("01-native-additional-bar");
            nativeBar.Find("Additional bar").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

            foreach(float angle in new[]{0f,90f,180f,270f})
            {
                camera.ConfigureView(angle,12,3.5f);
                yield return new WaitForSeconds(.2f);
                yield return Capture("01-body-view-"+angle.ToString("F0",System.Globalization.CultureInfo.InvariantCulture));
            }
            camera.ConfigureView(180,18,6.5f);yield return null;
            evidence.cameraYawBefore=camera.Yaw;camera.AddLookInput(new Vector2(10,0));
            yield return null;yield return null;
            evidence.cameraYawAfter=camera.Yaw;
            if(Mathf.Abs(Mathf.DeltaAngle(evidence.cameraYawBefore,evidence.cameraYawAfter))<5)
            {Finish("Camera orbit input did not change the actual camera.");yield break;}
            camera.ConfigureView(0,18,6.5f);yield return null;
            Vector3 direction=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
            var idleSkin=SkinnedPoseProbe.Capture(actor.gameObject);
            Vector3 start=actor.transform.position;actor.SetExternalMovement(Vector2.up,false);
            yield return new WaitForSeconds(.63f);
            evidence.walkSkin=SkinnedPoseProbe.Compare(idleSkin,SkinnedPoseProbe.Capture(actor.gameObject));
            var actorAnimation=actor.GetComponent<SemanticAnimationPlayer>();
            if(!evidence.walkSkin.Deformed||actorAnimation==null||actorAnimation.ResolvedSemantic!="walk")
            {Finish("Movement did not deform the original body through its walk animation.");yield break;}
            yield return new WaitForSeconds(.57f);actor.SetExternalMovement(Vector2.zero,false);yield return new WaitForSeconds(.3f);
            Vector3 delta=Vector3.ProjectOnPlane(actor.transform.position-start,Vector3.up);
            evidence.walkedDistance=delta.magnitude;evidence.directionDot=delta.magnitude>0?Vector3.Dot(direction,delta.normalized):0;
            yield return Capture("02-after-walking");
            if(evidence.walkedDistance<.5f||evidence.directionDot<.7f)
            {Finish("Real forward walking failed, not replaced with transform movement.");yield break;}
            var ethan=npcs.Spawns.FirstOrDefault(n=>n.npcType==7&&n.typeId==1081);
            if(ethan==null||!PlaceNear(ethan.position)){Finish("No valid placement near original Ethan.");yield break;}
            yield return new WaitForSeconds(1);
            var npc=ethan.activeInstance!=null?ethan.activeInstance.GetComponent<LegacyNpcRuntimeDescriptor>():null;
            evidence.originalEthanOpened=npc!=null&&npcInteraction.TryTalk(npc);
            if(!evidence.originalEthanOpened){Finish("Original Ethan cannot be opened through the world interaction adapter.");yield break;}
            var panel=FindFirstObjectByType<QuestWorldPanel>();
            if(panel==null||!panel.SelectVisibleQuest(3400))
            {Finish("Original quest cannot be selected in the displayed NPC panel.");yield break;}
            yield return Capture("03-original-npc-dialogue");
            evidence.questAccepted=panel.SubmitSelectedQuest();
            string reason=panel.ActionFailure;
            if(!evidence.questAccepted){Finish("Authored quest rejected: "+reason);yield break;}
            npcInteraction.CloseDialogue();evidence.steps.Add("NPC7/1081 opened; quest3400 accepted through real catalog.");
            var targets=monsters.Spawns.Where(s=>s.mobId==2011&&s.prefab!=null&&s.maxHealth>0)
                .OrderBy(s=>(s.position-actor.transform.position).sqrMagnitude).Take(5).ToArray();
            if(targets.Length!=5){Finish("Fewer than five authored fox spawns exist.");yield break;}
            foreach(var spawn in targets)
            {
                if(!PlaceNear(spawn.position)){Finish("No walkable support near fox spawn "+spawn.targetId+" at "+spawn.position+": "+lastPlacementFailure);yield break;}
                monsters.EvaluateNow();yield return new WaitForSeconds(.8f);
                var target=spawn.activeInstance!=null?spawn.activeInstance.GetComponent<ShaiyaCombatTarget>():null;
                if(target==null||!combat.Select(target)){Finish("Original fox not active/selectable.");yield break;}
                if(!combat.CanImpact(target.TargetId)){Finish("Fox obstructed/out of reach after collision-aware placement.");yield break;}
                var livingSkin=SkinnedPoseProbe.Capture(target.gameObject);
                var beforeAttack=SkinnedPoseProbe.Capture(actor.gameObject);
                bool measuredAttack=false;
                int initial=target.Health;float until=Time.realtimeSinceStartup+35;
                while(target.IsAlive&&Time.realtimeSinceStartup<until)
                {
                    Vector3 facing=Vector3.ProjectOnPlane(target.transform.position-actor.transform.position,Vector3.up);
                    if(facing.sqrMagnitude>.001f)actor.transform.rotation=Quaternion.LookRotation(facing);
                    bool accepted=combat.TryAttackSelected(95);
                    if(accepted&&!measuredAttack)
                    {
                        yield return new WaitForSeconds(.12f);
                        var difference=SkinnedPoseProbe.Compare(beforeAttack,SkinnedPoseProbe.Capture(actor.gameObject));
                        if(!difference.Deformed||actorAnimation.ResolvedSemantic!="attack_1")
                        {Finish("An accepted attack did not deform the original character through attack_1.");yield break;}
                        evidence.attackSkins.Add(difference);measuredAttack=true;
                        yield return Capture("04-attack-"+(evidence.kills+1));
                        yield return new WaitForSeconds(.13f);
                    }
                    else yield return new WaitForSeconds(.25f);
                }
                if(target.IsAlive||target.Health>=initial){Finish("No real fox death through accepted attacks.");yield break;}
                var animation=target.GetComponent<SemanticAnimationPlayer>();
                if(animation==null||animation.ResolvedSemantic!="dead"){Finish("HP changed but authored MON death animation missing.");yield break;}
                yield return new WaitForSeconds(.25f);
                var deathSkin=SkinnedPoseProbe.Compare(livingSkin,SkinnedPoseProbe.Capture(target.gameObject));
                if(!measuredAttack||!deathSkin.Deformed)
                {Finish("Damage changed health without a measured authored attack/death pose.");yield break;}
                evidence.deathSkins.Add(deathSkin);
                evidence.kills++;evidence.steps.Add("Fox "+spawn.targetId+": "+initial+" HP -> "+target.Health+" through combat adapter.");
                yield return Capture("04-fox-"+evidence.kills+"-defeated");
            }
            evidence.attackAnimations=actor.AttackAnimationCount;
            if(evidence.attackAnimations==0||!journal.Journal.Entries.TryGetValue(3400,out var progress)||progress.stage!=JournalStage.Ready)
            {Finish("Authored attack playback / five-kill quest objective not satisfied.");yield break;}
            if(!PlaceNear(ethan.position)){Finish("Return NPC placement failed.");yield break;}
            yield return new WaitForSeconds(1);
            npc=ethan.activeInstance!=null?ethan.activeInstance.GetComponent<LegacyNpcRuntimeDescriptor>():null;
            if(npc==null||!npcInteraction.TryTalk(npc)){Finish("Cannot return to original quest NPC.");yield break;}
            long beforeGold=journal.Journal.Gold,beforeXp=journal.Journal.Experience;
            if(!panel.SelectVisibleQuest(3400)){Finish("Ready quest is not selectable in the original NPC dialog.");yield break;}
            evidence.questDelivered=panel.SubmitSelectedQuest();reason=panel.ActionFailure;
            evidence.rewardGold=journal.Journal.Gold-beforeGold;evidence.rewardExperience=journal.Journal.Experience-beforeXp;
            yield return Capture("05-quest-reward");
            if(!evidence.questDelivered||evidence.rewardGold!=3000||evidence.rewardExperience!=5)
            {Finish("Original quest reward transaction did not match source: "+reason);yield break;}
            if(journal.Journal.Deliver(3400,npc.ServiceKey,0,out _))
            {Finish("Quest reward could be duplicated.");yield break;}
            Finish(null);
        }
        private string lastPlacementFailure="";
        private bool PlaceNear(Vector3 point)
        {
            actor.SetExternalMovement(Vector2.zero,false);
            Vector3[] offsets={Vector3.back,Vector3.right,Vector3.forward,Vector3.left};
            foreach(var offset in offsets)
                if(WorldGroundPlacement.TryPlace(actor,point+offset*2.2f,out lastPlacementFailure,horizontalRadius:.5f,verticalTolerance:8))return true;
            return false;
        }
        [Serializable] private sealed class ViewEvidence
        {
            public string scope="actual-unity-player-view-not-matched-native-camera";
            public int width,height;
            public Vector3 actorPosition,cameraPosition,cameraEuler;
            public float fov,resolvedDistance;
            public bool pivotObstructed,avatarOccluded;
        }
        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());
                var ui=NativeHudContractEvidence.Capture(npcInteraction);
                File.WriteAllText(Path.Combine(output,name+".ui.json"),JsonUtility.ToJson(ui,true));
                var camera=Camera.main;var orbit=camera!=null?camera.GetComponent<ShaiyaThirdPersonCamera>():null;
                var view=new ViewEvidence{width=texture.width,height=texture.height,actorPosition=actor.transform.position,
                    cameraPosition=camera!=null?camera.transform.position:Vector3.zero,
                    cameraEuler=camera!=null?camera.transform.eulerAngles:Vector3.zero,fov=camera!=null?camera.fieldOfView:0,
                    resolvedDistance=orbit!=null?orbit.ResolvedDistance:0,pivotObstructed=orbit!=null&&orbit.PivotObstructed,
                    avatarOccluded=orbit!=null&&orbit.AvatarOccluded};
                File.WriteAllBytes(Path.Combine(output,name+".view.json"),System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(view,true)));
            }
            finally{Destroy(texture);}
        }
        private void Finish(string failure)
        {
            if(finished)return;finished=true;
            if(actor!=null){actor.SetExternalMovement(Vector2.zero,false);actor.ReleaseExternalMovement();}
            if(npcInteraction!=null)npcInteraction.CloseDialogue();
            evidence.failure=failure??"";evidence.passed=failure==null;
            File.WriteAllText(Path.Combine(output,"starting-world-qualification.json"),JsonUtility.ToJson(evidence,true));
            if(evidence.passed)Debug.Log("DREYNOX_STARTING_WORLD_QUALIFICATION_OK");else Debug.LogError("DREYNOX_STARTING_WORLD_QUALIFICATION_FAILED: "+failure);
            Application.Quit(evidence.passed?0:2);
        }
    }
}
