using System;
using System.IO;
using System.Linq;
using Dreynox.Mmorpg.Editor.Corpus;
using Dreynox.Mmorpg.Editor.LegacyFormats;
using Dreynox.Mmorpg.Gameplay.Client;
using Dreynox.Mmorpg.Gameplay.Combat;
using Dreynox.Mmorpg.Interaction;
using Dreynox.Mmorpg.World;
using NUnit.Framework;
using UnityEngine;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class LocalPortalTravelTests
    {
        private GameObject fixtureRoot;
        private ShaiyaClientActor actor;
        private LocalPortalTravel travel;
        private ShaiyaCombatInteraction combat;
        private LegacyPortalRuntime portal;
        private static readonly Vector3 Origin = new Vector3(45000, 20000, 45000);
        [SetUp]
        public void SetUp()
        {
            // Other EditMode tests may leave an untitled scene open. Do not
            // replace, save or discard that scene just to construct a fixture.
            // Own only these transient objects, far from any existing geometry.
            fixtureRoot = new GameObject("PortalFixture_" + Guid.NewGuid().ToString("N"));
            var player = Make("portal test actor", Origin);
            var body = player.AddComponent<CharacterController>();
            body.height = 1.8f; body.center = Vector3.up * 0.9f; body.radius = 0.35f;
            actor = player.AddComponent<ShaiyaClientActor>();
            var world = Make("portal test session", Origin);
            var session = world.AddComponent<NativeWorldSession>(); session.Configure(1, actor, Origin);
            combat = world.AddComponent<ShaiyaCombatInteraction>(); combat.Bind(actor, null);
            travel = world.AddComponent<LocalPortalTravel>(); travel.Configure(session, actor, combat, null);
            var point = Make("authored portal", Origin + Vector3.forward);
            portal = point.AddComponent<LegacyPortalRuntime>();
            portal.Configure(1, 1, 0, 999, 1, Origin + Vector3.right * 10);
        }
        [TearDown]
        public void TearDown()
        {
            if (travel != null) WorldInputGate.Set(travel, false);
            if (fixtureRoot != null) UnityEngine.Object.DestroyImmediate(fixtureRoot);
            fixtureRoot = null;
            Physics.SyncTransforms();
        }
        private GameObject Make(string name, Vector3 position)
        {
            var value = new GameObject(name);
            value.transform.SetParent(fixtureRoot.transform, false);
            value.transform.position = position;
            return value;
        }
        private void AddFloor()
        {
            var floor = Make("actual collision support", Origin + Vector3.down * 0.5f);
            floor.AddComponent<BoxCollider>().size = new Vector3(40, 1, 40);
            Physics.SyncTransforms();
        }
        [Test]
        public void SameMapTravelUsesOriginalTargetAndActualGround()
        {
            AddFloor();
            Assert.IsTrue(travel.TryTravel(portal, 1, 1), travel.LastMessage);
            Assert.AreEqual(1, travel.TravelSerial);
            Assert.AreEqual(Origin + Vector3.right * 10, travel.LastAuthoredDestination);
            Assert.AreEqual(Origin.x + 10, actor.transform.position.x, 0.01f);
            Assert.That(actor.transform.position.y - Origin.y, Is.InRange(0.04f, 0.2f));
        }
        [Test]
        public void UnavailableOtherMapDoesNotPretendTravelSucceeded()
        {
            AddFloor(); portal.Configure(1, 1, 0, 999, 19, Origin + Vector3.right * 10);
            Assert.IsFalse(travel.TryTravel(portal, 1, 1));
            Assert.AreEqual(Origin, actor.transform.position); Assert.AreEqual(0, travel.TravelSerial);
            Assert.IsTrue(travel.LastMessage.Contains("19"));
        }
        [TestCase(0, 1)] [TestCase(1, 2)] [TestCase(1000, 1)]
        public void OriginalLevelAndFactionRestrictionsAreNotBypassed(int level, int faction)
        {
            AddFloor();
            Assert.IsFalse(travel.TryTravel(portal, level, faction));
            Assert.AreEqual(Origin, actor.transform.position);
        }
        [Test]
        public void NoGroundOrRemoteRequestCannotMoveThePlayer()
        {
            Assert.IsFalse(travel.TryTravel(portal, 1, 1));
            Assert.AreEqual(Origin, actor.transform.position);
            AddFloor(); portal.transform.position = Origin + Vector3.forward * 100;
            Assert.IsFalse(travel.TryTravel(portal, 1, 1));
            Assert.AreEqual(Origin, actor.transform.position);
        }
        [Test]
        public void DialogueAndClosedBossPortalsBlockLocalTravel()
        {
            AddFloor(); WorldInputGate.Set(travel, true);
            Assert.IsFalse(travel.TryTravel(portal, 1, 1)); WorldInputGate.Set(travel, false);
            portal.Configure(1, 3, 0, 999, 1, Origin + Vector3.right * 10);
            Assert.IsFalse(travel.TryTravel(portal, 1, 1));
            portal.SetOpen(true); Assert.IsTrue(travel.TryTravel(portal, 1, 1), travel.LastMessage);
        }
        [TestCase(false)] [TestCase(true)]
        public void PortalCancelsPendingStrikesButPreservesAnAlreadyAcceptedImpact(bool impactOccurred)
        {
            AddFloor();
            var enemy = Make("original target", Origin + Vector3.forward * 2);
            var target = enemy.AddComponent<ShaiyaCombatTarget>(); target.Configure(12, 100);
            Assert.IsTrue(combat.Select(target)); Assert.IsTrue(actor.Combat.RequestAttack(25));
            if (impactOccurred) actor.Combat.Tick(0.2);
            Assert.IsTrue(travel.TryTravel(portal, 1, 1), travel.LastMessage);
            Assert.IsNull(combat.SelectedTarget); Assert.IsNull(actor.Combat.SelectedTargetId);
            actor.Combat.Tick(1); combat.FlushHitToScene();
            Assert.AreEqual(impactOccurred ? 75 : 100, target.Health);
        }
        [Test]
        public void CanonicalMap1ReturnPortalIsNotAnInventedDestination()
        {
            var corpus = CanonicalClientCorpus.FromStoredRoot();
            if (corpus == null) Assert.Ignore("Requires original DATA.");
            var map = LegacySvmapParser.Parse(corpus.Resolve("DATA_Español/world/1.svmap"));
            var returns = map.Portals.Where(p => p.TargetMapId == 1).ToArray();
            Assert.AreEqual(1, returns.Length);
            Assert.AreEqual(1, returns[0].FactionOrPortalId);
            Assert.AreEqual(new Vector3(542.25f, 77.75f, 1760.25f), returns[0].TargetPosition);
        }
    }
}
