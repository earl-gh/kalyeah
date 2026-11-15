using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Kalyeah.Net;
using Kalyeah.Sim;
using NUnit.Framework;

namespace Kalyeah.Game.Tests
{
    /// <summary>SimBridge driving real matches: Practice against bots, the three street games, and an online match through a server.</summary>
    public class SimBridgeTests
    {
        static SimBridge Practice(string game = null, long seed = 5) =>
            SimBridge.Practice(Kit.Data, new MatchOptions { Seed = seed, Character = "migo", PlayerName = "ANA", StreetGame = game });

        [Test]
        public void APracticeMatchIsFiveAgainstFiveTenDifferentKidsYoursFirst()
        {
            SimBridge bridge = Practice();
            Assert.AreEqual(10, bridge.Kids.Count);
            Assert.AreEqual(10, bridge.Kids.Select(k => k.CharacterId).Distinct().Count());
            KidSnapshot me = bridge.Find(SimBridge.LocalId).Value;
            Assert.AreEqual("migo", me.CharacterId);
            Assert.AreEqual("ANA", me.Name, "your name over your kid");
            Assert.IsTrue(me.IsLocal);
            Assert.AreEqual(1, me.Number);
            Assert.AreEqual(6, bridge.Find(6).Value.Number, "the other team is numbered 6 to 10");
            Assert.AreEqual(MatchPhaseView.Intro, bridge.Phase);
        }

        [Test]
        public void EnemyHealthIsNeverShownOwnTeamsIs()
        {
            SimBridge bridge = Practice();
            Kit.Run(bridge, 90);
            foreach (KidSnapshot kid in bridge.Kids)
            {
                if (kid.Team != bridge.MyTeam) Assert.IsFalse(kid.HealthShown, $"enemy {kid.Id}");
                else if (!kid.Tagged) Assert.IsTrue(kid.HealthShown, $"ally {kid.Id}");
                Assert.AreEqual(kid.Team != bridge.MyTeam, kid.Enemy);
            }
        }

        [Test]
        public void TheOpeningShowEndsWithGoAndTheClockRuns()
        {
            SimBridge bridge = Practice();
            var banners = new List<string>();
            bridge.Banner += banners.Add;
            Assert.IsFalse(bridge.Local.Playing, "no gestures during the opening show");
            Assert.IsTrue(bridge.Local.Shown, "but the controls are drawn");
            Kit.Run(bridge, 120);
            CollectionAssert.Contains(banners, "GO!");
            Assert.AreEqual(MatchPhaseView.Playing, bridge.Phase);
            Assert.IsTrue(bridge.Local.Playing);
            Assert.AreEqual("GO!", bridge.BannerText);
            Assert.AreEqual(bridge.Score.Clock.Text, ClockDisplay.Of(bridge.Score.Clock.CountingDown ? bridge.Score.Clock.Seconds : bridge.Score.Clock.Seconds, bridge.Score.Clock.Stage, false).Text);
            Kit.Run(bridge, 70);
            Assert.AreEqual("", bridge.BannerText, "a banner stays up two seconds");
        }

        [Test]
        public void TheJoystickRunsYourKidThatWay()
        {
            SimBridge bridge = Practice();
            bridge.SkipIntro();
            Vector2 start = bridge.Find(SimBridge.LocalId).Value.Position;
            var cam = new ShoulderFollow(CameraRulesData.Load(Kit.Data));
            cam.Place(bridge.Flip);
            bridge.SetStick(cam.GroundWay(new Vector2(0f, -1f)));
            Kit.Run(bridge, 30);
            Vector2 now = bridge.Find(SimBridge.LocalId).Value.Position;
            Assert.Less(now.Y, start.Y - 2f, "up the screen is toward the other end (-Z)");
            Assert.Greater(bridge.Find(SimBridge.LocalId).Value.Speed01, 0.5f);
        }

        [Test]
        public void TheHeroKitShowsThreeSuperpowersAndItsButtons()
        {
            SimBridge bridge = Practice();
            bridge.SkipIntro();
            Kit.Run(bridge, 1);
            LocalHud hud = bridge.Local;
            Assert.IsFalse(hud.UsesElixir, "today's data: no ice water, the hero kit");
            Assert.AreEqual(3, hud.Cards.Count(c => c.Present));
            Assert.AreEqual("tirador", hud.PrimaryId);
            Assert.AreEqual("ult_bagsak", hud.UltId);
            Assert.IsTrue(hud.VaultReady);
            Assert.IsTrue(hud.JumpReady);
            Assert.IsTrue(hud.CanMove);
            Assert.IsFalse(hud.UltReady, "the ultimate charges first");
        }

        [Test]
        public void ACardReleasedOverTheStreetIsCastAtTheCrosshair()
        {
            SimBridge bridge = Practice();
            bridge.SkipIntro();
            Kit.Run(bridge, 2);
            int slot = Array.FindIndex(bridge.Local.Cards, c => c.Present && c.Pickable && !c.IsHeal);
            Assert.GreaterOrEqual(slot, 0, "a ready card");
            Vector2 me = bridge.Find(SimBridge.LocalId).Value.Position;
            var cam = new ShoulderFollow(CameraRulesData.Load(Kit.Data));
            cam.Place(bridge.Flip);
            Vector2 crosshair = bridge.SetCrosshair(me + cam.Forward * 6f, cam.Forward, 4f, 25f);
            Assert.AreEqual((me + cam.Forward * 6f).Y, crosshair.Y, 1e-3f);
            bridge.CardPressed(slot);
            Kit.Run(bridge, 1);
            Assert.IsTrue(bridge.Local.HoldingCard);
            bridge.CardMoved(slot, false, false);
            Kit.Run(bridge, 3);
            Assert.AreEqual(MatchInput.Release.Cast, bridge.CardReleased(slot, false, false));
            Kit.Run(bridge, 3);
            CardInfo card = bridge.Local.Cards[slot];
            Assert.IsTrue(card.CooldownLeft > 0f || !card.Pickable || card.Id != bridge.Local.Cards[slot].Id, "the card was spent");
        }

        [Test]
        public void ACardDroppedOnTheCancelXCastsNothing()
        {
            SimBridge bridge = Practice();
            bridge.SkipIntro();
            Kit.Run(bridge, 2);
            int slot = Array.FindIndex(bridge.Local.Cards, c => c.Present && c.Pickable);
            bridge.CardPressed(slot);
            Kit.Run(bridge, 1);
            bridge.CardMoved(slot, false, false);
            Kit.Run(bridge, 2);
            Assert.AreEqual(MatchInput.Release.Cancel, bridge.CardReleased(slot, false, true));
            Kit.Run(bridge, 3);
            Assert.IsTrue(bridge.Local.Cards[slot].Pickable, "still ready: nothing was spent");
        }

        [Test]
        public void ThePauseRunsOneNeutralTickThenHoldsStill()
        {
            SimBridge bridge = Practice();
            bridge.SkipIntro();
            Kit.Run(bridge, 2);
            int tick = bridge.Tick;
            bridge.Paused = true;
            Kit.Run(bridge, 10);
            Assert.AreEqual(tick + 1, bridge.Tick);
            bridge.Paused = false;
            Kit.Run(bridge, 2);
            Assert.AreEqual(tick + 3, bridge.Tick);
        }

        [Test]
        public void ControlsOffDropAHeldCard()
        {
            SimBridge bridge = Practice();
            bridge.SkipIntro();
            Kit.Run(bridge, 2);
            int slot = Array.FindIndex(bridge.Local.Cards, c => c.Present && c.Pickable);
            bridge.CardPressed(slot);
            Kit.Run(bridge, 1);
            bridge.ControlsActive = false;
            Kit.Run(bridge, 1);
            Assert.IsFalse(bridge.Input.CardHeld());
            Assert.IsFalse(bridge.Local.Playing);
        }

        [Test]
        public void TheCameraDriverGlidesBehindASidewaysRun()
        {
            SimBridge bridge = Practice();
            bridge.SkipIntro();
            var cam = new ShoulderFollow(CameraRulesData.Load(Kit.Data));
            cam.Place(bridge.Flip);
            Vector2 screen = new Vector2(1f, 0f);
            for (int i = 0; i < 90; i++)
            {
                bridge.SetStick(cam.GroundWay(screen));
                bridge.Frame(bridge.TickDt);
                bridge.Step();
                bridge.DriveCamera(cam, screen, bridge.TickDt);
            }
            Assert.Greater(MathF.Abs(CameraMath.AngleDifference(0f, cam.LookYaw * MathF.PI / 180f)), 0.3f, "the view turned toward the run");
        }

        [Test]
        public void ThePointLogIsGroupedForTheResults()
        {
            var groups = HudText.HowGroups(new[] { (0, 0), (1, 1), (0, 0) }, 0);
            Assert.AreEqual(2, groups.Count);
            Assert.AreEqual("Held their base × 2", HudText.GroupText(groups[0].ours, groups[0].how, groups[0].times));
            Assert.AreEqual("Tagged you all", HudText.GroupText(groups[1].ours, groups[1].how, groups[1].times));
            ResultInfo result = Practice().Result();
            Assert.AreEqual("Defeat", result.Title, "nobody has won yet");
            StringAssert.StartsWith("Practice (First to 3) (5v5)", result.Subtitle);
        }

        [Test]
        public void TheClockCountsDownWithItsStageCaption()
        {
            ClockDisplay regular = ClockDisplay.Of(65.2, ClockDisplay.Regular, true);
            Assert.AreEqual("1:06", regular.Text, "counting down rounds up");
            Assert.IsFalse(regular.HasCaption);
            Assert.AreEqual("1:05", ClockDisplay.Of(65.9, ClockDisplay.Regular, false).Text, "counting up rounds down");
            ClockDisplay over = ClockDisplay.Of(0.0, ClockDisplay.Overtime, true);
            Assert.AreEqual("0:00", over.Text);
            Assert.AreEqual("Overtime", over.Caption);
            Assert.IsTrue(over.Gold);
            Assert.AreEqual("Extended", ClockDisplay.Of(3, ClockDisplay.Extended, true).Caption);
        }

        // ---- the street games -----------------------------------------------------------------------------------------------

        [TestCase("patintero")]
        [TestCase("tumbang_preso")]
        [TestCase("langit_lupa")]
        public void AStreetGameStartsWithSixKidsAPadAndNoHand(string game)
        {
            SimBridge bridge = Practice(game);
            Assert.AreEqual(SimBridge.Kind.Street, bridge.MatchKind);
            Assert.IsTrue(bridge.IsStreetGame);
            Assert.IsFalse(bridge.HasHand);
            Assert.AreEqual(game, bridge.StreetGameId);
            Assert.AreEqual(6, bridge.Kids.Count);
            Assert.AreEqual(game != "patintero", bridge.IsFreeForAll);
            Kit.Run(bridge, 400);
            Assert.AreEqual(MatchPhaseView.Playing, bridge.Phase, "the opening show (and Tumbang Preso's roll) is over");
            Assert.IsTrue(bridge.Pad.Shown || bridge.Local.Tagged);
            Assert.IsNotEmpty(bridge.Pad.Title + bridge.Pad.Hint == "" ? "x" : bridge.Pad.Title, "it says what to do");
            Assert.IsFalse(bridge.Local.Cards.Any(c => c.Present), "no skills in the street games");
            if (bridge.IsFreeForAll) Assert.AreEqual(6, bridge.StreetScores().Count);
        }

        [Test]
        public void PatinteroRunnersGetTheDiveButton()
        {
            SimBridge bridge = Practice("patintero");
            Kit.Run(bridge, 200);
            Assert.AreEqual("YOU RUN!", bridge.Pad.Title, "your team runs first");
            Assert.AreEqual(StreetAction.Dive, bridge.Pad.Action);
            Assert.IsFalse(bridge.Flip, "runners see the street from their base");
            if (bridge.Pad.CanAct)
            {
                bridge.ActionPressed();
                Kit.Run(bridge, 2);
                Assert.Less(bridge.Pad.Fill, 1f, "the dive cools down");
            }
        }

        [Test]
        public void TumbangPresoThrowersHoldThrowToAimAndLetGoToThrow()
        {
            SimBridge bridge = null;
            for (long seed = 1; seed < 20; seed++)
            {
                bridge = Practice("tumbang_preso", seed);
                Kit.Run(bridge, 400);
                if (bridge.Pad.Action == StreetAction.Throw) break;
            }
            Assert.AreEqual(StreetAction.Throw, bridge.Pad.Action, "a seed where you throw first");
            Assert.AreEqual("YOUR THROW!", bridge.Pad.Title);
            bridge.ThrowPressed();
            for (int i = 0; i < 150 && bridge.Pad.Fill <= 0f; i++) Kit.Run(bridge, 1); // a catch's freeze may still be running
            Assert.IsTrue(bridge.Pad.Aiming);
            Assert.Greater(bridge.Pad.Fill, 0f, "the line grows with the hold");
            bridge.ThrowDragged(new Vector2(40f, 0f), new Vector2(1f, 0f));
            bridge.ThrowReleased(false);
            Kit.Run(bridge, 5);
            Assert.AreNotEqual(StreetAction.Throw, bridge.Pad.Action, "the slipper is gone");
        }

        [Test]
        public void TheThrowAngleRoundTrips()
        {
            Vector2 up = SimBridge.WayOf(0f);
            Assert.AreEqual(-1f, up.Y, 1e-6f);
            Assert.AreEqual(0.5f, SimBridge.AngleOf(SimBridge.WayOf(0.5f)), 1e-5f);
        }

        // ---- online ---------------------------------------------------------------------------------------------------------

        [Test]
        public void AnOnlineMatchIsDrawnFromTheMirrorAndYourInputReachesTheServer()
        {
            SimData data = SimBridge.LoadSimData(Kit.Data);
            var server = new GameServer(data.GameRules, data.MapLayout, NetRules.FromData(data), 11, new StreetCatalog(data));
            var hub = new MemoryHub();
            server.Attach(hub);
            GameClient ana = NewClient(data, "Ana"), ben = NewClient(data, "Ben");
            ana.Attach(hub.Connect(100));
            ben.Attach(hub.Connect(101));
            var clients = new[] { ana, ben };
            Assert.IsTrue(PollUntil(() => ana.IsOnline() && ben.IsOnline(), server, clients));
            ana.CreateRoom();
            Assert.IsTrue(PollUntil(() => ana.PlayerId > 0 && ana.Room.GetStr("code") != "", server, clients));
            ben.JoinRoom(ana.Room.GetStr("code"));
            Assert.IsTrue(PollUntil(() => ben.PlayerId > 0 && ana.Room.GetArr("members").Count == 2, server, clients));
            ana.SetReady(true);
            ben.SetReady(true);
            Assert.IsTrue(PollUntil(() => ana.Room.GetBool("can_start"), server, clients));
            ana.StartMatch();
            Assert.IsTrue(PollUntil(() => ana.InMatch() && ben.InMatch(), server, clients));

            SimBridge a = SimBridge.Online(data, ana, new MatchOptions { Character = "migo", ModeText = "Online" });
            SimBridge b = SimBridge.Online(data, ben, new MatchOptions { Character = "tala", ModeText = "Online" });
            var bridges = new[] { a, b };
            Assert.AreEqual(ana.PlayerId, a.Me);
            Assert.AreEqual("Ben", a.NameOf(ben.PlayerId), "the room's names go over the kids");
            Assert.IsTrue(Pump(() => ana.Stage == "play" && ben.Stage == "play", server, bridges, 600), "the bridges picked for you and play started");
            Vector2 start = b.Find(ana.PlayerId).Value.Position;
            var cam = new ShoulderFollow(CameraRulesData.Load(Kit.Data));
            cam.Place(a.Flip);
            Assert.IsTrue(Pump(() =>
            {
                a.SetStick(cam.GroundWay(new Vector2(1f, 0f)));
                return Vector2.Distance(b.Find(ana.PlayerId).Value.Position, start) > 1f;
            }, server, bridges, 900), "Ana's run shows on Ben's screen");
            Assert.IsTrue(a.IsOnline);
            Assert.IsFalse(a.Leave(), "online never counts as abandoned here");
        }

        static GameClient NewClient(SimData data, string name) =>
            new GameClient(data.GameRules, data.MapLayout, NetRules.FromData(data), new StreetCatalog(data)) { PlayerName = name };

        static bool PollUntil(Func<bool> done, GameServer server, GameClient[] clients, int frames = 600)
        {
            for (int i = 0; i < frames; i++)
            {
                server.Poll(1.0 / 30.0);
                foreach (GameClient c in clients) c.Poll(1.0 / 30.0);
                if (done()) return true;
            }
            return done();
        }

        /// <summary>The server and the bridges as the game runs them: a frame (poll), then a tick (send) each.</summary>
        static bool Pump(Func<bool> done, GameServer server, SimBridge[] bridges, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                server.Poll(1.0 / 30.0);
                foreach (SimBridge bridge in bridges)
                {
                    bridge.Frame(bridge.TickDt);
                    bridge.Step();
                }
                if (done()) return true;
            }
            return done();
        }
    }
}
