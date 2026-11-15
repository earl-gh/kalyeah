using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Kit;

namespace Kalyeah.Sim.Tests
{
    /// <summary>Port of test_cards.gd (the cooldown shuffler, elixir off, hand of two).</summary>
    public class CardCycleTests
    {
        GameRules _rules;

        [SetUp]
        public void SetUp()
        {
            _rules = Rules();
            _rules.CardCycle = true;
            _rules.ElixirEnabled = false;
            _rules.HandSize = 2;
        }

        MatchSim Sim(long seed = 5)
        {
            var sim = new MatchSim(_rules, Layout, seed);
            sim.AddPlayer(1, 0);
            sim.AddPlayer(2, 1);
            sim.Players[2].Position = new Vec2(100.0, 100.0);
            sim.DealCards(1);
            sim.DealCards(2);
            return sim;
        }

        static void Use(MatchSim sim, int slot)
        {
            Tick(sim, 1, Vec2.Zero, PlayerInput.CardBits[slot]);
            Tick(sim, 1, Vec2.Zero, 0);
        }

        static List<string> All(PlayerState s)
        {
            var found = s.Weapons.Where(w => w != "").ToList();
            if (s.NextCard != "") found.Add(s.NextCard);
            if (s.ShuffleCard != "") found.Add(s.ShuffleCard);
            found.AddRange(s.Incoming);
            found.AddRange(s.Deck);
            return found;
        }

        [Test]
        public void EveryPlayerHoldsTwoCardsOneWaitsAndTheRestAreTheDeck()
        {
            PlayerState s = Sim().Players[1];
            Assert.AreEqual(2, s.Weapons.Count);
            Assert.AreNotEqual("", s.NextCard);
            Assert.AreEqual(_rules.Weapons.Count - 3, s.Deck.Count);
            Assert.AreEqual(_rules.Weapons.Count, All(s).Distinct().Count());
        }

        [Test]
        public void TheSameSeedDealsTheSameDecks()
        {
            CollectionAssert.AreEqual(Sim(7).Players[1].Weapons, Sim(7).Players[1].Weapons);
            CollectionAssert.AreEqual(Sim(7).Players[1].Deck, Sim(7).Players[1].Deck);
            CollectionAssert.AreNotEqual(Sim(7).Players[1].Deck, Sim(8).Players[1].Deck);
        }

        [Test]
        public void UsingACardDropsTheNextCardInAtOnceAndTheUsedOneGoesToTheShuffler()
        {
            MatchSim sim = Sim();
            PlayerState s = sim.Players[1];
            string used = s.Weapons[0], next = s.NextCard, other = s.Weapons[1];
            Use(sim, 0);
            Assert.AreEqual(next, s.Weapons[0]);
            Assert.AreEqual(other, s.Weapons[1]);
            Assert.AreEqual(used, s.ShuffleCard);
            Assert.AreEqual("", s.NextCard);
            CollectionAssert.AreEqual(new[] { 0.0, 0.0 }, s.WeaponCooldowns);
            Assert.IsTrue(sim.WeaponReady(s, 0));
            Assert.IsTrue(sim.WeaponReady(s, 1));
        }

        [Test]
        public void TheShufflerPlaysTheUsedCardsCooldownThenShowsANewNextCard()
        {
            MatchSim sim = Sim();
            PlayerState s = sim.Players[1];
            string used = s.Weapons[0], front = s.Deck[0];
            Use(sim, 0);
            Assert.AreEqual(sim.WeaponDefs[used].Cooldown, s.ShuffleTotal, 0.001);
            Steps(sim, GMath.Ceili((sim.WeaponDefs[used].Cooldown - 0.5) / DT));
            Assert.AreEqual("", s.NextCard);
            Steps(sim, GMath.Ceili(1.0 / DT));
            Assert.AreEqual(front, s.NextCard);
            Assert.AreEqual("", s.ShuffleCard);
            Assert.AreEqual(used, s.Deck.Last());
        }

        [Test]
        public void UsingTheSecondCardWhileTheShufflerWorksLeavesASlotEmpty()
        {
            MatchSim sim = Sim();
            PlayerState s = sim.Players[1];
            string first = s.Weapons[0], second = s.Weapons[1], front = s.Deck[0];
            Use(sim, 0);
            Use(sim, 1);
            Assert.AreEqual("", s.Weapons[1]);
            Assert.IsFalse(sim.WeaponReady(s, 1));
            CollectionAssert.AreEqual(new[] { second }, s.Incoming);
            Steps(sim, GMath.Ceili((sim.WeaponDefs[first].Cooldown + 0.5) / DT));
            Assert.AreEqual(front, s.Weapons[1]);
            Assert.AreEqual(second, s.ShuffleCard);
        }

        [Test]
        public void ANewRoundEmptiesTheShufflerAndFillsTheHand()
        {
            MatchSim sim = Sim();
            PlayerState s = sim.Players[1];
            Use(sim, 0);
            Use(sim, 1);
            Assert.AreEqual("", s.Weapons[1]);
            sim.Objectives.ResetRound(sim);
            Assert.AreEqual("", s.ShuffleCard);
            CollectionAssert.IsEmpty(s.Incoming);
            Assert.AreNotEqual("", s.Weapons[0]);
            Assert.AreNotEqual("", s.Weapons[1]);
            Assert.AreNotEqual("", s.NextCard);
            Assert.AreEqual(_rules.Weapons.Count, All(s).Count);
        }

        [Test]
        public void ThereIsNoSwapWithCards()
        {
            MatchSim sim = Sim();
            sim.Kill(1);
            Assert.IsFalse(sim.SwapLoadout(1, "bato_light", "lata"));
        }

        [Test]
        public void WithoutCardsNothingCycles()
        {
            _rules.CardCycle = false;
            var sim = new MatchSim(_rules, Layout, 5);
            sim.AddPlayer(1, 0);
            sim.SetLoadout(1, "bato_light", "papel_shield");
            sim.CycleUsed(sim.Players[1], 0);
            CollectionAssert.AreEqual(new[] { "bato_light", "papel_shield" }, sim.Players[1].Weapons);
            Assert.IsFalse(sim.Players[1].Cards);
        }
    }

    /// <summary>Port of test_elixir.gd.</summary>
    public class ElixirTests
    {
        static readonly Dictionary<string, int> Costs = new Dictionary<string, int>
        {
            { "bato_light", 2 }, { "bato_heavy", 4 }, { "gunting_light", 2 }, { "gunting_heavy", 3 }, { "papel_trap", 3 }, { "papel_shield", 4 },
            { "tsinelas_light", 3 }, { "tsinelas_heavy", 4 }, { "lata", 5 }, { "jacks", 4 }, { "bola", 4 }, { "trumpo", 5 }, { "heal", 4 },
            { "langit_lupa", 5 },
        };

        GameRules _rules;
        int _casts;

        [SetUp]
        public void SetUp()
        {
            _rules = Rules();
            _rules.CardCycle = true;
            _rules.HandSize = 4;
            _rules.ElixirEnabled = true;
            _casts = 0;
        }

        MatchSim Sim()
        {
            var sim = new MatchSim(_rules, OpenLayout(), 5);
            sim.AddPlayer(1, 0);
            sim.AddPlayer(2, 1);
            sim.Players[1].Position = new Vec2(0.0, 5.0);
            sim.Players[2].Position = new Vec2(0.0, -5.0);
            sim.DealCards(1);
            sim.DealCards(2);
            sim.WeaponCast += (id, w) => _casts++;
            return sim;
        }

        static void Hold(MatchSim sim, string weapon)
        {
            PlayerState s = sim.Players[1];
            int at = s.Weapons.IndexOf(weapon);
            if (at >= 0) s.Weapons[at] = s.Weapons[0];
            else if (s.NextCard == weapon) s.NextCard = s.Weapons[0];
            else s.Deck[s.Deck.IndexOf(weapon)] = s.Weapons[0];
            s.Weapons[0] = weapon;
        }

        static void Tap(MatchSim sim, int releaseBits = 0)
        {
            Tick(sim, 1, Vec2.Zero, PlayerInput.BtnCard1);
            Tick(sim, 1, Vec2.Zero, releaseBits);
        }

        [Test]
        public void EveryWeaponCostsItsGddElixir()
        {
            Assert.AreEqual(Costs.Count, Data.GameRules.Weapons.Count);
            foreach (WeaponDef def in Data.GameRules.Weapons)
            {
                Assert.That(def.ElixirCost, Is.InRange(1, (int)Data.GameRules.ElixirMax));
                Assert.AreEqual(Costs[def.Id], def.ElixirCost, def.Id);
            }
        }

        [Test]
        public void APlayerStartsWithFiveAndFillsAtTheRulesRateUpToTen()
        {
            MatchSim sim = Sim();
            Assert.AreEqual(5.0, sim.Players[1].Elixir, 1e-4);
            Steps(sim, 30);
            Assert.AreEqual(5.0 + _rules.ElixirRegenPerS, sim.Players[1].Elixir, 0.001);
            Steps(sim, 30 * 20);
            Assert.AreEqual(10.0, sim.Players[1].Elixir, 1e-4);
        }

        [Test]
        public void ElixirDoesNotFillDuringTheOpeningShow()
        {
            MatchSim sim = Sim();
            sim.BeginIntro();
            Steps(sim, 30);
            Assert.AreEqual(5.0, sim.Players[1].Elixir, 1e-4);
        }

        [Test]
        public void ACastSpendsTheCardsCost()
        {
            MatchSim sim = Sim();
            Hold(sim, "lata");
            sim.Players[1].Elixir = 6.0;
            Tap(sim);
            Assert.AreEqual(1.0 + _rules.ElixirRegenPerS * 2.0 * DT, sim.Players[1].Elixir, 0.001);
        }

        [Test]
        public void ACardAboveTheBarCannotBePickedUpOrCast()
        {
            MatchSim sim = Sim();
            Hold(sim, "lata");
            PlayerState s = sim.Players[1];
            s.Elixir = 4.0;
            Tick(sim, 1, Vec2.Zero, PlayerInput.BtnCard1);
            Assert.Less(s.AimHold[0], 0.0);
            Assert.IsFalse(sim.CanAfford(s, 0));
            Tick(sim, 1, Vec2.Zero, 0);
            Assert.AreEqual(0, _casts);
            Assert.AreEqual("lata", s.Weapons[0]);
        }

        [Test]
        public void HoldingAnUnaffordableCardWhileTheBarFillsCastsNothing()
        {
            MatchSim sim = Sim();
            Hold(sim, "lata");
            PlayerState s = sim.Players[1];
            s.Elixir = 4.9;
            for (int i = 0; i < 30; i++) Tick(sim, 1, Vec2.Zero, PlayerInput.BtnCard1);
            Assert.Greater(s.Elixir, 5.0);
            Tick(sim, 1, Vec2.Zero, 0);
            Assert.AreEqual(0, _casts);
        }

        [Test]
        public void ACancelSpendsNothing()
        {
            MatchSim sim = Sim();
            Hold(sim, "lata");
            Tap(sim, PlayerInput.BtnAimCancel);
            Assert.AreEqual(0, _casts);
            Assert.AreEqual(5.0 + 2.0 * _rules.ElixirRegenPerS / 30.0, sim.Players[1].Elixir, 1e-4);
            Assert.AreEqual("lata", sim.Players[1].Weapons[0]);
        }

        [Test]
        public void AReleaseThatCastsNothingSpendsNothing()
        {
            MatchSim sim = Sim();
            Hold(sim, "bato_light");
            sim.Players[2].Position = new Vec2(0.0, -20.0);
            Tap(sim);
            Assert.AreEqual(0, _casts);
            Assert.AreEqual(5.0 + 2.0 * _rules.ElixirRegenPerS / 30.0, sim.Players[1].Elixir, 1e-4);
            Assert.AreEqual("bato_light", sim.Players[1].Weapons[0]);
        }

        [Test]
        public void WithElixirNoCardPlaysACooldownAndTheUsedCardGoesToTheBackOfTheDeck()
        {
            MatchSim sim = Sim();
            Hold(sim, "bato_heavy");
            string next = sim.Players[1].NextCard;
            Tap(sim);
            Assert.AreEqual(1, _casts);
            CollectionAssert.AreEqual(new[] { 0.0, 0.0, 0.0, 0.0 }, sim.Players[1].WeaponCooldowns);
            Assert.AreEqual("", sim.Players[1].ShuffleCard);
            Assert.AreEqual(next, sim.Players[1].Weapons[0]);
            Assert.AreEqual("bato_heavy", sim.Players[1].Deck.Last());
        }

        [Test]
        public void ElixirKeepsFillingWhileTaggedAndARescueKeepsIt()
        {
            MatchSim sim = Sim();
            sim.AddPlayer(3, 0);
            sim.DealCards(3);
            sim.Players[1].Elixir = 2.0;
            sim.Kill(1);
            Steps(sim, 30);
            double filled = 2.0 + _rules.ElixirRegenPerS;
            Assert.AreEqual(filled, sim.Players[1].Elixir, 0.001);
            sim.Objectives.Rescue(sim, sim.Players[1], 3);
            Assert.AreEqual(filled, sim.Players[1].Elixir, 0.001);
        }

        static void PlayAPoint(MatchSim sim)
        {
            sim.Kill(2);
            for (int i = 0; i < 30 * 18; i++)
            {
                sim.Step(DT);
                if (sim.Phase == MatchSim.MatchPhase.Playing && sim.Score.Points[0] > 0) return;
            }
        }

        [Test]
        public void ANewRoundStartsEveryoneAtFiveElixirUnlessTheResetIsOff()
        {
            MatchSim sim = Sim();
            sim.Players[1].Elixir = 9.0;
            PlayAPoint(sim);
            Assert.AreEqual(1, sim.Score.Points[0]);
            Assert.AreEqual(5.0, sim.Players[1].Elixir, 0.05);
            _rules.ElixirResetOnRound = false;
            MatchSim keep = Sim();
            keep.Players[1].Elixir = 9.0;
            PlayAPoint(keep);
            Assert.Greater(keep.Players[1].Elixir, 9.0);
        }
    }

    /// <summary>Port of test_stamina.gd.</summary>
    public class StaminaTests
    {
        GameRules _rules;

        [SetUp]
        public void SetUp()
        {
            _rules = Rules();
            _rules.StaminaEnabled = true;
            _rules.SprintEnabled = false;
            _rules.TripChance = 0.0;
        }

        MatchSim Sim()
        {
            var sim = new MatchSim(_rules, Layout, 5);
            sim.AddPlayer(1, 0);
            sim.AddPlayer(2, 1);
            sim.Players[1].Position = new Vec2(-12.0, 6.2);
            sim.Players[2].Position = new Vec2(12.0, -6.2);
            sim.Equip(1);
            sim.Equip(2);
            return sim;
        }

        static void Walk(MatchSim sim, int ticks)
        {
            for (int i = 0; i < ticks; i++)
                Tick(sim, 1, new Vec2((i / 60) % 2 == 0 ? 1.0 : -1.0, 0.0), 0);
        }

        static void Stand(MatchSim sim, int ticks) => Run(sim, 1, Vec2.Zero, ticks);

        static void Exhaust(MatchSim sim)
        {
            sim.Players[1].Stamina = 1.0;
            Walk(sim, 4);
        }

        [Test]
        public void MovingDrainsTenASecond()
        {
            MatchSim sim = Sim();
            Assert.AreEqual(_rules.StaminaMax, sim.Players[1].Stamina);
            Walk(sim, 30);
            Assert.AreEqual(90.0, sim.Players[1].Stamina, 0.001);
        }

        [Test]
        public void WalkingToAMoveTargetDrainsToo()
        {
            MatchSim sim = Sim();
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, sim.Tick).WithMoveTarget(new Vec2(12.0, 6.2)));
            sim.Step(DT);
            Stand(sim, 29);
            Assert.AreEqual(90.0, sim.Players[1].Stamina, 0.001);
        }

        [Test]
        public void StandingStillRefillsAfterHalfASecond()
        {
            MatchSim sim = Sim();
            sim.Players[1].Stamina = 40.0;
            Stand(sim, 15);
            Assert.AreEqual(40.0, sim.Players[1].Stamina, 0.001);
            Stand(sim, 30);
            Assert.AreEqual(65.0, sim.Players[1].Stamina, 0.001);
            Stand(sim, 90);
            Assert.AreEqual(_rules.StaminaMax, sim.Players[1].Stamina);
        }

        [Test]
        public void AnyMovementRestartsTheWait()
        {
            MatchSim sim = Sim();
            sim.Players[1].Stamina = 40.0;
            for (int i = 0; i < 20; i++)
            {
                Stand(sim, 14);
                Walk(sim, 1);
            }
            Assert.Less(sim.Players[1].Stamina, 40.0);
        }

        [Test]
        public void AtZeroTheKidIsOverclockedAndWalksAtHalfSpeed()
        {
            MatchSim sim = Sim();
            var exhausted = new List<int>();
            sim.Exhausted += id => exhausted.Add(id);
            sim.Players[1].Stamina = 1.0;
            sim.SetInput(1, PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, sim.Tick).WithMoveTarget(new Vec2(12.0, 6.2)));
            Steps(sim, 5);
            PlayerState s = sim.Players[1];
            CollectionAssert.AreEqual(new[] { 1 }, exhausted);
            Assert.IsTrue(s.Overclocked);
            Assert.IsTrue(s.HasMoveTarget);
            Vec2 from = s.Position;
            sim.Step(DT);
            Assert.AreEqual(_rules.MoveSpeed * _rules.OverclockSpeedShare * DT, s.Position.DistanceTo(from), 0.001);
        }

        [Test]
        public void TheBarBlinksForTwoSecondsBeforeItRefills()
        {
            MatchSim sim = Sim();
            Exhaust(sim);
            PlayerState s = sim.Players[1];
            Assert.AreEqual(_rules.ExhaustSeconds, s.ExhaustLeft, 2.0 * DT);
            Walk(sim, GMath.Roundi(_rules.ExhaustSeconds / DT) - 3);
            Assert.AreEqual(0.0, s.Stamina);
            Assert.IsTrue(s.Overclocked);
            Assert.Greater(s.ExhaustLeft, 0.0);
            Walk(sim, 6);
            Assert.AreEqual(0.0, s.ExhaustLeft);
            Assert.Greater(s.Stamina, 0.0);
            Assert.IsTrue(s.Overclocked);
        }

        [Test]
        public void TheKidWalksUntilTheBarIsFullAgain()
        {
            MatchSim sim = Sim();
            Exhaust(sim);
            PlayerState s = sim.Players[1];
            int ticks = 0;
            while (s.Overclocked && ticks < 600)
            {
                Walk(sim, 1);
                ticks++;
            }
            Assert.IsFalse(s.Overclocked);
            Assert.AreEqual(_rules.StaminaMax, s.Stamina);
            Assert.AreEqual(_rules.ExhaustSeconds + _rules.StaminaMax / _rules.StaminaRegenPerS, ticks * DT, 0.3);
        }

        [Test]
        public void AnOverclockedKidCanStillCastButAStunStopsIt()
        {
            MatchSim sim = Sim();
            Exhaust(sim);
            PlayerState s = sim.Players[1];
            s.Weapons[0] = "papel_trap";
            var casts = new List<string>();
            sim.WeaponCast += (id, w) => casts.Add(w);
            Tick(sim, 1, Vec2.Zero, PlayerInput.BtnCard1);
            Tick(sim, 1, Vec2.Zero, 0);
            Assert.IsTrue(s.Overclocked);
            CollectionAssert.AreEqual(new[] { "papel_trap" }, casts);
            s.Effects.ImmunityLength = 0.0;
            sim.ApplyEffect(1, StatusEffects.Type.Stun, 3.0, 0.0);
            Vec2 at = s.Position;
            Walk(sim, 10);
            Assert.AreEqual(at, s.Position);
        }

        [Test]
        public void ARescueRefillsTheBar()
        {
            MatchSim sim = Sim();
            sim.AddPlayer(3, 0);
            sim.Equip(3);
            Exhaust(sim);
            sim.Kill(1);
            sim.Objectives.Rescue(sim, sim.Players[1], 3);
            Assert.AreEqual(_rules.StaminaMax, sim.Players[1].Stamina);
            Assert.AreEqual(0.0, sim.Players[1].ExhaustLeft);
            Assert.IsFalse(sim.Players[1].Overclocked);
        }

        [Test]
        public void TrainingDummiesHaveNoStamina()
        {
            var sim = new MatchSim(_rules, Layout, 5);
            sim.AddDummy(9, 1, new Vec2(5.0, 0.0), DummyBrain.Patrolling(5.0, 3.0, 1.0));
            Steps(sim, 30 * 30);
            Assert.IsFalse(sim.Players[9].HasStamina);
            Assert.AreEqual(_rules.StaminaMax, sim.Players[9].Stamina);
        }

        [Test]
        public void WithStaminaOffNothingDrains()
        {
            _rules.StaminaEnabled = false;
            MatchSim sim = Sim();
            Walk(sim, 30 * 12);
            Assert.AreEqual(_rules.StaminaMax, sim.Players[1].Stamina);
        }
    }
}
