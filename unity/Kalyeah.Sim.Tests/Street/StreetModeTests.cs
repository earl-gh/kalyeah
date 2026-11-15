using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Street.StreetKit;

namespace Kalyeah.Sim.Tests.Street
{
    /// <summary>What every street game shares (the "both" part of tests/unit/test_street_modes.gd) and the snapshot a client's copy gets.</summary>
    public class StreetModeTests
    {
        [Test]
        public void EachIdMakesItsGameAndAgawangBaseHasNone()
        {
            Assert.IsInstanceOf<TumbangPreso>(StreetMode.Create(TumbangRules));
            Assert.IsInstanceOf<Kalyeah.Sim.Patintero>(StreetMode.Create(PatinteroRules));
            Assert.IsInstanceOf<LangitLupa>(StreetMode.Create(LangitRules));
            Assert.IsNull(StreetMode.RulesFor(Kit.Data, "street"), "Agawang Base is the base game");
            Assert.IsFalse(StreetMode.Create(PatinteroRules).IsFreeForAll());
            Assert.IsTrue(StreetMode.Create(TumbangRules).IsFreeForAll());
        }

        [Test]
        public void ThereAreNoSkillsAndNoDamageInTheStreetGames()
        {
            foreach (StreetModeRules modeRules in new[] { TumbangRules, PatinteroRules })
            {
                MatchSim sim = NewMatch(modeRules, 3);
                sim.Damage(1, 500);
                Assert.IsFalse(sim.Players[1].Tagged, $"{modeRules.Title}: damage never tags");
                sim.Players[1].Elixir = 10.0;
                for (int slot = 0; slot < sim.Players[1].Weapons.Count; slot++)
                    Assert.IsFalse(sim.WeaponReady(sim.Players[1], slot), $"{modeRules.Title}: no card is ready");
                sim.SetInput(1, PlayerInput.Create(Vec2.Zero, new Vec2(0.0, -1.0), PlayerInput.BtnCard3 | PlayerInput.BtnCard4, 0));
                Kit.Steps(sim, 10);
                Assert.AreEqual(0, sim.Weapons.Projectiles.Count + sim.Weapons.Zones.Count + sim.Weapons.Shields.Count, $"{modeRules.Title}: nothing cast");
                Assert.IsFalse(sim.Players[1].HasStamina, $"{modeRules.Title}: nobody tires");
            }
        }

        [Test]
        public void BotsPlayBothGamesAndThePointsAndRolesChangeHands()
        {
            MatchSim tumbang = NewMatch(TumbangRules, 6);
            MatchSim patintero = NewMatch(PatinteroRules, 3);
            foreach (MatchSim sim in new[] { tumbang, patintero })
            {
                var coach = new StreetModeCoach();
                foreach (int id in sim.Players.Keys) coach.Add(id);
                var swaps = new List<int>();
                var mode = (StreetMode)sim.Mode;
                mode.RolesSwapped += who => swaps.Add(who);
                while (sim.Phase != MatchSim.MatchPhase.MatchOver && sim.MatchTime < mode.Rules.MatchSeconds + 120.0)
                {
                    coach.Drive(sim);
                    sim.Step(DT);
                }
                TestContext.WriteLine($"{mode.Rules.Title}: {swaps.Count} changes of roles");
                Assert.Greater(swaps.Count, 1, $"{mode.Rules.Title}: the roles change hands");
            }
            Assert.Greater(((TumbangPreso)tumbang.Mode).Points.Sum(p => p.Value), 2, "Tumbang Preso: the bots score");
            Assert.Greater(patintero.Score.Points[0] + patintero.Score.Points[1], 1, "Patintero: the bots score");
        }

        /// <summary>A client's copy written from a snapshot holds the same game (the tables are copies, not the server's own).</summary>
        [Test]
        public void ASnapshotRestoresIntoAClientsCopy()
        {
            foreach (StreetModeRules rules in new[] { TumbangRules, PatinteroRules, LangitRules })
            {
                MatchSim sim = NewMatch(rules, 3);
                var coach = new StreetModeCoach();
                foreach (int id in sim.Players.Keys) coach.Add(id);
                for (int i = 0; i < 900; i++)
                {
                    coach.Drive(sim);
                    sim.Step(DT);
                }
                var server = (StreetMode)sim.Mode;
                OrderedDict<string, object> data = server.Snapshot();
                CollectionAssert.AreEqual(server.SyncedFields(), data.Keys.Where(k => k != "flights").ToList(), rules.Title);
                StreetMode copy = StreetMode.Create(rules);
                copy.Restore(data, sim);
                OrderedDict<string, object> again = copy.Snapshot();
                foreach (var pair in data) AssertSame(pair.Value, again[pair.Key], $"{rules.Title}.{pair.Key}");
                if (copy is TumbangPreso tp)
                    foreach (var flight in ((TumbangPreso)server).Flights) Assert.AreSame(flight.Value, tp.Flights[flight.Key], "the copy's slipper is the sim's projectile");
            }
        }

        [Test]
        public void ARestoreSkipsAFieldOfAnotherTypeAndAMissingOne()
        {
            var copy = (Kalyeah.Sim.Patintero)StreetMode.Create(PatinteroRules);
            var data = new OrderedDict<string, object>();
            data["attack_team"] = "one";
            data["time_left"] = 12.5;
            data["jailed"] = new OrderedDict<int, double>();
            data["caught"] = CopyWith(3, 0.5);
            copy.Jailed[7] = 1;
            copy.Restore(data);
            Assert.AreEqual(0, copy.AttackTeam, "a string is not a team");
            Assert.AreEqual(12.5, copy.TimeLeft);
            Assert.AreEqual(1, copy.Jailed[7], "a table of another type is skipped");
            Assert.AreEqual(0.5, copy.Caught[3]);
            ((OrderedDict<int, double>)data["caught"])[3] = 9.0;
            Assert.AreEqual(0.5, copy.Caught[3], "the copy owns its table");
        }

        static OrderedDict<int, double> CopyWith(int key, double value)
        {
            var table = new OrderedDict<int, double>();
            table[key] = value;
            return table;
        }

        static void AssertSame(object want, object got, string at)
        {
            if (want is System.Collections.IEnumerable wantItems && !(want is string))
            {
                var a = wantItems.Cast<object>().ToList();
                var b = ((System.Collections.IEnumerable)got).Cast<object>().ToList();
                CollectionAssert.AreEqual(a, b, at);
                Assert.AreNotSame(want, got, $"{at}: a copy");
                return;
            }
            Assert.AreEqual(want, got, at);
        }
    }
}
