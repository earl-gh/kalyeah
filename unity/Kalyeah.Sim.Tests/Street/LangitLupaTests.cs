using System.Collections.Generic;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Street.StreetKit;

namespace Kalyeah.Sim.Tests.Street
{
    /// <summary>Langit Lupa (port of tests/unit/test_langit_lupa.gd).</summary>
    public class LangitLupaTests
    {
        const int Taya = 4;

        static GameRules TiringRules()
        {
            GameRules rules = Kit.Rules();
            rules.StaminaEnabled = true;
            return rules;
        }

        /// <summary>Four kids, ids 1..4, kid 4 the taya, every kid far from the others on the street (none on a langit).</summary>
        static MatchSim NewLangit(StreetModeRules rules = null)
        {
            var sim = new MatchSim(TiringRules(), Kit.Layout, 7);
            for (int i = 0; i < 4; i++) sim.AddPlayer(1 + i, 0);
            sim.StartMode(StreetMode.Create(rules ?? LangitRules));
            LangitLupa game = Game(sim);
            game.TayaId = Taya;
            game.PlaceTeams(sim);
            game.GraceLeft = 0.0;
            Vec2[] spots = { new Vec2(-10.0, 22.0), new Vec2(10.0, 22.0), new Vec2(-10.0, -22.0), new Vec2(10.0, -22.0) };
            for (int i = 0; i < 4; i++)
            {
                sim.Players[1 + i].Position = spots[i];
                sim.EndMove(sim.Players[1 + i]);
            }
            foreach (PlayerState state in sim.Players.Values) Assert.IsFalse(game.OnLangit(state), $"kid {state.Id} starts on the street");
            return sim;
        }

        static LangitLupa Game(MatchSim sim) => (LangitLupa)sim.Mode;

        /// <summary>test_langit_lupa.gd `_wait`: ceil(seconds / dt - 0.001) ticks.</summary>
        static void WaitL(MatchSim sim, double seconds) => Kit.Steps(sim, GMath.Ceili(seconds / DT - 0.001));

        static Vec2 SpotCenter(int index) => LangitRules.LangitSpots[index].GetCenter();

        static StreetModeRules With(System.Action<StreetModeRules> change)
        {
            StreetModeRules rules = LangitRules.Duplicate();
            change(rules);
            return rules;
        }

        static bool Encloses(Rect2 outer, Rect2 inner) =>
            inner.Position.X >= outer.Position.X && inner.Position.Y >= outer.Position.Y
            && inner.Position.X + inner.Size.X <= outer.Position.X + outer.Size.X && inner.Position.Y + inner.Size.Y <= outer.Position.Y + outer.Size.Y;

        [Test]
        public void LangitLupaIsAStreetGameOfItsOwnWithOneTayaAndPointsPerKid()
        {
            StreetModeRules rules = StreetMode.RulesFor(Kit.Data, "langit_lupa");
            Assert.AreEqual("Langit Lupa", rules.Title);
            Assert.IsInstanceOf<LangitLupa>(StreetMode.Create(rules));
            Assert.IsInstanceOf<TayaGame>(StreetMode.Create(rules), "it shares the one-taya scoring with Tumbang Preso");
            Assert.IsInstanceOf<TayaGame>(StreetMode.Create(StreetMode.RulesFor(Kit.Data, "tumbang_preso")));
            Assert.Greater(rules.LangitSpots.Count, 3, "a few platforms on the street");
            foreach (Rect2 spot in rules.LangitSpots) Assert.IsTrue(Encloses(Kit.Layout.LaneRect(), spot), $"every platform is on the street: {spot}");
            MatchSim sim = NewLangit();
            Assert.IsTrue(sim.Mode is StreetMode m && m.IsFreeForAll());
            Assert.AreEqual(4, Game(sim).Points.Count);
        }

        [Test]
        public void TheTayaStartsInTheMiddleAndTheOthersOnARingAndOnlyTheOthersTire()
        {
            var sim = new MatchSim(TiringRules(), Kit.Layout, 3);
            for (int i = 0; i < 4; i++) sim.AddPlayer(1 + i, 0);
            sim.StartMode(StreetMode.Create(LangitRules));
            LangitLupa game = Game(sim);
            Assert.IsTrue(sim.Players.ContainsKey(game.TayaId), "the first taya is one of the kids, drawn at random");
            foreach (PlayerState state in sim.Players.Values)
            {
                if (state.Id == game.TayaId)
                {
                    Assert.AreEqual(Vec2.Zero, state.Position);
                    Assert.IsFalse(state.HasStamina, "the taya never tires");
                }
                else
                {
                    Assert.AreEqual(LangitRules.RingRadius, state.Position.Length(), 0.01);
                    Assert.IsTrue(state.HasStamina, "everyone else has stamina");
                }
            }
        }

        [Test]
        public void TheTayaTagsAKidOnTheStreetWhoBecomesTheTayaAndTheRoundResets()
        {
            MatchSim sim = NewLangit();
            LangitLupa game = Game(sim);
            var changes = new List<(int, int)>();
            game.TayaChanged += (old, now) => changes.Add((old, now));
            sim.Players[1].Position = new Vec2(3.0, 3.0);
            sim.Players[Taya].Position = new Vec2(3.0, 4.0);
            sim.Step(DT);
            CollectionAssert.AreEqual(new[] { (Taya, 1) }, changes, "kid 1 is the taya now");
            Assert.AreEqual(LangitRules.TagPoints, game.Points[Taya], "the tagger's bonus");
            Assert.IsTrue(game.IsResetting());
            Assert.IsFalse(sim.Players[Taya].HasStamina, "nothing changes until the round resets");
            WaitL(sim, LangitRules.ResetSeconds + DT);
            Assert.IsFalse(game.IsResetting());
            Assert.AreEqual(Vec2.Zero, sim.Players[1].Position, "the new taya starts in the middle");
            Assert.IsFalse(sim.Players[1].HasStamina);
            Assert.IsTrue(sim.Players[Taya].HasStamina, "the old taya tires like everyone else now");
        }

        [Test]
        public void AKidOnAPlatformIsSafeUntilItsSafeTimeRunsOutAndTheStreetRefillsIt()
        {
            MatchSim sim = NewLangit();
            LangitLupa game = Game(sim);
            PlayerState kid = sim.Players[1];
            kid.Position = SpotCenter(2);
            sim.Players[Taya].Position = SpotCenter(2) + new Vec2(0.0, 1.0);
            WaitL(sim, LangitRules.LangitSeconds - 0.2);
            Assert.AreEqual(Taya, game.TayaId, "safe on the langit");
            Assert.IsTrue(game.IsSafe(kid));
            WaitL(sim, 0.3);
            Assert.AreEqual(1, game.TayaId, "its safe time ran out: the langit no longer saves it");
            MatchSim other = NewLangit();
            LangitLupa g = Game(other);
            g.SafeLeft[2] = 0.0;
            other.Players[2].Position = new Vec2(-4.0, 10.0);
            WaitL(other, 1.0);
            Assert.AreEqual(LangitRules.LangitRefillPerS * 1.0, g.SafeLeft[2], 0.05, "the safe time comes back on the street");
        }

        [Test]
        public void APlatformWearsOutUnderTheKidsAndBreaksUntilTheNextRound()
        {
            MatchSim sim = NewLangit(With(r => r.LangitSeconds = 100.0));
            LangitLupa game = Game(sim);
            var broke = new List<int>();
            game.SpotBroke += index => broke.Add(index);
            sim.Players[1].Position = SpotCenter(0) + new Vec2(-0.5, 0.0);
            sim.Players[2].Position = SpotCenter(0) + new Vec2(0.5, 0.0);
            WaitL(sim, LangitRules.LangitWearSeconds / 2.0 - 0.2);
            Assert.IsTrue(game.SpotStanding(0), "two kids wear it twice as fast, not yet broken");
            Assert.Greater(game.WearShare(0), 0.9);
            WaitL(sim, 0.3);
            CollectionAssert.AreEqual(new[] { 0 }, broke);
            Assert.IsFalse(game.SpotStanding(0));
            Assert.IsFalse(game.OnLangit(sim.Players[1]), "a broken platform is just street");
            game.PlaceTeams(sim);
            Assert.IsTrue(game.SpotStanding(0), "back for the next round");
        }

        [Test]
        public void TheLupaButtonRaisesAMoundUnderTheKidThatSavesItAndSinksAndWaits()
        {
            MatchSim sim = NewLangit();
            LangitLupa game = Game(sim);
            PlayerState kid = sim.Players[1];
            kid.Position = new Vec2(4.0, 12.0);
            Assert.IsTrue(game.CanRaise(kid));
            Assert.IsFalse(game.CanRaise(sim.Players[Taya]), "the taya raises none");
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnCard2, sim.Tick));
            sim.Step(DT);
            Assert.IsTrue(game.Mounds.ContainsKey(1));
            Assert.IsTrue(game.OnLangit(kid));
            Assert.IsFalse(game.CanRaise(kid), "one at a time: it waits `mound_cooldown`");
            sim.Step(DT);
            Assert.AreEqual(LangitRules.MoundCooldown - DT * 2.0, game.MoundWait[1], 0.001, "a held button raises no second mound");
            sim.SetInput(1, null);
            sim.Players[Taya].Position = kid.Position + new Vec2(0.0, 1.0);
            WaitL(sim, 1.0);
            Assert.AreEqual(Taya, game.TayaId, "safe on its mound");
            sim.Players[Taya].Position = new Vec2(10.0, -22.0);
            WaitL(sim, LangitRules.MoundSeconds);
            Assert.IsFalse(game.Mounds.ContainsKey(1), "the mound sank");
            Assert.IsFalse(game.OnLangit(kid));
            WaitL(sim, LangitRules.MoundCooldown - LangitRules.MoundSeconds);
            Assert.IsTrue(game.CanRaise(kid), "ready again");
        }

        [Test]
        public void TheLupaButtonWaitsWhileTheSafeTimeIsUsedUpSoNoMoundIsWasted()
        {
            MatchSim sim = NewLangit();
            LangitLupa game = Game(sim);
            PlayerState kid = sim.Players[1];
            game.SafeLeft[1] = 0.0;
            Assert.IsFalse(game.CanRaise(kid), "no safe time: a mound would save nothing");
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnCard2, sim.Tick));
            sim.Step(DT);
            Assert.IsFalse(game.Mounds.ContainsKey(1), "the press raises nothing");
            Assert.AreEqual(0.0, game.MoundWait.GetOrDefault(1, 0.0), "and the wait does not start");
            sim.SetInput(1, null);
            WaitL(sim, 0.5);
            Assert.IsTrue(game.CanRaise(kid), "the street gives some back: ready again");
        }

        [Test]
        public void StandingOnALangitNeverRefillsStaminaButTheStreetDoes()
        {
            MatchSim sim = NewLangit(With(r => r.LangitSeconds = 100.0));
            PlayerState kid = sim.Players[1];
            kid.Position = SpotCenter(5);
            kid.Stamina = 20.0;
            WaitL(sim, 3.0);
            Assert.AreEqual(20.0, kid.Stamina, "no rest in heaven");
            kid.Position = new Vec2(-4.0, 12.0);
            WaitL(sim, 3.0);
            Assert.Greater(kid.Stamina, 20.0, "the street refills it");
            MatchSim off = NewLangit(With(r =>
            {
                r.LangitSeconds = 100.0;
                r.LangitBlocksRest = false;
            }));
            off.Players[1].Position = SpotCenter(5);
            off.Players[1].Stamina = 20.0;
            WaitL(off, 3.0);
            Assert.Greater(off.Players[1].Stamina, 20.0, "the data toggle turns it off");
        }

        [Test]
        public void EveryKidButTheTayaEarnsAPointForEachStretchItStaysFree()
        {
            MatchSim sim = NewLangit();
            LangitLupa game = Game(sim);
            WaitL(sim, LangitRules.SurviveSeconds * 2.0 + DT);
            foreach (int id in new[] { 1, 2, 3 }) Assert.AreEqual(2, game.Points[id], $"kid {id}");
            Assert.AreEqual(0, game.Points[Taya], "the taya earns none");
        }

        [Test]
        public void AfterTheTimeTheKidWithTheMostPointsWins()
        {
            MatchSim sim = NewLangit();
            LangitLupa game = Game(sim);
            game.Points[2] = 5;
            game.TimeLeft = DT;
            sim.Step(DT);
            Assert.AreEqual(MatchSim.MatchPhase.MatchOver, sim.Phase);
            Assert.AreEqual(2, game.WinnerId);
            Assert.IsTrue(game.LocalResult(sim, 2).Won);
        }

        [Test]
        public void BrownoutsComeOnAFixedClockAndEachKidSeesOnlyTheKidsNearIt()
        {
            MatchSim sim = NewLangit();
            LangitLupa game = Game(sim);
            StreetModeRules r = LangitRules;
            game.TimeLeft = r.MatchSeconds - (r.BrownoutFirst - 1.0);
            Assert.IsFalse(game.IsBrownout());
            game.TimeLeft = r.MatchSeconds - (r.BrownoutFirst + 1.0);
            Assert.IsTrue(game.IsBrownout());
            Assert.AreEqual(r.BrownoutSeconds - 1.0, game.BrownoutLeft(), 0.001);
            game.TimeLeft = r.MatchSeconds - (r.BrownoutFirst + r.BrownoutSeconds + 1.0);
            Assert.IsFalse(game.IsBrownout(), "the lights come back");
            game.TimeLeft = r.MatchSeconds - (r.BrownoutFirst + r.BrownoutEvery + 1.0);
            Assert.IsTrue(game.IsBrownout(), "and go out again a while later");
            sim.Players[1].Position = Vec2.Zero;
            sim.Players[2].Position = new Vec2(r.BrownoutSight - 0.5, 0.0);
            sim.Players[3].Position = new Vec2(-r.BrownoutSight - 0.5, 0.0);
            Assert.IsTrue(game.VisibleTo(sim, 1, 1));
            Assert.IsTrue(game.VisibleTo(sim, 1, 2), "near: seen");
            Assert.IsFalse(game.VisibleTo(sim, 1, 3), "far: not seen");
            CollectionAssert.AreEqual(new[] { 3, Taya }, game.HiddenFrom(sim, 1));
            game.TimeLeft = r.MatchSeconds;
            CollectionAssert.IsEmpty(game.HiddenFrom(sim, 1), "with the lights on everyone sees everyone");
        }

        [Test]
        public void APhoneThatCannotSeeAKidIsNotToldWhereItsMoundIs()
        {
            MatchSim sim = NewLangit();
            LangitLupa game = Game(sim);
            game.RaiseMound(sim, sim.Players[3]);
            game.RaiseMound(sim, sim.Players[2]);
            OrderedDict<string, object> data = game.FilterFor(game.Snapshot(), new[] { 3 });
            var mounds = (OrderedDict<int, Vec2>)data["mounds"];
            Assert.IsFalse(mounds.ContainsKey(3));
            Assert.IsTrue(mounds.ContainsKey(2));
            Assert.IsFalse(((OrderedDict<int, double>)data["mound_left"]).ContainsKey(3));
            Assert.IsTrue(game.Mounds.ContainsKey(3), "the server's own game keeps it");
            var copy = (LangitLupa)StreetMode.Create(LangitRules);
            data["unseen"] = new List<int> { 3 };
            copy.Restore(data);
            Assert.IsTrue(copy.Unseen.Contains(3));
            copy.Restore(game.Snapshot());
            Assert.IsEmpty(copy.Unseen, "a snapshot without the list: everyone is seen");
        }

        [Test]
        public void BotsPlayAWholeMatchTheTayaChangesHandsAndTheyUseTheLangitAndMounds()
        {
            var sim = new MatchSim(TiringRules(), Kit.Layout, 11);
            for (int i = 0; i < 6; i++) sim.AddPlayer(1 + i, 0);
            sim.StartMode(StreetMode.Create(LangitRules));
            LangitLupa game = Game(sim);
            var coach = new StreetModeCoach();
            foreach (int id in sim.Players.Keys) coach.Add(id);
            var swaps = new List<int>();
            var raised = new List<int>();
            int safeTicks = 0;
            game.RolesSwapped += who => swaps.Add(who);
            game.MoundRaised += id => raised.Add(id);
            while (sim.Phase != MatchSim.MatchPhase.MatchOver && sim.MatchTime < LangitRules.MatchSeconds + 120.0)
            {
                coach.Drive(sim);
                sim.Step(DT);
                foreach (PlayerState state in sim.Players.Values)
                    if (game.IsSafe(state)) safeTicks += 1;
            }
            TestContext.WriteLine($"Langit Lupa bots: {swaps.Count} changes of taya, {raised.Count} mounds, {safeTicks * DT:F0} kid-seconds safe");
            Assert.AreEqual(MatchSim.MatchPhase.MatchOver, sim.Phase, "the match ends");
            Assert.Greater(swaps.Count, 3, "the taya changes hands");
            Assert.Greater(raised.Count, 0, "the bots raise mounds");
            Assert.Greater(safeTicks * DT, 30.0, "the bots climb the langit");
        }
    }
}
