using System;
using System.Collections.Generic;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Kit;

namespace Kalyeah.Sim.Tests
{
    /// <summary>
    /// Deterministic 30 Hz NUnit coverage for Barangay Living World, Neutrals, Vendors,
    /// Tall-Cover Stealth, Shallow Water, Half-Time Side Swap, and MapLayout asymmetric data
    /// (docs/SPEC.md §6, docs/MAPS.md, docs/KITS.md L36, docs/GRAPHICS.md).
    /// </summary>
    public class BarangayNeutralsTests
    {
        static OrderedDict<int, PlayerState> MakePlayers(params (int id, int team, string kid, Vec2 pos, int hp)[] specs)
        {
            var dict = new OrderedDict<int, PlayerState>();
            for (int i = 0; i < specs.Length; i++)
            {
                var s = specs[i];
                var p = new PlayerState
                {
                    Id = s.id,
                    Team = s.team,
                    CharacterId = s.kid,
                    Position = s.pos,
                    SpawnPosition = s.pos,
                    Hp = s.hp,
                    Radius = 0.45,
                    Elixir = 2.0,
                    Stamina = 100.0,
                    UltCharge = 85.0,
                };
                p.WeaponCooldowns = new List<double> { 4.0, 7.0 };
                dict[s.id] = p;
            }
            return dict;
        }

        static void Advance(BarangayNeutrals neutrals, OrderedDict<int, PlayerState> players, GodotRng rng, double seconds)
        {
            int ticks = GMath.Ceili(seconds / DT);
            for (int i = 0; i < ticks; i++)
                neutrals.Step(DT, players, rng);
        }

        static double PointToSegmentDistance(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            double lenSq = ab.LengthSquared();
            double t = lenSq > 1e-9 ? GMath.Clampf((p - a).Dot(ab) / lenSq, 0.0, 1.0) : 0.0;
            Vec2 closest = a + ab * t;
            return p.DistanceTo(closest);
        }

        [Test]
        public void MapLayout_BarangayDataLoadsWithZeroUnmappedKeysAndValidAsymmetricGeometry()
        {
            SimData fresh = SimData.Load(GoldenMatchTests.DataDir);
            MapLayout layout = fresh.MapLayout;
            CollectionAssert.IsEmpty(fresh.UnmappedKeys, "rules__map_layout.json must have zero unmapped keys");

            // 6 CODM-style callout regions
            IReadOnlyList<CalloutRegion> callouts = layout.GetCalloutRegions();
            Assert.AreEqual(6, callouts.Count);
            string[] expectedNames = { "HALL", "COURT", "BRIDGE", "CHAPEL", "SARI-SARI", "CREEK" };
            for (int i = 0; i < expectedNames.Length; i++)
                Assert.AreEqual(expectedNames[i], callouts[i].Name);
            Assert.AreEqual("HALL", layout.CalloutAt(layout.UptownBaseCenter));
            Assert.AreEqual("BRIDGE", layout.CalloutAt(Vec2.Zero));
            Assert.AreEqual("CHAPEL", layout.CalloutAt(layout.RiversideBaseCenter));

            // Asymmetric bases and ~15 m side-offset jails clear of obstacles
            double uptownJailDist = layout.UptownBaseCenter.DistanceTo(layout.UptownJailCenter);
            double riversideJailDist = layout.RiversideBaseCenter.DistanceTo(layout.RiversideJailCenter);
            Assert.That(uptownJailDist, Is.InRange(13.0, 18.0), "Uptown jail sits ~15 m off to the side of the Hall steps");
            Assert.That(riversideJailDist, Is.InRange(13.0, 18.0), "Riverside jail sits ~15 m off to the side of the Chapel yard");
            Assert.IsTrue(layout.InArena(layout.UptownJailCenter));
            Assert.IsTrue(layout.InArena(layout.RiversideJailCenter));

            List<Rect2> solid = layout.ObstacleRects();
            for (int i = 0; i < solid.Count; i++)
            {
                Assert.IsFalse(solid[i].HasPoint(layout.UptownJailCenter), $"Uptown jail clear of obstacle {i}");
                Assert.IsFalse(solid[i].HasPoint(layout.RiversideJailCenter), $"Riverside jail clear of obstacle {i}");
            }

            // 3 climb spots (Uptown backboard, Riverside sari-sari roof, Mid bridge jeepney)
            IReadOnlyList<ClimbSpot> climbs = layout.GetClimbSpots();
            Assert.AreEqual(3, climbs.Count);
            Assert.IsTrue(layout.TryGetClimbSpotAt(climbs[0].Position, out ClimbSpot foundClimb));
            Assert.AreEqual("backboard_stand", foundClimb.Id);

            // 2 equidistant dry obstacle-free vendors
            IReadOnlyList<VendorSpot> vendors = layout.GetVendorSpots();
            Assert.AreEqual(2, vendors.Count);
            Assert.AreEqual("mang_taho", vendors[0].Id);
            Assert.AreEqual("fishball_cart", vendors[1].Id);
            double upMin = Math.Min(
                layout.UptownBaseCenter.DistanceTo(vendors[0].Position),
                layout.UptownBaseCenter.DistanceTo(vendors[1].Position));
            double rvMin = Math.Min(
                layout.RiversideBaseCenter.DistanceTo(vendors[0].Position),
                layout.RiversideBaseCenter.DistanceTo(vendors[1].Position));
            Assert.AreEqual(upMin, rvMin, 0.05, "Both halves have identical distance to their nearer vendor");
            for (int i = 0; i < vendors.Count; i++)
            {
                Assert.IsFalse(layout.IsInShallowWater(vendors[i].Position), $"Vendor {vendors[i].Id} stands on dry bank");
                for (int o = 0; o < solid.Count; o++)
                    Assert.IsFalse(solid[o].HasPoint(vendors[i].Position), $"Vendor {vendors[i].Id} clear of obstacle {o}");
            }

            // Selecta loop waypoints & Dragon/Baron spawns inside arena and clear of buildings
            IReadOnlyList<Vec2> loop = layout.GetSelectaWaypoints();
            Assert.GreaterOrEqual(loop.Count, 8);
            for (int i = 0; i < loop.Count; i++)
            {
                Assert.IsTrue(layout.InArena(loop[i]), $"Selecta waypoint {i} inside arena");
                for (int o = 0; o < solid.Count; o++)
                    Assert.IsFalse(solid[o].HasPoint(loop[i]), $"Selecta waypoint {i} clear of obstacle {o}");
            }

            // Parked bridge jeepney climb spot sits dry, clear of obstacles, and >= 4 m clear of every Selecta loop segment
            ClimbSpot bridgeJeep = climbs[2];
            Assert.AreEqual("bridge_jeepney", bridgeJeep.Id);
            Assert.IsFalse(layout.IsInShallowWater(bridgeJeep.Position), "bridge_jeepney stands on dry bridge apron");
            for (int o = 0; o < solid.Count; o++)
                Assert.IsFalse(solid[o].HasPoint(bridgeJeep.Position), $"bridge_jeepney clear of obstacle {o}");
            for (int i = 0; i < loop.Count; i++)
            {
                Vec2 a = loop[i];
                Vec2 b = loop[(i + 1) % loop.Count];
                double dist = PointToSegmentDistance(bridgeJeep.Position, a, b);
                Assert.GreaterOrEqual(dist, 4.0, $"bridge_jeepney must keep >= 4 m clearance from Selecta segment {i} (got {dist:F2} m)");
            }

            Assert.AreEqual(
                layout.UptownBaseCenter.DistanceTo(layout.DragonSpawn),
                layout.RiversideBaseCenter.DistanceTo(layout.DragonSpawn),
                0.05,
                "Dragon sandbar sits equidistant between both bases");
            Assert.IsTrue(layout.InArena(layout.BaronSpawn));
            Assert.IsFalse(layout.IsInShallowWater(layout.BaronSpawn));
        }

        [Test]
        public void Vendors_Untargetable_HealOverEatingWindowWithSlow_AndRestock30s()
        {
            var neutrals = new BarangayNeutrals(Layout, Rules());
            VendorState taho = neutrals.Vendors[0];
            Assert.IsTrue(taho.IsReady);
            Assert.IsFalse(neutrals.TryDamageVendor(taho.Id, 50), "Vendors are untargetable");

            var players = MakePlayers(
                (1, 0, "narda", taho.Position, 50),
                (2, 1, "jumong", taho.Position + new Vec2(0.5, 0.0), 40));

            int usedCount = 0;
            int restockedCount = 0;
            neutrals.VendorUsed += (pid, vid) => { if (pid == 1 && vid == taho.Id) usedCount++; };
            neutrals.VendorRestocked += vid => { if (vid == taho.Id) restockedCount++; };

            // First tick: player 1 claims Mang Taho; vendor goes empty immediately so player 2 cannot double-dip
            neutrals.Step(DT, players, new GodotRng(7));
            Assert.AreEqual(1, usedCount);
            Assert.IsTrue(taho.IsEmpty);
            Assert.IsTrue(neutrals.IsPlayerEating(1));
            Assert.IsFalse(neutrals.IsPlayerEating(2));
            Assert.AreEqual(0.70, neutrals.GetEatingSpeedMultiplier(1), 0.001, "Eater moves 30% slower while eating");
            Assert.AreEqual(0.70, neutrals.GetPlayerMoveSpeedMultiplier(1, 0, "narda", taho.Position), 0.001);

            // Halfway through 2.0 s eating window (~1.0 s): ~+20 HP healed, still slowed
            Advance(neutrals, players, new GodotRng(8), 1.0);
            Assert.That(players[1].Hp, Is.InRange(68, 72));
            Assert.IsTrue(neutrals.IsPlayerEating(1));
            Assert.AreEqual(40, players[2].Hp, "Empty vendor does not heal second kid");

            // End of 2.0 s eating window: full +40 HP healed (50 -> 90), eating slow ends
            Advance(neutrals, players, new GodotRng(9), 1.1);
            Assert.AreEqual(90, players[1].Hp);
            Assert.IsFalse(neutrals.IsPlayerEating(1));
            Assert.AreEqual(1.0, neutrals.GetEatingSpeedMultiplier(1), 0.001);
            Assert.IsTrue(taho.IsEmpty, "Still restocking until 30 s elapsed");

            // Move both kids away while finishing the 30 s restock timer
            players[1].Position = new Vec2(0.0, 50.0);
            players[2].Position = new Vec2(0.0, -50.0);
            Advance(neutrals, players, new GodotRng(10), 28.5);
            Assert.IsTrue(taho.IsReady, "Vendor restocks cleanly after 30 s");
            Assert.AreEqual(1, restockedCount);
        }

        [Test]
        public void SelectaBike_PatrolsRiverRoadLoop_ClaimsTeamSpeedAndWaterBuff_AndRespawns()
        {
            var neutrals = new BarangayNeutrals(Layout, Rules());
            var rng = new GodotRng(101);
            var players = MakePlayers(
                (1, 0, "narda", new Vec2(0.0, 60.0), 65),
                (2, 0, "jopay", new Vec2(10.0, 60.0), 100));

            Assert.IsFalse(neutrals.Selecta.Active, "Selecta bike spawns at 0:30");
            Assert.IsFalse(neutrals.TryDamageSelecta(30), "Selecta bike is untargetable");

            Advance(neutrals, players, rng, 30.1);
            Assert.IsTrue(neutrals.Selecta.Active);
            Vec2 startPos = neutrals.Selecta.Position;

            // Patrol moves along waypoints inside the arena
            Advance(neutrals, players, rng, 4.0);
            Assert.Greater(neutrals.Selecta.Position.DistanceTo(startPos), 8.0, "Selecta bike patrols continuously");
            Assert.IsTrue(Layout.InArena(neutrals.Selecta.Position));

            // Player 1 touches the Selecta bike
            int claimedBy = -1;
            neutrals.SelectaClaimed += (pid, team) => claimedBy = pid;
            players[1].Position = neutrals.Selecta.Position;
            double allyWaterBefore = players[2].Elixir;
            neutrals.Step(DT, players, rng);

            Assert.AreEqual(1, claimedBy);
            Assert.IsFalse(neutrals.Selecta.Active);
            Assert.AreEqual(60.0, neutrals.Selecta.RespawnLeft, 0.05);
            Assert.AreEqual(85, players[1].Hp, "Catcher receives instant +20 HP heal");
            Assert.IsTrue(neutrals.GetTeamBuffs(0).HasSelecta);
            Assert.IsTrue(neutrals.HasFootAura(0));
            Assert.AreEqual(1.20, neutrals.GetPlayerMoveSpeedMultiplier(2, 0, "jopay", players[2].Position), 0.001, "Team gains +20% speed");
            Assert.Greater(neutrals.GetTeamWaterRegenBonus(0), 0.0);

            // Water/Elixir regenerates on teammates during the 10 s Selecta buff
            Advance(neutrals, players, rng, 2.0);
            Assert.Greater(players[2].Elixir, allyWaterBefore + 5.0);

            // Buff expires after 10 s, bike respawns after 60 s total
            Advance(neutrals, players, rng, 9.0);
            Assert.IsFalse(neutrals.GetTeamBuffs(0).HasSelecta);
            Assert.IsFalse(neutrals.HasFootAura(0));

            players[1].Position = new Vec2(0.0, 60.0);
            Advance(neutrals, players, rng, 50.0);
            Assert.IsTrue(neutrals.Selecta.Active, "Selecta bike respawns 60 s after claim");
        }

        [Test]
        public void CrocodileDragon_IdleToAngryToFlee_IgnoresCcAndReveals_GrantsTeamDamageBuff()
        {
            var neutrals = new BarangayNeutrals(Layout, Rules());
            var rng = new GodotRng(202);
            Vec2 crocSpot = neutrals.Dragon.HomePosition;
            var players = MakePlayers(
                (1, 0, "narda", crocSpot + new Vec2(0.0, 4.0), 100),
                (6, 1, "inggo", crocSpot + new Vec2(0.0, -4.0), 100));

            Assert.AreEqual(NeutralBossState.Dormant, neutrals.Dragon.State);
            Assert.IsFalse(neutrals.DamageNeutral(NeutralBossKind.Dragon, 20, 1, 0, true, rng), "Dormant dragon ignores damage");

            // Spawns at 60 s in calm Idle state
            Advance(neutrals, players, rng, 60.1);
            Assert.AreEqual(NeutralBossState.Idle, neutrals.Dragon.State);
            Assert.AreEqual(600, neutrals.Dragon.Hp);
            Assert.AreEqual(BarangayNeutrals.BossIdleScale, neutrals.Dragon.VisualScale, 0.01);

            // Ignores CC & reveals (docs/KITS.md L36)
            Assert.IsFalse(neutrals.TryApplyNeutralCcOrReveal(NeutralBossKind.Dragon, StatusEffects.Type.Stun));
            Assert.IsFalse(neutrals.TryApplyNeutralCcOrReveal(NeutralBossKind.Dragon, StatusEffects.Type.Slow));
            Assert.IsFalse(neutrals.DamageNeutral(NeutralBossKind.Dragon, 20, 1, 0, isDamageSkillOrStone: false, rng));
            Assert.AreEqual(NeutralBossState.Idle, neutrals.Dragon.State);
            Assert.AreEqual(600, neutrals.Dragon.Hp);

            // Damaged by stone -> becomes Angry, grows larger, retaliates lightly
            Assert.IsTrue(neutrals.DamageNeutral(NeutralBossKind.Dragon, 580, 1, 0, isDamageSkillOrStone: true, rng));
            Assert.AreEqual(NeutralBossState.Angry, neutrals.Dragon.State);
            Assert.AreEqual(20, neutrals.Dragon.Hp);

            Advance(neutrals, players, rng, 1.0);
            Assert.Greater(neutrals.Dragon.VisualScale, 1.35, "Angry Crocodile grows scarier");
            Assert.Less(players[1].Hp, 100, "Angry Crocodile retaliates lightly at attacker");

            // Team 1 steals last hit -> enters Fleeing (zero gore!), shrinks & runs off, awards Team 1 Dragon buff + foot aura
            int defeatedByTeam = -1;
            neutrals.NeutralBossDefeated += (kind, pid, team) => { if (kind == NeutralBossKind.Dragon) defeatedByTeam = team; };
            Assert.IsTrue(neutrals.DamageNeutral(NeutralBossKind.Dragon, 20, 6, 1, isDamageSkillOrStone: true, rng));
            Assert.AreEqual(0, neutrals.Dragon.Hp);
            Assert.AreEqual(NeutralBossState.Fleeing, neutrals.Dragon.State);
            Assert.AreEqual(1, defeatedByTeam, "Last hit steals Dragon buff");
            Assert.IsTrue(neutrals.GetTeamBuffs(1).HasDragon);
            Assert.IsTrue(neutrals.HasFootAura(1));
            Assert.IsFalse(neutrals.HasFootAura(0));
            Assert.AreEqual(1.08, neutrals.GetDamageMultiplier(1, "inggo"), 0.001);

            Advance(neutrals, players, rng, 1.0);
            Assert.Less(neutrals.Dragon.VisualScale, 0.90, "Fleeing Crocodile shrinks");
            Assert.Greater(neutrals.Dragon.Position.DistanceTo(crocSpot), 4.0, "Fleeing Crocodile runs off");

            // Finishes fleeing -> Dormant with 120 s respawn
            Advance(neutrals, players, rng, 2.0);
            Assert.AreEqual(NeutralBossState.Dormant, neutrals.Dragon.State);
            Assert.That(neutrals.Dragon.RespawnLeft, Is.InRange(118.0, 120.0));
        }

        [Test]
        public void PhilippineEagleBaron_SpawnsAt180s_GrantsRoleSpecificBuffs_AndRespawns()
        {
            var neutrals = new BarangayNeutrals(Layout, Rules());
            var rng = new GodotRng(303);
            var players = MakePlayers(
                (1, 0, "narda", neutrals.Baron.HomePosition + new Vec2(-4.0, 0.0), 100),
                (2, 0, "jopay", Vec2.Zero, 100),
                (3, 0, "kikay", Vec2.Zero, 100),
                (4, 0, "tere", Vec2.Zero, 100));

            Assert.AreEqual(NeutralBossState.Dormant, neutrals.Baron.State);
            Advance(neutrals, players, rng, 180.1);
            Assert.AreEqual(NeutralBossState.Idle, neutrals.Baron.State);

            Assert.IsTrue(neutrals.DamageNeutral(NeutralBossKind.Baron, 300, 1, 0, true, rng));
            Assert.AreEqual(NeutralBossState.Angry, neutrals.Baron.State);
            Assert.IsTrue(neutrals.DamageNeutral(NeutralBossKind.Baron, 300, 1, 0, true, rng));
            Assert.AreEqual(NeutralBossState.Fleeing, neutrals.Baron.State);
            Assert.IsTrue(neutrals.GetTeamBuffs(0).HasBaron);
            Assert.IsTrue(neutrals.HasFootAura(0));

            // Role-specific +8% Baron buffs (docs/SPEC.md §6)
            Assert.AreEqual(1.08, neutrals.GetDamageMultiplier(0, "narda"), 0.001, "Striker +8% damage");
            Assert.AreEqual(1.00, neutrals.GetDamageMultiplier(0, "jopay"), 0.001, "Defender gets mitigation, not damage");
            Assert.AreEqual(0.92, neutrals.GetDamageTakenMultiplier(0, "jopay"), 0.001, "Defender -8% damage taken");
            Assert.AreEqual(1.08, neutrals.GetPlayerMoveSpeedMultiplier(3, 0, "kikay", new Vec2(0.0, 20.0)), 0.001, "Seeker +8% speed");
            Assert.AreEqual(0.92, neutrals.GetCooldownMultiplier(0, "tere"), 0.001, "Controller -8% cooldowns");

            // Fleeing finishes into 180 s respawn timer
            Advance(neutrals, players, rng, 2.7);
            Assert.AreEqual(NeutralBossState.Dormant, neutrals.Baron.State);
            Assert.That(neutrals.Baron.RespawnLeft, Is.InRange(178.0, 180.0));
        }

        [Test]
        public void TallCoverStealth_HiddenBeyond6m_VisibleToAlliesWithin6mOrRevealed_AndBreakingActionEndsStealth()
        {
            var neutrals = new BarangayNeutrals(Layout, Rules());
            Rect2 tallZone = Layout.GetTallCoverZones()[0];
            Vec2 hiddenPos = tallZone.GetCenter();
            Vec2 farEnemy = hiddenPos + new Vec2(10.0, 0.0);
            Vec2 nearEnemy = hiddenPos + new Vec2(5.5, 0.0);
            Vec2 edgeEnemy = hiddenPos + new Vec2(6.0, 0.0);
            Vec2 openStreet = new Vec2(0.0, 30.0);

            Assert.IsTrue(neutrals.IsHiddenInTallCover(hiddenPos, farEnemy, revealed: false), "Hidden from enemy beyond 6 m");
            Assert.IsFalse(neutrals.IsHiddenInTallCover(hiddenPos, nearEnemy, revealed: false), "Visible to enemy within 6 m");
            Assert.IsFalse(neutrals.IsHiddenInTallCover(hiddenPos, edgeEnemy, revealed: false), "Visible at exact 6 m reveal radius");
            Assert.IsFalse(neutrals.IsHiddenInTallCover(hiddenPos, farEnemy, revealed: true), "Visible when revealed by Seeker");
            Assert.IsFalse(neutrals.IsHiddenInTallCover(hiddenPos, farEnemy, revealed: false, sameTeam: true, breakingAction: false), "Always visible to allies");
            Assert.IsFalse(neutrals.IsHiddenInTallCover(hiddenPos, farEnemy, revealed: false, sameTeam: false, breakingAction: true), "Attack/rescue/capture ends hiding");
            Assert.IsFalse(neutrals.IsHiddenInTallCover(openStreet, farEnemy, revealed: false), "Not hidden outside tall cover");
        }

        [Test]
        public void ShallowWater_SlowsOffBridgeAndMakesLoudFootsteps_DryOnAllThreeBridges()
        {
            var neutrals = new BarangayNeutrals(Layout, Rules());

            // Mid-river off-bridge (between west & center bridges)
            double riverX = -18.0;
            Vec2 inRiver = new Vec2(riverX, MapLayout.CanalZ(riverX));
            Assert.AreEqual(0.85, neutrals.GetTerrainSpeedMultiplier(inRiver), 0.001, "Shallow river slows by 15%");
            Assert.IsTrue(neutrals.IsLoudFootsteps(inRiver));

            // Dry on all 3 bridges (West, Center, East)
            double[] bridgeXs = { MapLayout.LaneWestX(0.0), MapLayout.LaneCenterX(0.0), MapLayout.LaneEastX(0.0) };
            for (int i = 0; i < bridgeXs.Length; i++)
            {
                Vec2 onBridge = new Vec2(bridgeXs[i], MapLayout.CanalZ(bridgeXs[i]));
                Assert.AreEqual(1.0, neutrals.GetTerrainSpeedMultiplier(onBridge), 0.001, $"Bridge {i} is dry");
                Assert.IsFalse(neutrals.IsLoudFootsteps(onBridge));
            }

            // Dry on regular street
            Vec2 street = new Vec2(0.0, 35.0);
            Assert.AreEqual(1.0, neutrals.GetTerrainSpeedMultiplier(street), 0.001);
            Assert.IsFalse(neutrals.IsLoudFootsteps(street));
        }

        [Test]
        public void HalfTimeSideSwap_PreservesScores_SwapsHalves_ResetsWaterAndUlt_Runs10sPhase()
        {
            Assert.AreEqual(2, HalfTimeSideSwap.HalfTimeRoundFor(3), "Blitz swaps after round 2");
            Assert.AreEqual(4, HalfTimeSideSwap.HalfTimeRoundFor(5), "Quick swaps after round 4");
            Assert.AreEqual(6, HalfTimeSideSwap.HalfTimeRoundFor(7), "Standard swaps after round 6");

            GameRules rules = Rules();
            var neutrals = new BarangayNeutrals(Layout, rules);
            var score = new MatchScore(firstTo: 5);
            score.AwardPoint(0);
            score.AwardPoint(0);
            score.AwardPoint(1);
            score.AwardPoint(1); // 2-2 after 4 completed rounds

            var players = MakePlayers(
                (1, 0, "narda", new Vec2(5.0, 30.0), 40),
                (6, 1, "inggo", new Vec2(-5.0, -30.0), 55));
            players[1].Tagged = true;
            players[1].PrimaryCd = 1.2;
            players[1].DashCd = 3.5;

            Assert.AreEqual(MapLayout.HalfRiverside, neutrals.SideSwap.SideForTeam(0));
            Assert.AreEqual(MapLayout.HalfUptown, neutrals.SideSwap.SideForTeam(1));

            // Round 3 does not trigger Quick half-time yet
            Assert.IsFalse(neutrals.SideSwap.TryBeginHalfTimeSwap(3, 5, score, players, rules, Layout, neutrals));

            bool started = false;
            bool finished = false;
            neutrals.SideSwap.SwapStarted += dur => { if (Math.Abs(dur - 10.0) < 0.01) started = true; };
            neutrals.SideSwap.SwapFinished += () => finished = true;

            Assert.IsTrue(neutrals.SideSwap.TryBeginHalfTimeSwap(4, 5, score, players, rules, Layout, neutrals));
            Assert.IsTrue(started);
            Assert.IsTrue(neutrals.SideSwap.Swapped);
            Assert.IsTrue(neutrals.SideSwap.IsSwapping);
            Assert.AreEqual(10.0, neutrals.SideSwap.SwapPhaseLeft, 0.001);

            // Halves swapped cleanly; round scores preserved identically
            Assert.AreEqual(MapLayout.HalfUptown, neutrals.SideSwap.SideForTeam(0));
            Assert.AreEqual(MapLayout.HalfRiverside, neutrals.SideSwap.SideForTeam(1));
            Assert.AreEqual(2, score.Points[0], "Team 0 round score preserved");
            Assert.AreEqual(2, score.Points[1], "Team 1 round score preserved");

            // Water (Elixir), UltCharge, cooldowns, HP, and spawn locations reset
            Assert.AreEqual(rules.ElixirMax, players[1].Elixir, 0.001);
            Assert.AreEqual(0.0, players[1].UltCharge, 0.001);
            Assert.AreEqual(0.0, players[1].PrimaryCd, 0.001);
            Assert.AreEqual(0.0, players[1].DashCd, 0.001);
            Assert.AreEqual(0.0, players[1].WeaponCooldowns[0], 0.001);
            Assert.AreEqual(rules.PlayerMaxHp, players[1].Hp);
            Assert.IsFalse(players[1].Tagged);
            Assert.AreEqual(Layout.UptownBaseCenter, players[1].SpawnPosition);
            Assert.AreEqual(Layout.RiversideBaseCenter, players[6].SpawnPosition);

            // Cannot trigger a second time
            Assert.IsFalse(neutrals.SideSwap.TryBeginHalfTimeSwap(5, 5, score, players, rules, Layout, neutrals));

            // Stepping during 10 s swap phase freezes neutral timers until swap completes
            Advance(neutrals, players, new GodotRng(404), 5.0);
            Assert.IsTrue(neutrals.SideSwap.IsSwapping);
            Assert.AreEqual(0.0, neutrals.RoundClock, 0.001);

            Advance(neutrals, players, new GodotRng(404), 5.1);
            Assert.IsFalse(neutrals.SideSwap.IsSwapping);
            Assert.IsTrue(finished);
        }

        [Test]
        public void Determinism_TwoRunsWithSameSeedProduceBitIdenticalNeutralState()
        {
            BarangayNeutrals RunOnce(out OrderedDict<int, PlayerState> finalPlayers)
            {
                var n = new BarangayNeutrals(Layout, Rules());
                var rng = new GodotRng(4242);
                finalPlayers = MakePlayers(
                    (1, 0, "narda", new Vec2(-42.5, 2.6), 55),
                    (6, 1, "inggo", new Vec2(-7.0, 3.0), 100));

                for (int tick = 0; tick < 6000; tick++)
                {
                    if (tick == 1850)
                        n.DamageNeutral(NeutralBossKind.Dragon, 120, 6, 1, true, rng);
                    if (tick == 1920)
                        n.DamageNeutral(NeutralBossKind.Dragon, 480, 6, 1, true, rng);
                    if (tick == 5450)
                        n.DamageNeutral(NeutralBossKind.Baron, 600, 1, 0, true, rng);
                    n.Step(DT, finalPlayers, rng);
                }
                return n;
            }

            BarangayNeutrals a = RunOnce(out OrderedDict<int, PlayerState> pa);
            BarangayNeutrals b = RunOnce(out OrderedDict<int, PlayerState> pb);

            Assert.AreEqual(a.RoundClock, b.RoundClock, 1e-12);
            Assert.AreEqual(a.Selecta.Position, b.Selecta.Position);
            Assert.AreEqual(a.Selecta.Facing, b.Selecta.Facing);
            Assert.AreEqual(a.Selecta.WaypointIndex, b.Selecta.WaypointIndex);
            Assert.AreEqual(a.Dragon.State, b.Dragon.State);
            Assert.AreEqual(a.Dragon.Position, b.Dragon.Position);
            Assert.AreEqual(a.Dragon.VisualScale, b.Dragon.VisualScale, 1e-12);
            Assert.AreEqual(a.Dragon.RespawnLeft, b.Dragon.RespawnLeft, 1e-12);
            Assert.AreEqual(a.Baron.State, b.Baron.State);
            Assert.AreEqual(a.Baron.Position, b.Baron.Position);
            Assert.AreEqual(a.GetTeamBuffs(0).BaronLeft, b.GetTeamBuffs(0).BaronLeft, 1e-12);
            Assert.AreEqual(a.GetTeamBuffs(1).DragonLeft, b.GetTeamBuffs(1).DragonLeft, 1e-12);
            Assert.AreEqual(pa[1].Hp, pb[1].Hp);
            Assert.AreEqual(pa[6].Hp, pb[6].Hp);
        }
    }
}
