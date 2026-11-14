using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NUnit.Framework;

namespace Kalyeah.Sim.Tests.Street
{
    /// <summary>
    /// The port plays the very street matches the Godot game played (unity/Golden/Street/*.json, recorded by
    /// tools/unity_export/export_street.gd): the practice setup, kid 1's recorded inputs, the port's own StreetModeCoach for kids 2..6 (whose
    /// inputs must equal the recorded ones), and every tick's players, the game's whole snapshot, the projectiles, the game's queries and every
    /// signal must match.
    /// </summary>
    public class StreetGoldenTests
    {
        const double PositionTolerance = 1e-3;
        const double ScalarTolerance = 1e-9;

        static string GoldenDir => Path.Combine(Path.GetDirectoryName(typeof(StreetGoldenTests).Assembly.Location), "../../../../Golden/Street");

        public static IEnumerable<string> Recordings() =>
            Directory.GetFiles(GoldenDir, "street_*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(n => n);

        [Test]
        public void EveryStreetGameIsRecorded()
        {
            CollectionAssert.IsSupersetOf(Recordings().ToList(), new[] { "street_patintero", "street_tumbang_preso", "street_langit_lupa" });
        }

        /// <summary>A Vec2 in the plain tree: compared with the position tolerance.</summary>
        sealed class V2
        {
            public double X, Y;
            public V2(Vec2 v) { X = v.x; Y = v.y; }
        }

        [TestCaseSource(nameof(Recordings))]
        public void ReplayMatchesGodot(string name)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(GoldenDir, name + ".json")));
            JsonElement root = doc.RootElement;
            GameRules rules = Kit.Rules();
            foreach (JsonProperty o in root.GetProperty("rules").EnumerateObject()) GoldenMatchTests.SetRule(rules, o.Name, o.Value);
            StreetModeRules game = Kit.Data.StreetModeRules(root.GetProperty("game_id").GetString()).Duplicate();
            foreach (JsonProperty o in root.GetProperty("game").EnumerateObject()) SetGameRule(game, o.Name, o.Value);
            var sim = new MatchSim(rules, Kit.Layout, root.GetProperty("seed").GetInt64());

            // practice_match.gd `_setup_street_game`
            bool forAll = StreetMode.Create(game).IsFreeForAll();
            Assert.AreEqual(root.GetProperty("for_all").GetBoolean(), forAll);
            int local = root.GetProperty("local").GetInt32();
            sim.AddPlayer(local, 0);
            var coach = new StreetModeCoach();
            foreach (int id in new[] { 2, 3 })
            {
                sim.AddPlayer(id, 0);
                coach.Add(id);
            }
            foreach (int id in new[] { 4, 5, 6 })
            {
                sim.AddPlayer(id, forAll ? 0 : 1);
                coach.Add(id);
            }
            CollectionAssert.AreEqual(root.GetProperty("coached").EnumerateArray().Select(e => e.GetInt32()).ToList(), coach.Ids);
            sim.AssignCharacters();
            sim.SetCharacter(local, "kiko");
            sim.StartMode(StreetMode.Create(game));
            sim.BeginIntro();
            var mode = (StreetMode)sim.Mode;
            var ids = sim.Players.Keys.ToList();
            foreach (JsonElement s in root.GetProperty("start").EnumerateArray())
            {
                PlayerState state = sim.Players[s.GetProperty("id").GetInt32()];
                Assert.AreEqual(s.GetProperty("team").GetInt32(), state.Team, $"{name}: team of {state.Id}");
                Assert.AreEqual(s.GetProperty("character").GetString(), state.CharacterId, $"{name}: character of {state.Id}");
                Assert.AreEqual(s.GetProperty("x").GetDouble(), state.Position.x, PositionTolerance, $"{name}: start x of {state.Id}");
                Assert.AreEqual(s.GetProperty("y").GetDouble(), state.Position.y, PositionTolerance, $"{name}: start z of {state.Id}");
            }

            var events = new List<object[]>();
            Hook(sim, mode, events);
            JsonElement expectedEvents = root.GetProperty("events");
            JsonElement inputs = root.GetProperty("inputs");
            JsonElement frames = root.GetProperty("frames");
            int ticks = frames.GetArrayLength();
            Assert.AreEqual(ticks, inputs.GetArrayLength());
            double dt = root.GetProperty("dt").GetDouble();
            int checkedEvents = 0;
            var worst = new double[1];
            for (int t = 0; t < ticks; t++)
            {
                string at = $"{name} tick {t}";
                Assert.AreEqual(t, sim.Tick, at);
                coach.Drive(sim);
                foreach (JsonElement row in inputs[t].EnumerateArray())
                {
                    int id = row[0].GetInt32();
                    var move = new Vec2(row[1].GetDouble(), row[2].GetDouble());
                    var aim = new Vec2(row[3].GetDouble(), row[4].GetDouble());
                    int buttons = row[5].GetInt32();
                    if (id == local)
                    {
                        sim.SetInput(id, PlayerInput.Create(move, aim, buttons, t));
                        continue;
                    }
                    PlayerInput bot = sim.Inputs.GetOrDefault(id);
                    Assert.IsNotNull(bot, $"{at}: the coach gave kid {id} no input");
                    Assert.AreEqual(move.x, bot.Move.x, 1e-6, $"{at}: kid {id}'s stick x");
                    Assert.AreEqual(move.y, bot.Move.y, 1e-6, $"{at}: kid {id}'s stick z");
                    Assert.AreEqual(aim.x, bot.Aim.x, 1e-6, $"{at}: kid {id}'s aim x");
                    Assert.AreEqual(aim.y, bot.Aim.y, 1e-6, $"{at}: kid {id}'s aim z");
                    Assert.AreEqual(buttons, bot.Buttons, $"{at}: kid {id}'s buttons");
                }
                Assert.AreEqual(inputs[t].GetArrayLength(), ids.Count(id => sim.Inputs.ContainsKey(id)), $"{at}: who has an input");
                sim.Step(dt);
                CheckFrame(at, sim, mode, ids, frames[t], worst);
                for (; checkedEvents < expectedEvents.GetArrayLength() && expectedEvents[checkedEvents][0].GetInt32() <= t; checkedEvents++)
                {
                    Assert.Less(checkedEvents, events.Count, $"{at}: missing event {expectedEvents[checkedEvents]}");
                    Same(expectedEvents[checkedEvents], events[checkedEvents].ToList(), $"{at}: event #{checkedEvents}", worst);
                }
                Assert.AreEqual(checkedEvents, events.Count, $"{at}: extra event {(events.Count > checkedEvents ? string.Join(" ", events[checkedEvents]) : "")}");
            }
            Assert.AreEqual(expectedEvents.GetArrayLength(), checkedEvents, $"{name}: every event checked");

            Assert.AreEqual(root.GetProperty("phase").GetInt32(), (int)sim.Phase, $"{name}: the match is over");
            Assert.AreEqual(root.GetProperty("winner").GetInt32(), sim.Score.Winner, $"{name}: winner");
            foreach (JsonElement r in root.GetProperty("result").EnumerateArray())
            {
                StreetMode.Result result = mode.LocalResult(sim, r[0].GetInt32());
                Assert.AreEqual(r[1].GetBoolean(), result.Won, $"{name}: result of {r[0]}");
                Assert.AreEqual(r[2].GetInt32(), result.Mine, $"{name}: result of {r[0]}");
                Assert.AreEqual(r[3].GetInt32(), result.Theirs, $"{name}: result of {r[0]}");
            }
            if (mode is TayaGame taya)
                CollectionAssert.AreEqual(root.GetProperty("ranking").EnumerateArray().Select(e => e.GetInt32()).ToList(), taya.Ranking(), $"{name}: ranking");
            TestContext.WriteLine($"{name}: {ticks} ticks, {checkedEvents} events, largest position gap {worst[0]:E2}");
        }

        static void SetGameRule(StreetModeRules rules, string snakeName, JsonElement value)
        {
            FieldInfo field = typeof(StreetModeRules).GetField(SimData.Pascal(snakeName));
            Assert.IsNotNull(field, "no street rule " + snakeName);
            object v = field.FieldType == typeof(bool) ? value.GetBoolean() : field.FieldType == typeof(int) ? value.GetInt32() : (object)value.GetDouble();
            field.SetValue(rules, v);
        }

        static void CheckFrame(string at, MatchSim sim, StreetMode mode, List<int> ids, JsonElement frame, double[] worst)
        {
            Assert.AreEqual(frame.GetProperty("tick").GetInt32(), sim.Tick, at);
            Assert.AreEqual(frame.GetProperty("phase").GetInt32(), (int)sim.Phase, $"{at}: phase");
            Assert.AreEqual(frame.GetProperty("intro_left").GetDouble(), sim.IntroLeft, ScalarTolerance, $"{at}: intro");
            Assert.AreEqual(frame.GetProperty("score")[0].GetInt32(), sim.Score.Points[0], $"{at}: score 0");
            Assert.AreEqual(frame.GetProperty("score")[1].GetInt32(), sim.Score.Points[1], $"{at}: score 1");
            Assert.AreEqual(frame.GetProperty("winner").GetInt32(), sim.Score.Winner, $"{at}: winner");
            Assert.AreEqual(frame.GetProperty("log").GetInt32(), sim.PointLog.Count, $"{at}: point log");
            Assert.AreEqual(frame.GetProperty("clock").GetDouble(), sim.ClockLeft(), ScalarTolerance, $"{at}: clock");
            JsonElement players = frame.GetProperty("players");
            for (int i = 0; i < players.GetArrayLength(); i++)
            {
                JsonElement p = players[i];
                PlayerState s = sim.Players[p[0].GetInt32()];
                Same(p, new List<object>
                {
                    s.Id, new V2(s.Position), new V2(s.Facing), s.Hp, s.Tagged ? 1 : 0, s.Stamina, s.HasStamina ? 1 : 0, s.CastLockLeft,
                    s.HasMoveTarget ? 1 : 0, s.Overclocked ? 1 : 0,
                }, $"{at}: player {s.Id}", worst, flatVectors: true);
            }
            Same(frame.GetProperty("mode"), Plain(mode.Snapshot()), $"{at}: snapshot", worst);
            var projectiles = sim.Weapons.Projectiles.Select(p => (object)new List<object> { p.Id, p.OwnerId, new V2(p.Position), p.Travelled }).ToList();
            Same(frame.GetProperty("proj"), projectiles, $"{at}: projectiles", worst, flatVectors: true);
            Same(frame.GetProperty("q"), Queries(sim, mode, ids), $"{at}: queries", worst);
        }

        /// <summary>The same queries as export_street.gd `_queries`.</summary>
        static OrderedDict<string, object> Queries(MatchSim sim, StreetMode mode, List<int> ids)
        {
            var kids = new List<object>();
            foreach (int id in ids)
            {
                PlayerState s = sim.Players[id];
                var row = new List<object> { id, mode.IsAttacker(s) ? 1 : 0, mode.IsTaggable(s) ? 1 : 0, mode.SpeedScale(s), mode.CanRest(s) ? 1 : 0 };
                switch (mode)
                {
                    case Patintero lines:
                        row.AddRange(new object[] { lines.CanDive(s) ? 1 : 0, lines.DiveReadyShare(id), lines.IsTaggedOut(id) ? 1 : 0 });
                        break;
                    case TumbangPreso tp:
                        TumbangPreso.Guide guide = tp.AimGuide(sim, id);
                        row.AddRange(new object[]
                        {
                            tp.CanKick(s) ? 1 : 0, tp.CanThrow(s) ? 1 : 0, tp.AimRange(id), tp.IsHeldBack(s) ? 1 : 0,
                            guide == null ? new List<object>() : new List<object> { new V2(guide.Point), new V2(guide.CanDir), new V2(guide.SlipperDir) },
                        });
                        break;
                    case LangitLupa ll:
                        row.AddRange(new object[]
                        {
                            ll.IsSafe(s) ? 1 : 0, ll.CanRaise(s) ? 1 : 0, ll.SafeShare(id), ll.SpotAt(s.Position), ll.MoundAt(s.Position),
                            ll.HiddenFrom(sim, id).Cast<object>().ToList(),
                        });
                        break;
                }
                kids.Add(row);
            }
            var output = new OrderedDict<string, object>();
            output["leader"] = mode.Leader(sim);
            output["resetting"] = mode.IsResetting();
            output["kids"] = kids;
            if (mode is Patintero p) output["out"] = p.RunnersOut();
            if (mode is LangitLupa l) output["brownout"] = l.BrownoutLeft();
            return output;
        }

        static void Hook(MatchSim sim, StreetMode mode, List<object[]> events)
        {
            void Add(string e, params object[] args) => events.Add(new object[] { sim.Tick, e }.Concat(args).ToArray());
            sim.PlayerTagged += (id, by) => Add("tagged", id, by);
            sim.PlayerRescued += (id, by) => Add("rescued", id, by);
            sim.PointScored += (team, how) => Add("point", team, how);
            sim.MatchWon += team => Add("won", team);
            sim.PointReset += () => Add("reset");
            sim.IntroFinished += () => Add("intro_finished");
            sim.WeaponCast += (id, w) => Add("cast", id, w);
            sim.WeaponHit += (id, w) => Add("hit", id, w);
            sim.PlayerDamaged += (id, amount) => Add("damaged", id, amount);
            sim.Exhausted += id => Add("exhausted", id);
            mode.Tagged += (tagger, target) => Add("m_tagged", tagger, target);
            mode.Scored += (team, id) => Add("m_scored", team, id);
            mode.RolesSwapped += who => Add("m_roles_swapped", who);
            switch (mode)
            {
                case Patintero lines:
                    lines.RunnerCrossed += id => Add("runner_crossed", id);
                    lines.Dived += (id, way, time, recover) => Add("dived", id, new V2(way), time, recover);
                    lines.JailedRunner += (id, side) => Add("jailed_runner", id, side);
                    break;
                case TumbangPreso tp:
                    tp.SlipperThrown += id => Add("slipper_thrown", id);
                    tp.SlipperLanded += (id, where, hit) => Add("slipper_landed", id, new V2(where), hit);
                    tp.CanFlung += (from, path) => Add("can_flung", new V2(from), path.Select(v => (object)new V2(v)).ToList());
                    tp.SlipperFlung += (id, from, path) => Add("slipper_flung", id, new V2(from), path.Select(v => (object)new V2(v)).ToList());
                    tp.CanKicked += id => Add("can_kicked", id);
                    tp.SlipperFetched += id => Add("slipper_fetched", id);
                    tp.CanPlaced += id => Add("can_placed", id);
                    tp.TayaChanged += (old, now, why) => Add("taya_changed", old, now, why);
                    break;
                case LangitLupa ll:
                    ll.MoundRaised += id => Add("mound_raised", id);
                    ll.MoundFell += id => Add("mound_fell", id);
                    ll.SpotBroke += index => Add("spot_broke", index);
                    ll.TayaChanged += (old, now) => Add("taya_changed", old, now);
                    break;
            }
        }

        // ---- plain trees -------------------------------------------------------------------------------

        /// <summary>A snapshot value as a plain tree: OrderedDict to an ordered list of (string key, value), Vec2 to V2, enums to ints.</summary>
        static object Plain(object value)
        {
            switch (value)
            {
                case null: return null;
                case Vec2 v: return new V2(v);
                case Enum e: return Convert.ToInt32(e);
                case string _: return value;
                case bool _: return value;
                case OrderedDict<string, object> d:
                    return d.Select(pair => new KeyValuePair<string, object>(pair.Key, Plain(pair.Value))).ToList();
            }
            Type type = value.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(OrderedDict<,>))
            {
                var pairs = new List<KeyValuePair<string, object>>();
                foreach (object pair in (IEnumerable)value)
                {
                    Type pt = pair.GetType();
                    pairs.Add(new KeyValuePair<string, object>(pt.GetProperty("Key").GetValue(pair).ToString(), Plain(pt.GetProperty("Value").GetValue(pair))));
                }
                return pairs;
            }
            if (value is IEnumerable list) return list.Cast<object>().Select(Plain).ToList();
            return value;
        }

        /// <summary>`want` (the recording) against `got` (a plain tree, or an OrderedDict of one). With `flatVectors` a V2 in a list stands for
        /// two numbers of the recorded row.</summary>
        static void Same(JsonElement want, object got, string at, double[] worst, bool flatVectors = false)
        {
            if (got is OrderedDict<string, object> dict) got = Plain(dict);
            switch (want.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    var pairs = got as List<KeyValuePair<string, object>>;
                    Assert.IsNotNull(pairs, $"{at}: a table, got {got}");
                    var keys = want.EnumerateObject().Select(p => p.Name).ToList();
                    CollectionAssert.AreEqual(keys, pairs.Select(p => p.Key).ToList(), $"{at}: keys");
                    foreach (var pair in pairs) Same(want.GetProperty(pair.Key), pair.Value, $"{at}.{pair.Key}", worst);
                    return;
                }
                case JsonValueKind.Array:
                {
                    if (got is V2 v)
                    {
                        Assert.AreEqual(2, want.GetArrayLength(), $"{at}: a vector");
                        Near(want[0].GetDouble(), v.X, at + ".x", worst);
                        Near(want[1].GetDouble(), v.Y, at + ".y", worst);
                        return;
                    }
                    var items = got as IList;
                    Assert.IsNotNull(items, $"{at}: a list, got {got}");
                    var flat = new List<object>();
                    foreach (object item in items)
                    {
                        if (flatVectors && item is V2 fv)
                        {
                            flat.Add(new Exact(fv.X));
                            flat.Add(new Exact(fv.Y));
                        }
                        else flat.Add(item);
                    }
                    Assert.AreEqual(want.GetArrayLength(), flat.Count, $"{at}: length");
                    for (int i = 0; i < flat.Count; i++)
                    {
                        if (flat[i] is Exact x) Near(want[i].GetDouble(), x.Value, $"{at}[{i}]", worst);
                        else Same(want[i], flat[i], $"{at}[{i}]", worst, flatVectors);
                    }
                    return;
                }
                case JsonValueKind.Number:
                    Assert.IsNotNull(got, $"{at}: a number");
                    double number = got is V2 ? double.NaN : Convert.ToDouble(got);
                    Assert.AreEqual(want.GetDouble(), number, ScalarTolerance, at);
                    return;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    Assert.AreEqual(want.GetBoolean(), got, at);
                    return;
                case JsonValueKind.String:
                    Assert.AreEqual(want.GetString(), got, at);
                    return;
                case JsonValueKind.Null:
                    Assert.IsNull(got, at);
                    return;
            }
        }

        sealed class Exact
        {
            public double Value;
            public Exact(double value) { Value = value; }
        }

        static void Near(double want, double got, string at, double[] worst)
        {
            double gap = Math.Abs(want - got);
            if (gap > worst[0]) worst[0] = gap;
            Assert.AreEqual(want, got, PositionTolerance, at);
        }
    }
}
