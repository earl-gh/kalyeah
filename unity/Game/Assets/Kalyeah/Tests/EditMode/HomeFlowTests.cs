using System;
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
        public void LeftColumnAndNav_ShowSampleContent_AndTapsSayTheSystemIsNotBuilt()
        {
            Assert.AreEqual("karding", AppSession.DefaultCharacter, "Karding is the default home kid");
            foreach (string card in new[] { "Pass", "Event", "Daily" })
            {
                Transform t = _home.SafeRoot.Find("HubBody/LeftColumn/Card_" + card);
                Assert.IsNotNull(t, card + " card");
                t.GetComponent<Button>().onClick.Invoke();
                Assert.IsNotEmpty(_home.ToastText, card + " tap says its system comes later");
            }
            _home.SafeRoot.Find("HubBody/BottomNav/Nav_Store").GetComponent<Button>().onClick.Invoke();
            StringAssert.Contains("store", _home.ToastText);
            _home.SafeRoot.Find("HubBody/BottomNav/Nav_Kids").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(MenuStack.KidsId, _navigated);
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
        public void Home_ProfileButton_NavigatesToFullScreenProfile()
        {
            Transform profile = _home.SafeRoot.Find("TopBar/Profile");
            Assert.IsNotNull(profile, "TopBar/Profile button");
            profile.GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(MenuStack.ProfileId, _navigated, "tapping Profile on Home opens the full-screen Profile hub, not a sheet modal");
            Assert.IsFalse(_home.Sheets.IsOpen, "legacy profile sheet modal must stay closed");
        }

        [Test]
        public void ProfileScreen_OverviewHistoryIdentity_WorksEndToEnd()
        {
            string savedName = AppSession.Current.PlayerName;
            int savedAvatar = AppSession.Current.AvatarIndex;
            bool hadAvatarPref = PlayerPrefs.HasKey(HomeSheetsView.AvatarPrefKey);
            int savedAvatarPref = PlayerPrefs.GetInt(HomeSheetsView.AvatarPrefKey, 0);
            bool hadCustomPref = PlayerPrefs.HasKey(HomeSheetsView.CustomPhotoPrefKey);
            int savedCustomPref = PlayerPrefs.GetInt(HomeSheetsView.CustomPhotoPrefKey, 0);

            string navTarget = null;
            int sheetsProfileChanges = 0;
            Action onSheetsChanged = () => sheetsProfileChanges++;
            _home.Sheets.ProfileChanged += onSheetsChanged;

            var profile = new ProfileScreenView(
                _home.Root.parent,
                LoadData(),
                onBack: () => navTarget = "back",
                onOpenHistory: () => navTarget = MenuStack.HistoryId,
                onOpenKids: () => navTarget = MenuStack.KidsId,
                sheets: _home.Sheets);
            try
            {
                profile.Show();
                Assert.AreEqual(ProfileScreenView.TabOverview, profile.ActiveTab);
                foreach (Text text in profile.SafeRoot.GetComponentsInChildren<Text>(false))
                    foreach (string rankWord in new[] { "KARTON", "KAHOY", "BAKAL", "PILAK", "GINTO", " LP" })
                        StringAssert.DoesNotContain(rankWord, text.text.ToUpperInvariant(), "profile overview stays rank-free until Ranked exists");

                profile.ChangeKidButton.onClick.Invoke();
                Assert.AreEqual(MenuStack.KidsId, navTarget);

                profile.HistoryButton.onClick.Invoke();
                Assert.AreEqual(ProfileScreenView.TabHistory, profile.ActiveTab);
                profile.FullHistoryButton.onClick.Invoke();
                Assert.AreEqual(MenuStack.HistoryId, navTarget);

                profile.EditIdentityButton.onClick.Invoke();
                Assert.AreEqual(ProfileScreenView.TabIdentity, profile.ActiveTab);

                // Header BackButton pops straight back to Home (matching Settings/Codex), while HandleBack unwinds tabs
                profile.BackButton.onClick.Invoke();
                Assert.AreEqual("back", navTarget);

                Assert.IsTrue(profile.HandleBack(), "Back from Identity tab unwinds to Overview first");
                Assert.AreEqual(ProfileScreenView.TabOverview, profile.ActiveTab);
                Assert.IsFalse(profile.HandleBack(), "Back from Overview pops back to Home");

                profile.SetActiveTab(ProfileScreenView.TabIdentity);
                profile.PlayerNameInput.text = "kalyeah99";
                Assert.IsTrue(profile.SavePlayerName());
                Assert.AreEqual("KALYEAH99", AppSession.Current.PlayerName);

                // Selecting avatar syncs through HomeSheetsView & fires ProfileChanged (including index 0)
                profile.SelectAvatar(2);
                Assert.AreEqual(2, AppSession.Current.AvatarIndex);
                Assert.AreEqual(2, _home.Sheets.AvatarIndex);
                Assert.GreaterOrEqual(sheetsProfileChanges, 1);

                profile.SelectAvatar(0);
                Assert.AreEqual(0, AppSession.Current.AvatarIndex);
                Assert.AreEqual(0, _home.Sheets.AvatarIndex);

                // Opening ID camera over Profile closes cleanly back to Profile without opening legacy ProfileSheet
                profile.TakeSelfieButton.onClick.Invoke();
                Assert.IsTrue(_home.Sheets.IsOpen);
                Assert.AreEqual(HomeSheetsView.SheetKind.IdCamera, _home.Sheets.ActiveSheet);
                Assert.AreSame(profile.Root, _home.Sheets.Root.parent);
                Transform camBack = _home.Sheets.SafeRoot.Find("SheetCard/CardCreamFill/IdCameraSheet/Buttons/BackOrRetakeButton");
                Assert.IsNotNull(camBack, "IdCamera BackOrRetakeButton");
                camBack.GetComponent<Button>().onClick.Invoke();
                Assert.IsFalse(_home.Sheets.IsOpen, "IdCamera back button closes cleanly instead of opening legacy ProfileSheet");
                Assert.AreSame(_home.Root, _home.Sheets.Root.parent, "Sheets root returns to Home on close");

                profile.TakeSelfieButton.onClick.Invoke();
                Assert.IsTrue(_home.Sheets.IsOpen);
                Assert.IsTrue(profile.HandleBack(), "HandleBack closes IdCamera modal first");
                Assert.IsFalse(_home.Sheets.IsOpen);
                Assert.AreEqual(ProfileScreenView.TabIdentity, profile.ActiveTab);
            }
            finally
            {
                _home.Sheets.ProfileChanged -= onSheetsChanged;
                _home.Sheets.Close();
                profile.Dispose();
                AppSession.Current.PlayerName = savedName;
                AppSession.Current.AvatarIndex = savedAvatar;
                if (hadAvatarPref) PlayerPrefs.SetInt(HomeSheetsView.AvatarPrefKey, savedAvatarPref);
                else PlayerPrefs.DeleteKey(HomeSheetsView.AvatarPrefKey);
                if (hadCustomPref) PlayerPrefs.SetInt(HomeSheetsView.CustomPhotoPrefKey, savedCustomPref);
                else PlayerPrefs.DeleteKey(HomeSheetsView.CustomPhotoPrefKey);
            }
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

        [Test]
        public void Home_ManilaStreetStage_IsActiveWith16x9RenderTextureAndCleanStudioFallback()
        {
            Assert.IsTrue(_home.IsManilaStreetStageActive, "Home must render the live sunlit 3D Manila street stage");
            Assert.IsNotNull(_home.HeroTexture, "Home HeroStage RenderTexture must exist");
            Assert.AreEqual(HeroStage.StreetTextureWidth, _home.HeroTexture.width, "Manila street stage RT width must be 1280");
            Assert.AreEqual(HeroStage.StreetTextureHeight, _home.HeroTexture.height, "Manila street stage RT height must be 720");
            Assert.IsNull(_home.Root.Find("Backdrop/Stall"), "Legacy dark warehouse stall backdrop must be retired");

            var prevAmbientMode = RenderSettings.ambientMode;
            Color prevSky = RenderSettings.ambientSkyColor;
            Color prevEq = RenderSettings.ambientEquatorColor;
            Color prevGround = RenderSettings.ambientGroundColor;
            try
            {
                using (var stage = new HeroStage { ShowManilaStreetStage = true })
                {
                    Assert.AreEqual(HeroStage.StreetTextureWidth, stage.Texture.width);
                    Assert.AreEqual(HeroStage.StreetTextureHeight, stage.Texture.height);
                    Assert.IsNotNull(stage.StageRoot.Find("ManilaStreetCorner"), "ManilaStreetCorner diorama must be constructed");
                    Assert.IsTrue(stage.StageRoot.Find("ManilaStreetCorner").gameObject.activeSelf);

                    Light[] lights = stage.StageRoot.GetComponentsInChildren<Light>(true);
                    int dirCount = 0, spotCount = 0, pointCount = 0;
                    for (int i = 0; i < lights.Length; i++)
                    {
                        if (lights[i].type == LightType.Directional) dirCount++;
                        else if (lights[i].type == LightType.Spot) spotCount++;
                        else if (lights[i].type == LightType.Point) pointCount++;
                    }
                    Assert.AreEqual(1, dirCount, "Manila street HeroStage must keep strictly 1 Directional Key light");
                    Assert.AreEqual(1, spotCount, "Manila street HeroStage must keep 1 cool Spot Rim light");
                    Assert.AreEqual(1, pointCount, "Manila street HeroStage must keep 1 gentle Point Fill light");

                    stage.ShowManilaStreetStage = false;
                    Assert.AreEqual(HeroStage.StudioTextureWidth, stage.Texture.width, "Studio fallback RT width must restore to 768");
                    Assert.AreEqual(HeroStage.StudioTextureHeight, stage.Texture.height, "Studio fallback RT height must restore to 1024");
                    Assert.IsFalse(stage.StageRoot.Find("ManilaStreetCorner").gameObject.activeSelf);
                }
            }
            finally
            {
                RenderSettings.ambientMode = prevAmbientMode;
                RenderSettings.ambientSkyColor = prevSky;
                RenderSettings.ambientEquatorColor = prevEq;
                RenderSettings.ambientGroundColor = prevGround;
            }
        }

        [Test]
        public void Home_HeroNameplate_IsHiddenOnHomeWhilePreservingKidButtonContract()
        {
            Assert.IsFalse(_home.IsHeroPlateVisible, "Home must show zero character name, role, or map nameplate");
            Assert.IsNotNull(_home.KidButton, "KidButton property contract remains non-null");
            Assert.IsFalse(_home.KidButton.gameObject.activeInHierarchy, "KidPlate must remain inactive on Home");

            Transform heroZone = _home.SafeRoot.Find("HubBody/HeroZone");
            Assert.IsNotNull(heroZone);
            Text[] activeHeroTexts = heroZone.GetComponentsInChildren<Text>(includeInactive: false);
            Assert.AreEqual(0, activeHeroTexts.Length, "Zero visible nameplate text may appear inside HeroZone on Home");
        }

        [Test]
        public void Home_L3CardlessProfile_MatchesL4HeightAndCenterline_AndModePillHasNoDarkSmudge()
        {
            Canvas.ForceUpdateCanvases();
            _home.RefreshLayout();
            Canvas.ForceUpdateCanvases();

            Assert.IsNotNull(_home.ProfileVisualRect, "L3 ProfileVisualRect");
            Assert.IsNotNull(_home.CurrencyStripVisualRect, "L4 CurrencyStripVisualRect");
            Assert.IsNotNull(_home.SettingsVisualRect, "L4 SettingsVisualRect");

            float l3H = _home.ProfileVisualRect.rect.height;
            float l4CurrH = _home.CurrencyStripVisualRect.rect.height;
            float l4SetH = _home.SettingsVisualRect.rect.height;
            Assert.AreEqual(HomeScreenView.TopBarHeight, l3H, 1f, "L3 visual height must match TopBarHeight (56 cpx)");
            Assert.LessOrEqual(Mathf.Abs(l3H - l4CurrH), 2f, "L3 profile visual height must match L4 currency strip height within 2 cpx");
            Assert.LessOrEqual(Mathf.Abs(l3H - l4SetH), 2f, "L3 profile visual height must match L4 settings square height within 2 cpx");

            float l3CenterY = WorldCenterY(_home.ProfileVisualRect);
            float l4CurrCenterY = WorldCenterY(_home.CurrencyStripVisualRect);
            float l4SetCenterY = WorldCenterY(_home.SettingsVisualRect);
            Assert.LessOrEqual(Mathf.Abs(l3CenterY - l4CurrCenterY), 2f, "L3 and L4 currency strip must share the exact top-bar centerline");
            Assert.LessOrEqual(Mathf.Abs(l3CenterY - l4SetCenterY), 2f, "L3 and L4 settings square must share the exact top-bar centerline");

            Transform profile = _home.SafeRoot.Find("TopBar/Profile");
            Assert.IsNotNull(profile);
            Assert.IsNull(profile.Find("PlateHost"), "L3 profile must be cardless (no backing card host)");
            Assert.IsNull(profile.Find("Plate"), "L3 profile must be cardless (no backing plate)");

            Transform modeLabelTr = _home.SafeRoot.Find("HubBody/ModeColumn/ModeZone/Pill/ModeLabel");
            Assert.IsNotNull(modeLabelTr, "L7 Paper-White mode pill label");
            Text modeLabel = modeLabelTr.GetComponent<Text>();
            Assert.IsNotNull(modeLabel);
            Outline modeOutline = modeLabel.GetComponent<Outline>();
            Assert.IsTrue(modeOutline == null || !modeOutline.enabled, "L7 dark-ink Paper-White pill label must have outline disabled");
        }

        static float WorldCenterY(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return 0.5f * (corners[0].y + corners[2].y);
        }
    }
}
