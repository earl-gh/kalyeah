namespace Kalyeah.Sim
{
    /// <summary>
    /// Authoritative card-button arbitration and cast-on-release state machine (port of weapon_casting.gd). One slot held at a time;
    /// simultaneous presses prefer the lowest slot. The release casts.
    /// </summary>
    public sealed class WeaponCasting
    {
        /// <summary>Hand slot to its PlayerInput bit.</summary>
        public static readonly int[] CardBits = PlayerInput.CardBits;
        public static readonly int[] WeaponBits = PlayerInput.CardBits;

        /// <summary>Advances one player's weapon input. Releasing an active aim consumes the tick.</summary>
        public void Handle(MatchSim sim, PlayerState state, PlayerInput input, double dt)
        {
            int activeSlot = ActiveSlot(state);
            if (activeSlot >= 0) StepActive(sim, state, input, dt, activeSlot);
            else StartFreshAim(sim, state, input);
            state.PreviousButtons = input.Buttons;
        }

        /// <summary>A weapon slot can start aiming: equipped, off cooldown, and casting allowed.</summary>
        public bool WeaponReady(PlayerState state, int slot) =>
            !state.Tagged && state.Effects.CanCast()
            && slot >= 0 && slot < state.Weapons.Count && state.Weapons[slot] != ""
            && slot < state.WeaponCooldowns.Count
            && state.WeaponCooldowns[slot] <= 0.0;

        /// <summary>The aim a release would use: the stick past the deadzone, else the auto-aim locked at the press (or a live one).</summary>
        public Vec2 ResolvedAim(MatchSim sim, PlayerState state, int slot, Vec2 stick)
        {
            if (stick.Length() >= sim.Rules.AimDeadzone) return stick.LimitLength(1.0);
            if (state.AimHold[slot] >= 0.0) return state.AimLock[slot];
            return sim.Weapons.AutoAim(sim, state, sim.WeaponDefs[state.Weapons[slot]]);
        }

        /// <summary>The target a TARGETED weapon would throw at (locked at the press while held).</summary>
        public int ResolvedTarget(MatchSim sim, PlayerState state, int slot)
        {
            if (state.AimHold[slot] >= 0.0) return state.AimTarget[slot];
            PlayerState target = sim.NearestEnemy(state, sim.WeaponDefs[state.Weapons[slot]].MaxRange);
            return target != null ? target.Id : -1;
        }

        /// <summary>The sole active aim; a malformed state with several keeps the lowest slot and cancels the others.</summary>
        int ActiveSlot(PlayerState state)
        {
            int active = -1;
            for (int slot = 0; slot < state.AimHold.Count; slot++)
            {
                if (state.AimHold[slot] < 0.0) continue;
                if (active < 0) active = slot;
                else state.AimHold[slot] = -1.0;
            }
            return active;
        }

        void StepActive(MatchSim sim, PlayerState state, PlayerInput input, double dt, int slot)
        {
            int bit = CardBits[slot];
            if (input.IsPressed(bit))
            {
                if (state.Tagged || !state.Effects.CanCast() || sim.CastingBlocked(state)) state.AimHold[slot] = -1.0;
                else state.AimHold[slot] += dt;
                return;
            }
            if ((state.PreviousButtons & bit) != 0)
            {
                if (!input.IsPressed(PlayerInput.BtnAimCancel)) TryFire(sim, state, slot, input, state.AimHold[slot]);
            }
            state.AimHold[slot] = -1.0;
        }

        void StartFreshAim(MatchSim sim, PlayerState state, PlayerInput input)
        {
            int count = GMath.Mini(CardBits.Length, state.AimHold.Count);
            for (int slot = 0; slot < count; slot++)
            {
                int bit = CardBits[slot];
                bool freshPress = input.IsPressed(bit) && (state.PreviousButtons & bit) == 0;
                if (!freshPress) continue;
                // an unaffordable card cannot be picked up
                if (sim.WeaponReady(state, slot))
                {
                    state.AimHold[slot] = 0.0;
                    LockAim(sim, state, slot);
                }
                // The lowest freshly pressed slot owns this input even when it was not ready.
                break;
            }
        }

        void LockAim(MatchSim sim, PlayerState state, int slot)
        {
            WeaponDef def = sim.WeaponDefs[state.Weapons[slot]];
            state.AimLock[slot] = sim.Weapons.AutoAim(sim, state, def);
            PlayerState target = sim.NearestEnemy(state, def.MaxRange);
            state.AimTarget[slot] = target != null ? target.Id : -1;
        }

        void TryFire(MatchSim sim, PlayerState state, int slot, PlayerInput input, double holdSeconds)
        {
            if (!state.CanAct() || !sim.WeaponReady(state, slot)) return;
            WeaponDef def = sim.WeaponDefs[state.Weapons[slot]];
            Vec2 aim;
            int targetId = state.AimTarget[slot];
            if (input.IsPressed(PlayerInput.BtnAimPoint) && input.AimPoint.IsFinite())
            {
                aim = sim.AimFromPoint(state, def, input.AimPoint);
                if (def.Shape == WeaponDef.WeaponShape.Targeted) targetId = sim.TargetNearPoint(state, def, input.AimPoint);
            }
            else
            {
                aim = ResolvedAim(sim, state, slot, input.Aim.IsFinite() ? input.Aim : Vec2.Zero);
            }
            if (!sim.Weapons.Fire(sim, state, def, aim, holdSeconds, targetId)) return;
            if (sim.PaysElixir(state)) state.Elixir = GMath.Maxf(0.0, state.Elixir - def.ElixirCost);
            else state.WeaponCooldowns[slot] = def.Cooldown;
            sim.CycleUsed(state, slot);
            state.OutOfCombatTime = 0.0;
            state.Sprinting = false;
            state.CastLockLeft = def.CastLock;
            if (def.CastLock > 0.0) sim.EndMove(state);
            sim.EmitWeaponCast(state.Id, def.Id);
        }
    }
}
