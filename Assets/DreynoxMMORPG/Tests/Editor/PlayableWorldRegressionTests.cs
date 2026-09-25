using System.Collections.Generic;
using Dreynox.Mmorpg.Gameplay.CameraSystem;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.ParityCore;
using Dreynox.Mmorpg.World;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class PlayableWorldRegressionTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private static readonly Vector3 Far = new Vector3(25000f, 0f, 25000f);
        private GameObject Make(string name, Vector3 offset)
        {
            var go = new GameObject(name); go.transform.position = Far + offset; objects.Add(go); return go;
        }
        [TearDown] public void Clear() { foreach (var go in objects) if (go != null) Object.DestroyImmediate(go); objects.Clear(); }

        [TestCase(0f)] [TestCase(90f)] [TestCase(180f)] [TestCase(270f)]
        public void ForwardInputTracksUnityCameraForward(float yaw)
        {
            ClientCoordinateCore.ResolveUnityCameraRelative(0, 1, yaw, out double x, out double z);
            Vector3 actual = new Vector3((float)x, 0, (float)z);
            Assert.Greater(Vector3.Dot(actual, Quaternion.Euler(0, yaw, 0) * Vector3.forward), 0.99999f);
        }
        [Test] public void CameraExcludesSelfAndRetractsBeforeWall()
        {
            var player = Make("actor", Vector3.zero);
            var self = player.AddComponent<SphereCollider>(); self.center = Vector3.up * 1.55f; self.radius = 0.8f;
            var camera = Make("camera", Vector3.zero).AddComponent<ShaiyaThirdPersonCamera>();
            camera.SetTarget(player.transform); camera.ConfigureView(0, 0, 10);
            Physics.SyncTransforms(); camera.Step(1f / 60, Vector2.zero, 0);
            Assert.AreEqual(10, camera.ResolvedDistance, 0.01f, "Own body must not collapse camera.");
            var wall = Make("wall", new Vector3(0,1.55f,-3)); var box = wall.AddComponent<BoxCollider>(); box.size = new Vector3(10,10,0.5f);
            Physics.SyncTransforms(); camera.Step(1f / 60, Vector2.zero, 0);
            Assert.Less(camera.ResolvedDistance, 2.6f, "Collision must retract in this frame, not lerp through wall.");
            Assert.Greater(camera.ResolvedDistance, 1f);
        }
        [Test] public void CombatRechecksLineOfSightAndPooledIdentityAtImpact()
        {
            var player = Make("actor", Vector3.zero).AddComponent<ShaiyaClientActor>();
            var target = Make("mob", new Vector3(0,0,2)).AddComponent<ShaiyaCombatTarget>();
            target.gameObject.AddComponent<BoxCollider>().center = Vector3.up;
            target.Configure(1,100);
            var interaction = Make("interaction", Vector3.zero).AddComponent<ShaiyaCombatInteraction>(); interaction.Bind(player, null);
            Physics.SyncTransforms(); Assert.IsTrue(interaction.Select(target)); Assert.IsTrue(interaction.TryAttackSelected(30));
            var wall = Make("wall", new Vector3(0,1,1)); wall.AddComponent<BoxCollider>().size = new Vector3(5,3,0.3f);
            Physics.SyncTransforms(); player.Combat.Tick(0.2); interaction.FlushHitToScene();
            Assert.AreEqual(100, target.Health, "No damage through a newly obstructing wall.");
            Object.DestroyImmediate(wall); Physics.SyncTransforms(); player.Combat.Tick(1);
            Assert.IsTrue(interaction.TryAttackSelected(30)); target.Configure(2,100);
            player.Combat.Tick(0.2); interaction.FlushHitToScene(); Assert.AreEqual(100,target.Health,"No hit delivered to reused instance.");
        }
        [Test] public void CombatRejectsFarTargets()
        {
            var actor = Make("actor",Vector3.zero).AddComponent<ShaiyaClientActor>();
            var target = Make("far mob",Vector3.forward*50).AddComponent<ShaiyaCombatTarget>(); target.Configure(10,100);
            var interaction = Make("interaction",Vector3.zero).AddComponent<ShaiyaCombatInteraction>(); interaction.Bind(actor,null);
            Assert.IsTrue(interaction.Select(target)); Assert.IsFalse(interaction.TryAttackSelected(30));
        }
        [Test] public void LeavingAndReenteringStreamingRangeDoesNotHealOrResurrectMobs()
        {
            var observer = Make("observer",Vector3.zero);
            var prefab = Make("unit fixture",Vector3.zero); prefab.AddComponent<ShaiyaCombatTarget>(); prefab.SetActive(false);
            var streamer = Make("streamer",Vector3.zero).AddComponent<LegacyMonsterSpawnStreamer>();
            var definition = new LegacyMonsterSpawnDefinition { prefab=prefab, targetId=7, maxHealth=100, position=Far };
            streamer.Configure(observer.transform,new[]{definition}); streamer.EvaluateNow();
            definition.activeInstance.GetComponent<ShaiyaCombatTarget>().ApplyDamage(40);
            observer.transform.position = Far+Vector3.right*500; streamer.EvaluateNow();
            observer.transform.position = Far; streamer.EvaluateNow();
            Assert.AreEqual(60,definition.activeInstance.GetComponent<ShaiyaCombatTarget>().Health);
            definition.activeInstance.GetComponent<ShaiyaCombatTarget>().ApplyDamage(60);
            observer.transform.position = Far+Vector3.right*500; streamer.EvaluateNow();
            observer.transform.position = Far; streamer.EvaluateNow(); Assert.IsNull(definition.activeInstance);
        }
    }
}
