namespace Kalyeah.Sim
{
    /// <summary>
    /// Authoritative card/skill-button arbitration and cast-on-release state machine (port of weapon_casting.gd).
    /// Supports both legacy weapon slots and docs/KITS.md 3-skill spec kits ([S1, S2, ULT], where BtnUltimate also drives slot 2).
    /// One slot held at a time; simultaneous presses prefer the lowest slot. The release casts.
    /// </summary>
    public sealed class WeaponCasting
    {
        /// <summary>Hand slot to its PlayerInput bit.</summary>
        public static readonly int[] CardBits = PlayerInput.CardBits;
        public static readonly int[] WeaponBits = PlayerInput.CardBits;

        static bool SlotPressed(PlayerState state, int buttons, int slot)
        {
            if (slot < 0 || slot >= CardBits.Length) return false;
            if ((buttons & CardBits[slot]) != 0) return true;
            return state.UsingSpecKit && slot == 2 && (buttons & PlayerInput.BtnUltimate) != 0;
        }

        /// <summary>Advances one player's weapon/skill input. Releasing an active aim consumes the tick.</summary>
        public void Handle(MatchSim sim, PlayerState state, PlayerInput input, double dt)
        {
            int activeSlot = ActiveSlot(state);
            if (activeSlot >= 0) StepActive(sim, state, input, dt, activeSlot);
            else StartFreshAim(sim, state, input);
            state.PreviousButtons = input.Buttons;
        }

        /// <summary>A weapon or skill slot can start aiming: equipped, off cooldown (or has charges/recast), and casting allowed.</summary>
        public bool WeaponReady(PlayerState state, int slot)
        {
            if (state.Tagged || !state.Effects.CanCast()) return false;
            if (slot < 0 || slot >= state.Weapons.Count || state.Weapons[slot] == "") return false;
            if (slot >= state.WeaponCooldowns.Count) return false;
            if (state.UsingSpecKit && slot == 0 && state.SyatoRecastLeft > 0.0 && state.SyatoTargetId >= 0)
                return true;
            return state.WeaponCooldowns[slot] <= 0.0;
        }

        /// <summary>The aim a release would use: the stick past the deadzone, else the auto-aim locked at the press (or a live one).</summary>
        public Vec2 ResolvedAim(MatchSim sim, PlayerState state, int slot, Vec2 stick)
        {
            if (stick.Length() >= sim.Rules.AimDeadzone) return stick.LimitLength(1.0);
            if (state.AimHold[slot] >= 0.0) return state.AimLock[slot];
            if (state.UsingSpecKit)
            {
                SkillDef skill = sim.GetPlayerSkill(state, slot);
                if (skill != null) return sim.Weapons.AutoAimSkill(sim, state, skill);
            }
            return sim.Weapons.AutoAim(sim, state, sim.WeaponDefs[state.Weapons[slot]]);
        }

        /// <summary>The target a TARGETED weapon would throw at (locked at the press while held).</summary>
        public int ResolvedTarget(MatchSim sim, PlayerState state, int slot)
        {
            if (state.AimHold[slot] >= 0.0) return state.AimTarget[slot];
            if (state.UsingSpecKit)
            {
                SkillDef skill = sim.GetPlayerSkill(state, slot);
                if (skill == null) return -1;
                PlayerState t = sim.NearestEnemy(state, skill.MaxRange);
                return t != null ? t.Id : -1;
            }
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
            if (SlotPressed(state, input.Buttons, slot))
            {
                if (state.Tagged || !state.Effects.CanCast() || sim.CastingBlocked(state)) state.AimHold[slot] = -1.0;
                else state.AimHold[slot] += dt;
                return;
            }
            if (SlotPressed(state, state.PreviousButtons, slot))
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
                bool freshPress = SlotPressed(state, input.Buttons, slot) && !SlotPressed(state, state.PreviousButtons, slot);
                if (!freshPress) continue;
                // an unaffordable card or skill cannot be picked up
                if (sim.WeaponReady(state, slot))
                {
                    state.AimHold[slot] = 0.0;
                    LockAim(sim, state, slot);
                }
                else if (state.UsingSpecKit && WeaponReady(state, slot) && !sim.CastingBlocked(state) && !sim.CanAfford(state, slot))
                {
                    sim.EmitWaterInsufficient(state.Id);
                }
                // The lowest freshly pressed slot owns this input even when it was not ready.
                break;
            }
        }

        void LockAim(MatchSim sim, PlayerState state, int slot)
        {
            if (state.UsingSpecKit)
            {
                SkillDef skill = sim.GetPlayerSkill(state, slot);
                if (skill != null)
                {
                    state.AimLock[slot] = sim.Weapons.AutoAimSkill(sim, state, skill);
                    PlayerState skillTarget = sim.NearestEnemy(state, skill.MaxRange);
                    state.AimTarget[slot] = skillTarget != null ? skillTarget.Id : -1;
                    return;
                }
            }
            WeaponDef def = sim.WeaponDefs[state.Weapons[slot]];
            state.AimLock[slot] = sim.Weapons.AutoAim(sim, state, def);
            PlayerState target = sim.NearestEnemy(state, def.MaxRange);
            state.AimTarget[slot] = target != null ? target.Id : -1;
        }

        void TryFire(MatchSim sim, PlayerState state, int slot, PlayerInput input, double holdSeconds)
        {
            if (!state.CanAct() || !sim.WeaponReady(state, slot)) return;
            if (state.UsingSpecKit)
            {
                SkillDef skill = sim.GetPlayerSkill(state, slot);
                if (skill != null)
                {
                    TryFireSkill(sim, state, slot, skill, input, holdSeconds);
                    return;
                }
            }
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
            state.JogTime = 0.0;
            state.Sprinting = false;
            state.CastLockLeft = def.CastLock;
            if (def.CastLock > 0.0) sim.EndMove(state);
            sim.EmitWeaponCast(state.Id, def.Id);
        }

        void TryFireSkill(MatchSim sim, PlayerState state, int slot, SkillDef skill, PlayerInput input, double holdSeconds)
        {
            bool isSyatoRecast = slot == 0 && skill.Mechanic == SkillMechanic.Syato
                && state.SyatoRecastLeft > 0.0 && state.SyatoTargetId >= 0;

            Vec2 aim;
            int targetId = state.AimTarget[slot];
            if (input.IsPressed(PlayerInput.BtnAimPoint) && input.AimPoint.IsFinite())
                aim = sim.AimFromPointSkill(state, skill, input.AimPoint);
            else
                aim = ResolvedAim(sim, state, slot, input.Aim.IsFinite() ? input.Aim : Vec2.Zero);

            if (!sim.Weapons.FireSkill(sim, state, skill, aim, holdSeconds, targetId, slot)) return;

            if (!isSyatoRecast)
            {
                if (!skill.IsUltimate && skill.WaterCost > 0)
                    state.Water = GMath.Maxf(0.0, state.Water - skill.WaterCost);

                if (slot == 0 && skill.MaxCharges > 1)
                {
                    state.S1Charges = GMath.Maxi(0, state.S1Charges - 1);
                    if (state.S1ChargeRegenLeft <= 0.0) state.S1ChargeRegenLeft = skill.Cooldown;
                    state.WeaponCooldowns[0] = state.S1Charges > 0 ? 0.25 : state.S1ChargeRegenLeft;
                }
                else
                {
                    state.WeaponCooldowns[slot] = skill.Cooldown;
                }
            }

            if (skill.Mechanic != SkillMechanic.Tago)
                state.InvisibleLeft = 0.0;

            state.OutOfCombatTime = 0.0;
            state.JogTime = 0.0;
            state.Sprinting = false;
            if (skill.IsUltimate) sim.EmitUltimateCast(state.Id);
            sim.EmitWeaponCast(state.Id, skill.Id);
        }
    }
}
