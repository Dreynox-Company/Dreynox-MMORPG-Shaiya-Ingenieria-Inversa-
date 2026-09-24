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

        [Test]
        public void VisualMetricsAreExactForIdenticalFrames()
        {
            byte[] frame =
            {
                0, 0, 0,
                255, 255, 255,
                64, 128, 192,
                12, 34, 56
            };

            VisualMetricResult result = VisualMetricCore.CompareRgb24(frame, frame);
            Assert.AreEqual(4, result.PixelCount);
            Assert.AreEqual(0.0, result.Mae, 0.0000001);
            Assert.AreEqual(0.0, result.Rmse, 0.0000001);
            Assert.IsTrue(double.IsPositiveInfinity(result.Psnr));
            Assert.AreEqual(1.0, result.Ssim, 0.0000001);
        }

        [Test]
        public void VisualMetricsDetectAChangedFrame()
        {
            byte[] reference = { 0, 0, 0, 255, 255, 255 };
            byte[] candidate = { 255, 255, 255, 255, 255, 255 };

            VisualMetricResult result = VisualMetricCore.CompareRgb24(reference, candidate);
            Assert.Greater(result.Mae, 0.0);
            Assert.Greater(result.Rmse, 0.0);
            Assert.Less(result.Ssim, 1.0);
        }

        [Test]
        public void CapturedTargetAttackDoesNotRetargetBeforeExecution()
        {
            var combat = new CombatCore();
            combat.RegisterTarget(10, 1000);
            combat.RegisterTarget(11, 1000);

            Assert.IsTrue(combat.SelectTarget(10));
            int captured = combat.SelectedTargetId.Value;
            Assert.IsTrue(combat.SelectTarget(11));

            Assert.IsTrue(combat.RequestAttackAt(captured, 125, 0.01, 0.02, 0.01));
            combat.Tick(0.03);

            Assert.AreEqual(875, combat.Targets[10].Health);
            Assert.AreEqual(1000, combat.Targets[11].Health);
            Assert.AreEqual(11, combat.SelectedTargetId.Value);
            Assert.AreEqual(10, combat.LastHitTargetId.Value);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(144)]
        public void CombatDescentDurationIsFrameRateIndependent(int framesPerSecond)
        {
            var flight = new FlightTransitionCore();
            flight.SetWingsEquipped(true);
            Assert.IsTrue(flight.ToggleManualFlight());
            flight.Tick(FlightTransitionCore.DefaultTakeoffSeconds);
            flight.SetMoving(true);
            flight.SetCombatGuard(true);
            Assert.IsTrue(flight.BeginCombatDescent());

            double dt = 1.0 / framesPerSecond;
            double elapsed = 0.0;
            while (flight.Phase == ClientFlightPhase.CombatDescending &&
                   elapsed < 1.0)
            {
                flight.Tick(dt);
                elapsed += dt;
            }

            Assert.AreEqual(ClientFlightPhase.Grounded, flight.Phase);
            Assert.GreaterOrEqual(
                elapsed + 0.0000001,
                FlightTransitionCore.DefaultCombatDescentSeconds);
            Assert.LessOrEqual(
                elapsed,
                FlightTransitionCore.DefaultCombatDescentSeconds + dt + 0.0000001);
        }

        [Test]
        public void CanonicalWingPositionTableCoversEveryLegacyCombination()
        {
            Assert.AreEqual(48, LegacyWingPoseCore.Count);

            for (int family = 0; family < 4; family++)
            for (int job = 0; job < 6; job++)
            for (int sex = 0; sex < 2; sex++)
            {
                Assert.IsTrue(
                    LegacyWingPoseCore.TryResolve(
                        family,
                        job,
                        sex,
                        out LegacyWingPose pose),
                    "Missing wing profile for " +
                    family + "/" + job + "/" + sex);

                Assert.AreEqual(4, pose.BoneIndex);
                Assert.AreEqual(90.0, pose.RotZ, 0.0000001);
                Assert.AreEqual(0.0, pose.LeftRight, 0.0000001);
            }
        }

        [Test]
        public void CanonicalWingPositionSamplesMatchSuppliedCorpus()
        {
            LegacyWingPose humanMaleFighter =
                LegacyWingPoseCore.Resolve(0, 0, 0);
            Assert.AreEqual(170.0, humanMaleFighter.RotX, 0.0000001);
            Assert.AreEqual(0.05, humanMaleFighter.UpDown, 0.0000001);
            Assert.AreEqual(-0.18, humanMaleFighter.FrontBack, 0.0000001);

            LegacyWingPose elfFemaleRogue =
                LegacyWingPoseCore.Resolve(1, 2, 1);
            Assert.AreEqual(175.0, elfFemaleRogue.RotX, 0.0000001);
            Assert.AreEqual(0.03, elfFemaleRogue.UpDown, 0.0000001);
            Assert.AreEqual(-0.12, elfFemaleRogue.FrontBack, 0.0000001);

            LegacyWingPose deathEaterMaleShooter =
                LegacyWingPoseCore.Resolve(3, 3, 0);
            Assert.AreEqual(185.0, deathEaterMaleShooter.RotX, 0.0000001);
            Assert.AreEqual(0.15, deathEaterMaleShooter.UpDown, 0.0000001);
            Assert.AreEqual(-0.14, deathEaterMaleShooter.FrontBack, 0.0000001);
        }

        [Test]
        public void OfflineWorldPopulationMatchesCapturedPs0032Sessions()
        {
            Assert.AreEqual(13, LegacyWorldPopulationCore.All.Count);
            Assert.AreEqual(30800, LegacyWorldPopulationCore.LoginPort);

            LegacyWorldPopulation map0 = LegacyWorldPopulationCore.Get(0);
            Assert.AreEqual(11, map0.Portals);
            Assert.AreEqual(141, map0.Npcs);
            Assert.AreEqual(509, map0.MobAreas);
            Assert.AreEqual(1330, map0.Mobs);
            Assert.AreEqual(1, map0.Obelisks);

            LegacyWorldPopulation map1 = LegacyWorldPopulationCore.Get(1);
            Assert.AreEqual(247, map1.Npcs);
            Assert.AreEqual(502, map1.MobAreas);
            Assert.AreEqual(1186, map1.Mobs);

            LegacyWorldPopulation map2 = LegacyWorldPopulationCore.Get(2);
            Assert.AreEqual(194, map2.Npcs);
            Assert.AreEqual(800, map2.MobAreas);
            Assert.AreEqual(868, map2.Mobs);
        }

        [Test]
        public void CharacterFlowSupportsCreateDeleteAndAppearance()
        {
            var flow = new ClientFlowCore();

            Assert.IsTrue(flow.ReadyForLogin());
            Assert.IsTrue(flow.BeginConnect());
            Assert.IsTrue(flow.LoginAccepted());
            Assert.IsTrue(flow.SelectServer(1));

            Assert.IsTrue(flow.SetCharacterList(
                new[]
                {
                    new CharacterSummaryCore(
                        100,
                        "Existing",
                        33,
                        0,
                        1,
                        2,
                        1,
                        3,
                        4,
                        0,
                        CharacterDifficultyMode.Basic)
                }));

            Assert.IsTrue(flow.BeginCharacterCreate(1));
            Assert.AreEqual(ClientFlowState.CharacterCreate, flow.State);
            Assert.AreEqual(1, flow.CharacterCreateSlot.Value);

            var request = new CharacterCreationRequestCore(
                "NewHero",
                1,
                3,
                5,
                0,
                2,
                1,
                CharacterDifficultyMode.Ultimate);

            Assert.AreEqual("NewHero", request.Name);
            Assert.AreEqual(3, request.Family);
            Assert.AreEqual(5, request.Job);
            Assert.AreEqual(0, request.Sex);
            Assert.AreEqual(2, request.Face);
            Assert.AreEqual(1, request.Hair);
            Assert.AreEqual(CharacterDifficultyMode.Ultimate, request.Mode);

            var created = new CharacterSummaryCore(
                101,
                request.Name,
                1,
                request.Slot,
                request.Family,
                request.Job,
                request.Sex,
                request.Face,
                request.Hair,
                0,
                request.Mode);

            Assert.IsTrue(flow.CharacterCreated(created));
            Assert.AreEqual(ClientFlowState.CharacterSelect, flow.State);
            Assert.AreEqual(2, flow.Characters.Count);
            Assert.AreEqual(101, flow.Characters[1].CharacterId);

            Assert.IsTrue(flow.CharacterDeleted(100));
            Assert.AreEqual(1, flow.Characters.Count);
            Assert.AreEqual(101, flow.Characters[0].CharacterId);
        }

        [Test]
        public void CharacterCreateRejectsOccupiedAndOutOfRangeSlots()
        {
            var flow = new ClientFlowCore();
            flow.ReadyForLogin();
            flow.BeginConnect();
            flow.LoginAccepted();
            flow.SelectServer(1);
            flow.SetCharacterList(
                new[]
                {
                    new CharacterSummaryCore(1, "Hero", 1, 0)
                });

            Assert.IsFalse(flow.BeginCharacterCreate(0));
            Assert.IsFalse(flow.BeginCharacterCreate(-1));
            Assert.IsFalse(flow.BeginCharacterCreate(5));
            Assert.IsTrue(flow.BeginCharacterCreate(1));
            Assert.IsTrue(flow.CancelCharacterCreate());
            Assert.AreEqual(ClientFlowState.CharacterSelect, flow.State);
        }

        [Test]
        public void CanonicalNpcServiceGroupsResolveDeterministically()
        {
            Assert.AreEqual(
                NpcServiceKind.Shop,
                NpcServiceResolverCore.Resolve(1, false));

            Assert.AreEqual(
                NpcServiceKind.Gatekeeper,
                NpcServiceResolverCore.Resolve(2, false));

            Assert.AreEqual(
                NpcServiceKind.Blacksmith | NpcServiceKind.Quest,
                NpcServiceResolverCore.Resolve(3, true));

            Assert.AreEqual(
                NpcServiceKind.Warehouse | NpcServiceKind.Quest,
                NpcServiceResolverCore.Resolve(6, true));

            Assert.AreEqual(
                NpcServiceKind.Quest,
                NpcServiceResolverCore.Resolve(8, true));

            Assert.AreEqual(
                NpcServiceKind.None,
                NpcServiceResolverCore.Resolve(8, false));
        }

        [Test]
        public void SvmapPortalRulesMatchOfflineServerSemantics()
        {
            var neutral =
                new PortalTravelCore(
                    0,
                    0,
                    1,
                    80,
                    1,
                    10,
                    20,
                    30);

            Assert.IsTrue(neutral.IsOpenByDefault);
            Assert.IsTrue(neutral.CanEnter(40, 1, false));
            Assert.IsTrue(neutral.CanEnter(40, 2, false));

            var light =
                new PortalTravelCore(
                    0,
                    1,
                    20,
                    30,
                    18,
                    100,
                    10,
                    200);

            Assert.IsTrue(light.CanEnter(20, 1, false));
            Assert.IsTrue(light.CanEnter(30, 1, false));
            Assert.IsFalse(light.CanEnter(19, 1, false));
            Assert.IsFalse(light.CanEnter(31, 1, false));
            Assert.IsFalse(light.CanEnter(25, 2, false));

            var boss =
                new PortalTravelCore(
                    0,
                    7,
                    1,
                    80,
                    42,
                    500,
                    20,
                    500);

            Assert.IsTrue(boss.IsBossActivatedPortal);
            Assert.IsFalse(boss.IsOpenByDefault);
            Assert.IsFalse(boss.CanEnter(50, 1, false));
            Assert.IsTrue(boss.CanEnter(50, 1, true));
            Assert.IsTrue(boss.CanEnter(50, 2, true));
        }

        [Test]
        public void NativeVisualReferenceSuiteIsCanonicalAndUnique()
        {
            Assert.AreEqual(
                8,
                NativeVisualReferenceCore.All.Count);

            var ids =
                new System.Collections.Generic.HashSet<string>(
                    System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0;
                 i < NativeVisualReferenceCore.All.Count;
                 i++)
            {
                NativeVisualReference reference =
                    NativeVisualReferenceCore.All[i];

                Assert.AreEqual(
                    1024,
                    reference.Width);

                Assert.AreEqual(
                    768,
                    reference.Height);

                Assert.AreEqual(
                    64,
                    reference.Sha256.Length);

                Assert.AreEqual(
                    3,
                    reference.NativeCropX);

                Assert.AreEqual(
                    26,
                    reference.NativeCropY);

                Assert.AreEqual(
                    1021,
                    reference.NativeCropWidth);

                Assert.AreEqual(
                    739,
                    reference.NativeCropHeight);

                Assert.IsTrue(
                    ids.Add(
                        reference.ScenarioId));
            }

            Assert.AreEqual(
                "2fd2807d305f5ae589f30232ac31c52a12f9caef66ed1b674ca6405a607f5549",
                NativeVisualReferenceCore
                    .Get("character-editor")
                    .Sha256);

            Assert.AreEqual(
                "c19cb5f06154bf6eacb029b08be7f6762bd9a002c044ff170363a9dfeadee6a0",
                NativeVisualReferenceCore
                    .Get("world-loaded")
                    .Sha256);

            Assert.AreEqual(
                "509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d",
                NativeVisualReferenceCore
                    .OriginalClientSha256);
        }

        [Test]
        public void VaniFrameTimingUsesObservedMillisecondIntervals()
        {
            Assert.AreEqual(
                0.066,
                LegacyVaniAnimationCore.FrameDurationSeconds(66),
                0.0000001);

            Assert.AreEqual(
                1.056,
                LegacyVaniAnimationCore.CycleSeconds(16, 66),
                0.0000001);

            Assert.AreEqual(
                0,
                LegacyVaniAnimationCore.ResolveFrameIndex(
                    0.065,
                    16,
                    66));

            Assert.AreEqual(
                1,
                LegacyVaniAnimationCore.ResolveFrameIndex(
                    0.066,
                    16,
                    66));

            Assert.AreEqual(
                15,
                LegacyVaniAnimationCore.ResolveFrameIndex(
                    1.055,
                    16,
                    66));

            Assert.AreEqual(
                0,
                LegacyVaniAnimationCore.ResolveFrameIndex(
                    1.056,
                    16,
                    66));

            Assert.AreEqual(
                30,
                LegacyVaniAnimationCore.ResolveFrameIndex(
                    0.999,
                    61,
                    33));

            Assert.AreEqual(
                10,
                LegacyVaniAnimationCore.ResolveFrameIndex(
                    1.0,
                    21,
                    100));
        }

        [Test]
        public void Ps0032RigSelectionMatchesRecoveredX86Table()
        {
            LegacyCharacterRigSelection[] cases =
            {
                LegacyCharacterRigCore.Resolve(0, 0, 0),
                LegacyCharacterRigCore.Resolve(0, 5, 0),
                LegacyCharacterRigCore.Resolve(0, 0, 1),
                LegacyCharacterRigCore.Resolve(0, 5, 1),

                LegacyCharacterRigCore.Resolve(1, 2, 0),
                LegacyCharacterRigCore.Resolve(1, 4, 0),
                LegacyCharacterRigCore.Resolve(1, 2, 1),
                LegacyCharacterRigCore.Resolve(1, 4, 1),

                LegacyCharacterRigCore.Resolve(2, 0, 0),
                LegacyCharacterRigCore.Resolve(2, 3, 0),
                LegacyCharacterRigCore.Resolve(2, 0, 1),
                LegacyCharacterRigCore.Resolve(2, 3, 1),

                LegacyCharacterRigCore.Resolve(3, 2, 0),
                LegacyCharacterRigCore.Resolve(3, 4, 0),
                LegacyCharacterRigCore.Resolve(3, 2, 1),
                LegacyCharacterRigCore.Resolve(3, 4, 1)
            };

            string[] expected =
            {
                "humf", "humm", "huwf", "huwm",
                "elmr", "elmm", "elwr", "elwm",
                "demf", "demr", "dewf", "dewr",
                "vimr", "vimm", "viwr", "viwm"
            };

            for (int i = 0; i < cases.Length; i++)
            {
                Assert.AreEqual(
                    i,
                    cases[i].NativeRigIndex);

                Assert.AreEqual(
                    expected[i],
                    cases[i].Prefix);
            }

            Assert.AreEqual(
                0,
                LegacyCharacterRigCore.ResolveFamilyForJob(
                    0,
                    0));

            Assert.AreEqual(
                1,
                LegacyCharacterRigCore.ResolveFamilyForJob(
                    0,
                    2));

            Assert.AreEqual(
                2,
                LegacyCharacterRigCore.ResolveFamilyForJob(
                    2,
                    3));

            Assert.AreEqual(
                3,
                LegacyCharacterRigCore.ResolveFamilyForJob(
                    2,
                    5));

            Assert.IsFalse(
                LegacyCharacterRigCore.IsJobAllowed(
                    0,
                    2));

            Assert.Throws<ArgumentException>(
                () => new CharacterSummaryCore(
                    777,
                    "Invalid",
                    1,
                    0,
                    0,
                    2,
                    0,
                    0,
                    0,
                    0,
                    CharacterDifficultyMode.Basic));
        }

        [Test]
        public void CanonicalJobOrderMatchesPs0032SData()
        {
            string[] expected =
            {
                "Fighter",
                "Defender",
                "Ranger",
                "Archer",
                "Mage",
                "Priest"
            };

            for (int job = 0;
                 job < expected.Length;
                 job++)
            {
                Assert.AreEqual(
                    expected[job],
                    LegacyCharacterRigCore.ResolveGlobalJobName(
                        job));
            }

            Assert.AreEqual(
                "Warrior",
                LegacyCharacterRigCore.ResolveDisplayJob(
                    2,
                    0));

            Assert.AreEqual(
                "Guardian",
                LegacyCharacterRigCore.ResolveDisplayJob(
                    2,
                    1));

            Assert.AreEqual(
                "Assassin",
                LegacyCharacterRigCore.ResolveDisplayJob(
                    3,
                    2));

            Assert.AreEqual(
                "Hunter",
                LegacyCharacterRigCore.ResolveDisplayJob(
                    2,
                    3));

            Assert.AreEqual(
                "Pagan",
                LegacyCharacterRigCore.ResolveDisplayJob(
                    3,
                    4));

            Assert.AreEqual(
                "Oracle",
                LegacyCharacterRigCore.ResolveDisplayJob(
                    3,
                    5));
        }

    }
}
