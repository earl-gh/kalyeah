using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Kalyeah.Game
{
    /// <summary>The home lobby's online-first PLAY: no server opens the offline prompt; the offline hub replaces the lobby UI; Back unwinds both.</summary>
    public sealed class HomeFlowTests
    {
        GameObject _root;
        HomeScreenView _home;
        string _navigated;
        string _savedServerUrl;
        GameMode _savedMode;

        [SetUp]
        public void SetUp()
        {
            _savedServerUrl = AppSession.Current.ServerUrl;
            _savedMode = AppSession.Current.Mode;
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
    }
}
