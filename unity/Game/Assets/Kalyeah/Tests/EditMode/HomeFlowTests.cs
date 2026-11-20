using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityObject = UnityEngine.Object;

namespace Kalyeah.Game
{
    /// <summary>
    /// Home (docs/SPEC.md §4 and §8): online-first PLAY and the launch connection check open the offline prompt; the offline hub
    /// replaces the home UI; Back unwinds both; the mode card never states rules; rank stays off home; match history opens from
    /// the profile; Donate lives in Settings.
    /// </summary>
    public sealed class HomeFlowTests
    {
        GameObject _root;
        HomeScreenView _home;
        string _navigated;
        string _savedServerUrl;
        GameMode _savedMode;
        int _savedFirstTo;

        [SetUp]
        public void SetUp()
        {
            _savedServerUrl = AppSession.Current.ServerUrl;
            _savedMode = AppSession.Current.Mode;
            _savedFirstTo = AppSession.Current.FirstTo;
            _root = new GameObject("HomeFlowTestRoot");
            Canvas canvas = Ui.MakeCanvas("HomeFlowCanvas", _root.transform, null);
            _navigated = null;
            _home = new HomeScreenView(canvas.transform, LoadData(), id => _navigated = id, _ => { });
        }

        [TearDown]
        public void TearDown()
        {
            _home?.Dispose();
            if (_root != null) UnityObject.DestroyImmediate(_root);
            AppSession.Current.ServerUrl = _savedServerUrl;
            AppSession.Current.Mode = _savedMode;
            AppSession.Current.FirstTo = _savedFirstTo;
        }

        static DataSource LoadData()
        {
            DataSource res = KalyeahResources.Data();
            return new DataSource(name =>
            {
                try
                {
                    return res.Text(name);
                }
                catch (KeyNotFoundException)
                {
                    string path = Path.Combine(Application.dataPath, "Imported", "Resources", "Data", name + ".json");
                    return File.Exists(path) ? File.ReadAllText(path) : null;
                }
            });
        }

        [Test]
        public void Play_WithoutServer_OpensOfflinePrompt_AndBackClosesIt()
        {
            AppSession.Current.ServerUrl = "";
            _home.PlayButton.onClick.Invoke();

            Assert.IsTrue(_home.OfflinePromptOpen, "PLAY with no server must offer offline play");
            Assert.IsNull(_navigated, "PLAY with no server must not open the online lobby");
            Assert.IsTrue(_home.HandleBack(), "Back must consume the open prompt");
            Assert.IsFalse(_home.OfflinePromptOpen);
        }

        [Test]
        public void Play_WithServer_OpensOnlineLobby()
        {
            AppSession.Current.ServerUrl = "ws://127.0.0.1:7777";
            _home.PlayButton.onClick.Invoke();

            Assert.AreEqual(MenuStack.LobbyId, _navigated);
            Assert.AreEqual(GameMode.Online, AppSession.Current.Mode);
            Assert.IsFalse(_home.OfflinePromptOpen);
        }

        [Test]
        public void OfflineHub_ReplacesLobbyUi_AndBackRestoresIt()
        {
            Transform topBar = _home.SafeRoot.Find("TopBar");
            Transform hub = _home.SafeRoot.Find("HubBody");

            _home.OpenOfflineHub();
            Assert.IsTrue(_home.OfflineHubOpen);
            Assert.IsFalse(topBar.gameObject.activeSelf, "the lobby top bar hides behind the offline hub");
            Assert.IsFalse(hub.gameObject.activeSelf, "the lobby body hides behind the offline hub");

            Assert.IsTrue(_home.HandleBack());
            Assert.IsFalse(_home.OfflineHubOpen);
            Assert.IsTrue(topBar.gameObject.activeSelf);
            Assert.IsTrue(hub.gameObject.activeSelf);
        }

        [Test]
        public void ModeCard_SaysHowTheModeFeels_NeverItsRules()
        {
            for (int i = 0; i < 2; i++)
            {
                string line = _home.ModeLine;
                Assert.IsNotEmpty(line);
                StringAssert.DoesNotMatch("[0-9]", line, "no team size or score target on the mode card");
                StringAssert.DoesNotContain("FIRST TO", line.ToUpperInvariant());
                StringAssert.DoesNotContain("MAP", line.ToUpperInvariant(), "no map rules on the mode card");
                _home.ModeButton.onClick.Invoke();
            }
        }

        [Test]
        public void Profile_OnHome_ShowsNoRank()
        {
            Transform profile = _home.SafeRoot.Find("TopBar/Profile");
            Assert.IsNotNull(profile);
            foreach (Text text in profile.GetComponentsInChildren<Text>(true))
                foreach (string rankWord in new[] { "KARTON", "KAHOY", "BAKAL", "PILAK", "GINTO", " LP" })
                    StringAssert.DoesNotContain(rankWord, text.text.ToUpperInvariant(), "rank shows only where Ranked is chosen");
        }

        [Test]
        public void LaunchCheck_WithoutServer_OpensOfflinePrompt_AndRetryChecksAgainWithoutOpeningTheLobby()
        {
            AppSession.Current.ServerUrl = "";
            Assert.IsFalse(_home.CheckConnectionAtLaunch());
            Assert.IsTrue(_home.OfflinePromptOpen, "offline at launch must offer offline play before home");

            Button retry = _home.Root.Find("OfflinePrompt/Card/Actions/Retry").GetComponent<Button>();
            retry.onClick.Invoke();
            Assert.IsTrue(_home.OfflinePromptOpen, "still offline: the prompt stays");
            Assert.IsNull(_navigated, "RETRY at launch checks the connection; it never opens the lobby");
        }

        [Test]
        public void ProfileSheet_MatchHistory_OpensHistory()
        {
            _home.Sheets.OpenProfile();
            _home.Sheets.HistoryButton.onClick.Invoke();
            Assert.AreEqual(MenuStack.HistoryId, _navigated);
            Assert.IsFalse(_home.Sheets.IsOpen, "the profile closes on the way to history");
        }

        [Test]
        public void Settings_DonateTab_OpensDonate()
        {
            int donations = 0;
            var settings = new SettingsScreenView(_home.Root.parent, () => { }, () => donations++);
            try
            {
                settings.SetActiveTab(SettingsScreenView.TabDonate);
                Assert.AreEqual(SettingsScreenView.TabDonate, settings.ActiveTab);
                settings.DonateButton.onClick.Invoke();
                Assert.AreEqual(1, donations);
            }
            finally
            {
                settings.Dispose();
            }
        }
    }
}
