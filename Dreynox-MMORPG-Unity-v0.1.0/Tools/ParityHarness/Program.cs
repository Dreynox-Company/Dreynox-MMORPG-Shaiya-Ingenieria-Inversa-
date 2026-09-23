using System;
using Dreynox.Mmorpg.ParityCore;

namespace Dreynox.Mmorpg.ParityHarness
{
    internal static class Program
    {
        private static int _count;

        private static void Check(bool condition, string name)
        {
            _count++;
            if (!condition) throw new InvalidOperationException("PARITY FAIL: " + name);
            Console.WriteLine("PASS " + name);
        }

        public static void Main()
        {
            var motion = new ClientMotionCore();
            Check(motion.State == ClientMotionState.Idle, "initial standing idle");
            motion.SetMove(0, 1, false);
            Check(motion.State == ClientMotionState.Walk, "W chooses walk");
            var generation = motion.ClipGeneration;
            motion.SetMove(0, 1, false);
            Check(motion.ClipGeneration == generation, "key repeat does not restart clip");
            motion.SetMove(0, 1, true);
            Check(motion.State == ClientMotionState.Run, "W+Shift chooses run");
            motion.SetMove(0, 1, false);
            Check(motion.State == ClientMotionState.Walk, "Shift release returns walk");
            motion.SetMove(0, 0, false);
            Check(motion.State == ClientMotionState.Idle, "release W returns idle");
            motion.SetMove(0, 0, true);
            Check(motion.State == ClientMotionState.Idle, "Shift alone remains idle");
            motion.SetMove(0, 1, false);
            motion.SetFocus(false);
            Check(motion.State == ClientMotionState.Idle && !motion.HasMovement, "focus loss cancels movement");
            motion.SetFocus(true);
            Check(motion.BeginJump(), "jump starts from ground state");
            Check(motion.State == ClientMotionState.Jump, "jump semantic selected");
            motion.Tick(1.0);
            Check(motion.State == ClientMotionState.Idle, "jump returns idle");

            var equipment = new EquipmentRuleCore();
            equipment.EquipMainHand(ClientWeaponFamily.OneHand);
            Check(equipment.EquipShield(), "one hand allows shield");
            equipment.EquipMainHand(ClientWeaponFamily.Spear);
            Check(!equipment.HasOffHandShield, "two hand spear removes offhand");
            motion.SetWeaponFamily(ClientWeaponFamily.Spear);
            motion.SetMove(0, 1, true);
            Check(motion.ClipKey == "run_spear", "spear uses dedicated run semantic");

            motion.SetMove(0, 0, false);
            motion.SetWingsEquipped(true);
            Check(!motion.FlightRequested && motion.State == ClientMotionState.Idle, "equip wings does not auto fly");
            Check(motion.ToggleFlight(), "manual flight toggle accepted");
            Check(motion.State == ClientMotionState.Hover, "wing idle chooses hover");
            motion.SetMove(0, 1, false);
            Check(motion.State == ClientMotionState.Flight, "wing movement chooses flight");
            motion.SetWingsEquipped(false);
            Check(motion.State == ClientMotionState.Walk, "remove wings restores ground motion");

            equipment.EquipWings(25, 0.1);
            equipment.SetMount(true, 1.2);
            Check(Math.Abs(equipment.ResolveWingRootHeight(10) - 11.3) < 0.0001, "wing inherits seat height once");
            Check(Math.Abs(equipment.ResolveWingRootHeight(10) - 11.3) < 0.0001, "seat height does not accumulate");
            motion.SetMounted(true);
            motion.SetMove(0, 1, false);
            Check(motion.State == ClientMotionState.MountWalk, "mounted W chooses mount walk");
            motion.SetMove(0, 1, true);
            Check(motion.State == ClientMotionState.MountRun, "mounted Shift chooses mount run");
            motion.SetMove(0, 0, false);
            Check(motion.State == ClientMotionState.MountIdle, "mounted release returns riding idle");

            var combat = new CombatCore();
            combat.RegisterTarget(10, 1000);
            combat.RegisterTarget(11, 1000);
            Check(combat.SelectTarget(10), "visible opponent selectable");
            Check(combat.RequestAttack(150, 0.05, 0.10, 0.10), "attack request accepted");
            Check(combat.InCombatGuard, "attack enters guard window");
            combat.SelectTarget(11);
            combat.Tick(0.11);
            Check(combat.Targets[10].Health == 850, "in-flight attack remains bound original opponent");
            Check(combat.Targets[11].Health == 1000, "opponents keep independent health");
            combat.Tick(7.7);
            Check(combat.InCombatGuard, "guard remains before eight seconds");
            combat.Tick(0.5);
            Check(!combat.InCombatGuard, "guard ends after eight seconds");

            var planner = new WorldSectorPlanner(1, 100);
            var p1 = planner.Update(0, 0);
            Check(p1.Load.Count > 0, "large map streams proximity");
            var p2 = planner.Update(400, 0);
            Check(p2.Unload.Count > 0 && p2.Load.Count > 0, "departed sectors freed");

            var trade = new TradeCore();
            trade.Open(1, 2);
            trade.SetItem(1, 100, 1);
            trade.Confirm(1);
            Check(trade.Phase == TradePhase.Locked, "trade first confirmation locks one side");
            trade.Confirm(2);
            Check(trade.Phase == TradePhase.Completed, "trade requires both confirmations");

            var party = new PartyCore();
            party.Create(1);
            party.Add(2);
            var raid = new RaidCore();
            raid.AddGroup(party);
            Check(raid.ContainsMember(2), "party member represented in raid");

            Console.WriteLine("PARITY HARNESS OK: " + _count + " checks");
        }
    }
}
