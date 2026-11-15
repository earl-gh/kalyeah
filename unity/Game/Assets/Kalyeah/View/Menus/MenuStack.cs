using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Push/pop UGUI screen stack for the Title/Home menu flow, handling Android Back navigation, a root
    /// "Quit Kalyeah?" confirmation modal, and PlayerPrefs persistence for <see cref="AppSession.Current"/>
    /// and <see cref="SettingsStore.Current"/>.
    /// </summary>
    public sealed class MenuStack : IDisposable
    {
        public const string HomeId = "home";
        public const string PickId = "pick";
        public const string CodexId = "codex";
        public const string HistoryId = "history";
        public const string SettingsId = "settings";
        public const string LobbyId = "lobby";
        public const string HotspotId = "hotspot";

        static readonly Color ScrimColor = new Color(0.03f, 0.05f, 0.12f, 0.82f);
        static readonly Color InkBorderColor = new Color(0.09f, 0.07f, 0.14f, 0.98f);
        static readonly Color CardBodyColor = new Color(0.98f, 0.95f, 0.89f, 1f);
        static readonly Color HeaderBandColor = new Color(0.92f, 0.47f, 0.16f, 1f);
        static readonly Color CancelBtnColor = new Color(0.19f, 0.48f, 0.88f, 1f);
        static readonly Color QuitBtnColor = new Color(0.86f, 0.25f, 0.22f, 1f);
        static readonly Color EspressoText = new Color(0.14f, 0.10f, 0.08f, 1f);

        static readonly Dictionary<string, Sprite> UiSpriteCache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        static Sprite _roundedSprite;

        sealed class ScreenEntry
        {
            public string Id;
            public RectTransform Root;
            public Action OnShow;
            public Action OnHide;
            public Func<bool> OnBackOverride;
        }

        [ExecuteAlways]
        sealed class StackLifecycle : MonoBehaviour
        {
            [NonSerialized] public MenuStack Owner;
            bool _viewportSubscribed;

            void OnEnable()
            {
                SubscribeViewport();
                Owner?.RefreshModalLayout();
            }

            void OnDisable() => UnsubscribeViewport();

            void OnRectTransformDimensionsChange() => Owner?.RefreshModalLayout();

            void OnApplicationPause(bool paused)
            {
                if (paused) Owner?.SavePersistedState();
            }

            void OnDestroy()
            {
                UnsubscribeViewport();
                Owner?.DetachPersistenceListeners();
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

        readonly Dictionary<string, ScreenEntry> _screens = new Dictionary<string, ScreenEntry>(StringComparer.Ordinal);
        readonly List<string> _stack = new List<string>(8);
        readonly Action _onQuitConfirmed;
        readonly AudioHub _audio;

        Transform _modalParent;
        Canvas _canvas;
        RectTransform _quitModalRoot;
        RectTransform _quitSafeRoot;
        SafeArea _quitSafeArea;
        RectTransform _quitScrimRect;
        RectTransform _quitCard;
        RectTransform _quitHeaderRect;
        RectTransform _quitMessageRect;
        RectTransform _cancelRect;
        RectTransform _quitRect;
        Text _quitTitleLabel;
        Text _quitBodyLabel;
        Text _cancelLabel;
        Text _quitLabel;
        Button _cancelButton;
        Button _quitButton;
        StackLifecycle _lifecycle;
        string _defaultRootId;
        bool _persistenceBound;
        bool _disposed;

        public event Action<string> ScreenChanged;
        public event Action<bool> QuitModalChanged;

        public string CurrentId => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

        public RectTransform CurrentRoot =>
            CurrentId != null && _screens.TryGetValue(CurrentId, out ScreenEntry entry) ? entry.Root : null;

        public int Depth => _stack.Count;

        public bool IsAtRoot => _stack.Count <= 1;

        public bool IsQuitModalOpen => _quitModalRoot != null && _quitModalRoot.gameObject.activeSelf;

        public RectTransform QuitModalRoot => _quitModalRoot;

        public Button QuitCancelButton => _cancelButton;

        public Button QuitConfirmButton => _quitButton;

        public MenuStack(Transform modalParent = null, Action onQuitConfirmed = null, AudioHub audio = null)
        {
            _modalParent = modalParent;
            _onQuitConfirmed = onQuitConfirmed ?? DefaultQuitApplication;
            _audio = audio;

            if (_modalParent != null)
            {
                _canvas = _modalParent.GetComponentInParent<Canvas>();
                EnsureLifecycle(_modalParent.gameObject);
                BuildQuitModal(_modalParent);
            }
        }

        public void Register(string id, RectTransform root, Action onShow = null)
        {
            Register(id, root, onShow, null, null);
        }

        public void Register(
            string id,
            RectTransform root,
            Action onShow,
            Action onHide,
            Func<bool> onBackOverride = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Screen id cannot be empty.", nameof(id));
            if (root == null) throw new ArgumentNullException(nameof(root));

            string key = id.Trim();
            _screens[key] = new ScreenEntry
            {
                Id = key,
                Root = root,
                OnShow = onShow,
                OnHide = onHide,
                OnBackOverride = onBackOverride,
            };

            if (_defaultRootId == null || string.Equals(key, HomeId, StringComparison.Ordinal))
                _defaultRootId = key;

            if (_modalParent == null && root.parent != null)
            {
                _modalParent = root.parent;
                _canvas = _modalParent.GetComponentInParent<Canvas>();
                EnsureLifecycle(_modalParent.gameObject);
                BuildQuitModal(_modalParent);
            }

            bool isActiveTop = string.Equals(CurrentId, key, StringComparison.Ordinal);
            root.gameObject.SetActive(isActiveTop);
            KeepModalOnTop();
        }

        public bool Contains(string id) => !string.IsNullOrWhiteSpace(id) && _screens.ContainsKey(id.Trim());

        public void Push(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            string key = id.Trim();
            if (!_screens.TryGetValue(key, out ScreenEntry target) || target.Root == null) return;

            HideQuitModal(playSound: false);

            string previousId = CurrentId;
            if (string.Equals(previousId, key, StringComparison.Ordinal))
            {
                target.Root.gameObject.SetActive(true);
                KeepModalOnTop();
                target.OnShow?.Invoke();
                FocusInitialSelectable(target.Root);
                return;
            }

            if (previousId != null && _screens.TryGetValue(previousId, out ScreenEntry previous) && previous.Root != null)
            {
                previous.Root.gameObject.SetActive(false);
                previous.OnHide?.Invoke();
            }

            int existingIndex = _stack.IndexOf(key);
            if (existingIndex >= 0)
            {
                _stack.RemoveRange(existingIndex + 1, _stack.Count - existingIndex - 1);
            }
            else
            {
                if (_stack.Count == 0 && _defaultRootId != null && !string.Equals(_defaultRootId, key, StringComparison.Ordinal)
                    && _screens.TryGetValue(_defaultRootId, out ScreenEntry rootEntry) && rootEntry.Root != null)
                {
                    rootEntry.Root.gameObject.SetActive(false);
                    _stack.Add(_defaultRootId);
                }
                _stack.Add(key);
            }

            target.Root.gameObject.SetActive(true);
            target.Root.SetAsLastSibling();
            KeepModalOnTop();
            target.OnShow?.Invoke();
            FocusInitialSelectable(target.Root);
            ScreenChanged?.Invoke(key);
        }

        public void Pop()
        {
            if (IsQuitModalOpen)
            {
                HideQuitModal(playSound: true);
                return;
            }

            if (_stack.Count <= 1) return;

            string poppedId = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);

            if (_screens.TryGetValue(poppedId, out ScreenEntry popped) && popped.Root != null)
            {
                popped.Root.gameObject.SetActive(false);
                popped.OnHide?.Invoke();
            }

            string nextId = CurrentId;
            if (nextId != null && _screens.TryGetValue(nextId, out ScreenEntry next) && next.Root != null)
            {
                next.Root.gameObject.SetActive(true);
                next.Root.SetAsLastSibling();
                KeepModalOnTop();
                next.OnShow?.Invoke();
                FocusInitialSelectable(next.Root);
            }

            PlayFeedback(cancel: true);
            ScreenChanged?.Invoke(nextId);
        }

        public void PopToRoot()
        {
            HideQuitModal(playSound: false);
            if (_stack.Count <= 1) return;

            for (int i = _stack.Count - 1; i >= 1; i--)
            {
                string id = _stack[i];
                if (_screens.TryGetValue(id, out ScreenEntry entry) && entry.Root != null)
                {
                    entry.Root.gameObject.SetActive(false);
                    entry.OnHide?.Invoke();
                }
                _stack.RemoveAt(i);
            }

            string rootId = CurrentId;
            if (rootId != null && _screens.TryGetValue(rootId, out ScreenEntry root) && root.Root != null)
            {
                root.Root.gameObject.SetActive(true);
                root.Root.SetAsLastSibling();
                KeepModalOnTop();
                root.OnShow?.Invoke();
                FocusInitialSelectable(root.Root);
            }

            ScreenChanged?.Invoke(rootId);
        }

        /// <summary>
        /// Handles Android Back / Escape: dismisses the Quit modal if open, lets an active subscreen consume Back if needed,
        /// pops back toward Home when deeper in the stack, or opens the "Quit Kalyeah?" confirmation modal on Home.
        /// </summary>
        public bool HandleBack()
        {
            if (IsQuitModalOpen)
            {
                HideQuitModal(playSound: true);
                return true;
            }

            string activeId = CurrentId;
            if (activeId != null && _screens.TryGetValue(activeId, out ScreenEntry active) && active.OnBackOverride != null)
            {
                if (active.OnBackOverride.Invoke())
                    return true;
            }

            if (_stack.Count > 1)
            {
                Pop();
                return true;
            }

            ShowQuitModal();
            return true;
        }

        public void ShowQuitModal()
        {
            if (_quitModalRoot == null)
            {
                Transform targetParent = _modalParent ?? CurrentRoot?.parent ?? CurrentRoot;
                if (targetParent == null) return;
                _modalParent = targetParent;
                _canvas = _modalParent.GetComponentInParent<Canvas>();
                EnsureLifecycle(_modalParent.gameObject);
                BuildQuitModal(_modalParent);
            }

            RefreshQuitCopy();
            RefreshModalLayout();
            _quitModalRoot.gameObject.SetActive(true);
            _quitModalRoot.SetAsLastSibling();
            PlayFeedback(cancel: false);
            FocusInitialSelectable(_quitModalRoot);
            QuitModalChanged?.Invoke(true);
        }

        public void HideQuitModal() => HideQuitModal(playSound: true);

        void HideQuitModal(bool playSound)
        {
            if (_quitModalRoot == null || !_quitModalRoot.gameObject.activeSelf) return;
            _quitModalRoot.gameObject.SetActive(false);
            if (playSound) PlayFeedback(cancel: true);
            if (CurrentRoot != null) FocusInitialSelectable(CurrentRoot);
            QuitModalChanged?.Invoke(false);
        }

        public void ConfirmQuit()
        {
            SavePersistedState();
            PlayFeedback(cancel: false);
            if (_quitModalRoot != null)
                _quitModalRoot.gameObject.SetActive(false);
            _onQuitConfirmed?.Invoke();
        }

        /// <summary>Loads <see cref="SettingsStore.Current"/> and <see cref="AppSession.Current"/> from <see cref="PlayerPrefs"/> and binds auto-save.</summary>
        public void LoadPersistedState()
        {
            LoadPersistedState(AppSession.Current, SettingsStore.Current);
            BindPersistenceListeners();
        }

        /// <summary>Saves <see cref="SettingsStore.Current"/> and <see cref="AppSession.Current"/> to <see cref="PlayerPrefs"/>.</summary>
        public void SavePersistedState()
        {
            SavePersistedState(AppSession.Current, SettingsStore.Current);
        }

        public static void LoadPersistedState(AppSession session, SettingsStore settings)
        {
            if (settings != null && PlayerPrefs.HasKey(SettingsStore.PlayerPrefsKey))
            {
                string settingsJson = PlayerPrefs.GetString(SettingsStore.PlayerPrefsKey, "");
                settings.LoadJson(settingsJson);
            }

            if (settings != null)
            {
                Haptics.Enabled = settings.Haptics;
                if (settings.TargetFrameRate > 0)
                    Application.targetFrameRate = settings.TargetFrameRate;
            }

            if (session != null && PlayerPrefs.HasKey(AppSession.PlayerPrefsKey))
            {
                string sessionJson = PlayerPrefs.GetString(AppSession.PlayerPrefsKey, "");
                session.LoadJson(sessionJson);
            }
        }

        public static void SavePersistedState(AppSession session, SettingsStore settings)
        {
            if (settings != null)
            {
                Haptics.Enabled = settings.Haptics;
                PlayerPrefs.SetString(SettingsStore.PlayerPrefsKey, settings.ToJson());
            }

            if (session != null)
            {
                PlayerPrefs.SetString(AppSession.PlayerPrefsKey, session.ToJson());
            }

            PlayerPrefs.Save();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DetachPersistenceListeners();
            if (_quitSafeArea != null)
            {
                _quitSafeArea.Changed -= OnQuitSafeAreaChanged;
                _quitSafeArea = null;
            }
            if (_quitModalRoot != null)
            {
                KalyeahResources.Kill(_quitModalRoot.gameObject);
                _quitModalRoot = null;
            }
        }

        void BindPersistenceListeners()
        {
            if (_persistenceBound) return;
            _persistenceBound = true;
            AppSession.Current.Changed += OnStateChanged;
            SettingsStore.Current.Changed += OnStateChanged;
        }

        void DetachPersistenceListeners()
        {
            if (!_persistenceBound) return;
            _persistenceBound = false;
            AppSession.Current.Changed -= OnStateChanged;
            SettingsStore.Current.Changed -= OnStateChanged;
        }

        void OnStateChanged()
        {
            SavePersistedState();
            if (IsQuitModalOpen) RefreshQuitCopy();
        }

        void EnsureLifecycle(GameObject host)
        {
            if (_lifecycle != null || host == null) return;
            _lifecycle = host.GetComponent<StackLifecycle>();
            if (_lifecycle == null) _lifecycle = host.AddComponent<StackLifecycle>();
            _lifecycle.Owner = this;
        }

        void BuildQuitModal(Transform parent)
        {
            if (_quitModalRoot != null || parent == null) return;

            _quitModalRoot = Ui.Fill("QuitModal", parent);

            Image scrim = Ui.MakeImage("Scrim", _quitModalRoot, ScrimColor, raycast: true);
            _quitScrimRect = scrim.rectTransform;
            _quitScrimRect.anchorMin = Vector2.zero;
            _quitScrimRect.anchorMax = Vector2.one;
            _quitScrimRect.sizeDelta = Vector2.zero;

            SafeArea parentSafe = parent.GetComponentInParent<SafeArea>();
            if (parentSafe != null)
            {
                _quitSafeArea = parentSafe;
                _quitSafeRoot = Ui.Fill("SafeArea", _quitModalRoot);
            }
            else
            {
                _quitSafeRoot = Ui.Fill("SafeArea", _quitModalRoot);
                _quitSafeArea = _quitSafeRoot.gameObject.AddComponent<SafeArea>();
                _quitSafeArea.Apply();
            }
            _quitSafeArea.Changed += OnQuitSafeAreaChanged;

            RectTransform inkOuter = Ui.Panel(
                "QuitCardFrame",
                _quitSafeRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(520f, 340f));
            Image outerImg = inkOuter.gameObject.AddComponent<Image>();
            outerImg.sprite = RoundedSprite();
            outerImg.type = Image.Type.Sliced;
            outerImg.color = InkBorderColor;
            outerImg.raycastTarget = true;
            _quitCard = inkOuter;

            RectTransform body = Ui.Fill("QuitCardBody", inkOuter);
            body.offsetMin = new Vector2(5f, 5f);
            body.offsetMax = new Vector2(-5f, -5f);
            Image bodyImg = body.gameObject.AddComponent<Image>();
            bodyImg.sprite = RoundedSprite();
            bodyImg.type = Image.Type.Sliced;
            bodyImg.color = CardBodyColor;
            bodyImg.raycastTarget = false;

            _quitHeaderRect = Ui.Panel(
                "HeaderBand",
                body,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -6f),
                new Vector2(-12f, 76f));
            Image headerImg = _quitHeaderRect.gameObject.AddComponent<Image>();
            headerImg.sprite = RoundedSprite();
            headerImg.type = Image.Type.Sliced;
            headerImg.color = HeaderBandColor;
            headerImg.raycastTarget = false;

            _quitTitleLabel = Ui.MakeText(
                "QuitTitle",
                _quitHeaderRect,
                "Quit Kalyeah?",
                32,
                Ui.Cream,
                TextAnchor.MiddleCenter,
                KalyeahResources.DisplayFont());
            _quitTitleLabel.resizeTextForBestFit = true;
            _quitTitleLabel.resizeTextMinSize = 20;
            _quitTitleLabel.resizeTextMaxSize = 32;
            RectTransform titleRect = _quitTitleLabel.rectTransform;
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(16f, 4f);
            titleRect.offsetMax = new Vector2(-16f, -4f);

            _quitBodyLabel = Ui.MakeText(
                "QuitMessage",
                body,
                "Leave the street and close the game?",
                22,
                EspressoText,
                TextAnchor.MiddleCenter);
            _quitBodyLabel.fontStyle = FontStyle.Bold;
            _quitBodyLabel.GetComponent<Outline>().enabled = false;
            _quitBodyLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _quitBodyLabel.verticalOverflow = VerticalWrapMode.Truncate;
            _quitBodyLabel.resizeTextForBestFit = true;
            _quitBodyLabel.resizeTextMinSize = 18;
            _quitBodyLabel.resizeTextMaxSize = 22;
            _quitMessageRect = _quitBodyLabel.rectTransform;
            _quitMessageRect.anchorMin = Vector2.zero;
            _quitMessageRect.anchorMax = Vector2.one;
            _quitMessageRect.offsetMin = new Vector2(24f, 126f);
            _quitMessageRect.offsetMax = new Vector2(-24f, -94f);

            _cancelButton = MakeModalButton(
                "Cancel",
                body,
                "Cancel",
                CancelBtnColor,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(20f, 18f),
                out _cancelRect,
                out _cancelLabel);
            _cancelButton.onClick.AddListener(() => HideQuitModal(playSound: true));

            _quitButton = MakeModalButton(
                "Quit",
                body,
                "Quit",
                QuitBtnColor,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-20f, 18f),
                out _quitRect,
                out _quitLabel);
            _quitButton.onClick.AddListener(ConfirmQuit);

            RefreshQuitCopy();
            RefreshModalLayout();
            _quitModalRoot.gameObject.SetActive(false);
        }

        static Button MakeModalButton(
            string name,
            Transform parent,
            string text,
            Color tint,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 anchoredPos,
            out RectTransform rect,
            out Text label)
        {
            rect = Ui.Panel(name, parent, anchor, anchor, pivot, anchoredPos, new Vector2(212f, 96f));
            Image border = rect.gameObject.AddComponent<Image>();
            border.sprite = RoundedSprite();
            border.type = Image.Type.Sliced;
            border.color = InkBorderColor;
            border.raycastTarget = true;

            RectTransform fillRect = Ui.Fill("Fill", rect);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            Image bg = fillRect.gameObject.AddComponent<Image>();
            bg.sprite = RoundedSprite();
            bg.type = Image.Type.Sliced;
            bg.color = tint;
            bg.raycastTarget = false;

            Button btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.97f, 0.88f, 1f);
            colors.pressedColor = new Color(0.84f, 0.84f, 0.88f, 1f);
            btn.colors = colors;

            label = Ui.MakeText("Label", fillRect, text, 26, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 18;
            label.resizeTextMaxSize = 26;
            RectTransform lr = label.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(12f, 4f);
            lr.offsetMax = new Vector2(-12f, -4f);
            return btn;
        }

        void RefreshQuitCopy()
        {
            if (_quitTitleLabel == null) return;
            bool fil = string.Equals(SettingsStore.Current.Language, "fil", StringComparison.OrdinalIgnoreCase);
            _quitTitleLabel.text = "Quit Kalyeah?";
            _quitBodyLabel.text = fil
                ? "Uuwi ka na ba mula sa kalye at isasara ang laro?"
                : "Head home from the street and close the game?";
            _cancelLabel.text = "Cancel";
            _quitLabel.text = "Quit";
        }

        void OnQuitSafeAreaChanged(SafeArea _) => RefreshModalLayout();

        void OnSimulatedViewportChanged()
        {
            if (_quitSafeArea != null)
                _quitSafeArea.ForceApply();
            RefreshModalLayout();
        }

        public void RefreshModalLayout()
        {
            if (_quitCard == null) return;
            if (_canvas == null && _modalParent != null)
                _canvas = _modalParent.GetComponentInParent<Canvas>();

            if (_quitSafeArea != null)
                _quitSafeArea.Apply();

            StretchFullBleedToCanvas(_quitScrimRect, _canvas);

            Vector2 safe = ScreenFitMetrics.SafeCanvasSize(_canvas);
            float minTouch = ScreenFitMetrics.ClampTouchSize(96f, ScreenFitMetrics.MinTouchDp, _canvas);
            float minGutter = Mathf.Max(16f, ScreenFitMetrics.DpToCanvasPx(ScreenFitMetrics.MinGutterDp, _canvas));
            float btnHeight = Mathf.Clamp(minTouch, 96f, 118f);
            const float headerH = 76f;

            float cardWidth = Mathf.Clamp(safe.x - 40f, Mathf.Max(380f, minTouch * 2f + minGutter + 52f), 540f);
            cardWidth = Mathf.Min(cardWidth, Mathf.Max(320f, safe.x - 20f));
            float cardHeight = Mathf.Clamp(headerH + btnHeight + 164f, 336f, Mathf.Max(336f, safe.y - 24f));
            cardHeight = Mathf.Min(cardHeight, Mathf.Max(280f, safe.y - 20f));

            float innerWidth = cardWidth - 10f;
            float btnWidth = Mathf.Max(minTouch, (innerWidth - 40f - minGutter) * 0.5f);

            _quitCard.anchoredPosition = Vector2.zero;
            _quitCard.sizeDelta = new Vector2(cardWidth, cardHeight);
            if (_quitHeaderRect != null)
                _quitHeaderRect.sizeDelta = new Vector2(-12f, headerH);
            if (_quitMessageRect != null)
            {
                _quitMessageRect.offsetMin = new Vector2(24f, 18f + btnHeight + 14f);
                _quitMessageRect.offsetMax = new Vector2(-24f, -(headerH + 18f));
            }
            if (_cancelRect != null) _cancelRect.sizeDelta = new Vector2(btnWidth, btnHeight);
            if (_quitRect != null) _quitRect.sizeDelta = new Vector2(btnWidth, btnHeight);
        }

        internal static void StretchFullBleedToCanvas(RectTransform target, Canvas canvas)
        {
            if (target == null) return;
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;

            if (canvas == null || !(target.parent is RectTransform parentRt)) return;
            var canvasRt = canvas.transform as RectTransform;
            if (canvasRt == null || parentRt == canvasRt) return;

            Canvas.ForceUpdateCanvases();
            Rect canvasRect = canvasRt.rect;
            if (canvasRect.width <= 1f || canvasRect.height <= 1f) return;

            Vector3 minWorld = canvasRt.TransformPoint(new Vector3(canvasRect.xMin, canvasRect.yMin, 0f));
            Vector3 maxWorld = canvasRt.TransformPoint(new Vector3(canvasRect.xMax, canvasRect.yMax, 0f));
            Vector3 localMin = parentRt.InverseTransformPoint(minWorld);
            Vector3 localMax = parentRt.InverseTransformPoint(maxWorld);
            Rect parentRect = parentRt.rect;
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = new Vector2(localMin.x - parentRect.xMin, localMin.y - parentRect.yMin);
            target.offsetMax = new Vector2(localMax.x - parentRect.xMax, localMax.y - parentRect.yMax);
        }

        void KeepModalOnTop()
        {
            if (_quitModalRoot != null)
                _quitModalRoot.SetAsLastSibling();
        }

        void PlayFeedback(bool cancel)
        {
            if (cancel) Haptics.CancelTick();
            else Haptics.LightTap();
            _audio?.PlayUiTap(cancel);
        }

        static void FocusInitialSelectable(RectTransform screenRoot)
        {
            if (screenRoot == null || EventSystem.current == null) return;
            Selectable[] selectables = screenRoot.GetComponentsInChildren<Selectable>(false);
            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable s = selectables[i];
                if (s != null && s.IsActive() && s.interactable)
                {
                    EventSystem.current.SetSelectedGameObject(s.gameObject);
                    return;
                }
            }
        }

        internal static Sprite TryUiSprite(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            if (UiSpriteCache.TryGetValue(resourcePath, out Sprite cached) && cached != null)
                return cached;
            Texture2D tex = KalyeahResources.Texture(resourcePath);
            if (tex == null) return null;
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = tex.name;
            UiSpriteCache[resourcePath] = sprite;
            return sprite;
        }

        internal static Sprite RoundedSprite()
        {
            if (_roundedSprite != null) return _roundedSprite;
            const int size = 32;
            const int radius = 9;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "KalyeahMenuRounded",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = 0f;
                    if (x < radius) dx = radius - (x + 0.5f);
                    else if (x >= size - radius) dx = (x + 0.5f) - (size - radius);

                    float dy = 0f;
                    if (y < radius) dy = radius - (y + 0.5f);
                    else if (y >= size - radius) dy = (y + 0.5f) - (size - radius);

                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)(Mathf.Clamp01(radius + 0.5f - dist) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            _roundedSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0u,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            _roundedSprite.name = "KalyeahMenuRounded";
            return _roundedSprite;
        }

        static void DefaultQuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
