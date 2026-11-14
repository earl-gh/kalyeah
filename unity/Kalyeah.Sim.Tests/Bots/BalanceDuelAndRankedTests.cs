using System.Collections.Generic;
using NUnit.Framework;

namespace Kalyeah.Sim.Tests.Bots
{
    /// <summary>Port of tests/unit/test_balance_duel.gd: the bot-versus-bot duel behind the balance numbers.</summary>
    public class BalanceDuelTests
    {
        static readonly string[] Hurts = { "bato_light", "tsinelas_light" };
        static readonly string[] Harmless = { "papel_shield", "papel_trap" };
        static GameRules Rules => Kit.Data.GameRules;

        [Test]
        public void EveryTwoDifferentWeaponsMakeOneLoadout()
        {
            int count = Rules.Weapons.Count;
            List<string[]> loadouts = BalanceDuel.AllLoadouts(Rules);
            Assert.AreEqual(count * (count - 1) / 2, loadouts.Count);
            var seen = new HashSet<string>();
            foreach (string[] pair in loadouts)
            {
                Assert.AreNotEqual(pair[0], pair[1]);
                Assert.IsTrue(seen.Add(pair[0] + "+" + pair[1]), "no loadout twice");
            }
        }

        [Test]
        public void TheArenaHasNoCoverAndLeavesTheRealStreetAlone()
        {
            MapLayout walled = Kit.Layout.Duplicate();
            walled.Obstacles = new List<Rect2> { new Rect2(-13.5, 10.5, 3.0, 0.5) };
            MapLayout open = BalanceDuel.Arena(walled);
            Assert.IsEmpty(open.Obstacles);
            Assert.IsNotEmpty(walled.Obstacles, "the street it was made from keeps its cover");
            Assert.AreEqual(Kit.Layout.LaneLength, open.LaneLength);
        }

        [Test]
        public void ADuelIsTheSameEveryTimeForOneSeed()
        {
            BalanceDuel.Result first = BalanceDuel.Run(Rules, Kit.Layout, Hurts, new[] { "gunting_heavy", "jacks" }, 5, 30.0);
            BalanceDuel.Result second = BalanceDuel.Run(Rules, Kit.Layout, Hurts, new[] { "gunting_heavy", "jacks" }, 5, 30.0);
            Assert.AreEqual(first.Winner, second.Winner);
            Assert.AreEqual(first.Seconds, second.Seconds);
            CollectionAssert.AreEqual(first.Hp, second.Hp);
        }

        [Test]
        public void ALoadoutThatHurtsBeatsOneThatCannot()
        {
            BalanceDuel.Result result = BalanceDuel.Run(Rules, Kit.Layout, Hurts, Harmless, 1);
            Assert.AreEqual(0, result.Winner);
            Assert.Greater(result.Hp[0], 0);
            Assert.AreEqual(0, result.Hp[1], "the other is the one tagged");
        }

        [Test]
        public void TheWinnerIsTheSameLoadoutWithTheSidesSwapped()
        {
            Assert.AreEqual(1, BalanceDuel.Run(Rules, Kit.Layout, Harmless, Hurts, 1).Winner, "now the second team's loadout wins");
        }

        [Test]
        public void TwoLoadoutsThatCannotHurtDrawAtTheTimeLimit()
        {
            GameRules passive = Kit.Rules();
            passive.PrimaryWeapon = null;
            passive.UltimateWeapon = null;
            BalanceDuel.Result result = BalanceDuel.Run(passive, Kit.Layout, Harmless, new[] { "langit_lupa", "papel_shield" }, 1, 8.0);
            Assert.AreEqual(BalanceDuel.NoWinner, result.Winner);
            Assert.AreEqual(8.0, result.Seconds, 0.1);
            CollectionAssert.AreEqual(new[] { Rules.PlayerMaxHp, Rules.PlayerMaxHp }, result.Hp);
        }

        [Test]
        public void ADecidedDuelEndsWhenSomeoneIsTagged()
        {
            Assert.Less(BalanceDuel.Run(Rules, Kit.Layout, Hurts, Harmless, 2).Seconds, BalanceDuel.MaxSeconds);
        }
    }

    /// <summary>Port of the RankedSystem part of tests/unit/test_barangay_5v5.gd (seven tiers, LP maths, the 0 LP floor, the saved record).</summary>
    public class RankedSystemTests
    {
        [Test]
        public void SevenTagalogTiers()
        {
            Assert.AreEqual(7, RankedSystem.Tiers.Count);
            var names = new Dictionary<int, string>
            {
                [0] = "Kahoy", [199] = "Kahoy", [200] = "Bakal", [399] = "Bakal", [400] = "Tanso", [599] = "Tanso", [600] = "Pilak",
                [849] = "Pilak", [850] = "Ginto", [1149] = "Ginto", [1150] = "Brilyante", [1499] = "Brilyante", [1500] = "Batang Kalyeah",
                [2500] = "Batang Kalyeah",
            };
            foreach (var pair in names) Assert.AreEqual(pair.Value, RankedSystem.TierName(pair.Key), $"{pair.Key} LP");
            Assert.AreEqual("ginto", RankedSystem.TierForLp(900).Id);
            Assert.AreEqual(-1, RankedSystem.Tiers[6].MaxLp, "the top tier is uncapped");
            GColor kahoy = RankedSystem.ParseColor(RankedSystem.Tiers[0].Color);
            Assert.AreEqual(0xc8 / 255.0, kahoy.R, 1e-6);
            Assert.AreEqual(0x4b / 255.0, kahoy.B, 1e-6);
        }

        [Test]
        public void LpMathsFloorAndTheSavedRecord()
        {
            var record = new RankedSystem.Record();
            RankedSystem.Outcome floorLoss = RankedSystem.RecordMatch(record, "online", false, 0, 3);
            Assert.AreEqual(0, floorLoss.OldLp);
            Assert.AreEqual(0, floorLoss.NewLp, "floor protection prevents negative LP");
            Assert.IsFalse(floorLoss.Promoted);
            Assert.AreEqual(0, record.Lp);

            RankedSystem.Outcome firstWin = RankedSystem.RecordMatch(record, "online", true, 3, 1);
            Assert.Greater(firstWin.Delta, 0, "an online victory grants LP");
            Assert.AreEqual(firstWin.Delta, firstWin.NewLp);
            Assert.AreEqual(firstWin.NewLp, record.Lp);

            bool promotedSeen = firstWin.Promoted;
            for (int i = 0; i < 15 && record.Lp < 220; i++)
                promotedSeen |= RankedSystem.RecordMatch(record, "online", true, 3, 0).Promoted;
            Assert.GreaterOrEqual(record.Lp, 200, "repeated online wins climb into Bakal");
            Assert.IsTrue(promotedSeen, "crossing 200 LP reports a promotion");
            Assert.AreEqual("Bakal", RankedSystem.TierName(record.Lp));

            int preLoss = record.Lp;
            RankedSystem.Outcome loss = RankedSystem.RecordMatch(record, "online", false, 1, 3);
            Assert.Less(loss.Delta, 0, "an online defeat deducts LP above 0");
            Assert.AreEqual(System.Math.Max(0, preLoss + loss.Delta), loss.NewLp);
            Assert.AreEqual(loss.NewLp, record.Lp);
            Assert.GreaterOrEqual(record.PeakLp, preLoss, "the peak remembers the best");
        }

        [Test]
        public void PracticeClimbsSlowerThanOnline()
        {
            Assert.AreEqual(12 + 3, RankedSystem.PreviewMatch("practice", true, 9, 0, 0).Delta, "margin capped at 3");
            Assert.AreEqual(30 + 10, RankedSystem.PreviewMatch("online", true, 9, 0, 0).Delta, "margin capped at 5, 2 LP a point");
            Assert.AreEqual(-6, RankedSystem.PreviewMatch(" Practice ", false, 0, 1, 100).Delta);
            Assert.AreEqual(-18, RankedSystem.PreviewMatch("hotspot", false, 0, 1, 100).Delta);
            Assert.AreEqual(1.0, RankedSystem.TierProgress(5000));
            Assert.AreEqual(0.5, RankedSystem.TierProgress(100), 1e-12);
        }
    }
}
