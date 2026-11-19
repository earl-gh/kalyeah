using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using static Kalyeah.Sim.Tests.Kit;

namespace Kalyeah.Sim.Tests
{
    /// <summary>
    /// Ports of test_characters.gd, test_barangay_5v5.gd (section 3), test_character_select.gd (sim logic),
    /// and test_ui_round18.gd (weapon naming scheme): exact 10-kid roster, cosmetic-only fairness,
    /// seeded per-team unique AssignCharacters, SetCharacter team swap &amp; TeammatesKids lock,
    /// and all-weapon descriptions &amp; Name / Name · Role display scheme.
    /// </summary>
    public class RosterAndWeaponMetaTests
    {
        static readonly string[] ExpectedKids =
        {
            "narda", "jumong", "inggo",
            "jopay", "oriang", "paeng",
            "kikay", "karding", "manoy",
            "tere", "pamela", "domeng",
        };

        static List<string> Assigned(long seed, int count)
        {
            var sim = new MatchSim(Data.GameRules, Layout, seed);
            for (int i = 0; i < count; i++) sim.AddPlayer(i + 1, i % 2);
            sim.AssignCharacters();
            return sim.Players.Select(p => p.Value.CharacterId).ToList();
        }

        [Test]
        public void ExactTwelveKidsOrderDisplayNamesAndCastReleaseTimings()
        {
            GameRules rules = Data.GameRules;
            Assert.AreEqual(ExpectedKids.Length, rules.Characters.Count);
            var ids = new List<string>();
            foreach (CharacterDef def in rules.Characters)
            {
                Assert.IsNotNull(def);
                ids.Add(def.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.DisplayName), $"{def.Id} has DisplayName");
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.Description), $"{def.Id} has Description");
                Assert.Greater(def.CastReleaseStanding, 0.0, $"{def.Id} standing cast release");
                Assert.Greater(def.CastReleaseMoving, 0.0, $"{def.Id} moving cast release");
            }
            CollectionAssert.AreEqual(ExpectedKids, ids);
        }

        [Test]
        public void AssignCharactersGivesDistinctKidsPerTeamIsSeededAndPurelyCosmetic()
        {
            GameRules rules = Data.GameRules;
            var sim = new MatchSim(rules, Layout, 505);
            for (int i = 0; i < 5; i++)
            {
                sim.AddPlayer(i + 1, 0);
                sim.AddPlayer(i + 6, 1);
            }
            sim.AssignCharacters();
            Assert.AreEqual(10, sim.Players.Count);

            var team0 = new HashSet<string>();
            var team1 = new HashSet<string>();
            foreach (var pair in sim.Players)
            {
                PlayerState p = pair.Value;
                Assert.AreNotEqual("", p.CharacterId);
                Assert.AreEqual(rules.PlayerMaxHp, p.Hp, "cosmetic fairness: identical max HP");
                Assert.AreEqual(rules.PlayerRadius, p.Radius, 1e-4, "cosmetic fairness: identical hitbox radius");
                if (p.Team == 0) Assert.IsTrue(team0.Add(p.CharacterId), $"team 0 duplicate {p.CharacterId}");
                else Assert.IsTrue(team1.Add(p.CharacterId), $"team 1 duplicate {p.CharacterId}");
            }
            Assert.AreEqual(5, team0.Count);
            Assert.AreEqual(5, team1.Count);

            for (int seed = 0; seed < 20; seed++)
            {
                var five = new MatchSim(rules, Layout, seed);
                for (int id = 1; id <= 5; id++) five.AddPlayer(id, 0);
                five.AssignCharacters();
                List<string> picked = five.Players.Select(p => p.Value.CharacterId).ToList();
                Assert.AreEqual(5, picked.Distinct().Count(), $"seed {seed} has 5 distinct allies");
            }

            CollectionAssert.AreEqual(Assigned(42, 10), Assigned(42, 10));
            bool differs = Enumerable.Range(1, 9).Any(s => !Assigned(s, 10).SequenceEqual(Assigned(42, 10)));
            Assert.IsTrue(differs, "different seeds shuffle character pools differently");
        }

        [Test]
        public void SetCharacterSwapsWithinSameTeamAllowsOpponentDuplicatesAndTeammatesKidsLocksConfirmed()
        {
            var sim = new MatchSim(Data.GameRules, Layout, 1);
            for (int id = 1; id <= 4; id++) sim.AddPlayer(id, id <= 2 ? 0 : 1);
            string[] preset = { "narda", "jumong", "jopay", "oriang" };
            for (int id = 1; id <= 4; id++) sim.Players[id].CharacterId = preset[id - 1];

            Assert.IsFalse(sim.SetCharacter(99, "jopay"), "unknown player rejected");
            Assert.IsFalse(sim.SetCharacter(1, "not_a_kid"), "unknown character rejected");

            Assert.IsTrue(sim.SetCharacter(1, "jopay"));
            Assert.AreEqual("jopay", sim.Players[1].CharacterId, "opponent's toni does not block us");
            Assert.AreEqual("jopay", sim.Players[3].CharacterId, "opponent keeps theirs");

            Assert.IsTrue(sim.SetCharacter(2, "jopay"));
            Assert.AreEqual("jopay", sim.Players[2].CharacterId);
            Assert.AreEqual("jumong", sim.Players[1].CharacterId, "teammate receives previous character");

            var confirmed = new[] { 1, 3 };
            CollectionAssert.AreEqual(new[] { "jumong" }, sim.TeammatesKids(2, confirmed),
                "only confirmed teammates on the same team are locked");
            CollectionAssert.IsEmpty(sim.TeammatesKids(1, new[] { 3 }),
                "confirmed opponents never lock a kid on our team");
        }

        [Test]
        public void AllWeaponsHaveDescriptionsUniqueNamesAndRoleNamingScheme()
        {
            GameRules rules = Data.GameRules;
            var nameScheme = new Regex(@"^[A-Z][a-z]+( · [A-Z][A-Za-z\- ]+)?$");
            var displayNames = new HashSet<string>();
            var shortNames = new HashSet<string>();

            foreach (WeaponDef def in rules.Weapons)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.Description), $"{def.Id} has Description");
                Assert.IsTrue(nameScheme.IsMatch(def.DisplayName), $"'{def.DisplayName}' matches Name or Name · Role");
                Assert.IsFalse(def.DisplayName.Contains("("), $"{def.Id} has no brackets");
                Assert.IsTrue(displayNames.Add(def.DisplayName), $"unique DisplayName {def.DisplayName}");
                Assert.IsTrue(shortNames.Add(def.ShortName), $"unique ShortName {def.ShortName}");
            }

            foreach (string game in new[] { "Bato", "Gunting", "Tsinelas" })
            {
                string key = game.ToLowerInvariant();
                WeaponDef light = Data.Weapon(key + "_light");
                WeaponDef heavy = Data.Weapon(key + "_heavy");
                Assert.IsTrue(light.DisplayName.StartsWith(game + " · "), light.DisplayName);
                Assert.IsTrue(heavy.DisplayName.StartsWith(game + " · "), heavy.DisplayName);
                Assert.AreNotEqual(light.DisplayName, heavy.DisplayName);
            }

            Assert.IsNotNull(rules.PrimaryWeapon);
            Assert.IsFalse(string.IsNullOrWhiteSpace(rules.PrimaryWeapon.DisplayName));
            Assert.IsFalse(string.IsNullOrWhiteSpace(rules.PrimaryWeapon.Description));
            Assert.IsNotNull(rules.UltimateWeapon);
            Assert.IsFalse(string.IsNullOrWhiteSpace(rules.UltimateWeapon.DisplayName));
            Assert.IsFalse(string.IsNullOrWhiteSpace(rules.UltimateWeapon.Description));
        }

        [Test]
        public void WeaponPickDealsNoDamageConfirmLockAndReplaceSlot()
        {
            var sim = new MatchSim(Data.GameRules, Layout, 1);
            Assert.IsFalse(WeaponPick.DealsNoDamage(new string[0], sim.WeaponDefs));
            Assert.IsTrue(WeaponPick.DealsNoDamage(new[] { "papel_shield", "heal", "papel_trap" }, sim.WeaponDefs));
            Assert.IsFalse(WeaponPick.DealsNoDamage(new[] { "papel_shield", "heal", "bato_light" }, sim.WeaponDefs));
            Assert.IsFalse(WeaponPick.DealsNoDamage(new[] { "papel_shield", "unknown_id" }, sim.WeaponDefs));

            string[] ids = { "bato_light", "bato_heavy", "lata", "jacks" };
            var pick = new WeaponPick(ids, 10.0, new[] { "lata", "jacks" });
            Assert.IsTrue(pick.CanConfirm());
            Assert.IsTrue(pick.ReplaceSlot(1, "bato_light"));
            CollectionAssert.AreEqual(new[] { "lata", "bato_light" }, pick.Picks);
            Assert.IsFalse(pick.ReplaceSlot(0, "bato_light"), "rejects duplicate");
            Assert.IsFalse(pick.ReplaceSlot(0, "missing"), "rejects unavailable");
            Assert.IsFalse(pick.ReplaceSlot(2, "jacks"), "rejects out-of-range slot");
            Assert.IsTrue(pick.Confirm());
            Assert.IsFalse(pick.ReplaceSlot(0, "jacks"), "locked after Confirm");
            pick.Toggle("bato_heavy");
            CollectionAssert.AreEqual(new[] { "lata", "bato_light" }, pick.Picks, "Toggle ignored after Confirm");
        }
    }
}
