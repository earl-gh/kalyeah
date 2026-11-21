using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SVector2 = System.Numerics.Vector2;
using SVector3 = System.Numerics.Vector3;

namespace Kalyeah.Game
{
    /// <summary>
    /// Preallocated 10-kid world-to-screen overhead HUD plates (<c>0 B</c> per-frame GC):
    /// rounded-rect plate, team number badge, <c>"You"</c> label on local kid, enemy HP strictly hidden during play,
    /// off-screen viewport culling, <c>30 m</c> enemy / <c>55 m</c> ally distance + building OBB line-of-sight culling
    /// (unless damaged within <c>1.5 s</c>), sacred <c>(0.50, 0.54)</c> <c>72 dp</c> crosshair exclusion push + fade,
    /// <c>+24 px</c> <c>"You"</c> top protection, distance scale <c>[0.65..1.05]</c>, and vertical overlap de-stacking.
    /// </summary>
    public sealed class OverheadBarsView
    {
        public const int MaxPlates = 12;
        public const float BarWidth = 96f;
        public const float BarHeight = 16f;
        public const float BorderPx = 2f;
        public const float StaminaHeight = 7f;
        public const float BadgeSize = 24f;
        public const float BadgeOverlap = 6f;
        public const float DefaultBarScale = 0.78f;
        public const float DefaultShoulderNameReach = 40f;
        public const float MinScale = 0.65f;
        public const float MaxScale = 1.05f;
        public const float NearScaleMeters = 4f;
        public const float FarScaleMeters = DefaultShoulderNameReach;
        public const float MaxEnemyDistanceMeters = 30f;
        public const float MaxAllyDistanceMeters = 55f;
        public const float RecentlyDamagedSeconds = 1.5f;
        public const float CrosshairExclusionDp = 72f;
        public const float CrosshairNearAlpha = 0.42f;
        public const float LocalYouTopPadPx = 24f;
        public const float MinVerticalSeparationPx = 18f;
        public const float BaseKidHeadMeters = 1.85f;
        public const float BaseKidChestMeters = 1.15f;
        public const float DefaultBuildingHeightMeters = 4.8f;

        public static readonly Vector2 CrosshairViewport = new Vector2(0.50f, 0.54f);

        const int DamageTrackSlots = 32;

        static readonly string[] NumberStrings =
        {
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12",
        };

        static readonly Color WaterBarColor = new Color(0.16f, 0.78f, 1.00f, 0.98f);
        static readonly Color ShieldBarColor = new Color(0.54f, 0.92f, 1.00f, 0.92f);
        static readonly Color RevealEyeColor = new Color(1.00f, 0.80f, 0.24f, 0.98f);
        static Sprite s_eyeSprite;

        /// <summary>Cached crisp almond-eye icon sprite shared by revealed overhead markers and local REVEALED badge.</summary>
        internal static Sprite EyeIconSprite()
        {
            if (s_eyeSprite != null) return s_eyeSprite;
            const int w = 48;
            const int h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "KalyeahRevealEye",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[w * h];
            float cx = w * 0.5f;
            float cy = h * 0.5f;
            float rx = w * 0.44f;
            float ry = h * 0.36f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - cx) / rx;
                    float dy = (y + 0.5f - cy) / ry;
                    float ax = Mathf.Abs(dx);
                    float lid = Mathf.Max(0f, 1f - ax * ax);
                    float edgeDist = Mathf.Abs(Mathf.Abs(dy) - lid);
                    float outline = ax <= 1.02f ? Mathf.Clamp01(1f - edgeDist * 3.8f) : 0f;

                    float irisDist = Mathf.Sqrt(((x + 0.5f - cx) * (x + 0.5f - cx)) + ((y + 0.5f - cy) * (y + 0.5f - cy)));
                    float insideEye = ax < 0.96f && Mathf.Abs(dy) <= lid ? 1f : 0f;
                    float irisRing = insideEye * Mathf.Clamp01(1f - Mathf.Abs(irisDist - 6.8f) * 0.65f);
                    float pupil = insideEye * Mathf.Clamp01((3.6f - irisDist) * 0.85f);
                    float a = Mathf.Clamp01(Mathf.Max(outline, Mathf.Max(irisRing, pupil)));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_eyeSprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
            s_eyeSprite.name = "KalyeahRevealEye";
            return s_eyeSprite;
        }

        sealed class PlateSlot
        {
            public GameObject RootObj;
            public RectTransform Root;
            public CanvasGroup Group;

            public GameObject HpPlateObj;
            public RectTransform HpPlateRect;
            public RectTransform HpFillRect;
            public Image HpFillImage;
            public GameObject ShieldFillObj;
            public RectTransform ShieldFillRect;

            public GameObject StaminaPlateObj;
            public RectTransform StaminaPlateRect;
            public RectTransform StaminaFillRect;
            public Image StaminaFillImage;

            public Image BadgeFill;
            public Text BadgeText;

            public GameObject RevealEyeObj;
            public Image RevealEyeIcon;

            public GameObject YouObj;
            public GameObject StatusObj;
            public Text StatusText;

            public int BoundPlayerId;
            public int LastBadgeNumber = -1;
            public float LastAlpha = 1f;
            public bool HpVisible;
            public bool StaminaVisible;
            public bool Active;
            public string LastStatus = string.Empty;
        }

        public RectTransform Root { get; }
        public float KidViewScale { get; set; } = 0.82f;
        public float TopReservedPx { get; set; } = 86f;
        public int ActivePlateCount { get; private set; }
        public bool IsVisible => _visible;

        /// <summary>data/rules/camera_rules.tres <c>bar_scale</c> (0.78): scales HP and stamina bar dimensions while keeping 2 px borders and badges crisp.</summary>
        public float BarScale
        {
            get => _barScale;
            set
            {
                float clamped = Mathf.Clamp(value, 0.4f, 2f);
                if (Mathf.Abs(_barScale - clamped) > 0.0005f)
                {
                    _barScale = clamped;
                    ApplyBarScaleToPlates();
                }
            }
        }

        /// <summary>data/rules/camera_rules.tres <c>shoulder_name_reach</c> (40 m): distance scale horizon and non-local overhead label reach.</summary>
        public float ShoulderNameReach
        {
            get => _shoulderNameReach;
            set => _shoulderNameReach = Mathf.Max(0f, value);
        }

        float ScaledBarWidth => Mathf.Round(BarWidth * _barScale);
        float ScaledBarHeight => Mathf.Max(10f, Mathf.Round(BarHeight * _barScale));
        float ScaledStaminaHeight => Mathf.Max(5f, Mathf.Round(StaminaHeight * _barScale));

        readonly Canvas _canvas;
        readonly PlateSlot[] _plates = new PlateSlot[MaxPlates];
        readonly int[] _candidateIndices = new int[MaxPlates];
        readonly float[] _candidateDepths = new float[MaxPlates];
        readonly Rect[] _placedRects = new Rect[MaxPlates];

        readonly int[] _trackedPlayerIds = new int[DamageTrackSlots];
        readonly int[] _trackedPrevHp = new int[DamageTrackSlots];
        readonly float[] _lastDamagedClock = new float[DamageTrackSlots];
        int _trackedCount;
        float _prevClockSeconds = -1f;
        float _barScale = DefaultBarScale;
        float _shoulderNameReach = DefaultShoulderNameReach;
        bool _visible = true;

        public OverheadBarsView(Transform parent, Canvas canvas = null)
            : this(parent, canvas, null, null)
        {
        }

        public OverheadBarsView(Transform parent, Canvas canvas, DataSource data, CameraRulesData cameraRules = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _canvas = canvas != null ? canvas : parent.GetComponentInParent<Canvas>();

            Configure(data ?? KalyeahResources.Data(), cameraRules);

            Root = Ui.Fill("OverheadBars", parent);
            for (int i = 0; i < MaxPlates; i++)
                _plates[i] = BuildPlate(i);
        }

        /// <summary>Loads <c>bar_scale</c>, <c>shoulder_name_reach</c>, and <c>kid_view_scale</c> from <c>rules__camera_rules</c>.</summary>
        public void Configure(DataSource data, CameraRulesData cameraRules = null)
        {
            if (data != null)
            {
                try
                {
                    Dictionary<string, object> p = data.Props("rules__camera_rules");
                    if (p != null)
                    {
                        if (p.TryGetValue("bar_scale", out object bs) && bs != null)
                            _barScale = Mathf.Clamp(Convert.ToSingle(bs, System.Globalization.CultureInfo.InvariantCulture), 0.4f, 2f);
                        if (p.TryGetValue("shoulder_name_reach", out object snr) && snr != null)
                            _shoulderNameReach = Mathf.Max(0f, Convert.ToSingle(snr, System.Globalization.CultureInfo.InvariantCulture));
                        if (p.TryGetValue("kid_view_scale", out object kvs) && kvs != null)
                            KidViewScale = Mathf.Clamp(Convert.ToSingle(kvs, System.Globalization.CultureInfo.InvariantCulture), 0.35f, 2f);
                    }
                }
                catch
                {
                    // Keep defaults when constructed in isolated unit tests without Resources/Data.
                }
            }

            if (cameraRules != null && cameraRules.KidViewScale > 0.01f)
                KidViewScale = cameraRules.KidViewScale;

            ApplyBarScaleToPlates();
        }

        void ApplyBarScaleToPlates()
        {
            float barW = ScaledBarWidth;
            float barH = ScaledBarHeight;
            float stamH = ScaledStaminaHeight;
            for (int i = 0; i < MaxPlates; i++)
            {
                PlateSlot slot = _plates[i];
                if (slot == null) continue;
                slot.HpPlateRect.sizeDelta = new Vector2(barW, barH);
                slot.StaminaPlateRect.anchoredPosition = new Vector2(BadgeSize - BadgeOverlap, -(barH * 0.5f) + BorderPx);
                slot.StaminaPlateRect.sizeDelta = new Vector2(barW, stamH);
                slot.Root.sizeDelta = slot.HpVisible
                    ? new Vector2(barW + BadgeSize - BadgeOverlap, barH)
                    : new Vector2(BadgeSize, barH);
            }
        }

        PlateSlot BuildPlate(int index)
        {
            var slot = new PlateSlot();
            float barW = ScaledBarWidth;
            float barH = ScaledBarHeight;
            float stamH = ScaledStaminaHeight;
            float totalWidth = barW + BadgeSize - BadgeOverlap;
            RectTransform root = Ui.Panel(
                $"OverheadPlate_{index}",
                Root,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(totalWidth, barH));
            slot.Root = root;
            slot.RootObj = root.gameObject;

            var group = slot.RootObj.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 1f;
            slot.Group = group;

            int barR = KomiksUi.BarRadius(barH);

            // HP Plate (shown ONLY for local player and allies; strictly hidden on enemies)
            Image hpPlate = KomiksUi.MakeInkPanel("HpPlate", root, KomiksUi.EmptyBar, KomiksUi.SoftBorder, barR, 2);
            RectTransform hpRect = hpPlate.rectTransform;
            hpRect.anchorMin = hpRect.anchorMax = new Vector2(0f, 0.5f);
            hpRect.pivot = new Vector2(0f, 0.5f);
            hpRect.anchoredPosition = new Vector2(BadgeSize - BadgeOverlap, 0f);
            hpRect.sizeDelta = new Vector2(barW, barH);
            slot.HpPlateObj = hpPlate.gameObject;
            slot.HpPlateRect = hpRect;

            RectTransform hpInner = Ui.Panel("HpInner", hpRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-BorderPx * 2f, -BorderPx * 2f));
            Image hpFill = KomiksUi.MakeSlicedFill("HpFill", hpInner, KomiksUi.SelfHealthColor(0), Mathf.Max(2, barR - 1));
            RectTransform fillRect = hpFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.sizeDelta = Vector2.zero;
            slot.HpFillImage = hpFill;
            slot.HpFillRect = fillRect;

            Image shieldFill = KomiksUi.MakeSlicedFill("ShieldFill", hpInner, ShieldBarColor, Mathf.Max(2, barR - 1));
            RectTransform shRect = shieldFill.rectTransform;
            shRect.anchorMin = new Vector2(1f, 0f);
            shRect.anchorMax = Vector2.one;
            shRect.pivot = new Vector2(1f, 0.5f);
            shRect.sizeDelta = Vector2.zero;
            slot.ShieldFillRect = shRect;
            slot.ShieldFillObj = shieldFill.gameObject;
            slot.ShieldFillObj.SetActive(false);

            // Gapless Stamina / Overclock strip directly sharing HpPlate's bottom border (local kid only)
            Image stamPlate = KomiksUi.MakeInkPanel("StaminaPlate", root, KomiksUi.EmptyBar, KomiksUi.SoftBorder, 3, 2);
            RectTransform stamRect = stamPlate.rectTransform;
            stamRect.anchorMin = stamRect.anchorMax = new Vector2(0f, 0.5f);
            stamRect.pivot = new Vector2(0f, 1f);
            stamRect.anchoredPosition = new Vector2(BadgeSize - BadgeOverlap, -(barH * 0.5f) + BorderPx);
            stamRect.sizeDelta = new Vector2(barW, stamH);
            slot.StaminaPlateObj = stamPlate.gameObject;
            slot.StaminaPlateRect = stamRect;

            RectTransform stamInner = Ui.Panel("StamInner", stamRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-BorderPx * 2f, -BorderPx * 2f));
            Image stamFill = KomiksUi.MakeSlicedFill("StamFill", stamInner, Color.white, 2);
            RectTransform stamFillRect = stamFill.rectTransform;
            stamFillRect.anchorMin = Vector2.zero;
            stamFillRect.anchorMax = Vector2.one;
            stamFillRect.pivot = new Vector2(0f, 0.5f);
            stamFillRect.sizeDelta = Vector2.zero;
            slot.StaminaFillImage = stamFill;
            slot.StaminaFillRect = stamFillRect;

            // Team Number Badge (always shown on visible allies and visible in-LOS enemies)
            Image badgeBorder = KomiksUi.MakeInkPanel("BadgeBorder", root, KomiksUi.SlateEspresso, KomiksUi.SoftBorder, KomiksUi.RadiusS, 2);
            RectTransform badgeRect = badgeBorder.rectTransform;
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0f, 0.5f);
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.anchoredPosition = Vector2.zero;
            badgeRect.sizeDelta = new Vector2(BadgeSize, BadgeSize);

            Image badgeFill = KomiksUi.MakeSlicedFill("BadgeFill", badgeRect, KomiksUi.TeamOwnColor(0), KomiksUi.RadiusS - 2);
            RectTransform bfRect = badgeFill.rectTransform;
            bfRect.anchorMin = Vector2.zero;
            bfRect.anchorMax = Vector2.one;
            bfRect.sizeDelta = new Vector2(-BorderPx * 2f, -BorderPx * 2f);
            slot.BadgeFill = badgeFill;

            Text badgeText = Ui.MakeText("Number", badgeRect, "1", 16, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            badgeText.rectTransform.anchorMin = Vector2.zero;
            badgeText.rectTransform.anchorMax = Vector2.one;
            badgeText.rectTransform.sizeDelta = Vector2.zero;
            slot.BadgeText = badgeText;

            // Revealed "EYE" badge above enemy number badge when outlined through walls/tall cover
            RectTransform eyePill = Ui.Panel("RevealEye", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(26f, 18f));
            Image eyeBg = KomiksUi.MakeInkPanel("EyeBg", eyePill, KomiksUi.InkOutline, RevealEyeColor, 6, 1, raycast: false);
            eyeBg.rectTransform.anchorMin = Vector2.zero;
            eyeBg.rectTransform.anchorMax = Vector2.one;
            eyeBg.rectTransform.sizeDelta = Vector2.zero;
            Image eyeIcon = Ui.MakeImage("EyeGlyph", eyePill, RevealEyeColor, round: false, raycast: false);
            eyeIcon.sprite = EyeIconSprite();
            eyeIcon.preserveAspect = true;
            eyeIcon.rectTransform.anchorMin = new Vector2(0.12f, 0.14f);
            eyeIcon.rectTransform.anchorMax = new Vector2(0.88f, 0.86f);
            eyeIcon.rectTransform.sizeDelta = Vector2.zero;
            slot.RevealEyeObj = eyePill.gameObject;
            slot.RevealEyeIcon = eyeIcon;
            slot.RevealEyeObj.SetActive(false);

            // "You" label at top-left of local player's bar (never player names)
            Text youText = Ui.MakeText("YouLabel", root, "You", 18, KomiksUi.SunlitCream, TextAnchor.LowerLeft);
            RectTransform youRect = youText.rectTransform;
            youRect.anchorMin = youRect.anchorMax = new Vector2(0f, 1f);
            youRect.pivot = new Vector2(0f, 0f);
            youRect.anchoredPosition = new Vector2(BadgeSize + 2f, 1f);
            youRect.sizeDelta = new Vector2(56f, 20f);
            slot.YouObj = youText.gameObject;

            // Single italic priority status word above the plate
            Text statusText = Ui.MakeText("StatusWord", root, string.Empty, 18, KomiksUi.SunlitCream, TextAnchor.LowerCenter);
            statusText.fontStyle = FontStyle.Italic;
            RectTransform statusRect = statusText.rectTransform;
            statusRect.anchorMin = statusRect.anchorMax = new Vector2(0.5f, 1f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.anchoredPosition = new Vector2(6f, 3f);
            statusRect.sizeDelta = new Vector2(140f, 22f);
            slot.StatusText = statusText;
            slot.StatusObj = statusText.gameObject;

            slot.RootObj.SetActive(false);
            return slot;
        }

        /// <summary>Maps camera-to-head distance in meters onto the GDD overhead scale range [0.65..1.05].</summary>
        public static float ComputeDistanceScale(float distanceMeters)
            => ComputeDistanceScale(distanceMeters, DefaultShoulderNameReach);

        /// <summary>Maps camera-to-head distance in meters onto [0.65..1.05] using <c>shoulder_name_reach</c> as the far horizon.</summary>
        public static float ComputeDistanceScale(float distanceMeters, float shoulderNameReach)
        {
            float farMeters = shoulderNameReach > NearScaleMeters + 1f ? shoulderNameReach : DefaultShoulderNameReach;
            float t = Mathf.InverseLerp(NearScaleMeters, farMeters, Mathf.Max(0f, distanceMeters));
            return Mathf.Clamp(Mathf.Lerp(MaxScale, MinScale, t), MinScale, MaxScale);
        }

        /// <summary>
        /// Resolves at most ONE priority italic status word:
        /// Stunned / Rooted / Tripped / Polymorphed (and hard CC) &gt; Slipping / Slowed &gt; Langit! &gt; Exhausted.
        /// </summary>
        public static string ResolvePriorityStatus(in KidSnapshot kid)
        {
            if (kid.StatusKind != StatusKind.None && (kid.StatusLeft > 0f || kid.StatusKind == StatusKind.Tripped))
            {
                switch (kid.StatusKind)
                {
                    case StatusKind.Stun: return "Stunned";
                    case StatusKind.Root: return "Rooted";
                    case StatusKind.Tripped: return "Tripped";
                    case StatusKind.Polymorph: return "Polymorphed";
                    case StatusKind.Knockout: return "Knocked Out";
                    case StatusKind.Airborne: return "Airborne";
                    case StatusKind.Bounce: return "Bounced";
                    case StatusKind.Slip: return "Slipping";
                    case StatusKind.Slow: return "Slowed";
                }
            }

            if (kid.LangitProtected)
                return "Langit!";

            if (kid.Overclocked)
                return "Exhausted";

            return string.Empty;
        }

        /// <summary>True when the active overhead plate for <paramref name="playerId"/> is currently showing an HP bar.</summary>
        public bool IsHealthVisibleFor(int playerId)
        {
            for (int i = 0; i < MaxPlates; i++)
            {
                PlateSlot p = _plates[i];
                if (p.Active && p.BoundPlayerId == playerId)
                    return p.HpVisible;
            }
            return false;
        }

        public void SetVisible(bool visible)
        {
            if (_visible == visible && (Root == null || Root.gameObject.activeSelf == visible))
                return;
            _visible = visible;
            if (!visible)
                HideAll();
            if (Root != null && Root.gameObject.activeSelf != visible)
                Root.gameObject.SetActive(visible);
        }

        public void HideAll()
        {
            ActivePlateCount = 0;
            for (int i = 0; i < MaxPlates; i++)
            {
                PlateSlot p = _plates[i];
                if (p.Active)
                {
                    p.Active = false;
                    p.HpVisible = false;
                    p.StaminaVisible = false;
                    p.RootObj.SetActive(false);
                }
            }
        }

        /// <summary>Updates all visible 10-kid overhead plates with 0 B heap allocation.</summary>
        public void Refresh(
            SimBridge sim,
            Camera camera,
            Func<int, Vector3?> worldFeetOverride = null,
            MapLayoutData layout = null)
        {
            if (!_visible || sim == null || camera == null || sim.Phase == MatchPhaseKind.PointFreeze || sim.Phase == MatchPhaseKind.MatchOver)
            {
                HideAll();
                return;
            }

            IReadOnlyList<KidSnapshot> kids = sim.Kids;
            int totalKids = kids.Count;
            if (totalKids == 0)
            {
                HideAll();
                return;
            }

            float nowClock = sim.ClockSeconds;
            bool clockRewound = _prevClockSeconds >= 0f && nowClock < _prevClockSeconds - 0.25f;
            _prevClockSeconds = nowClock;
            if (clockRewound)
                _trackedCount = 0;

            UpdateRecentDamageTracking(kids, totalKids, nowClock);

            int localId = sim.LocalPlayerId;
            int localTeam = sim.LocalPlayerTeam;
            int maxHp = Mathf.Max(1, sim.MaxHp);
            int cbMode = SettingsStore.Current.ColorblindMode;
            bool reduceMotion = SettingsStore.Current.ReduceMotion;

            float reachScale = _shoulderNameReach > 0.01f ? (_shoulderNameReach / DefaultShoulderNameReach) : 1f;
            float maxEnemyDist = MaxEnemyDistanceMeters * reachScale;
            float maxAllyDist = MaxAllyDistanceMeters * reachScale;
            float barW = ScaledBarWidth;
            float barH = ScaledBarHeight;
            float stamH = ScaledStaminaHeight;

            // Order candidates: local player first, then visible allies/enemies sorted near-to-far
            int count = 0;
            Vector3 camPos = camera.transform.position;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < totalKids && count < MaxPlates; i++)
                {
                    KidSnapshot kid = kids[i];
                    bool isLocal = kid.Id == localId;
                    if ((pass == 0) != isLocal) continue;
                    if (kid.Tagged) continue;

                    Vector3 headWorld = ComputeBodyWorld(in kid, BaseKidHeadMeters, worldFeetOverride);
                    Vector3 vp = camera.WorldToViewportPoint(headWorld);

                    // Strictly cull behind-camera and off-screen kids — NEVER clamp off-screen kids onto viewport edges
                    if (vp.z <= 0.1f || vp.x < 0.01f || vp.x > 0.99f || vp.y < 0.01f || vp.y > 0.99f)
                        continue;

                    float dist = Vector3.Distance(camPos, headWorld);
                    bool isEnemy = !isLocal && kid.Team != localTeam;
                    bool isRevealed = isEnemy && kid.RevealedOutlineLeft > 0.01f;
                    if (!isLocal)
                    {
                        // Invisible enemy (Kikay Tago!) hides overhead plate unless revealed
                        if (isEnemy && kid.Invisible && !isRevealed)
                            continue;

                        float maxDist = (isEnemy && !isRevealed) ? maxEnemyDist : maxAllyDist;
                        if (dist > maxDist)
                            continue;

                        if (isEnemy && !isRevealed && !WasDamagedRecently(kid.Id, nowClock))
                        {
                            Vector3 chestWorld = ComputeBodyWorld(in kid, BaseKidChestMeters, worldFeetOverride);
                            if (IsEnemyOccluded(camPos, chestWorld, layout))
                                continue;
                        }
                    }

                    _candidateIndices[count] = i;
                    _candidateDepths[count] = isLocal ? -1f : dist;
                    count++;
                }
            }

            // Insertion sort non-local candidates by depth ascending (<= 11 elements, 0 allocations)
            for (int i = 2; i < count; i++)
            {
                int idxKey = _candidateIndices[i];
                float depthKey = _candidateDepths[i];
                int j = i - 1;
                while (j >= 1 && _candidateDepths[j] > depthKey)
                {
                    _candidateIndices[j + 1] = _candidateIndices[j];
                    _candidateDepths[j + 1] = _candidateDepths[j];
                    j--;
                }
                _candidateIndices[j + 1] = idxKey;
                _candidateDepths[j + 1] = depthKey;
            }

            Rect parentRect = Root.rect;
            if (parentRect.width <= 1f || parentRect.height <= 1f)
            {
                Vector2 safeSize = ScreenFitMetrics.SafeCanvasSize(_canvas);
                parentRect = new Rect(-safeSize.x * 0.5f, -safeSize.y * 0.5f, safeSize.x, safeSize.y);
            }

            Vector2Int screenSizePx = ScreenFitMetrics.ScreenSizePx;
            Vector2 crosshairScreenPx = new Vector2(screenSizePx.x * CrosshairViewport.x, screenSizePx.y * CrosshairViewport.y);
            Vector2 crosshairLocal = ScreenToLocalPoint(crosshairScreenPx, camera, parentRect, screenSizePx);
            float crosshairExclusionRadius = ScreenFitMetrics.DpToCanvasPx(CrosshairExclusionDp, _canvas);

            int placedCount = 0;
            for (int c = 0; c < count; c++)
            {
                KidSnapshot kid = kids[_candidateIndices[c]];
                PlateSlot slot = _plates[placedCount];

                Vector3 headWorld = ComputeBodyWorld(in kid, BaseKidHeadMeters, worldFeetOverride);
                float dist = Vector3.Distance(camPos, headWorld);
                float scale = ComputeDistanceScale(dist, _shoulderNameReach);

                Vector3 screenPt = camera.WorldToScreenPoint(headWorld);
                Vector2 localPt = ScreenToLocalPoint(new Vector2(screenPt.x, screenPt.y), camera, parentRect, screenSizePx);

                bool isLocal = kid.Id == localId;
                bool isEnemy = !isLocal && kid.Team != localTeam;
                bool isRevealed = isEnemy && kid.RevealedOutlineLeft > 0.01f;
                bool showHp = !isEnemy;
                bool showStamina = isLocal && ((kid.UsingSpecKit && kid.MaxWater > 0.01f) || kid.MaxStamina > 0.01f);
                bool withinLabelReach = isLocal || isRevealed || _shoulderNameReach <= 0f || dist <= _shoulderNameReach || WasDamagedRecently(kid.Id, nowClock);
                string statusWord = withinLabelReach ? ResolvePriorityStatus(in kid) : string.Empty;

                float rawWidth = showHp ? (barW + BadgeSize - BadgeOverlap) : BadgeSize;
                float w = rawWidth * scale;

                // Asymmetric vertical bounds relative to plate center so local "You" (+24 px), stamina/water, and revealed eye are fully protected
                float bottomExtent = (barH * 0.5f + (showStamina ? (stamH - BorderPx) : 0f) + 2f) * scale;
                float topExtent;
                if (isLocal)
                    topExtent = (barH * 0.5f + LocalYouTopPadPx + (statusWord.Length > 0 ? 24f : 0f)) * scale + 4f;
                else if (isRevealed)
                    topExtent = (barH * 0.5f + 22f + (statusWord.Length > 0 ? 20f : 0f)) * scale;
                else if (statusWord.Length > 0)
                    topExtent = (barH * 0.5f + 22f) * scale;
                else
                    topExtent = (barH * 0.5f + 2f) * scale;

                if (bottomExtent + topExtent < MinVerticalSeparationPx)
                {
                    float pad = (MinVerticalSeparationPx - (bottomExtent + topExtent)) * 0.5f;
                    bottomExtent += pad;
                    topExtent += pad;
                }

                float cx = localPt.x;
                float cy = localPt.y;
                bool nearCrosshair = false;

                // Sacred (0.50, 0.54) crosshair exclusion: push non-local plates outside/above 72 dp radius and lower alpha
                if (!isLocal)
                {
                    nearCrosshair = PushOutsideCrosshairZone(ref cx, ref cy, crosshairLocal, crosshairExclusionRadius, bottomExtent);
                }

                // Vertical overlap de-stacking: lift overlapping bars upward so ally bars clear "You" and each other cleanly
                Rect candidate = Rect.MinMaxRect(cx - w * 0.5f, cy - bottomExtent, cx + w * 0.5f, cy + topExtent);
                int guard = 0;
                bool adjusted = true;
                while (adjusted && guard++ < MaxPlates)
                {
                    adjusted = false;
                    for (int p = 0; p < placedCount; p++)
                    {
                        Rect taken = _placedRects[p];
                        bool xOverlap = candidate.xMin < taken.xMax + 6f && candidate.xMax > taken.xMin - 6f;
                        if (!xOverlap) continue;

                        float takenCenterY = taken.yMin + taken.height * 0.5f;
                        float candCenterY = candidate.yMin + candidate.height * 0.5f;
                        bool boxOverlap = candidate.yMin < taken.yMax + 3f && candidate.yMax > taken.yMin - 3f;
                        bool centerTooClose = Mathf.Abs(candCenterY - takenCenterY) < MinVerticalSeparationPx;
                        if (boxOverlap || centerTooClose)
                        {
                            float liftedBottom = Mathf.Max(taken.yMax + 3f, takenCenterY + MinVerticalSeparationPx - bottomExtent);
                            cy = liftedBottom + bottomExtent;
                            if (!isLocal && PushOutsideCrosshairZone(ref cx, ref cy, crosshairLocal, crosshairExclusionRadius, bottomExtent))
                                nearCrosshair = true;
                            candidate = Rect.MinMaxRect(cx - w * 0.5f, cy - bottomExtent, cx + w * 0.5f, cy + topExtent);
                            adjusted = true;
                        }
                    }
                }

                float maxCenterY = parentRect.yMax - TopReservedPx - topExtent;
                if (cy > maxCenterY)
                {
                    // Cull non-local plates pushed off the top of the screen rather than clamping back onto "You" or the crosshair
                    if (!isLocal)
                        continue;
                    cy = Mathf.Max(parentRect.yMin + bottomExtent, maxCenterY);
                    candidate = Rect.MinMaxRect(cx - w * 0.5f, cy - bottomExtent, cx + w * 0.5f, cy + topExtent);
                }

                _placedRects[placedCount] = candidate;
                float targetAlpha = (!isLocal && nearCrosshair) ? CrosshairNearAlpha : 1f;

                ApplyPlate(
                    slot,
                    in kid,
                    kids,
                    isLocal,
                    showHp,
                    showStamina,
                    statusWord,
                    new Vector2(cx, cy),
                    scale,
                    targetAlpha,
                    maxHp,
                    localTeam,
                    cbMode,
                    reduceMotion,
                    barW,
                    barH);
                placedCount++;
            }

            ActivePlateCount = placedCount;
            for (int i = placedCount; i < MaxPlates; i++)
            {
                PlateSlot unused = _plates[i];
                if (unused.Active)
                {
                    unused.Active = false;
                    unused.HpVisible = false;
                    unused.StaminaVisible = false;
                    unused.RootObj.SetActive(false);
                }
            }
        }

        Vector2 ScreenToLocalPoint(Vector2 screenPt, Camera fallbackCamera, Rect parentRect, Vector2Int screenSizePx)
        {
            if (_canvas != null && Root.gameObject.activeInHierarchy)
            {
                Camera eventCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : (_canvas.worldCamera != null ? _canvas.worldCamera : fallbackCamera);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screenPt, eventCam, out Vector2 localPt))
                    return localPt;
            }

            float nx = (screenPt.x / Mathf.Max(1f, screenSizePx.x)) - 0.5f;
            float ny = (screenPt.y / Mathf.Max(1f, screenSizePx.y)) - 0.5f;
            return new Vector2(nx * parentRect.width, ny * parentRect.height);
        }

        static bool PushOutsideCrosshairZone(
            ref float cx,
            ref float cy,
            Vector2 crosshairLocal,
            float exclusionRadius,
            float bottomExtent)
        {
            float dx = cx - crosshairLocal.x;
            float dy = cy - crosshairLocal.y;
            float distSq = dx * dx + dy * dy;
            if (distSq >= exclusionRadius * exclusionRadius)
                return false;

            // Prefer lifting cleanly above the reticle sight circle so center horizon targets stay unobstructed below
            if (Mathf.Abs(dx) < exclusionRadius && dy >= -24f)
            {
                float chordY = Mathf.Sqrt(Mathf.Max(0f, exclusionRadius * exclusionRadius - dx * dx));
                cy = crosshairLocal.y + chordY + Mathf.Max(4f, bottomExtent * 0.35f);
            }
            else
            {
                float dist = Mathf.Sqrt(distSq);
                Vector2 dir = dist > 0.001f ? new Vector2(dx / dist, dy / dist) : Vector2.up;
                cx = crosshairLocal.x + dir.x * (exclusionRadius + 4f);
                cy = crosshairLocal.y + dir.y * (exclusionRadius + 4f);
            }
            return true;
        }

        void UpdateRecentDamageTracking(IReadOnlyList<KidSnapshot> kids, int totalKids, float nowClock)
        {
            for (int i = 0; i < totalKids; i++)
            {
                KidSnapshot k = kids[i];
                int slot = FindOrAddDamageTrackSlot(k.Id, k.Hp);
                if (slot < 0) continue;

                int prevHp = _trackedPrevHp[slot];
                if (!k.Tagged && k.Hp < prevHp)
                    _lastDamagedClock[slot] = nowClock;
                else if (k.Tagged || k.Hp > prevHp)
                    _lastDamagedClock[slot] = -999f;

                _trackedPrevHp[slot] = k.Hp;
            }
        }

        int FindOrAddDamageTrackSlot(int playerId, int initialHp)
        {
            for (int i = 0; i < _trackedCount; i++)
            {
                if (_trackedPlayerIds[i] == playerId)
                    return i;
            }
            if (_trackedCount >= DamageTrackSlots)
                return -1;

            int idx = _trackedCount++;
            _trackedPlayerIds[idx] = playerId;
            _trackedPrevHp[idx] = initialHp;
            _lastDamagedClock[idx] = -999f;
            return idx;
        }

        bool WasDamagedRecently(int playerId, float nowClock)
        {
            for (int i = 0; i < _trackedCount; i++)
            {
                if (_trackedPlayerIds[i] == playerId)
                {
                    float t = _lastDamagedClock[i];
                    return t >= 0f && (nowClock - t) <= RecentlyDamagedSeconds;
                }
            }
            return false;
        }

        static bool IsEnemyOccluded(Vector3 camWorld, Vector3 enemyChestWorld, MapLayoutData layout)
        {
            Vector3 seg = enemyChestWorld - camWorld;
            float segLen = seg.magnitude;
            if (segLen <= 1.2f)
                return false;

            // 1. Unity Physics collider linecast (ignores near-camera & target-body 0.55 m endpoints)
            Vector3 dir = seg / segLen;
            float probeLen = segLen - 1.10f;
            if (probeLen > 0.1f &&
                Physics.Raycast(camWorld + dir * 0.55f, dir, out RaycastHit hit, probeLen, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y < 0.85f)
                    return true;
            }

            // 2. Deterministic analytic MapLayoutData oriented building/cover parcel OBB check
            if (layout != null && layout.Obstacles != null)
            {
                var fromSim = new SVector3(camWorld.x, camWorld.y, -camWorld.z);
                var toSim = new SVector3(enemyChestWorld.x, enemyChestWorld.y, -enemyChestWorld.z);
                int parcels = layout.ParcelCount;
                bool oriented = layout.HasOrientedParcels;

                for (int p = 0; p < parcels; p++)
                {
                    GroundRect r = layout.ParcelRect(p);
                    string prop = layout.ParcelProp(p);
                    float height = (prop == "cover_wall" || prop == "bench")
                        ? Mathf.Max(0.9f, layout.CoverHeight)
                        : DefaultBuildingHeightMeters;

                    float yaw = oriented ? layout.ParcelYawRad(p) : 0f;
                    if (SegmentIntersectsParcelObb(fromSim, toSim, r, height, yaw))
                        return true;
                }
            }

            return false;
        }

        static bool SegmentIntersectsParcelObb(SVector3 fromSim, SVector3 toSim, GroundRect rect, float height, float yawRad)
        {
            SVector2 center = rect.Center;
            float halfW = rect.W * 0.5f;
            float halfH = rect.H * 0.5f;
            if (halfW <= 0.01f || halfH <= 0.01f)
                return false;

            float cos = MathF.Cos(yawRad);
            float sin = MathF.Sin(yawRad);

            float fdx = fromSim.X - center.X;
            float fdz = fromSim.Z - center.Y;
            var localStart = new SVector3(fdx * cos - fdz * sin, fromSim.Y, fdx * sin + fdz * cos);

            // Skip if camera already sits inside this parcel footprint
            if (MathF.Abs(localStart.X) <= halfW && MathF.Abs(localStart.Z) <= halfH && localStart.Y <= height)
                return false;

            float tdx = toSim.X - center.X;
            float tdz = toSim.Z - center.Y;
            var localEnd = new SVector3(tdx * cos - tdz * sin, toSim.Y, tdx * sin + tdz * cos);
            SVector3 delta = localEnd - localStart;

            float tMin = 0.01f;
            float tMax = 0.97f;
            if (!ClipSlab(localStart.X, delta.X, -halfW, halfW, ref tMin, ref tMax)) return false;
            if (!ClipSlab(localStart.Y, delta.Y, 0f, height, ref tMin, ref tMax)) return false;
            if (!ClipSlab(localStart.Z, delta.Z, -halfH, halfH, ref tMin, ref tMax)) return false;
            return tMax >= tMin;
        }

        static bool ClipSlab(float origin, float dir, float minBound, float maxBound, ref float tMin, ref float tMax)
        {
            if (MathF.Abs(dir) < 1e-6f)
                return origin >= minBound && origin <= maxBound;

            float inv = 1f / dir;
            float t1 = (minBound - origin) * inv;
            float t2 = (maxBound - origin) * inv;
            if (t1 > t2)
            {
                float tmp = t1;
                t1 = t2;
                t2 = tmp;
            }
            if (t1 > tMin) tMin = t1;
            if (t2 < tMax) tMax = t2;
            return tMax >= tMin;
        }

        Vector3 ComputeBodyWorld(in KidSnapshot kid, float baseHeightMeters, Func<int, Vector3?> worldFeetOverride)
        {
            float crouchMul = kid.Crouching ? 0.78f : 1f;
            float yOffset = baseHeightMeters * KidViewScale * crouchMul;
            if (worldFeetOverride != null)
            {
                Vector3? customFeet = worldFeetOverride(kid.Id);
                if (customFeet.HasValue)
                    return customFeet.Value + new Vector3(0f, yOffset, 0f);
            }
            return KalyeahResources.World(kid.Position, yOffset + kid.AirY);
        }

        static void ApplyPlate(
            PlateSlot slot,
            in KidSnapshot kid,
            IReadOnlyList<KidSnapshot> allKids,
            bool isLocal,
            bool showHp,
            bool showStamina,
            string statusWord,
            Vector2 anchoredPos,
            float scale,
            float alpha,
            int maxHp,
            int localTeam,
            int cbMode,
            bool reduceMotion,
            float barW,
            float barH)
        {
            if (!slot.Active)
            {
                slot.Active = true;
                slot.RootObj.SetActive(true);
            }

            slot.BoundPlayerId = kid.Id;
            slot.Root.anchoredPosition = anchoredPos;
            slot.Root.localScale = new Vector3(scale, scale, 1f);
            if (Mathf.Abs(slot.LastAlpha - alpha) > 0.005f)
            {
                slot.LastAlpha = alpha;
                slot.Group.alpha = alpha;
            }

            // Center badge cleanly when enemy HP plate is hidden; anchor left of bar when ally/self HP is shown
            slot.Root.sizeDelta = showHp
                ? new Vector2(barW + BadgeSize - BadgeOverlap, barH)
                : new Vector2(BadgeSize, barH);

            int badgeNum = ComputeSlotNumber(in kid, allKids);
            if (slot.LastBadgeNumber != badgeNum)
            {
                slot.LastBadgeNumber = badgeNum;
                slot.BadgeText.text = (badgeNum >= 0 && badgeNum < NumberStrings.Length) ? NumberStrings[badgeNum] : NumberStrings[0];
            }
            slot.BadgeFill.color = KomiksUi.TeamColor(kid.Team, localTeam, cbMode);

            if (slot.HpVisible != showHp || slot.HpPlateObj.activeSelf != showHp)
            {
                slot.HpVisible = showHp;
                slot.HpPlateObj.SetActive(showHp);
            }

            if (showHp)
            {
                float hpRatio = Mathf.Clamp01((float)kid.Hp / maxHp);
                slot.HpFillRect.anchorMax = new Vector2(hpRatio, 1f);
                slot.HpFillImage.color = isLocal ? KomiksUi.SelfHealthColor(cbMode) : KomiksUi.AllyHealthColor(cbMode);

                bool hasShield = kid.ShieldHp > 0.5f;
                if (slot.ShieldFillObj.activeSelf != hasShield)
                    slot.ShieldFillObj.SetActive(hasShield);
                if (hasShield)
                {
                    float shRatio = Mathf.Clamp01(kid.ShieldHp / maxHp);
                    slot.ShieldFillRect.anchorMin = new Vector2(Mathf.Clamp01(1f - shRatio), 0f);
                    slot.ShieldFillRect.anchorMax = Vector2.one;
                }
            }

            if (slot.StaminaVisible != showStamina || slot.StaminaPlateObj.activeSelf != showStamina)
            {
                slot.StaminaVisible = showStamina;
                slot.StaminaPlateObj.SetActive(showStamina);
            }

            if (showStamina)
            {
                if (kid.UsingSpecKit && kid.MaxWater > 0.01f)
                {
                    float waterRatio = Mathf.Clamp01(kid.Water / kid.MaxWater);
                    slot.StaminaFillRect.anchorMax = new Vector2(waterRatio, 1f);
                    slot.StaminaFillImage.color = WaterBarColor;
                }
                else
                {
                    float stamRatio = kid.MaxStamina > 0.001f ? (kid.Stamina / kid.MaxStamina) : 1f;
                    Color stamCol = KomiksUi.StaminaColor(stamRatio, kid.Overclocked, kid.ExhaustLeft, reduceMotion, out float displayRatio);
                    slot.StaminaFillRect.anchorMax = new Vector2(displayRatio, 1f);
                    slot.StaminaFillImage.color = stamCol;
                }
            }

            bool showRevealEye = !isLocal && kid.Team != localTeam && kid.RevealedOutlineLeft > 0.01f;
            if (slot.RevealEyeObj.activeSelf != showRevealEye)
                slot.RevealEyeObj.SetActive(showRevealEye);
            if (showRevealEye && slot.RevealEyeIcon != null)
                slot.RevealEyeIcon.color = RevealEyeColor;

            if (slot.YouObj.activeSelf != isLocal)
                slot.YouObj.SetActive(isLocal);

            bool hasStatus = statusWord.Length > 0;
            if (slot.StatusObj.activeSelf != hasStatus)
                slot.StatusObj.SetActive(hasStatus);

            if (hasStatus)
            {
                if (!ReferenceEquals(slot.LastStatus, statusWord) && slot.LastStatus != statusWord)
                {
                    slot.LastStatus = statusWord;
                    slot.StatusText.text = statusWord;
                }
                slot.StatusText.rectTransform.anchoredPosition = isLocal
                    ? new Vector2(6f, 23f)
                    : (showRevealEye ? new Vector2(0f, 24f) : new Vector2(showHp ? 6f : 0f, 3f));
            }
            else
            {
                slot.LastStatus = string.Empty;
            }
        }

        static int ComputeSlotNumber(in KidSnapshot target, IReadOnlyList<KidSnapshot> allKids)
        {
            int row = 0;
            int teamZero = 0;
            int teamOne = 0;
            bool before = true;
            int n = allKids.Count;
            for (int i = 0; i < n; i++)
            {
                KidSnapshot other = allKids[i];
                if (other.Id == target.Id)
                    before = false;
                else if (before && other.Team == target.Team)
                    row++;

                if (other.Team == 0) teamZero++;
                else teamOne++;
            }

            int teamCap = Mathf.Max(teamZero, teamOne);
            int num = (target.Team == 0 ? 1 : teamCap + 1) + row;
            return Mathf.Clamp(num, 1, NumberStrings.Length - 1);
        }
    }
}
