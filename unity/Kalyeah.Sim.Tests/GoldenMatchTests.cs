using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NUnit.Framework;

namespace Kalyeah.Sim.Tests
{
    /// <summary>
    /// The port plays the very matches the Godot game played (unity/Golden/*.json, recorded by tools/unity_export/export.gd): same setup,
    /// the recorded inputs fed tick by tick, and every tick's state and every signal must match.
    /// </summary>
    public class GoldenMatchTests
    {
        public static string Golden(string name) => Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../Golden", name);
        public static string DataDir => Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../Data");

        static SimData _data;
        public static SimData Data => _data ??= SimData.Load(DataDir);

        public static IEnumerable<string> Recordings()
        {
            string dir = Path.Combine(Path.GetDirectoryName(typeof(GoldenMatchTests).Assembly.Location), "../../../../Golden");
            return Directory.GetFiles(dir, "*.json").Select(Path.GetFileNameWithoutExtension).Where(n => n != "rng").OrderBy(n => n);
        }

        [Test]
        public void EveryScenarioIsRecorded()
        {
            var names = Recordings().ToList();
            CollectionAssert.IsSupersetOf(names, new[] { "match_1", "match_7", "match_2026", "brawl_11", "brawl_cards_5", "brawl_cooldown_cards_8", "capture_3", "practice_4" });
        }

        [TestCaseSource(nameof(Recordings))]
        public void ReplayMatchesGodot(string name)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Golden(name + ".json")));
            JsonElement root = doc.RootElement;
            GameRules rules = Data.GameRules.Duplicate();
            if (root.TryGetProperty("rules", out JsonElement overrides) && overrides.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty o in overrides.EnumerateObject()) SetRule(rules, o.Name, o.Value);
            var sim = new MatchSim(rules, Data.MapLayout, root.GetProperty("seed").GetInt64());
            var ids = new List<int>();
            foreach (JsonElement entry in root.GetProperty("roster").EnumerateArray())
            {
                int id = entry.GetProperty("id").GetInt32();
                int team = entry.GetProperty("team").GetInt32();
                ids.Add(id);
                if (entry.TryGetProperty("brain", out JsonElement brain))
                    sim.AddDummy(id, team, new Vec2(entry.GetProperty("x").GetDouble(), entry.GetProperty("y").GetDouble()), Brain(brain.GetString(), entry));
                else
                    sim.AddPlayer(id, team);
            }
            sim.AssignCharacters();
            foreach (int id in ids) sim.Equip(id, true);

            foreach (JsonElement start in root.GetProperty("start").EnumerateArray())
            {
                PlayerState s = sim.Players[start.GetProperty("id").GetInt32()];
                Assert.AreEqual(start.GetProperty("character").GetString(), s.CharacterId, $"{name}: character of {s.Id}");
                CollectionAssert.AreEqual(Strings(start.GetProperty("hand")), s.Weapons, $"{name}: hand of {s.Id}");
                CollectionAssert.AreEqual(Strings(start.GetProperty("deck")), s.Deck, $"{name}: deck of {s.Id}");
                if (start.TryGetProperty("next", out JsonElement next)) Assert.AreEqual(next.GetString(), s.NextCard, $"{name}: next of {s.Id}");
            }
            if (root.TryGetProperty("place", out JsonElement place))
                foreach (JsonElement p in place.EnumerateArray())
                    sim.Players[p[0].GetInt32()].Position = new Vec2(p[1].GetDouble(), p[2].GetDouble());

            var events = new List<string>();
            Hook(sim, events);
            var expectedEvents = root.GetProperty("events").EnumerateArray().Select(EventKey).ToList();
            var script = new Dictionary<int, List<JsonElement>>();
            if (root.TryGetProperty("script", out JsonElement scriptLog))
                foreach (JsonElement entry in scriptLog.EnumerateArray())
                {
                    int t = entry.GetProperty("t").GetInt32();
                    if (!script.ContainsKey(t)) script[t] = new List<JsonElement>();
                    script[t].Add(entry);
                }

            double dt = 1.0 / 30.0;
            JsonElement inputs = root.GetProperty("inputs");
            JsonElement frames = root.GetProperty("frames");
            int ticks = frames.GetArrayLength();
            int eventsChecked = 0;
            var worst = new Worst();
            for (int t = 0; t < ticks; t++)
            {
                if (script.TryGetValue(t, out var calls))
                    foreach (JsonElement call in calls)
                        if (call.TryGetProperty("kill", out JsonElement kill)) sim.Kill(kill.GetInt32());
                foreach (JsonElement row in inputs[t].EnumerateArray())
                {
                    var input = PlayerInput.Create(new Vec2(row[1].GetDouble(), row[2].GetDouble()), new Vec2(row[3].GetDouble(), row[4].GetDouble()),
                        row[5].GetInt32(), t);
                    if (row.GetArrayLength() >= 10)
                    {
                        input.MoveTarget = new Vec2(row[6].GetDouble(), row[7].GetDouble());
                        input.AimPoint = new Vec2(row[8].GetDouble(), row[9].GetDouble());
                    }
                    sim.SetInput(row[0].GetInt32(), input);
                }
                sim.Step(dt);
                CheckFrame(name, sim, frames[t], worst);
                while (eventsChecked < expectedEvents.Count && expectedEvents[eventsChecked].t <= t)
                {
                    Assert.Less(eventsChecked, events.Count, $"{name} tick {t}: missing event {expectedEvents[eventsChecked].key}");
                    Assert.AreEqual(expectedEvents[eventsChecked].key, events[eventsChecked], $"{name} tick {t}: event #{eventsChecked}");
                    eventsChecked++;
                }
                Assert.AreEqual(eventsChecked, events.Count, $"{name} tick {t}: extra event {(events.Count > eventsChecked ? events[eventsChecked] : "")}");
            }
            Assert.AreEqual(expectedEvents.Count, eventsChecked, $"{name}: every event checked");
            TestContext.WriteLine($"{name}: {ticks} ticks, {eventsChecked} events; largest gaps: position {worst.Position:E2}, elixir {worst.Elixir:E2}, " +
                $"stamina {worst.Stamina:E2}, ult {worst.Ult:E2}, capture {worst.Capture:E2}");
        }

        sealed class Worst
        {
            public double Position, Elixir, Stamina, Ult, Capture;
        }

        static void CheckFrame(string name, MatchSim sim, JsonElement frame, Worst worst)
        {
            int t = frame.GetProperty("tick").GetInt32();
            string at = $"{name} tick {t}";
            Assert.AreEqual(t, sim.Tick, at);
            Assert.AreEqual(frame.GetProperty("phase").GetInt32(), (int)sim.Phase, $"{at}: phase");
            Assert.AreEqual(frame.GetProperty("score")[0].GetInt32(), sim.Score.Points[0], $"{at}: score 0");
            Assert.AreEqual(frame.GetProperty("score")[1].GetInt32(), sim.Score.Points[1], $"{at}: score 1");
            Assert.AreEqual(frame.GetProperty("capture_team").GetInt32(), sim.CaptureTeam, $"{at}: capture team");
            Near(frame.GetProperty("capture_elapsed").GetDouble(), sim.CaptureElapsed, 1e-6, $"{at}: capture elapsed", ref worst.Capture);
            if (frame.TryGetProperty("capture_needed", out JsonElement needed))
            {
                Assert.AreEqual(needed.GetDouble(), sim.CaptureNeeded, 1e-9, $"{at}: capture needed");
                Assert.AreEqual(frame.GetProperty("capture_rate").GetDouble(), sim.CaptureRate, 1e-9, $"{at}: capture rate");
                JsonElement rescue = frame.GetProperty("rescue");
                Assert.AreEqual(rescue[0].GetInt32(), sim.RescueRescuer[0], $"{at}: rescuer 0");
                Assert.AreEqual(rescue[1].GetInt32(), sim.RescueRescuer[1], $"{at}: rescuer 1");
                Assert.AreEqual(rescue[2].GetDouble(), sim.RescueElapsed[0], 1e-6, $"{at}: rescue elapsed 0");
                Assert.AreEqual(rescue[3].GetDouble(), sim.RescueElapsed[1], 1e-6, $"{at}: rescue elapsed 1");
                Assert.AreEqual(rescue[4].GetDouble(), sim.RescueReadyLeft[0], 1e-6, $"{at}: rescue ready 0");
                Assert.AreEqual(rescue[5].GetDouble(), sim.RescueReadyLeft[1], 1e-6, $"{at}: rescue ready 1");
                Assert.AreEqual(frame.GetProperty("round_time").GetDouble(), sim.RoundTime, 1e-9, $"{at}: round time");
                JsonElement objects = frame.GetProperty("objects");
                Assert.AreEqual(objects[0].GetInt32(), sim.Weapons.Projectiles.Count, $"{at}: projectiles");
                Assert.AreEqual(objects[1].GetInt32(), sim.Weapons.Zones.Count, $"{at}: zones");
                Assert.AreEqual(objects[2].GetInt32(), sim.Weapons.Shields.Count, $"{at}: shields");
                CollectionAssert.AreEqual(frame.GetProperty("powerups").EnumerateArray().Select(e => e.GetInt32() == 1).ToList(),
                    sim.Powerups.Select(p => p.Active).ToList(), $"{at}: power-ups");
            }
            JsonElement players = frame.GetProperty("players");
            bool hasExtra = frame.TryGetProperty("extra", out JsonElement extra);
            for (int i = 0; i < players.GetArrayLength(); i++)
            {
                JsonElement p = players[i];
                PlayerState s = sim.Players[p[0].GetInt32()];
                string who = $"{at}: player {s.Id}";
                Near(p[1].GetDouble(), s.Position.x, 1e-3, who + " x", ref worst.Position);
                Near(p[2].GetDouble(), s.Position.y, 1e-3, who + " z", ref worst.Position);
                Assert.AreEqual(p[3].GetInt32(), s.Hp, who + " hp");
                Assert.AreEqual(p[4].GetInt32() == 1, s.Tagged, who + " tagged");
                Near(p[5].GetDouble(), s.Elixir, 1e-4, who + " elixir", ref worst.Elixir);
                Near(p[6].GetDouble(), s.Stamina, 1e-4, who + " stamina", ref worst.Stamina);
                if (!hasExtra) continue;
                JsonElement x = extra[i];
                Near(x[0].GetDouble(), s.UltCharge, 1e-4, who + " ult", ref worst.Ult);
                Assert.AreEqual(x[1].GetDouble(), s.AirY, 1e-6, who + " air");
                Assert.AreEqual(x[2].GetDouble(), s.Facing.x, 1e-4, who + " facing x");
                Assert.AreEqual(x[3].GetDouble(), s.Facing.y, 1e-4, who + " facing z");
                Assert.AreEqual(x[4].GetInt32(), s.BuffType, who + " buff");
                Assert.AreEqual(x[5].GetInt32(), ActiveEffect(s), who + " effect");
                Assert.AreEqual(x[6].GetInt32() == 1, s.HasMoveTarget, who + " move target");
                Assert.AreEqual(x[7].GetString(), string.Join(",", s.Weapons), who + " hand");
                Assert.AreEqual(x[8].GetString(), s.NextCard, who + " next card");
                Assert.AreEqual(x[9].GetInt32(), s.Deck.Count, who + " deck size");
                Assert.AreEqual(x[10].GetDouble(), s.WeaponCooldowns.Sum(), 1e-6, who + " cooldowns");
                Assert.AreEqual(x[11].GetInt32() == 1, s.Overclocked, who + " overclocked");
            }
        }

        static DummyBrain Brain(string kind, JsonElement entry)
        {
            switch (kind)
            {
                case "patrol":
                    return DummyBrain.Patrolling(entry.GetProperty("origin").GetDouble(), entry.GetProperty("range").GetDouble(), entry.GetProperty("speed").GetDouble());
                case "rescuer":
                    return DummyBrain.Rescuer(new Vec2(entry.GetProperty("x").GetDouble(), entry.GetProperty("y").GetDouble()));
            }
            return DummyBrain.Standing();
        }

        static int ActiveEffect(PlayerState s)
        {
            int found = 0;
            foreach (StatusEffects.Type type in Enum.GetValues(typeof(StatusEffects.Type)))
                if (type != StatusEffects.Type.None && s.Effects.Has(type)) found = (int)type;
            return found;
        }

        static void Near(double want, double got, double tolerance, string what, ref double worst)
        {
            double gap = Math.Abs(want - got);
            if (gap > worst) worst = gap;
            Assert.AreEqual(want, got, tolerance, what);
        }

        static List<string> Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()).ToList();

        static (int t, string key) EventKey(JsonElement e)
        {
            int t = e.GetProperty("t").GetInt32();
            var parts = new List<string> { t.ToString(), e.GetProperty("e").GetString() };
            foreach (JsonProperty prop in e.EnumerateObject())
                if (prop.Name != "t" && prop.Name != "e") parts.Add(prop.Name + "=" + prop.Value.ToString());
            return (t, string.Join(" ", parts));
        }

        static void Hook(MatchSim sim, List<string> events)
        {
            void Add(string e, params (string, object)[] fields) =>
                events.Add(string.Join(" ", new[] { sim.Tick.ToString(), e }.Concat(fields.Select(f => f.Item1 + "=" + f.Item2))));
            sim.PlayerTagged += (id, by) => Add("tagged", ("id", id), ("by", by));
            sim.PlayerRescued += (id, by) => Add("rescued", ("id", id), ("by", by));
            sim.PointScored += (team, how) => Add("point", ("team", team), ("how", how));
            sim.MatchWon += team => Add("won", ("team", team));
            sim.PointReset += () => Add("reset");
            sim.EffectApplied += (id, type) => Add("effect", ("id", id), ("type", (int)type));
            sim.PlayerHealed += (id, amount) => Add("healed", ("id", id), ("amount", amount));
            sim.WeaponCast += (id, w) => Add("cast", ("id", id), ("w", w));
            sim.WeaponHit += (id, w) => Add("hit", ("id", id), ("w", w));
            sim.CardDrawn += (id, slot) => Add("card", ("id", id), ("slot", slot));
            sim.PlayerDamaged += (id, amount) => Add("damaged", ("id", id), ("amount", amount));
            sim.Exhausted += id => Add("exhausted", ("id", id));
            sim.PlayerDashed += id => Add("dashed", ("id", id));
            sim.UltimateCast += id => Add("ultimate", ("id", id));
            sim.PowerupTaken += (id, stand, kind) => Add("powerup", ("id", id), ("stand", stand), ("kind", kind));
        }

        /// <summary>Sets a GameRules number or toggle by its GDScript name (a recording's `rules` overrides).</summary>
        public static void SetRule(GameRules rules, string snakeName, JsonElement value)
        {
            FieldInfo field = typeof(GameRules).GetField(SimData.Pascal(snakeName));
            Assert.IsNotNull(field, "no rule " + snakeName);
            object v = field.FieldType == typeof(bool) ? value.GetBoolean()
                : field.FieldType == typeof(int) ? value.GetInt32()
                : (object)value.GetDouble();
            field.SetValue(rules, v);
        }
    }
}
