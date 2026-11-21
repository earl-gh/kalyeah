using System;
using System.Collections.Generic;
using Kalyeah.Sim;
using UnityEngine;
using UnityEngine.UI;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// Compact interactive Barangay tactical minimap + tap-to-toggle landscape Valorant-style full-map overlay
    /// (docs/SPEC.md §6 &amp; §8, docs/MAPS.md, docs/GRAPHICS.md L64, L74 &amp; L125).
    /// Displays CODM-style callouts (HALL, COURT, BRIDGE, CHAPEL, SARI-SARI, CREEK), active bottom-rim callout tag,
    /// vendor restock rings, moving Selecta bike, Crocodile (Dragon) &amp; Philippine Eagle (Baron) markers/timers,
    /// and accessibility sound-direction edge ripples — strictly 0 B per-frame GC while keeping collapsed <see cref="Root"/>
    /// at exact <see cref="PanelSize"/> (zero overlap with <c>AdsButton</c>).
    /// </summary>
    public sealed class BarangayMinimapView : IDisposable
    {
        public const float PanelSize = 148f;
        public const float RadarRadiusPx = 66f;
        public const float DefaultWorldRadius = 84f;

        const int BackdropTexSize = 256;
        const int FullMapTexSize = 384;
        const int MaxKidDots = 12;
        const int MaxMeriendaDots = 4;
        const int MaxCalloutLabels = 6;
        const int MaxVendorMarkers = 2;
        const int MaxSoundRipples = 6;
        const int MaxRevealedSlots = 12;
        const int MaxTimerSeconds = 300;
        const int LegendRowCount = 6;
        const float FullMapRightColumnWidth = 244f;
        const float FovHalfAngleRad = 0.44f;
        const float FovRayLengthPx = 22f;
        const float FullMapFovRayLengthPx = 42f;
        const float RippleDuration = 0.85f;
        const float StreetHearingRangeM = 28f;
        const float RiverHearingRangeM = 36f;

        // Flat Valorant-style Ink / Paper / Red + neutral objective accents (docs/GRAPHICS.md L155)
        static readonly Color TacticalInk = new Color(0.059f, 0.086f, 0.122f, 0.95f);
        static readonly Color TacticalRailBg = new Color(0.043f, 0.067f, 0.098f, 0.96f);
        static readonly Color TacticalScrim = new Color(0.031f, 0.047f, 0.071f, 0.74f);
        static readonly Color TacticalPillBg = new Color(0.045f, 0.068f, 0.100f, 0.82f);
        static readonly Color TacticalPaper = new Color(0.945f, 0.929f, 0.890f, 0.96f);
        static readonly Color TacticalPaperMuted = new Color(0.860f, 0.845f, 0.810f, 0.82f);
        static readonly Color TacticalBorder = new Color(0.820f, 0.805f, 0.770f, 0.34f);
        static readonly Color TacticalSoftOutline = new Color(0.040f, 0.060f, 0.120f, 0.62f);
        static readonly Color TacticalRed = new Color(0.941f, 0.275f, 0.310f, 0.96f);
        static readonly Color CaptureGold = new Color(1.00f, 0.82f, 0.32f, 0.94f);
        static readonly Color RescueCooldownWhite = new Color(1.00f, 1.00f, 1.00f, 0.50f);
        static readonly Color VendorReadyColor = new Color(0.980f, 0.745f, 0.235f, 0.96f);
        static readonly Color VendorEmptyColor = new Color(0.620f, 0.620f, 0.640f, 0.42f);
        static readonly Color SelectaColor = new Color(0.320f, 0.900f, 0.960f, 0.98f);
        static readonly Color DragonIdleColor = new Color(0.380f, 0.850f, 0.480f, 0.96f);
        static readonly Color BaronIdleColor = new Color(0.980f, 0.760f, 0.240f, 0.98f);
        static readonly Color BossAngryColor = new Color(0.960f, 0.320f, 0.250f, 0.98f);
        static readonly Color BossDormantColor = new Color(0.760f, 0.770f, 0.800f, 0.52f);
        static readonly Color TallCoverLegendColor = new Color(0.360f, 0.720f, 0.440f, 0.95f);
        static readonly Color ClimbLegendColor = new Color(0.950f, 0.780f, 0.340f, 0.96f);
        static readonly Color RippleFootstepColor = new Color(0.960f, 0.940f, 0.880f, 0.92f);
        static readonly Color RippleSkillColor = new Color(1.000f, 0.480f, 0.320f, 0.96f);

        // Compact minimap callout coordinates offset cleanly off Uptown/Riverside bases & Crocodile/Eagle icons
        static readonly SVector2[] CompactCalloutWorldPos =
        {
            new SVector2(0f, -42f),   // HALL (south of Uptown base at -64)
            new SVector2(-38f, -42f), // COURT
            new SVector2(20f, 0f),    // BRIDGE (east of Crocodile at -7, 0)
            new SVector2(0f, 42f),    // CHAPEL (north of Riverside base at +64)
            new SVector2(-28f, 28f),  // SARI-SARI
            new SVector2(44f, 20f),   // CREEK (clear of Eagle at 54,-3.8 & Fishball at 42.5,-2.6)
        };

        static readonly string[] LegendCopy =
        {
            "DRAGON (CROC)\n+8% Team Dmg · 90s",
            "BARON (EAGLE)\n+8% Role Buff · 90s",
            "TAHO / FISHBALL\n+40 HP · 30s Restock",
            "SELECTA BIKE\n+20 HP · Speed + H2O",
            "TALL COVER\nHidden Beyond 6 m",
            "CLIMB SPOTS\nBackboard · Roof · Jeep",
        };

        static readonly Color[] LegendSwatches =
        {
            DragonIdleColor,
            BaronIdleColor,
            VendorReadyColor,
            SelectaColor,
            TallCoverLegendColor,
            ClimbLegendColor,
        };

        static readonly string[] PrebuiltTimers = BuildTimerTable(MaxTimerSeconds);
        static readonly string[] PrebuiltCounts = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        struct SoundRippleSlot
        {
            public Image Graphic;
            public RectTransform Rect;
            public SVector2 SourceXz;
            public float TimeLeft;
            public float Intensity;
            public bool IsSkill;
        }

        struct RevealedSlot
        {
            public int PlayerId;
            public float TimeLeft;
        }

        public RectTransform Root { get; }
        public RectTransform FullMapRoot { get; }
        public bool IsVisible { get; private set; }
        public bool IsFullMapOpen { get; private set; }
        public bool ShowEnemies { get; set; }
        public bool LeftHanded { get; private set; }

        public event Action<bool> FullMapToggled;

        readonly Canvas _canvas;
        readonly Image _backdropImage;
        readonly Image[] _rescueRects = new Image[2];
        readonly Image[] _baseRings = new Image[2];
        readonly Image[] _basePosts = new Image[2];
        readonly Image[] _captureHalos = new Image[2];
        readonly Image[] _meriendaDots = new Image[MaxMeriendaDots];
        readonly Image[] _vendorDots = new Image[MaxVendorMarkers];
        readonly Image[] _vendorRings = new Image[MaxVendorMarkers];
        readonly Image _selectaDot;
        readonly Image _dragonDot;
        readonly Image _baronDot;
        readonly Text _dragonMiniTimer;
        readonly Text _baronMiniTimer;
        readonly Text[] _miniCallouts = new Text[MaxCalloutLabels];
        readonly Text _activeCalloutLabel;
        readonly SoundRippleSlot[] _soundRipples = new SoundRippleSlot[MaxSoundRipples];
        readonly Image[] _kidDots = new Image[MaxKidDots];
        readonly RectTransform _selfDotRect;
        readonly Image _selfDotImage;
        readonly RectTransform _fovLeftRect;
        readonly RectTransform _fovRightRect;

        // Landscape Valorant Full-Map Overlay widgets (all pre-allocated, 0 B per-frame GC when open)
        readonly RectTransform _fullScrimRect;
        readonly RectTransform _fullFrameRect;
        readonly RectTransform _fullLeftColRect;
        readonly RectTransform _fullRightColRect;
        readonly RectTransform _fullMapAreaRect;
        readonly Image _fullBackdropImage;
        readonly RectTransform _fullCloseBtnRect;
        readonly RectTransform _fullLegendHeaderRect;
        readonly RectTransform[] _fullLegendRows = new RectTransform[LegendRowCount];
        readonly Text _fullTopHalfLabel;
        readonly Text _fullBottomHalfLabel;
        readonly Text[] _fullCallouts = new Text[MaxCalloutLabels];
        readonly Image[] _fullBaseRings = new Image[2];
        readonly Image[] _fullCaptureHalos = new Image[2];
        readonly Image[] _fullJailBoxes = new Image[2];
        readonly Text[] _fullJailCounts = new Text[2];
        readonly Image[] _fullVendorDots = new Image[MaxVendorMarkers];
        readonly Image[] _fullVendorRings = new Image[MaxVendorMarkers];
        readonly Text[] _fullVendorLabels = new Text[MaxVendorMarkers];
        readonly Image _fullSelectaDot;
        readonly Image _fullDragonDot;
        readonly Text _fullDragonLabel;
        readonly Image _fullBaronDot;
        readonly Text _fullBaronLabel;
        readonly Image[] _fullKidDots = new Image[MaxKidDots];
        readonly RectTransform _fullSelfDotRect;
        readonly Image _fullSelfDotImage;
        readonly RectTransform _fullFovLeftRect;
        readonly RectTransform _fullFovRightRect;

        readonly RevealedSlot[] _revealedEnemies = new RevealedSlot[MaxRevealedSlots];
        readonly int[] _lastEffectIds = new int[16];
        int _lastEffectCount;
        float _lastRefreshClock = -1f;
        float _footstepRippleCooldown;
        float _fullMapRadiusPx = 218f;
        int _backdropOrientedSide = 1;

        SimBridge _lastSim;
        float _lastCameraYaw;
        BarangayNeutrals _lastNeutrals;

        MapLayoutData _layout;
        BarangayNeutrals _neutrals;
        Texture2D _backdropTex;
        Sprite _backdropSprite;
        Texture2D _fullMapTex;
        Sprite _fullMapSprite;
        bool _disposed;

        public BarangayMinimapView(Transform parent, MapLayoutData layout = null, Canvas canvas = null, bool initialVisible = false)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _canvas = canvas;
            _layout = layout;

            Root = Ui.Panel(
                "BarangayMinimap",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -14f),
                new Vector2(PanelSize, PanelSize));

            var backdropGo = new GameObject("RadarBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            backdropGo.transform.SetParent(Root, false);
            _backdropImage = backdropGo.GetComponent<Image>();
            _backdropImage.raycastTarget = true;
            _backdropImage.rectTransform.anchorMin = Vector2.zero;
            _backdropImage.rectTransform.anchorMax = Vector2.one;
            _backdropImage.rectTransform.sizeDelta = Vector2.zero;

            var tapBtn = backdropGo.GetComponent<Button>();
            tapBtn.transition = Selectable.Transition.None;
            tapBtn.targetGraphic = _backdropImage;
            tapBtn.onClick.AddListener(OnMinimapTapped);

            RebuildBackdropTexture();

            // 6 CODM/Valorant callout labels on the compact minimap (>= 10 px, light outline, offset off bases/bosses)
            Font displayFont = KalyeahResources.DisplayFont();
            IReadOnlyList<CalloutRegion> defaultCallouts = MapLayout.DefaultBarangayCallouts();
            for (int i = 0; i < MaxCalloutLabels; i++)
            {
                string title = i < defaultCallouts.Count ? defaultCallouts[i].Name : "";
                Text lbl = Ui.MakeText($"MiniCallout_{i}", Root, title, 10, TacticalPaperMuted, TextAnchor.MiddleCenter, displayFont);
                SetCenteredPivot(lbl.rectTransform, new Vector2(58f, 14f));
                SoftenOutline(lbl);
                _miniCallouts[i] = lbl;
            }

            // 2 Rescue Zones (own + enemy)
            for (int i = 0; i < 2; i++)
            {
                Image rescue = KomiksUi.MakeSlicedFill($"Rescue_{i}", Root, RescueCooldownWhite, 3);
                SetCenteredPivot(rescue.rectTransform, new Vector2(10f, 14f));
                _rescueRects[i] = rescue;
            }

            // 2 Base circles + electric posts + capture pulse halos (local-team capturing only!)
            Sprite ringSprite = KomiksUi.RingSprite(64, 4);
            for (int i = 0; i < 2; i++)
            {
                Image halo = Ui.MakeImage($"BaseCaptureHalo_{i}", Root, CaptureGold);
                halo.sprite = ringSprite;
                SetCenteredPivot(halo.rectTransform, new Vector2(22f, 22f));
                halo.gameObject.SetActive(false);
                _captureHalos[i] = halo;

                Image ring = Ui.MakeImage($"BaseRing_{i}", Root, Color.white);
                ring.sprite = ringSprite;
                SetCenteredPivot(ring.rectTransform, new Vector2(16f, 16f));
                _baseRings[i] = ring;

                Image post = Ui.MakeImage($"BasePost_{i}", Root, Color.white, round: true);
                SetCenteredPivot(post.rectTransform, new Vector2(6f, 6f));
                _basePosts[i] = post;
            }

            // Legacy Merienda stand dots (kept for backward compatibility)
            for (int i = 0; i < MaxMeriendaDots; i++)
            {
                Image stand = Ui.MakeImage($"Merienda_{i}", Root, KomiksUi.MangoGold, round: true);
                SetCenteredPivot(stand.rectTransform, new Vector2(6f, 6f));
                stand.gameObject.SetActive(false);
                _meriendaDots[i] = stand;
            }

            // 2 Fixed Vendors (Mang Taho & Fishball Cart) with restock rings
            for (int i = 0; i < MaxVendorMarkers; i++)
            {
                Image vRing = Ui.MakeImage($"VendorRing_{i}", Root, VendorReadyColor);
                vRing.sprite = ringSprite;
                SetCenteredPivot(vRing.rectTransform, new Vector2(12f, 12f));
                _vendorRings[i] = vRing;

                Image vDot = Ui.MakeImage($"VendorDot_{i}", Root, VendorReadyColor, round: true);
                SetCenteredPivot(vDot.rectTransform, new Vector2(6f, 6f));
                _vendorDots[i] = vDot;
            }

            // Selecta ice-cream bike moving dot
            _selectaDot = Ui.MakeImage("SelectaDot", Root, SelectaColor, round: true);
            SetCenteredPivot(_selectaDot.rectTransform, new Vector2(7f, 7f));
            _selectaDot.gameObject.SetActive(false);

            // Crocodile (Dragon) & Philippine Eagle (Baron) markers + compact >= 10 px timers
            _dragonDot = Ui.MakeImage("DragonMarker", Root, DragonIdleColor);
            SetCenteredPivot(_dragonDot.rectTransform, new Vector2(9.5f, 9.5f));
            _dragonDot.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            _dragonMiniTimer = Ui.MakeText("DragonTimer", Root, "", 10, TacticalPaper, TextAnchor.MiddleCenter, displayFont);
            SetCenteredPivot(_dragonMiniTimer.rectTransform, new Vector2(44f, 14f));
            SoftenOutline(_dragonMiniTimer);
            _dragonMiniTimer.gameObject.SetActive(false);

            _baronDot = Ui.MakeImage("BaronMarker", Root, BaronIdleColor);
            SetCenteredPivot(_baronDot.rectTransform, new Vector2(10f, 10f));
            _baronDot.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            _baronMiniTimer = Ui.MakeText("BaronTimer", Root, "", 10, TacticalPaper, TextAnchor.MiddleCenter, displayFont);
            SetCenteredPivot(_baronMiniTimer.rectTransform, new Vector2(44f, 14f));
            SoftenOutline(_baronMiniTimer);
            _baronMiniTimer.gameObject.SetActive(false);

            // Remote ally & seen/revealed enemy dots
            for (int i = 0; i < MaxKidDots; i++)
            {
                Image dot = Ui.MakeImage($"KidDot_{i}", Root, Color.white, round: true);
                SetCenteredPivot(dot.rectTransform, new Vector2(8f, 8f));
                dot.gameObject.SetActive(false);
                _kidDots[i] = dot;
            }

            // Local camera FOV rays + self dot on top
            _fovLeftRect = BuildFovRay(Root, "FovRayLeft", FovRayLengthPx);
            _fovRightRect = BuildFovRay(Root, "FovRayRight", FovRayLengthPx);

            _selfDotImage = Ui.MakeImage("SelfDot", Root, KomiksUi.SelfHealthColor(0), round: true);
            _selfDotRect = _selfDotImage.rectTransform;
            SetCenteredPivot(_selfDotRect, new Vector2(10f, 10f));
            _selfDotImage.gameObject.SetActive(false);

            // Accessibility sound-direction ripples at minimap edge (docs/GRAPHICS.md L74)
            for (int i = 0; i < MaxSoundRipples; i++)
            {
                Image rip = Ui.MakeImage($"SoundRipple_{i}", Root, RippleFootstepColor);
                rip.sprite = ringSprite;
                SetCenteredPivot(rip.rectTransform, new Vector2(14f, 14f));
                rip.gameObject.SetActive(false);
                _soundRipples[i] = new SoundRippleSlot { Graphic = rip, Rect = rip.rectTransform };
            }

            // Subtle CODM-style bottom-rim active-callout pill strictly inside Root's 148x148 bounds
            Image activePill = KomiksUi.MakeSlicedFill("ActiveCalloutPill", Root, TacticalPillBg, 4);
            RectTransform pillRt = activePill.rectTransform;
            pillRt.anchorMin = pillRt.anchorMax = new Vector2(0.5f, 0f);
            pillRt.pivot = new Vector2(0.5f, 0f);
            pillRt.anchoredPosition = new Vector2(0f, 2f);
            pillRt.sizeDelta = new Vector2(96f, 16f);
            _activeCalloutLabel = Ui.MakeText("ActiveCalloutText", pillRt, "BARANGAY", 11, TacticalPaper, TextAnchor.MiddleCenter, displayFont);
            StretchFill(_activeCalloutLabel.rectTransform);
            SoftenOutline(_activeCalloutLabel);

            // Sibling Full-Map Overlay (closed by default; never expands Root or overlaps AdsButton!)
            FullMapRoot = Ui.Fill("BarangayFullMapOverlay", parent);
            BuildFullMapOverlay(
                ringSprite,
                displayFont,
                out _fullScrimRect,
                out _fullFrameRect,
                out _fullLeftColRect,
                out _fullRightColRect,
                out _fullMapAreaRect,
                out _fullBackdropImage,
                out _fullCloseBtnRect,
                out _fullLegendHeaderRect,
                out _fullTopHalfLabel,
                out _fullBottomHalfLabel,
                out _fullSelectaDot,
                out _fullDragonDot,
                out _fullDragonLabel,
                out _fullBaronDot,
                out _fullBaronLabel,
                out _fullSelfDotRect,
                out _fullSelfDotImage,
                out _fullFovLeftRect,
                out _fullFovRightRect);
            RebuildFullMapTexture();
            FullMapRoot.gameObject.SetActive(false);

            // Initial default marker placement so even static captures read cleanly
            SyncCalloutLabels(DefaultWorldRadius, 1);

            SetVisible(initialVisible);
        }

        void BuildFullMapOverlay(
            Sprite ringSprite,
            Font displayFont,
            out RectTransform scrimRect,
            out RectTransform frameRect,
            out RectTransform leftColRect,
            out RectTransform rightColRect,
            out RectTransform mapAreaRect,
            out Image mapImg,
            out RectTransform closeBtnRect,
            out RectTransform legendHeaderRect,
            out Text topHalfLbl,
            out Text bottomHalfLbl,
            out Image selectaDot,
            out Image dragonDot,
            out Text dragonLbl,
            out Image baronDot,
            out Text baronLbl,
            out RectTransform selfDotRect,
            out Image selfDotImg,
            out RectTransform fovL,
            out RectTransform fovR)
        {
            // Full-bleed tap scrim (sibling before FullMapFrame — zero nested Selectables; covers notch + gesture pill)
            Image scrim = Ui.MakeImage("FullMapScrim", FullMapRoot, TacticalScrim, raycast: true);
            scrimRect = scrim.rectTransform;
            StretchFill(scrimRect);
            var scrimBtn = scrim.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.targetGraphic = scrim;
            scrimBtn.onClick.AddListener(() => SetFullMapOpen(false));

            // Flat Valorant Ink/Paper 2-column landscape tactical container (left: unobstructed map, right: close + legend)
            frameRect = Ui.Panel(
                "FullMapFrame",
                FullMapRoot,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(760f, 600f));

            Image frameBg = Ui.MakeImage("FrameBg", frameRect, TacticalInk, raycast: true);
            StretchFill(frameBg.rectTransform);

            // Crisp 3 px Valorant top crimson accent bar + subtle perimeter border
            Image topAccent = Ui.MakeImage("TopAccent", frameRect, TacticalRed);
            topAccent.rectTransform.anchorMin = new Vector2(0f, 1f);
            topAccent.rectTransform.anchorMax = new Vector2(1f, 1f);
            topAccent.rectTransform.pivot = new Vector2(0.5f, 1f);
            topAccent.rectTransform.anchoredPosition = Vector2.zero;
            topAccent.rectTransform.sizeDelta = new Vector2(0f, 3f);

            Image frameStroke = KomiksUi.MakeInkPanel("FrameBorder", frameRect, Color.clear, TacticalBorder, radiusPx: 4, borderPx: 2);
            StretchFill(frameStroke.rectTransform);

            // ---- Left Column: Map Title, UPTOWN/RIVERSIDE Headers, Unobstructed Tactical Map ----
            leftColRect = Ui.Panel(
                "FullMapLeftCol",
                frameRect,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(760f - FullMapRightColumnWidth, 0f));

            Text title = Ui.MakeText("MapTitle", leftColRect, "BARANGAY // TACTICAL MAP", 15, TacticalPaper, TextAnchor.MiddleLeft, displayFont);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 11;
            title.resizeTextMaxSize = 15;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(18f, -10f);
            title.rectTransform.sizeDelta = new Vector2(-36f, 24f);

            topHalfLbl = Ui.MakeText("TopHalfHeader", leftColRect, "UPTOWN", 15, TacticalPaperMuted, TextAnchor.MiddleCenter, displayFont);
            topHalfLbl.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            topHalfLbl.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            topHalfLbl.rectTransform.pivot = new Vector2(0.5f, 1f);
            topHalfLbl.rectTransform.anchoredPosition = new Vector2(0f, -38f);
            topHalfLbl.rectTransform.sizeDelta = new Vector2(200f, 20f);

            bottomHalfLbl = Ui.MakeText("BottomHalfHeader", leftColRect, "RIVERSIDE", 15, TacticalPaperMuted, TextAnchor.MiddleCenter, displayFont);
            bottomHalfLbl.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            bottomHalfLbl.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            bottomHalfLbl.rectTransform.pivot = new Vector2(0.5f, 0f);
            bottomHalfLbl.rectTransform.anchoredPosition = new Vector2(0f, 12f);
            bottomHalfLbl.rectTransform.sizeDelta = new Vector2(200f, 20f);

            mapAreaRect = Ui.Panel(
                "FullMapCanvas",
                leftColRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f),
                new Vector2(468f, 468f));

            mapImg = Ui.MakeImage("FullMapBackdrop", mapAreaRect, Color.white);
            StretchFill(mapImg.rectTransform);

            // ---- Right Column: >= 48 dp Close Button + Stacked Readable Legend Rows ----
            rightColRect = Ui.Panel(
                "FullMapRightCol",
                frameRect,
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0.5f),
                Vector2.zero,
                new Vector2(FullMapRightColumnWidth, 0f));

            Image railBg = Ui.MakeImage("RightRailBg", rightColRect, TacticalRailBg);
            StretchFill(railBg.rectTransform);

            Image divider = Ui.MakeImage("ColumnDivider", rightColRect, TacticalBorder);
            divider.rectTransform.anchorMin = new Vector2(0f, 0f);
            divider.rectTransform.anchorMax = new Vector2(0f, 1f);
            divider.rectTransform.pivot = new Vector2(0f, 0.5f);
            divider.rectTransform.anchoredPosition = Vector2.zero;
            divider.rectTransform.sizeDelta = new Vector2(1.5f, -6f);

            float closeSize = ScreenFitMetrics.ClampTouchSize(52f, ScreenFitMetrics.MinTouchDp, _canvas);
            closeBtnRect = Ui.Panel(
                "FullMapCloseButton",
                rightColRect,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-12f, -12f),
                new Vector2(closeSize, closeSize));
            Image closeBg = Ui.MakeImage("CloseBg", closeBtnRect, TacticalRed, raycast: true);
            StretchFill(closeBg.rectTransform);
            Text closeTxt = Ui.MakeText("CloseText", closeBtnRect, "X", 22, TacticalPaper, TextAnchor.MiddleCenter, displayFont);
            StretchFill(closeTxt.rectTransform);
            var closeBtn = closeBtnRect.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() => SetFullMapOpen(false));

            Text legendHeader = Ui.MakeText("LegendHeader", rightColRect, "MAP KEY", 14, TacticalPaper, TextAnchor.MiddleLeft, displayFont);
            legendHeaderRect = legendHeader.rectTransform;
            legendHeaderRect.anchorMin = new Vector2(0f, 1f);
            legendHeaderRect.anchorMax = new Vector2(1f, 1f);
            legendHeaderRect.pivot = new Vector2(0f, 1f);
            legendHeaderRect.anchoredPosition = new Vector2(16f, -(closeSize + 24f));
            legendHeaderRect.sizeDelta = new Vector2(-32f, 22f);

            Font bodyFont = KalyeahResources.UiFont();
            for (int i = 0; i < LegendRowCount; i++)
            {
                RectTransform rowRt = Ui.Panel(
                    $"LegendRow_{i}",
                    rightColRect,
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(14f, -(closeSize + 52f + i * 54f)),
                    new Vector2(-28f, 46f));
                _fullLegendRows[i] = rowRt;

                Image swatch = Ui.MakeImage("Swatch", rowRt, LegendSwatches[i], round: i >= 2);
                swatch.rectTransform.anchorMin = swatch.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                swatch.rectTransform.pivot = new Vector2(0f, 0.5f);
                swatch.rectTransform.anchoredPosition = new Vector2(2f, 0f);
                swatch.rectTransform.sizeDelta = new Vector2(12f, 12f);
                if (i < 2) swatch.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);

                Text rowLbl = Ui.MakeText("Copy", rowRt, LegendCopy[i], 14, TacticalPaper, TextAnchor.MiddleLeft, bodyFont);
                rowLbl.resizeTextForBestFit = true;
                rowLbl.resizeTextMinSize = 10;
                rowLbl.resizeTextMaxSize = 14;
                rowLbl.horizontalOverflow = HorizontalWrapMode.Wrap;
                rowLbl.verticalOverflow = VerticalWrapMode.Overflow;
                rowLbl.rectTransform.anchorMin = new Vector2(0f, 0f);
                rowLbl.rectTransform.anchorMax = new Vector2(1f, 1f);
                rowLbl.rectTransform.offsetMin = new Vector2(22f, 2f);
                rowLbl.rectTransform.offsetMax = new Vector2(-2f, -2f);
                SoftenOutline(rowLbl);
            }

            // ---- Full-Map Tactical Entities (inside FullMapCanvas) ----
            IReadOnlyList<CalloutRegion> callouts = MapLayout.DefaultBarangayCallouts();
            for (int i = 0; i < MaxCalloutLabels; i++)
            {
                string titleStr = i < callouts.Count ? callouts[i].Name : "";
                Text lbl = Ui.MakeText($"FullCallout_{i}", mapAreaRect, titleStr, 13, TacticalPaper, TextAnchor.MiddleCenter, displayFont);
                SetCenteredPivot(lbl.rectTransform, new Vector2(96f, 18f));
                SoftenOutline(lbl);
                _fullCallouts[i] = lbl;
            }

            for (int i = 0; i < 2; i++)
            {
                Image halo = Ui.MakeImage($"FullCaptureHalo_{i}", mapAreaRect, CaptureGold);
                halo.sprite = ringSprite;
                SetCenteredPivot(halo.rectTransform, new Vector2(46f, 46f));
                halo.gameObject.SetActive(false);
                _fullCaptureHalos[i] = halo;

                Image ring = Ui.MakeImage($"FullBaseRing_{i}", mapAreaRect, Color.white);
                ring.sprite = ringSprite;
                SetCenteredPivot(ring.rectTransform, new Vector2(36f, 36f));
                _fullBaseRings[i] = ring;

                Image jail = KomiksUi.MakeSlicedFill($"FullJail_{i}", mapAreaRect, RescueCooldownWhite, 4);
                SetCenteredPivot(jail.rectTransform, new Vector2(22f, 26f));
                _fullJailBoxes[i] = jail;

                Text jCount = Ui.MakeText($"FullJailCount_{i}", jail.transform, "0", 13, TacticalPaper, TextAnchor.MiddleCenter, displayFont);
                StretchFill(jCount.rectTransform);
                _fullJailCounts[i] = jCount;
            }

            string[] vShort = { "TAHO", "FISHBALL" };
            for (int i = 0; i < MaxVendorMarkers; i++)
            {
                Image vRing = Ui.MakeImage($"FullVendorRing_{i}", mapAreaRect, VendorReadyColor);
                vRing.sprite = ringSprite;
                SetCenteredPivot(vRing.rectTransform, new Vector2(22f, 22f));
                _fullVendorRings[i] = vRing;

                Image vDot = Ui.MakeImage($"FullVendorDot_{i}", mapAreaRect, VendorReadyColor, round: true);
                SetCenteredPivot(vDot.rectTransform, new Vector2(11f, 11f));
                _fullVendorDots[i] = vDot;

                Text vLbl = Ui.MakeText($"FullVendorLbl_{i}", mapAreaRect, vShort[i], 11, VendorReadyColor, TextAnchor.MiddleCenter, displayFont);
                SetCenteredPivot(vLbl.rectTransform, new Vector2(76f, 16f));
                SoftenOutline(vLbl);
                _fullVendorLabels[i] = vLbl;
            }

            selectaDot = Ui.MakeImage("FullSelectaDot", mapAreaRect, SelectaColor, round: true);
            SetCenteredPivot(selectaDot.rectTransform, new Vector2(13f, 13f));
            selectaDot.gameObject.SetActive(false);

            dragonDot = Ui.MakeImage("FullDragonDot", mapAreaRect, DragonIdleColor);
            SetCenteredPivot(dragonDot.rectTransform, new Vector2(18f, 18f));
            dragonDot.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            dragonLbl = Ui.MakeText("FullDragonLbl", mapAreaRect, "CROC", 11, TacticalPaper, TextAnchor.MiddleCenter, displayFont);
            SetCenteredPivot(dragonLbl.rectTransform, new Vector2(76f, 16f));
            SoftenOutline(dragonLbl);

            baronDot = Ui.MakeImage("FullBaronDot", mapAreaRect, BaronIdleColor);
            SetCenteredPivot(baronDot.rectTransform, new Vector2(20f, 20f));
            baronDot.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            baronLbl = Ui.MakeText("FullBaronLbl", mapAreaRect, "EAGLE", 11, TacticalPaper, TextAnchor.MiddleCenter, displayFont);
            SetCenteredPivot(baronLbl.rectTransform, new Vector2(76f, 16f));
            SoftenOutline(baronLbl);

            for (int i = 0; i < MaxKidDots; i++)
            {
                Image dot = Ui.MakeImage($"FullKidDot_{i}", mapAreaRect, Color.white, round: true);
                SetCenteredPivot(dot.rectTransform, new Vector2(13f, 13f));
                dot.gameObject.SetActive(false);
                _fullKidDots[i] = dot;
            }

            fovL = BuildFovRay(mapAreaRect, "FullFovLeft", FullMapFovRayLengthPx);
            fovR = BuildFovRay(mapAreaRect, "FullFovRight", FullMapFovRayLengthPx);

            selfDotImg = Ui.MakeImage("FullSelfDot", mapAreaRect, KomiksUi.SelfHealthColor(0), round: true);
            selfDotRect = selfDotImg.rectTransform;
            SetCenteredPivot(selfDotRect, new Vector2(16f, 16f));
            selfDotImg.gameObject.SetActive(false);
        }

        static RectTransform BuildFovRay(Transform parent, string name, float lengthPx)
        {
            Image ray = Ui.MakeImage(name, parent, new Color(1f, 1f, 1f, 0.36f));
            RectTransform rt = ray.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(2f, lengthPx);
            ray.gameObject.SetActive(false);
            return rt;
        }

        static void SetCenteredPivot(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }

        static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void SoftenOutline(Text label)
        {
            if (label != null && label.TryGetComponent(out Outline ol))
            {
                ol.effectColor = TacticalSoftOutline;
                ol.effectDistance = new Vector2(1f, -1f);
            }
        }

        void OnMinimapTapped()
        {
            if (_disposed || !IsVisible) return;
            Haptics.LightTap();
            ToggleFullMap();
        }

        public void BindNeutrals(BarangayNeutrals neutrals)
        {
            if (_disposed) return;
            _neutrals = neutrals;
        }

        public void SetVisible(bool visible)
        {
            if (_disposed) return;
            IsVisible = visible;
            if (Root != null && Root.gameObject.activeSelf != visible)
                Root.gameObject.SetActive(visible);
            if (!visible && IsFullMapOpen)
                SetFullMapOpen(false);
        }

        public void Toggle() => SetVisible(!IsVisible);

        public void SetFullMapOpen(bool open)
        {
            if (_disposed) return;
            if (IsFullMapOpen == open) return;
            IsFullMapOpen = open;
            if (FullMapRoot != null)
            {
                if (open) LayoutFullMapOverlay();
                FullMapRoot.gameObject.SetActive(open);
                if (open)
                {
                    FullMapRoot.SetAsLastSibling();
                    SyncFullMapImmediate();
                }
            }
            FullMapToggled?.Invoke(open);
        }

        public void ToggleFullMap() => SetFullMapOpen(!IsFullMapOpen);

        /// <summary>Marks an enemy id as revealed on minimap &amp; full map for <paramref name="durationSeconds"/> (0 B GC).</summary>
        public void MarkEnemyRevealed(int playerId, float durationSeconds)
        {
            if (_disposed || durationSeconds <= 0f) return;
            int empty = -1;
            for (int i = 0; i < MaxRevealedSlots; i++)
            {
                if (_revealedEnemies[i].PlayerId == playerId && _revealedEnemies[i].TimeLeft > 0f)
                {
                    _revealedEnemies[i].TimeLeft = Mathf.Max(_revealedEnemies[i].TimeLeft, durationSeconds);
                    return;
                }
                if (empty < 0 && _revealedEnemies[i].TimeLeft <= 0f)
                    empty = i;
            }
            int slot = empty >= 0 ? empty : 0;
            _revealedEnemies[slot] = new RevealedSlot { PlayerId = playerId, TimeLeft = durationSeconds };
        }

        public void ClearRevealedEnemies()
        {
            for (int i = 0; i < MaxRevealedSlots; i++)
                _revealedEnemies[i] = default;
        }

        /// <summary>Spawns an accessibility sound-direction ripple on the minimap edge toward <paramref name="sourceSimXz"/> (0 B GC).</summary>
        public void PushSoundRipple(SVector2 sourceSimXz, float intensity = 1f, bool isSkill = false)
        {
            if (_disposed || !IsVisible) return;
            int chosen = 0;
            float lowest = float.MaxValue;
            for (int i = 0; i < MaxSoundRipples; i++)
            {
                if (_soundRipples[i].TimeLeft <= 0f)
                {
                    chosen = i;
                    break;
                }
                if (_soundRipples[i].TimeLeft < lowest)
                {
                    lowest = _soundRipples[i].TimeLeft;
                    chosen = i;
                }
            }
            _soundRipples[chosen].SourceXz = sourceSimXz;
            _soundRipples[chosen].TimeLeft = RippleDuration;
            _soundRipples[chosen].Intensity = Mathf.Clamp(intensity, 0.35f, 1.5f);
            _soundRipples[chosen].IsSkill = isSkill;
        }

        public void SetLeftHanded(bool leftHanded)
        {
            if (_disposed || LeftHanded == leftHanded) return;
            LeftHanded = leftHanded;
            if (leftHanded)
            {
                Root.anchorMin = Root.anchorMax = new Vector2(1f, 1f);
                Root.pivot = new Vector2(1f, 1f);
                Root.anchoredPosition = new Vector2(-18f, -14f);
            }
            else
            {
                Root.anchorMin = Root.anchorMax = new Vector2(0f, 1f);
                Root.pivot = new Vector2(0f, 1f);
                Root.anchoredPosition = new Vector2(18f, -14f);
            }
        }

        public void SetLayout(MapLayoutData layout)
        {
            if (_disposed) return;
            _layout = layout;
            RebuildBackdropTexture();
            RebuildFullMapTexture();
            if (IsFullMapOpen) SyncFullMapImmediate();
        }

        void LayoutFullMapOverlay()
        {
            if (_fullScrimRect != null)
                MenuStack.StretchFullBleedToCanvas(_fullScrimRect, _canvas);

            if (_fullFrameRect == null || _fullMapAreaRect == null) return;
            Vector2 safe = ScreenFitMetrics.SafeCanvasSize(_canvas);
            float frameW = Mathf.Clamp(safe.x - 36f, 560f, 760f);
            float frameH = Mathf.Clamp(safe.y - 24f, 420f, 600f);
            _fullFrameRect.sizeDelta = new Vector2(frameW, frameH);

            float rightColW = Mathf.Clamp(FullMapRightColumnWidth, 224f, frameW * 0.36f);
            float leftColW = frameW - rightColW;
            if (_fullLeftColRect != null) _fullLeftColRect.sizeDelta = new Vector2(leftColW, 0f);
            if (_fullRightColRect != null) _fullRightColRect.sizeDelta = new Vector2(rightColW, 0f);

            // Unobstructed square tactical map inside Left Column with clean top/bottom header clearance
            float mapSpan = Mathf.Clamp(Mathf.Min(frameH - 116f, leftColW - 36f), 260f, 468f);
            _fullMapAreaRect.sizeDelta = new Vector2(mapSpan, mapSpan);
            _fullMapRadiusPx = mapSpan * 0.47f;

            // >= 48 dp Close button pinned top-right of Right Column; Legend header & 6 rows stacked cleanly underneath
            float closeSize = ScreenFitMetrics.ClampTouchSize(52f, ScreenFitMetrics.MinTouchDp, _canvas);
            if (_fullCloseBtnRect != null)
                _fullCloseBtnRect.sizeDelta = new Vector2(closeSize, closeSize);

            float keyHeaderY = -(closeSize + 24f);
            if (_fullLegendHeaderRect != null)
                _fullLegendHeaderRect.anchoredPosition = new Vector2(16f, keyHeaderY);

            float rowsTop = closeSize + 52f;
            float availH = Mathf.Max(220f, frameH - rowsTop - 12f);
            float rowPitch = Mathf.Clamp(availH / LegendRowCount, 38f, 68f);
            float rowH = Mathf.Clamp(rowPitch - 4f, 34f, 54f);
            for (int i = 0; i < LegendRowCount; i++)
            {
                RectTransform rowRt = _fullLegendRows[i];
                if (rowRt == null) continue;
                rowRt.anchoredPosition = new Vector2(14f, -(rowsTop + i * rowPitch));
                rowRt.sizeDelta = new Vector2(-28f, rowH);
            }
        }

        /// <summary>Immediately positions all full-map markers on the opening frame so nothing ever piles at (0, 0).</summary>
        void SyncFullMapImmediate()
        {
            if (_lastSim != null)
            {
                Refresh(_lastSim, _lastCameraYaw, _lastNeutrals ?? _neutrals);
                return;
            }

            float worldR = _layout != null && _layout.ArenaRadius > 1f ? _layout.ArenaRadius : DefaultWorldRadius;
            int ownSide = _neutrals != null ? _neutrals.SideSwap.SideForTeam(0) : 1;
            int cbMode = SettingsStore.Current.ColorblindMode;
            float fullScalePx = _fullMapRadiusPx / Mathf.Max(1f, worldR);

            SyncBackdropOrientation(ownSide);
            SyncCalloutLabels(worldR, ownSide);
            SyncFullMapHalfHeaders(ownSide);

            // Default base rings & jails
            for (int t = 0; t < 2; t++)
            {
                int side = t == 0 ? 1 : -1;
                SVector2 center = _layout != null ? _layout.BaseCenter(side) : new SVector2(0f, side * (worldR - 20f));
                float radiusM = _layout != null ? _layout.BaseRadius : 12.5f;
                float fullDiam = Mathf.Clamp(radiusM * 2f * fullScalePx, 24f, 48f);
                Color col = KomiksUi.TeamColor(t, 0, cbMode);
                _fullBaseRings[t].rectTransform.anchoredPosition = WorldToFullMap(center, worldR, ownSide, _fullMapRadiusPx);
                _fullBaseRings[t].rectTransform.sizeDelta = new Vector2(fullDiam, fullDiam);
                _fullBaseRings[t].color = col;
                SetActiveIfNeeded(_fullCaptureHalos[t].gameObject, false);

                GroundRect zone = _layout != null
                    ? _layout.RescueRect(t == 0 ? -1 : 1)
                    : new GroundRect(-side * (worldR * 0.78f), -side * (worldR * 0.375f), 9f, 15f);
                _fullJailBoxes[t].rectTransform.anchoredPosition = WorldToFullMap(zone.Center, worldR, ownSide, _fullMapRadiusPx);
                Color jCol = col;
                jCol.a = 0.78f;
                _fullJailBoxes[t].color = jCol;
            }

            // Default vendors + Crocodile + Philippine Eagle
            IReadOnlyList<VendorSpot> defVendors = MapLayout.DefaultBarangayVendors();
            for (int i = 0; i < MaxVendorMarkers && i < defVendors.Count; i++)
            {
                Vector2 fp = WorldToFullMap(ToSVec2(defVendors[i].Position), worldR, ownSide, _fullMapRadiusPx);
                SetActiveIfNeeded(_fullVendorDots[i].gameObject, true);
                SetActiveIfNeeded(_fullVendorRings[i].gameObject, true);
                SetActiveIfNeeded(_fullVendorLabels[i].gameObject, true);
                _fullVendorDots[i].rectTransform.anchoredPosition = fp;
                _fullVendorRings[i].rectTransform.anchoredPosition = fp;
                _fullVendorLabels[i].rectTransform.anchoredPosition = fp + new Vector2(0f, -16f);
            }

            Vector2 crocPos = WorldToFullMap(new SVector2(-7f, 0f), worldR, ownSide, _fullMapRadiusPx);
            _fullDragonDot.rectTransform.anchoredPosition = crocPos;
            _fullDragonLabel.rectTransform.anchoredPosition = crocPos + new Vector2(0f, -17f);

            Vector2 eaglePos = WorldToFullMap(new SVector2(54f, -3.8f), worldR, ownSide, _fullMapRadiusPx);
            _fullBaronDot.rectTransform.anchoredPosition = eaglePos;
            _fullBaronLabel.rectTransform.anchoredPosition = eaglePos + new Vector2(0f, -17f);
        }

        /// <summary>
        /// Converts sim ground XZ into centered UGUI radar coordinates (+X right, +Y up) rotated by <paramref name="ownSide"/>
        /// so the local team's own base (+Z for Team 0, -Z for Team 1) always sits at the bottom (-Y) and the enemy base at the top (+Y).
        /// </summary>
        public static Vector2 WorldToRadar(SVector2 simXz, float arenaRadius, int ownSide) =>
            WorldToFullMap(simXz, arenaRadius, ownSide, RadarRadiusPx);

        /// <summary>Converts sim ground XZ into centered UGUI coordinates for any circular map radius in canvas pixels.</summary>
        public static Vector2 WorldToFullMap(SVector2 simXz, float arenaRadius, int ownSide, float mapRadiusPx)
        {
            float r = Mathf.Max(1f, arenaRadius);
            float scalePx = mapRadiusPx / r;
            float side = ownSide >= 0 ? 1f : -1f;
            var p = new Vector2(simXz.X * side * scalePx, -simXz.Y * side * scalePx);
            float maxLen = Mathf.Max(4f, mapRadiusPx - 2f);
            float sq = p.sqrMagnitude;
            if (sq > maxLen * maxLen)
                p = p * (maxLen / Mathf.Sqrt(sq));
            return p;
        }

        /// <summary>Syncs radar markers (and full-map overlay when open) with 0 B heap allocation.</summary>
        public void Refresh(SimBridge sim, float cameraYawDegrees = 0f) =>
            Refresh(sim, cameraYawDegrees, _neutrals);

        /// <summary>Syncs radar + living-world neutrals + full-map overlay with 0 B heap allocation.</summary>
        public void Refresh(SimBridge sim, float cameraYawDegrees, BarangayNeutrals neutrals)
        {
            if (_disposed || !IsVisible || sim == null)
                return;
            if (Root != null && !Root.gameObject.activeInHierarchy)
            {
                if (IsFullMapOpen) SetFullMapOpen(false);
                return;
            }

            _lastSim = sim;
            _lastCameraYaw = cameraYawDegrees;
            _lastNeutrals = neutrals;

            float dt = _lastRefreshClock >= 0f
                ? Mathf.Clamp(sim.ClockSeconds - _lastRefreshClock, 0f, 0.25f)
                : sim.TickDt;
            _lastRefreshClock = sim.ClockSeconds;

            TickRevealedEnemies(dt);

            float worldR = sim.ArenaRadius > 1f
                ? sim.ArenaRadius
                : (_layout != null && _layout.ArenaRadius > 1f ? _layout.ArenaRadius : DefaultWorldRadius);
            int localId = sim.LocalPlayerId;
            int localTeam = sim.LocalPlayerTeam;
            int ownSide = neutrals != null
                ? neutrals.SideSwap.SideForTeam(localTeam)
                : (localTeam == 0 ? 1 : -1);
            int cbMode = SettingsStore.Current.ColorblindMode;
            float scalePx = RadarRadiusPx / Mathf.Max(1f, worldR);
            float fullScalePx = _fullMapRadiusPx / Mathf.Max(1f, worldR);

            SyncBackdropOrientation(ownSide);
            SyncCalloutLabels(worldR, ownSide);
            SyncBases(sim, worldR, ownSide, cbMode, scalePx, fullScalePx);
            SyncRescues(sim, worldR, ownSide, localTeam, cbMode, scalePx, fullScalePx);
            SyncMeriendas(sim, worldR, ownSide);
            SyncVendorsAndNeutrals(sim, neutrals, worldR, ownSide);
            SVector2 localPos = SyncKids(sim, neutrals, worldR, ownSide, localId, localTeam, cbMode, cameraYawDegrees);
            SyncActiveCallout(localPos, neutrals);
            DetectAudibleRipples(sim, neutrals, localId, localTeam, localPos, dt);
            SyncSoundRipples(localPos, ownSide, dt);

            if (IsFullMapOpen)
                SyncFullMapHalfHeaders(ownSide);
        }

        void SyncBackdropOrientation(int ownSide)
        {
            int normalized = ownSide >= 0 ? 1 : -1;
            if (_backdropOrientedSide == normalized) return;
            _backdropOrientedSide = normalized;
            Vector3 rot = normalized < 0 ? new Vector3(0f, 0f, 180f) : Vector3.zero;
            if (_backdropImage != null) _backdropImage.rectTransform.localEulerAngles = rot;
            if (_fullBackdropImage != null) _fullBackdropImage.rectTransform.localEulerAngles = rot;
        }

        void SyncCalloutLabels(float worldR, int ownSide)
        {
            IReadOnlyList<CalloutRegion> regions = _neutrals != null
                ? _neutrals.Layout.GetCalloutRegions()
                : MapLayout.DefaultBarangayCallouts();
            int count = Mathf.Min(regions.Count, MaxCalloutLabels);
            for (int i = 0; i < MaxCalloutLabels; i++)
            {
                bool active = i < count;
                Text mini = _miniCallouts[i];
                SetActiveIfNeeded(mini.gameObject, active);
                if (active)
                {
                    // Compact minimap uses base/boss-clear label offsets; full map uses true architectural label centers
                    SVector2 compactPos = i < CompactCalloutWorldPos.Length
                        ? CompactCalloutWorldPos[i]
                        : ToSVec2(regions[i].LabelPos);
                    mini.rectTransform.anchoredPosition = WorldToRadar(compactPos, worldR, ownSide);
                    if (IsFullMapOpen)
                    {
                        SVector2 fullPos = ToSVec2(regions[i].LabelPos);
                        _fullCallouts[i].rectTransform.anchoredPosition = WorldToFullMap(fullPos, worldR, ownSide, _fullMapRadiusPx);
                    }
                }
                if (IsFullMapOpen) SetActiveIfNeeded(_fullCallouts[i].gameObject, active);
            }
        }

        void SyncActiveCallout(SVector2 localPos, BarangayNeutrals neutrals)
        {
            if (_activeCalloutLabel == null) return;
            string callout = neutrals != null
                ? neutrals.Layout.CalloutAt(ToSimVec2(localPos))
                : DefaultCalloutAt(localPos);
            string display = string.IsNullOrEmpty(callout) ? "BARANGAY" : callout;
            if (_activeCalloutLabel.text != display)
                _activeCalloutLabel.text = display;
        }

        static string DefaultCalloutAt(SVector2 pos)
        {
            IReadOnlyList<CalloutRegion> regions = MapLayout.DefaultBarangayCallouts();
            Vec2 p = ToSimVec2(pos);
            for (int i = 0; i < regions.Count; i++)
                if (regions[i].Bounds.HasPoint(p)) return regions[i].Name;
            string nearest = "";
            double bestSq = 14.0 * 14.0;
            for (int i = 0; i < regions.Count; i++)
            {
                double dSq = p.DistanceSquaredTo(regions[i].LabelPos);
                if (dSq < bestSq)
                {
                    bestSq = dSq;
                    nearest = regions[i].Name;
                }
            }
            return nearest;
        }

        void SyncFullMapHalfHeaders(int ownSide)
        {
            string bottomHalf = MapLayout.HalfDisplayName(ownSide);
            string topHalf = MapLayout.HalfDisplayName(-ownSide);
            if (_fullTopHalfLabel.text != topHalf) _fullTopHalfLabel.text = topHalf;
            if (_fullBottomHalfLabel.text != bottomHalf) _fullBottomHalfLabel.text = bottomHalf;
        }

        void SyncBases(SimBridge sim, float worldR, int ownSide, int cbMode, float scalePx, float fullScalePx)
        {
            IReadOnlyList<BaseObjectiveSnapshot> bases = sim.Bases;
            for (int t = 0; t < 2; t++)
            {
                SVector2 center;
                SVector2 postPos;
                float radiusM;
                bool capturing = false;

                if (bases != null && t < bases.Count && bases[t].Radius > 0.1f)
                {
                    BaseObjectiveSnapshot b = bases[t];
                    center = b.Center;
                    postPos = b.PostPosition;
                    radiusM = b.Radius;
                    // Secret-capture rule (docs/GRAPHICS.md L64): never show a HUD warning halo to defenders; only the capturing team sees it!
                    capturing = sim.Phase == MatchPhaseKind.Playing
                        && b.CapturingTeam == sim.LocalPlayerTeam
                        && b.CaptureProgress01 > 0.001f;
                }
                else if (_layout != null)
                {
                    int side = t == 0 ? 1 : -1;
                    center = _layout.BaseCenter(side);
                    postPos = _layout.PostPosition(side);
                    radiusM = _layout.BaseRadius;
                }
                else
                {
                    float z = (t == 0 ? 1f : -1f) * (worldR - 14f);
                    center = new SVector2(0f, z);
                    postPos = new SVector2(0f, z + (t == 0 ? 3.5f : -3.5f));
                    radiusM = 8f;
                }

                Color col = KomiksUi.TeamColor(t, sim.LocalPlayerTeam, cbMode);
                float diamPx = Mathf.Clamp(radiusM * 2f * scalePx, 12f, 24f);

                RectTransform ringRt = _baseRings[t].rectTransform;
                ringRt.anchoredPosition = WorldToRadar(center, worldR, ownSide);
                ringRt.sizeDelta = new Vector2(diamPx, diamPx);
                _baseRings[t].color = col;

                RectTransform postRt = _basePosts[t].rectTransform;
                postRt.anchoredPosition = WorldToRadar(postPos, worldR, ownSide);
                _basePosts[t].color = Color.Lerp(col, Color.white, 0.35f);

                float pulse = SettingsStore.Current.ReduceMotion
                    ? 1f
                    : (1f + 0.14f * Mathf.Sin(sim.ClockSeconds * 7f));

                Image halo = _captureHalos[t];
                SetActiveIfNeeded(halo.gameObject, capturing);
                if (capturing)
                {
                    halo.rectTransform.anchoredPosition = ringRt.anchoredPosition;
                    halo.rectTransform.sizeDelta = new Vector2((diamPx + 7f) * pulse, (diamPx + 7f) * pulse);
                }

                if (IsFullMapOpen)
                {
                    float fullDiam = Mathf.Clamp(radiusM * 2f * fullScalePx, 24f, 48f);
                    Vector2 fullPos = WorldToFullMap(center, worldR, ownSide, _fullMapRadiusPx);
                    _fullBaseRings[t].rectTransform.anchoredPosition = fullPos;
                    _fullBaseRings[t].rectTransform.sizeDelta = new Vector2(fullDiam, fullDiam);
                    _fullBaseRings[t].color = col;

                    Image fHalo = _fullCaptureHalos[t];
                    SetActiveIfNeeded(fHalo.gameObject, capturing);
                    if (capturing)
                    {
                        fHalo.rectTransform.anchoredPosition = fullPos;
                        fHalo.rectTransform.sizeDelta = new Vector2((fullDiam + 12f) * pulse, (fullDiam + 12f) * pulse);
                    }
                }
            }
        }

        void SyncRescues(SimBridge sim, float worldR, int ownSide, int localTeam, int cbMode, float scalePx, float fullScalePx)
        {
            IReadOnlyList<RescueObjectiveSnapshot> rescues = sim.Rescues;
            for (int t = 0; t < 2; t++)
            {
                GroundRect zone;
                bool ready = true;
                int waitingCount = 0;
                if (rescues != null && t < rescues.Count && rescues[t].Zone.W > 0.1f)
                {
                    RescueObjectiveSnapshot r = rescues[t];
                    zone = r.Zone;
                    ready = r.Ready || r.CooldownLeft <= 0.001f;
                    waitingCount = r.WaitingCount;
                }
                else if (_layout != null)
                {
                    zone = _layout.RescueRect(t == 0 ? -1 : 1);
                }
                else
                {
                    float side = t == 0 ? 1f : -1f;
                    zone = new GroundRect(-side * (worldR * 0.78f), -side * (worldR * 0.375f), 5f, 8f);
                }

                Color tint;
                if (ready)
                {
                    tint = KomiksUi.TeamColor(t, localTeam, cbMode);
                    tint.a = 0.78f;
                }
                else
                {
                    tint = RescueCooldownWhite;
                }

                Image img = _rescueRects[t];
                RectTransform rt = img.rectTransform;
                rt.anchoredPosition = WorldToRadar(zone.Center, worldR, ownSide);
                rt.sizeDelta = new Vector2(
                    Mathf.Clamp(zone.W * scalePx, 7f, 18f),
                    Mathf.Clamp(zone.H * scalePx, 9f, 22f));
                img.color = tint;

                if (IsFullMapOpen)
                {
                    Image fullJail = _fullJailBoxes[t];
                    fullJail.rectTransform.anchoredPosition = WorldToFullMap(zone.Center, worldR, ownSide, _fullMapRadiusPx);
                    fullJail.rectTransform.sizeDelta = new Vector2(
                        Mathf.Clamp(zone.W * fullScalePx, 18f, 34f),
                        Mathf.Clamp(zone.H * fullScalePx, 22f, 40f));
                    fullJail.color = tint;

                    string countStr = PrebuiltCounts[Mathf.Clamp(waitingCount, 0, PrebuiltCounts.Length - 1)];
                    if (_fullJailCounts[t].text != countStr)
                        _fullJailCounts[t].text = countStr;
                }
            }
        }

        void SyncMeriendas(SimBridge sim, float worldR, int ownSide)
        {
            IReadOnlyList<MeriendaStandSnapshot> stands = sim.MeriendaStands;
            int count = stands != null ? Mathf.Min(stands.Count, MaxMeriendaDots) : 0;
            for (int i = 0; i < MaxMeriendaDots; i++)
            {
                bool on = i < count;
                Image dot = _meriendaDots[i];
                SetActiveIfNeeded(dot.gameObject, on);
                if (!on) continue;
                MeriendaStandSnapshot s = stands[i];
                dot.rectTransform.anchoredPosition = WorldToRadar(s.Position, worldR, ownSide);
                Color c = KomiksUi.MeriendaColor(s.Item);
                c.a = s.Active ? 0.90f : 0.22f;
                dot.color = c;
            }
        }

        void SyncVendorsAndNeutrals(SimBridge sim, BarangayNeutrals neutrals, float worldR, int ownSide)
        {
            bool reduceMotion = SettingsStore.Current.ReduceMotion;

            // 1. Vendors (Mang Taho & Fishball Cart) + restock rings
            if (neutrals != null)
            {
                IReadOnlyList<VendorState> vendors = neutrals.Vendors;
                int vc = Mathf.Min(vendors.Count, MaxVendorMarkers);
                for (int i = 0; i < MaxVendorMarkers; i++)
                {
                    bool on = i < vc;
                    SetActiveIfNeeded(_vendorDots[i].gameObject, on);
                    SetActiveIfNeeded(_vendorRings[i].gameObject, on);
                    if (IsFullMapOpen)
                    {
                        SetActiveIfNeeded(_fullVendorDots[i].gameObject, on);
                        SetActiveIfNeeded(_fullVendorRings[i].gameObject, on);
                        SetActiveIfNeeded(_fullVendorLabels[i].gameObject, on);
                    }
                    if (!on) continue;

                    VendorState v = vendors[i];
                    SVector2 vPos = ToSVec2(v.Position);
                    Color dotCol = v.IsReady ? VendorReadyColor : VendorEmptyColor;
                    Color ringCol = v.IsReady
                        ? VendorReadyColor
                        : Color.Lerp(VendorEmptyColor, VendorReadyColor, (float)v.RestockProgress01 * 0.65f);

                    Vector2 miniPos = WorldToRadar(vPos, worldR, ownSide);
                    _vendorDots[i].rectTransform.anchoredPosition = miniPos;
                    _vendorDots[i].color = dotCol;
                    _vendorRings[i].rectTransform.anchoredPosition = miniPos;
                    _vendorRings[i].color = ringCol;
                    float ringScale = v.IsReady ? 1f : (0.72f + 0.28f * (float)v.RestockProgress01);
                    _vendorRings[i].rectTransform.sizeDelta = new Vector2(12f * ringScale, 12f * ringScale);

                    if (IsFullMapOpen)
                    {
                        Vector2 fullPos = WorldToFullMap(vPos, worldR, ownSide, _fullMapRadiusPx);
                        _fullVendorDots[i].rectTransform.anchoredPosition = fullPos;
                        _fullVendorDots[i].color = dotCol;
                        _fullVendorRings[i].rectTransform.anchoredPosition = fullPos;
                        _fullVendorRings[i].color = ringCol;
                        _fullVendorRings[i].rectTransform.sizeDelta = new Vector2(22f * ringScale, 22f * ringScale);
                        _fullVendorLabels[i].rectTransform.anchoredPosition = fullPos + new Vector2(0f, -16f);
                        _fullVendorLabels[i].color = dotCol;
                    }
                }
            }
            else
            {
                IReadOnlyList<VendorSpot> defVendors = MapLayout.DefaultBarangayVendors();
                for (int i = 0; i < MaxVendorMarkers && i < defVendors.Count; i++)
                {
                    SVector2 vPos = ToSVec2(defVendors[i].Position);
                    Vector2 miniPos = WorldToRadar(vPos, worldR, ownSide);
                    SetActiveIfNeeded(_vendorDots[i].gameObject, true);
                    SetActiveIfNeeded(_vendorRings[i].gameObject, true);
                    _vendorDots[i].rectTransform.anchoredPosition = miniPos;
                    _vendorDots[i].color = VendorReadyColor;
                    _vendorRings[i].rectTransform.anchoredPosition = miniPos;
                    _vendorRings[i].color = VendorReadyColor;
                    if (IsFullMapOpen)
                    {
                        Vector2 fullPos = WorldToFullMap(vPos, worldR, ownSide, _fullMapRadiusPx);
                        SetActiveIfNeeded(_fullVendorDots[i].gameObject, true);
                        SetActiveIfNeeded(_fullVendorRings[i].gameObject, true);
                        SetActiveIfNeeded(_fullVendorLabels[i].gameObject, true);
                        _fullVendorDots[i].rectTransform.anchoredPosition = fullPos;
                        _fullVendorRings[i].rectTransform.anchoredPosition = fullPos;
                        _fullVendorLabels[i].rectTransform.anchoredPosition = fullPos + new Vector2(0f, -16f);
                    }
                }
            }

            // 2. Selecta Ice-Cream Bike moving dot
            bool selectaOn = neutrals != null && neutrals.Selecta.Active;
            SetActiveIfNeeded(_selectaDot.gameObject, selectaOn);
            if (IsFullMapOpen) SetActiveIfNeeded(_fullSelectaDot.gameObject, selectaOn);
            if (selectaOn)
            {
                SVector2 sPos = ToSVec2(neutrals.Selecta.Position);
                float pulse = reduceMotion ? 1f : (1f + 0.16f * Mathf.Sin(sim.ClockSeconds * 6f));
                _selectaDot.rectTransform.anchoredPosition = WorldToRadar(sPos, worldR, ownSide);
                _selectaDot.rectTransform.sizeDelta = new Vector2(7f * pulse, 7f * pulse);
                if (IsFullMapOpen)
                {
                    _fullSelectaDot.rectTransform.anchoredPosition = WorldToFullMap(sPos, worldR, ownSide, _fullMapRadiusPx);
                    _fullSelectaDot.rectTransform.sizeDelta = new Vector2(13f * pulse, 13f * pulse);
                }
            }

            // 3. Crocodile (Dragon) & Philippine Eagle (Baron) markers + countdowns
            SyncBossMarker(
                neutrals != null ? neutrals.Dragon : null,
                new SVector2(-7f, 0f),
                DragonIdleColor,
                "CROC",
                sim.ClockSeconds,
                60f,
                worldR,
                ownSide,
                reduceMotion,
                _dragonDot,
                _dragonMiniTimer,
                _fullDragonDot,
                _fullDragonLabel);

            SyncBossMarker(
                neutrals != null ? neutrals.Baron : null,
                new SVector2(54f, -3.8f),
                BaronIdleColor,
                "EAGLE",
                sim.ClockSeconds,
                180f,
                worldR,
                ownSide,
                reduceMotion,
                _baronDot,
                _baronMiniTimer,
                _fullBaronDot,
                _fullBaronLabel);
        }

        void SyncBossMarker(
            NeutralBossInstance boss,
            SVector2 fallbackPos,
            Color idleColor,
            string fallbackTag,
            float clockSeconds,
            float fallbackSpawnSec,
            float worldR,
            int ownSide,
            bool reduceMotion,
            Image miniDot,
            Text miniTimer,
            Image fullDot,
            Text fullLabel)
        {
            SVector2 pos = boss != null ? ToSVec2(boss.Position) : fallbackPos;
            NeutralBossState state = boss != null
                ? boss.State
                : (clockSeconds >= fallbackSpawnSec ? NeutralBossState.Idle : NeutralBossState.Dormant);
            float respawnLeft = boss != null
                ? (float)boss.RespawnLeft
                : Mathf.Max(0f, fallbackSpawnSec - clockSeconds);

            Color col;
            float scale = 1f;
            if (state == NeutralBossState.Angry)
            {
                col = BossAngryColor;
                scale = reduceMotion ? 1.18f : (1.15f + 0.16f * Mathf.Sin(clockSeconds * 9f));
            }
            else if (state == NeutralBossState.Idle)
            {
                col = idleColor;
            }
            else if (state == NeutralBossState.Fleeing)
            {
                col = BossDormantColor;
                scale = 0.72f;
            }
            else
            {
                col = BossDormantColor;
            }

            Vector2 miniPos = WorldToRadar(pos, worldR, ownSide);
            miniDot.rectTransform.anchoredPosition = miniPos;
            miniDot.color = col;
            miniDot.rectTransform.sizeDelta = new Vector2(9.5f * scale, 9.5f * scale);

            bool showTimer = state == NeutralBossState.Dormant && respawnLeft > 0.5f;
            SetActiveIfNeeded(miniTimer.gameObject, showTimer);
            if (showTimer)
            {
                miniTimer.rectTransform.anchoredPosition = miniPos + new Vector2(0f, -12f);
                string tStr = FormatTimer(respawnLeft);
                if (miniTimer.text != tStr) miniTimer.text = tStr;
            }

            if (IsFullMapOpen)
            {
                Vector2 fullPos = WorldToFullMap(pos, worldR, ownSide, _fullMapRadiusPx);
                fullDot.rectTransform.anchoredPosition = fullPos;
                fullDot.color = col;
                fullDot.rectTransform.sizeDelta = new Vector2(18f * scale, 18f * scale);
                fullLabel.rectTransform.anchoredPosition = fullPos + new Vector2(0f, -17f);
                string labelText = showTimer ? FormatTimer(respawnLeft) : fallbackTag;
                if (fullLabel.text != labelText) fullLabel.text = labelText;
                fullLabel.color = state == NeutralBossState.Dormant ? TacticalPaperMuted : col;
            }
        }

        SVector2 SyncKids(
            SimBridge sim,
            BarangayNeutrals neutrals,
            float worldR,
            int ownSide,
            int localId,
            int localTeam,
            int cbMode,
            float cameraYawDeg)
        {
            IReadOnlyList<KidSnapshot> kids = sim.Kids;
            int n = kids.Count;
            SVector2 localPos = SVector2.Zero;
            for (int i = 0; i < n; i++)
            {
                if (kids[i].Id == localId)
                {
                    localPos = kids[i].Position;
                    break;
                }
            }

            int dotCursor = 0;
            bool hasSelf = false;

            for (int i = 0; i < n; i++)
            {
                KidSnapshot kid = kids[i];
                if (kid.Id == localId)
                {
                    hasSelf = true;
                    Vector2 selfPos = WorldToRadar(kid.Position, worldR, ownSide);
                    _selfDotRect.anchoredPosition = selfPos;
                    Color selfCol = KomiksUi.SelfHealthColor(cbMode);
                    selfCol.a = kid.Tagged ? 0.48f : 1f;
                    _selfDotImage.color = selfCol;

                    float yawRad = cameraYawDeg * Mathf.Deg2Rad;
                    float side = ownSide >= 0 ? 1f : -1f;
                    var radarFwd = new Vector2(-Mathf.Sin(yawRad) * side, Mathf.Cos(yawRad) * side);
                    if (radarFwd.sqrMagnitude < 0.0001f) radarFwd = Vector2.up;
                    else radarFwd.Normalize();

                    float centerAngleDeg = Mathf.Atan2(-radarFwd.x, radarFwd.y) * Mathf.Rad2Deg;
                    float halfDeg = FovHalfAngleRad * Mathf.Rad2Deg;

                    _fovLeftRect.anchoredPosition = selfPos;
                    _fovLeftRect.localEulerAngles = new Vector3(0f, 0f, centerAngleDeg + halfDeg);
                    _fovRightRect.anchoredPosition = selfPos;
                    _fovRightRect.localEulerAngles = new Vector3(0f, 0f, centerAngleDeg - halfDeg);

                    if (IsFullMapOpen)
                    {
                        Vector2 fullSelf = WorldToFullMap(kid.Position, worldR, ownSide, _fullMapRadiusPx);
                        _fullSelfDotRect.anchoredPosition = fullSelf;
                        _fullSelfDotImage.color = selfCol;
                        _fullFovLeftRect.anchoredPosition = fullSelf;
                        _fullFovLeftRect.localEulerAngles = new Vector3(0f, 0f, centerAngleDeg + halfDeg);
                        _fullFovRightRect.anchoredPosition = fullSelf;
                        _fullFovRightRect.localEulerAngles = new Vector3(0f, 0f, centerAngleDeg - halfDeg);
                    }
                    continue;
                }

                bool isAlly = kid.Team == localTeam;
                bool revealed = IsEnemyRevealed(kid.Id);
                if (!isAlly)
                {
                    if (!ShowEnemies && !revealed) continue;
                    // Tall-cover stealth: enemies hidden beyond 6 m never leak onto minimap or full map unless revealed
                    if (neutrals != null && !revealed && !kid.Tagged)
                    {
                        bool breaking = kid.NearEnemyBase || kid.InRescueZone || !string.IsNullOrEmpty(kid.CastingWeaponId);
                        if (neutrals.IsHiddenInTallCover(ToSimVec2(kid.Position), ToSimVec2(localPos), revealed: false, sameTeam: false, breakingAction: breaking))
                            continue;
                    }
                }
                if (dotCursor >= MaxKidDots) continue;

                Color c = isAlly ? KomiksUi.TeamAllyColor(cbMode) : KomiksUi.TeamEnemyColor(cbMode);
                c.a = kid.Tagged ? 0.42f : 0.96f;

                Image dot = _kidDots[dotCursor];
                SetActiveIfNeeded(dot.gameObject, true);
                dot.rectTransform.anchoredPosition = WorldToRadar(kid.Position, worldR, ownSide);
                dot.color = c;

                if (IsFullMapOpen)
                {
                    Image fullDot = _fullKidDots[dotCursor];
                    SetActiveIfNeeded(fullDot.gameObject, true);
                    fullDot.rectTransform.anchoredPosition = WorldToFullMap(kid.Position, worldR, ownSide, _fullMapRadiusPx);
                    fullDot.color = c;
                }
                dotCursor++;
            }

            SetActiveIfNeeded(_selfDotImage.gameObject, hasSelf);
            SetActiveIfNeeded(_fovLeftRect.gameObject, hasSelf);
            SetActiveIfNeeded(_fovRightRect.gameObject, hasSelf);
            if (IsFullMapOpen)
            {
                SetActiveIfNeeded(_fullSelfDotImage.gameObject, hasSelf);
                SetActiveIfNeeded(_fullFovLeftRect.gameObject, hasSelf);
                SetActiveIfNeeded(_fullFovRightRect.gameObject, hasSelf);
            }

            for (int i = dotCursor; i < MaxKidDots; i++)
            {
                SetActiveIfNeeded(_kidDots[i].gameObject, false);
                if (IsFullMapOpen) SetActiveIfNeeded(_fullKidDots[i].gameObject, false);
            }
            return localPos;
        }

        void DetectAudibleRipples(SimBridge sim, BarangayNeutrals neutrals, int localId, int localTeam, SVector2 localPos, float dt)
        {
            IReadOnlyList<WorldEffectSnapshot> effects = sim.WorldEffects;
            int newCount = 0;
            if (effects != null)
            {
                for (int i = 0; i < effects.Count && newCount < _lastEffectIds.Length; i++)
                {
                    WorldEffectSnapshot fx = effects[i];
                    if (fx.Team == localTeam) continue;
                    bool seenBefore = false;
                    for (int k = 0; k < _lastEffectCount; k++)
                    {
                        if (_lastEffectIds[k] == fx.Id) { seenBefore = true; break; }
                    }
                    if (!seenBefore)
                    {
                        float distSq = (fx.Position - localPos).LengthSquared();
                        if (distSq <= RiverHearingRangeM * RiverHearingRangeM && distSq > 2.25f)
                            PushSoundRipple(fx.Position, 1.15f, isSkill: true);
                    }
                    _lastEffectIds[newCount++] = fx.Id;
                }
            }
            _lastEffectCount = newCount;

            _footstepRippleCooldown = Mathf.Max(0f, _footstepRippleCooldown - dt);
            if (_footstepRippleCooldown > 0f) return;

            IReadOnlyList<KidSnapshot> kids = sim.Kids;
            for (int i = 0; i < kids.Count; i++)
            {
                KidSnapshot kid = kids[i];
                if (kid.Id == localId || kid.Team == localTeam || kid.Tagged || kid.Crouching) continue;
                bool inRiver = neutrals != null
                    ? neutrals.IsLoudFootsteps(ToSimVec2(kid.Position))
                    : IsInDefaultShallowRiver(kid.Position);
                bool loud = inRiver ? kid.Speed01 > 0.20f : (kid.Sprinting || kid.Speed01 > 0.72f);
                if (!loud) continue;

                float range = inRiver ? RiverHearingRangeM : StreetHearingRangeM;
                float distSq = (kid.Position - localPos).LengthSquared();
                if (distSq <= range * range && distSq > 4f)
                {
                    PushSoundRipple(kid.Position, inRiver ? 1.1f : 0.85f, isSkill: false);
                    _footstepRippleCooldown = 0.42f;
                    break;
                }
            }
        }

        void SyncSoundRipples(SVector2 localPos, int ownSide, float dt)
        {
            bool reduceMotion = SettingsStore.Current.ReduceMotion;
            float side = ownSide >= 0 ? 1f : -1f;
            float rimR = RadarRadiusPx - 2.5f;

            for (int i = 0; i < MaxSoundRipples; i++)
            {
                if (_soundRipples[i].TimeLeft <= 0f)
                {
                    SetActiveIfNeeded(_soundRipples[i].Graphic.gameObject, false);
                    continue;
                }

                _soundRipples[i].TimeLeft = Mathf.Max(0f, _soundRipples[i].TimeLeft - dt);
                float t01 = 1f - (_soundRipples[i].TimeLeft / RippleDuration);
                if (_soundRipples[i].TimeLeft <= 0.001f)
                {
                    SetActiveIfNeeded(_soundRipples[i].Graphic.gameObject, false);
                    continue;
                }

                SVector2 delta = _soundRipples[i].SourceXz - localPos;
                var dir = new Vector2(delta.X * side, -delta.Y * side);
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
                else dir.Normalize();

                SetActiveIfNeeded(_soundRipples[i].Graphic.gameObject, true);
                _soundRipples[i].Rect.anchoredPosition = dir * rimR;
                float size = reduceMotion
                    ? (11f * _soundRipples[i].Intensity)
                    : (Mathf.Lerp(9f, 20f, t01) * _soundRipples[i].Intensity);
                _soundRipples[i].Rect.sizeDelta = new Vector2(size, size);

                Color col = _soundRipples[i].IsSkill ? RippleSkillColor : RippleFootstepColor;
                col.a *= Mathf.Clamp01(1f - t01 * t01);
                _soundRipples[i].Graphic.color = col;
            }
        }

        void TickRevealedEnemies(float dt)
        {
            for (int i = 0; i < MaxRevealedSlots; i++)
            {
                if (_revealedEnemies[i].TimeLeft > 0f)
                    _revealedEnemies[i].TimeLeft = Mathf.Max(0f, _revealedEnemies[i].TimeLeft - dt);
            }
        }

        bool IsEnemyRevealed(int playerId)
        {
            for (int i = 0; i < MaxRevealedSlots; i++)
                if (_revealedEnemies[i].PlayerId == playerId && _revealedEnemies[i].TimeLeft > 0f) return true;
            return false;
        }

        static bool IsInDefaultShallowRiver(SVector2 xz)
        {
            if (Mathf.Abs(xz.Y - MapLayoutData.CanalZ(xz.X)) > 3.75f) return false;
            if (Mathf.Abs(xz.X) <= 4.8f || Mathf.Abs(Mathf.Abs(xz.X) - 36.5f) <= 4.8f) return false;
            return true;
        }

        static void SetActiveIfNeeded(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        static SVector2 ToSVec2(Vec2 v) => new SVector2(v.X, v.Y);
        static Vec2 ToSimVec2(SVector2 v) => new Vec2(v.X, v.Y);

        static string FormatTimer(float seconds)
        {
            int idx = Mathf.Clamp(Mathf.CeilToInt(seconds), 0, MaxTimerSeconds);
            return PrebuiltTimers[idx];
        }

        static string[] BuildTimerTable(int maxSeconds)
        {
            var table = new string[maxSeconds + 1];
            for (int i = 0; i <= maxSeconds; i++)
                table[i] = $"{i / 60}:{(i % 60):00}";
            return table;
        }

        void RebuildBackdropTexture()
        {
            if (_disposed) return;
            if (_backdropTex == null)
            {
                _backdropTex = new Texture2D(BackdropTexSize, BackdropTexSize, TextureFormat.RGBA32, false)
                {
                    name = "BarangayMinimapBackdrop",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
            }

            PaintBarangayMapTexture(_backdropTex, BackdropTexSize, (RadarRadiusPx / PanelSize) * BackdropTexSize, highDetail: false);

            if (_backdropSprite == null)
            {
                _backdropSprite = Sprite.Create(
                    _backdropTex,
                    new Rect(0f, 0f, BackdropTexSize, BackdropTexSize),
                    new Vector2(0.5f, 0.5f),
                    100f);
                _backdropSprite.name = "BarangayMinimapSprite";
            }
            if (_backdropImage != null)
                _backdropImage.sprite = _backdropSprite;
        }

        void RebuildFullMapTexture()
        {
            if (_disposed) return;
            if (_fullMapTex == null)
            {
                _fullMapTex = new Texture2D(FullMapTexSize, FullMapTexSize, TextureFormat.RGBA32, false)
                {
                    name = "BarangayFullMapBackdrop",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
            }

            PaintBarangayMapTexture(_fullMapTex, FullMapTexSize, FullMapTexSize * 0.47f, highDetail: true);

            if (_fullMapSprite == null)
            {
                _fullMapSprite = Sprite.Create(
                    _fullMapTex,
                    new Rect(0f, 0f, FullMapTexSize, FullMapTexSize),
                    new Vector2(0.5f, 0.5f),
                    100f);
                _fullMapSprite.name = "BarangayFullMapSprite";
            }
            if (_fullBackdropImage != null)
                _fullBackdropImage.sprite = _fullMapSprite;
        }

        void PaintBarangayMapTexture(Texture2D tex, int texSize, float rPx, bool highDetail)
        {
            var pixels = new Color32[texSize * texSize];
            float half = texSize * 0.5f;
            float worldR = (_layout != null && _layout.ArenaRadius > 1f) ? _layout.ArenaRadius : DefaultWorldRadius;
            float mPerPx = worldR / Mathf.Max(1f, rPx);
            IReadOnlyList<Rect2> tallZones = MapLayout.DefaultBarangayTallCoverZones();
            IReadOnlyList<ClimbSpot> climbs = MapLayout.DefaultBarangayClimbSpots();

            Color uptownTint = new Color(0.11f, 0.14f, 0.18f, highDetail ? 0.96f : 0.72f);
            Color riversideTint = new Color(0.15f, 0.13f, 0.11f, highDetail ? 0.96f : 0.72f);
            Color riverColor = new Color(0.18f, 0.58f, 0.62f, 0.90f);
            Color bridgeColor = new Color(0.82f, 0.78f, 0.70f, 0.92f);
            Color tallCoverColor = new Color(0.24f, 0.50f, 0.30f, 0.88f);
            Color climbColor = new Color(0.95f, 0.78f, 0.34f, 0.96f);

            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float edgeAlpha = Mathf.Clamp01(rPx + 0.6f - dist);
                    if (edgeAlpha <= 0.001f)
                    {
                        pixels[y * texSize + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // Sim ground coords for Team 0 view (+X right, -Z up)
                    float wx = dx * mPerPx;
                    float wz = -dy * mPerPx;

                    float halfBlend = Mathf.Clamp01((wz + 12f) / 24f);
                    Color col = Color.Lerp(uptownTint, riversideTint, halfBlend);

                    bool onPath = Mathf.Abs(wx) < 3.8f || Mathf.Abs(Mathf.Abs(wx) - 35.5f) < 3.6f || Mathf.Abs(Mathf.Abs(wz) - 27f) < 3.0f;
                    if (onPath)
                        col = Color.Lerp(col, TacticalPaper, highDetail ? 0.12f : 0.09f);

                    float canalZ = MapLayoutData.CanalZ(wx);
                    if (Mathf.Abs(wz - canalZ) < 3.4f)
                    {
                        bool onBridge = Mathf.Abs(wx) < 4.8f || Mathf.Abs(Mathf.Abs(wx) - 36.5f) < 4.8f;
                        col = onBridge
                            ? Color.Lerp(col, bridgeColor, highDetail ? 0.55f : 0.45f)
                            : Color.Lerp(col, riverColor, highDetail ? 0.52f : 0.40f);
                    }

                    var simPt = new Vec2(wx, wz);
                    for (int t = 0; t < tallZones.Count; t++)
                    {
                        if (tallZones[t].HasPoint(simPt))
                        {
                            bool hatch = ((x + y) & 3) < 2;
                            col = Color.Lerp(col, tallCoverColor, hatch ? 0.50f : 0.25f);
                            break;
                        }
                    }

                    if (_layout != null && _layout.Obstacles != null)
                    {
                        int parcels = _layout.ParcelCount;
                        var pt = new SVector2(wx, wz);
                        for (int p = 0; p < parcels; p++)
                        {
                            if (_layout.ParcelRect(p).Contains(pt))
                            {
                                col = Color.Lerp(col, KomiksUi.SandBorder, highDetail ? 0.34f : 0.24f);
                                break;
                            }
                        }
                    }

                    if (highDetail)
                    {
                        for (int c = 0; c < climbs.Count; c++)
                        {
                            float cdx = wx - climbs[c].Position.X;
                            float cdz = wz - climbs[c].Position.Y;
                            if (cdx * cdx + cdz * cdz <= 4.5f)
                            {
                                col = Color.Lerp(col, climbColor, 0.65f);
                                break;
                            }
                        }
                    }

                    float rimBlend = Mathf.Clamp01(1f - Mathf.Abs(dist - (rPx - 1.2f)) * 0.85f);
                    col = Color.Lerp(col, new Color(0.95f, 0.94f, 0.90f, 0.58f), rimBlend * 0.80f);
                    col.a *= edgeAlpha;
                    pixels[y * texSize + x] = col;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _lastSim = null;
            _lastNeutrals = null;

            if (_backdropImage != null)
                _backdropImage.sprite = null;
            if (_fullBackdropImage != null)
                _fullBackdropImage.sprite = null;

            if (_backdropSprite != null)
            {
                KalyeahResources.Kill(_backdropSprite);
                _backdropSprite = null;
            }
            if (_backdropTex != null)
            {
                KalyeahResources.Kill(_backdropTex);
                _backdropTex = null;
            }
            if (_fullMapSprite != null)
            {
                KalyeahResources.Kill(_fullMapSprite);
                _fullMapSprite = null;
            }
            if (_fullMapTex != null)
            {
                KalyeahResources.Kill(_fullMapTex);
                _fullMapTex = null;
            }
            if (FullMapRoot != null)
            {
                KalyeahResources.Kill(FullMapRoot.gameObject);
            }
        }
    }
}
