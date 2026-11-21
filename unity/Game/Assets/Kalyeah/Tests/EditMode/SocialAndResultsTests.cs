using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityObject = UnityEngine.Object;

namespace Kalyeah.Game
{
    /// <summary>
    /// EditMode NUnit verification suite for Chunk 4 (LoreCodexData, MvpCalculator, PingAndBanterView, MatchResultsView):
    /// proves zero raw-damage MVP bias, 12-kid/4-purok/6-rival respectful lore coverage, 1-honor-per-match enforcement
    /// (teammate or rival), all 5 tactical pings + cooldown + zero-cost cancel, and 4-aspect bounds/48dp/sightbox/dark-ink parity.
    /// </summary>
    [TestFixture]
    public sealed class SocialAndResultsTests
    {
        readonly struct ViewportProfile
        {
            public readonly int Width;
            public readonly int Height;
            public readonly Rect RawSafeRect;
            public readonly ScreenOrientation Orientation;
            public readonly string Label;

            public ViewportProfile(int width, int height, Rect rawSafeRect, ScreenOrientation orientation, string label)
            {
                Width = width;
                Height = height;
                RawSafeRect = rawSafeRect;
                Orientation = orientation;
                Label = label;
            }
        }

        readonly List<IDisposable> _disposables = new List<IDisposable>();
        readonly List<UnityObject> _cleanup = new List<UnityObject>();
        readonly List<RenderTexture> _targets = new List<RenderTexture>();
        bool _savedLeftHanded;

        [SetUp]
        public void SetUp()
        {
            _savedLeftHanded = SettingsStore.Current != null && SettingsStore.Current.LeftHanded;
            if (SettingsStore.Current != null)
                SettingsStore.Current.LeftHanded = false;
            ScreenFitMetrics.ClearSimulatedViewport();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _disposables.Count - 1; i >= 0; i--)
            {
                try { _disposables[i]?.Dispose(); }
                catch (Exception) { }
            }
            _disposables.Clear();

            ScreenFitMetrics.ClearSimulatedViewport();

            for (int i = _cleanup.Count - 1; i >= 0; i--)
            {
                if (_cleanup[i] != null)
                    KalyeahResources.Kill(_cleanup[i]);
            }
            _cleanup.Clear();

            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                if (_targets[i] != null)
                {
                    _targets[i].Release();
                    KalyeahResources.Kill(_targets[i]);
                }
            }
            _targets.Clear();

            if (SettingsStore.Current != null)
                SettingsStore.Current.LeftHanded = _savedLeftHanded;
        }

        public static IEnumerable<TestCaseData> CanonicalProfiles()
        {
            yield return new TestCaseData(
                844,
                390,
                ScreenFitMetrics.AsymmetricCutoutSafeRect(844, 390, 44f, cutoutOnRight: false),
                ScreenOrientation.LandscapeLeft,
                "19.5:9 Notched 844x390 (+44px cutout)").SetName("Chunk4_844x390_Notched19_5x9");

            yield return new TestCaseData(
                1280,
                720,
                new Rect(0f, 0f, 1280f, 720f),
                ScreenOrientation.LandscapeLeft,
                "16:9 Reference 1280x720").SetName("Chunk4_1280x720_Reference16x9");

            yield return new TestCaseData(
                934,
                400,
                new Rect(0f, 0f, 934f, 400f),
                ScreenOrientation.LandscapeLeft,
                "21:9 UltraWide 934x400").SetName("Chunk4_934x400_UltraWide21x9");

            yield return new TestCaseData(
                1024,
                768,
                new Rect(0f, 0f, 1024f, 768f),
                ScreenOrientation.LandscapeLeft,
                "4:3 Inner Foldable 1024x768").SetName("Chunk4_1024x768_Foldable4x3");
        }

        // =================================================================================================================
        // 1. MvpCalculator & Reward Rules (docs/SPEC.md §8 L376-383, §9 L401-409)
        // =================================================================================================================

        [Test]
        public void MvpCalculator_AwardsObjectiveSupportDefenderOverPureDamageStriker_WithZeroRawDamageBias()
        {
            var pureDamageStriker = new MatchPlayerStats(
                playerId: 1, team: 0, playerName: "DAMAGEONLY", kidId: "narda", isLocalPlayer: false,
                tags: 1, assists: 0, rescues: 0, captures: 0, supportScore: 0, rawDamage: 4800);

            var objectiveDefender = new MatchPlayerStats(
                playerId: 2, team: 0, playerName: "KALYEAHPRO99", kidId: "paeng", isLocalPlayer: true,
                tags: 0, assists: 2, rescues: 3, captures: 1, supportScore: 4, rawDamage: 90);

            var redSeeker = new MatchPlayerStats(
                playerId: 6, team: 1, playerName: "SCOUTKARDING", kidId: "karding", isLocalPlayer: false,
                tags: 2, assists: 4, rescues: 2, captures: 1, supportScore: 5, rawDamage: 320);

            var redDamageStriker = new MatchPlayerStats(
                playerId: 7, team: 1, playerName: "JUMONGBLAST", kidId: "jumong", isLocalPlayer: false,
                tags: 2, assists: 0, rescues: 0, captures: 0, supportScore: 0, rawDamage: 5200);

            var roster = new[] { pureDamageStriker, objectiveDefender, redSeeker, redDamageStriker };

            MvpCalculator.ResolveTeamMvps(roster, out int blueMvp, out int redMvp);
            Assert.AreEqual(2, blueMvp, "High-rescue/support Defender must beat pure-damage Striker for Blue MVP");
            Assert.AreEqual(6, redMvp, "High-assist/reveal Seeker must beat pure-damage Striker for Red MVP");

            // Exact zero raw-damage invariance check
            var zeroDmg = new MatchPlayerStats(10, 0, "A", "inggo", false, tags: 2, assists: 1, rescues: 1, captures: 0, supportScore: 2, rawDamage: 0);
            var hugeDmg = new MatchPlayerStats(10, 0, "A", "inggo", false, tags: 2, assists: 1, rescues: 1, captures: 0, supportScore: 2, rawDamage: 999999);
            Assert.AreEqual(MvpCalculator.ComputeScore(in zeroDmg), MvpCalculator.ComputeScore(in hugeDmg), 0.0001f,
                "RawDamage must have strictly zero effect on MVP score");

            // Barya & Mastery XP schedule per docs/SPEC.md §9 L406 & §8 L382
            Assert.AreEqual(60, MvpCalculator.ComputeRewards(MatchRewardMode.Blitz, won: false, isMvp: false).TotalBarya);
            Assert.AreEqual(100, MvpCalculator.ComputeRewards(MatchRewardMode.Blitz, won: true, isMvp: false).TotalBarya);
            Assert.AreEqual(90, MvpCalculator.ComputeRewards(MatchRewardMode.Quick, won: false, isMvp: false).TotalBarya);
            Assert.AreEqual(150, MvpCalculator.ComputeRewards(MatchRewardMode.Quick, won: true, isMvp: false).TotalBarya);
            Assert.AreEqual(120, MvpCalculator.ComputeRewards(MatchRewardMode.Standard, won: false, isMvp: false).TotalBarya);
            Assert.AreEqual(200, MvpCalculator.ComputeRewards(MatchRewardMode.Standard, won: true, isMvp: false).TotalBarya);

            MatchRewardSummary mvpFirstWin = MvpCalculator.ComputeRewards(
                MatchRewardMode.Blitz, won: true, isMvp: true, isFirstWinOfDay: true, isRanked: true);
            Assert.AreEqual(420, mvpFirstWin.TotalBarya, "Blitz win (100) + MVP (20) + First Win (300) = 420");
            Assert.Greater(mvpFirstWin.MvpMasteryBonusXp, 0, "MVP must grant bonus mastery XP");
            Assert.AreEqual(1, mvpFirstWin.RankedStarDelta);
            Assert.IsFalse(mvpFirstWin.StarSavedByMvp);

            // Losing-team MVP in Ranked keeps their star (SPEC.md L382)
            MatchRewardSummary rankedLossMvp = MvpCalculator.ComputeRewards(
                MatchRewardMode.Standard, won: false, isMvp: true, isRanked: true);
            Assert.IsTrue(rankedLossMvp.StarSavedByMvp, "Losing-team MVP in Ranked must save their star");
            Assert.AreEqual(0, rankedLossMvp.RankedStarDelta);
            Assert.AreEqual(140, rankedLossMvp.TotalBarya, "Standard loss (120) + MVP (20) = 140");

            MatchRewardSummary rankedLossNonMvp = MvpCalculator.ComputeRewards(
                MatchRewardMode.Standard, won: false, isMvp: false, isRanked: true);
            Assert.IsFalse(rankedLossNonMvp.StarSavedByMvp);
            Assert.AreEqual(-1, rankedLossNonMvp.RankedStarDelta);

            // Weekly 3,000 Barya cap clamp (SPEC.md L407)
            MatchRewardSummary capped = MvpCalculator.ComputeRewards(
                MatchRewardMode.Standard, won: true, isMvp: true, isFirstWinOfDay: true, weeklyBaryaAlreadyEarned: 2950);
            Assert.AreEqual(50, capped.TotalBarya, "Barya rewards must clamp inside the 3,000 weekly cap");

            // Hotspot gives zero rewards (SPEC.md L409)
            MatchRewardSummary hotspot = MvpCalculator.ComputeRewards(
                MatchRewardMode.Blitz, won: true, isMvp: true, isFirstWinOfDay: true, isHotspot: true);
            Assert.AreEqual(0, hotspot.TotalBarya);
            Assert.AreEqual(0, hotspot.TotalMasteryXp);

            // Mastery levels 1..7
            Assert.AreEqual(1, MvpCalculator.MasteryLevelForXp(0));
            Assert.AreEqual(7, MvpCalculator.MasteryLevelForXp(6000));
        }

        // =================================================================================================================
        // 2. LoreCodexData (docs/LORE.md: 4 Puroks, 12 Kids, 6 Rival Pairs, Ate Lorna, Respectful Banter)
        // =================================================================================================================

        [Test]
        public void LoreCodexData_CoversAll12Kids4Puroks6RivalPairs_AndGeneratesRespectfulDeterministicBanter()
        {
            Assert.AreEqual(4, LoreCodexData.Puroks.Count, "Must define exactly 4 puroks");
            Assert.AreEqual(12, LoreCodexData.Kids.Count, "Must define all 12 kids");
            Assert.AreEqual(6, LoreCodexData.RivalPairs.Count, "Must define exactly 6 rival pairs");

            var seenKids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int p = 0; p < LoreCodexData.Puroks.Count; p++)
            {
                PurokEntry purok = LoreCodexData.Puroks[p];
                Assert.AreEqual(3, purok.KidIds.Length, $"Purok {purok.Name} must have exactly 3 kids");
                Assert.IsFalse(string.IsNullOrWhiteSpace(purok.HomeMap));
                for (int k = 0; k < purok.KidIds.Length; k++)
                    Assert.IsTrue(seenKids.Add(purok.KidIds[k]), $"Duplicate kid '{purok.KidIds[k]}' in purok table");
            }
            Assert.AreEqual(12, seenKids.Count);

            // Verify every kid in AppSession.DefaultRoster exists with full lore metadata
            for (int i = 0; i < AppSession.DefaultRoster.Length; i++)
            {
                string id = AppSession.DefaultRoster[i];
                Assert.IsTrue(LoreCodexData.TryGetKid(id, out KidLoreEntry entry), $"Missing lore entry for '{id}'");
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.DisplayName), id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Personality), id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.HomeAndFamily), id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.DreamJob), id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.IdleAction), id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.VictoryPose), id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.AteLornaCodexSummary), id);
                AssertRespectfulLine(entry.AteLornaCodexSummary, "Codex:" + id);
            }

            // Verify exact 6 symmetric rival pairs from docs/LORE.md L18
            var expectedRivals = new (string a, string b)[]
            {
                ("narda", "jumong"),
                ("inggo", "manoy"),
                ("jopay", "paeng"),
                ("oriang", "pamela"),
                ("kikay", "karding"),
                ("tere", "domeng"),
            };

            for (int i = 0; i < expectedRivals.Length; i++)
            {
                string a = expectedRivals[i].a;
                string b = expectedRivals[i].b;
                Assert.IsTrue(LoreCodexData.AreRivals(a, b), $"{a} and {b} must be rivals");
                Assert.IsTrue(LoreCodexData.AreRivals(b, a), $"{b} and {a} rivalry must be symmetric");
                Assert.AreEqual(b, LoreCodexData.GetRivalId(a));
                Assert.AreEqual(a, LoreCodexData.GetRivalId(b));
                Assert.IsFalse(LoreCodexData.AreFriends(a, b), "Rivals are from different puroks");

                BanterLine tagAB = LoreCodexData.ResolveTagBanter(a, b, seed: 17);
                BanterLine tagBA = LoreCodexData.ResolveTagBanter(b, a, seed: 17);
                Assert.AreEqual(BanterRelation.Rival, tagAB.Relation);
                Assert.AreEqual(BanterRelation.Rival, tagBA.Relation);
                AssertRespectfulLine(tagAB.Text, $"Tag:{a}->{b}");
                AssertRespectfulLine(tagBA.Text, $"Tag:{b}->{a}");
            }

            // Same-purok friendship check
            Assert.IsTrue(LoreCodexData.AreFriends("narda", "jopay"));
            Assert.IsTrue(LoreCodexData.AreFriends("inggo", "karding"));
            Assert.IsTrue(LoreCodexData.AreFriends("jumong", "oriang"));
            Assert.IsTrue(LoreCodexData.AreFriends("paeng", "tere"));
            Assert.IsFalse(LoreCodexData.AreFriends("narda", "narda"));

            // Deterministic match-start banter: Rival -> PurokFriend -> Ate Lorna fallback
            BanterLine startRival1 = LoreCodexData.ResolveMatchStartBanter(new[] { "narda", "jopay" }, new[] { "jumong", "tere" }, seed: 42);
            BanterLine startRival2 = LoreCodexData.ResolveMatchStartBanter(new[] { "narda", "jopay" }, new[] { "jumong", "tere" }, seed: 42);
            Assert.AreEqual(BanterRelation.Rival, startRival1.Relation);
            Assert.AreEqual(startRival1.Text, startRival2.Text, "Match-start banter must be deterministic");
            AssertRespectfulLine(startRival1.Text, "StartRival");

            BanterLine startFriend = LoreCodexData.ResolveMatchStartBanter(new[] { "narda", "jopay" }, new[] { "tere", "karding" }, seed: 7);
            Assert.AreEqual(BanterRelation.PurokFriend, startFriend.Relation);
            AssertRespectfulLine(startFriend.Text, "StartFriend");

            BanterLine startAteLorna = LoreCodexData.ResolveMatchStartBanter(new[] { "narda" }, new[] { "tere" }, seed: 9);
            Assert.AreEqual(BanterRelation.AteLorna, startAteLorna.Relation);
            Assert.AreEqual(LoreCodexData.AteLornaId, startAteLorna.SpeakerId);
            AssertRespectfulLine(startAteLorna.Text, "StartAteLorna");

            BanterLine rescueFriend = LoreCodexData.ResolveRescueBanter("jopay", "narda", seed: 5);
            Assert.AreEqual(BanterRelation.PurokFriend, rescueFriend.Relation);
            AssertRespectfulLine(rescueFriend.Text, "RescueFriend");

            BanterLine rescueRival = LoreCodexData.ResolveRescueBanter("jumong", "narda", seed: 5);
            Assert.AreEqual(BanterRelation.Rival, rescueRival.Relation);
            AssertRespectfulLine(rescueRival.Text, "RescueRival");
        }

        // =================================================================================================================
        // 3. MatchResultsView Functional & 4-Aspect ScreenFit Audit
        // =================================================================================================================

        [Test]
        public void MatchResultsView_EnforcesSingleHonorPerMatchForTeammateOrRival_AndDisplaysDualMvpsAndStarSaved()
        {
            var profile = new ViewportProfile(1280, 720, new Rect(0f, 0f, 1280f, 720f), ScreenOrientation.LandscapeLeft, "16:9");
            Canvas canvas = BuildMenuCanvasRig("ResultsFunc", profile, out _);

            int playAgainClicks = 0;
            int homeClicks = 0;
            var view = new MatchResultsView(canvas.transform, () => playAgainClicks++, () => homeClicks++, canvas);
            _disposables.Add(view);

            int honoredId = -1;
            string honoredName = string.Empty;
            int honorEvents = 0;
            view.PlayerHonored += (id, name) =>
            {
                honoredId = id;
                honoredName = name;
                honorEvents++;
            };

            // Bind Ranked Defeat where local player (id 2, Paeng) is losing-team MVP -> STAR SAVED
            MatchResultsPayload rankedDefeat = BuildSamplePayload(localWon: false, isRanked: true);
            view.Bind(rankedDefeat);

            Assert.AreEqual("DEFEAT", view.OutcomeTitleText);
            Assert.AreEqual(2, view.BlueMvpPlayerId, "Defender Paeng must be Blue MVP");
            Assert.AreEqual(6, view.RedMvpPlayerId, "Seeker Karding must be Red MVP");
            Assert.IsTrue(view.LastRewards.StarSavedByMvp);

            Assert.IsTrue(view.TryGetRowMvpBadgeText(2, out string blueBadge));
            StringAssert.Contains("STAR SAVED", blueBadge);
            Assert.IsTrue(view.TryGetRowMvpBadgeText(6, out string redBadge));
            Assert.AreEqual("MVP", redBadge);

            // RawDamage is displayed per docs/SPEC.md §8 L378 while never biasing MVP selection
            Assert.IsTrue(view.TryGetRowKidRoleText(1, out string strikerMeta));
            StringAssert.Contains("3.8k DMG", strikerMeta);
            Assert.IsTrue(view.TryGetRowKidRoleText(2, out string mvpMeta));
            StringAssert.Contains("140 DMG", mvpMeta);

            // Self-honor must be rejected
            Assert.IsFalse(view.TryHonorPlayer(2), "Local player cannot honor self");
            Assert.IsFalse(view.HasHonored);
            Assert.AreEqual(0, honorEvents);

            // Honor a RIVAL (enemy player id 6) -> succeeds once and locks further honors
            Assert.IsTrue(view.TryHonorPlayer(6), "Honoring a rival must succeed");
            Assert.IsTrue(view.HasHonored);
            Assert.AreEqual(6, view.HonoredPlayerId);
            Assert.AreEqual(6, honoredId);
            Assert.AreEqual("SCOUTKARDING", honoredName);
            Assert.AreEqual(1, honorEvents);

            Assert.IsFalse(view.TryHonorPlayer(1), "Second honor on a teammate must be blocked");
            Assert.IsFalse(view.TryHonorPlayer(7), "Second honor on another rival must be blocked");
            Assert.AreEqual(1, honorEvents);

            // Re-bind Victory match -> resets honor lock, allows honoring a TEAMMATE once
            view.Bind(BuildSamplePayload(localWon: true, isRanked: false));
            Assert.AreEqual("VICTORY", view.OutcomeTitleText);
            Assert.IsFalse(view.HasHonored, "Re-binding a new match must reset honor state");
            Assert.IsTrue(view.TryGetRowHonorButton(1, out Button mateBtn));
            mateBtn.onClick.Invoke();
            Assert.IsTrue(view.HasHonored);
            Assert.AreEqual(1, view.HonoredPlayerId);
            Assert.AreEqual(2, honorEvents);
            Assert.IsFalse(view.TryHonorPlayer(6), "After honoring teammate, honoring rival must be blocked");

            view.PlayAgainButton.onClick.Invoke();
            view.HomeButton.onClick.Invoke();
            Assert.AreEqual(1, playAgainClicks);
            Assert.AreEqual(1, homeClicks);
        }

        [TestCaseSource(nameof(CanonicalProfiles))]
        public void MatchResultsView_AcrossCanonical4AspectMatrix_PassesSafeArea48DpFullBleedDisjointAndDarkInk(
            int width,
            int height,
            Rect rawSafePx,
            ScreenOrientation orientation,
            string label)
        {
            var profile = new ViewportProfile(width, height, rawSafePx, orientation, label);
            Canvas canvas = BuildMenuCanvasRig($"Results_{width}x{height}", profile, out Camera cam);

            var view = new MatchResultsView(canvas.transform, () => { }, () => { }, canvas);
            _disposables.Add(view);

            // Audit Victory layout (incl. 12-char player names + Barya + Mastery XP + Dual MVP)
            view.Bind(BuildSamplePayload(localWon: true, isRanked: false));
            AuditResultsSurface(canvas, cam, view, profile, "MatchResultsView/Victory");

            // Honor a rival and re-audit (gold HONORED dark-ink pill must keep zero outline smudge & zero truncation!)
            Assert.IsTrue(view.TryHonorPlayer(6));
            Assert.IsTrue(view.TryGetRowHonorButton(6, out Button honoredBtn));
            Text honoredLabel = honoredBtn.GetComponentInChildren<Text>(includeInactive: false);
            Assert.IsNotNull(honoredLabel);
            Assert.AreEqual("HONORED", honoredLabel.text);
            AuditResultsSurface(canvas, cam, view, profile, "MatchResultsView/Honored");
            AssertTextIntegrity(view.Root, $"MatchResultsView/Honored[{profile.Label}]");

            // Audit Ranked Defeat with losing-team MVP "MVP · STAR SAVED"
            view.Bind(BuildSamplePayload(localWon: false, isRanked: true));
            AuditResultsSurface(canvas, cam, view, profile, "MatchResultsView/RankedDefeatStarSaved");
        }

        [Test]
        public void MatchResultsView_ReflowsCleanlyOn180DegreeLandscapeOrientationFlip()
        {
            Rect leftCutout = ScreenFitMetrics.AsymmetricCutoutSafeRect(844, 390, 44f, cutoutOnRight: false);
            var profileLeft = new ViewportProfile(844, 390, leftCutout, ScreenOrientation.LandscapeLeft, "844x390 LandscapeLeft");
            Canvas canvas = BuildMenuCanvasRig("ResultsFlip", profileLeft, out Camera cam);

            var view = new MatchResultsView(canvas.transform, () => { }, () => { }, canvas);
            _disposables.Add(view);
            view.Bind(BuildSamplePayload(localWon: true, isRanked: true));
            AuditResultsSurface(canvas, cam, view, profileLeft, "ResultsFlip/Left");

            Rect rightCutout = ScreenFitMetrics.AsymmetricCutoutSafeRect(844, 390, 44f, cutoutOnRight: true);
            var profileRight = new ViewportProfile(844, 390, rightCutout, ScreenOrientation.LandscapeRight, "844x390 LandscapeRight");
            ScreenFitMetrics.SetSimulatedViewport(new Vector2Int(844, 390), rightCutout, null, ScreenOrientation.LandscapeRight);
            AuditResultsSurface(canvas, cam, view, profileRight, "ResultsFlip/Right");
            Assert.AreEqual(44f, view.SafeAreaComponent.InsetsPx.z, 0.5f);
            Assert.AreEqual(0f, view.SafeAreaComponent.InsetsPx.x, 0.5f);
        }

        // =================================================================================================================
        // 4. PingAndBanterView Functional & 4-Aspect Sightbox / 48dp Audit
        // =================================================================================================================

        [Test]
        public void PingAndBanterView_EmitsAll5Pings_EnforcesCooldown_AndHonorsZeroCostCancel()
        {
            var profile = new ViewportProfile(1280, 720, new Rect(0f, 0f, 1280f, 720f), ScreenOrientation.LandscapeLeft, "16:9");
            Canvas canvas = BuildMenuCanvasRig("PingFunc", profile, out _);
            RectTransform safeRt = Ui.Fill("SafeArea", canvas.transform);
            SafeArea safe = safeRt.gameObject.AddComponent<SafeArea>();
            safe.ForceApply();

            var view = new PingAndBanterView(safeRt, canvas, interactive: true);
            _disposables.Add(view);

            var emittedKinds = new List<TacticalPingKind>(8);
            var emittedMsgs = new List<string>(8);
            view.PingEmitted += (k, msg) =>
            {
                emittedKinds.Add(k);
                emittedMsgs.Add(msg);
            };

            // Center cancel must emit nothing and incur ZERO cooldown
            Assert.IsFalse(view.SimulateHoldDragRelease(Vector2.zero, holdSeconds: 0.30f), "Releasing in center must cancel");
            Assert.AreEqual(0, emittedKinds.Count);
            Assert.AreEqual(0f, view.CooldownRemaining, 0.001f, "Cancel must not trigger anti-spam cooldown");

            // Emit all 5 radial tactical pings via wheel hold-drag-release, proving cooldown blocks spam between them
            TacticalPingKind[] allKinds =
            {
                TacticalPingKind.Attack,
                TacticalPingKind.Defend,
                TacticalPingKind.Rescue,
                TacticalPingKind.Danger,
                TacticalPingKind.OnMyWay,
            };

            for (int i = 0; i < allKinds.Length; i++)
            {
                TacticalPingKind kind = allKinds[i];
                Vector2 drag = PingAndBanterView.GetWedgeDirection(kind) * 80f;
                Assert.IsTrue(view.SimulateHoldDragRelease(drag, holdSeconds: 0.30f), $"Wheel ping {kind} must emit");
                Assert.AreEqual(i + 1, emittedKinds.Count);
                Assert.AreEqual(kind, emittedKinds[i]);
                Assert.IsFalse(string.IsNullOrWhiteSpace(emittedMsgs[i]));

                // Anti-spam guard blocks immediate repeat ping
                Assert.IsFalse(view.TryEmitPing(kind), "Anti-spam cooldown must reject immediate repeat ping");
                Assert.AreEqual(i + 1, emittedKinds.Count);

                // Advance cooldown cleanly
                view.Tick(PingAndBanterView.CooldownSeconds + 0.05f);
                Assert.AreEqual(0f, view.CooldownRemaining, 0.001f);
            }

            // Short tap emits DefaultTapPing
            view.DefaultTapPing = TacticalPingKind.Rescue;
            Assert.IsTrue(view.SimulateHoldDragRelease(Vector2.zero, holdSeconds: 0.05f));
            Assert.AreEqual(TacticalPingKind.Rescue, view.LastEmittedPing);

            // Banter toast shows and auto-dismisses cleanly via Tick
            BanterLine line = view.ShowTagBanter("narda", "jumong", seed: 11);
            Assert.IsTrue(view.IsBanterVisible);
            Assert.AreEqual(line.SpeakerName, view.ActiveSpeakerName);
            view.Tick(PingAndBanterView.DefaultBanterDuration + 0.1f);
            Assert.IsFalse(view.IsBanterVisible, "Banter toast must fade out and hide after its duration");
        }

        [TestCaseSource(nameof(CanonicalProfiles))]
        public void PingAndBanterView_AcrossCanonical4AspectMatrix_KeepsBanterAndTriggerOutsideCenterSightbox(
            int width,
            int height,
            Rect rawSafePx,
            ScreenOrientation orientation,
            string label)
        {
            var profile = new ViewportProfile(width, height, rawSafePx, orientation, label);

            foreach (bool leftHanded in new[] { false, true })
            {
                SettingsStore.Current.LeftHanded = leftHanded;
                Canvas canvas = BuildMenuCanvasRig($"Ping_{width}x{height}_LH{leftHanded}", profile, out Camera cam);
                RectTransform safeRt = Ui.Fill("SafeArea", canvas.transform);
                SafeArea safe = safeRt.gameObject.AddComponent<SafeArea>();
                safe.ForceApply();

                var view = new PingAndBanterView(safeRt, canvas, interactive: true);
                _disposables.Add(view);

                // Display longest Ate Lorna intro banter to stress worst-case text fit & sightbox clearance
                BanterLine ateLorna = LoreCodexData.ResolveAteLornaIntro("Barangay", seed: 0);
                view.ShowBanter(in ateLorna);

                ForceCanvasAndSafe(canvas, view.Root, view.RefreshLayout);

                Rect safePx = ScreenFitMetrics.ResolveSafeRectPx();
                Rect sightboxPx = ComputeCenterSightbox(safePx);
                float minTouchPx = ScreenFitMetrics.DpToScreenPx(ScreenFitMetrics.MinTouchDp);
                string ctx = $"PingAndBanter[{label},LH={leftHanded}]";

                Rect pingBtnPx = GetScreenRect(view.PingButtonRect, cam);
                Rect banterPx = GetScreenRect(view.BanterRoot, cam);

                AssertTouchTarget48Dp(pingBtnPx, minTouchPx, $"{ctx} PingButton");
                AssertContainedInSafeRect(safePx, pingBtnPx, $"{ctx} PingButton");
                AssertContainedInSafeRect(safePx, banterPx, $"{ctx} BanterStrip");

                Assert.IsFalse(
                    Shrink(pingBtnPx, 1f).Overlaps(Shrink(sightboxPx, 1f)),
                    $"{ctx}: PingButton {pingBtnPx} intrudes into 45%x50% center sightbox {sightboxPx}");
                Assert.IsFalse(
                    Shrink(banterPx, 1f).Overlaps(Shrink(sightboxPx, 1f)),
                    $"{ctx}: BanterStrip {banterPx} intrudes into 45%x50% center sightbox {sightboxPx}");
                Assert.IsFalse(
                    Shrink(pingBtnPx, 1f).Overlaps(Shrink(banterPx, 1f)),
                    $"{ctx}: PingButton {pingBtnPx} overlaps BanterStrip {banterPx}");

                // Open radial wheel and verify 100% SafeArea containment + crisp labels
                view.OpenWheelForTesting();
                ForceCanvasAndSafe(canvas, view.Root, view.RefreshLayout);
                Rect wheelPx = GetScreenRect(view.WheelRoot, cam);
                AssertContainedInSafeRect(safePx, wheelPx, $"{ctx} RadialWheel");

                AssertNoDarkInkDarkOutlineSmudge(view.Root, ctx);
                AssertTextIntegrity(view.Root, ctx);
                view.CloseWheel();
            }

            SettingsStore.Current.LeftHanded = false;
        }

        // =================================================================================================================
        // Helpers & Unified Surface Bounds Auditors
        // =================================================================================================================

        static MatchResultsPayload BuildSamplePayload(bool localWon, bool isRanked)
        {
            return new MatchResultsPayload
            {
                LocalTeamWon = localWon,
                LocalTeam = 0,
                BlueScore = localWon ? 3 : 2,
                RedScore = localWon ? 1 : 3,
                Mode = isRanked ? MatchRewardMode.Standard : MatchRewardMode.Blitz,
                IsRanked = isRanked,
                IsHotspot = false,
                IsFirstWinOfDay = localWon,
                WeeklyBaryaAlreadyEarned = 0,
                MapName = "Barangay",
                Players = new[]
                {
                    new MatchPlayerStats(1, 0, "DAMAGEONLY", "narda", false, tags: 1, assists: 0, rescues: 0, captures: 0, supportScore: 0, rawDamage: 3800),
                    new MatchPlayerStats(2, 0, "KALYEAHPRO99", "paeng", true, tags: 1, assists: 3, rescues: 3, captures: 1, supportScore: 4, rawDamage: 140),
                    new MatchPlayerStats(3, 0, "JOPAYFORT", "jopay", false, tags: 2, assists: 1, rescues: 1, captures: 0, supportScore: 2, rawDamage: 420),
                    new MatchPlayerStats(4, 0, "KIKAYKITE", "kikay", false, tags: 1, assists: 2, rescues: 0, captures: 1, supportScore: 2, rawDamage: 310),
                    new MatchPlayerStats(5, 0, "INGGOSIPA", "inggo", false, tags: 3, assists: 1, rescues: 0, captures: 0, supportScore: 1, rawDamage: 920),

                    new MatchPlayerStats(6, 1, "SCOUTKARDING", "karding", false, tags: 2, assists: 4, rescues: 2, captures: 1, supportScore: 5, rawDamage: 380),
                    new MatchPlayerStats(7, 1, "JUMONGBLAST", "jumong", false, tags: 3, assists: 0, rescues: 0, captures: 0, supportScore: 0, rawDamage: 4100),
                    new MatchPlayerStats(8, 1, "ORIANGCARE", "oriang", false, tags: 1, assists: 2, rescues: 1, captures: 0, supportScore: 3, rawDamage: 260),
                    new MatchPlayerStats(9, 1, "PAMELARULE", "pamela", false, tags: 1, assists: 2, rescues: 0, captures: 0, supportScore: 2, rawDamage: 390),
                    new MatchPlayerStats(10, 1, "DOMENGPLAN", "domeng", false, tags: 0, assists: 1, rescues: 1, captures: 0, supportScore: 2, rawDamage: 210),
                },
            };
        }

        Canvas BuildMenuCanvasRig(string rootPrefix, ViewportProfile profile, out Camera cam)
        {
            ScreenFitMetrics.EnsureEnhancedTouch();
            Ui.EnsureEventSystem();
            ScreenFitMetrics.SetSimulatedViewport(
                new Vector2Int(profile.Width, profile.Height),
                profile.RawSafeRect,
                null,
                profile.Orientation);

            var rt = new RenderTexture(profile.Width, profile.Height, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            _targets.Add(rt);

            var root = new GameObject($"{rootPrefix}_{profile.Width}x{profile.Height}");
            _cleanup.Add(root);

            var camGo = new GameObject("UiCamera", typeof(Camera));
            camGo.transform.SetParent(root.transform, false);
            cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.targetTexture = rt;
            cam.aspect = (float)profile.Width / profile.Height;

            Canvas canvas = Ui.MakeCanvas(rootPrefix + "_Canvas", root.transform, cam);
            Canvas.ForceUpdateCanvases();
            return canvas;
        }

        static void AuditResultsSurface(Canvas canvas, Camera cam, MatchResultsView view, ViewportProfile profile, string label)
        {
            ForceCanvasAndSafe(canvas, view.Root, view.RefreshLayout);

            Rect expectedSafePx = ScreenFitMetrics.ResolveSafeRectPx();
            float minTouchPx = ScreenFitMetrics.DpToScreenPx(ScreenFitMetrics.MinTouchDp);
            string ctx = $"{label}[{profile.Label}]";

            // 1. Full-bleed backdrop covers 100% of raw screen bounds (incl. notches & 18 dp gesture pill)
            Rect backdropPx = GetScreenRect(view.BackdropRect, cam);
            Assert.LessOrEqual(backdropPx.xMin, 1.5f, $"{ctx}: backdrop xMin");
            Assert.LessOrEqual(backdropPx.yMin, 1.5f, $"{ctx}: backdrop yMin");
            Assert.GreaterOrEqual(backdropPx.xMax, profile.Width - 1.5f, $"{ctx}: backdrop xMax");
            Assert.GreaterOrEqual(backdropPx.yMax, profile.Height - 1.5f, $"{ctx}: backdrop yMax");

            // 2. All 4 structural bands are strictly disjoint and contained inside SafeArea
            RectTransform[] bands =
            {
                view.HeroBannerRect,
                view.TeamHeadersRect,
                view.ScoreboardViewportRect,
                view.BottomBarRect,
            };
            var bandRects = new Rect[bands.Length];
            for (int i = 0; i < bands.Length; i++)
            {
                bandRects[i] = GetScreenRect(bands[i], cam);
                AssertContainedInSafeRect(expectedSafePx, bandRects[i], $"{ctx} band '{bands[i].name}'");
            }
            for (int i = 0; i < bands.Length; i++)
            {
                for (int j = i + 1; j < bands.Length; j++)
                {
                    Assert.IsFalse(
                        Shrink(bandRects[i], 1f).Overlaps(Shrink(bandRects[j], 1f)),
                        $"{ctx}: band '{bands[i].name}' {bandRects[i]} overlaps '{bands[j].name}' {bandRects[j]}");
                }
            }

            // 3. Every active Button meets >= 48 dp, SafeArea containment, zero nesting, and zero pairwise overlap
            Button[] buttons = view.Root.GetComponentsInChildren<Button>(includeInactive: false);
            var btnRects = new List<(Button Btn, RectTransform Rt, Rect Px)>(buttons.Length);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button b = buttons[i];
                if (b == null || !b.enabled || !b.gameObject.activeInHierarchy) continue;
                var rt = b.transform as RectTransform;
                if (rt == null) continue;
                Rect px = GetScreenRect(rt, cam);
                AssertTouchTarget48Dp(px, minTouchPx, $"{ctx} Button '{rt.name}'");
                AssertContainedInSafeRect(expectedSafePx, px, $"{ctx} Button '{rt.name}'");
                btnRects.Add((b, rt, px));
            }
            Assert.AreEqual(11, btnRects.Count, $"{ctx}: expected 9 Honor buttons + HOME + PLAY AGAIN");

            for (int i = 0; i < btnRects.Count; i++)
            {
                for (int j = i + 1; j < btnRects.Count; j++)
                {
                    Assert.IsFalse(btnRects[i].Rt.IsChildOf(btnRects[j].Rt), $"{ctx}: nested Buttons");
                    Assert.IsFalse(btnRects[j].Rt.IsChildOf(btnRects[i].Rt), $"{ctx}: nested Buttons");
                    Assert.IsFalse(
                        Shrink(btnRects[i].Px, 1f).Overlaps(Shrink(btnRects[j].Px, 1f)),
                        $"{ctx}: Button '{btnRects[i].Rt.name}' {btnRects[i].Px} overlaps '{btnRects[j].Rt.name}' {btnRects[j].Px}");
                }
            }

            // 4. Zero dark-ink + dark-outline smudge and full text integrity
            AssertNoDarkInkDarkOutlineSmudge(view.Root, ctx);
            AssertTextIntegrity(view.Root, ctx);
        }

        static void ForceCanvasAndSafe(Canvas canvas, RectTransform root, Action onReflow)
        {
            Canvas.ForceUpdateCanvases();
            SafeArea[] safes = canvas.GetComponentsInChildren<SafeArea>(includeInactive: true);
            for (int i = 0; i < safes.Length; i++)
                safes[i].ForceApply();
            onReflow?.Invoke();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            Canvas.ForceUpdateCanvases();
        }

        static Rect GetScreenRect(RectTransform rt, Camera cam)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Canvas rootCanvas = rt.GetComponentInParent<Canvas>();
            Camera uiCam = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? (rootCanvas.worldCamera != null ? rootCanvas.worldCamera : cam)
                : null;

            float xMin = float.MaxValue;
            float yMin = float.MaxValue;
            float xMax = float.MinValue;
            float yMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(uiCam, corners[i]);
                if (sp.x < xMin) xMin = sp.x;
                if (sp.y < yMin) yMin = sp.y;
                if (sp.x > xMax) xMax = sp.x;
                if (sp.y > yMax) yMax = sp.y;
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        static Rect Shrink(Rect r, float px)
        {
            float dx = Mathf.Min(px, r.width * 0.25f);
            float dy = Mathf.Min(px, r.height * 0.25f);
            return Rect.MinMaxRect(r.xMin + dx, r.yMin + dy, r.xMax - dx, r.yMax - dy);
        }

        static Rect ComputeCenterSightbox(Rect safePx) =>
            new Rect(
                safePx.xMin + safePx.width * 0.275f,
                safePx.yMin + safePx.height * 0.25f,
                safePx.width * 0.45f,
                safePx.height * 0.50f);

        static void AssertContainedInSafeRect(Rect safePx, Rect childPx, string label)
        {
            const float tol = 1.5f;
            Assert.GreaterOrEqual(childPx.xMin, safePx.xMin - tol, $"{label} left ({childPx.xMin:F1}) < safe ({safePx.xMin:F1})");
            Assert.GreaterOrEqual(childPx.yMin, safePx.yMin - tol, $"{label} bottom ({childPx.yMin:F1}) < safe ({safePx.yMin:F1})");
            Assert.LessOrEqual(childPx.xMax, safePx.xMax + tol, $"{label} right ({childPx.xMax:F1}) > safe ({safePx.xMax:F1})");
            Assert.LessOrEqual(childPx.yMax, safePx.yMax + tol, $"{label} top ({childPx.yMax:F1}) > safe ({safePx.yMax:F1})");
        }

        static void AssertTouchTarget48Dp(Rect rect, float minTouchPx, string label)
        {
            const float tol = 0.75f;
            Assert.GreaterOrEqual(rect.width, minTouchPx - tol, $"{label} width {rect.width:F1}px < 48dp ({minTouchPx:F1}px)");
            Assert.GreaterOrEqual(rect.height, minTouchPx - tol, $"{label} height {rect.height:F1}px < 48dp ({minTouchPx:F1}px)");
        }

        static void AssertNoDarkInkDarkOutlineSmudge(Transform root, string context)
        {
            Text[] texts = root.GetComponentsInChildren<Text>(includeInactive: false);
            for (int i = 0; i < texts.Length; i++)
            {
                Text txt = texts[i];
                if (txt == null || !txt.enabled || !txt.gameObject.activeInHierarchy) continue;
                if (string.IsNullOrWhiteSpace(txt.text) || txt.color.a <= 0.05f) continue;

                float inkLum = Ui.Luminance(txt.color);
                if (inkLum >= 0.25f) continue;

                Outline[] outlines = txt.GetComponents<Outline>();
                for (int k = 0; k < outlines.Length; k++)
                {
                    Outline ol = outlines[k];
                    if (ol == null || !ol.enabled || ol.effectColor.a <= 0.05f) continue;
                    float outlineLum = Ui.Luminance(ol.effectColor);
                    Assert.GreaterOrEqual(
                        outlineLum,
                        0.35f,
                        $"{context}: dark-ink Text '{txt.name}' (\"{txt.text}\") has smudged dark Outline");
                }
            }
        }

        static void AssertTextIntegrity(Transform root, string context)
        {
            Text[] texts = root.GetComponentsInChildren<Text>(includeInactive: false);
            for (int i = 0; i < texts.Length; i++)
            {
                Text txt = texts[i];
                if (txt == null || !txt.enabled || !txt.gameObject.activeInHierarchy) continue;
                if (string.IsNullOrWhiteSpace(txt.text) || txt.color.a <= 0.05f) continue;

                Rect rect = txt.rectTransform.rect;
                Assert.GreaterOrEqual(rect.width, 8f, $"{context}: Text '{txt.name}' collapsed width={rect.width:F1}");
                Assert.GreaterOrEqual(rect.height, 8f, $"{context}: Text '{txt.name}' collapsed height={rect.height:F1}");

                if (txt.verticalOverflow == VerticalWrapMode.Truncate)
                {
                    string trimmed = txt.text.Replace("\r\n", "\n").Trim();
                    if (trimmed.Length == 0) continue;
                    TextGenerationSettings gen = txt.GetGenerationSettings(rect.size);
                    txt.cachedTextGenerator.Populate(trimmed, gen);
                    Assert.GreaterOrEqual(
                        txt.cachedTextGenerator.characterCount,
                        trimmed.Length,
                        $"{context}: Text '{txt.name}' truncated ({txt.cachedTextGenerator.characterCount}/{trimmed.Length}): \"{trimmed}\"");
                }
            }
        }

        static void AssertRespectfulLine(string line, string context)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(line), $"{context}: line must not be blank");
            string lower = line.ToLowerInvariant();
            string[] forbidden = { "ugly", "pangit", "poor", "mahirap", "stupid", "bobo", "fat", "taba", "loser" };
            for (int i = 0; i < forbidden.Length; i++)
                Assert.IsFalse(lower.Contains(forbidden[i]), $"{context}: disrespectful token '{forbidden[i]}' in \"{line}\"");
        }
    }
}
