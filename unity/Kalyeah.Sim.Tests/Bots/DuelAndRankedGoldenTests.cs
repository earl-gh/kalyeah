using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Kalyeah.Sim.Tests.Bots
{
    /// <summary>BalanceDuel and RankedSystem against the Godot game's own results (unity/Golden/Bots/balance_duel.json, ranked.json).</summary>
    public class DuelAndRankedGoldenTests
    {
        static JsonDocument Load(string name) => JsonDocument.Parse(File.ReadAllText(BotGoldenTests.Golden(name)));

        static List<string> Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()).ToList();

        [Test]
        public void EveryDuelEndsLikeInGodot()
        {
            using JsonDocument doc = Load("balance_duel.json");
            int count = 0;
            foreach (JsonElement duel in doc.RootElement.GetProperty("results").EnumerateArray())
            {
                List<string> first = Strings(duel.GetProperty("first"));
                List<string> second = Strings(duel.GetProperty("second"));
                string what = $"{string.Join("+", first)} vs {string.Join("+", second)} seed {duel.GetProperty("seed").GetInt64()}";
                BalanceDuel.Result result = BalanceDuel.Run(Kit.Rules(), Kit.Layout, first, second, duel.GetProperty("seed").GetInt64(),
                    duel.GetProperty("seconds").GetDouble(), duel.GetProperty("skill").GetDouble(), duel.GetProperty("cover").GetBoolean());
                Assert.AreEqual(duel.GetProperty("winner").GetInt32(), result.Winner, what + ": winner");
                Assert.AreEqual(duel.GetProperty("at").GetDouble(), result.Seconds, 1e-9, what + ": seconds");
                Assert.AreEqual(duel.GetProperty("hp")[0].GetInt32(), result.Hp[0], what + ": hp of the first");
                Assert.AreEqual(duel.GetProperty("hp")[1].GetInt32(), result.Hp[1], what + ": hp of the second");
                count++;
            }
            Assert.GreaterOrEqual(count, 7);
        }

        [Test]
        public void TheLoadoutListIsGodots()
        {
            using JsonDocument doc = Load("balance_duel.json");
            var want = doc.RootElement.GetProperty("loadouts").EnumerateArray().Select(p => string.Join("+", Strings(p))).ToList();
            CollectionAssert.AreEqual(want, BalanceDuel.AllLoadouts(Kit.Rules()).Select(p => string.Join("+", p)).ToList());
        }

        /// <summary>One duel stepped tick by tick: both fighters' positions, hp and inputs, and every signal.</summary>
        [Test]
        public void ADuelPlaysTickByTickLikeInGodot()
        {
            using JsonDocument doc = Load("balance_duel.json");
            JsonElement trace = doc.RootElement.GetProperty("trace");
            JsonElement duel = doc.RootElement.GetProperty("results")[trace.GetProperty("duel").GetInt32()];
            GameRules rules = Kit.Rules();
            var sim = new MatchSim(rules, BalanceDuel.Arena(Kit.Layout), duel.GetProperty("seed").GetInt64());
            var top = new Vec2(0.0, -BalanceDuel.StartDistance / 2.0);
            var bottom = new Vec2(0.0, BalanceDuel.StartDistance / 2.0);
            FightBrain low = FightBrain.Fighter(bottom);
            FightBrain high = FightBrain.Fighter(top);
            low.Skill = duel.GetProperty("skill").GetDouble();
            high.Skill = low.Skill;
            sim.AddDummy(1, 0, bottom, low);
            sim.AddDummy(2, 1, top, high);
            List<string> first = Strings(duel.GetProperty("first"));
            List<string> second = Strings(duel.GetProperty("second"));
            sim.SetLoadout(1, first[0], first[1]);
            sim.SetLoadout(2, second[0], second[1]);
            var events = new List<string>();
            BotGoldenTests.Hook(sim, events);
            int t = 0;
            foreach (JsonElement row in trace.GetProperty("ticks").EnumerateArray())
            {
                sim.Step(1.0 / 30.0);
                for (int i = 0; i < 2; i++)
                {
                    JsonElement p = row[i];
                    PlayerState s = sim.Players[i + 1];
                    PlayerInput input = sim.Inputs[i + 1];
                    string who = $"tick {t}: fighter {i + 1}";
                    Assert.AreEqual(p[0].GetDouble(), s.Position.x, 1e-3, who + " x");
                    Assert.AreEqual(p[1].GetDouble(), s.Position.y, 1e-3, who + " z");
                    Assert.AreEqual(p[2].GetInt32(), s.Hp, who + " hp");
                    Assert.AreEqual(p[3].GetInt32() == 1, s.Tagged, who + " tagged");
                    Assert.AreEqual(p[4].GetInt32(), input.Buttons, who + " buttons");
                    Assert.AreEqual(p[5].GetDouble(), input.MoveTarget.x, 1e-3, who + " move target x");
                    Assert.AreEqual(p[6].GetDouble(), input.MoveTarget.y, 1e-3, who + " move target z");
                    Assert.AreEqual(p[7].GetDouble(), input.AimPoint.x, 1e-3, who + " aim point x");
                    Assert.AreEqual(p[8].GetDouble(), input.AimPoint.y, 1e-3, who + " aim point z");
                }
                t++;
            }
            Assert.Greater(t, 30);
            CollectionAssert.AreEqual(trace.GetProperty("events").EnumerateArray().Select(e => BotGoldenTests.EventKey(e).key).ToList(), events);
        }

        [Test]
        public void TiersAreGodots()
        {
            using JsonDocument doc = Load("ranked.json");
            foreach (JsonElement row in doc.RootElement.GetProperty("tiers").EnumerateArray())
            {
                int lp = row.GetProperty("lp").GetInt32();
                Assert.AreEqual(row.GetProperty("index").GetInt32(), RankedSystem.TierIndex(lp), $"{lp}: index");
                Assert.AreEqual(row.GetProperty("name").GetString(), RankedSystem.TierName(lp), $"{lp}: name");
                Assert.AreEqual(row.GetProperty("id").GetString(), RankedSystem.TierForLp(lp).Id, $"{lp}: id");
                Assert.AreEqual(row.GetProperty("next").GetInt32(), RankedSystem.NextTierLp(lp), $"{lp}: next");
                Assert.AreEqual(row.GetProperty("progress").GetDouble(), RankedSystem.TierProgress(lp), 1e-12, $"{lp}: progress");
            }
        }

        [Test]
        public void PreviewsAreGodots()
        {
            using JsonDocument doc = Load("ranked.json");
            int count = 0;
            foreach (JsonElement row in doc.RootElement.GetProperty("previews").EnumerateArray())
            {
                RankedSystem.Outcome got = RankedSystem.PreviewMatch(row.GetProperty("mode").GetString(), row.GetProperty("won").GetBoolean(),
                    row.GetProperty("my").GetInt32(), row.GetProperty("their").GetInt32(), row.GetProperty("base").GetInt32());
                CheckOutcome(row, got, $"preview #{count}");
                count++;
            }
            Assert.Greater(count, 100);
        }

        [Test]
        public void RecordedMatchesClimbLikeInGodot()
        {
            using JsonDocument doc = Load("ranked.json");
            var record = new RankedSystem.Record();
            int i = 0;
            foreach (JsonElement row in doc.RootElement.GetProperty("history").EnumerateArray())
            {
                RankedSystem.Outcome got = RankedSystem.RecordMatch(record, row.GetProperty("mode").GetString(), row.GetProperty("won").GetBoolean(),
                    row.GetProperty("my").GetInt32(), row.GetProperty("their").GetInt32());
                CheckOutcome(row, got, $"match #{i}");
                RankedSystem.Outcome last = RankedSystem.ConsumeLastResult(record);
                Assert.AreEqual(row.GetProperty("consumed").GetBoolean(), last != null);
                Assert.AreEqual(got, last);
                Assert.IsNull(RankedSystem.ConsumeLastResult(record), "taken once");
                Assert.AreEqual(row.GetProperty("lp").GetInt32(), record.Lp, $"match #{i}: lp");
                Assert.AreEqual(row.GetProperty("peak").GetInt32(), record.PeakLp, $"match #{i}: peak");
                i++;
            }
            Assert.AreEqual(i, record.Matches);
        }

        static void CheckOutcome(JsonElement row, RankedSystem.Outcome got, string what)
        {
            Assert.AreEqual(row.GetProperty("old").GetInt32(), got.OldLp, what + " old");
            Assert.AreEqual(row.GetProperty("new").GetInt32(), got.NewLp, what + " new");
            Assert.AreEqual(row.GetProperty("delta").GetInt32(), got.Delta, what + " delta");
            Assert.AreEqual(row.GetProperty("tier").GetString(), got.Tier, what + " tier");
            Assert.AreEqual(row.GetProperty("promoted").GetBoolean(), got.Promoted, what + " promoted");
        }
    }
}
