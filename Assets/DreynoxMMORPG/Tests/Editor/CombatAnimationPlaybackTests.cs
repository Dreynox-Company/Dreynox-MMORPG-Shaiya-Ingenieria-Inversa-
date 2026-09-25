using System;
using Dreynox.Mmorpg.Gameplay.AnimationSystem;
using Dreynox.Mmorpg.ParityCore;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dreynox.Mmorpg.Tests.Editor
{
    public sealed class CombatAnimationPlaybackTests
    {
        [Test]
        public void RepeatedLocomotionDoesNotRestartButANewAttackDoes()
        {
            var go = new GameObject("Playback fixture");
            var clip = new AnimationClip();
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 0));
            var catalog = ScriptableObject.CreateInstance<AnimationStateCatalog>();
            try
            {
                catalog.ReplaceEntries(new[] {
                    new AnimationStateCatalog.Entry { semanticState="walk",clip=clip,playbackSpeed=1 },
                    new AnimationStateCatalog.Entry { semanticState="attack_1",clip=clip,playbackSpeed=1 }});
                var player = go.AddComponent<SemanticAnimationPlayer>(); player.Catalog = catalog;
                Assert.IsTrue(player.PlaySemantic("walk", 0));
                int serial = player.PlaybackSerial;
                Assert.IsTrue(player.PlaySemantic("walk", 0));
                Assert.AreEqual(serial, player.PlaybackSerial, "Held W must not restart ANI every frame.");
                Assert.IsTrue(player.ReplaySemantic("attack_1", 0));
                Assert.AreEqual(serial + 1, player.PlaybackSerial);
                Assert.IsTrue(player.ReplaySemantic("attack_1", 0));
                Assert.AreEqual(serial + 2, player.PlaybackSerial, "Another accepted strike must replay the same one-shot.");
                Assert.AreEqual("attack_1", player.ResolvedSemantic);
                Assert.IsFalse(player.ReplaySemantic("missing_attack", 0));
                Assert.AreEqual(serial + 2, player.PlaybackSerial, "A missing animation must not count as rendered.");
                player.Stop(); Assert.IsFalse(player.HasPlayableClip);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(catalog); Object.DestroyImmediate(clip); }
        }
        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void HitRecoveryAndGuardAreIndependentOfStepSize(int fps)
        {
            var combat = new CombatCore(); combat.RegisterTarget(10, 100); combat.SelectTarget(10);
            Assert.IsTrue(combat.RequestAttack(25));
            for (int i = 0; i < fps; i++) combat.Tick(1.0/fps);
            Assert.AreEqual(AttackPhase.Idle, combat.Phase);
            Assert.AreEqual(75, combat.Targets[10].Health);
            Assert.AreEqual(1, combat.HitSerial);
            Assert.AreEqual(1, combat.AttackSerial);
            Assert.AreEqual(7.18, combat.GuardRemaining, 1e-8);
            Assert.IsTrue(combat.RequestAttack(25));
            Assert.AreEqual(2, combat.AttackSerial);
            Assert.IsFalse(combat.RequestAttack(25));
            Assert.AreEqual(2, combat.AttackSerial, "Rejected input is not a new attack.");
        }
        [Test]
        public void ALongFrameConsumesBothPhasesOnceWithoutAnExtraHit()
        {
            var combat = new CombatCore(); combat.RegisterTarget(1,100);combat.SelectTarget(1);
            combat.RequestAttack(25);combat.Tick(1);
            Assert.AreEqual(AttackPhase.Idle,combat.Phase);
            Assert.AreEqual(75,combat.Targets[1].Health);
            Assert.AreEqual(1,combat.HitSerial);
            combat.Tick(9);Assert.IsFalse(combat.InCombatGuard);
            Assert.Throws<ArgumentOutOfRangeException>(()=>combat.RequestAttack(25, double.NaN));
        }
    }
}
