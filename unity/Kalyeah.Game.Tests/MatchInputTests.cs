using Kalyeah.Sim;
using NUnit.Framework;

namespace Kalyeah.Game.Tests
{
    /// <summary>
    /// Port of tests/unit/test_card_drag_input.gd (the MatchInput part) plus the hero buttons: tap to move and cards to cast, as the
    /// PlayerInput the sim receives (docs/GDD.md "Controls").
    /// </summary>
    public class MatchInputTests
    {
        const long Now = 10000;
        static readonly Vec2 Point = new Vec2(1.5, -4.0);

        static MatchInput Make() => new MatchInput { TapTime = 0.3 };

        [Test]
        public void AStreetTapSendsAMoveTargetOnce()
        {
            MatchInput input = Make();
            input.FollowHold = false;
            input.MovePress(Point);
            input.MoveRelease();
            PlayerInput built = input.Build(1);
            Assert.IsTrue(built.IsPressed(PlayerInput.BtnMoveTarget));
            Assert.AreEqual(Point, built.MoveTarget);
            Assert.AreEqual(Vec2.Zero, built.Move);
            Assert.IsFalse(input.Build(2).IsPressed(PlayerInput.BtnMoveTarget), "sent once");
        }

        [Test]
        public void AHeldFingerDragsTheTargetEveryTickAndLettingGoKeepsTheLastPoint()
        {
            MatchInput input = Make();
            input.MovePress(Point);
            Assert.AreEqual(Point, input.Build(1).MoveTarget);
            input.MoveDrag(new Vec2(2.0, -5.0));
            PlayerInput built = input.Build(2);
            Assert.IsTrue(built.IsPressed(PlayerInput.BtnMoveTarget));
            Assert.AreEqual(new Vec2(2.0, -5.0), built.MoveTarget);
            Assert.IsTrue(input.Build(3).IsPressed(PlayerInput.BtnMoveTarget), "held still: still sent");
            input.MoveRelease();
            Assert.IsFalse(input.Build(4).IsPressed(PlayerInput.BtnMoveTarget));
        }

        [Test]
        public void WithoutFollowHoldADragDoesNotMoveTheTarget()
        {
            MatchInput input = Make();
            input.FollowHold = false;
            input.MovePress(Point);
            input.Build(1);
            input.MoveDrag(new Vec2(9.0, 9.0));
            Assert.IsFalse(input.Build(2).IsPressed(PlayerInput.BtnMoveTarget));
        }

        [Test]
        public void AQuickTapOnACardIsAHeldTickThenAPlainRelease()
        {
            MatchInput input = Make();
            Assert.IsTrue(input.CardPress(2, Now));
            PlayerInput held = input.Build(1);
            Assert.IsTrue(held.IsPressed(PlayerInput.BtnCard3));
            Assert.IsFalse(held.IsPressed(PlayerInput.BtnAimPoint));
            Assert.AreEqual(MatchInput.Release.Tap, input.CardRelease(Now + 200));
            PlayerInput release = input.Build(2);
            Assert.AreEqual(0, release.Buttons & (PlayerInput.CardBits[2] | PlayerInput.BtnAimPoint | PlayerInput.BtnAimCancel), "the sim auto-aims");
        }

        [Test]
        public void ATapInsideOneTickStillShowsTheSimOneHeldTick()
        {
            MatchInput input = Make();
            input.CardPress(0, Now);
            input.CardRelease(Now + 20);
            Assert.IsTrue(input.Build(1).IsPressed(PlayerInput.BtnCard1), "held once");
            Assert.IsFalse(input.Build(2).IsPressed(PlayerInput.BtnCard1), "then released");
            Assert.AreEqual(0, input.Build(3).Buttons, "and nothing after");
        }

        [Test]
        public void ACardDraggedOntoTheStreetSendsThePointEachTickAndCastsThere()
        {
            MatchInput input = Make();
            input.CardPress(1, Now);
            input.CardDrag(false, new Vec2(0.5, -2.0));
            PlayerInput held = input.Build(1);
            Assert.IsTrue(held.IsPressed(PlayerInput.BtnCard2));
            Assert.IsTrue(held.IsPressed(PlayerInput.BtnAimPoint));
            Assert.AreEqual(new Vec2(0.5, -2.0), held.AimPoint);
            input.CardDrag(false, Point);
            Assert.AreEqual(Point, input.Build(2).AimPoint);
            Assert.AreEqual(MatchInput.Release.Cast, input.CardRelease(Now + 900), "held long but over the street: still a cast");
            PlayerInput release = input.Build(3);
            Assert.IsFalse(release.IsPressed(PlayerInput.BtnCard2));
            Assert.IsTrue(release.IsPressed(PlayerInput.BtnAimPoint));
            Assert.AreEqual(Point, release.AimPoint);
            Assert.IsFalse(release.IsPressed(PlayerInput.BtnAimCancel));
        }

        [Test]
        public void DroppingTheCardBackOnTheHandCancels()
        {
            MatchInput input = Make();
            input.CardPress(0, Now);
            input.CardDrag(false, Point);
            input.Build(1);
            input.CardDrag(false, null);
            Assert.AreEqual(MatchInput.Release.Cancel, input.ReleaseKind(Now + 100));
            input.CardRelease(Now + 100);
            PlayerInput release = input.Build(2);
            Assert.IsTrue(release.IsPressed(PlayerInput.BtnAimCancel));
            Assert.IsFalse(release.IsPressed(PlayerInput.BtnAimPoint));
        }

        [Test]
        public void DraggingBackOntoItsOwnCardAfterLeavingItIsACancelNotATap()
        {
            MatchInput input = Make();
            input.CardPress(0, Now);
            input.CardDrag(false, null);
            input.CardDrag(true, null);
            Assert.AreEqual(MatchInput.Release.Cancel, input.CardRelease(Now + 50));
        }

        [Test]
        public void ALongPressOnTheCardWithoutDraggingDoesNothing()
        {
            MatchInput input = Make();
            input.CardPress(0, Now);
            input.Build(1);
            Assert.AreEqual(MatchInput.Release.Cancel, input.CardRelease(Now + 301));
            Assert.IsTrue(input.Build(2).IsPressed(PlayerInput.BtnAimCancel));
        }

        [Test]
        public void OneCardAtATime()
        {
            MatchInput input = Make();
            Assert.IsTrue(input.CardPress(0, Now));
            Assert.IsFalse(input.CardPress(3, Now));
            Assert.AreEqual(0, input.Build(1).Buttons & PlayerInput.BtnCard4);
        }

        [Test]
        public void ACancelledHoldSendsCancel()
        {
            MatchInput input = Make();
            input.CardPress(0, Now);
            input.CardDrag(false, Point);
            input.Build(1);
            input.CardCancel();
            PlayerInput release = input.Build(2);
            Assert.IsTrue(release.IsPressed(PlayerInput.BtnAimCancel));
            Assert.IsFalse(release.IsPressed(PlayerInput.BtnCard1));
        }

        [Test]
        public void AFingerOnACardAndAFingerOnTheStreetGoOutInTheSameTick()
        {
            MatchInput input = Make();
            input.CardPress(1, Now);
            input.CardDrag(false, new Vec2(3.0, -3.0));
            input.MovePress(Point);
            PlayerInput built = input.Build(1);
            Assert.IsTrue(built.IsPressed(PlayerInput.BtnCard2));
            Assert.IsTrue(built.IsPressed(PlayerInput.BtnMoveTarget));
            Assert.AreEqual(Point, built.MoveTarget);
            Assert.AreEqual(new Vec2(3.0, -3.0), built.AimPoint);
        }

        [Test]
        public void ClearForgetsEverything()
        {
            MatchInput input = Make();
            input.CardPress(0, Now);
            input.MovePress(Point);
            input.SetPrimary(true, Point);
            input.Clear();
            Assert.AreEqual(0, input.Build(1).Buttons);
            Assert.IsFalse(input.CardHeld());
        }

        [Test]
        public void TheStickKeepsItsLengthUpToOne()
        {
            MatchInput input = Make();
            input.Stick = new Vec2(0.3, 0.4);
            Assert.AreEqual(0.5, input.Build(1).Move.Length(), 1e-6, "walk: half a push");
            input.Stick = new Vec2(3.0, 4.0);
            Assert.AreEqual(1.0, input.Build(2).Move.Length(), 1e-6);
        }

        [Test]
        public void APrimaryTapInsideOneTickIsSentOnceWithItsAimPoint()
        {
            MatchInput input = Make();
            input.SetPrimary(true, Point);
            input.SetPrimary(false, new Vec2(2.0, 2.0));
            PlayerInput tap = input.Build(1);
            Assert.IsTrue(tap.IsPressed(PlayerInput.BtnPrimary));
            Assert.AreEqual(new Vec2(2.0, 2.0), tap.AimPoint);
            Assert.IsFalse(input.Build(2).IsPressed(PlayerInput.BtnPrimary));
        }

        [Test]
        public void TheHeroButtonsAreOneTickPressesAndCrouchIsHeld()
        {
            MatchInput input = Make();
            input.PressDash();
            input.PressJump();
            input.PressSlide();
            input.PressUltimate(Point);
            PlayerInput built = input.Build(1);
            Assert.IsTrue(built.IsPressed(PlayerInput.BtnDash));
            Assert.IsTrue(built.IsPressed(PlayerInput.BtnJump));
            Assert.IsTrue(built.IsPressed(PlayerInput.BtnSlide));
            Assert.IsTrue(built.IsPressed(PlayerInput.BtnUltimate));
            Assert.AreEqual(Point, built.AimPoint, "the ultimate lands at the crosshair");
            Assert.AreEqual(0, input.Build(2).Buttons);
            input.SetCrouch(true);
            Assert.IsTrue(input.Build(3).IsPressed(PlayerInput.BtnCrouch));
            Assert.IsTrue(input.Build(4).IsPressed(PlayerInput.BtnCrouch), "held");
            input.SetCrouch(false);
            Assert.IsFalse(input.Build(5).IsPressed(PlayerInput.BtnCrouch));
            input.SetCrouch(true);
            input.SetCrouch(false);
            Assert.IsTrue(input.Build(6).IsPressed(PlayerInput.BtnCrouch), "a quick tap still crouches for a tick");
        }

        [Test]
        public void AThrowIsHeldWithItsAimThenReleasedOrCancelled()
        {
            MatchInput input = Make();
            input.ThrowPress(new Vec2(0.0, -1.0));
            PlayerInput held = input.Build(1);
            Assert.IsTrue(held.IsPressed(PlayerInput.BtnCard1));
            Assert.AreEqual(new Vec2(0.0, -1.0), held.Aim);
            input.ThrowTurn(new Vec2(0.6, -0.8));
            Assert.AreEqual(new Vec2(0.6, -0.8), input.Build(2).Aim);
            input.ThrowRelease(true);
            PlayerInput release = input.Build(3);
            Assert.IsFalse(release.IsPressed(PlayerInput.BtnCard1));
            Assert.IsTrue(release.IsPressed(PlayerInput.BtnAimCancel));
            input.ThrowPress(new Vec2(0.0, -1.0));
            input.ThrowRelease(false);
            Assert.IsTrue(input.Build(4).IsPressed(PlayerInput.BtnCard1), "a throw inside one tick is held once");
            Assert.AreEqual(0, input.Build(5).Buttons & (PlayerInput.BtnCard1 | PlayerInput.BtnAimCancel));
        }

        [Test]
        public void KickRidesTheSecondCardsBitOnce()
        {
            MatchInput input = Make();
            input.KickPress();
            Assert.IsTrue(input.Build(1).IsPressed(PlayerInput.BtnCard2));
            Assert.IsFalse(input.Build(2).IsPressed(PlayerInput.BtnCard2));
        }
    }
}
