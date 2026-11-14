using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// Langit Lupa (port of scripts/sim/langit_lupa.gd; docs/GDD.md "Langit Lupa"): one taya chases everyone else over the whole street. A
    /// kid standing on a langit ("heaven": a raised platform, or a mound it raised itself with the Lupa button, BtnCard2) is safe, but only for
    /// `LangitSeconds` in all, refilled on the lupa (the street). The platforms wear out under the kids' feet and break until the next round;
    /// standing on one never refills stamina; and now and then a brownout leaves each kid seeing only the kids near it. A tagged kid is the new
    /// taya and the round resets. Every kid but the taya earns a point for each `SurviveSeconds` it stays free, the tagger a bonus.
    /// </summary>
    public sealed class LangitLupa : TayaGame
    {
        /// <summary>(id): `id` raised a mound (where it stands is in `Mounds`).</summary>
        public event Action<int> MoundRaised;
        /// <summary>(id): the mound of `id` sank back into the street.</summary>
        public event Action<int> MoundFell;
        /// <summary>(index): platform `index` (of `Rules.LangitSpots`) wore out and broke.</summary>
        public event Action<int> SpotBroke;
        /// <summary>(oldTaya, newTaya): `newTaya` took the "it" (tagged).</summary>
        public event Action<int, int> TayaChanged;

        /// <summary>The weapon id the mound's cast is sent to the views as.</summary>
        public const string MoundCast = "langit_lupa";
        /// <summary>A mound is raised this far inside the street's edges (m).</summary>
        public const double MoundMargin = 0.6;

        /// <summary>Platform index to kid-seconds of standing left before it breaks (0: broken, until the next round).</summary>
        public OrderedDict<int, double> WearLeft = new OrderedDict<int, double>();
        /// <summary>id to where its mound stands, and seconds until it sinks.</summary>
        public OrderedDict<int, Vec2> Mounds = new OrderedDict<int, Vec2>();
        public OrderedDict<int, double> MoundLeft = new OrderedDict<int, double>();
        /// <summary>id to seconds until it can raise the next mound.</summary>
        public OrderedDict<int, double> MoundWait = new OrderedDict<int, double>();
        /// <summary>id to seconds of safety left on a langit (drains there, refills on the street).</summary>
        public OrderedDict<int, double> SafeLeft = new OrderedDict<int, double>();
        /// <summary>id to seconds until its next survival point.</summary>
        public OrderedDict<int, double> SurviveLeft = new OrderedDict<int, double>();
        /// <summary>A client's copy only: the kids this phone may not see now (the server left them out of its snapshot in a brownout).</summary>
        public HashSet<int> Unseen = new HashSet<int>();

        readonly OrderedDict<int, bool> _lupaDown = new OrderedDict<int, bool>();
        /// <summary>True on a client's copy (written from a snapshot): who it may see is the server's call (`Unseen`).</summary>
        bool _copy;

        public LangitLupa(StreetModeRules rules) : base(rules) { }

        protected override void AddSyncedFields(List<SyncedField> fields)
        {
            base.AddSyncedFields(fields);
            fields.Add(Table("wear_left", WearLeft));
            fields.Add(Table("mounds", Mounds));
            fields.Add(Table("mound_left", MoundLeft));
            fields.Add(Table("mound_wait", MoundWait));
            fields.Add(Table("safe_left", SafeLeft));
            fields.Add(Table("survive_left", SurviveLeft));
        }

        /// <summary>A client's copy also learns which kids it may not see (`unseen`, a list of ids; absent: it sees everyone).</summary>
        public override void Restore(OrderedDict<string, object> data, MatchSim sim = null)
        {
            base.Restore(data, sim);
            _copy = true;
            Unseen.Clear();
            if (data.TryGetValue("unseen", out object hidden) && hidden is IEnumerable<int> ids)
                foreach (int id in ids) Unseen.Add(id);
        }

        public override void Setup(MatchSim sim)
        {
            base.Setup(sim);
            SyncStamina(sim);
        }

        /// <summary>Only the kids who are not the taya tire (the taya never does).</summary>
        void SyncStamina(MatchSim sim)
        {
            foreach (PlayerState state in sim.Players.Values)
            {
                state.HasStamina = state.Id != TayaId;
                sim.RefillStamina(state);
            }
        }

        // ---- heaven and earth ----------------------------------------------------------------------

        /// <summary>The platform under `position` that still stands, or -1.</summary>
        public int SpotAt(Vec2 position)
        {
            for (int index = 0; index < Rules.LangitSpots.Count; index++)
                if (WearLeft.GetOrDefault(index, 0.0) > 0.0 && Rules.LangitSpots[index].HasPoint(position)) return index;
            return -1;
        }

        /// <summary>True while platform `index` still stands.</summary>
        public bool SpotStanding(int index) => WearLeft.GetOrDefault(index, 0.0) > 0.0;

        /// <summary>How worn platform `index` is (0 new to 1 broken).</summary>
        public double WearShare(int index)
        {
            if (Rules.LangitWearSeconds <= 0.0) return 0.0;
            return 1.0 - GMath.Clampf(WearLeft.GetOrDefault(index, 0.0) / Rules.LangitWearSeconds, 0.0, 1.0);
        }

        /// <summary>The kid whose mound stands under `position`, or -1.</summary>
        public int MoundAt(Vec2 position)
        {
            foreach (var pair in Mounds)
                if (pair.Value.DistanceTo(position) <= Rules.MoundRadius) return pair.Key;
            return -1;
        }

        /// <summary>True while `state` stands on a langit (a platform or a mound), safe or not.</summary>
        public bool OnLangit(PlayerState state) => SpotAt(state.Position) >= 0 || MoundAt(state.Position) >= 0;

        /// <summary>True while the langit under `state` saves it from the taya (its safe time is not used up).</summary>
        public bool IsSafe(PlayerState state) => IsAttacker(state) && OnLangit(state) && SafeLeft.GetOrDefault(state.Id, 0.0) > 0.0;

        /// <summary>How much safe time `id` has left (0 to 1).</summary>
        public double SafeShare(int id) =>
            Rules.LangitSeconds > 0.0 ? GMath.Clampf(SafeLeft.GetOrDefault(id, 0.0) / Rules.LangitSeconds, 0.0, 1.0) : 0.0;

        public override bool IsTaggable(PlayerState state) => IsAttacker(state) && !state.Tagged && !IsSafe(state);

        public override bool CanRest(PlayerState state) => !(Rules.LangitBlocksRest && OnLangit(state));

        /// <summary>True when `state` could raise a mound now: not the taya, free, able to move, its wait is over, and it has safe time left.</summary>
        public bool CanRaise(PlayerState state) =>
            IsAttacker(state) && !state.Tagged && state.Effects.CanMove() && !IsResetting()
            && MoundWait.GetOrDefault(state.Id, 0.0) <= 0.0 && SafeLeft.GetOrDefault(state.Id, 0.0) > 0.0;

        /// <summary>How ready the next mound of `id` is (0 just raised to 1 ready).</summary>
        public double MoundReadyShare(int id)
        {
            if (Rules.MoundCooldown <= 0.0) return 1.0;
            return 1.0 - GMath.Clampf(MoundWait.GetOrDefault(id, 0.0) / Rules.MoundCooldown, 0.0, 1.0);
        }

        /// <summary>The Lupa button raises a mound under the kid (pressed, not held).</summary>
        public override void HandleInput(MatchSim sim, PlayerState state, PlayerInput input, double dt)
        {
            bool pressed = input != null && input.IsPressed(PlayerInput.BtnCard2);
            if (pressed && !_lupaDown.GetOrDefault(state.Id, false) && CanRaise(state)) RaiseMound(sim, state);
            _lupaDown[state.Id] = pressed;
        }

        /// <summary>A mound of earth rises under `state` (its old one, if any, sinks first).</summary>
        public void RaiseMound(MatchSim sim, PlayerState state)
        {
            if (Mounds.ContainsKey(state.Id)) MoundFell?.Invoke(state.Id);
            Vec2 at = sim.Layout.KeepInStreet(state.Position, MoundMargin);
            Mounds[state.Id] = at;
            MoundLeft[state.Id] = Rules.MoundSeconds;
            MoundWait[state.Id] = Rules.MoundCooldown;
            sim.EmitWeaponCast(state.Id, MoundCast);
            MoundRaised?.Invoke(state.Id);
        }

        // ---- the brownout --------------------------------------------------------------------------

        /// <summary>Seconds of play so far (the match clock counts down from `MatchSeconds`).</summary>
        public double Elapsed() => Rules.MatchSeconds - TimeLeft;

        /// <summary>True while the street is dark.</summary>
        public bool IsBrownout() => BrownoutLeft() > 0.0;

        /// <summary>Seconds the brownout still lasts (0 when the lights are on).</summary>
        public double BrownoutLeft()
        {
            if (Rules.BrownoutSeconds <= 0.0 || TimeLeft <= 0.0) return 0.0;
            double since = Elapsed() - Rules.BrownoutFirst;
            if (since < 0.0) return 0.0;
            double into = Rules.BrownoutEvery > 0.0 ? since % Rules.BrownoutEvery : since;
            return GMath.Maxf(0.0, Rules.BrownoutSeconds - into);
        }

        /// <summary>True when the kid `viewer` can see `target` now: always, except in a brownout beyond `BrownoutSight` (or, on a client's
        /// copy, a kid the server left out).</summary>
        public override bool VisibleTo(MatchSim sim, int viewer, int target)
        {
            if (viewer == target || !IsBrownout()) return true;
            if (_copy) return !Unseen.Contains(target);  // the server already measured, on the real positions
            PlayerState a = sim.Players.GetOrDefault(viewer);
            PlayerState b = sim.Players.GetOrDefault(target);
            if (a == null || b == null) return true;
            return a.Position.DistanceTo(b.Position) <= Rules.BrownoutSight;
        }

        public override List<int> HiddenFrom(MatchSim sim, int viewer)
        {
            var hidden = new List<int>();
            if (!IsBrownout() || !sim.Players.ContainsKey(viewer)) return hidden;
            foreach (int id in sim.Players.Keys)
                if (!VisibleTo(sim, viewer, id)) hidden.Add(id);
            return hidden;
        }

        /// <summary>A viewer's copy of the game's data: the mounds of the kids it cannot see are left out.</summary>
        public override OrderedDict<string, object> FilterFor(OrderedDict<string, object> data, IList<int> hidden)
        {
            var output = new OrderedDict<string, object>();
            foreach (var pair in data) output[pair.Key] = pair.Value;
            if (output.GetOrDefault("mounds") is OrderedDict<int, Vec2> mounds)
            {
                OrderedDict<int, Vec2> table = CopyTable(mounds);
                foreach (int id in hidden) table.Remove(id);
                output["mounds"] = table;
            }
            if (output.GetOrDefault("mound_left") is OrderedDict<int, double> left)
            {
                OrderedDict<int, double> table = CopyTable(left);
                foreach (int id in hidden) table.Remove(id);
                output["mound_left"] = table;
            }
            return output;
        }

        // ---- one tick ------------------------------------------------------------------------------

        protected override void StepGame(MatchSim sim, double dt)
        {
            foreach (int id in MoundWait.KeyList()) MoundWait[id] = GMath.Maxf(0.0, MoundWait[id] - dt);
            foreach (int id in MoundLeft.KeyList())
            {
                MoundLeft[id] -= dt;
                if (MoundLeft[id] <= 0.0)
                {
                    Mounds.Remove(id);
                    MoundLeft.Remove(id);
                    MoundFell?.Invoke(id);
                }
            }
            foreach (PlayerState state in sim.Players.Values)
            {
                if (!IsAttacker(state) || state.Tagged) continue;
                StepKid(sim, state, dt);
                if (sim.Phase != MatchSim.MatchPhase.Playing) return;
            }
            CheckTags(sim);
        }

        /// <summary>The safe time of a kid drains on a langit (wearing the platform) and refills on the street; it earns survival points.</summary>
        void StepKid(MatchSim sim, PlayerState state, double dt)
        {
            int id = state.Id;
            int spot = SpotAt(state.Position);
            if (spot >= 0 || MoundAt(state.Position) >= 0)
            {
                SafeLeft[id] = GMath.Maxf(0.0, SafeLeft.GetOrDefault(id, 0.0) - dt);
                if (spot >= 0 && Rules.LangitWearSeconds > 0.0)
                {
                    WearLeft[spot] = GMath.Maxf(0.0, WearLeft[spot] - dt);
                    if (WearLeft[spot] <= 0.0) SpotBroke?.Invoke(spot);
                }
            }
            else
            {
                SafeLeft[id] = GMath.Minf(Rules.LangitSeconds, SafeLeft.GetOrDefault(id, 0.0) + Rules.LangitRefillPerS * dt);
            }
            if (Rules.SurviveSeconds <= 0.0) return;
            SurviveLeft[id] = SurviveLeft.GetOrDefault(id, Rules.SurviveSeconds) - dt;
            if (SurviveLeft[id] <= 0.0)
            {
                SurviveLeft[id] += Rules.SurviveSeconds;
                Award(sim, state, false);
            }
        }

        /// <summary>The taya touched a kid off a langit: the tagger gets its bonus and the kid is the new taya; the round resets.</summary>
        protected override void Tag(MatchSim sim, PlayerState guard, PlayerState runner)
        {
            sim.EmitWeaponCast(guard.Id, TagCast);
            EmitTagged(guard.Id, runner.Id);
            if (Rules.TagPoints > 0)
            {
                Points[guard.Id] = Points.GetOrDefault(guard.Id, 0) + Rules.TagPoints;
                EmitScored(guard.Team, guard.Id);
            }
            int old = TayaId;
            TayaId = runner.Id;
            TayaChanged?.Invoke(old, runner.Id);
            EmitRolesSwapped(runner.Id);
            BeginReset(sim);
        }

        protected override void NewRound(MatchSim sim)
        {
            base.NewRound(sim);
            SyncStamina(sim);
        }

        /// <summary>Every platform stands again, the mounds sink, every kid's safe time and survival clock start full.</summary>
        protected override void ResetGame(MatchSim sim)
        {
            WearLeft.Clear();
            for (int index = 0; index < Rules.LangitSpots.Count; index++)
                WearLeft[index] = Rules.LangitWearSeconds > 0.0 ? Rules.LangitWearSeconds : 1.0;  // (0: it never wears)
            foreach (int id in Mounds.Keys) MoundFell?.Invoke(id);
            Mounds.Clear();
            MoundLeft.Clear();
            MoundWait.Clear();
            SafeLeft.Clear();
            SurviveLeft.Clear();
            foreach (int id in sim.Players.Keys)
            {
                SafeLeft[id] = Rules.LangitSeconds;
                SurviveLeft[id] = Rules.SurviveSeconds;
            }
        }

        /// <summary>The taya starts in the middle of the street; everyone else on a ring round it, in the order of their ids.</summary>
        public override Vec2 Spot(MatchSim sim, PlayerState state)
        {
            if (state.Id == TayaId) return Vec2.Zero;
            var others = new List<int>();
            foreach (int id in sim.Players.Keys)
                if (id != TayaId) others.Add(id);
            int slot = others.IndexOf(state.Id);
            double angle = GMath.Tau * slot / GMath.Maxi(others.Count, 1) - GMath.Pi / 2.0;
            return sim.Layout.KeepInStreet(Vec2.FromAngle(angle) * Rules.RingRadius, state.Radius);
        }
    }
}
