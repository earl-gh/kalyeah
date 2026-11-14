using System.Linq;
using System.Text.Json;
using Kalyeah.Sim;
using NUnit.Framework;

namespace Kalyeah.Net.Tests
{
    /// <summary>
    /// Snapshots of a scripted 5v5 fight (net_snapshots.json): the server's SNAPSHOT bytes, the private part, and a client's mirror after
    /// applying them (captured again, byte for byte) must be Godot's.
    /// </summary>
    public class SnapshotGoldenTests
    {
        /// <summary>export_net.gd `scripted_input`: a formula of the id and the tick only.</summary>
        public static PlayerInput ScriptedInput(int id, int t)
        {
            var move = new Vec2((double)((t * 7 + id * 3) % 11 - 5) / 5.0, (double)((t * 5 + id) % 13 - 6) / 6.0);
            if ((t / 30 + id) % 4 == 0) move = Vec2.Zero;
            var aim = new Vec2((double)(id % 3 - 1) * 0.3, id <= 5 ? -0.6 : 0.6);
            int buttons = 0;
            int phase = (t + id * 5) % 40;
            if (phase <= (id % 3) * 4) buttons |= PlayerInput.CardBits[((t + id * 5) / 40 + id) % 2];
            if ((t + id) % 9 == 0) buttons |= PlayerInput.BtnPrimary;
            if ((t + id * 13) % 150 == 0) buttons |= PlayerInput.BtnDash;
            if ((t + id * 11) % 70 == 0) buttons |= PlayerInput.BtnJump;
            if ((t + id * 17) % 110 == 0) buttons |= PlayerInput.BtnSlide;
            if ((t + id * 19) % 130 < 10) buttons |= PlayerInput.BtnCrouch;
            if ((t + id * 3) % 60 == 0) buttons |= PlayerInput.BtnUltimate;
            PlayerInput input = PlayerInput.Create(move, aim, buttons, t);
            if ((t + id) % 47 == 0) input.WithMoveTarget(new Vec2((double)id - 5.5, (double)(t % 17) - 8.0));
            if (id % 2 == 0 && phase == (id % 3) * 4 + 1) input.WithAimPoint(new Vec2((double)id - 5.0, id <= 5 ? -6.0 : 6.0));
            return input;
        }

        [TestCase(0)]
        [TestCase(1)]
        public void SnapshotsAndTheMirrorMatchGodot(int scenario)
        {
            using JsonDocument doc = NetKit.Golden("net_snapshots.json");
            JsonElement s = doc.RootElement.GetProperty("scenarios")[scenario];
            long seed = s.GetProperty("seed").GetInt64();
            int ticks = s.GetProperty("ticks").GetInt32();
            GameRules rules = NetKit.Rules.Duplicate();
            if (s.GetProperty("cards").GetBoolean())
            {
                rules.CardCycle = true;
                rules.ElixirEnabled = true;
                rules.StaminaEnabled = true;
                rules.HandSize = 4;
            }
            var sim = new MatchSim(rules, NetKit.Layout, seed);
            for (int id = 1; id <= 10; id++) sim.AddPlayer(id, id <= 5 ? 0 : 1);
            sim.AssignCharacters();
            for (int id = 1; id <= 10; id++)
            {
                sim.Equip(id, false);
                int row = (id - 1) % 5;
                sim.Players[id].Position = new Vec2(-8.0 + 4.0 * row, id <= 5 ? 6.0 : -6.0);
            }
            var mirror = new MatchSim(rules, NetKit.Layout, seed);
            for (int id = 1; id <= 10; id++) mirror.AddPlayer(id, id <= 5 ? 0 : 1);
            var frames = s.GetProperty("frames").EnumerateArray().ToList();
            int frame = 0;
            var acks = new OrderedDict<int, int>();
            for (int t = 0; t < ticks; t++)
            {
                for (int id = 1; id <= 10; id++)
                {
                    sim.SetInput(id, ScriptedInput(id, t));
                    acks[id] = t;
                }
                sim.Step(1.0 / 30.0);
                if (frame >= frames.Count || frames[frame].GetProperty("tick").GetInt32() != sim.Tick) continue;
                JsonElement f = frames[frame++];
                string where = "seed " + seed + " tick " + sim.Tick;
                GDict publicPart = Snapshot.Capture(sim, acks);
                Assert.AreEqual(f.GetProperty("public").GetString(), NetKit.Hex(publicPart), where + ": SNAPSHOT");
                Assert.AreEqual(f.GetProperty("private").GetString(), NetKit.Hex(Snapshot.PrivateFor(sim, 3)), where + ": private part");
                GDict message = publicPart.Duplicate();
                message.Merge(Snapshot.PrivateFor(sim, 3));
                Snapshot.Apply(mirror, message, 3);
                Assert.AreEqual(f.GetProperty("mirror").GetString(), NetKit.Hex(Snapshot.Capture(mirror, new OrderedDict<int, int>())), where + ": mirror");
                Assert.AreEqual(f.GetProperty("mirror_private").GetString(), NetKit.Hex(Snapshot.PrivateFor(mirror, 3)), where + ": mirror private");
                JsonElement[] local = f.GetProperty("mirror_local_pos").EnumerateArray().ToArray();
                Assert.AreEqual((float)local[0].GetDouble(), mirror.Players[3].Position.X, where);
                Assert.AreEqual((float)local[1].GetDouble(), mirror.Players[3].Position.Y, where);
                Assert.AreEqual(f.GetProperty("mirror_powerups").GetString(), NetKit.Hex(Snapshot.PowerupsAsGodot(mirror)), where + ": power-ups");
            }
            Assert.AreEqual(frames.Count, frame, "every recorded snapshot was compared");
        }
    }
}
