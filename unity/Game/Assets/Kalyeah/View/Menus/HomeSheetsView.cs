using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Modal sheets hosted on <see cref="HomeScreenView"/>:
    /// (a) <c>ProfileSheet</c> + local-only <c>IdCameraSheet</c> (all 10 kid avatar tiles + optional <see cref="WebCamTexture"/>
    ///     front-camera selfie stored strictly on-device under <see cref="Application.persistentDataPath"/>, never uploaded),
    /// (b) <c>GameOfMonthSheet</c> (<c>Data/rules__larong_kalye</c> monthly real-world street-game card via <c>ui/tips_icon</c>),
    /// (c) <c>DonateSheet</c> (<c>Data/rules__donate_info</c> grown-up confirmation modal via <c>ui/mail_icon</c>).
    /// Hidden by default and suppressed during non-interactive screenshot capture so <c>title.png</c> remains unobstructed.
    /// </summary>
    public sealed class HomeSheetsView : IDisposable
    {
        public enum SheetKind
        {
            None = 0,
            Profile = 1,
            IdCamera = 2,
            GameOfMonth = 3,
            Donate = 4,
        }

        public enum CameraStep
        {
            Ask = 0,
            Waiting = 1,
            Looking = 2,
            Snapped = 3,
            Denied = 4,
            NoCamera = 5,
        }

        public const string AvatarPrefKey = "kalyeah.profile.avatar";
        public const string CustomPhotoPrefKey = "kalyeah.profile.custom_photo";
        public const string LocalIdPhotoFileName = "id_photo.png";
        public const int IdPhotoSize = 256;

        public static readonly string[] AvatarIds =
        {
            "narda", "jumong", "inggo", "jopay", "oriang", "paeng",
            "kikay", "karding", "manoy", "tere", "pamela", "domeng",
        };

        static readonly string[] MonthNames =
        {
            "JANUARY", "FEBRUARY", "MARCH", "APRIL", "MAY", "JUNE",
            "JULY", "AUGUST", "SEPTEMBER", "OCTOBER", "NOVEMBER", "DECEMBER",
        };

        // Crops the 26 px white mat off 512x512 ui/avatars/<id>.png textures (matching avatar_photos.gd INSET = 26).
        static readonly Rect AvatarCropUv = new Rect(0.051f, 0.051f, 0.898f, 0.898f);
        static readonly Rect FullUv = new Rect(0f, 0f, 1f, 1f);

        static readonly Regex DisallowedNameChars = new Regex(@"[^\p{L}\p{N} '._-]", RegexOptions.Compiled);

        static readonly Color ScrimColor = new Color(0.01f, 0.02f, 0.05f, 0.86f);
        // tactical AAA dark-glass sheet palette (docs/SPEC.md §10)
        static readonly Color HeaderTop = new Color(0.115f, 0.175f, 0.285f, 0.95f);
        static readonly Color HeaderBottom = new Color(0.058f, 0.095f, 0.172f, 0.97f);
        static readonly Color PlateTop = new Color(0.095f, 0.148f, 0.238f, 0.92f);
        static readonly Color PlateBottom = new Color(0.048f, 0.080f, 0.148f, 0.96f);
        static readonly Color TileTop = new Color(0.090f, 0.138f, 0.225f, 0.95f);
        static readonly Color TileBottom = new Color(0.042f, 0.070f, 0.130f, 0.98f);
        static readonly Color InputIdleBorder = new Color(1.00f, 0.80f, 0.34f, 0.78f);
        static readonly Color Espresso = MenuArt.Cream;
        static readonly Color DarkOchre = MenuArt.Gold;
        static readonly Color MangoGold = MenuArt.Gold;
        static readonly Color FiestaOrange = MenuArt.Gold;
        static readonly Color BarrioBlue = new Color(0.17f, 0.25f, 0.42f, 1f);
        static readonly Color JadeGreen = MenuArt.SuccessGreen;
        static readonly Color SlateBtn = new Color(0.24f, 0.29f, 0.42f, 1f);
        static readonly Color ErrorRed = new Color(1f, 0.48f, 0.44f, 1f);

        /// <summary>Browser opener used by <c>DonateSheet</c> after grown-up confirmation; replaceable in tests.</summary>
        public static Action<string> OpenUrlHandler { get; set; } = Application.OpenURL;

        public static string LocalIdPhotoPath => Path.Combine(Application.persistentDataPath, LocalIdPhotoFileName);

        [ExecuteAlways]
        sealed class SheetsLifecycle : MonoBehaviour
        {
            [NonSerialized] public HomeSheetsView Owner;
            bool _viewportSubscribed;

            void OnEnable()
            {
                SubscribeViewport();
                Owner?.RefreshLayout();
            }

            void Update()
            {
                if (Application.isPlaying)
                    Owner?.Tick();
            }

            void OnDisable()
            {
                UnsubscribeViewport();
                Owner?.StopCamera();
            }

            void OnRectTransformDimensionsChange() => Owner?.RefreshLayout();

            void OnApplicationPause(bool paused)
            {
                Owner?.OnApplicationPaused(paused);
            }

            void OnDestroy()
            {
                UnsubscribeViewport();
                Owner?.StopCamera();
            }

            void SubscribeViewport()
            {
                if (_viewportSubscribed) return;
                ScreenFitMetrics.SimulatedViewportChanged += HandleViewportChanged;
                _viewportSubscribed = true;
            }

            void UnsubscribeViewport()
            {
                if (!_viewportSubscribed) return;
                ScreenFitMetrics.SimulatedViewportChanged -= HandleViewportChanged;
                _viewportSubscribed = false;
            }

            void HandleViewportChanged()
            {
                if (this == null)
                {
                    UnsubscribeViewport();
                    return;
                }
                Owner?.OnSimulatedViewportChanged();
            }
        }

        sealed class ScrimDismissCatcher : MonoBehaviour, IPointerClickHandler
        {
            [NonSerialized] public HomeSheetsView Owner;

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData != null && eventData.pointerCurrentRaycast.gameObject == gameObject)
                    Owner?.Close();
            }
        }

        sealed class AvatarTileUi
        {
            public string Id;
            public int Index;
            public Button Button;
            public RectTransform Rect;
            public Image Ring;
        }

        readonly DataSource _data;
        readonly AudioHub _audio;
        readonly Func<bool> _isInteractiveProvider;
        readonly List<AvatarTileUi> _avatarTiles = new List<AvatarTileUi>(10);

        Canvas _canvas;
        SafeArea _safeArea;
        RectTransform _scrimRect;
        RectTransform _modalCard;
        RectTransform _headerBandRect;
        Image _headerAccentRail;
        RawImage _headerIcon;
        Text _headerTitle;
        Button _closeBtn;
        RectTransform _closeBtnRect;

        // Sheet bodies
        RectTransform _profileBody;
        RectTransform _cameraBody;
        RectTransform _monthBody;
        RectTransform _donateBody;

        // ProfileSheet refs
        RectTransform _nametagCardRect;
        RectTransform _idPhotoFrameRect;
        RawImage _profilePreviewImg;
        RectTransform _nameCaptionRowRect;
        Text _nameTagCaptionText;
        RectTransform _nameFieldBoxRect;
        Image _inputFocusOutline;
        bool _wasNameFocused;
        InputField _profileNameInput;
        Text _profileLetterCount;
        Text _profileHintText;
        Button _saveProfileButton;
        RectTransform _saveProfileRect;
        RectTransform _historyRect;
        ScrollRect _photoScroll;
        RectTransform _photoScrollRect;
        RectTransform _photoGridContentRect;
        GridLayoutGroup _photoGrid;
        RectTransform _photoFooterRect;
        Button _cameraTileButton;
        RectTransform _cameraTileRect;
        Button _removePhotoButton;
        RectTransform _removePhotoRect;

        // IdCameraSheet refs
        Text _cameraMessageText;
        RectTransform _cameraMsgRect;
        RectTransform _cameraFrameRect;
        RawImage _cameraPreviewRaw;
        RectTransform _cameraBtnRowRect;
        Button _camPrimaryBtn;
        Text _camPrimaryLabel;
        RectTransform _camPrimaryRect;
        Button _camSecondaryBtn;
        Text _camSecondaryLabel;
        RectTransform _camSecondaryRect;
        WebCamTexture _webCam;
        bool _webCamMirrored;
        Texture2D _snappedPreviewTex;
        float _permissionWaitElapsed;

        // GameOfMonthSheet refs
        int _displayedMonth;
        RectTransform _monthNavRect;
        RectTransform _prevMonthRect;
        RectTransform _nextMonthRect;
        RectTransform _monthCenterStackRect;
        Text _monthKickerText;
        Button _prevMonthBtn;
        Button _nextMonthBtn;
        Text _monthGameNameText;
        Text _monthFactsText;
        RectTransform _monthListCardRect;
        ScrollRect _monthScroll;
        RectTransform _monthScrollContentRect;
        readonly Text[] _monthParagraphs = new Text[5];

        // DonateSheet refs
        Text _donateThanksText;
        RectTransform _donateGrownBoxRect;
        Text _donateGrownUpsText;
        RectTransform _donateSoonRect;
        Text _donateSoonText;
        RectTransform _donateActionsRow;
        Button _donatePrimaryBtn;
        RectTransform _donatePrimaryRect;
        Text _donatePrimaryLabel;
        Button _donateCancelConfirmBtn;
        RectTransform _donateCancelRect;
        bool _donateAwaitingGrownUpConfirm;
        string _donateUrl = "";

        // Persisted local profile photo state
        int _avatarIndex;
        int _lastSyncedSessionAvatar = -1;
        bool _hasCustomIdPhoto;
        Texture2D _customPhotoTex;
        bool _disposed;

        public RectTransform Root { get; }
        public RectTransform SafeRoot { get; }
        public SheetKind ActiveSheet { get; private set; }
        public CameraStep CurrentCameraStep { get; private set; }
        public bool IsOpen => ActiveSheet != SheetKind.None && Root != null && Root.gameObject.activeSelf;

        public int AvatarIndex => _avatarIndex;
        public string SelectedAvatarId => AvatarIds[Mathf.Clamp(_avatarIndex, 0, AvatarIds.Length - 1)];
        public bool HasCustomIdPhoto => _hasCustomIdPhoto && _customPhotoTex != null;

        public Texture CurrentIdTexture { get; private set; }
        public Rect CurrentIdUvRect { get; private set; } = AvatarCropUv;

        public InputField ProfileNameInput => _profileNameInput;
        public Button ProfileSaveButton => _saveProfileButton;
        public Button HistoryButton { get; private set; }
        public Button ProfileRemovePhotoButton => _removePhotoButton;
        public Button CameraTileButton => _cameraTileButton;
        public Button DonateActionButton => _donatePrimaryBtn;
        public bool HasDonateLink => !string.IsNullOrWhiteSpace(_donateUrl) && _donateUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        public event Action ProfileChanged;

        /// <summary>The profile's MATCH HISTORY button: match history lives in the profile, the Mobile Legends way (docs/SPEC.md §8).</summary>
        public event Action HistoryRequested;

        Transform _homeParent;
        bool _idCameraCloseOnExit;

        public HomeSheetsView(
            Transform parent,
            DataSource data = null,
            AudioHub audio = null,
            Func<bool> isInteractiveProvider = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _data = data;
            _audio = audio;
            _isInteractiveProvider = isInteractiveProvider;
            _canvas = parent.GetComponentInParent<Canvas>();

            Root = Ui.Fill("HomeSheetsModal", parent);
            _homeParent = parent;
            var lifecycle = Root.gameObject.AddComponent<SheetsLifecycle>();
            lifecycle.Owner = this;

            Image scrim = Ui.MakeImage("Scrim", Root, ScrimColor, raycast: true);
            _scrimRect = scrim.rectTransform;
            _scrimRect.anchorMin = Vector2.zero;
            _scrimRect.anchorMax = Vector2.one;
            _scrimRect.sizeDelta = Vector2.zero;
            var catcher = scrim.gameObject.AddComponent<ScrimDismissCatcher>();
            catcher.Owner = this;

            SafeArea existingSafe = parent.GetComponentInParent<SafeArea>();
            if (existingSafe != null)
            {
                _safeArea = existingSafe;
                SafeRoot = Ui.Fill("SafeArea", Root);
            }
            else
            {
                SafeRoot = Ui.Fill("SafeArea", Root);
                _safeArea = SafeRoot.gameObject.AddComponent<SafeArea>();
                _safeArea.Apply();
            }
            _safeArea.Changed += OnSafeAreaChanged;

            BuildModalShell();
            BuildProfileBody();
            BuildIdCameraBody();
            BuildGameOfMonthBody();
            BuildDonateBody();

            LoadSavedProfilePhoto();
            Root.gameObject.SetActive(false);
            ActiveSheet = SheetKind.None;
        }

        bool IsInteractive => _isInteractiveProvider == null || _isInteractiveProvider();

        void OnSafeAreaChanged(SafeArea _) => RefreshLayout();

        void OnSimulatedViewportChanged()
        {
            if (_safeArea != null)
                _safeArea.ForceApply();
            RefreshLayout();
        }

        public void OpenProfile()
        {
            if (!IsInteractive) return;
            StopCamera();
            ActiveSheet = SheetKind.Profile;

            SetHeader("YOUR PLAYER TAG", MangoGold, null);
            ShowOnlyBody(_profileBody);

            if (_profileNameInput != null)
            {
                _profileNameInput.text = AppSession.Current.PlayerName.ToUpperInvariant();
                UpdateLetterCount(_profileNameInput.text);
            }
            _wasNameFocused = false;
            if (_inputFocusOutline != null)
                _inputFocusOutline.color = InputIdleBorder;
            SetProfileHintVisible(false);

            RefreshProfileVisuals();
            PresentModal();
        }

        /// <summary>Opens the ID selfie camera over another full-screen view (e.g. <see cref="ProfileScreenView"/>); saving or backing closes back to that host.</summary>
        public void OpenIdCameraOver(Transform host)
        {
            if (!IsInteractive) return;
            if (host != null && Root != null) Root.SetParent(host, false);
            _idCameraCloseOnExit = true;
            OpenIdCamera();
        }

        public void OpenIdCamera()
        {
            if (!IsInteractive) return;
            ActiveSheet = SheetKind.IdCamera;

            SetHeader("YOUR ID PHOTO", MenuArt.Teal, null);
            ShowOnlyBody(_cameraBody);
            PresentModal();

            if (!Application.isPlaying)
            {
                SetCameraStep(CameraStep.NoCamera);
                RefreshLayout();
                return;
            }

            if (AndroidLocalLink.HasCameraPermission())
                BeginLiveCameraFeed();
            else
                SetCameraStep(CameraStep.Ask);
            RefreshLayout();
        }

        public void OpenGameOfMonth(int month = 0)
        {
            if (!IsInteractive) return;
            StopCamera();
            ActiveSheet = SheetKind.GameOfMonth;

            _displayedMonth = (month >= 1 && month <= 12) ? month : DateTime.Now.Month;
            SetHeader("GAME OF THE MONTH", FiestaOrange, KalyeahResources.Texture("ui/tips_icon"));
            ShowOnlyBody(_monthBody);
            RefreshGameOfMonthContent(_displayedMonth);
            PresentModal();
        }

        public void StepGameOfMonth(int delta)
        {
            if (_displayedMonth < 1 || _displayedMonth > 12)
                _displayedMonth = DateTime.Now.Month;
            int idx = ((_displayedMonth - 1 + delta) % 12 + 12) % 12;
            _displayedMonth = idx + 1;
            _audio?.PlayUiTap(false);
            Haptics.LightTap();
            RefreshGameOfMonthContent(_displayedMonth);
        }

        /// <summary>Opens the donate sheet over another screen (Settings' DONATE tab); <see cref="Close"/> returns it to home.</summary>
        public void OpenDonateOver(Transform host)
        {
            if (host != null && Root != null) Root.SetParent(host, false);
            OpenDonate();
        }

        public void OpenDonate()
        {
            if (!IsInteractive) return;
            StopCamera();
            ActiveSheet = SheetKind.Donate;
            _donateAwaitingGrownUpConfirm = false;

            SetHeader("SUPPORT KALYEAH", JadeGreen, KalyeahResources.Texture("ui/mail_icon"));
            ShowOnlyBody(_donateBody);
            RefreshDonateContent();
            PresentModal();
        }

        public bool Close()
        {
            StopCamera();
            ReleaseSnappedPreview();
            bool wasOpen = IsOpen;
            ActiveSheet = SheetKind.None;
            if (Root != null && Root.gameObject.activeSelf)
                Root.gameObject.SetActive(false);
            if (Root != null && _homeParent != null && Root.parent != _homeParent)
                Root.SetParent(_homeParent, false);
            _idCameraCloseOnExit = false;
            if (wasOpen)
            {
                _audio?.PlayUiTap("cancel");
                Haptics.LightTap();
            }
            return wasOpen;
        }

        public void SelectAvatar(int index)
        {
            int clamped = Mathf.Clamp(index, 0, AvatarIds.Length - 1);
            _avatarIndex = clamped;
            _hasCustomIdPhoto = false;
            DeleteLocalIdPhotoFile();
            ReleaseCustomPhotoTexture();
            UpdateCurrentIdTexture();
            RefreshProfileVisuals();

            int sessionClamped = Mathf.Clamp(_avatarIndex, 0, AppSession.AvatarPhotoIds.Length - 1);
            _lastSyncedSessionAvatar = sessionClamped;
            AppSession.Current.AvatarIndex = sessionClamped;
            AppSession.Current.HasCustomIdPhoto = false;
            PlayerPrefs.SetInt(AvatarPrefKey, _avatarIndex);
            PlayerPrefs.SetInt(CustomPhotoPrefKey, 0);
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);

            RefreshLayout();
            _audio?.PlayUiTap(false);
            Haptics.LightTap();
            ProfileChanged?.Invoke();
        }

        public void RemoveCustomPhoto()
        {
            _hasCustomIdPhoto = false;
            DeleteLocalIdPhotoFile();
            ReleaseCustomPhotoTexture();
            UpdateCurrentIdTexture();
            RefreshProfileVisuals();

            AppSession.Current.HasCustomIdPhoto = false;
            PlayerPrefs.SetInt(CustomPhotoPrefKey, 0);
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);

            RefreshLayout();
            _audio?.PlayUiTap("cancel");
            Haptics.LightTap();
            ProfileChanged?.Invoke();
        }

        public bool SaveProfileName()
        {
            string raw = _profileNameInput != null ? _profileNameInput.text : AppSession.Current.PlayerName;
            string cleaned = AppSession.CleanName((raw ?? "").ToUpperInvariant());
            if (string.IsNullOrEmpty(cleaned))
            {
                if (_profileHintText != null)
                    _profileHintText.text = "Pick a name!";
                SetProfileHintVisible(true);
                _audio?.PlayUiTap("deny");
                Haptics.CancelTick();
                return false;
            }

            AppSession.Current.PlayerName = cleaned;
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);
            _audio?.PlayUiTap("confirm");
            Haptics.LightTap();
            Close();
            ProfileChanged?.Invoke();
            return true;
        }

        public void RefreshLayout()
        {
            if (_modalCard == null) return;
            if (!IsInteractive && IsOpen)
            {
                Close();
                return;
            }

            if (_canvas == null && Root != null)
                _canvas = Root.GetComponentInParent<Canvas>();

            if (_safeArea != null)
                _safeArea.Apply();

            MenuStack.StretchFullBleedToCanvas(_scrimRect, _canvas);

            Vector2 safe = ScreenFitMetrics.SafeCanvasSize(_canvas);
            float safeW = Mathf.Max(640f, safe.x);
            float safeH = Mathf.Max(420f, safe.y);
            float minTouch = ScreenFitMetrics.ClampTouchSize(96f, ScreenFitMetrics.MinTouchDp, _canvas);
            float minGutter = Mathf.Max(14f, ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinGutterDp, _canvas));
            float headerH = Mathf.Max(108f, minTouch + 12f);

            float targetW;
            if (ActiveSheet == SheetKind.Profile)
                targetW = Mathf.Clamp(safeW - 36f, 640f, 1080f);
            else if (ActiveSheet == SheetKind.GameOfMonth)
                targetW = Mathf.Clamp(safeW * 0.76f, 640f, 940f);
            else
                targetW = Mathf.Clamp(safeW * 0.64f, 560f, 780f);
            targetW = Mathf.Min(targetW, safeW - 24f);

            float targetH = ActiveSheet == SheetKind.Donate
                ? Mathf.Clamp(safeH * 0.80f, 460f, 548f)
                : Mathf.Clamp(safeH - 32f, 520f, 648f);
            targetH = Mathf.Min(targetH, safeH - 20f);

            _modalCard.anchoredPosition = Vector2.zero;
            _modalCard.sizeDelta = new Vector2(targetW, targetH);

            if (_headerBandRect != null)
                _headerBandRect.sizeDelta = new Vector2(-12f, headerH);

            if (_closeBtnRect != null)
            {
                _closeBtnRect.anchoredPosition = new Vector2(-6f, 0f);
                _closeBtnRect.sizeDelta = new Vector2(minTouch, minTouch);
            }

            if (_headerTitle != null)
            {
                bool hasIcon = _headerIcon != null && _headerIcon.gameObject.activeSelf;
                _headerTitle.rectTransform.offsetMin = new Vector2(hasIcon ? 74f : 24f, 6f);
                _headerTitle.rectTransform.offsetMax = new Vector2(-(minTouch + 18f), -6f);
            }

            float bodyTopInset = -(headerH + 16f);
            ApplyContentPanelInsets(_profileBody, bodyTopInset);
            ApplyContentPanelInsets(_cameraBody, bodyTopInset);
            ApplyContentPanelInsets(_monthBody, bodyTopInset);
            ApplyContentPanelInsets(_donateBody, bodyTopInset);

            float bodyW = Mathf.Max(480f, targetW - 38f);
            float bodyH = Mathf.Max(300f, targetH - 10f - (headerH + 16f) - 14f);

            LayoutProfileSheet(bodyW, bodyH, minTouch, minGutter);
            LayoutCameraSheet(bodyW, bodyH, minTouch, minGutter);
            LayoutGameOfMonthSheet(bodyW, minTouch);
            LayoutDonateSheet(bodyW, bodyH, minTouch, minGutter);
        }

        static void ApplyContentPanelInsets(RectTransform panel, float topOffset)
        {
            if (panel == null) return;
            panel.offsetMin = new Vector2(14f, 14f);
            panel.offsetMax = new Vector2(-14f, topOffset);
        }

        void LayoutProfileSheet(float bodyW, float bodyH, float minTouch, float minGutter)
        {
            if (_profileBody == null) return;

            if (_saveProfileRect != null)
                _saveProfileRect.sizeDelta = new Vector2(0f, minTouch);
            if (_historyRect != null)
                _historyRect.sizeDelta = new Vector2(0f, minTouch);

            if (_nametagCardRect != null)
            {
                _nametagCardRect.offsetMin = new Vector2(0f, minTouch + 14f);
                _nametagCardRect.offsetMax = Vector2.zero;
            }

            if (_nameFieldBoxRect != null)
            {
                _nameFieldBoxRect.anchoredPosition = new Vector2(0f, 12f);
                _nameFieldBoxRect.sizeDelta = new Vector2(-24f, minTouch);
            }

            if (_nameCaptionRowRect != null)
            {
                _nameCaptionRowRect.anchoredPosition = new Vector2(0f, 12f + minTouch + 6f);
                _nameCaptionRowRect.sizeDelta = new Vector2(-24f, 26f);
            }

            if (_idPhotoFrameRect != null)
            {
                float nametagH = Mathf.Max(220f, bodyH - minTouch - 14f);
                float availPhotoH = nametagH - (12f + minTouch + 38f) - 18f;
                float leftColW = bodyW * 0.35f;
                float photoSide = Mathf.Clamp(Mathf.Min(availPhotoH, leftColW - 32f), 84f, 168f);
                _idPhotoFrameRect.anchoredPosition = new Vector2(0f, -12f);
                _idPhotoFrameRect.sizeDelta = new Vector2(photoSide, photoSide);
            }

            if (_photoFooterRect != null)
            {
                _photoFooterRect.sizeDelta = new Vector2(0f, minTouch);
                bool showRemove = HasCustomIdPhoto;
                if (_removePhotoButton != null)
                    _removePhotoButton.gameObject.SetActive(showRemove);

                if (_cameraTileRect != null)
                {
                    if (showRemove)
                    {
                        float rightW = Mathf.Max(minTouch * 2f + minGutter, bodyW * 0.63f);
                        float halfGapShare = Mathf.Clamp((minGutter * 0.5f) / rightW, 0.015f, 0.06f);
                        _cameraTileRect.anchorMin = new Vector2(0f, 0f);
                        _cameraTileRect.anchorMax = new Vector2(0.54f - halfGapShare, 1f);
                        if (_removePhotoRect != null)
                        {
                            _removePhotoRect.anchorMin = new Vector2(0.54f + halfGapShare, 0f);
                            _removePhotoRect.anchorMax = new Vector2(1f, 1f);
                            _removePhotoRect.offsetMin = Vector2.zero;
                            _removePhotoRect.offsetMax = Vector2.zero;
                        }
                    }
                    else
                    {
                        _cameraTileRect.anchorMin = Vector2.zero;
                        _cameraTileRect.anchorMax = Vector2.one;
                    }
                    _cameraTileRect.offsetMin = Vector2.zero;
                    _cameraTileRect.offsetMax = Vector2.zero;
                }
            }

            if (_photoScrollRect != null && _photoGrid != null && _photoGridContentRect != null)
            {
                _photoScrollRect.offsetMin = new Vector2(0f, minTouch + 14f);
                _photoScrollRect.offsetMax = new Vector2(0f, -36f);

                float rightColW = Mathf.Max(260f, Mathf.Floor(bodyW * 0.63f) - 4f);
                float availGridW = Mathf.Max(minTouch * 2f + 16f, rightColW);
                float availGridH = Mathf.Max(minTouch, bodyH - 36f - (minTouch + 14f) - 4f);
                const float spacing = 10f;

                int cols = (availGridW >= minTouch * 5f + spacing * 4f)
                    ? 5
                    : Mathf.Clamp(Mathf.FloorToInt((availGridW + spacing) / (minTouch + spacing)), 2, 5);
                int rows = Mathf.CeilToInt((float)_avatarTiles.Count / cols);

                float cellByW = Mathf.Floor((availGridW - spacing * (cols - 1)) / cols);
                float cellByH = rows <= 2 ? Mathf.Floor((availGridH - spacing * Mathf.Max(0, rows - 1)) / Mathf.Max(1, rows)) : cellByW;
                float tileSide = Mathf.Max(minTouch, Mathf.Min(cellByW, Mathf.Max(minTouch, Mathf.Min(cellByH, 124f))));

                float contentH = rows * tileSide + Mathf.Max(0, rows - 1) * spacing + 4f;
                _photoGrid.constraintCount = cols;
                _photoGrid.spacing = new Vector2(spacing, spacing);
                _photoGrid.cellSize = new Vector2(tileSide, tileSide);
                _photoGrid.childAlignment = contentH > availGridH + 1f ? TextAnchor.UpperCenter : TextAnchor.MiddleCenter;

                _photoGridContentRect.sizeDelta = new Vector2(0f, Mathf.Max(availGridH, contentH));
                if (ActiveSheet == SheetKind.Profile && Root != null && Root.gameObject.activeInHierarchy)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_photoGridContentRect);
            }
        }

        void LayoutCameraSheet(float bodyW, float bodyH, float minTouch, float minGutter)
        {
            if (_cameraBody == null) return;

            if (_cameraMsgRect != null)
            {
                _cameraMsgRect.anchoredPosition = new Vector2(0f, -4f);
                _cameraMsgRect.sizeDelta = new Vector2(-16f, 64f);
            }

            if (_cameraBtnRowRect != null)
            {
                _cameraBtnRowRect.anchoredPosition = new Vector2(0f, 4f);
                _cameraBtnRowRect.sizeDelta = new Vector2(-16f, minTouch);

                bool hasPrimary = _camPrimaryBtn != null && _camPrimaryBtn.gameObject.activeSelf;
                float rowW = Mathf.Max(minTouch * 2f + minGutter, bodyW - 16f);
                float halfGap = Mathf.Clamp((minGutter * 0.5f) / rowW, 0.015f, 0.06f);
                if (_camSecondaryRect != null)
                {
                    _camSecondaryRect.anchorMin = new Vector2(hasPrimary ? 0f : 0.20f, 0f);
                    _camSecondaryRect.anchorMax = new Vector2(hasPrimary ? (0.5f - halfGap) : 0.80f, 1f);
                    _camSecondaryRect.offsetMin = Vector2.zero;
                    _camSecondaryRect.offsetMax = Vector2.zero;
                }
                if (_camPrimaryRect != null && hasPrimary)
                {
                    _camPrimaryRect.anchorMin = new Vector2(0.5f + halfGap, 0f);
                    _camPrimaryRect.anchorMax = new Vector2(1f, 1f);
                    _camPrimaryRect.offsetMin = Vector2.zero;
                    _camPrimaryRect.offsetMax = Vector2.zero;
                }
            }

            if (_cameraFrameRect != null)
            {
                float topUsed = 74f;
                float bottomUsed = minTouch + 16f;
                float availMidH = Mathf.Max(110f, bodyH - topUsed - bottomUsed);
                float frameSide = Mathf.Clamp(availMidH - 8f, 108f, 256f);
                float midY = (bottomUsed - topUsed) * 0.5f;
                _cameraFrameRect.anchorMin = _cameraFrameRect.anchorMax = new Vector2(0.5f, 0.5f);
                _cameraFrameRect.anchoredPosition = new Vector2(0f, midY);
                _cameraFrameRect.sizeDelta = new Vector2(frameSide, frameSide);
            }
        }

        void LayoutGameOfMonthSheet(float bodyW, float minTouch)
        {
            if (_monthBody == null) return;

            float navH = Mathf.Max(104f, minTouch + 8f);
            if (_monthNavRect != null)
            {
                _monthNavRect.anchoredPosition = new Vector2(0f, -2f);
                _monthNavRect.sizeDelta = new Vector2(-8f, navH);
            }
            if (_prevMonthRect != null)
                _prevMonthRect.sizeDelta = new Vector2(minTouch, minTouch);
            if (_nextMonthRect != null)
                _nextMonthRect.sizeDelta = new Vector2(minTouch, minTouch);
            if (_monthCenterStackRect != null)
            {
                _monthCenterStackRect.offsetMin = new Vector2(minTouch + 16f, 2f);
                _monthCenterStackRect.offsetMax = new Vector2(-(minTouch + 16f), -2f);
            }
            if (_monthListCardRect != null)
            {
                _monthListCardRect.offsetMin = new Vector2(4f, 4f);
                _monthListCardRect.offsetMax = new Vector2(-4f, -(navH + 14f));
            }
            if (_monthScrollContentRect != null && ActiveSheet == SheetKind.GameOfMonth)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_monthScrollContentRect);
            }
        }

        void LayoutDonateSheet(float bodyW, float bodyH, float minTouch, float minGutter)
        {
            if (_donateBody == null) return;

            if (_donateSoonRect != null)
            {
                _donateSoonRect.anchoredPosition = new Vector2(0f, 8f);
                _donateSoonRect.sizeDelta = new Vector2(-24f, minTouch);
            }

            if (_donateActionsRow != null)
            {
                _donateActionsRow.anchoredPosition = new Vector2(0f, 8f);
                _donateActionsRow.sizeDelta = new Vector2(-24f, minTouch);
                RefreshDonateButtonAnchors(Mathf.Max(minTouch * 2f + minGutter, bodyW - 24f), minGutter);
            }

            if (_donateGrownBoxRect != null)
            {
                float topStart = 136f;
                float bottomClear = minTouch + 22f;
                float boxH = Mathf.Clamp(bodyH - topStart - bottomClear, 76f, 128f);
                _donateGrownBoxRect.anchoredPosition = new Vector2(0f, -topStart);
                _donateGrownBoxRect.sizeDelta = new Vector2(-24f, boxH);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_safeArea != null)
            {
                _safeArea.Changed -= OnSafeAreaChanged;
                _safeArea = null;
            }
            StopCamera();
            ReleaseSnappedPreview();
            ReleaseCustomPhotoTexture();
            if (Root != null)
                KalyeahResources.Kill(Root.gameObject);
        }

        // =================================================================================================================
        // Modal Shell & Sheet Builders
        // =================================================================================================================

        void BuildModalShell()
        {
            _modalCard = Ui.Panel(
                "SheetCard",
                SafeRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(880f, 620f));

            Image cardCatch = _modalCard.gameObject.AddComponent<Image>();
            cardCatch.color = Color.clear;
            cardCatch.raycastTarget = true;

            // Keep exact GameObject name "CardCreamFill" (contract in ScreenFitBoundsTests) with upgraded chamfered tactical glass
            RectTransform bodyFill = Ui.Fill("CardCreamFill", _modalCard);
            bodyFill.offsetMin = new Vector2(2f, 2f);
            bodyFill.offsetMax = new Vector2(-2f, -2f);
            Image glassBg = bodyFill.gameObject.AddComponent<Image>();
            glassBg.sprite = MenuArt.ChamferGlass(18);
            glassBg.type = Image.Type.Sliced;
            glassBg.color = Color.white;
            glassBg.raycastTarget = true;

            Ui.MakeCornerBrackets("CardBrackets", _modalCard, new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.52f), expand: 1f);

            _headerBandRect = Ui.Panel(
                "HeaderBand",
                bodyFill,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -6f),
                new Vector2(-12f, 108f));
            Image headerBandImg = _headerBandRect.gameObject.AddComponent<Image>();
            headerBandImg.sprite = MenuArt.ChamferCard(HeaderTop, HeaderBottom, MenuArt.PanelBorder, 12, 1, 0.26f);
            headerBandImg.type = Image.Type.Sliced;
            headerBandImg.color = Color.white;
            headerBandImg.raycastTarget = false;

            RectTransform accentRt = Ui.MakeAccentRail("HeaderAccent", _headerBandRect, MangoGold, width: 4f, verticalInset: 18f);
            _headerAccentRail = accentRt.GetComponent<Image>();

            RectTransform iconBox = Ui.Panel(
                "HeaderIconBox",
                _headerBandRect,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(20f, 0f),
                new Vector2(44f, 44f));
            var iconGo = new GameObject("HeaderIcon", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            iconGo.transform.SetParent(iconBox, false);
            _headerIcon = iconGo.GetComponent<RawImage>();
            _headerIcon.raycastTarget = false;
            Stretch(_headerIcon.rectTransform, 0f, 0f, 0f, 0f);
            var iconFitter = iconGo.GetComponent<AspectRatioFitter>();
            iconFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            iconFitter.aspectRatio = 1f;

            _headerTitle = Ui.MakeText("Title", _headerBandRect, "YOUR PLAYER TAG", 30, MenuArt.Cream, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            _headerTitle.resizeTextForBestFit = true;
            _headerTitle.resizeTextMinSize = 18;
            _headerTitle.resizeTextMaxSize = 30;
            RectTransform tr = _headerTitle.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(24f, 6f);
            tr.offsetMax = new Vector2(-114f, -6f);

            // Sleek chamfered close button inside a >= 48 dp transparent hit catcher
            var closeGo = new GameObject("CloseSheetButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(_headerBandRect, false);
            _closeBtnRect = closeGo.GetComponent<RectTransform>();
            _closeBtnRect.anchorMin = _closeBtnRect.anchorMax = new Vector2(1f, 0.5f);
            _closeBtnRect.pivot = new Vector2(1f, 0.5f);
            _closeBtnRect.anchoredPosition = new Vector2(-6f, 0f);
            _closeBtnRect.sizeDelta = new Vector2(96f, 96f);

            Image closeHit = closeGo.GetComponent<Image>();
            closeHit.color = Color.clear;
            closeHit.raycastTarget = true;

            RectTransform closeFaceRt = Ui.Panel("CloseFace", _closeBtnRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 52f));
            Image closeFaceImg = Ui.MakeSliced("Plate", closeFaceRt, MenuArt.CtaGhost(10), Color.white, raycast: false);

            Image closeGlyph = Ui.MakeImage("CloseGlyph", closeFaceRt, MenuArt.Cream, round: false, raycast: false);
            closeGlyph.sprite = MenuArt.CloseIcon();
            closeGlyph.rectTransform.anchorMin = closeGlyph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            closeGlyph.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            closeGlyph.rectTransform.sizeDelta = new Vector2(22f, 22f);

            _closeBtn = closeGo.GetComponent<Button>();
            _closeBtn.targetGraphic = closeFaceImg;
            _closeBtn.onClick.AddListener(() => Close());
            Ui.AddPressFeel(closeGo, 0.92f);
        }

        void BuildProfileBody()
        {
            _profileBody = MakeContentPanel("ProfileSheet");

            // Left column: School ID Nametag preview + >= 48 dp dark tactical NameFieldBox + >= 48 dp Save & History buttons
            RectTransform leftCol = Ui.Panel("TagPreviewCol", _profileBody, new Vector2(0f, 0f), new Vector2(0.35f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            leftCol.offsetMin = Vector2.zero;
            leftCol.offsetMax = Vector2.zero;

            _nametagCardRect = Ui.Panel("NametagCard", leftCol, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            _nametagCardRect.offsetMin = new Vector2(0f, 110f);
            _nametagCardRect.offsetMax = Vector2.zero;
            MakeTacticalPlate(_nametagCardRect, 12);

            _idPhotoFrameRect = Ui.Panel("IdPhotoFrame", _nametagCardRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(156f, 156f));
            Image photoWell = _idPhotoFrameRect.gameObject.AddComponent<Image>();
            photoWell.sprite = MenuArt.RoundSolid(12);
            photoWell.type = Image.Type.Sliced;
            photoWell.color = MenuArt.InputBg;
            photoWell.raycastTarget = false;

            Image spotlight = Ui.MakeImage("Spotlight", _idPhotoFrameRect, new Color(1f, 0.94f, 0.78f, 0.16f), round: false, raycast: false);
            spotlight.sprite = MenuArt.CardSpotlight();
            Stretch(spotlight.rectTransform, 4f, 4f, -4f, -4f);

            RectTransform previewSlot = Ui.Fill("PreviewSlot", _idPhotoFrameRect);
            previewSlot.offsetMin = new Vector2(4f, 4f);
            previewSlot.offsetMax = new Vector2(-4f, -4f);

            var previewGo = new GameObject("PreviewPhoto", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            previewGo.transform.SetParent(previewSlot, false);
            _profilePreviewImg = previewGo.GetComponent<RawImage>();
            _profilePreviewImg.raycastTarget = false;
            Stretch(_profilePreviewImg.rectTransform, 0f, 0f, 0f, 0f);
            var previewFit = previewGo.GetComponent<AspectRatioFitter>();
            previewFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            previewFit.aspectRatio = 1f;

            Ui.MakeSliced("PhotoOutline", _idPhotoFrameRect, MenuArt.RoundOutline(MenuArt.GoldBorder, 12, 2), Color.white, raycast: false);
            Ui.MakeCornerBrackets("PreviewBrackets", _idPhotoFrameRect, MenuArt.Gold, expand: 2f);

            _nameCaptionRowRect = Ui.Panel("NameCaptionRow", _nametagCardRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 114f), new Vector2(-24f, 26f));

            _nameTagCaptionText = Ui.MakeText("Caption", _nameCaptionRowRect, "PLAYER TAG", 17, MenuArt.GoldLight, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Ui.ClearOutline(_nameTagCaptionText);
            _nameTagCaptionText.resizeTextForBestFit = true;
            _nameTagCaptionText.resizeTextMinSize = 13;
            _nameTagCaptionText.resizeTextMaxSize = 17;
            RectTransform capR = _nameTagCaptionText.rectTransform;
            capR.anchorMin = Vector2.zero;
            capR.anchorMax = new Vector2(0.68f, 1f);
            capR.offsetMin = Vector2.zero;
            capR.offsetMax = Vector2.zero;

            _profileHintText = Ui.MakeText("Hint", _nameCaptionRowRect, "Pick a name!", 17, ErrorRed, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Ui.ClearOutline(_profileHintText);
            _profileHintText.resizeTextForBestFit = true;
            _profileHintText.resizeTextMinSize = 13;
            _profileHintText.resizeTextMaxSize = 17;
            RectTransform hr = _profileHintText.rectTransform;
            hr.anchorMin = Vector2.zero;
            hr.anchorMax = new Vector2(0.68f, 1f);
            hr.offsetMin = Vector2.zero;
            hr.offsetMax = Vector2.zero;
            _profileHintText.gameObject.SetActive(false);

            _profileLetterCount = Ui.MakeText("LetterCount", _nameCaptionRowRect, "4/12", 17, MenuArt.MutedText, TextAnchor.MiddleRight, KalyeahResources.DisplayFont());
            Ui.ClearOutline(_profileLetterCount);
            RectTransform lcR = _profileLetterCount.rectTransform;
            lcR.anchorMin = new Vector2(0.68f, 0f);
            lcR.anchorMax = Vector2.one;
            lcR.offsetMin = Vector2.zero;
            lcR.offsetMax = Vector2.zero;

            // Sleek deep-ink tactical input field with luminous gold border inside >= 48 dp hit target
            _nameFieldBoxRect = Ui.Panel("NameFieldBox", _nametagCardRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(-24f, 96f));
            Image inputHit = _nameFieldBoxRect.gameObject.AddComponent<Image>();
            inputHit.color = Color.clear;
            inputHit.raycastTarget = true;

            RectTransform inputFill = Ui.Panel("InputFill", _nameFieldBoxRect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 58f));
            Image inputBg = inputFill.gameObject.AddComponent<Image>();
            inputBg.raycastTarget = false;

            _inputFocusOutline = Ui.MakeSliced("FocusOutline", inputFill, MenuArt.RoundOutline(Color.white, 12, 2), InputIdleBorder, raycast: false);

            _profileNameInput = _nameFieldBoxRect.gameObject.AddComponent<InputField>();
            _profileNameInput.targetGraphic = inputBg;
            _profileNameInput.characterLimit = AppSession.MaxNameLength;
            _profileNameInput.lineType = InputField.LineType.SingleLine;
            _profileNameInput.caretColor = MenuArt.GoldLight;
            _profileNameInput.selectionColor = new Color(MenuArt.Gold.r, MenuArt.Gold.g, MenuArt.Gold.b, 0.32f);

            Text ph = Ui.MakeText("Placeholder", inputFill, "YOUR NAME", 20, MenuArt.MutedText, TextAnchor.MiddleCenter);
            Ui.ClearOutline(ph);
            Stretch(ph.rectTransform, 12f, 4f, -12f, -4f);

            Text txt = Ui.MakeText("Text", inputFill, AppSession.Current.PlayerName, 26, MenuArt.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            txt.supportRichText = false;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 16;
            txt.resizeTextMaxSize = 26;
            Stretch(txt.rectTransform, 12f, 4f, -12f, -4f);

            Ui.StyleTacticalInput(inputBg, _inputFocusOutline, txt, ph, focused: false);
            Ui.ClearOutline(ph);
            _inputFocusOutline.color = InputIdleBorder;

            _profileNameInput.textComponent = txt;
            _profileNameInput.placeholder = ph;
            _profileNameInput.onValueChanged.AddListener(OnProfileNameChanged);

            _saveProfileButton = MakeActionButton(
                "SaveProfileButton",
                leftCol,
                "Save Name",
                FiestaOrange,
                MenuArt.Ink,
                22,
                () => SaveProfileName(),
                out _saveProfileRect);
            _saveProfileRect.anchorMin = new Vector2(0f, 0f);
            _saveProfileRect.anchorMax = new Vector2(0.49f, 0f);
            _saveProfileRect.pivot = new Vector2(0.5f, 0f);
            _saveProfileRect.anchoredPosition = Vector2.zero;
            _saveProfileRect.sizeDelta = new Vector2(0f, 96f);

            HistoryButton = MakeActionButton(
                "HistoryButton",
                leftCol,
                "Match History",
                BarrioBlue,
                MenuArt.Cream,
                22,
                () => HistoryRequested?.Invoke(),
                out _historyRect);
            _historyRect.anchorMin = new Vector2(0.51f, 0f);
            _historyRect.anchorMax = new Vector2(1f, 0f);
            _historyRect.pivot = new Vector2(0.5f, 0f);
            _historyRect.anchoredPosition = Vector2.zero;
            _historyRect.sizeDelta = new Vector2(0f, 96f);

            // Right column: All 12 kid avatar tiles (>= 48 dp inside ScrollRect) + >= 48 dp ID Camera / Remove Photo footer
            RectTransform rightCol = Ui.Panel("PhotoOptionsCol", _profileBody, new Vector2(0.37f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            rightCol.offsetMin = Vector2.zero;
            rightCol.offsetMax = Vector2.zero;

            Text gridTitle = Ui.MakeText("GridHeading", rightCol, "CHOOSE KID AVATAR (12) OR SELFIE (ON-DEVICE ONLY)", 18, MenuArt.GoldLight, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            Ui.ClearOutline(gridTitle);
            gridTitle.resizeTextForBestFit = true;
            gridTitle.resizeTextMinSize = 14;
            gridTitle.resizeTextMaxSize = 18;
            RectTransform gtR = gridTitle.rectTransform;
            gtR.anchorMin = new Vector2(0f, 1f);
            gtR.anchorMax = new Vector2(1f, 1f);
            gtR.pivot = new Vector2(0.5f, 1f);
            gtR.anchoredPosition = Vector2.zero;
            gtR.sizeDelta = new Vector2(0f, 30f);

            _photoScrollRect = Ui.Panel("PhotoOptionsScroll", rightCol, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _photoScrollRect.offsetMin = new Vector2(0f, 110f);
            _photoScrollRect.offsetMax = new Vector2(0f, -36f);
            Image scrollCatch = _photoScrollRect.gameObject.AddComponent<Image>();
            scrollCatch.color = new Color(0f, 0f, 0f, 0.001f);
            scrollCatch.raycastTarget = true;
            _photoScrollRect.gameObject.AddComponent<RectMask2D>();

            _photoGridContentRect = Ui.Panel("PhotoOptions", _photoScrollRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 240f));
            _photoGrid = _photoGridContentRect.gameObject.AddComponent<GridLayoutGroup>();
            _photoGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _photoGrid.constraintCount = 5;
            _photoGrid.cellSize = new Vector2(106f, 106f);
            _photoGrid.spacing = new Vector2(10f, 10f);
            _photoGrid.childAlignment = TextAnchor.MiddleCenter;

            _photoScroll = _photoScrollRect.gameObject.AddComponent<ScrollRect>();
            _photoScroll.viewport = _photoScrollRect;
            _photoScroll.content = _photoGridContentRect;
            _photoScroll.horizontal = false;
            _photoScroll.vertical = true;
            _photoScroll.movementType = ScrollRect.MovementType.Clamped;
            _photoScroll.scrollSensitivity = 28f;

            _avatarTiles.Clear();
            for (int i = 0; i < AvatarIds.Length; i++)
                _avatarTiles.Add(BuildAvatarTile(_photoGridContentRect, AvatarIds[i], i));

            _photoFooterRect = Ui.Panel("PhotoFooterRow", rightCol, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 96f));

            _cameraTileButton = MakeActionButton(
                "CameraTile",
                _photoFooterRect,
                "ID Camera \u00B7 Take Selfie",
                BarrioBlue,
                MenuArt.Cream,
                21,
                OpenIdCamera,
                out _cameraTileRect,
                MenuArt.GlyphKind.Camera);
            _cameraTileRect.anchorMin = Vector2.zero;
            _cameraTileRect.anchorMax = Vector2.one;
            _cameraTileRect.offsetMin = Vector2.zero;
            _cameraTileRect.offsetMax = Vector2.zero;

            _removePhotoButton = MakeActionButton(
                "RemovePhotoButton",
                _photoFooterRect,
                "Remove Custom Photo",
                SlateBtn,
                MenuArt.Cream,
                19,
                RemoveCustomPhoto,
                out _removePhotoRect);
            _removePhotoRect.anchorMin = new Vector2(0.56f, 0f);
            _removePhotoRect.anchorMax = Vector2.one;
            _removePhotoRect.offsetMin = Vector2.zero;
            _removePhotoRect.offsetMax = Vector2.zero;
        }

        AvatarTileUi BuildAvatarTile(Transform parent, string avatarId, int index)
        {
            var go = new GameObject("Avatar_" + avatarId, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var frame = go.GetComponent<Image>();
            frame.sprite = MenuArt.Round(TileTop, TileBottom, MenuArt.PanelBorder, 10, 2, 0.20f);
            frame.type = Image.Type.Sliced;
            frame.color = Color.white;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = frame;
            int captured = index;
            btn.onClick.AddListener(() => SelectAvatar(captured));
            Ui.AddPressFeel(go, 0.94f);

            RectTransform inner = Ui.Fill("AvatarWell", go.transform);
            inner.offsetMin = new Vector2(4f, 4f);
            inner.offsetMax = new Vector2(-4f, -4f);

            var rawGo = new GameObject("Picture", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            rawGo.transform.SetParent(inner, false);
            var raw = rawGo.GetComponent<RawImage>();
            raw.texture = LoadAvatarTexture(avatarId, out Rect uv);
            raw.uvRect = uv;
            raw.raycastTarget = false;
            Stretch(raw.rectTransform, 0f, 0f, 0f, 0f);
            var fit = rawGo.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1f;

            // Transparent-center luminous gold frame + corner brackets + check medallion (zero veil over avatar face!)
            var ringGo = new GameObject("Ring", typeof(RectTransform), typeof(Image));
            ringGo.transform.SetParent(go.transform, false);
            var ring = ringGo.GetComponent<Image>();
            ring.sprite = MenuArt.RoundOutline(MenuArt.GoldBorder, 10, 3);
            ring.type = Image.Type.Sliced;
            ring.color = Color.white;
            ring.raycastTarget = false;
            Stretch(ring.rectTransform, 0f, 0f, 0f, 0f);

            Ui.MakeCornerBrackets("Brackets", ringGo.transform, MenuArt.GoldLight);

            Image checkBadge = Ui.MakeImage("SelectedDot", ringGo.transform, Color.white, round: false, raycast: false);
            checkBadge.sprite = MenuArt.CheckBadge();
            RectTransform checkRt = checkBadge.rectTransform;
            checkRt.anchorMin = checkRt.anchorMax = new Vector2(1f, 1f);
            checkRt.pivot = new Vector2(1f, 1f);
            checkRt.anchoredPosition = new Vector2(-4f, -4f);
            checkRt.sizeDelta = new Vector2(26f, 26f);

            return new AvatarTileUi
            {
                Id = avatarId,
                Index = index,
                Button = btn,
                Rect = go.GetComponent<RectTransform>(),
                Ring = ring,
            };
        }

        void BuildIdCameraBody()
        {
            _cameraBody = MakeContentPanel("IdCameraSheet");

            _cameraMessageText = Ui.MakeText(
                "Message",
                _cameraBody,
                "Smile! Kalyeah uses the camera for your ID photo. It stays on this phone only (never uploaded).",
                20,
                Espresso,
                TextAnchor.MiddleCenter);
            _cameraMessageText.fontStyle = FontStyle.Bold;
            Ui.ClearOutline(_cameraMessageText);
            _cameraMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _cameraMessageText.verticalOverflow = VerticalWrapMode.Truncate;
            _cameraMessageText.resizeTextForBestFit = true;
            _cameraMessageText.resizeTextMinSize = 12;
            _cameraMessageText.resizeTextMaxSize = 20;
            _cameraMsgRect = _cameraMessageText.rectTransform;
            _cameraMsgRect.anchorMin = new Vector2(0f, 1f);
            _cameraMsgRect.anchorMax = new Vector2(1f, 1f);
            _cameraMsgRect.pivot = new Vector2(0.5f, 1f);
            _cameraMsgRect.anchoredPosition = new Vector2(0f, -4f);
            _cameraMsgRect.sizeDelta = new Vector2(-16f, 64f);

            _cameraFrameRect = Ui.Panel("Frame", _cameraBody, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 220f));
            Image frameBg = _cameraFrameRect.gameObject.AddComponent<Image>();
            frameBg.sprite = MenuArt.ChamferGlass(12);
            frameBg.type = Image.Type.Sliced;
            frameBg.color = Color.white;
            frameBg.raycastTarget = false;

            var previewGo = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            previewGo.transform.SetParent(_cameraFrameRect, false);
            _cameraPreviewRaw = previewGo.GetComponent<RawImage>();
            _cameraPreviewRaw.raycastTarget = false;
            Stretch(_cameraPreviewRaw.rectTransform, 6f, 6f, -6f, -6f);

            Ui.MakeCornerBrackets("CamBrackets", _cameraFrameRect, MenuArt.Gold, expand: 1f);

            _cameraBtnRowRect = Ui.Panel("Buttons", _cameraBody, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(-16f, 96f));

            _camSecondaryBtn = MakeActionButton(
                "BackOrRetakeButton",
                _cameraBtnRowRect,
                "Back to Avatars",
                SlateBtn,
                MenuArt.Cream,
                22,
                OnCameraSecondaryClicked,
                out _camSecondaryRect);
            _camSecondaryRect.anchorMin = new Vector2(0f, 0f);
            _camSecondaryRect.anchorMax = new Vector2(0.48f, 1f);
            _camSecondaryRect.offsetMin = Vector2.zero;
            _camSecondaryRect.offsetMax = Vector2.zero;
            _camSecondaryLabel = _camSecondaryBtn.GetComponentInChildren<Text>();

            _camPrimaryBtn = MakeActionButton(
                "CameraPrimaryButton",
                _cameraBtnRowRect,
                "Allow Camera",
                FiestaOrange,
                MenuArt.Ink,
                22,
                OnCameraPrimaryClicked,
                out _camPrimaryRect);
            _camPrimaryRect.anchorMin = new Vector2(0.52f, 0f);
            _camPrimaryRect.anchorMax = new Vector2(1f, 1f);
            _camPrimaryRect.offsetMin = Vector2.zero;
            _camPrimaryRect.offsetMax = Vector2.zero;
            _camPrimaryLabel = _camPrimaryBtn.GetComponentInChildren<Text>();
        }

        void BuildGameOfMonthBody()
        {
            _monthBody = MakeContentPanel("GameOfMonthSheet");

            _monthNavRect = Ui.Panel("MonthHeaderRow", _monthBody, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(-8f, 104f));
            _prevMonthBtn = MakeNavChevronButton("PrevMonth", _monthNavRect, flipLeft: true, () => StepGameOfMonth(-1), out _prevMonthRect);
            _prevMonthRect.anchorMin = _prevMonthRect.anchorMax = new Vector2(0f, 0.5f);
            _prevMonthRect.pivot = new Vector2(0f, 0.5f);
            _prevMonthRect.anchoredPosition = Vector2.zero;
            _prevMonthRect.sizeDelta = new Vector2(96f, 96f);

            _nextMonthBtn = MakeNavChevronButton("NextMonth", _monthNavRect, flipLeft: false, () => StepGameOfMonth(1), out _nextMonthRect);
            _nextMonthRect.anchorMin = _nextMonthRect.anchorMax = new Vector2(1f, 0.5f);
            _nextMonthRect.pivot = new Vector2(1f, 0.5f);
            _nextMonthRect.anchoredPosition = Vector2.zero;
            _nextMonthRect.sizeDelta = new Vector2(96f, 96f);

            _monthCenterStackRect = Ui.Fill("MonthTitleStack", _monthNavRect);
            _monthCenterStackRect.offsetMin = new Vector2(112f, 2f);
            _monthCenterStackRect.offsetMax = new Vector2(-112f, -2f);

            _monthKickerText = Ui.MakeText("Month", _monthCenterStackRect, "JANUARY", 18, DarkOchre, TextAnchor.UpperCenter, KalyeahResources.DisplayFont());
            Ui.ClearOutline(_monthKickerText);
            RectTransform kr = _monthKickerText.rectTransform;
            kr.anchorMin = new Vector2(0f, 0.72f);
            kr.anchorMax = Vector2.one;
            kr.offsetMin = Vector2.zero;
            kr.offsetMax = Vector2.zero;

            _monthGameNameText = Ui.MakeText("GameName", _monthCenterStackRect, "Patintero", 28, Espresso, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Ui.ClearOutline(_monthGameNameText);
            _monthGameNameText.resizeTextForBestFit = true;
            _monthGameNameText.resizeTextMinSize = 15;
            _monthGameNameText.resizeTextMaxSize = 28;
            RectTransform gnR = _monthGameNameText.rectTransform;
            gnR.anchorMin = new Vector2(0f, 0.28f);
            gnR.anchorMax = new Vector2(1f, 0.74f);
            gnR.offsetMin = Vector2.zero;
            gnR.offsetMax = Vector2.zero;

            _monthFactsText = Ui.MakeText("Facts", _monthCenterStackRect, "", 18, MenuArt.MutedText, TextAnchor.LowerCenter);
            Ui.ClearOutline(_monthFactsText);
            _monthFactsText.resizeTextForBestFit = true;
            _monthFactsText.resizeTextMinSize = 13;
            _monthFactsText.resizeTextMaxSize = 18;
            RectTransform fr = _monthFactsText.rectTransform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = new Vector2(1f, 0.28f);
            fr.offsetMin = Vector2.zero;
            fr.offsetMax = Vector2.zero;

            _monthListCardRect = Ui.Panel("RulesListCard", _monthBody, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _monthListCardRect.offsetMin = new Vector2(4f, 4f);
            _monthListCardRect.offsetMax = new Vector2(-4f, -118f);
            MakeTacticalPlate(_monthListCardRect, 12);
            Ui.MakeAccentRail("RulesRail", _monthListCardRect, MenuArt.Gold, width: 4f, verticalInset: 14f);

            RectTransform scrollViewport = Ui.Fill("RulesViewport", _monthListCardRect);
            scrollViewport.offsetMin = new Vector2(8f, 4f);
            scrollViewport.offsetMax = new Vector2(-4f, -4f);
            Image vpImg = scrollViewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0.001f);
            vpImg.raycastTarget = true;
            scrollViewport.gameObject.AddComponent<RectMask2D>();

            _monthScrollContentRect = Ui.Panel("RulesContent", scrollViewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            var vlg = _monthScrollContentRect.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(18, 18, 14, 14);
            vlg.spacing = 12f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = _monthScrollContentRect.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _monthScroll = _monthListCardRect.gameObject.AddComponent<ScrollRect>();
            _monthScroll.viewport = scrollViewport;
            _monthScroll.content = _monthScrollContentRect;
            _monthScroll.horizontal = false;
            _monthScroll.vertical = true;
            _monthScroll.movementType = ScrollRect.MovementType.Clamped;
            _monthScroll.scrollSensitivity = 28f;

            string[] rowNames = { "YouNeed", "SetUp", "Play", "Win", "StaySafe" };
            for (int i = 0; i < 5; i++)
            {
                Text p = Ui.MakeText(rowNames[i], _monthScrollContentRect, "", 20, Espresso, TextAnchor.UpperLeft);
                p.fontStyle = FontStyle.Bold;
                Ui.ClearOutline(p);
                p.supportRichText = true;
                p.lineSpacing = 1.14f;
                p.horizontalOverflow = HorizontalWrapMode.Wrap;
                p.verticalOverflow = VerticalWrapMode.Overflow;
                _monthParagraphs[i] = p;
            }
        }

        void BuildDonateBody()
        {
            _donateBody = MakeContentPanel("DonateSheet");

            RectTransform heartWell = Ui.Panel("Heart", _donateBody, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(54f, 54f));
            Ui.MakeSliced("WellBg", heartWell, MenuArt.StatChip(12), Color.white, raycast: false);
            Ui.MakeSliced("WellRim", heartWell, MenuArt.ChamferOutline(MenuArt.GoldBorder, 12, 1), Color.white, raycast: false);
            Image heartGlyph = Ui.MakeImage("HeartGlyph", heartWell, MenuArt.Coral, round: false, raycast: false);
            heartGlyph.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Heart);
            heartGlyph.rectTransform.anchorMin = heartGlyph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            heartGlyph.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            heartGlyph.rectTransform.sizeDelta = new Vector2(26f, 26f);

            _donateThanksText = Ui.MakeText("Thanks", _donateBody, "Love Kalyeah? Chip in and help it grow!", 24, Espresso, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Ui.ClearOutline(_donateThanksText);
            _donateThanksText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _donateThanksText.verticalOverflow = VerticalWrapMode.Truncate;
            _donateThanksText.resizeTextForBestFit = true;
            _donateThanksText.resizeTextMinSize = 12;
            _donateThanksText.resizeTextMaxSize = 24;
            RectTransform tr = _donateThanksText.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(0f, -64f);
            tr.sizeDelta = new Vector2(-24f, 64f);

            _donateGrownBoxRect = Ui.Panel("GrownUpsNote", _donateBody, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(-24f, 96f));
            MakeTacticalPlate(_donateGrownBoxRect, 12);
            Ui.MakeAccentRail("GrownUpsRail", _donateGrownBoxRect, MenuArt.Gold, width: 4f, verticalInset: 14f);

            _donateGrownUpsText = Ui.MakeText("GrownUpsText", _donateGrownBoxRect, "Ask a grown-up first. Gifts give nothing in the game.", 20, Espresso, TextAnchor.MiddleCenter);
            _donateGrownUpsText.fontStyle = FontStyle.Bold;
            Ui.ClearOutline(_donateGrownUpsText);
            _donateGrownUpsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _donateGrownUpsText.verticalOverflow = VerticalWrapMode.Truncate;
            _donateGrownUpsText.resizeTextForBestFit = true;
            _donateGrownUpsText.resizeTextMinSize = 12;
            _donateGrownUpsText.resizeTextMaxSize = 20;
            Stretch(_donateGrownUpsText.rectTransform, 22f, 8f, -18f, -8f);

            _donateSoonText = Ui.MakeText("Soon", _donateBody, "Coming soon!", 24, DarkOchre, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Ui.ClearOutline(_donateSoonText);
            _donateSoonRect = _donateSoonText.rectTransform;
            _donateSoonRect.anchorMin = new Vector2(0f, 0f);
            _donateSoonRect.anchorMax = new Vector2(1f, 0f);
            _donateSoonRect.pivot = new Vector2(0.5f, 0f);
            _donateSoonRect.anchoredPosition = new Vector2(0f, 8f);
            _donateSoonRect.sizeDelta = new Vector2(-24f, 96f);

            _donateActionsRow = Ui.Panel("DonateActions", _donateBody, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(-24f, 96f));

            _donateCancelConfirmBtn = MakeActionButton(
                "CancelDonateButton",
                _donateActionsRow,
                "Cancel",
                SlateBtn,
                MenuArt.Cream,
                22,
                () =>
                {
                    _donateAwaitingGrownUpConfirm = false;
                    RefreshDonateButtons();
                    RefreshLayout();
                },
                out _donateCancelRect);
            _donateCancelRect.anchorMin = new Vector2(0f, 0f);
            _donateCancelRect.anchorMax = new Vector2(0.42f, 1f);
            _donateCancelRect.offsetMin = Vector2.zero;
            _donateCancelRect.offsetMax = Vector2.zero;

            _donatePrimaryBtn = MakeActionButton(
                "DonateButton",
                _donateActionsRow,
                "Donate",
                FiestaOrange,
                MenuArt.Ink,
                22,
                OnDonatePrimaryClicked,
                out _donatePrimaryRect);
            _donatePrimaryRect.anchorMin = new Vector2(0.46f, 0f);
            _donatePrimaryRect.anchorMax = new Vector2(1f, 1f);
            _donatePrimaryRect.offsetMin = Vector2.zero;
            _donatePrimaryRect.offsetMax = Vector2.zero;
            _donatePrimaryLabel = _donatePrimaryBtn.GetComponentInChildren<Text>();
        }

        // =================================================================================================================
        // Content & Camera State Logic
        // =================================================================================================================

        void LoadSavedProfilePhoto()
        {
            int fallbackAvatar = AppSession.Current.AvatarIndex;
            bool fallbackCustom = AppSession.Current.HasCustomIdPhoto;
            _avatarIndex = Mathf.Clamp(
                PlayerPrefs.HasKey(AvatarPrefKey) ? PlayerPrefs.GetInt(AvatarPrefKey, fallbackAvatar) : fallbackAvatar,
                0,
                AvatarIds.Length - 1);
            _hasCustomIdPhoto = PlayerPrefs.HasKey(CustomPhotoPrefKey)
                ? PlayerPrefs.GetInt(CustomPhotoPrefKey, 0) == 1
                : fallbackCustom;

            if (_hasCustomIdPhoto)
            {
                if (!TryLoadLocalIdPhotoFromDisk())
                {
                    _hasCustomIdPhoto = false;
                    PlayerPrefs.SetInt(CustomPhotoPrefKey, 0);
                }
            }

            int sessionClamped = Mathf.Clamp(_avatarIndex, 0, AppSession.AvatarPhotoIds.Length - 1);
            _lastSyncedSessionAvatar = sessionClamped;
            AppSession.Current.AvatarIndex = sessionClamped;
            AppSession.Current.HasCustomIdPhoto = _hasCustomIdPhoto;
            UpdateCurrentIdTexture();
        }

        public void SyncFromSession()
        {
            int sessionAvatar = Mathf.Clamp(AppSession.Current.AvatarIndex, 0, AppSession.AvatarPhotoIds.Length - 1);
            bool sessionCustom = AppSession.Current.HasCustomIdPhoto;

            bool externalAvatarChanged = sessionAvatar != _lastSyncedSessionAvatar;
            if (!externalAvatarChanged && sessionCustom == _hasCustomIdPhoto && CurrentIdTexture != null)
                return;

            if (externalAvatarChanged)
            {
                _avatarIndex = sessionAvatar;
                _lastSyncedSessionAvatar = sessionAvatar;
            }
            if (sessionCustom && _customPhotoTex == null)
                sessionCustom = TryLoadLocalIdPhotoFromDisk();
            _hasCustomIdPhoto = sessionCustom;
            UpdateCurrentIdTexture();
            if (ActiveSheet == SheetKind.Profile)
                RefreshProfileVisuals();
        }

        bool TryLoadLocalIdPhotoFromDisk()
        {
            try
            {
                string path = LocalIdPhotoPath;
                if (!File.Exists(path)) return false;
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes == null || bytes.Length == 0) return false;

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "KalyeahLocalIdPhoto",
                    wrapMode = TextureWrapMode.Clamp,
                };
                if (!ImageConversion.LoadImage(tex, bytes))
                {
                    KalyeahResources.Kill(tex);
                    return false;
                }

                ReleaseCustomPhotoTexture();
                _customPhotoTex = tex;
                return true;
            }
            catch
            {
                return false;
            }
        }

        void UpdateCurrentIdTexture()
        {
            if (_hasCustomIdPhoto && _customPhotoTex != null)
            {
                CurrentIdTexture = _customPhotoTex;
                CurrentIdUvRect = FullUv;
                return;
            }

            string id = AvatarIds[Mathf.Clamp(_avatarIndex, 0, AvatarIds.Length - 1)];
            CurrentIdTexture = LoadAvatarTexture(id, out Rect uv);
            CurrentIdUvRect = uv;
        }

        static Texture2D LoadAvatarTexture(string avatarId, out Rect uvRect)
        {
            Texture2D avatarMat = KalyeahResources.Texture("ui/avatars/" + avatarId);
            if (avatarMat != null)
            {
                uvRect = AvatarCropUv;
                return avatarMat;
            }

            Texture2D face = KalyeahResources.Texture("portraits/faces/" + avatarId);
            if (face != null)
            {
                uvRect = FullUv;
                return face;
            }

            Texture2D portrait = KalyeahResources.Texture("portraits/" + avatarId);
            uvRect = portrait != null && portrait.height > portrait.width
                ? new Rect(0f, 1f - (float)portrait.width / portrait.height, 1f, (float)portrait.width / portrait.height)
                : FullUv;
            return portrait;
        }

        void RefreshProfileVisuals()
        {
            if (_profilePreviewImg != null)
            {
                _profilePreviewImg.texture = CurrentIdTexture;
                _profilePreviewImg.uvRect = CurrentIdUvRect;
                _profilePreviewImg.enabled = CurrentIdTexture != null;
            }

            for (int i = 0; i < _avatarTiles.Count; i++)
            {
                AvatarTileUi tile = _avatarTiles[i];
                bool selected = !HasCustomIdPhoto && tile.Index == _avatarIndex;
                if (tile.Ring != null)
                    tile.Ring.gameObject.SetActive(selected);
            }

            if (_removePhotoButton != null)
                _removePhotoButton.gameObject.SetActive(HasCustomIdPhoto);
        }

        void SetProfileHintVisible(bool showHint)
        {
            if (_profileHintText != null)
                _profileHintText.gameObject.SetActive(showHint);
            if (_nameTagCaptionText != null)
                _nameTagCaptionText.gameObject.SetActive(!showHint);
        }

        void OnProfileNameChanged(string typed)
        {
            string filtered = DisallowedNameChars.Replace(typed ?? "", "").ToUpperInvariant();
            if (filtered.Length > AppSession.MaxNameLength)
                filtered = filtered.Substring(0, AppSession.MaxNameLength);

            if (_profileNameInput != null && !string.Equals(_profileNameInput.text, filtered, StringComparison.Ordinal))
                _profileNameInput.SetTextWithoutNotify(filtered);

            SetProfileHintVisible(false);
            UpdateLetterCount(filtered);
        }

        void UpdateLetterCount(string text)
        {
            if (_profileLetterCount != null)
            {
                int len = string.IsNullOrEmpty(text) ? 0 : text.Length;
                _profileLetterCount.text = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", len, AppSession.MaxNameLength);
            }
        }

        void SetCameraStep(CameraStep step)
        {
            CurrentCameraStep = step;
            bool showFrame = step == CameraStep.Looking || step == CameraStep.Snapped;
            if (_cameraFrameRect != null)
                _cameraFrameRect.gameObject.SetActive(showFrame);

            switch (step)
            {
                case CameraStep.Ask:
                    _cameraMessageText.text = "Smile! Kalyeah uses the camera for your ID photo. It stays on this phone only (never uploaded).";
                    _camSecondaryLabel.text = "Back to Avatars";
                    _camPrimaryBtn.gameObject.SetActive(true);
                    _camPrimaryLabel.text = "Allow Camera";
                    break;
                case CameraStep.Waiting:
                    _cameraMessageText.text = "Waiting for your camera permission...";
                    _camSecondaryLabel.text = "Back to Avatars";
                    _camPrimaryBtn.gameObject.SetActive(false);
                    break;
                case CameraStep.Looking:
                    _cameraMessageText.text = "Fit your face in the square!";
                    _camSecondaryLabel.text = "Back to Avatars";
                    _camPrimaryBtn.gameObject.SetActive(true);
                    _camPrimaryLabel.text = "Snap!";
                    break;
                case CameraStep.Snapped:
                    _cameraMessageText.text = "Looking good! Keep this photo on this phone?";
                    _camSecondaryLabel.text = "Retake";
                    _camPrimaryBtn.gameObject.SetActive(true);
                    _camPrimaryLabel.text = "Use Photo";
                    break;
                case CameraStep.Denied:
                    _cameraMessageText.text = "Camera access is off. Pick any of the 10 kid avatars or allow Camera in app settings.";
                    _camSecondaryLabel.text = "Back to Avatars";
                    _camPrimaryBtn.gameObject.SetActive(false);
                    break;
                case CameraStep.NoCamera:
                    _cameraMessageText.text = "No camera found on this device. Pick one of the 10 kid avatars!";
                    _camSecondaryLabel.text = "Back to Avatars";
                    _camPrimaryBtn.gameObject.SetActive(false);
                    break;
            }
        }

        void OnCameraSecondaryClicked()
        {
            if (CurrentCameraStep == CameraStep.Snapped)
            {
                ReleaseSnappedPreview();
                BeginLiveCameraFeed();
                RefreshLayout();
                return;
            }

            StopCamera();
            if (_idCameraCloseOnExit)
                Close();
            else
                OpenProfile();
        }

        void OnCameraPrimaryClicked()
        {
            if (CurrentCameraStep == CameraStep.Ask)
            {
                _permissionWaitElapsed = 0f;
                SetCameraStep(CameraStep.Waiting);
                RefreshLayout();
                AndroidLocalLink.RequestCameraPermission();
            }
            else if (CurrentCameraStep == CameraStep.Looking)
            {
                SnapSelfieFrame();
                RefreshLayout();
            }
            else if (CurrentCameraStep == CameraStep.Snapped)
            {
                SaveSnappedSelfieLocally();
            }
        }

        void BeginLiveCameraFeed()
        {
            StopCamera();
            ReleaseSnappedPreview();

            WebCamDevice[] devices;
            try
            {
                devices = WebCamTexture.devices;
            }
            catch
            {
                devices = Array.Empty<WebCamDevice>();
            }

            if (devices == null || devices.Length == 0)
            {
                SetCameraStep(CameraStep.NoCamera);
                return;
            }

            int chosenIdx = 0;
            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i].isFrontFacing)
                {
                    chosenIdx = i;
                    break;
                }
            }

            try
            {
                _webCamMirrored = devices[chosenIdx].isFrontFacing;
                _webCam = new WebCamTexture(devices[chosenIdx].name, 480, 480, 15);
                _webCam.Play();

                if (_cameraPreviewRaw != null)
                {
                    _cameraPreviewRaw.texture = _webCam;
                    _cameraPreviewRaw.uvRect = _webCamMirrored ? new Rect(1f, 0f, -1f, 1f) : FullUv;
                    _cameraPreviewRaw.rectTransform.localEulerAngles = Vector3.zero;
                }

                SetCameraStep(CameraStep.Looking);
            }
            catch
            {
                StopCamera();
                SetCameraStep(CameraStep.NoCamera);
            }
        }

        void SnapSelfieFrame()
        {
            if (_webCam == null || !_webCam.isPlaying || _webCam.width <= 16 || _webCam.height <= 16)
                return;

            int srcW = _webCam.width;
            int srcH = _webCam.height;
            Color32[] src = _webCam.GetPixels32();
            int rotCw = ((_webCam.videoRotationAngle % 360) + 360) % 360;
            bool vertMirrored = _webCam.videoVerticallyMirrored;
            bool mirrorX = _webCamMirrored;

            var dst = new Color32[IdPhotoSize * IdPhotoSize];
            int cropSide = Mathf.Min(srcW, srcH);
            int offX = (srcW - cropSide) / 2;
            int offY = (srcH - cropSide) / 2;

            for (int y = 0; y < IdPhotoSize; y++)
            {
                float baseV = (y + 0.5f) / IdPhotoSize;
                for (int x = 0; x < IdPhotoSize; x++)
                {
                    float u = (x + 0.5f) / IdPhotoSize;
                    float v = baseV;
                    if (mirrorX) u = 1f - u;
                    if (vertMirrored) v = 1f - v;

                    float ru = u;
                    float rv = v;
                    if (rotCw == 90) { ru = v; rv = 1f - u; }
                    else if (rotCw == 180) { ru = 1f - u; rv = 1f - v; }
                    else if (rotCw == 270) { ru = 1f - v; rv = u; }

                    int sx = Mathf.Clamp(offX + Mathf.FloorToInt(ru * cropSide), 0, srcW - 1);
                    int sy = Mathf.Clamp(offY + Mathf.FloorToInt(rv * cropSide), 0, srcH - 1);
                    dst[y * IdPhotoSize + x] = src[sy * srcW + sx];
                }
            }

            StopCamera();
            ReleaseSnappedPreview();

            _snappedPreviewTex = new Texture2D(IdPhotoSize, IdPhotoSize, TextureFormat.RGBA32, false)
            {
                name = "KalyeahSnappedIdPreview",
                wrapMode = TextureWrapMode.Clamp,
            };
            _snappedPreviewTex.SetPixels32(dst);
            _snappedPreviewTex.Apply(false, false);

            if (_cameraPreviewRaw != null)
            {
                _cameraPreviewRaw.texture = _snappedPreviewTex;
                _cameraPreviewRaw.uvRect = FullUv;
                _cameraPreviewRaw.rectTransform.localEulerAngles = Vector3.zero;
            }

            _audio?.PlayUiTap("confirm");
            Haptics.LightTap();
            SetCameraStep(CameraStep.Snapped);
        }

        void SaveSnappedSelfieLocally()
        {
            if (_snappedPreviewTex == null) return;
            try
            {
                byte[] png = ImageConversion.EncodeToPNG(_snappedPreviewTex);
                if (png == null || png.Length == 0) return;

                string path = LocalIdPhotoPath;
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, png);

                ReleaseCustomPhotoTexture();
                _customPhotoTex = _snappedPreviewTex;
                _snappedPreviewTex = null;
                _hasCustomIdPhoto = true;
                UpdateCurrentIdTexture();

                AppSession.Current.HasCustomIdPhoto = true;
                PlayerPrefs.SetInt(CustomPhotoPrefKey, 1);
                MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);

                _audio?.PlayUiTap("ready");
                Haptics.LightTap();
                ProfileChanged?.Invoke();
                if (_idCameraCloseOnExit)
                    Close();
                else
                    OpenProfile();
            }
            catch
            {
                _audio?.PlayUiTap("deny");
            }
        }

        void Tick()
        {
            if (!IsOpen) return;

            if (ActiveSheet == SheetKind.Profile && _profileNameInput != null && _inputFocusOutline != null)
            {
                bool focused = _profileNameInput.isFocused;
                if (focused != _wasNameFocused)
                {
                    _wasNameFocused = focused;
                    _inputFocusOutline.color = focused ? MenuArt.GoldBorder : InputIdleBorder;
                }
            }

            if (ActiveSheet == SheetKind.IdCamera)
            {
                if (CurrentCameraStep == CameraStep.Waiting)
                {
                    _permissionWaitElapsed += Time.unscaledDeltaTime;
                    if (AndroidLocalLink.HasCameraPermission())
                    {
                        BeginLiveCameraFeed();
                        RefreshLayout();
                    }
                    else if (_permissionWaitElapsed >= 2.5f)
                    {
                        SetCameraStep(CameraStep.Denied);
                        RefreshLayout();
                    }
                }
                else if (CurrentCameraStep == CameraStep.Looking && _webCam != null && _cameraPreviewRaw != null)
                {
                    _cameraPreviewRaw.rectTransform.localEulerAngles = new Vector3(0f, 0f, -_webCam.videoRotationAngle);
                }
            }
        }

        void OnApplicationPaused(bool paused)
        {
            if (ActiveSheet != SheetKind.IdCamera) return;
            if (paused)
            {
                if (CurrentCameraStep == CameraStep.Looking)
                    StopCamera();
            }
            else if (CurrentCameraStep == CameraStep.Looking)
            {
                BeginLiveCameraFeed();
                RefreshLayout();
            }
        }

        public void StopCamera()
        {
            if (_webCam != null)
            {
                try
                {
                    if (_webCam.isPlaying)
                        _webCam.Stop();
                }
                catch
                {
                    // Ignore platform webcam shutdown errors.
                }
                KalyeahResources.Kill(_webCam);
                _webCam = null;
            }
        }

        void RefreshGameOfMonthContent(int month)
        {
            int clampedMonth = Mathf.Clamp(month, 1, 12);
            if (_monthKickerText != null)
                _monthKickerText.text = MonthNames[clampedMonth - 1];

            Dictionary<string, object> game = FindMonthlyGame(clampedMonth);
            if (game == null)
            {
                _monthGameNameText.text = "Patintero";
                _monthFactsText.text = "6 to 12 players \u00B7 20 min";
                _monthParagraphs[0].text = "<color=#F5B93A>You need:</color> Chalk";
                _monthParagraphs[1].text = "<color=#F5B93A>Set up:</color> Draw a grid; split into guards and runners.";
                _monthParagraphs[2].text = "<color=#F5B93A>Play:</color> Guards stay on their lines. Cross every line and return.";
                _monthParagraphs[3].text = "<color=#F5B93A>Win:</color> Each returning runner scores 1. Most points after 3 rounds wins.";
                _monthParagraphs[4].text = "<color=#F5B93A>Stay safe:</color> Play away from traffic. Tag softly!";
            }
            else
            {
                string gameName = ReadString(game, "name", "Patintero");
                string also = ReadString(game, "also", "");
                _monthGameNameText.text = string.IsNullOrEmpty(also) ? gameName : (gameName + " \u00B7 " + also);

                int pMin = ReadInt(game, "players_min", 4);
                int pMax = ReadInt(game, "players_max", 10);
                string time = ReadString(game, "time", "20 min");
                string place = ReadString(game, "place", "Street or yard");
                string playersText = pMin != pMax
                    ? string.Format(CultureInfo.InvariantCulture, "{0} to {1} players", pMin, pMax)
                    : string.Format(CultureInfo.InvariantCulture, "{0} players", pMin);
                _monthFactsText.text = string.Format(CultureInfo.InvariantCulture, "{0} \u00B7 {1} \u00B7 {2}", playersText, time, place);

                string itemsLine = JoinItemsLowercaseTail(game.TryGetValue("items", out object rawItems) ? rawItems as List<object> : null);
                string setupLine = FirstNonEmpty(ReadString(game, "quick_setup", ""), JoinSentences(game, "setup"));
                string playLine = FirstNonEmpty(ReadString(game, "quick_play", ""), JoinSentences(game, "how"));
                string winLine = FirstNonEmpty(ReadString(game, "quick_win", ""), JoinSentences(game, "rules"));
                string safeLine = ReadString(game, "safety", "Play away from traffic!");

                _monthParagraphs[0].text = "<color=#F5B93A>You need:</color> " + itemsLine;
                _monthParagraphs[1].text = "<color=#F5B93A>Set up:</color> " + setupLine;
                _monthParagraphs[2].text = "<color=#F5B93A>Play:</color> " + playLine;
                _monthParagraphs[3].text = "<color=#F5B93A>Win:</color> " + winLine;
                _monthParagraphs[4].text = "<color=#F5B93A>Stay safe:</color> " + safeLine;
            }

            if (_monthScroll != null)
                _monthScroll.verticalNormalizedPosition = 1f;
            if (_monthScrollContentRect != null && Root != null && Root.gameObject.activeInHierarchy)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_monthScrollContentRect);
            }
        }

        Dictionary<string, object> FindMonthlyGame(int month)
        {
            Dictionary<string, object> props = TryProps("rules__larong_kalye");
            if (props == null || !props.TryGetValue("games", out object rawGames) || !(rawGames is List<object> list) || list.Count == 0)
                return null;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Dictionary<string, object> entry && ReadInt(entry, "month", -1) == month)
                    return entry;
            }
            return list[(month - 1) % list.Count] as Dictionary<string, object>;
        }

        void RefreshDonateContent()
        {
            Dictionary<string, object> info = TryProps("rules__donate_info");
            string thanks = ReadString(info, "thanks", "Love Kalyeah? Chip in and help it grow!");
            string grownUps = ReadString(info, "grown_ups", "Ask a grown-up first. Gifts give nothing in the game.");
            _donateUrl = ReadString(info, "url", "").Trim();

            if (_donateThanksText != null) _donateThanksText.text = thanks;
            if (_donateGrownUpsText != null) _donateGrownUpsText.text = grownUps;
            RefreshDonateButtons();
        }

        void RefreshDonateButtons()
        {
            bool hasLink = HasDonateLink;
            if (_donateSoonText != null)
                _donateSoonText.gameObject.SetActive(!hasLink);
            if (_donateActionsRow != null)
                _donateActionsRow.gameObject.SetActive(hasLink);
            if (!hasLink) return;

            if (_donateCancelConfirmBtn != null)
                _donateCancelConfirmBtn.gameObject.SetActive(_donateAwaitingGrownUpConfirm);

            RefreshDonateButtonAnchors(520f, 16f);
            if (_donatePrimaryLabel != null)
                _donatePrimaryLabel.text = _donateAwaitingGrownUpConfirm ? "Grown-up OK \u00B7 Open Link" : "Donate";
        }

        void RefreshDonateButtonAnchors(float rowW, float minGutter)
        {
            if (!HasDonateLink) return;
            float halfGap = Mathf.Clamp((minGutter * 0.5f) / Mathf.Max(240f, rowW), 0.015f, 0.06f);
            if (_donateCancelRect != null)
            {
                _donateCancelRect.anchorMin = new Vector2(0f, 0f);
                _donateCancelRect.anchorMax = new Vector2(0.44f - halfGap, 1f);
                _donateCancelRect.offsetMin = Vector2.zero;
                _donateCancelRect.offsetMax = Vector2.zero;
            }
            if (_donatePrimaryRect != null)
            {
                _donatePrimaryRect.anchorMin = new Vector2(_donateAwaitingGrownUpConfirm ? (0.44f + halfGap) : 0.16f, 0f);
                _donatePrimaryRect.anchorMax = new Vector2(_donateAwaitingGrownUpConfirm ? 1f : 0.84f, 1f);
                _donatePrimaryRect.offsetMin = Vector2.zero;
                _donatePrimaryRect.offsetMax = Vector2.zero;
            }
        }

        void OnDonatePrimaryClicked()
        {
            if (!HasDonateLink) return;
            if (!_donateAwaitingGrownUpConfirm)
            {
                _donateAwaitingGrownUpConfirm = true;
                _audio?.PlayUiTap(false);
                Haptics.LightTap();
                RefreshDonateButtons();
                RefreshLayout();
                return;
            }

            _donateAwaitingGrownUpConfirm = false;
            _audio?.PlayUiTap("confirm");
            Haptics.LightTap();
            string url = _donateUrl;
            Close();
            OpenUrlHandler?.Invoke(url);
        }

        // =================================================================================================================
        // UI Helpers
        // =================================================================================================================

        void PresentModal()
        {
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            RefreshLayout();
            if (_photoScroll != null && ActiveSheet == SheetKind.Profile)
                _photoScroll.verticalNormalizedPosition = 1f;
            _audio?.PlayUiTap(false);
            Haptics.LightTap();
        }

        void SetHeader(string title, Color bandColor, Texture2D icon)
        {
            if (_headerAccentRail != null)
                _headerAccentRail.color = bandColor;
            if (_headerIcon != null)
            {
                _headerIcon.texture = icon;
                _headerIcon.gameObject.SetActive(icon != null);
            }
            if (_headerTitle != null)
                _headerTitle.text = title;
        }

        void ShowOnlyBody(RectTransform active)
        {
            if (_profileBody != null) _profileBody.gameObject.SetActive(_profileBody == active);
            if (_cameraBody != null) _cameraBody.gameObject.SetActive(_cameraBody == active);
            if (_monthBody != null) _monthBody.gameObject.SetActive(_monthBody == active);
            if (_donateBody != null) _donateBody.gameObject.SetActive(_donateBody == active);
        }

        RectTransform MakeContentPanel(string name)
        {
            RectTransform rt = Ui.Fill(name, _modalCard.Find("CardCreamFill") ?? _modalCard);
            rt.offsetMin = new Vector2(14f, 14f);
            rt.offsetMax = new Vector2(-14f, -124f);
            return rt;
        }

        static void MakeTacticalPlate(RectTransform container, int cut)
        {
            Image bg = container.gameObject.AddComponent<Image>();
            bg.sprite = MenuArt.ChamferCard(PlateTop, PlateBottom, MenuArt.PanelBorder, cut, 1, 0.24f);
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
            bg.raycastTarget = false;
        }

        static Button MakeNavChevronButton(string name, Transform parent, bool flipLeft, Action onClick, out RectTransform rect)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            rect = go.GetComponent<RectTransform>();

            Image hitImg = go.GetComponent<Image>();
            hitImg.color = Color.clear; // transparent >= 48 dp hit catcher
            hitImg.raycastTarget = true;

            RectTransform faceRt = Ui.Panel("Face", rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 54f));
            Image faceImg = Ui.MakeSliced("Plate", faceRt, MenuArt.CtaGhost(10), Color.white, raycast: false);

            Image chev = Ui.MakeImage("Chevron", faceRt, MenuArt.GoldLight, round: false, raycast: false);
            chev.sprite = MenuArt.Glyph(MenuArt.GlyphKind.Chevron);
            RectTransform cr = chev.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.pivot = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(20f, 20f);
            if (flipLeft) cr.localEulerAngles = new Vector3(0f, 0f, 180f);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = faceImg;
            btn.onClick.AddListener(() => onClick?.Invoke());
            Ui.AddPressFeel(go, 0.93f);
            return btn;
        }

        static Button MakeActionButton(
            string name,
            Transform parent,
            string label,
            Color fill,
            Color textColor,
            int fontSize,
            Action onClick,
            out RectTransform rect,
            MenuArt.GlyphKind? icon = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            rect = go.GetComponent<RectTransform>();

            bool isPrimary = fill == FiestaOrange;
            Color resolvedTextColor = isPrimary ? MenuArt.Ink : textColor;

            Image hitImg = go.GetComponent<Image>();
            hitImg.color = Color.clear; // transparent >= 48 dp hit catcher
            hitImg.raycastTarget = true;

            RectTransform fillRect = Ui.Panel("Fill", rect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 58f));
            Image fillImg = fillRect.gameObject.AddComponent<Image>();
            fillImg.sprite = isPrimary ? MenuArt.CtaGold(12) : MenuArt.CtaGhost(12);
            fillImg.type = Image.Type.Sliced;
            fillImg.color = Color.white;
            fillImg.raycastTarget = false;

            if (isPrimary)
                Ui.AddSheen(fillRect);
            else
                Ui.MakeAccentRail("BtnRail", fillRect, fill == BarrioBlue ? MenuArt.Gold : MenuArt.PanelBorder, width: 3f, verticalInset: 12f);

            float textLeft = isPrimary ? 10f : 14f;
            if (icon.HasValue)
            {
                Image glyphImg = Ui.MakeImage("Icon", fillRect, MenuArt.GoldLight, round: false, raycast: false);
                glyphImg.sprite = MenuArt.Glyph(icon.Value);
                RectTransform ir = glyphImg.rectTransform;
                ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
                ir.pivot = new Vector2(0f, 0.5f);
                ir.anchoredPosition = new Vector2(16f, 0f);
                ir.sizeDelta = new Vector2(20f, 20f);
                textLeft = 42f;
            }

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = fillImg;
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.98f, 0.92f, 1f);
            colors.pressedColor = new Color(0.86f, 0.86f, 0.90f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());
            Ui.AddPressFeel(go, 0.95f);

            Text txt = Ui.MakeText("Label", fillRect, label, fontSize, resolvedTextColor, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 13;
            txt.resizeTextMaxSize = fontSize;
            Stretch(txt.rectTransform, textLeft, 4f, -10f, -4f);
            Ui.SetTextColor(txt, resolvedTextColor);
            return btn;
        }

        static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }

        Dictionary<string, object> TryProps(string name)
        {
            DataSource ds = _data;
            if (ds == null)
            {
                try { ds = KalyeahResources.Data(); }
                catch { return null; }
            }
            if (ds == null) return null;
            try { return ds.Props(name); }
            catch { return null; }
        }

        static string ReadString(Dictionary<string, object> d, string key, string fallback)
        {
            if (d != null && d.TryGetValue(key, out object v) && v != null)
                return Convert.ToString(v, CultureInfo.InvariantCulture) ?? fallback;
            return fallback;
        }

        static int ReadInt(Dictionary<string, object> d, string key, int fallback)
        {
            if (d != null && d.TryGetValue(key, out object v) && v != null)
            {
                try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); }
                catch { return fallback; }
            }
            return fallback;
        }

        static string JoinItemsLowercaseTail(List<object> items)
        {
            if (items == null || items.Count == 0) return "Chalk";
            var parts = new string[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                string s = Convert.ToString(items[i], CultureInfo.InvariantCulture) ?? "";
                if (i > 0 && s.Length > 0)
                    s = char.ToLowerInvariant(s[0]) + s.Substring(1);
                parts[i] = s;
            }
            return string.Join(", ", parts);
        }

        static string JoinSentences(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.TryGetValue(key, out object raw) || !(raw is List<object> list) || list.Count == 0)
                return "";
            var parts = new List<string>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                string s = Convert.ToString(list[i], CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(s)) parts.Add(s.Trim());
            }
            return string.Join(" ", parts);
        }

        static string FirstNonEmpty(string a, string b) => !string.IsNullOrWhiteSpace(a) ? a : b;

        static void DeleteLocalIdPhotoFile()
        {
            try
            {
                string path = LocalIdPhotoPath;
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Ignore IO error on cleanup.
            }
        }

        void ReleaseSnappedPreview()
        {
            if (_snappedPreviewTex != null)
            {
                KalyeahResources.Kill(_snappedPreviewTex);
                _snappedPreviewTex = null;
            }
        }

        void ReleaseCustomPhotoTexture()
        {
            if (_customPhotoTex != null)
            {
                KalyeahResources.Kill(_customPhotoTex);
                _customPhotoTex = null;
            }
        }
    }
}
