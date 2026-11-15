using System.Collections.Generic;
using Kalyeah.Sim;

namespace Kalyeah.Net
{
    /// <summary>
    /// Turns the server's MatchSim into plain wire data (the SNAPSHOT message) and writes that data into a client's mirror MatchSim,
    /// which the match views read (port of snapshot.gd). Player floats are snapped to 0.01 and default fields are left out (and put
    /// back to their defaults in `Apply`), exactly as the Godot server does, so the bytes are the same.
    /// </summary>
    public static class Snapshot
    {
        static double Snap(double value) => Wire.Snapped(value, 0.01);
        static Vec2 SnapVec(Vec2 v) => Wire.SnapVec(v, 0.01);

        public static GDict Capture(MatchSim sim, OrderedDict<int, int> acks)
        {
            var players = new GArray(VariantType.Dictionary);
            foreach (var pair in sim.Players)
            {
                int id = pair.Key;
                PlayerState p = pair.Value;
                var entry = GDict.Of("id", (long)id, "pos", p.Tagged ? p.Position : SnapVec(p.Position), "face", SnapVec(p.Facing),
                    "hp", (long)p.Hp, "w", GArray.StringNames(p.Weapons));
                if (p.Tagged) entry["tag"] = true;
                if (p.TagTime > 0.0) entry["tt"] = Snap(p.TagTime);
                bool hasCd = false;
                foreach (double cd in p.WeaponCooldowns)
                    if (cd > 0.0)
                    {
                        hasCd = true;
                        break;
                    }
                if (hasCd)
                {
                    var snapped = new GArray(VariantType.Float);
                    foreach (double cd in p.WeaponCooldowns) snapped.Add(Snap(cd));
                    entry["wcd"] = snapped;
                }
                GDict fx = EffectsSnapshot(p.Effects);
                if (fx.Count > 1 || fx.GetFloat("slow") != 0.0) entry["fx"] = fx;
                if (p.CastLockLeft > 0.0) entry["cl"] = Snap(p.CastLockLeft);
                if (p.NextCard != "") entry["nx"] = new StringName(p.NextCard);
                if (p.ShuffleCard != "") entry["sc"] = new StringName(p.ShuffleCard);
                if (p.ShuffleLeft > 0.0) entry["sl"] = Wire.Snapped(p.ShuffleLeft, 0.001);
                if (p.ShuffleTotal > 0.0) entry["st"] = Wire.Snapped(p.ShuffleTotal, 0.001);
                if (p.Cards) entry["cd"] = true;
                if (p.Incoming.Count > 0) entry["in"] = GArray.StringNames(p.Incoming);
                if (p.ExhaustLeft > 0.0) entry["ex"] = Snap(p.ExhaustLeft);
                if (p.Overclocked) entry["oc"] = true;
                if (p.TagOrder != 0) entry["to"] = (long)p.TagOrder;
                if (p.PrimaryCd > 0.0) entry["pcd"] = Snap(p.PrimaryCd);
                if (p.DashCd > 0.0) entry["dcd"] = Snap(p.DashCd);
                if (p.DashLeft > 0.0) entry["dl"] = Snap(p.DashLeft);
                if (p.DashLeft > 0.0 || p.DashDir != Vec2.Zero) entry["dd"] = SnapVec(p.DashDir);
                if (p.UltCharge > 0.0) entry["ult"] = Snap(p.UltCharge);
                if (p.Sprinting) entry["spr"] = true;
                // locomotion and street power-up buffs (left out when false / 0)
                if (p.Walking) entry["wlk"] = true;
                if (p.Crouching) entry["cr"] = true;
                if (p.AirY > 0.0) entry["ay"] = Snap(p.AirY);
                if (p.AirY > 0.0 || System.Math.Abs(p.VelY) > 0.005) entry["vy"] = Snap(p.VelY);
                if (p.JumpCd > 0.0) entry["jcd"] = Snap(p.JumpCd);
                if (p.SlideLeft > 0.0)
                {
                    entry["sll"] = Snap(p.SlideLeft);
                    entry["sld"] = SnapVec(p.SlideDir);
                }
                if (p.SlideCd > 0.0) entry["scd"] = Snap(p.SlideCd);
                if (p.BuffType != 0) entry["bt"] = (long)p.BuffType;
                if (p.BuffLeft > 0.0) entry["bl"] = Snap(p.BuffLeft);
                players.Add(entry);
            }
            var projectiles = new GArray(VariantType.Dictionary);
            foreach (WeaponSystem.Projectile projectile in sim.Weapons.Projectiles)
                projectiles.Add(GDict.Of("id", (long)projectile.Id, "w", new StringName(projectile.Def.Id), "pos", projectile.Position, "team", (long)projectile.Team));
            var zones = new GArray(VariantType.Dictionary);
            foreach (WeaponSystem.Zone zone in sim.Weapons.Zones)
                zones.Add(GDict.Of("id", (long)zone.Id, "w", new StringName(zone.Def.Id), "k", (long)zone.Kind, "c", zone.Center, "r", zone.Radius, "team", (long)zone.Team));
            var shields = new GArray(VariantType.Dictionary);
            foreach (WeaponSystem.Shield shield in sim.Weapons.Shields)
                shields.Add(GDict.Of("id", (long)shield.Id, "w", new StringName(shield.Def.Id), "c", shield.Center, "al", shield.Along,
                    "hw", shield.HalfWidth, "team", (long)shield.Team, "tl", shield.TimeLeft, "o", shield.Origin, "bhw", shield.BaseHalfWidth));
            var ackDict = new GDict(VariantType.Int, VariantType.Int);
            foreach (var ack in acks) ackDict[(long)ack.Key] = (long)ack.Value;
            var log = new GArray();
            foreach (Vec2I entry in sim.PointLog) log.Add(GArray.Of((long)entry.X, (long)entry.Y));
            var rescuers = new GArray(VariantType.Int);
            foreach (int who in sim.RescueRescuer) rescuers.Add((long)who);
            var output = GDict.Of(
                "t", (long)NetProtocol.Msg.Snapshot, "tick", (long)sim.Tick, "acks", ackDict,
                "players", players, "projectiles", projectiles, "zones", zones, "shields", shields,
                "phase", (long)sim.Phase, "freeze", sim.FreezeLeft, "intro", sim.IntroLeft, "clock", sim.MatchTime, "round", sim.RoundTime,
                "score", GDict.Of("p", GArray.Ints(sim.Score.Points), "ft", (long)sim.Score.FirstTo, "w", (long)sim.Score.Winner),
                "log", log,
                "cap", GDict.Of("team", (long)sim.CaptureTeam, "k", (long)sim.CurrentCaptureKind, "e", sim.CaptureElapsed, "need", sim.CaptureNeeded, "r", sim.CaptureRate),
                "resc", GDict.Of("who", rescuers, "e", GArray.Floats(sim.RescueElapsed), "ready", GArray.Floats(sim.RescueReadyLeft)),
                "mode", sim.Mode is StreetMode mode ? StreetWire.Snapshot(mode) : new GDict());
            if (sim.Powerups.Count > 0)
            {
                var wire = new GArray(VariantType.Dictionary);
                foreach (MatchSim.Powerup item in sim.Powerups)
                {
                    var entry = GDict.Of("i", (long)item.Id, "k", (long)item.Kind, "x", Snap(item.Pos.x), "y", Snap(item.Pos.y));
                    if (!item.Active) entry["a"] = false;
                    if (item.RespawnLeft > 0.0) entry["r"] = Snap(item.RespawnLeft);
                    wire.Add(entry);
                }
                output["pu"] = wire;
            }
            return output;
        }

        /// <summary>status_effects.gd `snapshot()`: {type: time left, ..., "slow": magnitude}.</summary>
        public static GDict EffectsSnapshot(StatusEffects effects)
        {
            var data = new GDict();
            foreach (var pair in effects.Active) data[(long)pair.Key] = pair.Value;
            data["slow"] = effects.SlowMagnitude;
            return data;
        }

        /// <summary>
        /// The part only `id`'s own phone gets (merged into its copy as "me"): its elixir, stamina and move target. Nobody else learns
        /// where a kid is walking or how much elixir it has.
        /// </summary>
        public static GDict PrivateFor(MatchSim sim, int id)
        {
            if (!sim.Players.TryGetValue(id, out PlayerState p)) return new GDict();
            var mine = GDict.Of("id", (long)id, "el", Snap(p.Elixir), "sta", Snap(p.Stamina), "still", Snap(p.StillTime));
            if (p.HasMoveTarget)
            {
                mine["hmt"] = true;
                mine["mt"] = SnapVec(p.MoveTarget);
            }
            if (p.StuckTime > 0.0) mine["stk"] = Snap(p.StuckTime);
            return GDict.Of("me", mine);
        }

        /// <summary>
        /// `message` (a snapshot for `viewer`'s phone) without what that phone may not know: in Langit Lupa's brownout, where the kids
        /// out of its sight are ("hid", the middle of the street) and their mounds. `message` itself when nothing is hidden.
        /// </summary>
        public static GDict HideFor(MatchSim sim, int viewer, GDict message)
        {
            if (!(sim.Mode is StreetMode mode)) return message;
            List<int> hidden = mode.HiddenFrom(sim, viewer);
            if (hidden.Count == 0) return message;
            GDict output = message.Duplicate();
            var players = new GArray(VariantType.Dictionary);
            foreach (object raw in message.GetArr("players"))
            {
                if (!(raw is GDict entry)) continue;
                if (hidden.Contains(entry.GetInt("id", -1)))
                {
                    GDict blank = entry.Duplicate();
                    blank["pos"] = Vec2.Zero;
                    blank["hid"] = true;
                    foreach (string key in new[] { "wlk", "cr", "ay", "vy", "sll", "sld" }) blank.Remove(key);
                    players.Add(blank);
                }
                else
                {
                    players.Add(entry);
                }
            }
            output["players"] = players;
            GDict modeData = StreetWire.FilterFor(mode, message.GetDict("mode"), hidden);
            var unseen = new GArray();
            foreach (int id in hidden) unseen.Add((long)id);
            modeData["unseen"] = unseen;
            output["mode"] = modeData;
            return output;
        }

        /// <summary>Writes `data` into `sim`. `localId`'s position is left to the caller (prediction).</summary>
        public static void Apply(MatchSim sim, GDict data, int localId)
        {
            sim.Tick = data.GetInt("tick", sim.Tick);
            foreach (object raw in data.GetArr("players"))
            {
                if (!(raw is GDict e)) continue;
                int id = e.GetInt("id", -1);
                if (!sim.Players.TryGetValue(id, out PlayerState p)) continue;
                bool hidden = e.GetBool("hid");
                p.CastLockLeft = e.GetFloat("cl");
                if (id != localId && !hidden) p.Position = e.GetVec2("pos", p.Position); // a hidden kid keeps where it was last seen
                p.Facing = e.GetVec2("face", p.Facing);
                p.Hp = e.GetInt("hp", p.Hp);
                p.Tagged = e.GetBool("tag");
                p.TagTime = e.GetFloat("tt");
                p.Weapons.Clear();
                p.Weapons.AddRange(Names(e.GetArr("w")));
                p.ResizeSlots();
                for (int slot = 0; slot < p.WeaponCooldowns.Count; slot++) p.WeaponCooldowns[slot] = 0.0;
                if (e.Get("wcd") is GArray wcd)
                    for (int slot = 0; slot < System.Math.Min(wcd.Count, p.WeaponCooldowns.Count); slot++) p.WeaponCooldowns[slot] = Wire.Number(wcd[slot]);
                p.Cards = e.GetBool("cd");
                p.NextCard = SName(e, "nx");
                p.ShuffleCard = SName(e, "sc");
                p.ShuffleLeft = e.GetFloat("sl");
                p.ShuffleTotal = e.GetFloat("st");
                p.Incoming.Clear();
                p.Incoming.AddRange(Names(e.GetArr("in")));
                p.ExhaustLeft = e.GetFloat("ex");
                p.Overclocked = e.GetBool("oc");
                p.TagOrder = e.GetInt("to");
                p.PrimaryCd = e.GetFloat("pcd");
                p.DashCd = e.GetFloat("dcd");
                p.DashLeft = e.GetFloat("dl");
                p.DashDir = e.GetVec2("dd", Vec2.Zero);
                p.UltCharge = e.GetFloat("ult");
                p.Sprinting = e.GetBool("spr");
                p.Walking = e.GetBool("wlk");
                p.Crouching = e.GetBool("cr");
                p.AirY = e.GetFloat("ay");
                p.VelY = e.GetFloat("vy");
                p.JumpCd = e.GetFloat("jcd");
                double slideLeft = e.GetFloat("sll");
                p.SlideLeft = slideLeft;
                p.SlideCd = e.GetFloat("scd");
                p.SlideDir = slideLeft > 0.0 ? e.GetVec2("sld", Vec2.Zero) : Vec2.Zero;
                p.BuffType = e.GetInt("bt");
                p.BuffLeft = e.GetFloat("bl");
                RestoreEffects(p.Effects, e.GetDict("fx"));
            }
            ApplyPrivate(sim, data.GetDict("me"), localId);
            ApplyWeapons(sim, data);
            ApplyPowerups(sim, data);
            bool wasIntro = sim.Phase == MatchSim.MatchPhase.Intro;
            sim.Phase = (MatchSim.MatchPhase)data.GetInt("phase", (int)sim.Phase);
            sim.IntroLeft = data.GetFloat("intro");
            sim.MatchTime = data.GetFloat("clock", sim.MatchTime);
            sim.RoundTime = data.GetFloat("round", sim.RoundTime);
            if (wasIntro && sim.Phase == MatchSim.MatchPhase.Playing) EventRaiser.Raise(sim, nameof(MatchSim.IntroFinished));
            sim.FreezeLeft = data.GetFloat("freeze");
            GDict score = data.GetDict("score");
            if (!score.IsEmpty)
            {
                GArray points = score.GetArr("p");
                var pts = new int[points.Count];
                for (int i = 0; i < pts.Length; i++) pts[i] = (int)Wire.TruncateToLong(Wire.Number(points[i]));
                sim.Score.Points = pts;
                sim.Score.FirstTo = score.GetInt("ft", sim.Score.FirstTo);
                sim.Score.Winner = score.GetInt("w", sim.Score.Winner);
            }
            if (data.Has("log"))
            {
                sim.PointLog.Clear();
                foreach (object raw in data.GetArr("log"))
                    if (raw is GArray entry && entry.Count >= 2)
                        sim.PointLog.Add(new Vec2I((int)Wire.TruncateToLong(Wire.Number(entry[0])), (int)Wire.TruncateToLong(Wire.Number(entry[1]))));
            }
            GDict capture = data.GetDict("cap");
            if (!capture.IsEmpty)
            {
                sim.CaptureTeam = capture.GetInt("team", sim.CaptureTeam);
                sim.CurrentCaptureKind = (MatchSim.CaptureKind)capture.GetInt("k", (int)sim.CurrentCaptureKind);
                sim.CaptureElapsed = capture.GetFloat("e", sim.CaptureElapsed);
                sim.CaptureNeeded = capture.GetFloat("need", sim.CaptureNeeded);
                sim.CaptureRate = capture.GetFloat("r", 1.0);
            }
            GDict rescue = data.GetDict("resc");
            if (!rescue.IsEmpty)
            {
                GArray who = rescue.GetArr("who");
                GArray elapsed = rescue.GetArr("e");
                for (int team = 0; team < System.Math.Min(2, System.Math.Min(who.Count, elapsed.Count)); team++)
                {
                    sim.RescueRescuer[team] = (int)Wire.TruncateToLong(Wire.Number(who[team]));
                    sim.RescueElapsed[team] = Wire.Number(elapsed[team]);
                }
                GArray ready = rescue.GetArr("ready");
                for (int team = 0; team < System.Math.Min(2, ready.Count); team++) sim.RescueReadyLeft[team] = Wire.Number(ready[team]);
            }
            if (sim.Mode is StreetMode mode && data.Get("mode", new GDict()) is GDict modeData) mode.Restore(StreetWire.FromWire(mode, modeData), sim);
        }

        static void RestoreEffects(StatusEffects effects, GDict data)
        {
            var restored = new OrderedDict<string, double>();
            restored["slow"] = data.GetFloat("slow");
            foreach (var pair in data)
                if (Wire.IsInt(pair.Key)) restored[Wire.AsLong(pair.Key).ToString(System.Globalization.CultureInfo.InvariantCulture)] = Wire.Number(pair.Value);
            effects.Restore(restored);
        }

        /// <summary>The recipient's own elixir, stamina and move target (absent from an old server: the mirror keeps its own).</summary>
        static void ApplyPrivate(MatchSim sim, GDict mine, int localId)
        {
            if (mine.IsEmpty || mine.GetInt("id", -1) != localId || !sim.Players.TryGetValue(localId, out PlayerState p)) return;
            p.Elixir = mine.GetFloat("el", p.Elixir);
            p.Stamina = mine.GetFloat("sta", p.Stamina);
            p.StillTime = mine.GetFloat("still", p.StillTime);
            p.HasMoveTarget = mine.GetBool("hmt");
            p.MoveTarget = mine.GetVec2("mt", Vec2.Zero);
            p.StuckTime = mine.GetFloat("stk");
        }

        static void ApplyPowerups(MatchSim sim, GDict data)
        {
            sim.Powerups.Clear();
            foreach (object raw in data.GetArr("pu"))
            {
                if (!(raw is GDict item)) continue;
                Vec2 pos = item.GetVec2("pos", new Vec2(item.GetFloat("x"), item.GetFloat("y")));
                sim.Powerups.Add(new MatchSim.Powerup
                {
                    Id = item.GetInt("i", item.GetInt("id")),
                    Kind = item.GetInt("k", item.GetInt("kind")),
                    Pos = pos,
                    Active = item.GetBool("a", item.GetBool("active", true)),
                    RespawnLeft = item.GetFloat("r", item.GetFloat("respawn_left")),
                });
            }
        }

        /// <summary>The power-ups as match_sim.gd keeps them (Array[Dictionary] of id, kind, pos, active, respawn_left).</summary>
        public static GArray PowerupsAsGodot(MatchSim sim)
        {
            var list = new GArray(VariantType.Dictionary);
            foreach (MatchSim.Powerup item in sim.Powerups)
                list.Add(GDict.Of("id", (long)item.Id, "kind", (long)item.Kind, "pos", item.Pos, "active", item.Active, "respawn_left", item.RespawnLeft));
            return list;
        }

        static void ApplyWeapons(MatchSim sim, GDict data)
        {
            WeaponSystem system = sim.Weapons;
            system.Projectiles.Clear();
            foreach (object raw in data.GetArr("projectiles"))
            {
                if (!(raw is GDict e) || !sim.WeaponDefs.TryGetValue(SName(e, "w"), out WeaponDef def)) continue;
                system.Projectiles.Add(new WeaponSystem.Projectile
                {
                    Id = e.GetInt("id"), Def = def, Position = e.GetVec2("pos", Vec2.Zero), Team = e.GetInt("team"),
                });
            }
            system.Zones.Clear();
            foreach (object raw in data.GetArr("zones"))
            {
                if (!(raw is GDict e) || !sim.WeaponDefs.TryGetValue(SName(e, "w"), out WeaponDef def)) continue;
                system.Zones.Add(new WeaponSystem.Zone
                {
                    Id = e.GetInt("id"), Def = def, Kind = (WeaponSystem.ZoneKind)e.GetInt("k"), Center = e.GetVec2("c", Vec2.Zero),
                    Radius = e.GetFloat("r"), Team = e.GetInt("team"),
                });
            }
            system.Shields.Clear();
            foreach (object raw in data.GetArr("shields"))
            {
                if (!(raw is GDict e)) continue;
                sim.WeaponDefs.TryGetValue(SName(e, "w"), out WeaponDef def);
                var shield = new WeaponSystem.Shield
                {
                    Id = e.GetInt("id"), Def = def, Center = e.GetVec2("c", Vec2.Zero), Along = e.GetVec2("al", new Vec2(1.0, 0.0)),
                    HalfWidth = e.GetFloat("hw"), Team = e.GetInt("team"),
                };
                shield.TimeLeft = e.GetFloat("tl", def != null ? def.Duration : 0.0);
                // its cast place and width let the mirror move and widen it as the server does; an older server sends neither
                shield.Origin = e.GetVec2("o", Vec2.Inf);
                shield.BaseHalfWidth = e.GetFloat("bhw", shield.HalfWidth);
                shield.PrevCenter = shield.Center;
                shield.PrevHalfWidth = shield.HalfWidth;
                system.Shields.Add(shield);
            }
        }

        static string SName(GDict d, string key) => Wire.SName(d.Get(key)).Value;

        static IEnumerable<string> Names(GArray raw)
        {
            foreach (object item in raw)
                if (Wire.IsStringLike(item)) yield return Wire.Text(item);
        }
    }
}
