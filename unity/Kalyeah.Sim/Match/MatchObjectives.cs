using System.Collections.Generic;
using System.Linq;

namespace Kalyeah.Sim
{
    /// <summary>Tagging, rescue, capture scoring, and between-point round reset rules (port of match_objectives.gd).</summary>
    public sealed class MatchObjectives
    {
        /// <summary>Off to the jail that holds the player's team (beside the enemy base under `RescueAtEnemyJail`).</summary>
        public void Tag(MatchSim sim, PlayerState state, int byId = -1)
        {
            int waiting = 0;
            foreach (var pair in sim.Players)
                if (pair.Value.Team == state.Team && pair.Value.Tagged) waiting += 1;
            state.Tagged = true;
            state.Hp = 0;
            state.TagTime = 0.0;
            sim.TagCounter += 1;
            state.TagOrder = sim.TagCounter;
            state.TaggedBy = byId;
            state.RevengeLeft = 0.0;
            state.ShieldLeft = 0.0;
            ClearActions(state);
            int jail = sim.JailSideFor(state.Team);
            state.Position = sim.Layout.TaggedPlace(jail, waiting);
            state.Facing = sim.Layout.TaggedFacing(jail);
            sim.EmitPlayerTagged(state.Id, byId);
            StartRevenge(sim, state.Id);
        }

        /// <summary>Revenge (docs/SPEC.md §5): everyone `taggerId` tagged walks free `RevengeReleaseSeconds` after `taggerId` is tagged.</summary>
        void StartRevenge(MatchSim sim, int taggerId)
        {
            if (sim.Rules.RevengeReleaseSeconds <= 0.0) return;
            foreach (var pair in sim.Players)
            {
                PlayerState waiting = pair.Value;
                if (waiting.Tagged && waiting.TaggedBy == taggerId && waiting.RevengeLeft <= 0.0)
                    waiting.RevengeLeft = sim.Rules.RevengeReleaseSeconds;
            }
        }

        /// <summary>Counts a pending revenge release down; at 0 the kid is back at its base behind a short shield.</summary>
        public void StepRevenge(MatchSim sim, PlayerState state, double dt)
        {
            if (state.RevengeLeft <= 0.0) return;
            state.RevengeLeft = GMath.Maxf(0.0, state.RevengeLeft - dt);
            if (state.RevengeLeft > MatchSim.Epsilon) return;
            Rescue(sim, state, MatchSim.ReleasedByRevenge);
            state.ShieldLeft = sim.Rules.RevengeShieldSeconds;
        }

        /// <summary>A tagged teammate waits on the sidewalk.</summary>
        public bool TeammateWaiting(MatchSim sim, int team)
        {
            foreach (var pair in sim.Players)
                if (pair.Value.Team == team && pair.Value.Tagged) return true;
            return false;
        }

        /// <summary>`state` stands in the jail where its team's tagged kids wait (the rescue area).</summary>
        public bool InRescueArea(MatchSim sim, PlayerState state) =>
            sim.Layout.RescueRect(sim.JailSideFor(state.Team)).Grow(state.Radius).HasPoint(state.Position);

        /// <summary>A stun-like crowd control (StatusEffects.HardCc) holds the kid.</summary>
        static bool HardStopped(PlayerState state)
        {
            foreach (StatusEffects.Type type in StatusEffects.HardCc)
                if (state.Effects.Has(type)) return true;
            return false;
        }

        /// <summary>The rescue progress a cancel keeps: the checkpoint once it was passed, else nothing.</summary>
        double KeptOnCancel(MatchSim sim, double elapsed)
        {
            double line = sim.Rules.RescueCheckpoint;
            return line > 0.0 && elapsed + 1e-6 >= line ? line : 0.0;
        }

        /// <summary>Under `RescueCancelsOnAction`: the rescuer moved off its spot, cast this tick, or is stunned.</summary>
        bool RescueBroken(MatchSim sim, PlayerState holder, int team)
        {
            if (!sim.Rules.SpecRules) return false;
            if (HardStopped(holder) || sim.CastThisTick(holder.Id)) return true;
            return holder.Position.DistanceTo(sim.RescueAnchor[team]) > sim.Rules.RescueStillTolerance;
        }

        /// <summary>
        /// Rescuing: one free teammate at a time stays in the rescue area for `rescue_hold_time` and frees the teammate tagged first.
        /// The area must be ready first (`rescue_cooldown`). With the spec rules (docs/SPEC.md §5) it works like a defuse: moving,
        /// casting or a stun cancels it, and a cancel keeps the progress back to the checkpoint once that was passed.
        /// </summary>
        public void CheckRescues(MatchSim sim, double dt)
        {
            for (int team = 0; team < 2; team++)
            {
                if (sim.RescueReadyLeft[team] > 0.0)
                {
                    sim.RescueReadyLeft[team] = GMath.Maxf(0.0, sim.RescueReadyLeft[team] - dt);
                    sim.RescueRescuer[team] = -1;
                    sim.RescueElapsed[team] = 0.0;
                    continue;
                }
                int holderId = sim.RescueRescuer[team];
                PlayerState holder = sim.Players.GetOrDefault(holderId);
                if (holder != null && (holder.Tagged || holder.Team != team || !InRescueArea(sim, holder))) holder = null;
                if (holder != null && RescueBroken(sim, holder, team)) holder = null;
                if (holder == null || !TeammateWaiting(sim, team))
                {
                    holder = null;
                    sim.RescueElapsed[team] = KeptOnCancel(sim, sim.RescueElapsed[team]);
                    if (!TeammateWaiting(sim, team)) sim.RescueElapsed[team] = 0.0;
                    foreach (var pair in sim.Players)
                    {
                        PlayerState candidate = pair.Value;
                        if (candidate.Id == holderId && sim.Rules.SpecRules) continue;
                        if (candidate.Team == team && !candidate.Tagged && InRescueArea(sim, candidate) && TeammateWaiting(sim, team)
                            && !(sim.Rules.SpecRules && (HardStopped(candidate) || sim.CastThisTick(candidate.Id))))
                        {
                            holder = candidate;
                            sim.RescueAnchor[team] = candidate.Position;
                            break;
                        }
                    }
                }
                sim.RescueRescuer[team] = holder != null ? holder.Id : -1;
                if (holder == null) continue;
                sim.RescueElapsed[team] += dt;
                if (sim.RescueElapsed[team] + 1e-6 < sim.Rules.RescueHoldTime) continue;
                sim.RescueElapsed[team] = 0.0;
                sim.RescueReadyLeft[team] = sim.Rules.RescueCooldown;
                foreach (PlayerState waiting in WaitingInTagOrder(sim, team))
                {
                    Rescue(sim, waiting, holder.Id);
                    if (!sim.Rules.RescueFreesAll) break;
                }
            }
        }

        /// <summary>The tagged players of `team`, the one tagged first at the front.</summary>
        List<PlayerState> WaitingInTagOrder(MatchSim sim, int team)
        {
            var found = new List<PlayerState>();
            foreach (var pair in sim.Players)
                if (pair.Value.Team == team && pair.Value.Tagged) found.Add(pair.Value);
            return found.OrderBy(s => s.TagOrder).ToList();
        }

        /// <summary>Back to the post at full HP.</summary>
        public void Rescue(MatchSim sim, PlayerState state, int byId)
        {
            state.Tagged = false;
            state.TagTime = 0.0;
            state.TagOrder = 0;
            state.TaggedBy = -1;
            state.RevengeLeft = 0.0;
            state.Hp = sim.Rules.PlayerMaxHp;
            state.Position = state.SpawnPosition;
            state.Facing = new Vec2(0.0, -sim.SideForTeam(state.Team));
            ClearActions(state);
            sim.RefillStamina(state);
            sim.EmitPlayerRescued(state.Id, byId);
        }

        /// <summary>Status effects, cast locks, weapon aiming and the walk to a move target end on a tag or rescue.</summary>
        public void ClearActions(PlayerState state)
        {
            state.CastLockLeft = 0.0;
            state.DashLeft = 0.0;
            state.Sprinting = false;
            state.Walking = false;
            state.Crouching = false;
            state.AirY = 0.0;
            state.VelY = 0.0;
            state.SlideLeft = 0.0;
            state.SlideDir = Vec2.Zero;
            state.RunTime = 0.0;
            state.JogTime = 0.0;
            state.BuffType = 0;
            state.BuffLeft = 0.0;
            state.BuffRegenCarry = 0.0;
            state.Effects.Clear();
            for (int i = 0; i < state.AimHold.Count; i++) state.AimHold[i] = -1.0;
            state.HasMoveTarget = false;
            state.StuckTime = 0.0;
        }

        /// <summary>How many free players of `team` stand in the enemy base (training dummies never count).</summary>
        public int CapturersInEnemyBase(MatchSim sim, int team, OrderedDict<int, DummyBrain> brains)
        {
            int enemySide = -sim.SideForTeam(team);
            int count = 0;
            foreach (PlayerState state in CaptureMembers(sim, team, brains))
                if (!state.Tagged && sim.Layout.InBase(enemySide, state.Position)
                    && !(sim.Rules.SpecRules && HardStopped(state))) count += 1;
            return count;
        }

        /// <summary>A free player of `team` (training dummies included) stands in its own base: under `CaptureContested` this pauses the enemy capture.</summary>
        public bool DefenderInOwnBase(MatchSim sim, int team)
        {
            int side = sim.SideForTeam(team);
            foreach (var pair in sim.Players)
                if (pair.Value.Team == team && !pair.Value.Tagged && sim.Layout.InBase(side, pair.Value.Position)) return true;
            return false;
        }

        /// <summary>Every player of `team` free and standing in the enemy base.</summary>
        public bool TeamInEnemyBase(MatchSim sim, int team, OrderedDict<int, DummyBrain> brains)
        {
            List<PlayerState> members = CaptureMembers(sim, team, brains);
            if (members.Count == 0) return false;
            int enemySide = -sim.SideForTeam(team);
            foreach (PlayerState state in members)
                if (state.Tagged || !sim.Layout.InBase(enemySide, state.Position)) return false;
            return true;
        }

        /// <summary>Every player of the other team waiting on the sidewalk (a team of training dummies never scores this way).</summary>
        public bool AllEnemiesTagged(MatchSim sim, int team, OrderedDict<int, DummyBrain> brains)
        {
            List<PlayerState> members = CaptureMembers(sim, 1 - team, brains);
            if (members.Count == 0 || CaptureMembers(sim, team, brains).Count == 0) return false;
            foreach (PlayerState state in members)
                if (!state.Tagged) return false;
            return true;
        }

        /// <summary>Team wipes score immediately; otherwise only one team's base countdown may run.</summary>
        public void CheckCaptures(MatchSim sim, OrderedDict<int, DummyBrain> brains, double dt)
        {
            bool wipedForZero = AllEnemiesTagged(sim, 0, brains);
            bool wipedForOne = AllEnemiesTagged(sim, 1, brains);
            if (wipedForZero != wipedForOne)
            {
                ScorePoint(sim, wipedForZero ? 0 : 1, MatchSim.CaptureKind.AllTagged);
                return;
            }
            if (wipedForZero && sim.Rules.SpecRules)
            {
                ReplayRound(sim);
                return;
            }
            int team = -1;
            MatchSim.CaptureKind kind = MatchSim.CaptureKind.None;
            int running = 0;
            int inBase = 0;
            for (int candidate = 0; candidate < 2; candidate++)
            {
                MatchSim.CaptureKind how = MatchSim.CaptureKind.None;
                int here = CapturersInEnemyBase(sim, candidate, brains);
                if (here > 0)
                {
                    how = MatchSim.CaptureKind.Base;
                    inBase = here;
                }
                if (how != MatchSim.CaptureKind.None)
                {
                    running += 1;
                    team = candidate;
                    kind = how;
                }
            }
            if (running != 1)
            {
                team = -1;
                kind = MatchSim.CaptureKind.None;
            }
            double drain = sim.Rules.CaptureDrainShare;
            if (team >= 0 && sim.Rules.SpecRules && DefenderInOwnBase(sim, 1 - team))
            {
                // a defender in its own base pauses the bar (shown, so bots and the HUD see the base is under attack): no progress, no drain
                if (team != sim.CaptureTeam || kind != sim.CurrentCaptureKind)
                {
                    sim.CaptureTeam = team;
                    sim.CurrentCaptureKind = kind;
                    sim.CaptureElapsed = 0.0;
                    sim.CaptureNeeded = sim.CurrentBaseCaptureTime();
                }
                sim.CaptureRate = 0.0;
                return;
            }
            if (team < 0 && drain > 0.0 && sim.CaptureTeam >= 0)
            {
                // nobody capturing: the bar keeps its progress and empties at `drain` of the fill speed
                sim.CaptureRate = -drain;
                sim.CaptureElapsed = GMath.Maxf(0.0, sim.CaptureElapsed - dt * drain);
                if (sim.CaptureElapsed <= 0.0) ClearCapture(sim);
                return;
            }
            if (team != sim.CaptureTeam || kind != sim.CurrentCaptureKind)
            {
                sim.CaptureTeam = team;
                sim.CurrentCaptureKind = kind;
                sim.CaptureElapsed = 0.0;
            }
            sim.CaptureRate = 1.0;
            if (team < 0) return;
            if (kind == MatchSim.CaptureKind.Base)
            {
                sim.CaptureNeeded = sim.CurrentBaseCaptureTime();
                sim.CaptureRate = 1.0 + (inBase - 1) * sim.Rules.CaptureAllyBonus;
            }
            sim.CaptureElapsed += dt * sim.CaptureRate;
            if (sim.CaptureElapsed >= sim.CaptureNeeded) ScorePoint(sim, team, kind);
        }

        public double CaptureTimeLeft(MatchSim sim)
        {
            if (sim.CaptureTeam < 0) return 0.0;
            // paused or draining: report the time one capturer would still need
            double rate = sim.CaptureRate > 0.0 ? sim.CaptureRate : 1.0;
            return GMath.Maxf(0.0, sim.CaptureNeeded - sim.CaptureElapsed) / GMath.Maxf(rate, 0.001);
        }

        /// <summary>Both teams won the round in the same tick: nobody scores and the round starts over (docs/SPEC.md §5).</summary>
        void ReplayRound(MatchSim sim)
        {
            ClearCapture(sim);
            foreach (var pair in sim.Players) sim.EndMove(pair.Value);
            sim.Phase = MatchSim.MatchPhase.PointFreeze;
            sim.FreezeLeft = sim.Rules.PointFreezeTime;
        }

        public void EndFreeze(MatchSim sim)
        {
            ResetRound(sim);
            sim.Phase = MatchSim.MatchPhase.Playing;
            sim.EmitPointReset();
        }

        /// <summary>The players a capture needs: everyone on `team` except training dummies.</summary>
        List<PlayerState> CaptureMembers(MatchSim sim, int team, OrderedDict<int, DummyBrain> brains)
        {
            var result = new List<PlayerState>();
            foreach (var pair in sim.Players)
                if (pair.Value.Team == team && !brains.ContainsKey(pair.Key)) result.Add(pair.Value);
            return result;
        }

        /// <summary>Awards `team` a point (`how` is a CaptureKind) and starts the freeze, or ends the match.</summary>
        public void ScorePoint(MatchSim sim, int team, MatchSim.CaptureKind how)
        {
            MatchScore.Result result = sim.Score.AwardPoint(team);
            sim.PointLog.Add(new Vec2I(team, (int)how));
            ClearCapture(sim);
            foreach (var pair in sim.Players) sim.EndMove(pair.Value);
            sim.EmitPointScored(team, (int)how);
            if (result == MatchScore.Result.Match)
            {
                sim.Phase = MatchSim.MatchPhase.MatchOver;
                sim.EmitMatchWon(team);
                return;
            }
            sim.Phase = MatchSim.MatchPhase.PointFreeze;
            sim.FreezeLeft = sim.Rules.PointFreezeTime;
        }

        void ClearCapture(MatchSim sim)
        {
            sim.CaptureTeam = -1;
            sim.CurrentCaptureKind = MatchSim.CaptureKind.None;
            sim.CaptureElapsed = 0.0;
        }

        /// <summary>Everyone back to their post at full HP, cooldowns and effects cleared; weapon objects removed.</summary>
        public void ResetRound(MatchSim sim)
        {
            sim.Weapons.Reset();
            sim.ResetPowerups();
            ClearCapture(sim);
            sim.RoundTime = 0.0;
            sim.CaptureNeeded = sim.Rules.CaptureSoloTime;
            sim.RescueRescuer = new[] { -1, -1 };
            sim.RescueElapsed = new[] { 0.0, 0.0 };
            double ready = sim.Rules.SpecRules ? 0.0 : sim.Rules.RescueCooldown;
            sim.RescueReadyLeft = new[] { ready, ready };
            sim.BarrierLeft = sim.Rules.BarrierSeconds;
            foreach (var pair in sim.Players)
            {
                PlayerState state = pair.Value;
                bool wasTagged = state.Tagged;
                state.TaggedBy = -1;
                state.RevengeLeft = 0.0;
                state.ShieldLeft = 0.0;
                ClearActions(state);
                state.PrimaryCd = 0.0;
                state.DashCd = 0.0;
                state.JumpCd = 0.0;
                state.SlideCd = 0.0;
                state.Position = state.SpawnPosition;
                state.Facing = new Vec2(0.0, -sim.SideForTeam(state.Team));
                state.Hp = sim.Rules.PlayerMaxHp;
                state.Tagged = false;
                state.TagTime = 0.0;
                state.TagOrder = 0;
                sim.RefillStamina(state);
                if (sim.Rules.ElixirResetOnRound && sim.PaysElixir(state)) state.Elixir = sim.Rules.ElixirStart;
                sim.FinishReloads(state);
                if (wasTagged) sim.EmitPlayerRescued(pair.Key, MatchSim.ReleasedAlone);
            }
        }
    }
}
