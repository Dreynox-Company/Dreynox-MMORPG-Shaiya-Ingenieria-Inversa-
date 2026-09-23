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

        [Test]
        public void InventoryProgressionQuestAndServicesAreDeterministic()
        {
            var inventory = new InventoryCore(4);
            Assert.AreEqual(0, inventory.Add(100, 120, 99));
            Assert.AreEqual(120, inventory.CountItem(100));
            Assert.IsTrue(inventory.Remove(100, 21));
            Assert.AreEqual(99, inventory.CountItem(100));

            var life = new LifeCore(1000);
            Assert.AreEqual(1000, life.Damage(1000));
            Assert.IsTrue(life.Dead);
            life.Rebirth(0.25);
            Assert.AreEqual(250, life.Health);

            var quest = new QuestCore(1);
            quest.AddObjective(10, 2);
            Assert.IsTrue(quest.Accept());
            Assert.IsTrue(quest.Progress(10, 2));
            Assert.AreEqual(QuestState.Completed, quest.State);
            Assert.IsTrue(quest.Reward());

            var shop = new ShopCore();
            shop.SetPrice(200, 100, 50);
            long gold = 500;
            Assert.IsTrue(shop.TryBuy(inventory, 200, 2, 99, ref gold));
            Assert.AreEqual(300, gold);
            Assert.IsTrue(shop.TrySell(inventory, 200, 1, ref gold));
            Assert.AreEqual(350, gold);
        }

        [Test]
        public void SkillsFriendsGuildAndClientFlowPreserveLockedState()
        {
            var resource = new ResourcePoolCore(100);
            var skills = new SkillCore();
            skills.Learn(new SkillDefinitionCore(1, 3, 25, 0.1, 0.2, 1.0, true));
            Assert.IsTrue(skills.TryCast(1, 77, resource));
            skills.Tick(0.11);
            Assert.NotNull(skills.LastCast);
            Assert.AreEqual(77, skills.LastCast.TargetId);
            Assert.AreEqual(3, skills.LastCast.Rank);

            var friends = new FriendsCore();
            Assert.IsTrue(friends.ReceiveRequest(5));
            Assert.IsTrue(friends.AcceptIncoming(5, true));
            Assert.IsTrue(friends.Friends[5].Online);

            var guild = new GuildCore("Test", 10);
            Assert.IsTrue(guild.Create(1));
            Assert.IsTrue(guild.AddMember(1, 2));
            Assert.IsTrue(guild.SetOfficer(1, 2, true));
            Assert.IsTrue(guild.AddMember(2, 3));

            var flow = new ClientFlowCore();
            Assert.IsTrue(flow.ReadyForLogin());
            Assert.IsTrue(flow.BeginConnect());
            Assert.IsTrue(flow.LoginAccepted());
            Assert.IsTrue(flow.SelectServer(1));
            Assert.IsTrue(flow.SetCharacterList(new[] { new CharacterSummaryCore(10, "Hero", 1, 0) }));
            Assert.IsTrue(flow.EnterCharacter(10));
            Assert.IsTrue(flow.WorldAccepted());
            Assert.AreEqual(ClientFlowState.InWorld, flow.State);
        }

        [Test]
        public void LootNpcWeatherAndBlacksmithRemainDataDriven()
        {
            var inventory = new InventoryCore(3);
            var loot = new LootCore();
            Assert.IsTrue(loot.Spawn(new LootDropCore(1, 300, 2, 9)));
            Assert.IsFalse(loot.TryCollect(1, 10, inventory, 99));
            Assert.IsTrue(loot.TryCollect(1, 9, inventory, 99));

            var npc = new NpcInteractionCore();
            npc.Register(7, NpcServiceKind.Shop | NpcServiceKind.Gatekeeper);
            Assert.IsTrue(npc.Open(7));
            Assert.IsTrue(npc.Supports(NpcServiceKind.Shop));
            Assert.IsFalse(npc.Supports(NpcServiceKind.Blacksmith));

            var weather = new WeatherCore();
            weather.TransitionTo(WeatherKindCore.Fog, 2);
            weather.Tick(1);
            Assert.AreEqual(0.5, weather.Blend, 0.0001);
            weather.Tick(1);
            Assert.AreEqual(WeatherKindCore.Fog, weather.Current);

            int level = 0;
            long gold = 1000;
            var blacksmith = new BlacksmithCore(new BlacksmithUpgradeProfile
            {
                MaxLevel = 3,
                CostForNextLevel = next => 100 * next,
                SuccessProbabilityForNextLevel = next => 0.5
            });
            Assert.IsTrue(blacksmith.TryUpgrade(ref level, ref gold, 0.25));
            Assert.AreEqual(1, level);
            Assert.AreEqual(900, gold);
        }
        [Test]
        public void CameraYawNinetyForwardResolvesNegativeWorldX()
        {
            ClientCoordinateCore.ResolveCameraRelative(0, 1, 90, out double worldX, out double worldZ);
            Assert.AreEqual(-1.0, worldX, 0.000001);
            Assert.AreEqual(0.0, worldZ, 0.000001);
        }

        [Test]
        public void FlightCombatDescentAndResumeMatchRecoveredContract()
        {
            var flight = new FlightTransitionCore();
            flight.SetWingsEquipped(true);
            Assert.AreEqual(ClientFlightPhase.Grounded, flight.Phase);
            Assert.IsFalse(flight.ManualRequested, "Equipping wings must not activate flight.");

            Assert.IsTrue(flight.ToggleManualFlight());
            Assert.AreEqual(ClientFlightPhase.TakingOff, flight.Phase);
            flight.Tick(FlightTransitionCore.DefaultTakeoffSeconds);
            Assert.AreEqual(ClientFlightPhase.Hover, flight.Phase);

            flight.SetMoving(true);
            Assert.AreEqual(ClientFlightPhase.Flight, flight.Phase);

            flight.SetCombatGuard(true);
            Assert.IsTrue(flight.BeginCombatDescent());
            Assert.AreEqual(ClientFlightPhase.CombatDescending, flight.Phase);
            flight.Tick(FlightTransitionCore.DefaultCombatDescentSeconds);
            Assert.AreEqual(ClientFlightPhase.Grounded, flight.Phase);
            Assert.IsTrue(flight.ManualRequested, "Combat landing preserves the user's flight intent.");

            flight.SetCombatGuard(false);
            Assert.AreEqual(ClientFlightPhase.TakingOff, flight.Phase);
            flight.Tick(FlightTransitionCore.DefaultTakeoffSeconds);
            Assert.AreEqual(ClientFlightPhase.Flight, flight.Phase);
        }

        [Test]
        public void RemovingWingsCancelsFlightIntentAndReturnsGrounded()
        {
            var flight = new FlightTransitionCore();
            flight.SetWingsEquipped(true);
            flight.ToggleManualFlight();
            flight.Tick(FlightTransitionCore.DefaultTakeoffSeconds);
            Assert.IsTrue(flight.Airborne);

            flight.SetWingsEquipped(false);
            Assert.IsFalse(flight.ManualRequested);
            Assert.AreEqual(ClientFlightPhase.Landing, flight.Phase);
            flight.Tick(FlightTransitionCore.DefaultLandingSeconds);
            Assert.AreEqual(ClientFlightPhase.Grounded, flight.Phase);
        }

    }
}
