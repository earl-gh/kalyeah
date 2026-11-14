using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Kalyeah.Sim.Tests.Bots
{
    /// <summary>
    /// The port's bots play the very matches the Godot bots played (unity/Golden/Bots/*.json, recorded by tools/unity_export/export_bots.gd):
    /// a 5v5 Practice match against bots per bot level, set up like practice_match.gd `_setup_bot_match`. Only your (id 1) inputs come from
    /// the file; the C# TeamCoaches drive the other nine, and every tick their plans and jobs, every bot's input, every player's state and
    /// every signal must match Godot's.
    /// </summary>
    public class BotGoldenTests
    {
        public static string Golden(string name) => Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../Golden/Bots", name);

        public static readonly string[] Levels = { "easy", "medium", "hard" };

        [Test]
        public void EveryBotLevelIsRecorded()
        {
            CollectionAssert.AreEqual(Levels, GoldenMatchTests.Data.GameRules.BotLevels.Select(l => l.Id).ToArray());
            foreach (string level in Levels) FileAssert.Exists(Golden($"bots_{level}.json"));
        }

        /// <summary>practice_match.gd `_assign_unique_roster` (view code, so the test does it): ten unique kids, the picked one first.</summary>
        public static void AssignUniqueRoster(MatchSim sim, GameRules rules, string avatar)
        {
            if (rules.Characters.Count < sim.Players.Count)
            {
                sim.AssignCharacters();
                sim.SetCharacter(1, avatar);
                return;
            }
            var pool = rules.Characters.Select(c => c.Id).ToList();
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = sim.Rng.RandiRange(0, i);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            int wanted = pool.IndexOf(avatar);
            if (wanted > 0)
            {
                pool[wanted] = pool[0];
                pool[0] = avatar;
            }
            int cursor = 0;
            foreach (var pair in sim.Players) pair.Value.CharacterId = pool[cursor++];
        }

        [TestCaseSource(nameof(Levels))]
        public void BotMatchMatchesGodot(string level)
        {
            string name = "bots_" + level;
            using var doc = JsonDocument.Parse(File.ReadAllText(Golden(name + ".json")));
            JsonElement root = doc.RootElement;
            Assert.AreEqual(level, root.GetProperty("level").GetString());
            GameRules rules = GoldenMatchTests.Data.GameRules.WithBotLevel(level);
            Assert.AreEqual(root.GetProperty("bot_skill").GetDouble(), rules.BotSkill, 1e-12, "the level's skill");
            var sim = new MatchSim(rules, GoldenMatchTests.Data.MapLayout, root.GetProperty("seed").GetInt64());
            // _setup_bot_match
            sim.AddPlayer(1, 0);
            var allies = new TeamCoach(0, 1);
            var foes = new TeamCoach(1);
            foreach (int id in new[] { 2, 3, 4, 5 })
            {
                sim.AddPlayer(id, 0);
                allies.Add(sim, id, rules.BotSkill);
            }
            foreach (int id in new[] { 6, 7, 8, 9, 10 })
            {
                sim.AddPlayer(id, 1);
                foes.Add(sim, id, rules.BotSkill);
            }
            var coaches = new[] { allies, foes };
            AssignUniqueRoster(sim, rules, root.GetProperty("avatar").GetString());
            sim.Equip(1);
            for (int id = 2; id <= 10; id++) sim.Equip(id, true);
            sim.BeginIntro();

            foreach (JsonElement start in root.GetProperty("start").EnumerateArray())
            {
                PlayerState s = sim.Players[start.GetProperty("id").GetInt32()];
                string who = $"{name}: player {s.Id}";
                Assert.AreEqual(start.GetProperty("team").GetInt32(), s.Team, who + " team");
                Assert.AreEqual(start.GetProperty("character").GetString(), s.CharacterId, who + " character");
                CollectionAssert.AreEqual(Strings(start.GetProperty("hand")), s.Weapons, who + " hand");
                CollectionAssert.AreEqual(Strings(start.GetProperty("deck")), s.Deck, who + " deck");
                Assert.AreEqual(start.GetProperty("next").GetString(), s.NextCard, who + " next");
                Assert.AreEqual(start.GetProperty("spawn")[0].GetDouble(), s.SpawnPosition.x, 1e-6, who + " spawn x");
                Assert.AreEqual(start.GetProperty("spawn")[1].GetDouble(), s.SpawnPosition.y, 1e-6, who + " spawn z");
            }
            JsonElement coachStart = root.GetProperty("coaches");
            for (int c = 0; c < coaches.Length; c++)
            {
                JsonElement want = coachStart[c];
                Assert.AreEqual(want.GetProperty("team").GetInt32(), coaches[c].Team);
                Assert.AreEqual(want.GetProperty("follow").GetInt32(), coaches[c].FollowId);
                foreach (JsonElement b in want.GetProperty("brains").EnumerateArray())
                {
                    TeamBrain brain = coaches[c].Brains[b[0].GetInt32()];
                    string who = $"{name}: brain {b[0].GetInt32()}";
                    Assert.AreEqual(b[1].GetDouble(), brain.Home.x, 1e-6, who + " home x");
                    Assert.AreEqual(b[2].GetDouble(), brain.Home.y, 1e-6, who + " home z");
                    Assert.AreEqual(b[3].GetDouble(), brain.Target.x, 1e-6, who + " courtyard x");
                    Assert.AreEqual(b[4].GetDouble(), brain.Target.y, 1e-6, who + " courtyard z");
                    Assert.AreEqual(b[5].GetDouble(), brain.Skill, 1e-12, who + " skill");
                    Assert.AreEqual(b[6].GetInt32(), (int)brain.Role, who + " role");
                }
            }

            var events = new List<string>();
            Hook(sim, events);
            var expectedEvents = root.GetProperty("events").EnumerateArray().Select(EventKey).ToList();
            JsonElement plans = root.GetProperty("plans");
            JsonElement inputs = root.GetProperty("inputs");
            JsonElement frames = root.GetProperty("frames");
            int ticks = frames.GetArrayLength();
            Assert.GreaterOrEqual(ticks, 1500);
            int eventsChecked = 0;
            int botInputs = 0;
            double worst = 0.0;
            for (int t = 0; t < ticks; t++)
            {
                string at = $"{name} tick {t}";
                bool playing = sim.Phase == MatchSim.MatchPhase.Playing;
                foreach (TeamCoach coach in coaches) coach.Drive(sim);
                for (int c = 0; c < coaches.Length; c++) CheckPlan(at, sim, coaches[c], plans[t][c]);
                JsonElement rows = inputs[t];
                JsonElement localRow = rows[0];
                Assert.AreEqual(1, localRow[0].GetInt32(), at);
                sim.SetInput(1, Input(localRow));
                var driven = new List<int>();
                foreach (TeamCoach coach in coaches)
                    foreach (int id in coach.Members())
                        if (playing && !sim.Players[id].Tagged) driven.Add(id);
                Assert.AreEqual(rows.GetArrayLength() - 1, driven.Count, $"{at}: bots driven");
                for (int r = 1; r < rows.GetArrayLength(); r++)
                {
                    JsonElement row = rows[r];
                    int id = row[0].GetInt32();
                    Assert.AreEqual(driven[r - 1], id, $"{at}: driven order");
                    CheckInput($"{at}: input of bot {id}", row, sim.Inputs[id], ref worst);
                    botInputs++;
                }
                sim.Step(1.0 / 30.0);
                CheckFrame(at, sim, frames[t], ref worst);
                while (eventsChecked < expectedEvents.Count && expectedEvents[eventsChecked].t <= t)
                {
                    Assert.Less(eventsChecked, events.Count, $"{at}: missing event {expectedEvents[eventsChecked].key}");
                    Assert.AreEqual(expectedEvents[eventsChecked].key, events[eventsChecked], $"{at}: event #{eventsChecked}");
                    eventsChecked++;
                }
                Assert.AreEqual(eventsChecked, events.Count, $"{at}: extra event {(events.Count > eventsChecked ? events[eventsChecked] : "")}");
            }
            Assert.AreEqual(expectedEvents.Count, eventsChecked, $"{name}: every event checked");
            TestContext.WriteLine($"{name}: {ticks} ticks, {botInputs} bot inputs, {eventsChecked} events; largest position gap {worst:E2}");
        }

        static PlayerInput Input(JsonElement row)
        {
            var input = PlayerInput.Create(new Vec2(row[1].GetDouble(), row[2].GetDouble()), new Vec2(row[3].GetDouble(), row[4].GetDouble()),
                row[5].GetInt32(), row[10].GetInt32());
            input.MoveTarget = new Vec2(row[6].GetDouble(), row[7].GetDouble());
            input.AimPoint = new Vec2(row[8].GetDouble(), row[9].GetDouble());
            return input;
        }

        static void CheckInput(string at, JsonElement row, PlayerInput got, ref double worst)
        {
            Assert.AreEqual(row[5].GetInt32(), got.Buttons, at + " buttons");
            Assert.AreEqual(row[10].GetInt32(), got.Tick, at + " tick");
            Near(row[1].GetDouble(), got.Move.x, 1e-6, at + " move x", ref worst);
            Near(row[2].GetDouble(), got.Move.y, 1e-6, at + " move z", ref worst);
            Near(row[3].GetDouble(), got.Aim.x, 1e-6, at + " aim x", ref worst);
            Near(row[4].GetDouble(), got.Aim.y, 1e-6, at + " aim z", ref worst);
            Near(row[6].GetDouble(), got.MoveTarget.x, 1e-3, at + " move target x", ref worst);
            Near(row[7].GetDouble(), got.MoveTarget.y, 1e-3, at + " move target z", ref worst);
            Near(row[8].GetDouble(), got.AimPoint.x, 1e-3, at + " aim point x", ref worst);
            Near(row[9].GetDouble(), got.AimPoint.y, 1e-3, at + " aim point z", ref worst);
        }

        static void CheckPlan(string at, MatchSim sim, TeamCoach coach, JsonElement plan)
        {
            string who = $"{at}: coach {coach.Team}";
            Assert.AreEqual(plan[0].GetInt32(), (int)coach.Plan, who + " plan");
            int rescuer = coach.RescuerId(sim);
            Assert.AreEqual(plan[1].GetInt32(), rescuer, who + " rescuer");
            CollectionAssert.AreEqual(Ints(plan[2]), coach.GuardIds(sim, rescuer), who + " guards");
            Assert.AreEqual(plan[3].GetInt32(), coach.PusherId(sim, rescuer), who + " pusher");
            CollectionAssert.AreEqual(Ints(plan[4]), coach.Holders, who + " holders");
            foreach (JsonElement b in plan[5].EnumerateArray())
            {
                TeamBrain brain = coach.Brains[b[0].GetInt32()];
                string bot = $"{who} bot {b[0].GetInt32()}";
                Assert.AreEqual(b[1].GetInt32(), (int)brain.Role, bot + " role");
                Assert.AreEqual(b[2].GetInt32(), brain.RescueDuty, bot + " rescue duty");
                Assert.AreEqual(b[3].GetInt32() == 1, brain.Hold, bot + " hold");
                Assert.AreEqual(b[4].GetDouble(), brain.Target.x, 1e-3, bot + " target x");
                Assert.AreEqual(b[5].GetDouble(), brain.Target.y, 1e-3, bot + " target z");
            }
        }

        static void CheckFrame(string at, MatchSim sim, JsonElement frame, ref double worst)
        {
            Assert.AreEqual(frame.GetProperty("tick").GetInt32(), sim.Tick, at);
            Assert.AreEqual(frame.GetProperty("phase").GetInt32(), (int)sim.Phase, $"{at}: phase");
            Assert.AreEqual(frame.GetProperty("score")[0].GetInt32(), sim.Score.Points[0], $"{at}: score 0");
            Assert.AreEqual(frame.GetProperty("score")[1].GetInt32(), sim.Score.Points[1], $"{at}: score 1");
            Assert.AreEqual(frame.GetProperty("capture_team").GetInt32(), sim.CaptureTeam, $"{at}: capture team");
            Assert.AreEqual(frame.GetProperty("capture_kind").GetInt32(), (int)sim.CurrentCaptureKind, $"{at}: capture kind");
            Assert.AreEqual(frame.GetProperty("capture_elapsed").GetDouble(), sim.CaptureElapsed, 1e-6, $"{at}: capture elapsed");
            Assert.AreEqual(frame.GetProperty("capture_needed").GetDouble(), sim.CaptureNeeded, 1e-9, $"{at}: capture needed");
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
            JsonElement players = frame.GetProperty("players");
            JsonElement extra = frame.GetProperty("extra");
            for (int i = 0; i < players.GetArrayLength(); i++)
            {
                JsonElement p = players[i];
                PlayerState s = sim.Players[p[0].GetInt32()];
                string who = $"{at}: player {s.Id}";
                Near(p[1].GetDouble(), s.Position.x, 1e-3, who + " x", ref worst);
                Near(p[2].GetDouble(), s.Position.y, 1e-3, who + " z", ref worst);
                Assert.AreEqual(p[3].GetInt32(), s.Hp, who + " hp");
                Assert.AreEqual(p[4].GetInt32() == 1, s.Tagged, who + " tagged");
                Assert.AreEqual(p[5].GetDouble(), s.Elixir, 1e-4, who + " elixir");
                Assert.AreEqual(p[6].GetDouble(), s.Stamina, 1e-4, who + " stamina");
                JsonElement x = extra[i];
                Assert.AreEqual(x[0].GetDouble(), s.UltCharge, 1e-4, who + " ult");
                Assert.AreEqual(x[1].GetDouble(), s.AirY, 1e-6, who + " air");
                Assert.AreEqual(x[2].GetDouble(), s.Facing.x, 1e-4, who + " facing x");
                Assert.AreEqual(x[3].GetDouble(), s.Facing.y, 1e-4, who + " facing z");
                Assert.AreEqual(x[4].GetInt32(), s.BuffType, who + " buff");
                Assert.AreEqual(x[5].GetInt32(), ActiveEffect(s), who + " effect");
                Assert.AreEqual(x[6].GetInt32() == 1, s.HasMoveTarget, who + " has move target");
                Assert.AreEqual(x[7].GetString(), string.Join(",", s.Weapons), who + " hand");
                Assert.AreEqual(x[8].GetString(), s.NextCard, who + " next card");
                Assert.AreEqual(x[9].GetInt32(), s.Deck.Count, who + " deck size");
                Assert.AreEqual(x[10].GetDouble(), s.WeaponCooldowns.Sum(), 1e-6, who + " cooldowns");
                Assert.AreEqual(x[11].GetInt32() == 1, s.Overclocked, who + " overclocked");
                Near(x[12].GetDouble(), s.MoveTarget.x, 1e-3, who + " move target x", ref worst);
                Near(x[13].GetDouble(), s.MoveTarget.y, 1e-3, who + " move target z", ref worst);
                Assert.AreEqual(x[14].GetDouble(), s.StuckTime, 1e-6, who + " stuck");
                Assert.AreEqual(x[15].GetDouble(), s.PrimaryCd, 1e-6, who + " primary cd");
                Assert.AreEqual(x[16].GetDouble(), s.DashCd, 1e-6, who + " dash cd");
                Assert.AreEqual(x[17].GetDouble(), s.BuffLeft, 1e-6, who + " buff left");
            }
        }

        public static int ActiveEffect(PlayerState s)
        {
            int found = 0;
            foreach (StatusEffects.Type type in Enum.GetValues(typeof(StatusEffects.Type)))
                if (type != StatusEffects.Type.None && s.Effects.Has(type)) found = (int)type;
            return found;
        }

        static void Near(double want, double got, double tolerance, string what, ref double worst)
        {
            double gap = Math.Abs(want - got);
            if (gap > worst && tolerance >= 1e-3) worst = gap;
            Assert.AreEqual(want, got, tolerance, what);
        }

        static List<string> Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()).ToList();
        static List<int> Ints(JsonElement array) => array.EnumerateArray().Select(e => e.GetInt32()).ToList();

        public static (int t, string key) EventKey(JsonElement e)
        {
            int t = e.GetProperty("t").GetInt32();
            var parts = new List<string> { t.ToString(), e.GetProperty("e").GetString() };
            foreach (JsonProperty prop in e.EnumerateObject())
                if (prop.Name != "t" && prop.Name != "e") parts.Add(prop.Name + "=" + prop.Value.ToString());
            return (t, string.Join(" ", parts));
        }

        public static void Hook(MatchSim sim, List<string> events)
        {
            void Add(string e, params (string, object)[] fields) =>
                events.Add(string.Join(" ", new[] { sim.Tick.ToString(), e }.Concat(fields.Select(f => f.Item1 + "=" + f.Item2))));
            sim.PlayerTagged += (id, by) => Add("tagged", ("id", id), ("by", by));
            sim.PlayerRescued += (id, by) => Add("rescued", ("id", id), ("by", by));
            sim.PointScored += (team, how) => Add("point", ("team", team), ("how", how));
            sim.MatchWon += team => Add("won", ("team", team));
            sim.PointReset += () => Add("reset");
            sim.IntroFinished += () => Add("intro");
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
    }
}
