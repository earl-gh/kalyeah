using System;
using System.Numerics;

namespace Kalyeah.Game
{
    /// <summary>
    /// Where the round ability arc's buttons stand (port of scripts/ui/hud/ability_arc.gd `_layout`, `corner_point`, `slot_at`, `way_for`),
    /// in the HUD's own pixels on the 1280 x 720 landscape base, Godot style: origin at the top left, y DOWN. Two kits (docs/GDD.md
    /// "Landscape layout"):
    /// - <b>Cards</b> (`GameRules.uses_elixir()`): the four cards on a quarter circle round the bottom-right corner, NEXT small in the corner,
    ///   the ice water bar and the stamina bar along the bottom centre, the cancel X above the arc while a card is held.
    /// - <b>Hero</b> (no elixir: today's data): the big primary (Tirador) in the corner, the three superpowers round it, the ultimate,
    ///   vault (dash), jump and crouch/slide beside it.
    /// Engine-free: the view places UGUI buttons at these points and asks it which button a finger is on.
    /// </summary>
    public sealed class ArcLayout
    {
        public const int Slots = 4;
        public const float ButtonRadius = 48f, NextRadius = 30f, ArcRadius = 160f, AimRadius = 160f, AimDeadzone = 22f;
        public const float CancelRadius = 40f, CancelGap = 18f, BarsWidth = 360f, SlimBar = 14f, StaminaBar = 8f, BarsGap = 8f;
        public const float CardGap = 10f;
        /// <summary>HudStyle.EDGE: the room the controls keep from the safe area's edge.</summary>
        public const float Edge = 24f;
        public static readonly float[] Angles = { 175f, 215f, 255f, 295f };

        public const float PrimaryRadius = 50f, HeroButtonRadius = 38f, HeroArcRadius = 122f, UltRadius = 37f, JumpRadius = 31f;
        public const float VaultRadius = 30f, CrouchRadius = 30f;
        /// <summary>The extra reach round a hero button that still counts as on it.</summary>
        public const float HeroTouchPad = 6f;
        public static readonly float[] HeroAngles = { 180f, 225f, 270f };

        public enum Button { None, Primary, Ultimate, Vault, Jump, Crouch }

        public bool UsesElixir { get; private set; }
        public Vector2 Size { get; private set; }
        public Vector2 Inset { get; private set; }
        public readonly Vector2[] Centers = new Vector2[Slots];
        public Vector2 Corner, NextCenter, CancelCenter;
        public Vector2 PrimaryCenter, UltCenter, JumpCenter, VaultCenter, CrouchCenter;
        /// <summary>(x, y, width, height), y down.</summary>
        public Vector4 BarRect, StaminaRect;

        public float SlotRadius => UsesElixir ? ButtonRadius : HeroButtonRadius;

        /// <summary>The arc's turning point for a screen of `screen` with the safe area's right/bottom insets `insets`.</summary>
        public static Vector2 CornerPoint(Vector2 screen, Vector2 insets)
        {
            Vector2 reach = Vector2.Zero;
            foreach (float angle in Angles) reach = Vector2.Max(reach, FromAngle(angle) * ArcRadius);
            Vector2 offset = reach + Vector2.One * (ButtonRadius + Edge);
            return screen - insets - offset;
        }

        public void Layout(Vector2 size, Vector2 inset, bool usesElixir)
        {
            Size = size;
            Inset = inset;
            UsesElixir = usesElixir;
            Corner = CornerPoint(size, inset);
            if (usesElixir)
            {
                float top = float.PositiveInfinity;
                for (int i = 0; i < Slots; i++)
                {
                    Centers[i] = Corner + FromAngle(Angles[i]) * ArcRadius;
                    top = MathF.Min(top, Centers[i].Y - ButtonRadius);
                }
                NextCenter = Corner + new Vector2(NextRadius * 0.6f, NextRadius * 0.4f);
                CancelCenter = new Vector2(Corner.X, top - CancelGap - CancelRadius);
            }
            else
            {
                Vector2 anchor = size - inset;
                PrimaryCenter = anchor - new Vector2(86f, 84f);
                for (int i = 0; i < HeroAngles.Length; i++) Centers[i] = PrimaryCenter + FromAngle(HeroAngles[i]) * HeroArcRadius;
                Centers[3] = PrimaryCenter + new Vector2(-312f, -10f);
                UltCenter = PrimaryCenter + new Vector2(-168f, -134f);
                JumpCenter = PrimaryCenter + new Vector2(8f, -204f);
                VaultCenter = PrimaryCenter + new Vector2(-208f, -44f);
                CrouchCenter = PrimaryCenter + new Vector2(-206f, 34f);
                NextCenter = PrimaryCenter;
                float topSlot = Centers[2].Y - HeroButtonRadius;
                CancelCenter = new Vector2(PrimaryCenter.X - 42f, MathF.Max(topSlot - CancelGap - CancelRadius - 48f, CancelRadius + 112f));
            }
            float left = (size.X - BarsWidth) / 2f;
            StaminaRect = new Vector4(left, size.Y - inset.Y - Edge - StaminaBar, BarsWidth, StaminaBar);
            BarRect = new Vector4(left, StaminaRect.Y - BarsGap - SlimBar, BarsWidth, SlimBar);
        }

        /// <summary>The card slot under `local` (-1: none). `hasCard[slot]`: the hero kit skips its empty slots.</summary>
        public int SlotAt(Vector2 local, bool[] hasCard)
        {
            for (int slot = 0; slot < Slots; slot++)
            {
                if (!UsesElixir && (hasCard == null || !hasCard[slot])) continue;
                if (Vector2.Distance(local, Centers[slot]) <= SlotRadius + CardGap) return slot;
            }
            return -1;
        }

        /// <summary>The hero button under `local` (cards kit: always None).</summary>
        public Button ButtonAt(Vector2 local)
        {
            if (UsesElixir) return Button.None;
            if (Vector2.Distance(local, PrimaryCenter) <= PrimaryRadius + HeroTouchPad) return Button.Primary;
            if (Vector2.Distance(local, VaultCenter) <= VaultRadius + HeroTouchPad) return Button.Vault;
            if (Vector2.Distance(local, UltCenter) <= UltRadius + HeroTouchPad) return Button.Ultimate;
            if (Vector2.Distance(local, JumpCenter) <= JumpRadius + HeroTouchPad) return Button.Jump;
            if (Vector2.Distance(local, CrouchCenter) <= CrouchRadius + HeroTouchPad) return Button.Crouch;
            return Button.None;
        }

        public static float RadiusOf(Button button) => button switch
        {
            Button.Primary => PrimaryRadius,
            Button.Ultimate => UltRadius,
            Button.Vault => VaultRadius,
            Button.Jump => JumpRadius,
            Button.Crouch => CrouchRadius,
            _ => 0f,
        };

        public Vector2 CenterOf(Button button) => button switch
        {
            Button.Primary => PrimaryCenter,
            Button.Ultimate => UltCenter,
            Button.Vault => VaultCenter,
            Button.Jump => JumpCenter,
            Button.Crouch => CrouchCenter,
            _ => Vector2.Zero,
        };

        /// <summary>A held card's finger at `local` is over the cancel X.</summary>
        public bool OverCancel(Vector2 local) => Vector2.Distance(local, CancelCenter) <= CancelRadius + CardGap;

        /// <summary>The aim way of a drag from slot `slot`'s button to `local` (length 0..1 of the card's range; zero inside the dead zone).</summary>
        public Vector2 WayFor(int slot, Vector2 local)
        {
            Vector2 offset = local - Centers[slot];
            if (offset.Length() <= AimDeadzone) return Vector2.Zero;
            Vector2 way = offset / AimRadius;
            return way.Length() > 1f ? Vector2.Normalize(way) : way;
        }

        /// <summary>Godot Vector2.from_angle of `degrees` (y down on screen).</summary>
        public static Vector2 FromAngle(float degrees)
        {
            float r = degrees * MathF.PI / 180f;
            return new Vector2(MathF.Cos(r), MathF.Sin(r));
        }
    }
}
