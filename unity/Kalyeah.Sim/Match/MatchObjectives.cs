using System.Collections.Generic;
using System.Linq;

namespace Kalyeah.Sim
{
    /// <summary>Tagging, rescue, capture scoring, and between-point round reset rules (port of match_objectives.gd).</summary>
    public sealed class MatchObjectives
    {
        /// <summary>Off to the sidewalk beside the player's team's rescue area.</summary>
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
            ClearActions(state);
            state.Position = sim.Layout.TaggedPlace(sim.SideForTeam(state.Team), waiting);
            state.Facing = sim.Layout.TaggedFacing(sim.SideForTeam(state.Team));
            sim.EmitPlayerTagged(state.Id, byId);
        }

        /// <summary>A tagged teammate waits on the sidewalk.</summary>
        public bool TeammateWaiting(MatchSim sim, int team)
        {
            foreach (var pair in sim.Players)
                if (pair.Value.Team == team && pair.Value.Tagged) return true;
            return false;
        }

        /// <summary>`state` stands in its own team's rescue area.</summary>
        public bool InRescueArea(MatchSim sim, PlayerState state) =>
            sim.Layout.RescueRect(sim.SideForTeam(state.Team)).Grow(state.Radius).HasPoint(state.Position);

        /// <summary>
        /// Rescuing: one free teammate at a time stays in the rescue area for `rescue_hold_time` and frees the teammate tagged first.
        /// The area must be ready first (`rescue_cooldown`).
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
                if (holder == null || !TeammateWaiting(sim, team))
                {
                    holder = null;
                    sim.RescueElapsed[team] = 0.0;
                    foreach (var pair in sim.Players)
                    {
                        PlayerState candidate = pair.Value;
                        if (candidate.Team == team && !candidate.Tagged && InRescueArea(sim, candidate) && TeammateWaiting(sim, team))
                        {
                            holder = candidate;
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
                if (!state.Tagged && sim.Layout.InBase(enemySide, state.Position)) count += 1;
            return count;
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
            return GMath.Maxf(0.0, sim.CaptureNeeded - sim.CaptureElapsed) / GMath.Maxf(sim.CaptureRate, 0.001);
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
        void ResetRound(MatchSim sim)
        {
            sim.Weapons.Reset();
            sim.ResetPowerups();
            ClearCapture(sim);
            sim.RoundTime = 0.0;
            sim.CaptureNeeded = sim.Rules.CaptureSoloTime;
            sim.RescueRescuer = new[] { -1, -1 };
            sim.RescueElapsed = new[] { 0.0, 0.0 };
            sim.RescueReadyLeft = new[] { sim.Rules.RescueCooldown, sim.Rules.RescueCooldown };
            foreach (var pair in sim.Players)
            {
                PlayerState state = pair.Value;
                bool wasTagged = state.Tagged;
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
