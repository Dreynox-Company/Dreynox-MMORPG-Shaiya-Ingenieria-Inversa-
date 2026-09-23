using Dreynox.Mmorpg.ParityCore;
using NUnit.Framework;

namespace Dreynox.Mmorpg.Tests
{
    public sealed class ClientParityCoreTests
    {
        [Test]
        public void LocomotionMatchesVerifiedFlutterBaseline()
        {
            var motion = new ClientMotionCore();
            Assert.AreEqual(ClientMotionState.Idle, motion.State);

            int initialGeneration = motion.ClipGeneration;
            motion.SetMove(0, 1, false);
            Assert.AreEqual(ClientMotionState.Walk, motion.State);
            int walkGeneration = motion.ClipGeneration;
            Assert.Greater(walkGeneration, initialGeneration);

            motion.SetMove(0, 1, false);
            Assert.AreEqual(walkGeneration, motion.ClipGeneration, "Key repeat must not restart the clip.");

            motion.SetMove(0, 1, true);
            Assert.AreEqual(ClientMotionState.Run, motion.State);
            motion.SetMove(0, 1, false);
            Assert.AreEqual(ClientMotionState.Walk, motion.State);
            motion.SetMove(0, 0, true);
            Assert.AreEqual(ClientMotionState.Idle, motion.State, "Shift alone must not move the actor.");

            motion.SetMove(0, 1, false);
            motion.SetFocus(false);
            Assert.AreEqual(ClientMotionState.Idle, motion.State);
            Assert.IsFalse(motion.HasMovement);
        }

        [Test]
        public void EquipmentRulesKeepOneHandShieldAndClearTwoHandOffhand()
        {
            var equipment = new EquipmentRuleCore();
            equipment.EquipMainHand(ClientWeaponFamily.OneHand);
            Assert.IsTrue(equipment.EquipShield());
            Assert.IsTrue(equipment.HasOffHandShield);
            equipment.EquipMainHand(ClientWeaponFamily.Spear);
            Assert.IsFalse(equipment.HasOffHandShield);
            Assert.IsTrue(equipment.UsesBothHands);
        }

        [Test]
        public void SpearHasDedicatedRunSemantic()
        {
            var motion = new ClientMotionCore();
            motion.SetWeaponFamily(ClientWeaponFamily.Spear);
            motion.SetMove(0, 1, true);
            Assert.AreEqual("run_spear", motion.ClipKey);
        }

        [Test]
        public void WingsDoNotAutomaticallyStartFlight()
        {
            var motion = new ClientMotionCore();
            motion.SetWingsEquipped(true);
            Assert.AreEqual(ClientMotionState.Idle, motion.State);
            Assert.IsFalse(motion.FlightRequested);
            Assert.IsTrue(motion.ToggleFlight());
            Assert.AreEqual(ClientMotionState.Hover, motion.State);
            motion.SetMove(0, 1, false);
            Assert.AreEqual(ClientMotionState.Flight, motion.State);
            motion.SetWingsEquipped(false);
            Assert.AreEqual(ClientMotionState.Walk, motion.State);
        }

        [Test]
        public void MountMovementAndWingSeatHeightAreStable()
        {
            var motion = new ClientMotionCore();
            var equipment = new EquipmentRuleCore();
            equipment.EquipWings(17.5, 0.12);
            equipment.SetMount(true, 1.25);
            motion.SetMounted(true);
            motion.SetMove(0, 0, false);
            Assert.AreEqual(ClientMotionState.MountIdle, motion.State);
            motion.SetMove(0, 1, false);
            Assert.AreEqual(ClientMotionState.MountWalk, motion.State);
            motion.SetMove(0, 1, true);
            Assert.AreEqual(ClientMotionState.MountRun, motion.State);
            Assert.AreEqual(11.37, equipment.ResolveWingRootHeight(10.0), 0.0001);
            Assert.AreEqual(11.37, equipment.ResolveWingRootHeight(10.0), 0.0001, "Seat calibration must not accumulate every frame.");
        }

        [Test]
        public void CombatLocksOriginalTargetAndTracksIndependentHealth()
        {
            var combat = new CombatCore();
            combat.RegisterTarget(10, 1000);
            combat.RegisterTarget(11, 1000);
            Assert.IsTrue(combat.SelectTarget(10));
            Assert.IsTrue(combat.RequestAttack(120, 0.05, 0.10, 0.10));
            Assert.IsTrue(combat.SelectTarget(11));
            combat.Tick(0.11);
            Assert.AreEqual(880, combat.Targets[10].Health);
            Assert.AreEqual(1000, combat.Targets[11].Health);
        }

        [Test]
        public void CombatGuardExpiresEightSecondsAfterLastHit()
        {
            var combat = new CombatCore();
            combat.RegisterTarget(1, 100);
            combat.SelectTarget(1);
            combat.RequestAttack(10, 0.01, 0.01, 0.01);
            combat.Tick(0.02);
            Assert.IsTrue(combat.InCombatGuard);
            combat.Tick(7.9);
            Assert.IsTrue(combat.InCombatGuard);
            combat.Tick(0.2);
            Assert.IsFalse(combat.InCombatGuard);
        }

        [Test]
        public void WorldStreamingLoadsNearbyAndFreesDepartedSectors()
        {
            var planner = new WorldSectorPlanner(1, 100.0);
            SectorPlan first = planner.Update(10, 10);
            Assert.Greater(first.Load.Count, 0);
            SectorPlan second = planner.Update(310, 10);
            Assert.Greater(second.Load.Count, 0);
            Assert.Greater(second.Unload.Count, 0);
        }

        [Test]
        public void TradeRequiresBothConfirmationsAndMutationUnlocksIt()
        {
            var trade = new TradeCore();
            trade.Open(1, 2);
            Assert.IsTrue(trade.SetItem(1, 900, 1));
            Assert.IsTrue(trade.Confirm(1));
            Assert.AreEqual(TradePhase.Locked, trade.Phase);
            Assert.IsTrue(trade.Confirm(2));
            Assert.AreEqual(TradePhase.Completed, trade.Phase);
        }

        [Test]
        public void PartyAndRaidMaintainMembership()
        {
            var party = new PartyCore();
            Assert.IsTrue(party.Create(1));
            Assert.IsTrue(party.Add(2));
            var raid = new RaidCore();
            Assert.IsTrue(raid.AddGroup(party));
            Assert.IsTrue(raid.ContainsMember(2));
            Assert.AreEqual(2, raid.MemberCount);
        }
    }
}
