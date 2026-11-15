namespace Kalyeah.Sim
{
    /// <summary>
    /// The one input format (port of player_input.gd): touch and network both produce this. Only move/aim/buttons/tick and street points,
    /// never a gameplay decision.
    /// </summary>
    public sealed class PlayerInput
    {
        public const int BtnCard1 = 1 << 0;
        public const int BtnCard2 = 1 << 1;
        public const int BtnCard3 = 1 << 2;
        public const int BtnCard4 = 1 << 3;
        /// <summary>The hand slots in order (slot 0 = BtnCard1).</summary>
        public static readonly int[] CardBits = { BtnCard1, BtnCard2, BtnCard3, BtnCard4 };
        public const int BtnWeapon1 = BtnCard1;
        public const int BtnWeapon2 = BtnCard2;
        /// <summary>Sent on the tick a card is released over the hand: no cast, nothing spent.</summary>
        public const int BtnAimCancel = 1 << 5;
        /// <summary>`MoveTarget` is a new street point this tick.</summary>
        public const int BtnMoveTarget = 1 << 6;
        /// <summary>`AimPoint` is the street point under the held card / crosshair.</summary>
        public const int BtnAimPoint = 1 << 7;
        public const int BtnPrimary = 1 << 8;
        public const int BtnDash = 1 << 9;
        public const int BtnUltimate = 1 << 10;
        public const int BtnJump = 1 << 11;
        public const int BtnCrouch = 1 << 12;
        public const int BtnSlide = 1 << 13;
        /// <summary>Every bit a client may send; anything else is dropped by the server.</summary>
        public const int KnownBits = BtnCard1 | BtnCard2 | BtnCard3 | BtnCard4 | BtnAimCancel | BtnMoveTarget | BtnAimPoint
            | BtnPrimary | BtnDash | BtnUltimate | BtnJump | BtnCrouch | BtnSlide;

        /// <summary>Analog movement stick. Non-zero clears the move target.</summary>
        public Vec2 Move = Vec2.Zero;
        /// <summary>Stick-style aim relative to the kid (length 0..1 of the range; zero = auto-aim).</summary>
        public Vec2 Aim = Vec2.Zero;
        public int Buttons;
        public int Tick;
        /// <summary>A street point (only read with BtnMoveTarget).</summary>
        public Vec2 MoveTarget = Vec2.Zero;
        /// <summary>A street point (only read with BtnAimPoint).</summary>
        public Vec2 AimPoint = Vec2.Zero;

        public static PlayerInput Create(Vec2 move, Vec2 aim, int buttons, int tick) =>
            new PlayerInput { Move = move, Aim = aim, Buttons = buttons, Tick = tick };

        /// <summary>Sets `MoveTarget` and BtnMoveTarget. Returns this.</summary>
        public PlayerInput WithMoveTarget(Vec2 point)
        {
            MoveTarget = point;
            Buttons |= BtnMoveTarget;
            return this;
        }

        /// <summary>Sets `AimPoint` and BtnAimPoint. Returns this.</summary>
        public PlayerInput WithAimPoint(Vec2 point)
        {
            AimPoint = point;
            Buttons |= BtnAimPoint;
            return this;
        }

        public bool IsPressed(int button) => (Buttons & button) != 0;
    }
}
