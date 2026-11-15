using System;
using System.Numerics;
using NUnit.Framework;

namespace Kalyeah.Game.Tests
{
    /// <summary>The shoulder camera's maths and follow (arena_camera.gd, camera_rules.gd) and the ability arc's geometry (ability_arc.gd).</summary>
    public class CameraAndArcTests
    {
        static CameraRulesData Rules() => CameraRulesData.Load(Kit.Data);

        [Test]
        public void AngleDifferenceIsTheShortestSignedTurn()
        {
            float d = CameraMath.AngleDifference(Deg(350f), Deg(10f));
            Assert.AreEqual(20f, d * 180f / MathF.PI, 1e-3f);
            Assert.AreEqual(-20f, CameraMath.AngleDifference(Deg(10f), Deg(350f)) * 180f / MathF.PI, 1e-3f);
        }

        [Test]
        public void TheAngleSpringCrossesTheSeamAndSettles()
        {
            float yaw = 350f, velocity = 0f;
            for (int i = 0; i < 120; i++) (yaw, velocity) = CameraMath.SmoothDampAngle(yaw, 20f, velocity, 0.22f, 160f, 1f / 60f);
            Assert.AreEqual(20f, yaw, 0.05f, "it went the short way over 0");
        }

        [Test]
        public void TheVectorSpringNeverOvershoots()
        {
            Vector2 at = Vector2.Zero, velocity = Vector2.Zero, goal = new Vector2(3f, 4f);
            for (int i = 0; i < 200; i++)
            {
                (at, velocity) = CameraMath.SmoothDamp(at, goal, velocity, 0.1f, 0f, 1f / 60f);
                Assert.LessOrEqual(at.Length(), 5.0001f);
            }
            Assert.AreEqual(5f, at.Length(), 1e-3f);
        }

        [Test]
        public void TheLateralOrbitIgnoresStraightRunsAndBackpedalsButFollowsAStrafe()
        {
            Assert.AreEqual(0f, CameraMath.LateralOrbitWeight(0f, 0f), 1e-5f, "straight ahead");
            Assert.AreEqual(0f, CameraMath.LateralOrbitWeight(0f, 180f), 1e-5f, "straight back: no whip");
            Assert.AreEqual(1f, CameraMath.LateralOrbitWeight(0f, 90f), 1e-4f, "sideways: full");
            Assert.Greater(CameraMath.LateralOrbitWeight(0f, 45f), 0.6f);
        }

        [Test]
        public void TheSpringArmPullsInFrontOfAWallAndInsideTheArena()
        {
            var pivot = new Vector3(0f, 1.4f, 0f);
            var desired = new Vector3(0f, 2f, 3f);
            var wall = new[] { new GroundRect(-5f, 1.5f, 10f, 1f) };
            Vector3 eye = CameraMath.ClampShoulderEye(pivot, desired, wall, 0f);
            Assert.Less(eye.Z, 1.5f - CameraMath.WallPad + 0.001f, "it stops before the padded wall");
            Assert.Greater(eye.Z, 0f);
            Vector3 free = CameraMath.ClampShoulderEye(pivot, desired, Array.Empty<GroundRect>(), 0f);
            Assert.AreEqual(desired, free, "nothing in the way: where it wanted");
            Vector3 edge = CameraMath.ClampShoulderEye(new Vector3(0f, 1.4f, 9f), new Vector3(0f, 2f, 12f), Array.Empty<GroundRect>(), 10f);
            Assert.LessOrEqual(new Vector2(edge.X, edge.Z).Length(), 10f - CameraMath.ArenaPad + 1e-3f);
        }

        [Test]
        public void APropIsHiddenWhenItCrossesTheLineToTheKidOrHugsTheCamera()
        {
            var eye = new Vector3(0f, 2f, 3f);
            var feet = Vector3.Zero;
            Assert.IsTrue(CameraMath.Blocks(eye, feet, 1.5f, 0f, new Vector3(-1f, 0f, 1f), new Vector3(1f, 3f, 2f)), "between");
            Assert.IsFalse(CameraMath.Blocks(eye, feet, 1.5f, 0f, new Vector3(4f, 0f, 1f), new Vector3(5f, 3f, 2f)), "beside");
            Assert.IsTrue(CameraMath.Blocks(eye, feet, 1.5f, 2.2f, new Vector3(1.5f, 0f, 3f), new Vector3(2f, 3f, 4f)), "within the clear reach");
        }

        [Test]
        public void ADragTurnsTheViewAndThePitchStaysInItsLimits()
        {
            CameraRulesData rules = Rules();
            var cam = new ShoulderFollow(rules);
            cam.Place(false);
            Assert.AreEqual(0f, cam.LookYaw);
            Assert.AreEqual(rules.LookPitchDegrees, cam.LookPitch);
            cam.Look(new Vector2(100f, 0f));
            Assert.AreEqual(CameraMath.PosMod(-100f * rules.LookSensitivity, 360f), cam.LookYaw, 1e-3f, "a drag right turns right");
            cam.Look(new Vector2(0f, 10000f));
            Assert.AreEqual(rules.LookPitchMax, cam.LookPitch, "down to the limit");
            cam.Look(new Vector2(0f, -100000f));
            Assert.AreEqual(rules.LookPitchMin, cam.LookPitch);
            cam.Place(true);
            Assert.AreEqual(180f, cam.LookYaw, "a change of side looks down the street from the other end");
        }

        [Test]
        public void TheViewGlidesBehindAStrafingKidOnlyAfterTheManualLookRunsOut()
        {
            CameraRulesData rules = Rules();
            var cam = new ShoulderFollow(rules);
            cam.Place(false);
            cam.Look(new Vector2(1f, 0f));
            float afterLook = cam.LookYaw;
            var east = new Vector2(1f, 0f); // running toward +X: 90 degrees off a view down -Z
            cam.SyncBehindCharacter(east, true, 0.1f);
            Assert.AreEqual(afterLook, cam.LookYaw, "held off right after a manual look");
            for (int i = 0; i < 300; i++) cam.SyncBehindCharacter(east, true, 1f / 60f);
            float runYaw = CameraMath.PosMod(MathF.Atan2(-east.X, -east.Y) * 180f / MathF.PI, 360f);
            Assert.Less(MathF.Abs(CameraMath.AngleDifference(Deg(cam.LookYaw), Deg(runYaw))), Deg(10f), "it ends up behind the run");
        }

        [Test]
        public void RunningStraightAtTheCameraNeverWhipsItRound()
        {
            var cam = new ShoulderFollow(Rules());
            cam.Place(false);
            for (int i = 0; i < 300; i++) cam.SyncBehindCharacter(new Vector2(0f, 1f), true, 1f / 60f);
            Assert.AreEqual(0f, cam.LookYaw, 1e-3f);
        }

        [Test]
        public void ThePostureDropsForACrouchAndThePivotFollowsTheKid()
        {
            var cam = new ShoulderFollow(Rules()) { ReducedMotion = true };
            cam.Place(false);
            cam.SyncPosture(true, 0f, 1f / 60f);
            Assert.AreEqual(ShoulderFollow.CrouchPivotOffset, cam.VerticalOffset, 1e-5f);
            cam.Follow(new Vector2(2f, 3f), snap: true);
            cam.Advance(1f / 60f);
            Assert.AreEqual(new Vector2(2f, 3f), cam.Aim);
            var (eye, pitch, yaw) = cam.Pose(Array.Empty<GroundRect>(), 0f);
            Assert.Greater(eye.Z, 3f, "the camera stands behind the kid (+Z, looking down -Z)");
            Assert.Greater(eye.X, 2f, "and to its right");
            Assert.AreEqual(cam.LookPitch, pitch);
            Assert.AreEqual(0f, yaw);
        }

        [Test]
        public void TheJoystickUpIsTheViewsForward()
        {
            var cam = new ShoulderFollow(Rules());
            cam.Place(false);
            Vector2 way = cam.GroundWay(new Vector2(0f, -1f));
            Assert.AreEqual(0f, way.X, 1e-5f);
            Assert.AreEqual(-1f, way.Y, 1e-5f);
            Assert.AreEqual(way, cam.Forward);
            Vector2 right = cam.ScreenWay(new Vector2(1f, 0f));
            Assert.AreEqual(new Vector2(1f, 0f), right, "+X is screen right when looking down -Z");
        }

        // ---- the arc ------------------------------------------------------------------------------------------------------

        static readonly Vector2 Screen = new Vector2(1280f, 720f);

        [Test]
        public void TheCardsArcTurnsRoundTheCornerWithNextInIt()
        {
            var arc = new ArcLayout();
            arc.Layout(Screen, Vector2.Zero, usesElixir: true);
            Vector2 corner = ArcLayout.CornerPoint(Screen, Vector2.Zero);
            Assert.AreEqual(corner, arc.Corner);
            for (int slot = 0; slot < ArcLayout.Slots; slot++)
            {
                Assert.AreEqual(ArcLayout.ArcRadius, Vector2.Distance(arc.Centers[slot], corner), 1e-3f);
                Assert.LessOrEqual(arc.Centers[slot].X + ArcLayout.ButtonRadius, Screen.X, "on screen");
                Assert.LessOrEqual(arc.Centers[slot].Y + ArcLayout.ButtonRadius, Screen.Y);
                Assert.AreEqual(slot, arc.SlotAt(arc.Centers[slot], null));
            }
            Assert.AreEqual(-1, arc.SlotAt(new Vector2(100f, 100f), null));
            Assert.IsTrue(arc.OverCancel(arc.CancelCenter));
            Assert.Less(arc.CancelCenter.Y, arc.Centers[2].Y, "the X stands above the arc");
            Assert.AreEqual(Vector2.Zero, arc.WayFor(0, arc.Centers[0] + new Vector2(10f, 0f)), "inside the dead zone");
            Assert.AreEqual(1f, arc.WayFor(0, arc.Centers[0] + new Vector2(0f, -500f)).Length(), 1e-4f, "at most the card's range");
        }

        [Test]
        public void TheHeroKitSitsInTheSafeAreaWithoutOverlaps()
        {
            var arc = new ArcLayout();
            var inset = new Vector2(48f, 20f);
            arc.Layout(Screen, inset, usesElixir: false);
            var buttons = new[] { ArcLayout.Button.Primary, ArcLayout.Button.Ultimate, ArcLayout.Button.Vault, ArcLayout.Button.Jump, ArcLayout.Button.Crouch };
            foreach (var b in buttons)
            {
                Vector2 c = arc.CenterOf(b);
                float r = ArcLayout.RadiusOf(b);
                Assert.LessOrEqual(c.X + r, Screen.X - inset.X, b.ToString());
                Assert.LessOrEqual(c.Y + r, Screen.Y - inset.Y, b.ToString());
                Assert.AreEqual(b, arc.ButtonAt(c), b.ToString());
                for (int slot = 0; slot < 3; slot++)
                    Assert.Greater(Vector2.Distance(c, arc.Centers[slot]), r + ArcLayout.HeroButtonRadius - 1f, $"{b} clear of slot {slot}");
            }
            var has = new[] { true, true, true, false };
            for (int slot = 0; slot < 3; slot++) Assert.AreEqual(slot, arc.SlotAt(arc.Centers[slot], has));
            Assert.AreEqual(-1, arc.SlotAt(arc.Centers[3], has), "an empty fourth slot is not a button");
            var cards = new ArcLayout();
            cards.Layout(Screen, inset, usesElixir: true);
            Assert.AreEqual(ArcLayout.Button.None, cards.ButtonAt(cards.Centers[0]), "the cards kit has no hero buttons");
        }

        [Test]
        public void TheBarsStandAtTheBottomCentre()
        {
            var arc = new ArcLayout();
            arc.Layout(Screen, Vector2.Zero, true);
            Assert.AreEqual((Screen.X - ArcLayout.BarsWidth) / 2f, arc.BarRect.X);
            Assert.AreEqual(Screen.Y - ArcLayout.Edge, arc.StaminaRect.Y + arc.StaminaRect.W * 0f + ArcLayout.StaminaBar, 1e-3f);
            Assert.Less(arc.BarRect.Y, arc.StaminaRect.Y, "the ice water bar over the stamina bar");
        }

        static float Deg(float d) => d * MathF.PI / 180f;
    }
}
