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

            var duel = new DuelCore();
            duel.Request(1, 2);
            Check(duel.Accept(3.0), "duel request accepted");
            duel.Tick(3.0);
            Check(duel.Phase == DuelPhase.Active, "duel countdown reaches active");
            Check(duel.Finish(2) && duel.WinnerId == 2, "duel records valid winner");

            var inventory = new InventoryCore(3);
            Check(inventory.Add(1001, 150, 99) == 0 && inventory.CountItem(1001) == 150, "inventory stacks across slots");
            Check(inventory.Remove(1001, 51) && inventory.CountItem(1001) == 99, "inventory removes exact quantity");
            var warehouse = new WarehouseCore(20);
            Check(warehouse.DepositGold(1000) && warehouse.WithdrawGold(250) && warehouse.Gold == 750, "warehouse gold is conserved");

            var stats = new StatsCore();
            stats.SetBase(StatKind.Strength, 10);
            stats.SetModifier("equipment", new System.Collections.Generic.Dictionary<StatKind, int> { [StatKind.Strength] = 7 });
            Check(stats.Get(StatKind.Strength) == 17, "stats combine base and modifiers");
            stats.RemoveModifier("equipment");
            Check(stats.Get(StatKind.Strength) == 10, "stat modifier removal restores base");

            var buffs = new BuffCore();
            buffs.Apply(50, 1, 10.0, 5.0);
            buffs.Tick(14.9);
            Check(buffs.Active.ContainsKey(50), "buff remains before expiration");
            buffs.Tick(15.0);
            Check(!buffs.Active.ContainsKey(50), "buff expires deterministically");

            var life = new LifeCore(1000);
            Check(life.Damage(1000) == 1000 && life.Dead, "death occurs at zero health");
            life.Rebirth(0.5);
            Check(!life.Dead && life.Health == 500, "rebirth restores configured health fraction");

            var quest = new QuestCore(77);
            quest.AddObjective(1, 2);
            quest.AddObjective(2, 1);
            Check(quest.Accept(), "quest accepted from available state");
            quest.Progress(1, 2);
            Check(quest.State == QuestState.Active, "quest waits for every objective");
            quest.Progress(2, 1);
            Check(quest.State == QuestState.Completed && quest.Reward(), "quest completes and rewards once objectives finish");

            var shopInventory = new InventoryCore(5);
            var shop = new ShopCore();
            shop.SetPrice(200, 100, 50);
            long shopGold = 1000;
            Check(shop.TryBuy(shopInventory, 200, 3, 99, ref shopGold) && shopGold == 700, "shop buys accepted quantity at configured price");
            Check(shop.TrySell(shopInventory, 200, 1, ref shopGold) && shopGold == 750, "shop sell returns configured value");

            var gate = new GatekeeperCore();
            gate.Add(new GatekeeperDestination { DestinationId = 3, MapId = 42, MinimumLevel = 10, Cost = 200, X = 1, Y = 2, Z = 3 });
            long travelGold = 500;
            Check(!gate.TryResolve(3, 9, ref travelGold, out _), "gatekeeper enforces level requirement");
            Check(gate.TryResolve(3, 10, ref travelGold, out GatekeeperDestination destination) && destination.MapId == 42 && travelGold == 300, "gatekeeper resolves data-driven destination and cost");

            int upgrade = 0;
            long upgradeGold = 1000;
            var blacksmith = new BlacksmithCore(new BlacksmithUpgradeProfile
            {
                MaxLevel = 5,
                CostForNextLevel = next => next * 100,
                SuccessProbabilityForNextLevel = next => 0.75
            });
            Check(blacksmith.TryUpgrade(ref upgrade, ref upgradeGold, 0.5) && upgrade == 1 && upgradeGold == 900, "blacksmith consumes configured cost and succeeds by supplied roll");
            Check(!blacksmith.TryUpgrade(ref upgrade, ref upgradeGold, 0.9) && upgrade == 1 && upgradeGold == 700, "blacksmith failure keeps level while preserving paid cost semantics");

            var mana = new ResourcePoolCore(100);
            var skills = new SkillCore();
            skills.Learn(new SkillDefinitionCore(300, 2, 20, 0.10, 0.20, 1.0, true));
            Check(skills.TryCast(300, 10, mana) && mana.Current == 80, "skill starts only with resource and required target");
            skills.Tick(0.11);
            Check(skills.LastCast != null && skills.LastCast.TargetId == 10 && skills.LastCast.Rank == 2, "skill cast locks original target and rank");
            skills.Tick(0.20);
            Check(skills.Phase == SkillCastPhase.Idle && !skills.TryCast(300, 10, mana), "skill cooldown blocks immediate recast");
            skills.Tick(0.70);
            Check(skills.TryCast(300, 10, mana), "skill becomes available after configured cooldown");

            var friends = new FriendsCore();
            Check(friends.ReceiveRequest(8) && friends.AcceptIncoming(8, true), "friend request acceptance creates friend");
            Check(friends.Friends[8].Online && friends.SetOnline(8, false) && !friends.Friends[8].Online, "friend online state updates independently");

            var guild = new GuildCore("ParityGuild", 20);
            Check(guild.Create(1) && guild.AddMember(1, 2) && guild.SetOfficer(1, 2, true), "guild leader adds and promotes officer");
            Check(guild.AddMember(2, 3), "guild officer can add member");
            Check(guild.TransferLeadership(1, 2) && guild.LeaderId == 2, "guild leadership transfer is deterministic");

            var flow = new ClientFlowCore();
            Check(flow.ReadyForLogin() && flow.BeginConnect() && flow.LoginAccepted(), "client boot/login/connect flow advances");
            Check(flow.SelectServer(1), "server selection advances to character select");
            Check(flow.SetCharacterList(new[] { new CharacterSummaryCore(100, "Parity", 80, 0) }), "character list accepts unique slots and ids");
            Check(flow.EnterCharacter(100) && flow.WorldAccepted() && flow.State == ClientFlowState.InWorld, "character enters world only after explicit world acceptance");

            var lootInventory = new InventoryCore(2);
            var loot = new LootCore();
            loot.Spawn(new LootDropCore(1, 500, 2, 7));
            Check(!loot.TryCollect(1, 8, lootInventory, 99), "reserved loot rejects another player");
            Check(loot.TryCollect(1, 7, lootInventory, 99) && lootInventory.CountItem(500) == 2, "reserved player collects loot atomically");

            var npc = new NpcInteractionCore();
            npc.Register(90, NpcServiceKind.Shop | NpcServiceKind.Blacksmith);
            Check(npc.Open(90) && npc.Supports(NpcServiceKind.Shop) && !npc.Supports(NpcServiceKind.Warehouse), "NPC exposes only registered services");

            Check(NpcServiceResolverCore.Resolve(1, false) == NpcServiceKind.Shop,
                "NpcQuest Merchant group maps to shop service");
            Check(NpcServiceResolverCore.Resolve(2, false) == NpcServiceKind.Gatekeeper,
                "NpcQuest GateKeeper group maps to gatekeeper service");
            Check(NpcServiceResolverCore.Resolve(3, true) ==
                  (NpcServiceKind.Blacksmith | NpcServiceKind.Quest),
                "blacksmith keeps quest service when quest links exist");
            Check(NpcServiceResolverCore.Resolve(6, true) ==
                  (NpcServiceKind.Warehouse | NpcServiceKind.Quest),
                "warehouse keeps quest service when quest links exist");

            var lightPortal = new PortalTravelCore(
                0, 1, 20, 30, 18, 100, 10, 200);
            Check(lightPortal.IsOpenByDefault &&
                  lightPortal.CanEnter(20, 1, false) &&
                  lightPortal.CanEnter(30, 1, false) &&
                  !lightPortal.CanEnter(19, 1, false) &&
                  !lightPortal.CanEnter(31, 1, false) &&
                  !lightPortal.CanEnter(25, 2, false),
                "light portal enforces inclusive level and faction rules");

            var bossPortal = new PortalTravelCore(
                0, 7, 1, 80, 42, 500, 20, 500);
            Check(bossPortal.IsBossActivatedPortal &&
                  !bossPortal.IsOpenByDefault &&
                  !bossPortal.CanEnter(50, 1, false) &&
                  bossPortal.CanEnter(50, 1, true) &&
                  bossPortal.CanEnter(50, 2, true),
                "boss portal stays closed until activated and then permits both factions");

            var weather = new WeatherCore();
            weather.TransitionTo(WeatherKindCore.Rain, 2.0);
            weather.Tick(1.0);
            Check(weather.Current == WeatherKindCore.Clear && Math.Abs(weather.Blend - 0.5) < 0.0001, "weather transition reports intermediate blend");
            weather.Tick(1.0);
            Check(weather.Current == WeatherKindCore.Rain && weather.Blend == 1.0, "weather transition completes at target");

            ClientCoordinateCore.ResolveCameraRelative(0, 1, 90, out double cameraWorldX, out double cameraWorldZ);
            Check(Math.Abs(cameraWorldX + 1.0) < 0.000001 && Math.Abs(cameraWorldZ) < 0.000001,
                "camera-relative forward at +90 degrees moves toward negative X");

            var flightTransition = new FlightTransitionCore();
            flightTransition.SetWingsEquipped(true);
            Check(!flightTransition.ManualRequested && flightTransition.Phase == ClientFlightPhase.Grounded,
                "equipping wings does not auto-start flight transition");
            Check(flightTransition.ToggleManualFlight() && flightTransition.Phase == ClientFlightPhase.TakingOff,
                "manual flight request enters takeoff");
            flightTransition.Tick(FlightTransitionCore.DefaultTakeoffSeconds);
            Check(flightTransition.Phase == ClientFlightPhase.Hover,
                "takeoff settles into hover without movement");
            flightTransition.SetMoving(true);
            Check(flightTransition.Phase == ClientFlightPhase.Flight,
                "movement changes hover to flight");
            flightTransition.SetCombatGuard(true);
            Check(flightTransition.BeginCombatDescent() &&
                  flightTransition.Phase == ClientFlightPhase.CombatDescending,
                "air attack requests combat descent");
            flightTransition.Tick(FlightTransitionCore.DefaultCombatDescentSeconds);
            Check(flightTransition.Phase == ClientFlightPhase.Grounded &&
                  flightTransition.ManualRequested,
                "combat descent reaches ground in recovered short window and preserves flight intent");
            flightTransition.SetCombatGuard(false);
            Check(flightTransition.Phase == ClientFlightPhase.TakingOff,
                "flight resumes only after combat guard clears");
            flightTransition.Tick(FlightTransitionCore.DefaultTakeoffSeconds);
            Check(flightTransition.Phase == ClientFlightPhase.Flight,
                "resumed flight returns to moving flight state");

            byte[] identicalFrame =
            {
                0, 0, 0,
                255, 255, 255,
                64, 128, 192,
                12, 34, 56
            };
            VisualMetricResult identicalMetrics = VisualMetricCore.CompareRgb24(
                identicalFrame,
                identicalFrame);
            Check(identicalMetrics.IsExact &&
                  double.IsPositiveInfinity(identicalMetrics.Psnr) &&
                  Math.Abs(identicalMetrics.Ssim - 1.0) < 0.0000001,
                "identical visual frames produce exact parity metrics");

            byte[] referenceFrame = { 0, 0, 0, 255, 255, 255 };
            byte[] changedFrame = { 255, 255, 255, 255, 255, 255 };
            VisualMetricResult changedMetrics = VisualMetricCore.CompareRgb24(
                referenceFrame,
                changedFrame);
            Check(changedMetrics.Mae > 0.0 &&
                  changedMetrics.Rmse > 0.0 &&
                  changedMetrics.Ssim < 1.0,
                "visual metrics detect changed frames");

            var deferredTargetCombat = new CombatCore();
            deferredTargetCombat.RegisterTarget(20, 1000);
            deferredTargetCombat.RegisterTarget(21, 1000);
            deferredTargetCombat.SelectTarget(20);
            int capturedDeferredTarget = deferredTargetCombat.SelectedTargetId.Value;
            deferredTargetCombat.SelectTarget(21);
            Check(deferredTargetCombat.RequestAttackAt(
                    capturedDeferredTarget,
                    125,
                    0.01,
                    0.02,
                    0.01),
                "deferred attack starts against captured target");
            deferredTargetCombat.Tick(0.03);
            Check(deferredTargetCombat.Targets[20].Health == 875 &&
                  deferredTargetCombat.Targets[21].Health == 1000 &&
                  deferredTargetCombat.SelectedTargetId == 21 &&
                  deferredTargetCombat.LastHitTargetId == 20,
                "deferred attack keeps original target even after selection changes");

            foreach (int fps in new[] { 30, 60, 144 })
            {
                var timedFlight = new FlightTransitionCore();
                timedFlight.SetWingsEquipped(true);
                timedFlight.ToggleManualFlight();
                timedFlight.Tick(FlightTransitionCore.DefaultTakeoffSeconds);
                timedFlight.SetMoving(true);
                timedFlight.SetCombatGuard(true);
                timedFlight.BeginCombatDescent();

                double frame = 1.0 / fps;
                double elapsed = 0.0;
                while (timedFlight.Phase == ClientFlightPhase.CombatDescending &&
                       elapsed < 1.0)
                {
                    timedFlight.Tick(frame);
                    elapsed += frame;
                }

                Check(timedFlight.Phase == ClientFlightPhase.Grounded &&
                      elapsed + 0.0000001 >= FlightTransitionCore.DefaultCombatDescentSeconds &&
                      elapsed <= FlightTransitionCore.DefaultCombatDescentSeconds + frame + 0.0000001,
                    "combat descent timing remains bounded at " + fps + " Hz");
            }

            Check(LegacyWingPoseCore.Count == 48,
                "canonical wing table contains 4x6x2 profiles");
            LegacyWingPose canonicalWing =
                LegacyWingPoseCore.Resolve(0, 0, 0);
            Check(canonicalWing.BoneIndex == 4 &&
                  Math.Abs(canonicalWing.RotX - 170.0) < 0.0000001 &&
                  Math.Abs(canonicalWing.RotZ - 90.0) < 0.0000001 &&
                  Math.Abs(canonicalWing.UpDown - 0.05) < 0.0000001 &&
                  Math.Abs(canonicalWing.FrontBack + 0.18) < 0.0000001,
                "canonical human fighter wing pose matches supplied WingPosition.xml");

            bool allWingProfilesPresent = true;
            for (int family = 0; family < 4; family++)
            for (int job = 0; job < 6; job++)
            for (int sex = 0; sex < 2; sex++)
            {
                if (!LegacyWingPoseCore.TryResolve(
                        family,
                        job,
                        sex,
                        out LegacyWingPose pose) ||
                    pose.BoneIndex != 4 ||
                    Math.Abs(pose.RotZ - 90.0) > 0.0000001)
                {
                    allWingProfilesPresent = false;
                }
            }
            Check(allWingProfilesPresent,
                "all canonical wing profiles resolve with verified bone and rotation");

            LegacyWorldPopulation offlineMap0 =
                LegacyWorldPopulationCore.Get(0);
            Check(LegacyWorldPopulationCore.LoginPort == 30800 &&
                  LegacyWorldPopulationCore.All.Count == 13,
                "offline ps0032 baseline exposes captured login port and maps");
            Check(offlineMap0.Portals == 11 &&
                  offlineMap0.Npcs == 141 &&
                  offlineMap0.MobAreas == 509 &&
                  offlineMap0.Mobs == 1330 &&
                  offlineMap0.Obelisks == 1,
                "offline map 0 population matches both captured sessions");

            Check(Math.Abs(LegacyTerrainHeightCore.Decode(11268) - 25.36) < 0.000001,
                "WLD raw height 11268 resolves to observed map0 world Y 25.36");
            Check(Math.Abs(LegacyTerrainHeightCore.Decode(10689) - 13.78) < 0.000001,
                "WLD raw height 10689 resolves to observed map0 world Y 13.78");
            Check(Math.Abs(LegacyTerrainHeightCore.Decode(11197) - 23.94) < 0.000001,
                "WLD raw height 11197 resolves to observed map0 world Y 23.94");
            Check(LegacyTerrainHeightCore.ResolutionForMapSize(2048) == 1025,
                "map 2048 uses canonical 1025x1025 terrain samples");
            Check(LegacyTerrainHeightCore.WorldToSampleIndex(184.4588165283203, 2048) == 92,
                "world X maps to the same terrain sample used by canonical NPC evidence");

            var characterFlow = new ClientFlowCore();
            characterFlow.ReadyForLogin();
            characterFlow.BeginConnect();
            characterFlow.LoginAccepted();
            characterFlow.SelectServer(1);
            characterFlow.SetCharacterList(
                new[]
                {
                    new CharacterSummaryCore(
                        900,
                        "Existing",
                        40,
                        0,
                        0,
                        1,
                        1,
                        2,
                        3,
                        0,
                        CharacterDifficultyMode.Basic)
                });

            Check(characterFlow.BeginCharacterCreate(1) &&
                  characterFlow.State == ClientFlowState.CharacterCreate &&
                  characterFlow.CharacterCreateSlot == 1,
                "character select opens an empty creation slot");

            var createRequest = new CharacterCreationRequestCore(
                "NewHero",
                1,
                3,
                5,
                0,
                2,
                1,
                CharacterDifficultyMode.Ultimate);

            var createdCharacter = new CharacterSummaryCore(
                901,
                createRequest.Name,
                1,
                createRequest.Slot,
                createRequest.Family,
                createRequest.Job,
                createRequest.Sex,
                createRequest.Face,
                createRequest.Hair,
                0,
                createRequest.Mode);

            Check(characterFlow.CharacterCreated(createdCharacter) &&
                  characterFlow.State == ClientFlowState.CharacterSelect &&
                  characterFlow.Characters.Count == 2,
                "character creation returns to select with preserved appearance");

            Check(characterFlow.CharacterDeleted(900) &&
                  characterFlow.Characters.Count == 1 &&
                  characterFlow.Characters[0].CharacterId == 901,
                "character deletion updates deterministic character slots");

            Check(
                NativeVisualReferenceCore.All.Count == 8,
                "native visual reference suite contains eight verified scenarios");

            NativeVisualReference nativeCharacterEditor =
                NativeVisualReferenceCore.Get(
                    "character-editor");

            Check(
                nativeCharacterEditor.Width == 1024 &&
                nativeCharacterEditor.Height == 768 &&
                nativeCharacterEditor.Sha256 ==
                    "2fd2807d305f5ae589f30232ac31c52a12f9caef66ed1b674ca6405a607f5549",
                "native character editor visual reference is pinned");

            Check(
                nativeCharacterEditor.NativeCropX == 3 &&
                nativeCharacterEditor.NativeCropY == 26 &&
                nativeCharacterEditor.NativeCropWidth == 1021 &&
                nativeCharacterEditor.NativeCropHeight == 739,
                "native Windows client crop calibration is pinned");

            NativeVisualReference nativeWorldLoaded =
                NativeVisualReferenceCore.Get(
                    "world-loaded");

            Check(
                nativeWorldLoaded.Width == 1024 &&
                nativeWorldLoaded.Height == 768 &&
                nativeWorldLoaded.Sha256 ==
                    "c19cb5f06154bf6eacb029b08be7f6762bd9a002c044ff170363a9dfeadee6a0",
                "native world-loaded visual reference is pinned");

            Check(
                NativeVisualReferenceCore.OriginalClientSha256 ==
                    "509c4a8fbe4d5292961fdfb6d1045795a7bb5970fcf2560fd1070aee18273c2d" &&
                NativeVisualReferenceCore.DiagnosticCaptureClientSha256 ==
                    "32232a8e3e176c32ac75ad357d1f70ccf8ccadc7f223384866afdd2c9e6282df",
                "native visual suite records original and diagnostic client identities");

            Check(
                Math.Abs(
                    LegacyVaniAnimationCore.FrameDurationSeconds(66) -
                    0.066) <
                0.0000001,
                "VANI 66 timing field resolves to 66 ms per frame");

            Check(
                LegacyVaniAnimationCore.ResolveFrameIndex(
                    0.065,
                    16,
                    66) == 0 &&
                LegacyVaniAnimationCore.ResolveFrameIndex(
                    0.066,
                    16,
                    66) == 1 &&
                LegacyVaniAnimationCore.ResolveFrameIndex(
                    1.056,
                    16,
                    66) == 0,
                "VANI frame selection is deterministic across cycle boundaries");

            Check(
                LegacyVaniAnimationCore.ResolveFrameIndex(
                    0.999,
                    61,
                    33) == 30 &&
                LegacyVaniAnimationCore.ResolveFrameIndex(
                    1.0,
                    21,
                    100) == 10,
                "VANI observed 33/100 ms intervals map to expected frames");

            string[] recoveredRigPrefixes =
            {
                LegacyCharacterRigCore.Resolve(0, 0, 0).Prefix,
                LegacyCharacterRigCore.Resolve(0, 5, 0).Prefix,
                LegacyCharacterRigCore.Resolve(0, 0, 1).Prefix,
                LegacyCharacterRigCore.Resolve(0, 5, 1).Prefix,
                LegacyCharacterRigCore.Resolve(1, 2, 0).Prefix,
                LegacyCharacterRigCore.Resolve(1, 4, 0).Prefix,
                LegacyCharacterRigCore.Resolve(1, 2, 1).Prefix,
                LegacyCharacterRigCore.Resolve(1, 4, 1).Prefix,
                LegacyCharacterRigCore.Resolve(2, 0, 0).Prefix,
                LegacyCharacterRigCore.Resolve(2, 3, 0).Prefix,
                LegacyCharacterRigCore.Resolve(2, 0, 1).Prefix,
                LegacyCharacterRigCore.Resolve(2, 3, 1).Prefix,
                LegacyCharacterRigCore.Resolve(3, 2, 0).Prefix,
                LegacyCharacterRigCore.Resolve(3, 4, 0).Prefix,
                LegacyCharacterRigCore.Resolve(3, 2, 1).Prefix,
                LegacyCharacterRigCore.Resolve(3, 4, 1).Prefix
            };

            Check(
                string.Join(",", recoveredRigPrefixes) ==
                    "humf,humm,huwf,huwm,elmr,elmm,elwr,elwm,demf,demr,dewf,dewr,vimr,vimm,viwr,viwm",
                "ps0032 x86 rig selector resolves all sixteen canonical prefixes");

            Check(
                LegacyCharacterRigCore.Resolve(0, 0, 0).NativeRigIndex == 0 &&
                LegacyCharacterRigCore.Resolve(0, 5, 0).NativeRigIndex == 1 &&
                LegacyCharacterRigCore.Resolve(3, 2, 1).NativeRigIndex == 14 &&
                LegacyCharacterRigCore.Resolve(3, 4, 1).NativeRigIndex == 15,
                "ps0032 rig index follows archetype + 2*sex + 4*family");

            Check(
                LegacyCharacterRigCore.ResolveFamilyForJob(0, 2) == 1 &&
                LegacyCharacterRigCore.ResolveFamilyForJob(2, 5) == 3 &&
                LegacyCharacterRigCore.ResolveGlobalJobName(2) == "Ranger" &&
                LegacyCharacterRigCore.ResolveGlobalJobName(5) == "Priest",
                "character make maps class choice to native race and canonical SData job order");

            Console.WriteLine("PARITY HARNESS OK: " + _count + " checks");
        }
    }
}
