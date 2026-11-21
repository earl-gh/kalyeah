using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityObject = UnityEngine.Object;

namespace Kalyeah.Game
{
    /// <summary>
    /// Kids (docs/SPEC.md §8): all 12 kids with a role filter, the role shown as an icon only on cards, a kid page with role,
    /// purok and story, SHOW ON HOME, and Back from a page to the grid (or home when the page was opened from home).
    /// </summary>
    public sealed class KidsScreenTests
    {
        GameObject _root;
        KidsScreenView _kids;
        string _savedKid;
        int _backs;

        [SetUp]
        public void SetUp()
        {
            _savedKid = AppSession.Current.SelectedCharacter;
            _root = new GameObject("KidsTestRoot");
            Canvas canvas = Ui.MakeCanvas("KidsCanvas", _root.transform, null);
            _backs = 0;
            _kids = new KidsScreenView(canvas.transform, LoadData(), () => _backs++);
        }

        [TearDown]
        public void TearDown()
        {
            _kids?.Dispose();
            if (_root != null) UnityObject.DestroyImmediate(_root);
            AppSession.Current.SelectedCharacter = _savedKid;
        }

        static DataSource LoadData()
        {
            DataSource res = KalyeahResources.Data();
            return new DataSource(name =>
            {
                try { return res.Text(name); }
                catch (KeyNotFoundException)
                {
                    string path = Path.Combine(Application.dataPath, "Imported", "Resources", "Data", name + ".json");
                    return File.Exists(path) ? File.ReadAllText(path) : null;
                }
            });
        }

        [Test]
        public void Grid_ShowsAllTwelveKids_AndTheRoleFilterKeepsThree()
        {
            CollectionAssert.AreEqual(AppSession.DefaultRoster, _kids.VisibleKids().ToArray());
            _kids.SetFilter("SEEKER");
            CollectionAssert.AreEquivalent(new[] { "kikay", "karding", "manoy" }, _kids.VisibleKids().ToArray());
            _kids.SetFilter("");
            Assert.AreEqual(12, _kids.VisibleKids().Count());
        }

        [Test]
        public void Cards_ShowTheRoleAsAnIconOnly()
        {
            Transform card = _kids.SafeRoot.Find("GridPanel/Grid/Kid_karding");
            Assert.IsNotNull(card);
            Assert.IsNotNull(card.Find("RoleChip/Icon"), "the role is an icon on the card");
            foreach (Text text in card.GetComponentsInChildren<Text>(true))
                foreach (string role in KidsScreenView.Filters.Where(f => f.Length > 0))
                    StringAssert.DoesNotContain(role, text.text.ToUpperInvariant(), "no role words on the cards");
        }

        [Test]
        public void KidPage_ShowsRolePurokAndTabs_AndBackReturnsToTheGrid()
        {
            _kids.Show();
            _kids.OpenPage("karding");
            Transform info = _kids.SafeRoot.Find("PagePanel/Info");
            Assert.AreEqual("KARDING", info.Find("Name").GetComponent<Text>().text);
            StringAssert.Contains("SEEKER", info.Find("Role").GetComponent<Text>().text);
            StringAssert.Contains("PALENGKE", info.Find("Role").GetComponent<Text>().text);
            StringAssert.Contains("chalk maps", info.Find("Personality").GetComponent<Text>().text);
            Assert.AreEqual(KidsScreenView.TabSkills, _kids.ActiveTab, "a kid page opens on Skills");

            Assert.IsTrue(_kids.HandleBack(), "Back from a page opened on the grid returns to the grid");
            Assert.IsNull(_kids.OpenKid);
            Assert.IsFalse(_kids.HandleBack(), "Back on the grid leaves the screen");
        }

        [Test]
        public void KidPage_SkillsTab_ShowsOneKitSkillAtATime_FromAThreeSkillSelector()
        {
            _kids.Show();
            _kids.OpenPage("karding");
            Transform content = _kids.SafeRoot.Find("PagePanel/ContentPanel/Content");
            Assert.AreEqual(3, content.Cast<Transform>().Count(t => t.name.StartsWith("Skill_")), "S1, S2 and the ultimate");

            string first = string.Join(" ", content.Find("SkillCard").GetComponentsInChildren<Text>(true).Select(t => t.text));
            StringAssert.Contains("Yoyo", first);
            StringAssert.Contains("25 water, 10s cooldown", first);

            content.Find("Skill_S2").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(1, _kids.SelectedSkill);
            string second = string.Join(" ", _kids.SafeRoot.Find("PagePanel/ContentPanel/Content/SkillCard").GetComponentsInChildren<Text>(true).Select(t => t.text));
            StringAssert.Contains("Piko", second);
            StringAssert.Contains("30 water, 14s cooldown", second);

            _kids.SelectSkill(2);
            string ultimate = string.Join(" ", _kids.SafeRoot.Find("PagePanel/ContentPanel/Content/SkillCard").GetComponentsInChildren<Text>(true).Select(t => t.text));
            StringAssert.Contains("Pogs Slam", ultimate);
            StringAssert.Contains("Ultimate, 85s cooldown", ultimate);

            _kids.OpenPage("narda");
            Assert.AreEqual(0, _kids.SelectedSkill, "a new kid page opens on the first skill");
        }

        [Test]
        public void KidPage_StoryTab_ShowsHomeFriendsAndRival_AndMasteryIsHonest()
        {
            _kids.Show();
            _kids.OpenPage("karding");
            _kids.SelectTab(KidsScreenView.TabStory);
            string story = string.Join(" ", _kids.SafeRoot.Find("PagePanel/ContentPanel/Content").GetComponentsInChildren<Text>(true).Select(t => t.text));
            foreach (string expected in new[] { "Lolo", "Engineer", "Inggo", "Pamela", "Kikay" })
                StringAssert.Contains(expected, story);

            _kids.SelectTab(KidsScreenView.TabMastery);
            string mastery = string.Join(" ", _kids.SafeRoot.Find("PagePanel/ContentPanel/Content").GetComponentsInChildren<Text>(true).Select(t => t.text));
            StringAssert.Contains("0 of 7", mastery, "no mastery exists yet: the tab says so instead of showing a made-up level");
        }

        [Test]
        public void KidPage_OpenedFromHome_BackLeavesTheScreen()
        {
            _kids.ShowKid("narda");
            Assert.AreEqual("narda", _kids.OpenKid);
            Assert.IsFalse(_kids.HandleBack(), "Back from the home kid's page returns home");
        }

        [Test]
        public void ShowOnHome_PinsTheKid_AndTheButtonSaysSo()
        {
            AppSession.Current.SelectedCharacter = "narda";
            _kids.Show();
            _kids.OpenPage("tere");
            Assert.IsTrue(_kids.HomeButton.interactable);
            _kids.HomeButton.onClick.Invoke();
            Assert.AreEqual("tere", AppSession.Current.SelectedCharacter);
            Assert.IsFalse(_kids.HomeButton.interactable, "already on home: nothing to press");
        }

        [Test]
        public void PurokAndStory_ParseTheDescription()
        {
            const string d = "Seeker from Palengke. A tinkerer who draws chalk maps of every aisle.";
            Assert.AreEqual("Palengke", KidsScreenView.Purok(d));
            Assert.AreEqual("A tinkerer who draws chalk maps of every aisle.", KidsScreenView.Story(d));
            Assert.AreEqual("SEEKER", HomeScreenView.ParseRole(d));
        }
    }
}
