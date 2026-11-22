using System.Collections.Generic;
using Kalyeah.Sim;

namespace Kalyeah.Net
{
    /// <summary>
    /// Turns the server's MatchSim into plain wire data (the SNAPSHOT message) and writes that data into a client's mirror MatchSim,
    /// which the match views read (port of snapshot.gd). Player floats are snapped to 0.01 and default fields are left out (and put
    /// back to their defaults in `Apply`), exactly as the Godot server does, so the bytes are the same.
    /// Spec kit fields (docs/KITS.md) are emitted ONLY when `sim.Rules.UsesSpecKits()` is true so all recorded goldens stay byte-identical.
    /// </summary>
    public static class Snapshot
    {
        static double Snap(double value) => Wire.Snapped(value, 0.01);
        static Vec2 SnapVec(Vec2 v) => Wire.SnapVec(v, 0.01);

        public static GDict Capture(MatchSim sim, OrderedDict<int, int> acks)
        {
            bool specKits = sim.Rules.UsesSpecKits();
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
                if (p.JogTime > 0.0) entry["jog"] = Snap(p.JogTime);
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

                if (specKits)
                {
                    if (p.UsingSpecKit) entry["kit"] = true;
                    if (p.BlurLeft > 0.0) entry["blr"] = Snap(p.BlurLeft);
                    if (p.RevealedOutlineLeft > 0.0) entry["rev"] = Snap(p.RevealedOutlineLeft);
                    if (p.RevealAlertLeft > 0.0) entry["ra"] = Snap(p.RevealAlertLeft);
                    if (p.KnownRevealAlert) entry["ral"] = true;
                    if (p.InvisibleLeft > 0.0) entry["inv"] = Snap(p.InvisibleLeft);
                    if (p.StiltsLeft > 0.0) entry["stl"] = Snap(p.StiltsLeft);
                    if (p.SackRaceLeft > 0.0) entry["sk"] = Snap(p.SackRaceLeft);
                    if (p.LuksongBakaLeft > 0.0) entry["bk"] = Snap(p.LuksongBakaLeft);
                    if (p.TrumpoSpinLeft > 0.0) entry["spn"] = Snap(p.TrumpoSpinLeft);
                    if (p.SungkaAbsorbLeft > 0.0) entry["sgk"] = Snap(p.SungkaAbsorbLeft);
                    if (p.ShieldHp > 0.0) entry["shd"] = Snap(p.ShieldHp);
                    if (p.ShieldHpLeft > 0.0) entry["shdl"] = Snap(p.ShieldHpLeft);
                    if (p.LangitLeft > 0.0) entry["lng"] = Snap(p.LangitLeft);
                    if (p.TayaCountLeft > 0.0) entry["tya"] = Snap(p.TayaCountLeft);
                    if (p.EmpoweredTiradorBonus > 0.0 && p.EmpoweredTiradorLeft > 0.0) entry["emp"] = Snap(p.EmpoweredTiradorBonus);
                    if (p.SpeedBoostLeft > 0.0) entry["sbl"] = Snap(p.SpeedBoostLeft);
                }
                players.Add(entry);
            }
            var projectiles = new GArray(VariantType.Dictionary);
            foreach (WeaponSystem.Projectile projectile in sim.Weapons.Projectiles)
                projectiles.Add(GDict.Of("id", (long)projectile.Id, "w", new StringName(projectile.EffectId), "pos", projectile.Position, "team", (long)projectile.Team));
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
            if (sim.BarrierLeft > 0.0) output["bar"] = sim.BarrierLeft;
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
            if (specKits) CaptureSpecWorldEntities(sim, output);
            return output;
        }

        static void CaptureSpecWorldEntities(MatchSim sim, GDict output)
        {
            if (sim.Weapons.Walls.Count > 0)
            {
                var arr = new GArray(VariantType.Dictionary);
                foreach (WeaponSystem.DestructibleWall w in sim.Weapons.Walls)
                    arr.Add(GDict.Of("id", (long)w.Id, "o", (long)w.OwnerId, "t", (long)w.Team, "s", new StringName(w.SkillId),
                        "c", SnapVec(w.Center), "al", SnapVec(w.Along), "hw", Snap(w.HalfWidth),
                        "hp", (long)w.Hp, "mhp", (long)w.MaxHp, "tl", Snap(w.TimeLeft), "d", Snap(w.Duration)));
                output["walls"] = arr;
            }
            if (sim.Weapons.Traps.Count > 0)
            {
                var arr = new GArray(VariantType.Dictionary);
                foreach (WeaponSystem.HiddenTrap tr in sim.Weapons.Traps)
                {
                    var d = GDict.Of("id", (long)tr.Id, "o", (long)tr.OwnerId, "t", (long)tr.Team, "s", new StringName(tr.SkillId),
                        "m", (long)tr.Mechanic, "p", SnapVec(tr.Position), "r", Snap(tr.TriggerRadius));
                    if (tr.Armed) d["ar"] = true;
                    if (tr.RevealedToEnemy) d["rv"] = true;
                    arr.Add(d);
                }
                output["traps"] = arr;
            }
            if (sim.Weapons.Sentries.Count > 0)
            {
                var arr = new GArray(VariantType.Dictionary);
                foreach (WeaponSystem.Sentry s in sim.Weapons.Sentries)
                    arr.Add(GDict.Of("id", (long)s.Id, "o", (long)s.OwnerId, "t", (long)s.Team, "s", new StringName(s.SkillId),
                        "p", SnapVec(s.Position), "r", Snap(s.Radius), "tl", Snap(s.TimeLeft), "d", Snap(s.Duration),
                        "pt", Snap(s.PingTimer), "pi", Snap(s.PingInterval), "hp", (long)s.Hp));
                output["sentries"] = arr;
            }
            if (sim.Weapons.Tethers.Count > 0)
            {
                var arr = new GArray(VariantType.Dictionary);
                foreach (WeaponSystem.AllyTether te in sim.Weapons.Tethers)
                    arr.Add(GDict.Of("id", (long)te.Id, "o", (long)te.OwnerId, "a", (long)te.AllyId, "t", (long)te.Team,
                        "s", new StringName(te.SkillId), "mr", Snap(te.MaxRange), "tl", Snap(te.TimeLeft), "d", Snap(te.Duration)));
                output["tethers"] = arr;
            }
            if (sim.Weapons.SkillZones.Count > 0)
            {
                var arr = new GArray(VariantType.Dictionary);
                foreach (WeaponSystem.SkillZone z in sim.Weapons.SkillZones)
                    arr.Add(GDict.Of("id", (long)z.Id, "o", (long)z.OwnerId, "t", (long)z.Team, "s", new StringName(z.SkillId),
                        "k", (long)z.Kind, "p", SnapVec(z.Position), "ep", SnapVec(z.EndPosition), "dir", SnapVec(z.Direction),
                        "al", SnapVec(z.Along), "r", Snap(z.Radius), "hw", Snap(z.HalfWidth), "ang", Snap(z.AngleDegrees),
                        "cnt", (long)z.Count, "sp", Snap(z.Spacing), "tl", Snap(z.TimeLeft), "d", Snap(z.Duration), "dl", Snap(z.DelayLeft)));
                output["szones"] = arr;
            }
            if (sim.Weapons.RevealMarkers.Count > 0)
            {
                var arr = new GArray(VariantType.Dictionary);
                foreach (WeaponSystem.RevealMarker m in sim.Weapons.RevealMarkers)
                    arr.Add(GDict.Of("id", (long)m.Id, "t", (long)m.Team, "tg", (long)m.TargetId, "s", new StringName(m.SkillId),
                        "p", SnapVec(m.Position), "tl", Snap(m.TimeLeft), "d", Snap(m.Duration)));
                output["rmarks"] = arr;
            }
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
        /// The part only `id`'s own phone gets (merged into its copy as "me"): its elixir, stamina, move target, and (under spec kits)
        /// water, Tirador ammo/reload/charge, S1 charges, recast window, and home fountain status.
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
            if (sim.Rules.UsesSpecKits())
            {
                mine["wat"] = Snap(p.Water);
                mine["ammo"] = (long)p.TiradorAmmo;
                if (p.TiradorReloadLeft > 0.0) mine["rld"] = Snap(p.TiradorReloadLeft);
                if (p.TiradorChargeTime >= 0.0) mine["chg"] = Snap(p.TiradorChargeTime);
                mine["c0"] = (long)p.S1Charges;
                if (p.S1ChargeRegenLeft > 0.0) mine["c0t"] = Snap(p.S1ChargeRegenLeft);
                if (p.SyatoRecastLeft > 0.0)
                {
                    mine["rcst"] = Snap(p.SyatoRecastLeft);
                    mine["rctg"] = (long)p.SyatoTargetId;
                }
                if (p.InHomeFountain) mine["fnt"] = true;
            }
            return GDict.Of("me", mine);
        }

        /// <summary>
        /// `message` (a snapshot for `viewer`'s phone) without what that phone may not know: Langit Lupa brownout hidden kids,
        /// plus (under spec kits) invisible enemies (Tago!) and hidden enemy traps outside 3m/unrevealed.
        /// </summary>
        public static GDict HideFor(MatchSim sim, int viewer, GDict message)
        {
            if (sim.Mode is StreetMode mode)
            {
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

            if (!sim.Rules.UsesSpecKits() || !sim.Players.TryGetValue(viewer, out PlayerState vp)) return message;
            int viewerTeam = vp.Team;
            bool needFilter = false;
            foreach (var pair in sim.Players)
                if (pair.Value.Team != viewerTeam && !sim.IsEnemyVisibleToTeam(pair.Value, viewerTeam)) { needFilter = true; break; }
            if (!needFilter && sim.Weapons.Traps.Count > 0)
            {
                for (int i = 0; i < sim.Weapons.Traps.Count; i++)
                    if (sim.Weapons.Traps[i].Team != viewerTeam && !sim.IsTrapVisibleToTeam(sim.Weapons.Traps[i], viewerTeam)) { needFilter = true; break; }
            }
            if (!needFilter && sim.Weapons.RevealMarkers.Count > 0)
            {
                for (int i = 0; i < sim.Weapons.RevealMarkers.Count; i++)
                    if (sim.Weapons.RevealMarkers[i].Team != viewerTeam) { needFilter = true; break; }
            }
            if (!needFilter) return message;

            GDict filtered = message.Duplicate();
            var filteredPlayers = new GArray(VariantType.Dictionary);
            foreach (object raw in message.GetArr("players"))
            {
                if (!(raw is GDict entry)) continue;
                int pid = entry.GetInt("id", -1);
                if (sim.Players.TryGetValue(pid, out PlayerState target) && !sim.IsEnemyVisibleToTeam(target, viewerTeam))
                {
                    GDict blank = entry.Duplicate();
                    blank["pos"] = Vec2.Zero;
                    blank["hid"] = true;
                    filteredPlayers.Add(blank);
                }
                else
                {
                    filteredPlayers.Add(entry);
                }
            }
            filtered["players"] = filteredPlayers;

            if (message.Has("traps"))
            {
                var visibleTraps = new GArray(VariantType.Dictionary);
                foreach (object raw in message.GetArr("traps"))
                {
                    if (!(raw is GDict td)) continue;
                    int trapId = td.GetInt("id", -1);
                    WeaponSystem.HiddenTrap found = null;
                    for (int i = 0; i < sim.Weapons.Traps.Count; i++)
                        if (sim.Weapons.Traps[i].Id == trapId) { found = sim.Weapons.Traps[i]; break; }
                    if (found == null || sim.IsTrapVisibleToTeam(found, viewerTeam)) visibleTraps.Add(td);
                }
                filtered["traps"] = visibleTraps;
            }
            if (message.Has("rmarks"))
            {
                var teamMarkers = new GArray(VariantType.Dictionary);
                foreach (object raw in message.GetArr("rmarks"))
                    if (raw is GDict rd && rd.GetInt("t", -1) == viewerTeam) teamMarkers.Add(rd);
                filtered["rmarks"] = teamMarkers;
            }
            return filtered;
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
                p.JogTime = e.GetFloat("jog");
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
                p.UsingSpecKit = e.GetBool("kit");
                p.BlurLeft = e.GetFloat("blr");
                p.RevealedOutlineLeft = e.GetFloat("rev");
                p.RevealAlertLeft = e.GetFloat("ra");
                p.KnownRevealAlert = e.GetBool("ral");
                p.InvisibleLeft = e.GetFloat("inv");
                p.StiltsLeft = e.GetFloat("stl");
                p.SackRaceLeft = e.GetFloat("sk");
                p.LuksongBakaLeft = e.GetFloat("bk");
                p.TrumpoSpinLeft = e.GetFloat("spn");
                p.SungkaAbsorbLeft = e.GetFloat("sgk");
                p.ShieldHp = e.GetFloat("shd");
                p.ShieldHpLeft = e.GetFloat("shdl");
                p.LangitLeft = e.GetFloat("lng");
                p.TayaCountLeft = e.GetFloat("tya");
                p.EmpoweredTiradorBonus = e.GetFloat("emp");
                p.EmpoweredTiradorLeft = p.EmpoweredTiradorBonus > 0.0 ? 0.5 : 0.0;
                p.SpeedBoostLeft = e.GetFloat("sbl");
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
            sim.BarrierLeft = data.GetFloat("bar", 0.0);
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

        /// <summary>The recipient's own elixir, stamina, move target, and spec kit resources (absent from an old server: mirror keeps its own).</summary>
        static void ApplyPrivate(MatchSim sim, GDict mine, int localId)
        {
            if (mine.IsEmpty || mine.GetInt("id", -1) != localId || !sim.Players.TryGetValue(localId, out PlayerState p)) return;
            p.Elixir = mine.GetFloat("el", p.Elixir);
            p.Stamina = mine.GetFloat("sta", p.Stamina);
            p.StillTime = mine.GetFloat("still", p.StillTime);
            p.HasMoveTarget = mine.GetBool("hmt");
            p.MoveTarget = mine.GetVec2("mt", Vec2.Zero);
            p.StuckTime = mine.GetFloat("stk");
            if (mine.Has("wat")) p.Water = mine.GetFloat("wat", p.Water);
            if (mine.Has("ammo")) p.TiradorAmmo = mine.GetInt("ammo", p.TiradorAmmo);
            p.TiradorReloadLeft = mine.GetFloat("rld");
            p.TiradorChargeTime = mine.Has("chg") ? mine.GetFloat("chg") : -1.0;
            if (mine.Has("c0")) p.S1Charges = mine.GetInt("c0", p.S1Charges);
            p.S1ChargeRegenLeft = mine.GetFloat("c0t");
            p.SyatoRecastLeft = mine.GetFloat("rcst");
            p.SyatoTargetId = mine.Has("rctg") ? mine.GetInt("rctg", -1) : -1;
            p.InHomeFountain = mine.GetBool("fnt");
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
                if (!(raw is GDict e)) continue;
                string wid = SName(e, "w");
                sim.WeaponDefs.TryGetValue(wid, out WeaponDef def);
                sim.SkillDefs.TryGetValue(wid, out SkillDef skill);
                if (def == null && skill == null) continue;
                system.Projectiles.Add(new WeaponSystem.Projectile
                {
                    Id = e.GetInt("id"), Def = def, Skill = skill, Position = e.GetVec2("pos", Vec2.Zero), Team = e.GetInt("team"),
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

            system.Walls.Clear();
            foreach (object raw in data.GetArr("walls"))
            {
                if (!(raw is GDict e)) continue;
                system.Walls.Add(new WeaponSystem.DestructibleWall
                {
                    Id = e.GetInt("id"), OwnerId = e.GetInt("o"), Team = e.GetInt("t"), SkillId = SName(e, "s"),
                    Center = e.GetVec2("c", Vec2.Zero), Along = e.GetVec2("al", new Vec2(1.0, 0.0)), HalfWidth = e.GetFloat("hw"),
                    Hp = e.GetInt("hp"), MaxHp = e.GetInt("mhp"), TimeLeft = e.GetFloat("tl"), Duration = e.GetFloat("d"),
                });
            }
            system.Traps.Clear();
            foreach (object raw in data.GetArr("traps"))
            {
                if (!(raw is GDict e)) continue;
                system.Traps.Add(new WeaponSystem.HiddenTrap
                {
                    Id = e.GetInt("id"), OwnerId = e.GetInt("o"), Team = e.GetInt("t"), SkillId = SName(e, "s"),
                    Mechanic = (SkillMechanic)e.GetInt("m"), Position = e.GetVec2("p", Vec2.Zero), TriggerRadius = e.GetFloat("r"),
                    Armed = e.GetBool("ar"), RevealedToEnemy = e.GetBool("rv"), TimeLeft = 9999.0,
                });
            }
            system.Sentries.Clear();
            foreach (object raw in data.GetArr("sentries"))
            {
                if (!(raw is GDict e)) continue;
                system.Sentries.Add(new WeaponSystem.Sentry
                {
                    Id = e.GetInt("id"), OwnerId = e.GetInt("o"), Team = e.GetInt("t"), SkillId = SName(e, "s"),
                    Position = e.GetVec2("p", Vec2.Zero), Radius = e.GetFloat("r"), TimeLeft = e.GetFloat("tl"),
                    Duration = e.GetFloat("d"), PingTimer = e.GetFloat("pt"), PingInterval = e.GetFloat("pi", 1.0), Hp = e.GetInt("hp", 1),
                });
            }
            system.Tethers.Clear();
            foreach (object raw in data.GetArr("tethers"))
            {
                if (!(raw is GDict e)) continue;
                system.Tethers.Add(new WeaponSystem.AllyTether
                {
                    Id = e.GetInt("id"), OwnerId = e.GetInt("o"), AllyId = e.GetInt("a"), Team = e.GetInt("t"),
                    SkillId = SName(e, "s"), MaxRange = e.GetFloat("mr"), TimeLeft = e.GetFloat("tl"), Duration = e.GetFloat("d"),
                });
            }
            system.SkillZones.Clear();
            foreach (object raw in data.GetArr("szones"))
            {
                if (!(raw is GDict e)) continue;
                system.SkillZones.Add(new WeaponSystem.SkillZone
                {
                    Id = e.GetInt("id"), OwnerId = e.GetInt("o"), Team = e.GetInt("t"), SkillId = SName(e, "s"),
                    Kind = (SkillZoneKind)e.GetInt("k"), Position = e.GetVec2("p", Vec2.Zero), EndPosition = e.GetVec2("ep", Vec2.Zero),
                    Direction = e.GetVec2("dir", Vec2.Zero), Along = e.GetVec2("al", new Vec2(1.0, 0.0)), Radius = e.GetFloat("r"),
                    HalfWidth = e.GetFloat("hw"), AngleDegrees = e.GetFloat("ang"), Count = e.GetInt("cnt", 1), Spacing = e.GetFloat("sp"),
                    TimeLeft = e.GetFloat("tl"), Duration = e.GetFloat("d"), DelayLeft = e.GetFloat("dl"),
                });
            }
            system.RevealMarkers.Clear();
            foreach (object raw in data.GetArr("rmarks"))
            {
                if (!(raw is GDict e)) continue;
                system.RevealMarkers.Add(new WeaponSystem.RevealMarker
                {
                    Id = e.GetInt("id"), Team = e.GetInt("t"), TargetId = e.GetInt("tg"), SkillId = SName(e, "s"),
                    Position = e.GetVec2("p", Vec2.Zero), TimeLeft = e.GetFloat("tl"), Duration = e.GetFloat("d"),
                });
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
