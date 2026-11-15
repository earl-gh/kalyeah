using System;
using System.Collections.Generic;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Kit;

namespace Kalyeah.Sim.Tests
{
    /// <summary>
    /// Ports of test_cast_exclusivity.gd (one-skill-at-a-time server arbitration) and
    /// test_no_cast_zones.gd (enemy base & active rescue area casting blocks).
    /// </summary>
    public class CastExclusivityTests
    {
        const int PlayerId = 1;
        const int BothWeapons = PlayerInput.BtnWeapon1 | PlayerInput.BtnWeapon2;

        static MatchSim Sim()
        {
            var sim = new MatchSim(Data.GameRules, Layout, 17);
            PlayerState player = sim.AddPlayer(PlayerId, 0);
            player.Position = Vec2.Zero;
            Assert.IsTrue(sim.SetLoadout(PlayerId, "papel_shield", "langit_lupa"));
            return sim;
        }

        static void SendInput(MatchSim sim, int buttons)
        {
            sim.SetInput(PlayerId, PlayerInput.Create(Vec2.Zero, new Vec2(0.0, -1.0), buttons, sim.Tick));
            sim.Step(DT);
        }

        [Test]
        public void SimultaneousPressAndReleaseCastsOnlyLowerSlot()
        {
            MatchSim sim = Sim();
            PlayerState player = sim.Players[PlayerId];
            SendInput(sim, BothWeapons);
            Assert.AreEqual(0.0, player.AimHold[0]);
            Assert.AreEqual(-1.0, player.AimHold[1], "slot 1 never becomes a second active aim");
            SendInput(sim, 0);
            Assert.Greater(player.WeaponCooldowns[0], 0.0, "deterministic lower slot casts");
            Assert.AreEqual(0.0, player.WeaponCooldowns[1], "dual input cannot cast both weapons");
        }

        [Test]
        public void ReleaseCannotStartOtherSlotInTheSameInput()
        {
            MatchSim sim = Sim();
            PlayerState player = sim.Players[PlayerId];
            SendInput(sim, PlayerInput.BtnWeapon1);
            SendInput(sim, PlayerInput.BtnWeapon2);
            Assert.Greater(player.WeaponCooldowns[0], 0.0, "releasing the active slot completes it");
            Assert.AreEqual(-1.0, player.AimHold[1], "the other fresh press is consumed by arbitration");
            SendInput(sim, PlayerInput.BtnWeapon2);
            SendInput(sim, 0);
            Assert.AreEqual(0.0, player.WeaponCooldowns[1], "holding the rejected press cannot precast");
            SendInput(sim, PlayerInput.BtnWeapon2);
            SendInput(sim, 0);
            Assert.Greater(player.WeaponCooldowns[1], 0.0, "a later fresh press casts normally");
        }

        [Test]
        public void SequentialFreshPressesCanCastEachSlotRapidly()
        {
            MatchSim sim = Sim();
            PlayerState player = sim.Players[PlayerId];
            SendInput(sim, PlayerInput.BtnWeapon1);
            SendInput(sim, 0);
            SendInput(sim, PlayerInput.BtnWeapon2);
            SendInput(sim, 0);
            Assert.Greater(player.WeaponCooldowns[0], 0.0);
            Assert.Greater(player.WeaponCooldowns[1], 0.0);
        }

        [Test]
        public void CancelDoesNotHandoffToASimultaneousOtherPress()
        {
            MatchSim sim = Sim();
            PlayerState player = sim.Players[PlayerId];
            SendInput(sim, PlayerInput.BtnWeapon1);
            SendInput(sim, PlayerInput.BtnWeapon2 | PlayerInput.BtnAimCancel);
            CollectionAssert.AreEqual(new[] { 0.0, 0.0 }, player.WeaponCooldowns);
            CollectionAssert.AreEqual(new[] { -1.0, -1.0 }, player.AimHold);
            SendInput(sim, PlayerInput.BtnWeapon2);
            SendInput(sim, 0);
            Assert.AreEqual(0.0, player.WeaponCooldowns[1], "rejected held input needs release + fresh press");
        }

        [Test]
        public void SimultaneousPressOnCooldownDoesNotPrecastOtherSlot()
        {
            MatchSim sim = Sim();
            PlayerState player = sim.Players[PlayerId];
            player.WeaponCooldowns[0] = DT * 2.0;
            SendInput(sim, BothWeapons);
            CollectionAssert.AreEqual(new[] { -1.0, -1.0 }, player.AimHold);
            SendInput(sim, BothWeapons);
            Assert.AreEqual(0.0, player.WeaponCooldowns[0]);
            CollectionAssert.AreEqual(new[] { -1.0, -1.0 }, player.AimHold, "held buttons do not start when ready");
            SendInput(sim, 0);
            CollectionAssert.AreEqual(new[] { 0.0, 0.0 }, player.WeaponCooldowns);
        }

        [Test]
        public void HardCcClearsActiveAimAndRequiresFreshPress()
        {
            MatchSim sim = Sim();
            PlayerState player = sim.Players[PlayerId];
            SendInput(sim, PlayerInput.BtnWeapon1);
            sim.ApplyEffect(PlayerId, StatusEffects.Type.Stun, DT * 2.0, 0.0);
            SendInput(sim, PlayerInput.BtnWeapon1);
            Assert.AreEqual(-1.0, player.AimHold[0]);
            SendInput(sim, PlayerInput.BtnWeapon1);
            SendInput(sim, 0);
            Assert.AreEqual(0.0, player.WeaponCooldowns[0], "holding through crowd control never restarts aim");
            SendInput(sim, PlayerInput.BtnWeapon1);
            SendInput(sim, 0);
            Assert.Greater(player.WeaponCooldowns[0], 0.0);
        }

        [Test]
        public void TagClearsActiveAimAndHeldInputCannotCastAfterRescue()
        {
            MatchSim sim = Sim();
            PlayerState player = sim.Players[PlayerId];
            SendInput(sim, PlayerInput.BtnWeapon1);
            sim.Kill(PlayerId);
            CollectionAssert.AreEqual(new[] { -1.0, -1.0 }, player.AimHold);
            sim.RescueReadyLeft = new[] { 0.0, 0.0 };
            PlayerState rescuer = sim.AddPlayer(2, 0);
            rescuer.Position = sim.Layout.RescueRect(sim.SideForTeam(0)).GetCenter();
            int ticks = GMath.Ceili((sim.Rules.RescueHoldTime + 0.3) / DT);
            for (int i = 0; i < ticks; i++)
                SendInput(sim, PlayerInput.BtnWeapon1);
            Assert.IsFalse(player.Tagged, "teammate rescues player");
            SendInput(sim, PlayerInput.BtnWeapon1);
            SendInput(sim, 0);
            Assert.AreEqual(0.0, player.WeaponCooldowns[0], "press held through tag and rescue is not fresh");
        }
    }

    public class NoCastZonesTests
    {
        GameRules _rules;

        [SetUp]
        public void SetUp()
        {
            _rules = Rules();
            _rules.CardCycle = true;
            _rules.ElixirEnabled = true;
        }

        MatchSim Sim()
        {
            var sim = new MatchSim(_rules, Layout, 5);
            sim.AddPlayer(1, 0);
            sim.AddPlayer(2, 0);
            sim.AddPlayer(3, 1);
            sim.Players[1].Position = new Vec2(0.0, 5.0);
            sim.Players[2].Position = new Vec2(8.0, 10.0);
            sim.Players[3].Position = new Vec2(0.0, -8.0);
            foreach (int id in new[] { 1, 2, 3 })
            {
                sim.DealCards(id);
                sim.Players[id].Elixir = _rules.ElixirMax;
            }
            sim.Players[1].Weapons[0] = "papel_trap";
            return sim;
        }

        static void Tap(MatchSim sim)
        {
            Tick(sim, 1, Vec2.Zero, PlayerInput.BtnCard1);
            Tick(sim, 1, Vec2.Zero, 0);
        }

        static void InEnemyBase(MatchSim sim) =>
            sim.Players[1].Position = Layout.BaseCenter(-sim.SideForTeam(0)) + new Vec2(1.0, 0.0);

        static void InRescueArea(MatchSim sim) =>
            sim.Players[1].Position = Layout.RescueRect(sim.SideForTeam(0)).GetCenter();

        [Test]
        public void ACardCastsInTheOpenStreet()
        {
            MatchSim sim = Sim();
            var casts = new List<(int, string)>();
            sim.WeaponCast += (id, w) => casts.Add((id, w));
            Tap(sim);
            CollectionAssert.AreEqual(new[] { (1, "papel_trap") }, casts);
            Assert.IsFalse(sim.CastingBlocked(sim.Players[1]));
        }

        [Test]
        public void KidStandingInEnemyBaseCannotCastAndPaysNothing()
        {
            MatchSim sim = Sim();
            InEnemyBase(sim);
            var casts = new List<string>();
            sim.WeaponCast += (_, w) => casts.Add(w);
            double before = sim.Players[1].Elixir;
            Assert.IsTrue(sim.CastingBlocked(sim.Players[1]));
            Assert.IsFalse(sim.WeaponReady(sim.Players[1], 0));
            Tap(sim);
            CollectionAssert.IsEmpty(casts);
            Assert.AreEqual(Math.Min(_rules.ElixirMax, before), sim.Players[1].Elixir, 0.001);
        }

        [Test]
        public void LeavingEnemyBaseRestoresCastAndHomeBaseAllowsDefense()
        {
            MatchSim sim = Sim();
            InEnemyBase(sim);
            sim.Step(DT);
            sim.Players[1].Position = new Vec2(0.0, 5.0);
            var casts = new List<string>();
            sim.WeaponCast += (_, w) => casts.Add(w);
            Tap(sim);
            Assert.AreEqual(1, casts.Count);

            sim.Players[1].Position = Layout.BaseCenter(sim.SideForTeam(0)) + new Vec2(1.0, 0.0);
            Assert.IsFalse(sim.CastingBlocked(sim.Players[1]), "defender at home can cast in own base");
        }

        [Test]
        public void RescueAreaBlocksRescuingAllyOnlyWhileTeammateWaitsAndNeverBlocksEnemyHunter()
        {
            MatchSim sim = Sim();
            InRescueArea(sim);
            Assert.IsFalse(sim.CastingBlocked(sim.Players[1]), "nobody tagged: rescue patio is normal ground");
            var casts = new List<string>();
            sim.WeaponCast += (_, w) => casts.Add(w);
            Tap(sim);
            Assert.AreEqual(1, casts.Count);

            casts.Clear();
            sim.Players[1].Weapons[0] = "papel_trap";
            sim.Players[1].Elixir = _rules.ElixirMax;
            sim.Kill(2);
            InRescueArea(sim);
            Assert.IsTrue(sim.CastingBlocked(sim.Players[1]), "teammate waiting: rescuer cannot cast");
            Tap(sim);
            CollectionAssert.IsEmpty(casts);

            sim.Players[3].Position = Layout.RescueRect(sim.SideForTeam(0)).GetCenter();
            Assert.IsFalse(sim.CastingBlocked(sim.Players[3]), "enemy hunting rescuer is not blocked");
        }

        [Test]
        public void CardHeldWhenWalkingIntoEnemyBaseIsDroppedAndTogglesDisableNoCastZones()
        {
            MatchSim sim = Sim();
            Tick(sim, 1, Vec2.Zero, PlayerInput.BtnCard1);
            Assert.GreaterOrEqual(sim.Players[1].AimHold[0], 0.0);
            InEnemyBase(sim);
            var casts = new List<string>();
            sim.WeaponCast += (_, w) => casts.Add(w);
            Tick(sim, 1, Vec2.Zero, PlayerInput.BtnCard1);
            Assert.Less(sim.Players[1].AimHold[0], 0.0, "entering enemy base drops held aim");
            Tick(sim, 1, Vec2.Zero, 0);
            CollectionAssert.IsEmpty(casts);

            _rules.NoCastInEnemyBase = false;
            _rules.NoCastInRescueArea = false;
            MatchSim unlocked = Sim();
            unlocked.Kill(2);
            InEnemyBase(unlocked);
            Assert.IsFalse(unlocked.CastingBlocked(unlocked.Players[1]));
            InRescueArea(unlocked);
            Assert.IsFalse(unlocked.CastingBlocked(unlocked.Players[1]));
        }
    }
}
