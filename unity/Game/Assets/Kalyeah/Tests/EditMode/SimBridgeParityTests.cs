using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    [TestFixture]
    public sealed class SimBridgeParityTests
    {
        string _savedSessionJson;
        string _savedSettingsJson;

        [SetUp]
        public void SetUp()
        {
            _savedSessionJson = AppSession.Current.ToJson();
            _savedSettingsJson = SettingsStore.Current.ToJson();
        }

        [TearDown]
        public void TearDown()
        {
            SettingsStore.Current.ResetToDefaults();
            if (!string.IsNullOrEmpty(_savedSettingsJson))
                SettingsStore.Current.LoadJson(_savedSettingsJson);
            if (!string.IsNullOrEmpty(_savedSessionJson))
                AppSession.Current.LoadJson(_savedSessionJson);
        }

        static DataSource LoadData()
        {
            DataSource res = KalyeahResources.Data();
            return new DataSource(name =>
            {
                try
                {
                    return res.Text(name);
                }
                catch (KeyNotFoundException)
                {
                    string path = Path.Combine(Application.dataPath, "Imported", "Resources", "Data", name + ".json");
                    return File.Exists(path) ? File.ReadAllText(path) : null;
                }
            });
        }

        static DataSource WithRuleOverrides(DataSource baseData, params (string key, string rawJsonValue)[] overrides)
        {
            return new DataSource(name =>
            {
                string json = baseData.Text(name);
                if (name != "rules__game_rules" || overrides == null)
                    return json;
                for (int i = 0; i < overrides.Length; i++)
                {
                    var (key, val) = overrides[i];
                    string pattern = "\"" + Regex.Escape(key) + "\"\\s*:\\s*[^,\\r\\n}]+";
                    json = Regex.Replace(json, pattern, "\"" + key + "\": " + val);
                }
                return json;
            });
        }

        [Test]
        public void SimBridge_5v5Roster_Objectives_AndPortDefineParity()
        {
            DataSource data = LoadData();
            var setup = new MatchSetup
            {
                Seed = 4242L,
                LocalCharacter = "ligaya",
                SuperpowerLoadout = new[] { "tsinelas_light", "bola", "papel_shield" },
                CardCycleOverride = false,
                FirstTo = 3,
            };
            var bridge = new SimBridge(data, setup);

#if KALYEAH_SIM_PORTED
            const bool expectedPorted = true;
#else
            const bool expectedPorted = false;
#endif
            Assert.AreEqual(expectedPorted, !bridge.UsesStandIn, "KALYEAH_SIM_PORTED must match !SimBridge.UsesStandIn");
            Assert.AreEqual(10, bridge.Kids.Count, "5v5 match spawns 10 kids (1 local + 4 allies + 5 enemies)");
            Assert.That(bridge.TickDt, Is.EqualTo(1f / 30f).Within(0.0001f));
            Assert.Greater(bridge.MaxHp, 0);
            Assert.Greater(bridge.ArenaRadius, 0f);

            int team0 = 0;
            int team1 = 0;
            var characters = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < bridge.Kids.Count; i++)
            {
                KidSnapshot k = bridge.Kids[i];
                if (k.Team == 0) team0++;
                else if (k.Team == 1) team1++;
                Assert.IsFalse(string.IsNullOrEmpty(k.CharacterId));
                characters.Add(k.CharacterId);
                Assert.AreEqual(bridge.MaxHp, k.Hp);
            }

            Assert.AreEqual(5, team0);
            Assert.AreEqual(5, team1);
            Assert.IsTrue(bridge.LocalKid.HasValue);
            Assert.AreEqual(SimBridge.LocalId, bridge.LocalKid.Value.Id);
            Assert.AreEqual("ligaya", bridge.LocalKid.Value.CharacterId);

#if KALYEAH_SIM_PORTED
            Assert.AreEqual(10, characters.Count, "All 10 kids in 5v5 have distinct characters");
            Assert.AreEqual(2, bridge.Bases.Count);
            Assert.AreEqual(2, bridge.Rescues.Count);
            Assert.Greater(bridge.Bases[0].Radius, 0f);
            Assert.Greater(bridge.Bases[1].Radius, 0f);
            Assert.AreNotEqual(bridge.Bases[0].Center, bridge.Bases[1].Center);
            Assert.Greater(bridge.Rescues[0].Zone.W, 0f);
            Assert.Greater(bridge.Rescues[1].Zone.H, 0f);
            Assert.AreEqual(4, bridge.MeriendaStands.Count);
            Assert.GreaterOrEqual(bridge.AllWeapons.Count, 14);
            Assert.IsTrue(bridge.PrimaryWeaponInfo.HasValue);
            Assert.IsTrue(bridge.UltimateWeaponInfo.HasValue);
#endif
        }

        [Test]
        public void SimBridge_FullLocalInputFrame_TriggersKitVitalsAndCooldowns()
        {
            DataSource fastUltData = WithRuleOverrides(LoadData(), ("ult_passive_per_s", "6000.0"));
            var bridge = new SimBridge(fastUltData, new MatchSetup
            {
                Seed = 777L,
                LocalCharacter = "junjun",
                SuperpowerLoadout = new[] { "tsinelas_light", "bola", "papel_shield" },
                CardCycleOverride = false,
                TrainingMode = true,
            });

            bridge.SkipIntro();
            SVector2 forward = new SVector2(0f, -1f);

            // Step 1: Primary (Tirador) + Jump
            bridge.SetLocalInput(new LocalInputFrame
            {
                Move = SVector2.Zero,
                Aim = forward,
                Buttons = InputButtons.Primary | InputButtons.Jump,
            });
            bridge.Step();

#if KALYEAH_SIM_PORTED
            KidSnapshot afterPrimaryJump = bridge.LocalKid.Value;
            Assert.Greater(afterPrimaryJump.PrimaryCooldown, 0f, "Primary Tirador starts cooldown");
            Assert.Greater(afterPrimaryJump.JumpCooldown, 0f, "Jump starts cooldown");
            Assert.Greater(afterPrimaryJump.AirY, 0f, "Jump lifts kid into the air");
            Assert.AreEqual(100f, afterPrimaryJump.UltCharge, 0.01f, "Passive ult charge filled to 100%");

            // Land back on the ground so Ground Slide can trigger cleanly
            bridge.SetLocalInput(default);
            for (int i = 0; i < 28 && bridge.LocalKid.Value.AirY > 0.005f; i++)
                bridge.Step();

            // Step 2: Run + Ground Slide
            bridge.SetLocalInput(new LocalInputFrame
            {
                Move = forward,
                Aim = forward,
                Buttons = InputButtons.Slide,
            });
            bridge.Step();
            KidSnapshot afterSlide = bridge.LocalKid.Value;
            Assert.IsTrue(afterSlide.Sliding, "Fast run + Slide enters sliding state");
            Assert.Greater(afterSlide.SlideCooldown, 0f, "Slide starts cooldown");

            // Step 3: Luksong-Tinik Dash
            bridge.SetLocalInput(new LocalInputFrame
            {
                Move = forward,
                Aim = forward,
                Buttons = InputButtons.Dash,
            });
            bridge.Step();
            KidSnapshot afterDash = bridge.LocalKid.Value;
            Assert.IsTrue(afterDash.Dashing, "Dash enters dashing state");
            Assert.Greater(afterDash.DashCooldown, 0f, "Dash starts cooldown");

            // Wait for dash to finish, then cast Card1, Card2, Card3 (press tick -> release tick)
            bridge.SetLocalInput(default);
            for (int i = 0; i < 12; i++)
                bridge.Step();

            int[] slotButtons = { InputButtons.Card1, InputButtons.Card2, InputButtons.Card3 };
            for (int slot = 0; slot < slotButtons.Length; slot++)
            {
                bridge.SetLocalInput(new LocalInputFrame
                {
                    Aim = forward,
                    Buttons = slotButtons[slot],
                });
                bridge.Step();

                bridge.SetLocalInput(new LocalInputFrame
                {
                    Aim = forward,
                    Buttons = 0,
                });
                bridge.Step();

                KidSnapshot snap = bridge.LocalKid.Value;
                Assert.AreEqual(3, snap.HandCount);
                Assert.Greater(snap.SlotCooldowns[slot], 0f, $"Superpower slot {slot + 1} entered cooldown after cast");
            }

            // Step 4: Bagsak ng Kalye Ultimate at 100% charge
            Assert.GreaterOrEqual(bridge.LocalKid.Value.UltCharge, 100f - 0.01f);
            bool ultFired = false;
            bridge.UltimateCast += id => { if (id == SimBridge.LocalId) ultFired = true; };

            SVector2 pos = bridge.LocalKid.Value.Position;
            bridge.SetLocalInput(new LocalInputFrame
            {
                Aim = forward,
                Buttons = InputButtons.Ultimate,
                AimPoint = pos + forward * 4f,
                HasAimPoint = true,
            });
            bridge.Step();
            Assert.IsTrue(ultFired, "UltimateCast event fired for local kid");
            Assert.Less(bridge.LocalKid.Value.UltCharge, 10f, "Ultimate consumes UltCharge");
#else
            Assert.AreEqual(1, bridge.Tick);
#endif
        }

        [Test]
        public void SimBridge_D13_TrySwapLoadout_RequiresTaggedStateAndUpdatesWeapons()
        {
            // Place a 1-HP local kid in front of an active Training fighter so the kid is tagged deterministically within ticks.
            DataSource tagData = WithRuleOverrides(
                LoadData(),
                ("player_max_hp", "1"),
                ("practice_fighters", "2"),
                ("dummy_stand_x", "0.0"),
                ("dummy_patrol_x", "2.0"),
                ("dummy_z", "50.0"),
                ("dummy_ally_x", "-62.0"),
                ("dummy_ally_z", "60.0"));

            var bridge = new SimBridge(tagData, new MatchSetup
            {
                Seed = 99L,
                LocalCharacter = "toni",
                SuperpowerLoadout = new[] { "bato_light", "tsinelas_light", "papel_shield" },
                CardCycleOverride = false,
                TrainingMode = true,
            });

            bridge.SkipIntro();
            Assert.IsFalse(bridge.CanSwapLoadout, "Free player cannot D13-swap loadout");
            Assert.IsFalse(bridge.TrySwapLoadout(new[] { "bola", "jacks", "lata" }));

#if KALYEAH_SIM_PORTED
            // Walk toward the fighter at (0, 50) until tagged
            bridge.SetLocalInput(new LocalInputFrame { Move = new SVector2(0f, -1f) });
            for (int i = 0; i < 180 && !bridge.LocalKid.Value.Tagged; i++)
                bridge.Step();

            Assert.IsTrue(bridge.LocalKid.Value.Tagged, "Local kid was tagged by training fighter");
            Assert.IsTrue(bridge.CanSwapLoadout, "Tagged player on sidewalk can D13-swap loadout");

            string[] swapped = { "bola", "jacks", "lata" };
            Assert.IsTrue(bridge.TrySwapLoadout(swapped));
            CollectionAssert.AreEqual(swapped, bridge.LocalKid.Value.HandWeaponIds);
#endif
        }

        [Test]
        public void AppSession_RankedLpProgression_AndJsonRoundtrip()
        {
            var session = new AppSession
            {
                PlayerName = "  Batang   Kalye! ",
                SelectedCharacter = "tala",
                BotDifficulty = "hard",
                FirstTo = 5,
                RoomCode = "KLYH2026",
                ServerUrl = "wss://kalyeah.example/ws",
            };
            Assert.AreEqual("Batang Kalye", session.PlayerName, "CleanName trims, collapses spaces, strips '!', caps at 12 chars");
            Assert.IsTrue(session.SetSuperpowers(new[] { "bola", "trumpo", "lata" }));
            Assert.AreEqual(0, session.Lp);
            Assert.AreEqual("kahoy", session.CurrentTier.Id);

            int previewWin = session.PreviewMatchLp("online", true, 3, 0);
            Assert.Greater(previewWin, 0);

            RankOutcomeInfo outcome = session.RecordMatchResult("online", "tala", true, 3, 0, 1700000123L);
            Assert.AreEqual(previewWin, outcome.Delta);
            Assert.AreEqual(outcome.NewLp, session.Lp);
            Assert.AreEqual(1, session.TotalMatches);
            Assert.AreEqual(1, session.TotalWins);
            Assert.AreEqual(1, session.History.Count);
            Assert.IsTrue(session.ConsumeLastOutcome().HasValue);
            Assert.IsFalse(session.ConsumeLastOutcome().HasValue, "ConsumeLastOutcome clears pending banner outcome");

            // Advance LP into Bakal (>= 200 LP)
            for (int i = 0; i < 8 && session.Lp < 200; i++)
                session.RecordMatchResult("online", "tala", true, 3, 0, 1700000200L + i);
            Assert.GreaterOrEqual(session.Lp, 200);
            Assert.AreEqual("bakal", session.CurrentTier.Id);

            string json = session.ToJson();
            var restored = new AppSession();
            Assert.IsTrue(restored.LoadJson(json));
            Assert.AreEqual(session.PlayerName, restored.PlayerName);
            Assert.AreEqual("tala", restored.SelectedCharacter);
            Assert.AreEqual("hard", restored.BotDifficulty);
            Assert.AreEqual(5, restored.FirstTo);
            Assert.AreEqual("KLYH2026", restored.RoomCode);
            Assert.AreEqual(session.Lp, restored.Lp);
            Assert.AreEqual(session.PeakLp, restored.PeakLp);
            Assert.AreEqual(session.TotalMatches, restored.TotalMatches);
            Assert.AreEqual(session.TotalWins, restored.TotalWins);
            Assert.AreEqual(session.CurrentTier.Id, restored.CurrentTier.Id);
            CollectionAssert.AreEqual(session.SelectedSuperpowers, restored.SelectedSuperpowers);
            Assert.AreEqual(session.History.Count, restored.History.Count);
        }

        [Test]
        public void SettingsStore_Clamping_UrlNormalization_AndJsonRoundtrip()
        {
            var store = new SettingsStore
            {
                MasterVolume = 0.75f,
                MusicVolume = 0.25f,
                SfxVolume = 0.5f,
                SoundsEnabled = true,
                MusicEnabled = false,
                Haptics = false,
                ScreenShake = false,
                ReduceMotion = true,
                LeftHanded = true,
                TiltToRun = true,
                AimGuide = false,
                CardsTrayMode = true,
                CardsTrayPlacement = "sides",
                ControlScale = 1.19f,
                ColorblindMode = 2,
                GraphicsQuality = 0,
                TargetFrameRate = 29,
                Language = "fil",
                BotDifficulty = "HARD",
            };

            Assert.AreEqual(0f, store.EffectiveMusicVolume, 0.0001f);
            Assert.AreEqual(0.375f, store.EffectiveSfxVolume, 0.0001f);
            Assert.AreEqual(SettingsStore.ScaleLarge, store.ControlScale, 0.0001f);
            Assert.AreEqual(30, store.TargetFrameRate);
            Assert.IsTrue(store.IsColorblind);
            Assert.IsTrue(store.IsTurboGraphics);
            Assert.AreEqual("hard", store.BotDifficulty);

            string json = store.ToJson();
            SettingsStore restored = SettingsStore.FromJson(json);
            Assert.AreEqual(0.75f, restored.MasterVolume, 0.0001f);
            Assert.AreEqual(0.25f, restored.MusicVolume, 0.0001f);
            Assert.AreEqual(0.5f, restored.SfxVolume, 0.0001f);
            Assert.IsFalse(restored.MusicEnabled);
            Assert.IsTrue(restored.ReduceMotion);
            Assert.IsTrue(restored.LeftHanded);
            Assert.IsTrue(restored.TiltToRun);
            Assert.IsFalse(restored.AimGuide);
            Assert.IsTrue(restored.CardsTrayMode);
            Assert.AreEqual("sides", restored.CardsTrayPlacement);
            Assert.AreEqual(SettingsStore.ScaleLarge, restored.ControlScale, 0.0001f);
            Assert.AreEqual(2, restored.ColorblindMode);
            Assert.AreEqual(0, restored.GraphicsQuality);
            Assert.AreEqual(30, restored.TargetFrameRate);
            Assert.AreEqual("fil", restored.Language);
            Assert.AreEqual("hard", restored.BotDifficulty);

            Assert.AreEqual("ws://localhost:9080", SettingsStore.NormalizeServerUrl("localhost:9080"));
            Assert.AreEqual("ws://192.168.43.1:9080", SettingsStore.NormalizeServerUrl("http://192.168.43.1:9080"));
            Assert.AreEqual("wss://play.kalyeah.game", SettingsStore.NormalizeServerUrl("https://play.kalyeah.game"));
        }

        [TestCase(StreetGameKind.Patintero)]
        [TestCase(StreetGameKind.TumbangPreso)]
        [TestCase(StreetGameKind.LangitLupa)]
        public void StreetBridge_AllThreeModes_Step60TicksDeterministically(StreetGameKind kind)
        {
            DataSource data = LoadData();
            const long seed = 20261114L;

            var runA = new StreetBridge(data, kind, seed, "migo");
            var runB = new StreetBridge(data, kind, seed, "migo");

            runA.SkipIntro();
            runB.SkipIntro();

            for (int tick = 0; tick < 60; tick++)
            {
                SVector2 move = tick < 30 ? new SVector2(0f, -1f) : new SVector2(0.5f, -0.5f);
                SVector2 aim = new SVector2(0f, -1f);
                bool primary = tick >= 10 && tick < 20;
                bool secondary = tick == 25;

                runA.SetLocalInput(move, aim, primary, secondary);
                runB.SetLocalInput(move, aim, primary, secondary);

                runA.Step();
                runB.Step();
            }

            Assert.AreEqual(60, runA.Tick);
            Assert.AreEqual(60, runB.Tick);
            Assert.AreEqual(runA.TimeLeft, runB.TimeLeft, 0.0001f);
            Assert.AreEqual(runA.Kids.Count, runB.Kids.Count);
#if KALYEAH_SIM_PORTED
            Assert.AreEqual(6, runA.Kids.Count, $"{kind} spawns 6 street players");
#endif
            for (int i = 0; i < runA.Kids.Count; i++)
            {
                StreetKidSnapshot ka = runA.Kids[i];
                StreetKidSnapshot kb = runB.Kids[i];
                Assert.AreEqual(ka.Id, kb.Id);
                Assert.AreEqual(ka.CharacterId, kb.CharacterId);
                Assert.AreEqual(ka.Role, kb.Role);
                Assert.AreEqual(ka.Position.X, kb.Position.X, 0.0001f, $"{kind} kid {ka.Id} X mismatch");
                Assert.AreEqual(ka.Position.Y, kb.Position.Y, 0.0001f, $"{kind} kid {ka.Id} Z mismatch");
                Assert.AreEqual(ka.Facing.X, kb.Facing.X, 0.0001f);
                Assert.AreEqual(ka.Facing.Y, kb.Facing.Y, 0.0001f);
                Assert.AreEqual(ka.Stamina, kb.Stamina, 0.0001f);
            }

#if KALYEAH_SIM_PORTED
            switch (kind)
            {
                case StreetGameKind.Patintero:
                    Assert.Greater(runA.PatinteroState.GuardLines.Count, 0);
                    Assert.AreEqual(runA.PatinteroState.StallLeft, runB.PatinteroState.StallLeft, 0.0001f);
                    break;
                case StreetGameKind.TumbangPreso:
                    Assert.Greater(runA.TumbangPresoState.TayaId, 0);
                    Assert.AreEqual(runA.TumbangPresoState.CanPosition, runB.TumbangPresoState.CanPosition);
                    break;
                case StreetGameKind.LangitLupa:
                    Assert.Greater(runA.LangitLupaState.Platforms.Count, 0);
                    Assert.AreEqual(runA.LangitLupaState.TayaId, runB.LangitLupaState.TayaId);
                    break;
            }
#endif
        }
    }
}
