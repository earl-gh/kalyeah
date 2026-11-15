using Kalyeah.Sim;

namespace Kalyeah.Game
{
    /// <summary>
    /// Port of scripts/input/match_input.gd: turns the local player's gestures into one PlayerInput per sim tick (docs/GDD.md "Controls").
    /// A tap or a held drag on the street (a move target), the one card held from the hand (cast at a point, tapped, or put back), the
    /// joystick, the hero buttons (primary, dash, ultimate, jump, crouch, slide) and Tumbang Preso's Throw / Kick. Engine-free; it decides
    /// only what the gesture was, never a rule.
    ///
    /// A held card is a small state machine so a quick tap is never lost: the press sends the card's bit; the release sends one tick
    /// without it, with the aim point (a cast there), with nothing (a tap: the sim auto-aims) or with BtnAimCancel.
    /// </summary>
    public sealed class MatchInput
    {
        /// <summary>What letting go of a held card means.</summary>
        public enum Release { None, Cast, Tap, Cancel }

        /// <summary>The longest press (seconds) on a card that still counts as a tap (GameRules.card_tap_time).</summary>
        public double TapTime = 0.3;
        /// <summary>A finger held on the street drags the move target with it (GameRules.move_follow_hold).</summary>
        public bool FollowHold = true;
        public int PendingButtons;

        public Vec2 MovePoint = Vec2.Zero;
        public bool MoveHeld;
        bool _moveFresh;

        /// <summary>The hand slot held right now (-1: none).</summary>
        public int HeldSlot = -1;
        /// <summary>The finger has left the card it pressed: letting go is a cast or a drop, never a tap.</summary>
        public bool LeftCard;
        /// <summary>The finger is over the street: `AimPoint` is the street point under it.</summary>
        public bool OverStreet;
        public Vec2 AimPoint = Vec2.Zero;
        public bool Aimed;
        public bool OutOfBounds;
        /// <summary>The joystick / knob as a way on the street (sim XZ, length 0..1).</summary>
        public Vec2 Stick = Vec2.Zero;

        public bool ThrowHeld;
        public Vec2 ThrowAim = new Vec2(0.0, -1.0);
        bool _throwRelease, _throwCancel, _throwSent;
        long _pressedMs;
        bool _sentHeld;
        Release _release = Release.None;
        int _releaseSlot = -1;
        Vec2 _releasePoint = Vec2.Zero;

        public bool PrimaryHeld;
        public Vec2 PrimaryAimPoint = Vec2.Zero;
        bool _primarySent, _primaryTap;
        bool _ultPending;
        Vec2 _ultAimPoint = Vec2.Zero;
        public bool CrouchHeld;
        bool _crouchSent, _crouchTap;

        /// <summary>A finger went down on the street at `point`: walk there.</summary>
        public void MovePress(Vec2 point)
        {
            MovePoint = point;
            MoveHeld = true;
            _moveFresh = true;
        }

        /// <summary>The finger on the street moved to `point`: the target follows it while it is held (if FollowHold).</summary>
        public void MoveDrag(Vec2 point)
        {
            if (!MoveHeld || !FollowHold) return;
            MovePoint = point;
            _moveFresh = true;
        }

        public void MoveRelease() => MoveHeld = false;

        /// <summary>The Kick / Dive / Lupa button: the next tick carries the second card's bit once.</summary>
        public void KickPress() => PendingButtons |= PlayerInput.BtnCard2;

        /// <summary>Holds or releases the hero primary (Tirador) aimed at `aimPoint`.</summary>
        public void SetPrimary(bool held, Vec2 aimPoint)
        {
            if (held)
            {
                if (!PrimaryHeld) _primarySent = false;
                PrimaryHeld = true;
                PrimaryAimPoint = aimPoint;
            }
            else
            {
                if (PrimaryHeld && !_primarySent)
                {
                    _primaryTap = true;
                    PrimaryAimPoint = aimPoint;
                }
                PrimaryHeld = false;
            }
        }

        public void PressDash() => PendingButtons |= PlayerInput.BtnDash;

        public void PressUltimate(Vec2 aimPoint)
        {
            PendingButtons |= PlayerInput.BtnUltimate;
            _ultPending = true;
            _ultAimPoint = aimPoint;
        }

        public void PressJump() => PendingButtons |= PlayerInput.BtnJump;

        /// <summary>Holds or releases crouch, keeping a quick tap for one tick.</summary>
        public void SetCrouch(bool active)
        {
            if (active)
            {
                if (!CrouchHeld) _crouchSent = false;
                CrouchHeld = true;
            }
            else
            {
                if (CrouchHeld && !_crouchSent) _crouchTap = true;
                CrouchHeld = false;
            }
        }

        public void PressSlide() => PendingButtons |= PlayerInput.BtnSlide;

        public void ThrowPress(Vec2 way)
        {
            if (ThrowHeld || _throwRelease) return;
            ThrowHeld = true;
            ThrowAim = way;
            _throwSent = false;
        }

        public void ThrowTurn(Vec2 way)
        {
            if (ThrowHeld) ThrowAim = way;
        }

        public void ThrowRelease(bool cancel)
        {
            if (!ThrowHeld) return;
            ThrowHeld = false;
            _throwRelease = true;
            _throwCancel = cancel;
        }

        /// <summary>A card in `slot` was picked up at `nowMs`. False (ignored) while another is held or a release is still to be sent.</summary>
        public bool CardPress(int slot, long nowMs)
        {
            if (HeldSlot >= 0 || _release != Release.None || slot < 0 || slot >= PlayerInput.CardBits.Length) return false;
            HeldSlot = slot;
            _pressedMs = nowMs;
            LeftCard = false;
            OverStreet = false;
            OutOfBounds = false;
            Aimed = false;
            _sentHeld = false;
            return true;
        }

        /// <summary>The held card's finger moved: still on its card (`onCard`), over the ground at `streetPoint` (null: over the hand).</summary>
        public void CardDrag(bool onCard, Vec2? streetPoint, bool inside = true)
        {
            if (HeldSlot < 0) return;
            if (!onCard) LeftCard = true;
            bool onGround = streetPoint.HasValue;
            OverStreet = onGround && inside;
            OutOfBounds = onGround && !inside;
            if (onGround)
            {
                LeftCard = true;
                Aimed = true;
                AimPoint = streetPoint.Value;
            }
        }

        public Release ReleaseKind(long nowMs)
        {
            if (HeldSlot < 0) return Release.None;
            if (OverStreet) return Release.Cast;
            if (!LeftCard && (nowMs - _pressedMs) <= TapTime * 1000.0) return Release.Tap;
            return Release.Cancel;
        }

        public Release CardRelease(long nowMs)
        {
            Release kind = ReleaseKind(nowMs);
            Finish(kind);
            return kind;
        }

        /// <summary>The held card was dropped by something else (a cancelled touch, a menu): nothing is cast.</summary>
        public void CardCancel()
        {
            if (HeldSlot >= 0) Finish(Release.Cancel);
        }

        public bool CardHeld() => HeldSlot >= 0;

        void Finish(Release kind)
        {
            _release = kind;
            _releaseSlot = HeldSlot;
            _releasePoint = AimPoint;
            HeldSlot = -1;
            LeftCard = false;
            OverStreet = false;
            OutOfBounds = false;
            Aimed = false;
        }

        /// <summary>Forget everything held or pending (a menu opened over the controls).</summary>
        public void Clear()
        {
            PendingButtons = 0;
            MoveHeld = false;
            _moveFresh = false;
            HeldSlot = -1;
            LeftCard = false;
            OverStreet = false;
            OutOfBounds = false;
            Aimed = false;
            _release = Release.None;
            _releaseSlot = -1;
            _sentHeld = false;
            ThrowHeld = false;
            _throwRelease = false;
            _throwCancel = false;
            _throwSent = false;
            PrimaryHeld = false;
            PrimaryAimPoint = Vec2.Zero;
            _primarySent = false;
            _primaryTap = false;
            _ultPending = false;
            _ultAimPoint = Vec2.Zero;
            CrouchHeld = false;
            _crouchSent = false;
            _crouchTap = false;
            Stick = Vec2.Zero;
        }

        /// <summary>This tick's input (the stick's length 0..1 is kept: walk or run).</summary>
        public PlayerInput Build(int tick)
        {
            PlayerInput input = PlayerInput.Create(Stick.LimitLength(1.0), Vec2.Zero, PendingButtons, tick);
            PendingButtons = 0;
            if (_moveFresh || (MoveHeld && FollowHold))
            {
                input.WithMoveTarget(MovePoint);
                _moveFresh = false;
            }
            if (CrouchHeld || _crouchTap)
            {
                input.Buttons |= PlayerInput.BtnCrouch;
                _crouchSent = CrouchHeld;
                _crouchTap = false;
            }
            if (PrimaryHeld || _primaryTap)
            {
                input.Buttons |= PlayerInput.BtnPrimary;
                input.WithAimPoint(PrimaryAimPoint);
                _primarySent = PrimaryHeld;
                _primaryTap = false;
            }
            if (_ultPending)
            {
                input.WithAimPoint(_ultAimPoint);
                _ultPending = false;
            }
            if (ThrowHeld)
            {
                input.Buttons |= PlayerInput.BtnCard1;
                input.Aim = ThrowAim;
                _throwSent = true;
            }
            else if (_throwRelease)
            {
                if (!_throwSent)
                {
                    // a press and release inside one tick: the sim must see the button held once before the release
                    input.Buttons |= PlayerInput.BtnCard1;
                    input.Aim = ThrowAim;
                    _throwSent = true;
                }
                else
                {
                    if (_throwCancel) input.Buttons |= PlayerInput.BtnAimCancel;
                    _throwRelease = false;
                    _throwSent = false;
                }
            }
            if (HeldSlot >= 0)
            {
                input.Buttons |= PlayerInput.CardBits[HeldSlot];
                _sentHeld = true;
                if (OverStreet) input.WithAimPoint(AimPoint);
            }
            else if (_release != Release.None)
            {
                int bit = PlayerInput.CardBits[_releaseSlot];
                if (!_sentHeld)
                {
                    // a press and release inside one tick: the sim must see the card held once before the release
                    input.Buttons |= bit;
                    _sentHeld = true;
                }
                else
                {
                    switch (_release)
                    {
                        case Release.Cast:
                            input.WithAimPoint(_releasePoint);
                            break;
                        case Release.Tap:
                            input.Buttons &= ~PlayerInput.BtnAimPoint;
                            break;
                        case Release.Cancel:
                            input.Buttons |= PlayerInput.BtnAimCancel;
                            input.Buttons &= ~PlayerInput.BtnAimPoint;
                            break;
                    }
                    _release = Release.None;
                    _releaseSlot = -1;
                    _sentHeld = false;
                }
            }
            return input;
        }
    }
}
