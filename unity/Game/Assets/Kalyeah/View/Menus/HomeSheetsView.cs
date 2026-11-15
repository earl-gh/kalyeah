using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Modal sheets hosted on <see cref="HomeScreenView"/>:
    /// (a) <c>ProfileSheet</c> + local-only <c>IdCameraSheet</c> (6 <c>ui/avatars/*</c> tiles + optional <see cref="WebCamTexture"/>
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
            "junjun", "ligaya", "migo", "toni", "popoy", "inday",
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

        static readonly Color ScrimColor = new Color(0.03f, 0.05f, 0.12f, 0.82f);
        static readonly Color KomiksInk = new Color(0.09f, 0.07f, 0.14f, 0.98f);
        static readonly Color SunlitCream = new Color(0.98f, 0.95f, 0.89f, 1f);
        static readonly Color SunlitPaper = new Color(0.93f, 0.88f, 0.78f, 1f);
        static readonly Color Espresso = new Color(0.17f, 0.13f, 0.11f, 1f);
        static readonly Color MutedEspresso = new Color(0.32f, 0.26f, 0.22f, 1f);
        static readonly Color OchreHeading = new Color(0.72f, 0.39f, 0.10f, 1f);
        static readonly Color MangoGold = new Color(0.97f, 0.68f, 0.15f, 1f);
        static readonly Color FiestaOrange = new Color(0.93f, 0.45f, 0.18f, 1f);
        static readonly Color BarrioBlue = new Color(0.19f, 0.48f, 0.88f, 1f);
        static readonly Color JadeGreen = new Color(0.14f, 0.64f, 0.54f, 1f);
        static readonly Color SlateBtn = new Color(0.24f, 0.29f, 0.42f, 1f);
        static readonly Color HeartRed = new Color(0.84f, 0.19f, 0.17f, 1f);
        static readonly Color ErrorRed = new Color(0.78f, 0.18f, 0.16f, 1f);

        /// <summary>Browser opener used by <c>DonateSheet</c> after grown-up confirmation; replaceable in tests.</summary>
        public static Action<string> OpenUrlHandler { get; set; } = Application.OpenURL;

        public static string LocalIdPhotoPath => Path.Combine(Application.persistentDataPath, LocalIdPhotoFileName);

        sealed class SheetsLifecycle : MonoBehaviour
        {
            public HomeSheetsView Owner;

            void Update() => Owner?.Tick();
            void OnDisable() => Owner?.StopCamera();
            void OnRectTransformDimensionsChange() => Owner?.RefreshLayout();

            void OnApplicationPause(bool paused)
            {
                Owner?.OnApplicationPaused(paused);
            }

            void OnDestroy() => Owner?.StopCamera();
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
        readonly List<AvatarTileUi> _avatarTiles = new List<AvatarTileUi>(6);

        Canvas _canvas;
        RectTransform _modalCard;
        Image _headerBandImg;
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
        RawImage _profilePreviewImg;
        InputField _profileNameInput;
        Text _profileLetterCount;
        Text _profileHintText;
        Button _cameraTileButton;
        RectTransform _photoGridRect;
        GridLayoutGroup _photoGrid;
        Button _removePhotoButton;
        RectTransform _removePhotoRect;
        Button _saveProfileButton;
        RectTransform _saveProfileRect;

        // IdCameraSheet refs
        Text _cameraMessageText;
        RectTransform _cameraFrameRect;
        RawImage _cameraPreviewRaw;
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
        Text _monthKickerText;
        Button _prevMonthBtn;
        Button _nextMonthBtn;
        Text _monthGameNameText;
        Text _monthFactsText;
        readonly Text[] _monthParagraphs = new Text[5];

        // DonateSheet refs
        Text _donateThanksText;
        Text _donateGrownUpsText;
        Text _donateSoonText;
        RectTransform _donateActionsRow;
        Button _donatePrimaryBtn;
        Text _donatePrimaryLabel;
        Button _donateCancelConfirmBtn;
        bool _donateAwaitingGrownUpConfirm;
        string _donateUrl = "";

        // Persisted local profile photo state
        int _avatarIndex;
        bool _hasCustomIdPhoto;
        Texture2D _customPhotoTex;
        bool _disposed;

        public RectTransform Root { get; }
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
        public Button ProfileRemovePhotoButton => _removePhotoButton;
        public Button CameraTileButton => _cameraTileButton;
        public Button DonateActionButton => _donatePrimaryBtn;
        public bool HasDonateLink => !string.IsNullOrWhiteSpace(_donateUrl) && _donateUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        public event Action ProfileChanged;

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
            var lifecycle = Root.gameObject.AddComponent<SheetsLifecycle>();
            lifecycle.Owner = this;

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
            if (_profileHintText != null)
                _profileHintText.gameObject.SetActive(false);

            RefreshProfileVisuals();
            PresentModal();
        }

        public void OpenIdCamera()
        {
            if (!IsInteractive) return;
            ActiveSheet = SheetKind.IdCamera;

            SetHeader("YOUR ID PHOTO", BarrioBlue, null);
            ShowOnlyBody(_cameraBody);
            PresentModal();

            if (!Application.isPlaying)
            {
                SetCameraStep(CameraStep.NoCamera);
                return;
            }

            if (AndroidLocalLink.HasCameraPermission())
                BeginLiveCameraFeed();
            else
                SetCameraStep(CameraStep.Ask);
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

            AppSession.Current.AvatarIndex = _avatarIndex;
            AppSession.Current.HasCustomIdPhoto = false;
            PlayerPrefs.SetInt(AvatarPrefKey, _avatarIndex);
            PlayerPrefs.SetInt(CustomPhotoPrefKey, 0);
            MenuStack.SavePersistedState(AppSession.Current, SettingsStore.Current);

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
                {
                    _profileHintText.text = "Pick a name!";
                    _profileHintText.gameObject.SetActive(true);
                }
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

            Vector2 safe = ScreenFitMetrics.SafeCanvasSize(_canvas);
            float safeW = Mathf.Max(640f, safe.x);
            float safeH = Mathf.Max(420f, safe.y);
            float minTouch = ScreenFitMetrics.ClampTouchSize(52f, ScreenFitMetrics.MinTouchDp, _canvas);

            float targetW = ActiveSheet == SheetKind.Profile
                ? Mathf.Clamp(safeW * 0.68f, 560f, 760f)
                : Mathf.Clamp(safeW * 0.56f, 480f, 640f);
            targetW = Mathf.Min(targetW, safeW - 24f);

            float targetH = ActiveSheet == SheetKind.Donate
                ? Mathf.Clamp(safeH * 0.66f, 340f, 430f)
                : Mathf.Clamp(safeH * 0.84f, 410f, 540f);
            targetH = Mathf.Min(targetH, safeH - 20f);

            _modalCard.sizeDelta = new Vector2(targetW, targetH);

            if (_closeBtnRect != null)
            {
                float closeSide = Mathf.Max(minTouch, 50f);
                _closeBtnRect.sizeDelta = new Vector2(closeSide, Mathf.Min(closeSide, 54f));
            }

            if (_photoGrid != null && _photoGridRect != null)
            {
                float gridW = Mathf.Max(220f, targetW * 0.54f - 28f);
                float gridH = Mathf.Max(150f, targetH - 148f);
                float cellByW = Mathf.Floor((gridW - 8f * 3f) / 4f);
                float cellByH = Mathf.Floor((gridH - 8f) / 2f);
                float tileSide = Mathf.Clamp(Mathf.Min(cellByW, cellByH), Mathf.Min(minTouch, 78f), 96f);
                _photoGrid.cellSize = new Vector2(tileSide, tileSide);
            }

            if (_cameraFrameRect != null)
            {
                float frameSide = Mathf.Clamp(targetH - 196f, 148f, 240f);
                _cameraFrameRect.sizeDelta = new Vector2(frameSide, frameSide);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
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
            Image scrim = Ui.MakeImage("Scrim", Root, ScrimColor, raycast: true);
            scrim.rectTransform.anchorMin = Vector2.zero;
            scrim.rectTransform.anchorMax = Vector2.one;
            scrim.rectTransform.sizeDelta = Vector2.zero;
            var scrimBtn = scrim.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(() => Close());

            _modalCard = Ui.Panel(
                "SheetCard",
                Root,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(680f, 480f));

            Image border = _modalCard.gameObject.AddComponent<Image>();
            border.sprite = MenuStack.RoundedSprite();
            border.type = Image.Type.Sliced;
            border.color = KomiksInk;
            border.raycastTarget = true;

            RectTransform bodyFill = Ui.Fill("CardCreamFill", _modalCard);
            bodyFill.offsetMin = new Vector2(4f, 4f);
            bodyFill.offsetMax = new Vector2(-4f, -4f);
            Image cream = bodyFill.gameObject.AddComponent<Image>();
            cream.sprite = MenuStack.RoundedSprite();
            cream.type = Image.Type.Sliced;
            cream.color = SunlitCream;
            cream.raycastTarget = true;

            RectTransform headerBand = Ui.Panel(
                "HeaderBand",
                bodyFill,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 58f));
            _headerBandImg = headerBand.gameObject.AddComponent<Image>();
            _headerBandImg.sprite = MenuStack.RoundedSprite();
            _headerBandImg.type = Image.Type.Sliced;
            _headerBandImg.color = MangoGold;

            var iconGo = new GameObject("HeaderIcon", typeof(RectTransform), typeof(RawImage));
            iconGo.transform.SetParent(headerBand, false);
            _headerIcon = iconGo.GetComponent<RawImage>();
            _headerIcon.raycastTarget = false;
            RectTransform ir = _headerIcon.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
            ir.pivot = new Vector2(0f, 0.5f);
            ir.anchoredPosition = new Vector2(14f, 0f);
            ir.sizeDelta = new Vector2(36f, 36f);

            _headerTitle = Ui.MakeText("Title", headerBand, "YOUR PLAYER TAG", 24, Ui.Cream, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            RectTransform tr = _headerTitle.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(18f, 2f);
            tr.offsetMax = new Vector2(-72f, -2f);

            _closeBtn = MakeActionButton(
                "CloseSheetButton",
                headerBand,
                "X",
                KomiksInk,
                Ui.Cream,
                20,
                () => Close(),
                out _closeBtnRect);
            _closeBtnRect.anchorMin = _closeBtnRect.anchorMax = new Vector2(1f, 0.5f);
            _closeBtnRect.pivot = new Vector2(1f, 0.5f);
            _closeBtnRect.anchoredPosition = new Vector2(-6f, 0f);
            _closeBtnRect.sizeDelta = new Vector2(52f, 48f);
        }

        void BuildProfileBody()
        {
            _profileBody = MakeContentPanel("ProfileSheet");

            // Left column: Nametag preview + editable name + Save / Remove Photo
            RectTransform leftCol = Ui.Panel("TagPreviewCol", _profileBody, new Vector2(0f, 0f), new Vector2(0.44f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            leftCol.offsetMin = new Vector2(8f, 8f);
            leftCol.offsetMax = new Vector2(-6f, -6f);

            RectTransform tagCard = Ui.Panel("NametagCard", leftCol, new Vector2(0f, 0.22f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            Image tagFrame = tagCard.gameObject.AddComponent<Image>();
            tagFrame.sprite = MenuStack.RoundedSprite();
            tagFrame.type = Image.Type.Sliced;
            tagFrame.color = SunlitPaper;

            RectTransform photoFrame = Ui.Panel("IdPhotoFrame", tagCard, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(118f, 118f));
            Image photoBorder = photoFrame.gameObject.AddComponent<Image>();
            photoBorder.sprite = MenuStack.RoundedSprite();
            photoBorder.type = Image.Type.Sliced;
            photoBorder.color = KomiksInk;

            var previewGo = new GameObject("PreviewPhoto", typeof(RectTransform), typeof(RawImage));
            previewGo.transform.SetParent(photoFrame, false);
            _profilePreviewImg = previewGo.GetComponent<RawImage>();
            _profilePreviewImg.raycastTarget = false;
            RectTransform pr = _profilePreviewImg.rectTransform;
            pr.anchorMin = Vector2.zero;
            pr.anchorMax = Vector2.one;
            pr.offsetMin = new Vector2(4f, 4f);
            pr.offsetMax = new Vector2(-4f, -4f);

            Text tagCap = Ui.MakeText("Caption", tagCard, "PLAYER TAG", 12, new Color(Espresso.r, Espresso.g, Espresso.b, 0.84f), TextAnchor.MiddleLeft);
            tagCap.fontStyle = FontStyle.Bold;
            tagCap.GetComponent<Outline>().enabled = false;
            RectTransform capR = tagCap.rectTransform;
            capR.anchorMin = new Vector2(0f, 0f);
            capR.anchorMax = new Vector2(0.65f, 0f);
            capR.pivot = new Vector2(0f, 0f);
            capR.anchoredPosition = new Vector2(12f, 64f);
            capR.sizeDelta = new Vector2(0f, 20f);

            _profileLetterCount = Ui.MakeText("LetterCount", tagCard, "4/12", 12, MutedEspresso, TextAnchor.MiddleRight);
            _profileLetterCount.fontStyle = FontStyle.Bold;
            _profileLetterCount.GetComponent<Outline>().enabled = false;
            RectTransform lcR = _profileLetterCount.rectTransform;
            lcR.anchorMin = new Vector2(0.65f, 0f);
            lcR.anchorMax = new Vector2(1f, 0f);
            lcR.pivot = new Vector2(1f, 0f);
            lcR.anchoredPosition = new Vector2(-12f, 64f);
            lcR.sizeDelta = new Vector2(0f, 20f);

            RectTransform inputBox = Ui.Panel("NameFieldBox", tagCard, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(-20f, 48f));
            Image inputBg = inputBox.gameObject.AddComponent<Image>();
            inputBg.sprite = MenuStack.RoundedSprite();
            inputBg.type = Image.Type.Sliced;
            inputBg.color = Color.white;
            inputBg.raycastTarget = true;

            _profileNameInput = inputBox.gameObject.AddComponent<InputField>();
            _profileNameInput.targetGraphic = inputBg;
            _profileNameInput.characterLimit = AppSession.MaxNameLength;
            _profileNameInput.lineType = InputField.LineType.SingleLine;

            Text ph = Ui.MakeText("Placeholder", inputBox, "Your name", 20, new Color(0.45f, 0.40f, 0.35f, 0.65f), TextAnchor.MiddleCenter);
            ph.GetComponent<Outline>().enabled = false;
            Stretch(ph.rectTransform, 8f, 4f, -8f, -4f);

            Text txt = Ui.MakeText("Text", inputBox, AppSession.Current.PlayerName, 22, Espresso, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            txt.GetComponent<Outline>().enabled = false;
            txt.supportRichText = false;
            Stretch(txt.rectTransform, 8f, 4f, -8f, -4f);

            _profileNameInput.textComponent = txt;
            _profileNameInput.placeholder = ph;
            _profileNameInput.onValueChanged.AddListener(OnProfileNameChanged);

            _profileHintText = Ui.MakeText("Hint", leftCol, "", 13, ErrorRed, TextAnchor.MiddleCenter);
            _profileHintText.fontStyle = FontStyle.Bold;
            _profileHintText.GetComponent<Outline>().enabled = false;
            RectTransform hr = _profileHintText.rectTransform;
            hr.anchorMin = new Vector2(0f, 0f);
            hr.anchorMax = new Vector2(1f, 0f);
            hr.pivot = new Vector2(0.5f, 0f);
            hr.anchoredPosition = new Vector2(0f, 56f);
            hr.sizeDelta = new Vector2(0f, 20f);
            _profileHintText.gameObject.SetActive(false);

            _saveProfileButton = MakeActionButton(
                "SaveProfileButton",
                leftCol,
                "Save",
                FiestaOrange,
                Ui.Cream,
                20,
                () => SaveProfileName(),
                out _saveProfileRect);
            _saveProfileRect.anchorMin = new Vector2(0f, 0f);
            _saveProfileRect.anchorMax = new Vector2(1f, 0f);
            _saveProfileRect.pivot = new Vector2(0.5f, 0f);
            _saveProfileRect.anchoredPosition = Vector2.zero;
            _saveProfileRect.sizeDelta = new Vector2(0f, 50f);

            // Right column: Camera tile + 6 synced ui/avatars/* tiles + Remove Photo button
            RectTransform rightCol = Ui.Panel("PhotoOptionsCol", _profileBody, new Vector2(0.45f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            rightCol.offsetMin = new Vector2(6f, 8f);
            rightCol.offsetMax = new Vector2(-8f, -6f);

            Text gridTitle = Ui.MakeText("GridHeading", rightCol, "CHOOSE AVATAR OR TAKE ID PHOTO (ON-DEVICE ONLY)", 13, OchreHeading, TextAnchor.MiddleLeft);
            gridTitle.fontStyle = FontStyle.Bold;
            gridTitle.GetComponent<Outline>().enabled = false;
            gridTitle.resizeTextForBestFit = true;
            gridTitle.resizeTextMinSize = 10;
            gridTitle.resizeTextMaxSize = 13;
            RectTransform gtR = gridTitle.rectTransform;
            gtR.anchorMin = new Vector2(0f, 1f);
            gtR.anchorMax = new Vector2(1f, 1f);
            gtR.pivot = new Vector2(0.5f, 1f);
            gtR.anchoredPosition = Vector2.zero;
            gtR.sizeDelta = new Vector2(0f, 24f);

            _photoGridRect = Ui.Panel("PhotoOptions", rightCol, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _photoGridRect.offsetMin = new Vector2(0f, 56f);
            _photoGridRect.offsetMax = new Vector2(0f, -28f);

            _photoGrid = _photoGridRect.gameObject.AddComponent<GridLayoutGroup>();
            _photoGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _photoGrid.constraintCount = 4;
            _photoGrid.cellSize = new Vector2(82f, 82f);
            _photoGrid.spacing = new Vector2(8f, 8f);
            _photoGrid.childAlignment = TextAnchor.MiddleCenter;

            // Tile 0: Local Selfie Camera
            _cameraTileButton = BuildCameraOptionTile(_photoGridRect);

            // Tiles 1..6: All 6 synced ui/avatars/* portraits
            _avatarTiles.Clear();
            for (int i = 0; i < AvatarIds.Length; i++)
                _avatarTiles.Add(BuildAvatarTile(_photoGridRect, AvatarIds[i], i));

            _removePhotoButton = MakeActionButton(
                "RemovePhotoButton",
                rightCol,
                "Remove Custom Photo",
                SlateBtn,
                Ui.Cream,
                16,
                RemoveCustomPhoto,
                out _removePhotoRect);
            _removePhotoRect.anchorMin = new Vector2(0f, 0f);
            _removePhotoRect.anchorMax = new Vector2(1f, 0f);
            _removePhotoRect.pivot = new Vector2(0.5f, 0f);
            _removePhotoRect.anchoredPosition = Vector2.zero;
            _removePhotoRect.sizeDelta = new Vector2(0f, 48f);
        }

        Button BuildCameraOptionTile(Transform parent)
        {
            var go = new GameObject("CameraTile", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var bg = go.GetComponent<Image>();
            bg.sprite = MenuStack.RoundedSprite();
            bg.type = Image.Type.Sliced;
            bg.color = BarrioBlue;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(OpenIdCamera);

            Text iconTxt = Ui.MakeText("CamGlyph", go.transform, "ID CAM", 16, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            RectTransform ir = iconTxt.rectTransform;
            ir.anchorMin = new Vector2(0f, 0.38f);
            ir.anchorMax = Vector2.one;
            ir.offsetMin = new Vector2(4f, 0f);
            ir.offsetMax = new Vector2(-4f, -4f);

            Text subTxt = Ui.MakeText("CamSub", go.transform, "Selfie", 13, SunlitCream, TextAnchor.MiddleCenter);
            subTxt.GetComponent<Outline>().enabled = false;
            RectTransform sr = subTxt.rectTransform;
            sr.anchorMin = Vector2.zero;
            sr.anchorMax = new Vector2(1f, 0.44f);
            sr.offsetMin = new Vector2(4f, 4f);
            sr.offsetMax = new Vector2(-4f, 0f);

            return btn;
        }

        AvatarTileUi BuildAvatarTile(Transform parent, string avatarId, int index)
        {
            var go = new GameObject("Avatar_" + avatarId, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var frame = go.GetComponent<Image>();
            frame.sprite = MenuStack.RoundedSprite();
            frame.type = Image.Type.Sliced;
            frame.color = KomiksInk;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = frame;
            int captured = index;
            btn.onClick.AddListener(() => SelectAvatar(captured));

            var rawGo = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
            rawGo.transform.SetParent(go.transform, false);
            var raw = rawGo.GetComponent<RawImage>();
            raw.texture = LoadAvatarTexture(avatarId);
            raw.uvRect = AvatarCropUv;
            raw.raycastTarget = false;
            Stretch(raw.rectTransform, 4f, 4f, -4f, -4f);

            var ringGo = new GameObject("Ring", typeof(RectTransform), typeof(Image));
            ringGo.transform.SetParent(go.transform, false);
            var ring = ringGo.GetComponent<Image>();
            ring.sprite = MenuStack.RoundedSprite();
            ring.type = Image.Type.Sliced;
            ring.color = new Color(1f, 0.78f, 0.16f, 0.36f);
            ring.raycastTarget = false;
            Stretch(ring.rectTransform, 0f, 0f, 0f, 0f);

            RectTransform checkBadge = Ui.Panel("SelectedDot", ringGo.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-3f, -3f), new Vector2(22f, 22f));
            Ui.MakeImage("DotBg", checkBadge, MangoGold, round: true).rectTransform.sizeDelta = new Vector2(22f, 22f);

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
                16,
                Espresso,
                TextAnchor.MiddleCenter);
            _cameraMessageText.GetComponent<Outline>().enabled = false;
            _cameraMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _cameraMessageText.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform msgR = _cameraMessageText.rectTransform;
            msgR.anchorMin = new Vector2(0f, 1f);
            msgR.anchorMax = new Vector2(1f, 1f);
            msgR.pivot = new Vector2(0.5f, 1f);
            msgR.anchoredPosition = new Vector2(0f, -8f);
            msgR.sizeDelta = new Vector2(-24f, 52f);

            _cameraFrameRect = Ui.Panel("Frame", _cameraBody, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210f, 210f));
            Image frameBg = _cameraFrameRect.gameObject.AddComponent<Image>();
            frameBg.sprite = MenuStack.RoundedSprite();
            frameBg.type = Image.Type.Sliced;
            frameBg.color = KomiksInk;

            var previewGo = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            previewGo.transform.SetParent(_cameraFrameRect, false);
            _cameraPreviewRaw = previewGo.GetComponent<RawImage>();
            _cameraPreviewRaw.raycastTarget = false;
            Stretch(_cameraPreviewRaw.rectTransform, 5f, 5f, -5f, -5f);

            RectTransform btnRow = Ui.Panel("Buttons", _cameraBody, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(-24f, 52f));

            _camSecondaryBtn = MakeActionButton(
                "BackOrRetakeButton",
                btnRow,
                "Back to Avatars",
                SlateBtn,
                Ui.Cream,
                18,
                OnCameraSecondaryClicked,
                out _camSecondaryRect);
            _camSecondaryRect.anchorMin = new Vector2(0f, 0f);
            _camSecondaryRect.anchorMax = new Vector2(0.48f, 1f);
            _camSecondaryRect.offsetMin = Vector2.zero;
            _camSecondaryRect.offsetMax = Vector2.zero;
            _camSecondaryLabel = _camSecondaryBtn.GetComponentInChildren<Text>();

            _camPrimaryBtn = MakeActionButton(
                "CameraPrimaryButton",
                btnRow,
                "Allow Camera",
                FiestaOrange,
                Ui.Cream,
                18,
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

            RectTransform monthNav = Ui.Panel("MonthHeaderRow", _monthBody, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(-20f, 48f));
            _prevMonthBtn = MakeActionButton("PrevMonth", monthNav, "<", SlateBtn, Ui.Cream, 20, () => StepGameOfMonth(-1), out RectTransform prevR);
            prevR.anchorMin = prevR.anchorMax = new Vector2(0f, 0.5f);
            prevR.pivot = new Vector2(0f, 0.5f);
            prevR.anchoredPosition = Vector2.zero;
            prevR.sizeDelta = new Vector2(56f, 48f);

            _nextMonthBtn = MakeActionButton("NextMonth", monthNav, ">", SlateBtn, Ui.Cream, 20, () => StepGameOfMonth(1), out RectTransform nextR);
            nextR.anchorMin = nextR.anchorMax = new Vector2(1f, 0.5f);
            nextR.pivot = new Vector2(1f, 0.5f);
            nextR.anchoredPosition = Vector2.zero;
            nextR.sizeDelta = new Vector2(56f, 48f);

            _monthKickerText = Ui.MakeText("Month", monthNav, "JANUARY", 14, OchreHeading, TextAnchor.UpperCenter);
            _monthKickerText.fontStyle = FontStyle.Bold;
            _monthKickerText.GetComponent<Outline>().enabled = false;
            RectTransform kr = _monthKickerText.rectTransform;
            kr.anchorMin = new Vector2(0f, 0.55f);
            kr.anchorMax = Vector2.one;
            kr.offsetMin = new Vector2(64f, 0f);
            kr.offsetMax = new Vector2(-64f, 0f);

            _monthGameNameText = Ui.MakeText("GameName", monthNav, "Patintero", 26, Espresso, TextAnchor.LowerCenter, KalyeahResources.DisplayFont());
            _monthGameNameText.GetComponent<Outline>().enabled = false;
            RectTransform gnR = _monthGameNameText.rectTransform;
            gnR.anchorMin = Vector2.zero;
            gnR.anchorMax = new Vector2(1f, 0.62f);
            gnR.offsetMin = new Vector2(64f, 0f);
            gnR.offsetMax = new Vector2(-64f, 0f);

            _monthFactsText = Ui.MakeText("Facts", _monthBody, "", 15, MutedEspresso, TextAnchor.MiddleCenter);
            _monthFactsText.fontStyle = FontStyle.Bold;
            _monthFactsText.GetComponent<Outline>().enabled = false;
            RectTransform fr = _monthFactsText.rectTransform;
            fr.anchorMin = new Vector2(0f, 1f);
            fr.anchorMax = new Vector2(1f, 1f);
            fr.pivot = new Vector2(0.5f, 1f);
            fr.anchoredPosition = new Vector2(0f, -56f);
            fr.sizeDelta = new Vector2(-24f, 24f);

            RectTransform listCard = Ui.Panel("RulesListCard", _monthBody, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            listCard.offsetMin = new Vector2(12f, 12f);
            listCard.offsetMax = new Vector2(-12f, -84f);
            Image cardPlate = listCard.gameObject.AddComponent<Image>();
            cardPlate.sprite = MenuStack.RoundedSprite();
            cardPlate.type = Image.Type.Sliced;
            cardPlate.color = SunlitPaper;

            var vlg = listCard.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 10, 10);
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;

            string[] rowNames = { "YouNeed", "SetUp", "Play", "Win", "StaySafe" };
            for (int i = 0; i < 5; i++)
            {
                Text p = Ui.MakeText(rowNames[i], listCard, "", 15, Espresso, TextAnchor.MiddleLeft);
                p.GetComponent<Outline>().enabled = false;
                p.supportRichText = true;
                p.horizontalOverflow = HorizontalWrapMode.Wrap;
                p.verticalOverflow = VerticalWrapMode.Truncate;
                p.resizeTextForBestFit = true;
                p.resizeTextMinSize = 12;
                p.resizeTextMaxSize = 16;
                _monthParagraphs[i] = p;
            }
        }

        void BuildDonateBody()
        {
            _donateBody = MakeContentPanel("DonateSheet");

            Text heart = Ui.MakeText("Heart", _donateBody, "\u2665", 44, HeartRed, TextAnchor.MiddleCenter);
            heart.GetComponent<Outline>().enabled = false;
            RectTransform hr = heart.rectTransform;
            hr.anchorMin = new Vector2(0f, 1f);
            hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.anchoredPosition = new Vector2(0f, -12f);
            hr.sizeDelta = new Vector2(-24f, 52f);

            _donateThanksText = Ui.MakeText("Thanks", _donateBody, "Love Kalyeah? Chip in and help it grow!", 22, Espresso, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            _donateThanksText.GetComponent<Outline>().enabled = false;
            _donateThanksText.horizontalOverflow = HorizontalWrapMode.Wrap;
            RectTransform tr = _donateThanksText.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(0f, -68f);
            tr.sizeDelta = new Vector2(-32f, 64f);

            RectTransform grownBox = Ui.Panel("GrownUpsNote", _donateBody, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(-36f, 72f));
            Image grownPlate = grownBox.gameObject.AddComponent<Image>();
            grownPlate.sprite = MenuStack.RoundedSprite();
            grownPlate.type = Image.Type.Sliced;
            grownPlate.color = SunlitPaper;

            _donateGrownUpsText = Ui.MakeText("GrownUpsText", grownBox, "Ask a grown-up first. Gifts give nothing in the game.", 16, MutedEspresso, TextAnchor.MiddleCenter);
            _donateGrownUpsText.fontStyle = FontStyle.Bold;
            _donateGrownUpsText.GetComponent<Outline>().enabled = false;
            _donateGrownUpsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            Stretch(_donateGrownUpsText.rectTransform, 14f, 6f, -14f, -6f);

            _donateSoonText = Ui.MakeText("Soon", _donateBody, "Coming soon!", 18, OchreHeading, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            _donateSoonText.GetComponent<Outline>().enabled = false;
            RectTransform sr = _donateSoonText.rectTransform;
            sr.anchorMin = new Vector2(0f, 0f);
            sr.anchorMax = new Vector2(1f, 0f);
            sr.pivot = new Vector2(0.5f, 0f);
            sr.anchoredPosition = new Vector2(0f, 20f);
            sr.sizeDelta = new Vector2(-32f, 48f);

            _donateActionsRow = Ui.Panel("DonateActions", _donateBody, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(-36f, 54f));

            _donateCancelConfirmBtn = MakeActionButton(
                "CancelDonateButton",
                _donateActionsRow,
                "Cancel",
                SlateBtn,
                Ui.Cream,
                18,
                () =>
                {
                    _donateAwaitingGrownUpConfirm = false;
                    RefreshDonateButtons();
                },
                out RectTransform cancelR);
            cancelR.anchorMin = new Vector2(0f, 0f);
            cancelR.anchorMax = new Vector2(0.42f, 1f);
            cancelR.offsetMin = Vector2.zero;
            cancelR.offsetMax = Vector2.zero;

            _donatePrimaryBtn = MakeActionButton(
                "DonateButton",
                _donateActionsRow,
                "Donate",
                FiestaOrange,
                Ui.Cream,
                18,
                OnDonatePrimaryClicked,
                out RectTransform primR);
            primR.anchorMin = new Vector2(0.45f, 0f);
            primR.anchorMax = new Vector2(1f, 1f);
            primR.offsetMin = Vector2.zero;
            primR.offsetMax = Vector2.zero;
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

            AppSession.Current.AvatarIndex = _avatarIndex;
            AppSession.Current.HasCustomIdPhoto = _hasCustomIdPhoto;
            UpdateCurrentIdTexture();
        }

        public void SyncFromSession()
        {
            int sessionAvatar = Mathf.Clamp(AppSession.Current.AvatarIndex, 0, AvatarIds.Length - 1);
            bool sessionCustom = AppSession.Current.HasCustomIdPhoto;
            if (sessionAvatar == _avatarIndex && sessionCustom == _hasCustomIdPhoto && CurrentIdTexture != null)
                return;

            _avatarIndex = sessionAvatar;
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
            CurrentIdTexture = LoadAvatarTexture(id);
            CurrentIdUvRect = AvatarCropUv;
        }

        static Texture2D LoadAvatarTexture(string avatarId)
        {
            return KalyeahResources.Texture("ui/avatars/" + avatarId)
                ?? KalyeahResources.Texture("portraits/faces/" + avatarId)
                ?? KalyeahResources.Texture("portraits/" + avatarId);
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

        void OnProfileNameChanged(string typed)
        {
            string filtered = DisallowedNameChars.Replace(typed ?? "", "").ToUpperInvariant();
            if (filtered.Length > AppSession.MaxNameLength)
                filtered = filtered.Substring(0, AppSession.MaxNameLength);

            if (_profileNameInput != null && !string.Equals(_profileNameInput.text, filtered, StringComparison.Ordinal))
                _profileNameInput.SetTextWithoutNotify(filtered);

            if (_profileHintText != null)
                _profileHintText.gameObject.SetActive(false);

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
                    _cameraMessageText.text = "Camera access is off. You can pick any of the 6 kid avatars or allow Camera in app settings.";
                    _camSecondaryLabel.text = "Back to Avatars";
                    _camPrimaryBtn.gameObject.SetActive(false);
                    break;
                case CameraStep.NoCamera:
                    _cameraMessageText.text = "No camera found on this device. Pick one of the 6 kid avatars!";
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
                return;
            }

            StopCamera();
            OpenProfile();
        }

        void OnCameraPrimaryClicked()
        {
            if (CurrentCameraStep == CameraStep.Ask)
            {
                _permissionWaitElapsed = 0f;
                SetCameraStep(CameraStep.Waiting);
                AndroidLocalLink.RequestCameraPermission();
            }
            else if (CurrentCameraStep == CameraStep.Looking)
            {
                SnapSelfieFrame();
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
                float v = (y + 0.5f) / IdPhotoSize;
                for (int x = 0; x < IdPhotoSize; x++)
                {
                    float u = (x + 0.5f) / IdPhotoSize;
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

            if (ActiveSheet == SheetKind.IdCamera)
            {
                if (CurrentCameraStep == CameraStep.Waiting)
                {
                    _permissionWaitElapsed += Time.unscaledDeltaTime;
                    if (AndroidLocalLink.HasCameraPermission())
                        BeginLiveCameraFeed();
                    else if (_permissionWaitElapsed >= 2.5f)
                        SetCameraStep(CameraStep.Denied);
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
                _monthParagraphs[0].text = "<b>You need:</b> Chalk";
                _monthParagraphs[1].text = "<b>Set up:</b> Draw a grid; split into guards and runners.";
                _monthParagraphs[2].text = "<b>Play:</b> Guards stay on their lines. Cross every line and return.";
                _monthParagraphs[3].text = "<b>Win:</b> Each returning runner scores 1. Most points after 3 rounds wins.";
                _monthParagraphs[4].text = "<b>Stay safe:</b> Play away from traffic. Tag softly!";
                return;
            }

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

            _monthParagraphs[0].text = "<b>You need:</b> " + itemsLine;
            _monthParagraphs[1].text = "<b>Set up:</b> " + setupLine;
            _monthParagraphs[2].text = "<b>Play:</b> " + playLine;
            _monthParagraphs[3].text = "<b>Win:</b> " + winLine;
            _monthParagraphs[4].text = "<b>Stay safe:</b> " + safeLine;
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

            if (_donatePrimaryBtn != null)
            {
                RectTransform pr = _donatePrimaryBtn.GetComponent<RectTransform>();
                pr.anchorMin = new Vector2(_donateAwaitingGrownUpConfirm ? 0.45f : 0.15f, 0f);
                pr.anchorMax = new Vector2(_donateAwaitingGrownUpConfirm ? 1f : 0.85f, 1f);
            }
            if (_donatePrimaryLabel != null)
                _donatePrimaryLabel.text = _donateAwaitingGrownUpConfirm ? "Grown-up OK \u00B7 Open Link" : "Donate";
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
            _audio?.PlayUiTap(false);
            Haptics.LightTap();
        }

        void SetHeader(string title, Color bandColor, Texture2D icon)
        {
            if (_headerTitle != null)
            {
                _headerTitle.text = title;
                _headerTitle.rectTransform.offsetMin = new Vector2(icon != null ? 58f : 18f, 2f);
            }
            if (_headerBandImg != null)
                _headerBandImg.color = bandColor;
            if (_headerIcon != null)
            {
                _headerIcon.texture = icon;
                _headerIcon.gameObject.SetActive(icon != null);
            }
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
            RectTransform rt = Ui.Fill(name, _modalCard);
            rt.offsetMin = new Vector2(10f, 10f);
            rt.offsetMax = new Vector2(-10f, -64f);
            return rt;
        }

        static Button MakeActionButton(
            string name,
            Transform parent,
            string label,
            Color fill,
            Color textColor,
            int fontSize,
            Action onClick,
            out RectTransform rect)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            rect = go.GetComponent<RectTransform>();

            var img = go.GetComponent<Image>();
            img.sprite = MenuStack.RoundedSprite();
            img.type = Image.Type.Sliced;
            img.color = fill;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());

            Text txt = Ui.MakeText("Label", rect, label, fontSize, textColor, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 12;
            txt.resizeTextMaxSize = fontSize;
            Stretch(txt.rectTransform, 8f, 4f, -8f, -4f);
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
