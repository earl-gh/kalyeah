using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// Patintero (port of scripts/sim/patintero.gd; docs/GDD.md "Patintero"): two horizontal lines across the street are the edges of the
    /// court, with one vertical line down the middle. Three guards keep the lines, one each (two take a horizontal, one the vertical): running
    /// keeps a guard on its line, a dive is the one thing that takes it off it. The runners run free over the whole court.
    /// - A runner has to cross the vertical line once before the far end counts.
    /// - A guard that moves into the tag circle of a runner tags it: the runner stumbles, then waits on the left or the right sidewalk (at
    ///   random) until its team earns a point.
    /// - ONE free runner past the last guard and back to the base earns the runners' team a point and resets the round (runners stay runners).
    /// - The guards earn a point only when all the runners are tagged, and the round resets with the roles switched.
    /// - A round in which no runner passes the first guard in `StallSeconds` is a point for the guards and the roles switch.
    /// Points belong to the team, not to its role. Runners and guards can Dive (a burst, then lying and getting up; a guard runs back to its
    /// line), with a cooldown that starts when the dive is over.
    /// </summary>
    public sealed class Patintero : StreetMode
    {
        /// <summary>(id): `id` got past the last guard and now has to come back to the base.</summary>
        public event Action<int> RunnerCrossed;
        /// <summary>(id, way, time, recover): `id` dived along `way`; the burst takes `time` s, then it lies and gets up for `recover` s.</summary>
        public event Action<int, Vec2, double, double> Dived;
        /// <summary>(id, side): a tagged runner reached the sidewalk on `side` (-1 left, 1 right).</summary>
        public event Action<int, int> JailedRunner;

        /// <summary>A guard's line: across the street (it moves along X) or down the middle (it moves along Z).</summary>
        public enum Axis { Horizontal = 0, Vertical = 1 }
        /// <summary>The parts of a dive (GDScript's `DivePhase`; renamed because the table of them is `DivePhase`).</summary>
        public enum DiveStage { None = 0, Burst = 1, Recover = 2, Return = 3 }

        /// <summary>The stick is locked this long (s) on every tick of a dive, so it never steers it.</summary>
        public const double DiveLock = 0.1;
        /// <summary>Tagged runners on one sidewalk stand this far apart (m).</summary>
        public const double WaitSpacing = 6.0;

        /// <summary>The runners who got past the last guard on this run (they score when they are back at the base).</summary>
        public OrderedDict<int, bool> Crossed = new OrderedDict<int, bool>();
        /// <summary>The runners that have crossed the vertical line on this run, and the side (-1, 1) of it each was last on.</summary>
        public OrderedDict<int, bool> MiddleCrossed = new OrderedDict<int, bool>();
        public OrderedDict<int, int> MiddleSide = new OrderedDict<int, int>();
        /// <summary>Guard id to its line's axis, and the line's fixed coordinate (z of a horizontal line, x of the vertical one).</summary>
        public OrderedDict<int, Axis> GuardAxis = new OrderedDict<int, Axis>();
        public OrderedDict<int, double> GuardLine = new OrderedDict<int, double>();
        /// <summary>id to the part of its dive (absent: not in a dive), the seconds left of the burst and the get-up, the way, the cooldown.</summary>
        public OrderedDict<int, DiveStage> DivePhase = new OrderedDict<int, DiveStage>();
        public OrderedDict<int, double> DiveLeft = new OrderedDict<int, double>();
        public OrderedDict<int, double> RecoverLeft = new OrderedDict<int, double>();
        public OrderedDict<int, Vec2> DiveWay = new OrderedDict<int, Vec2>();
        public OrderedDict<int, double> DiveCool = new OrderedDict<int, double>();
        /// <summary>id to seconds left of the stumble of a runner just tagged; id to the side (-1 or 1) of the sidewalk it waits on.</summary>
        public OrderedDict<int, double> Caught = new OrderedDict<int, double>();
        public OrderedDict<int, int> Jailed = new OrderedDict<int, int>();
        /// <summary>Seconds left for a runner to pass the first guard this round, and whether one has.</summary>
        public double StallLeft;
        public bool FirstGuardPassed;

        readonly OrderedDict<int, bool> _diveDown = new OrderedDict<int, bool>();
        bool _swapAfterReset;

        public Patintero(StreetModeRules rules) : base(rules)
        {
            StallLeft = rules.StallSeconds;
        }

        protected override void AddSyncedFields(List<SyncedField> fields)
        {
            base.AddSyncedFields(fields);
            fields.Add(Table("crossed", Crossed));
            fields.Add(Table("middle_crossed", MiddleCrossed));
            fields.Add(Table("middle_side", MiddleSide));
            fields.Add(Table("guard_axis", GuardAxis));
            fields.Add(Table("guard_line", GuardLine));
            fields.Add(Table("dive_phase", DivePhase));
            fields.Add(Table("dive_left", DiveLeft));
            fields.Add(Table("recover_left", RecoverLeft));
            fields.Add(Table("dive_way", DiveWay));
            fields.Add(Table("dive_cool", DiveCool));
            fields.Add(Table("caught", Caught));
            fields.Add(Table("jailed", Jailed));
            fields.Add(Value("stall_left", () => StallLeft, v => StallLeft = v));
            fields.Add(Value("first_guard_passed", () => FirstGuardPassed, v => FirstGuardPassed = v));
        }

        // ---- the court -----------------------------------------------------------------------------

        /// <summary>The z of horizontal line `index` (0: the one nearest the base, 1: the far one).</summary>
        public double HorizontalZ(int index) => index == 0 ? Rules.LineSpan : -Rules.LineSpan;

        /// <summary>The line the `slot`-th guard keeps: two horizontals first, the vertical for the third (the coordinate as a float).</summary>
        public (Axis axis, double line) LineFor(int slot)
        {
            if (slot <= 1) return (Axis.Horizontal, (float)HorizontalZ(slot));
            return (Axis.Vertical, 0.0);
        }

        public bool IsJailed(int id) => Jailed.ContainsKey(id);

        /// <summary>A tagged runner: stumbling, or waiting on a sidewalk.</summary>
        public bool IsTaggedOut(int id) => Jailed.ContainsKey(id) || Caught.ContainsKey(id);

        public override bool IsTaggable(PlayerState state) => base.IsTaggable(state) && !IsTaggedOut(state.Id);

        public override double CatchSeconds() => Rules.CatchSeconds;

        /// <summary>How many runners are tagged (stumbling or on a sidewalk).</summary>
        public int RunnersOut() => Jailed.Count + Caught.Count;

        public int RunnerCount(MatchSim sim)
        {
            int count = 0;
            foreach (PlayerState state in sim.Players.Values)
                if (IsAttacker(state)) count += 1;
            return count;
        }

        public override Vec2 MovePoint(PlayerState state, Vec2 point)
        {
            if (GuardLine.ContainsKey(state.Id) && !IsAttacker(state)) return OnLine(state.Id, point);
            return point;
        }

        public override void Constrain(MatchSim sim, PlayerState state)
        {
            if (IsResetting()) return;
            if (Jailed.ContainsKey(state.Id))
                KeepOnSidewalk(sim, state);
            else if (GuardLine.ContainsKey(state.Id) && !IsAttacker(state) && !IsBusy(state.Id))
                state.Position = OnLine(state.Id, state.Position);  // running keeps it on its line; a dive and the way back do not
        }

        /// <summary>`point` held to the line of guard `id`.</summary>
        Vec2 OnLine(int id, Vec2 point)
        {
            if (GuardAxis[id] == Axis.Horizontal) return new Vec2(point.x, GuardLine[id]);
            return new Vec2(GuardLine[id], GMath.Clampf(point.y, -Rules.LineSpan, Rules.LineSpan));
        }

        /// <summary>The x range (m, away from the middle) of the sidewalk a kid of `radius` can stand on (as a float vector).</summary>
        public Vec2 SidewalkRange(MatchSim sim, double radius)
        {
            double edge = sim.Layout.LaneWidth / 2.0;
            return new Vec2(edge + radius, edge + sim.Layout.BoundaryThickness + Rules.SidewalkWidth - radius);
        }

        /// <summary>A tagged runner is held on its sidewalk, within the length of the court.</summary>
        void KeepOnSidewalk(MatchSim sim, PlayerState state)
        {
            int side = Jailed[state.Id];
            Vec2 room = SidewalkRange(sim, state.Radius);
            state.Position.x = side * GMath.Clampf(side * state.Position.x, room.x, room.y);
            state.Position.y = GMath.Clampf(state.Position.y, -Rules.LineSpan, Rules.LineSpan);
        }

        // ---- diving --------------------------------------------------------------------------------

        /// <summary>True when `state` could dive now: free, not in a dive, and its cooldown is over.</summary>
        public bool CanDive(PlayerState state) =>
            !state.Tagged && !IsResetting() && !IsTaggedOut(state.Id) && !IsBusy(state.Id) && DiveCool.GetOrDefault(state.Id, 0.0) <= 0.0;

        /// <summary>How ready the dive of `id` is, from 0 (in a dive, or just over) to 1 (ready).</summary>
        public double DiveReadyShare(int id)
        {
            if (IsBusy(id)) return 0.0;
            return 1.0 - GMath.Clampf(DiveCool.GetOrDefault(id, 0.0) / GMath.Maxf(Rules.DiveCooldown, 0.001), 0.0, 1.0);
        }

        /// <summary>In the burst of a dive.</summary>
        public bool IsDiving(int id) => DivePhase.GetOrDefault(id, DiveStage.None) == DiveStage.Burst;

        /// <summary>In any part of a dive: the burst, lying and getting up, or (a guard only) running back to its line.</summary>
        public bool IsBusy(int id) => DivePhase.ContainsKey(id);

        /// <summary>A guard running back to the nearest point of its line.</summary>
        public bool IsReturning(int id) => DivePhase.GetOrDefault(id, DiveStage.None) == DiveStage.Return;

        /// <summary>The way `state` dives with the stick at `stick` (the way it faces without one).</summary>
        public Vec2 DiveWayFor(PlayerState state, Vec2 stick)
        {
            Vec2 way = stick.Length() > 0.1 ? stick : state.Facing;
            return way.Length() > 0.0 ? way.Normalized() : new Vec2(0.0, -1.0);
        }

        /// <summary>The nearest point of guard `id`'s line to `point`.</summary>
        public Vec2 NearestOnLine(int id, Vec2 point) => OnLine(id, point);

        public override void HandleInput(MatchSim sim, PlayerState state, PlayerInput input, double dt)
        {
            int id = state.Id;
            if (Jailed.ContainsKey(id))
            {
                _diveDown[id] = false;  // a tagged runner never dives: it only walks on its sidewalk, by the stick
                state.CastLockLeft = DiveLock;
                if (input != null && input.Move != Vec2.Zero)
                {
                    Vec2 stick = input.Move.LimitLength(1.0);
                    state.Facing = stick.Normalized();
                    state.Position += stick * sim.Rules.MoveSpeed * dt;
                }
                return;
            }
            if (Caught.ContainsKey(id))
            {
                state.CastLockLeft = DiveLock;  // it stumbles where it was caught
                Caught[id] -= dt;
                if (Caught[id] <= 0.0) SendToSidewalk(sim, state);
                return;
            }
            bool pressed = input != null && input.IsPressed(PlayerInput.BtnCard2);
            if (pressed && !_diveDown.GetOrDefault(id, false) && CanDive(state)) Dive(sim, state, DiveWayFor(state, input.Move));
            _diveDown[id] = pressed;
            if (!IsBusy(id)) return;
            state.CastLockLeft = DiveLock;  // the stick does not steer any part of a dive
            switch (DivePhase[id])
            {
                case DiveStage.Burst:
                    sim.MoveBy(state, DiveWay[id] * Rules.DiveSpeedShare * sim.Rules.MoveSpeed * dt);
                    DiveLeft[id] -= dt;
                    if (DiveLeft[id] <= 0.0)
                    {
                        DiveLeft.Remove(id);
                        DivePhase[id] = DiveStage.Recover;
                        RecoverLeft[id] = Rules.DiveRecover;
                    }
                    break;
                case DiveStage.Recover:
                    RecoverLeft[id] -= dt;
                    if (RecoverLeft[id] <= 0.0)
                    {
                        RecoverLeft.Remove(id);
                        if (GuardLine.ContainsKey(id) && !IsAttacker(state)) DivePhase[id] = DiveStage.Return;
                        else FinishDive(state);
                    }
                    break;
                case DiveStage.Return:
                    RunBack(sim, state, dt);
                    break;
            }
        }

        /// <summary>A guard runs back to the nearest point of its line, and the dive is over when it is there.</summary>
        void RunBack(MatchSim sim, PlayerState state, double dt)
        {
            Vec2 goal = OnLine(state.Id, state.Position);
            double reach = Rules.DiveReturnSpeedShare * sim.Rules.MoveSpeed * dt;
            double gap = state.Position.DistanceTo(goal);
            if (gap <= reach)
            {
                state.Position = goal;
                FinishDive(state);
                return;
            }
            Vec2 way = (goal - state.Position) / gap;
            state.Facing = way;
            sim.MoveBy(state, way * reach);
        }

        /// <summary>The dive is over (a runner stands, a guard is back on its line): it can steer again and the cooldown starts now.</summary>
        void FinishDive(PlayerState state)
        {
            ClearDive(state.Id);
            DiveCool[state.Id] = Rules.DiveCooldown;
            state.CastLockLeft = 0.0;
        }

        void ClearDive(int id)
        {
            DivePhase.Remove(id);
            DiveLeft.Remove(id);
            RecoverLeft.Remove(id);
            DiveWay.Remove(id);
        }

        void Dive(MatchSim sim, PlayerState state, Vec2 way)
        {
            DivePhase[state.Id] = DiveStage.Burst;
            DiveLeft[state.Id] = Rules.DiveTime;
            DiveWay[state.Id] = way;
            state.Facing = way;
            state.CastLockLeft = DiveLock;
            sim.EndMove(state);
            Dived?.Invoke(state.Id, way, Rules.DiveTime, Rules.DiveRecover);
        }

        // ---- one tick ------------------------------------------------------------------------------

        protected override void StepGame(MatchSim sim, double dt)
        {
            foreach (int id in DiveCool.KeyList()) DiveCool[id] = GMath.Maxf(0.0, DiveCool[id] - dt);
            foreach (PlayerState state in sim.Players.Values)
            {
                if (!IsAttacker(state) || IsTaggedOut(state.Id)) continue;
                if (state.Position.y < Rules.LineSpan) FirstGuardPassed = true;  // a runner is past the first guard: no stall this round
                WatchMiddle(state);
                if (!Crossed.ContainsKey(state.Id) && AtGoal(state.Position) && MiddleCrossed.ContainsKey(state.Id))
                {
                    Crossed[state.Id] = true;
                    RunnerCrossed?.Invoke(state.Id);
                }
                else if (Crossed.ContainsKey(state.Id) && AtHome(state.Position))
                {
                    // ONE free runner past the last guard and back at the base is the runners' point; the round resets, everyone is free
                    Crossed.Remove(state.Id);
                    MiddleCrossed.Remove(state.Id);
                    AddTeamPoint(sim, state.Team, state.Id);
                    if (sim.Phase == MatchSim.MatchPhase.Playing) BeginReset(sim);
                    return;
                }
            }
            CheckTags(sim);
        }

        /// <summary>Between the two horizontal lines a runner that changes sides of the vertical line has crossed it.</summary>
        void WatchMiddle(PlayerState state)
        {
            if (Math.Abs(state.Position.y) >= Rules.LineSpan || GMath.IsZeroApprox(state.Position.x)) return;
            int side = state.Position.x > 0.0 ? 1 : -1;
            if (MiddleSide.TryGetValue(state.Id, out int was) && was != side) MiddleCrossed[state.Id] = true;
            MiddleSide[state.Id] = side;
        }

        /// <summary>A guard moved into the tag circle of a runner: the runner stumbles, then goes to a sidewalk. When that was the last
        /// runner, the guards' team earns a point and the round resets with the roles switched.</summary>
        protected override void Tag(MatchSim sim, PlayerState guard, PlayerState runner)
        {
            sim.EmitWeaponCast(guard.Id, TagCast);
            EmitTagged(guard.Id, runner.Id);
            Caught[runner.Id] = Rules.CatchSeconds;
            Crossed.Remove(runner.Id);
            MiddleCrossed.Remove(runner.Id);
            MiddleSide.Remove(runner.Id);
            ClearDive(runner.Id);
            runner.CastLockLeft = DiveLock;
            sim.EndMove(runner);
            if (RunnersOut() >= RunnerCount(sim)) GuardsTakeTheRound(sim, guard.Team, guard.Id, MatchSim.CaptureKind.Touched);
        }

        /// <summary>The guards' team earns a point (every runner tagged, or the runners too slow), the round resets and the roles switch.</summary>
        void GuardsTakeTheRound(MatchSim sim, int team, int id, MatchSim.CaptureKind how)
        {
            AddTeamPoint(sim, team, id, how);
            if (sim.Phase == MatchSim.MatchPhase.Playing)
            {
                _swapAfterReset = true;
                BeginReset(sim);
            }
        }

        /// <summary>The stall rule: no runner past the first guard `StallSeconds` into the round is a point for the guards.</summary>
        protected override void StepTurn(MatchSim sim, double dt)
        {
            if (FirstGuardPassed) return;
            StallLeft -= dt;
            if (StallLeft <= 0.0)
            {
                int guard = -1;
                foreach (PlayerState state in sim.Players.Values)
                {
                    if (!IsAttacker(state))
                    {
                        guard = state.Id;
                        break;
                    }
                }
                GuardsTakeTheRound(sim, 1 - AttackTeam, guard, MatchSim.CaptureKind.TooSlow);
            }
        }

        /// <summary>The tagged runner's stumble is over: it goes to the middle of the left or the right sidewalk (at random) and waits.</summary>
        void SendToSidewalk(MatchSim sim, PlayerState state)
        {
            int id = state.Id;
            Caught.Remove(id);
            int side = sim.Rng.Randf() < 0.5 ? 1 : -1;
            int waiting = 0;
            foreach (var pair in Jailed)
                if (pair.Value == side) waiting += 1;
            Vec2 room = SidewalkRange(sim, state.Radius);
            // the first stands at the middle of the sidewalk, the next ones a little to either side of it
            int step = (waiting + 1) / 2;
            double along = step * WaitSpacing * (waiting % 2 == 1 ? 1.0 : -1.0);
            state.Position = new Vec2(side * (room.x + room.y) / 2.0, GMath.Clampf(along, -Rules.LineSpan, Rules.LineSpan));
            state.Facing = new Vec2(-(double)side, 0.0);
            state.CastLockLeft = 0.0;
            Jailed[id] = side;
            sim.EndMove(state);
            JailedRunner?.Invoke(id, side);
        }

        /// <summary>After a reset the roles may switch; everyone goes back to their places and the sidewalks are empty.</summary>
        protected override void NewRound(MatchSim sim)
        {
            bool switched = _swapAfterReset;
            if (switched)
            {
                _swapAfterReset = false;
                AttackTeam = 1 - AttackTeam;
            }
            base.NewRound(sim);
            if (switched) EmitRolesSwapped(AttackTeam);
        }

        protected override void ResetGame(MatchSim sim)
        {
            Crossed.Clear();
            MiddleCrossed.Clear();
            MiddleSide.Clear();
            GuardAxis.Clear();
            GuardLine.Clear();
            DivePhase.Clear();
            DiveLeft.Clear();
            RecoverLeft.Clear();
            DiveWay.Clear();
            DiveCool.Clear();
            Caught.Clear();
            Jailed.Clear();
            FirstGuardPassed = false;
            StallLeft = Rules.StallSeconds;
        }

        /// <summary>Each guard stands in the middle of its own line (where the vertical line crosses a horizontal one, at x = 0).</summary>
        public override Vec2 SpotFor(PlayerState state, bool attacker, int slot, int count)
        {
            if (attacker) return base.SpotFor(state, attacker, slot, count);
            (Axis axis, double line) = LineFor(slot);
            GuardAxis[state.Id] = axis;
            GuardLine[state.Id] = axis == Axis.Horizontal ? line : 0.0;
            return axis == Axis.Horizontal ? new Vec2(0.0, line) : new Vec2(0.0, 0.0);
        }
    }
}
