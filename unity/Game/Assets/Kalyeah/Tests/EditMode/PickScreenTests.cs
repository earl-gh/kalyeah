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
    /// Kid select (docs/SPEC.md §4 and §8): the 12 kids in four role rows, a tap previews the kid and their three skills,
    /// kids a teammate took are greyed and cannot be locked, the team bar says which roles are open, and LOCK IN saves the kid.
    /// </summary>
    public sealed class PickScreenTests
    {
        GameObject _root;
        PickScreenView _pick;
        string _savedKid;
        int _backs;
        int _confirms;

        [SetUp]
        public void SetUp()
        {
            _savedKid = AppSession.Current.SelectedCharacter;
            AppSession.Current.SelectedCharacter = "karding";
            _root = new GameObject("PickTestRoot");
            Canvas canvas = Ui.MakeCanvas("PickCanvas", _root.transform, null);
            _backs = 0;
            _confirms = 0;
            _pick = new PickScreenView(canvas.transform, LoadData(), () => _backs++, () => _confirms++);
        }

        [TearDown]
        public void TearDown()
        {
            _pick?.Dispose();
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

        string Texts(Transform parent) => string.Join(" ", parent.GetComponentsInChildren<Text>(true).Select(t => t.text));

        [Test]
        public void Roster_ShowsAllTwelveKids_InFourRoleRows()
        {
            int kids = 0;
            foreach (string role in PickScreenView.Roles)
            {
                Transform cards = _pick.SafeRoot.Find("Body/RosterPanel/Row_" + role + "/Cards");
                Assert.IsNotNull(cards, role + " row");
                Assert.AreEqual(3, cards.childCount, role + " has three kids");
                kids += cards.childCount;
            }
            Assert.AreEqual(AppSession.DefaultRoster.Length, kids);
            Assert.IsNotNull(_pick.SafeRoot.Find("Body/RosterPanel/Row_SEEKER/Cards/Kid_karding"));
        }

        [Test]
        public void Tapping_AKid_PreviewsTheirRoleAndName()
        {
            string chosen = null;
            _pick.CharacterSelected += id => chosen = id;
            _pick.SafeRoot.Find("Body/RosterPanel/Row_DEFENDER/Cards/Kid_jopay").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual("jopay", chosen);
            Assert.AreEqual("jopay", _pick.SelectedCharacterId);

            string plate = Texts(_pick.SafeRoot.Find("Body/NamePlate"));
            StringAssert.Contains("JOPAY", plate);
            StringAssert.Contains("UPTOWN", plate, "the plate names the kid's purok; the role is its icon");
            Assert.AreEqual(0, _confirms, "a preview never locks");
        }

        [Test]
        public void SkillSelector_ShowsOneSkillAtATime_StartingWithTheFirst()
        {
            Transform tabs = _pick.SafeRoot.Find("Body/DetailPanel/SkillTabs");
            Assert.AreEqual(3, tabs.Cast<Transform>().Count(t => t.gameObject.activeSelf), "S1, S2 and the ultimate");
            Transform card = _pick.SafeRoot.Find("Body/DetailPanel/SkillCard");

            Assert.AreEqual(0, _pick.SelectedSkill);
            string first = Texts(card);
            StringAssert.Contains("Yoyo", first);
            StringAssert.Contains("25 water, 10s cooldown", first);
            StringAssert.DoesNotContain("Pogs Slam", first);

            tabs.Find("Skill_2").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(2, _pick.SelectedSkill);
            string ultimate = Texts(card);
            StringAssert.Contains("Pogs Slam", ultimate);
            StringAssert.Contains("Ultimate, 85s cooldown", ultimate);

            _pick.SelectCharacter("narda");
            Assert.AreEqual(0, _pick.SelectedSkill, "a new kid opens on their first skill");
        }

        [Test]
        public void LockIn_SavesTheKid_AndLeavesTheScreen()
        {
            _pick.SelectCharacter("narda");
            Assert.IsTrue(_pick.CanConfirm);
            _pick.LockButton.onClick.Invoke();
            Assert.AreEqual("narda", AppSession.Current.SelectedCharacter);
            Assert.AreEqual(1, _confirms);
        }

        [Test]
        public void ATeammatesKid_IsGreyedAndCannotBeLocked()
        {
            _pick.SetTakenCharacters(new[] { "narda", "jopay" });
            _pick.SelectCharacter("narda");
            Assert.IsTrue(_pick.IsCharacterTaken("narda"));
            Assert.IsFalse(_pick.CanConfirm);
            Assert.IsFalse(_pick.LockButton.interactable);
            StringAssert.Contains("TAKEN", Texts(_pick.LockButton.transform));
            Assert.IsTrue(_pick.SafeRoot.Find("Body/RosterPanel/Row_STRIKER/Cards/Kid_narda/Dim").gameObject.activeSelf, "the card is dimmed with TAKEN");

            _pick.LockButton.onClick.Invoke();
            Assert.AreEqual(0, _confirms);
            Assert.AreNotEqual("narda", AppSession.Current.SelectedCharacter);

            _pick.SelectCharacter("tere");
            Assert.IsTrue(_pick.CanConfirm, "a kid nobody took locks");
        }

        [Test]
        public void TeamBar_NamesTheRolesTheTeamStillLacks()
        {
            // Karding is a Seeker: the other three roles are open
            CollectionAssert.AreEqual(new[] { "STRIKER", "DEFENDER", "CONTROLLER" }, _pick.MissingRoles());
            StringAssert.Contains("Still open", Texts(_pick.SafeRoot.Find("TeamBar")));

            // a full team with no Controller is a warning
            _pick.SetTakenCharacters(new[] { "narda", "jopay", "inggo", "manoy" });
            CollectionAssert.AreEqual(new[] { "CONTROLLER" }, _pick.MissingRoles());
            string bar = Texts(_pick.SafeRoot.Find("TeamBar"));
            StringAssert.Contains("No ", bar);
        }

        [Test]
        public void Back_RaisesBackRequested()
        {
            _pick.GoBack();
            Assert.AreEqual(1, _backs);
        }
    }
}
