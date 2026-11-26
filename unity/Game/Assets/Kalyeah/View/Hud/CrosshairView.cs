using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// High-contrast 3P shoulder reticle anchored at viewport (0.50, 0.54) (matching AimSolver.CrosshairViewport),
    /// keeping the sacred 45%x50% center sightbox 100% clean and non-raycastable:
    /// - Thin Deadlock ring + 4 shadow-outlined cardinal hairlines + crisp center dot
    /// - 75 ms hit-confirm X-tick + heavy golden/coral tag sting
    /// - Compact Tirador primary cooldown under-pip (hidden when ready)
    /// - Peripheral incoming-damage directional arcs placed outside the central sightbox
    /// </summary>
    public sealed class CrosshairView : MonoBehaviour
    {
        public static readonly Vector2 ReticleViewport = new Vector2(0.50f, 0.54f);
        public const float HitConfirmSeconds = 0.095f;
        public const float HeadshotConfirmSeconds = 0.165f;
        public const float TagStingSeconds = 0.34f;
        public const float DamageArcSeconds = 0.65f;

        const int DamageArcPoolSize = 4;
        const float BaseRingSize = 24f;
        const float BaseHairGap = 5.5f;
        const float HairLength = 7.5f;

        // Option A3 G8: razor-clean white 4-tick Valorant crosshair + warm Coral-Gold (#FF9B6A) hit-confirm & headshot X-ticks
        static readonly Color IdleColor = new Color(1f, 1f, 1f, 0.94f);
        static readonly Color IdleRingColor = new Color(1f, 1f, 1f, 0.18f);
        static readonly Color EmpoweredColor = MenuArt.WaterCyan;
        static readonly Color AimHeldColor = MenuArt.CoralGold;
        static readonly Color CancelAimColor = MenuArt.SignalVermilion;
        static readonly Color BodyHitCoralGold = MenuArt.CoralGold;
        static readonly Color HeadshotCoralBright = new Color(1f, 0.72f, 0.48f, 1f);
        static readonly Color TagStingCoralGold = MenuArt.CoralGold;
        static readonly Color ChargeStartColor = MenuArt.PaperWhite;
        static readonly Color ChargeMaxColor = MenuArt.WaterCyan;
        static readonly Color ReloadBarColor = MenuArt.CoralGold;

        static readonly string[] MeterStrings = BuildMeterStrings();
        static readonly string[] HoldMeterStrings = BuildMeterStrings("HOLD ");
        static readonly Color ReachNeedsChargeColor = MenuArt.CoralGold;
        static readonly Color ReachTooFarColor = MenuArt.SignalVermilion;
        /// <summary>The crosshair's distance readout stops here: past it the ray is open sky or the far end of the street.</summary>
        const float ReachProbeMeters = 64f;

        static Sprite _damageArcSprite;

        sealed class DamageArcSlot
        {
            public RectTransform Pivot;
            public Image Arc;
            public float TimeLeft;
            public float Duration;
            public float ScreenAngleDeg;
            public bool Heavy;
        }

        readonly RectTransform[] _cardinalTicks = new RectTransform[4];
        readonly Image[] _cardinalImages = new Image[4];
        readonly RectTransform[] _hitTicks = new RectTransform[4];
        readonly Image[] _hitTickImages = new Image[4];
        readonly DamageArcSlot[] _damageArcs = new DamageArcSlot[DamageArcPoolSize];
        readonly Dictionary<string, int> _lastCasterByWeapon = new Dictionary<string, int>(16, StringComparer.Ordinal);

        RectTransform _root;
        RectTransform _reticleCenter;
        Image _outerRing;
        Image _centerDot;
        Image _tagStingRing;

        RectTransform _primaryPipRoot;
        Image _primaryPipFill;
        RectTransform _chargeBarRoot;
        Image _chargeBarFill;
        Text _chargeRangeText;
        Text _reachText;
        /// <summary>The last crosshair distance check: 0 = a tap reaches, 1 = only a charged shot reaches, 2 = out of reach.</summary>
        public int ReachState { get; private set; } = -1;
        RectTransform _tagStampRoot;
        CanvasGroup _tagStampGroup;

        SimBridge _boundSim;
        int _coneCasterId = -1;
        string _coneWeaponId = "";
        int _lastEnemyCasterId = -1;
        int _lastHitConfirmTick = int.MinValue;
        float _lastCameraYawDeg;

        float _bloom;
        float _recoil;
        float _hitTimer;
        float _hitDuration = HitConfirmSeconds;
        bool _hitIsHeadshot;
        bool _hitIsTag;
        float _tagStampTimer;
        bool _isOverEnemy;
        bool _reduceMotion;
        int _colorblindMode;
        bool _updatingLayout;

        public AudioHub Audio { get; set; }
        public RectTransform Root => _root;
        public RectTransform ReticleRect => _reticleCenter;
        public bool IsOverEnemy => _isOverEnemy;
        public bool IsShowingHitConfirm => _hitTimer > 0.0001f;
        public bool IsShowingHeadshot => _hitTimer > 0.0001f && _hitIsHeadshot;
        public bool IsShowingTagSting => _hitTimer > 0.0001f && _hitIsTag;
        public bool IsShowingTagStamp => _tagStampTimer > 0.0001f;

        public static CrosshairView Build(Transform parent)
        {
            RectTransform root = Ui.Fill("CrosshairView", parent);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var view = root.gameObject.AddComponent<CrosshairView>();
            view._root = root;
            view._reduceMotion = SettingsStore.Current.ReduceMotion;
            view._colorblindMode = SettingsStore.Current.ColorblindMode;
            view.BuildHierarchy();
            view.RefreshLayout();
            return view;
        }

        void BuildHierarchy()
        {
            _reticleCenter = Ui.Panel("Reticle", _root, ReticleViewport, ReticleViewport, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 36f));

            _outerRing = Ui.MakeImage("OuterRing", _reticleCenter, IdleRingColor, round: false, raycast: false);
            _outerRing.sprite = HudIconCache.RingSprite();
            _outerRing.rectTransform.sizeDelta = new Vector2(BaseRingSize, BaseRingSize);

            _tagStingRing = Ui.MakeImage("TagStingRing", _reticleCenter, new Color(TagStingCoralGold.r, TagStingCoralGold.g, TagStingCoralGold.b, 0f), round: false, raycast: false);
            _tagStingRing.sprite = HudIconCache.RingSprite();
            _tagStingRing.rectTransform.sizeDelta = new Vector2(BaseRingSize + 10f, BaseRingSize + 10f);
            _tagStingRing.enabled = false;

            // 4 razor-clean white Valorant cardinal hairlines (0=Up, 1=Right, 2=Down, 3=Left)
            for (int i = 0; i < 4; i++)
            {
                Image tick = CreateStrokedTick($"Hair_{i}", _reticleCenter, IdleColor, new Vector2(2.2f, HairLength));
                tick.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -i * 90f);
                _cardinalTicks[i] = tick.rectTransform;
                _cardinalImages[i] = tick;
            }

            _centerDot = Ui.MakeImage("CenterDot", _reticleCenter, IdleColor, round: true, raycast: false);
            _centerDot.rectTransform.sizeDelta = new Vector2(3.2f, 3.2f);
            var dotShadow = _centerDot.gameObject.AddComponent<Outline>();
            dotShadow.effectColor = new Color(0f, 0f, 0f, 0.62f);
            dotShadow.effectDistance = new Vector2(1f, -1f);

            // 4 diagonal X-ticks in warm Coral-Gold (#FF9B6A) for hit-confirm, headshot, and heavy tag sting (45, 135, 225, 315 deg)
            for (int i = 0; i < 4; i++)
            {
                Image xTick = CreateStrokedTick($"HitX_{i}", _reticleCenter, BodyHitCoralGold, new Vector2(2.6f, 9.5f));
                xTick.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f + i * 90f);
                xTick.enabled = false;
                _hitTicks[i] = xTick.rectTransform;
                _hitTickImages[i] = xTick;
            }

            // Compact primary fire cooldown pip tucked 19 px below reticle (hidden at 0 cooldown)
            _primaryPipRoot = Ui.Panel("PrimaryCooldownPip", _reticleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -19f), new Vector2(22f, 4.5f));
            Image pipBg = Ui.MakeImage("Track", _primaryPipRoot, new Color(14f / 255f, 19f / 255f, 26f / 255f, 0.78f), round: false, raycast: false);
            pipBg.rectTransform.anchorMin = Vector2.zero;
            pipBg.rectTransform.anchorMax = Vector2.one;
            pipBg.rectTransform.sizeDelta = Vector2.zero;

            _primaryPipFill = Ui.MakeImage("Fill", _primaryPipRoot, MenuArt.PaperWhite, round: false, raycast: false);
            _primaryPipFill.type = Image.Type.Filled;
            _primaryPipFill.fillMethod = Image.FillMethod.Horizontal;
            _primaryPipFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _primaryPipFill.fillAmount = 1f;
            _primaryPipFill.rectTransform.anchorMin = Vector2.zero;
            _primaryPipFill.rectTransform.anchorMax = Vector2.one;
            _primaryPipFill.rectTransform.offsetMin = new Vector2(0.8f, 0.8f);
            _primaryPipFill.rectTransform.offsetMax = new Vector2(-0.8f, -0.8f);
            _primaryPipRoot.gameObject.SetActive(false);

            // Tirador hold-to-charge range bar (18 m -> 30 m) & reload progress bar directly under reticle (hidden when idle)
            _chargeBarRoot = Ui.Panel("ChargeRangeBar", _reticleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(58f, 6f));
            Image chargeBg = Ui.MakeImage("Track", _chargeBarRoot, new Color(14f / 255f, 19f / 255f, 26f / 255f, 0.86f), round: false, raycast: false);
            chargeBg.rectTransform.anchorMin = Vector2.zero;
            chargeBg.rectTransform.anchorMax = Vector2.one;
            chargeBg.rectTransform.sizeDelta = Vector2.zero;

            _chargeBarFill = Ui.MakeImage("Fill", _chargeBarRoot, ChargeStartColor, round: false, raycast: false);
            _chargeBarFill.type = Image.Type.Filled;
            _chargeBarFill.fillMethod = Image.FillMethod.Horizontal;
            _chargeBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _chargeBarFill.fillAmount = 0f;
            _chargeBarFill.rectTransform.anchorMin = Vector2.zero;
            _chargeBarFill.rectTransform.anchorMax = Vector2.one;
            _chargeBarFill.rectTransform.offsetMin = new Vector2(1f, 1f);
            _chargeBarFill.rectTransform.offsetMax = new Vector2(-1f, -1f);

            _chargeRangeText = Ui.MakeText("RangeMeters", _chargeBarRoot, "18m", 11, MenuArt.PaperWhite, TextAnchor.UpperCenter, KalyeahResources.DisplayFont());
            _chargeRangeText.resizeTextForBestFit = true;
            _chargeRangeText.resizeTextMinSize = 9;
            _chargeRangeText.resizeTextMaxSize = 11;
            _chargeRangeText.rectTransform.anchorMin = _chargeRangeText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _chargeRangeText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _chargeRangeText.rectTransform.anchoredPosition = new Vector2(0f, -2f);
            _chargeRangeText.rectTransform.sizeDelta = new Vector2(58f, 15f);
            _chargeBarRoot.gameObject.SetActive(false);

            // Distance to whatever the crosshair is on, coloured by whether a slingshot stone reaches it.
            _reachText = Ui.MakeText("ReachMeters", _reticleCenter, "", 18, MenuArt.PaperWhite, TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            _reachText.rectTransform.anchorMin = _reachText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _reachText.rectTransform.pivot = new Vector2(0f, 0.5f);
            _reachText.rectTransform.anchoredPosition = new Vector2(26f, 0f);
            _reachText.rectTransform.sizeDelta = new Vector2(110f, 24f);
            _reachText.raycastTarget = false;
            // Read over sky and street alike: a dark outline round the numbers.
            var reachInk = _reachText.gameObject.AddComponent<Outline>();
            reachInk.effectColor = new Color(0f, 0f, 0f, 0.85f);
            reachInk.effectDistance = new Vector2(1.5f, -1.5f);
            _reachText.gameObject.SetActive(false);

            // Bold "TAYA!" tag confirmation stamp tucked just below the reticle (only flashes for 340 ms on confirmed tag)
            _tagStampRoot = Ui.Panel("TayaStamp", _reticleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -34f), new Vector2(88f, 24f));
            _tagStampGroup = _tagStampRoot.gameObject.AddComponent<CanvasGroup>();
            _tagStampGroup.interactable = false;
            _tagStampGroup.blocksRaycasts = false;
            Image stampBg = KomiksUi.MakeInkPanel("Plate", _tagStampRoot, new Color(MenuArt.SignalVermilionDeep.r, MenuArt.SignalVermilionDeep.g, MenuArt.SignalVermilionDeep.b, 0.94f), TagStingCoralGold, 4, 2, raycast: false);
            stampBg.rectTransform.anchorMin = Vector2.zero;
            stampBg.rectTransform.anchorMax = Vector2.one;
            stampBg.rectTransform.sizeDelta = Vector2.zero;
            Text tagStampText = Ui.MakeText("Label", _tagStampRoot, "TAYA!", 15, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            tagStampText.resizeTextForBestFit = true;
            tagStampText.resizeTextMinSize = 11;
            tagStampText.resizeTextMaxSize = 15;
            tagStampText.rectTransform.anchorMin = Vector2.zero;
            tagStampText.rectTransform.anchorMax = Vector2.one;
            tagStampText.rectTransform.offsetMin = new Vector2(6f, 2f);
            tagStampText.rectTransform.offsetMax = new Vector2(-6f, -2f);
            _tagStampRoot.gameObject.SetActive(false);

            // Peripheral incoming-damage directional arcs (outside central 45%x50% sightbox)
            Sprite arcSprite = DamageArcSprite();
            for (int i = 0; i < DamageArcPoolSize; i++)
            {
                RectTransform pivot = Ui.Panel($"DamageArcPivot_{i}", _reticleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                Image arc = Ui.MakeImage("Arc", pivot, new Color(0.96f, 0.20f, 0.18f, 0f), round: false, raycast: false);
                arc.sprite = arcSprite;
                arc.rectTransform.anchorMin = arc.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                arc.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                arc.rectTransform.anchoredPosition = new Vector2(0f, 206f);
                arc.rectTransform.sizeDelta = new Vector2(124f, 28f);
                arc.enabled = false;
                _damageArcs[i] = new DamageArcSlot { Pivot = pivot, Arc = arc };
            }

            ApplyBloomPositions(0f, IdleColor, IdleRingColor);
        }

        static Image CreateStrokedTick(string name, Transform parent, Color color, Vector2 size)
        {
            Image img = Ui.MakeImage(name, parent, color, round: false, raycast: false);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            img.rectTransform.sizeDelta = size;
            var outline = img.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.52f);
            outline.effectDistance = new Vector2(1.1f, -1.1f);
            return img;
        }

        void OnEnable()
        {
            ScreenFitMetrics.SimulatedViewportChanged += RefreshLayout;
            SettingsStore.Current.Changed += OnSettingsChanged;
            OnSettingsChanged();
            RefreshLayout();
        }

        void OnDisable()
        {
            ScreenFitMetrics.SimulatedViewportChanged -= RefreshLayout;
            SettingsStore.Current.Changed -= OnSettingsChanged;
        }

        void OnDestroy() => Unbind();

        void OnRectTransformDimensionsChange()
        {
            if (!_updatingLayout && _root != null)
                RefreshLayout();
        }

        void OnSettingsChanged()
        {
            _reduceMotion = SettingsStore.Current.ReduceMotion;
            _colorblindMode = SettingsStore.Current.ColorblindMode;
        }

        /// <summary>Subscribes to SimBridge combat events for automatic 75 ms hit confirms, tag stings, recoil kicks, and damage arcs.</summary>
        public void Bind(SimBridge sim)
        {
            if (ReferenceEquals(_boundSim, sim)) return;
            Unbind();
            _boundSim = sim;
            if (_boundSim == null) return;

            _boundSim.WeaponCast += OnSimWeaponCast;
            _boundSim.ConeTriggered += OnSimConeTriggered;
            _boundSim.WeaponHit += OnSimWeaponHit;
            _boundSim.HitConfirmed += OnSimHitConfirmed;
            _boundSim.Tagged += OnSimTagged;
            _boundSim.PlayerDamaged += OnSimPlayerDamaged;
        }

        public void Unbind()
        {
            if (_boundSim == null) return;
            _boundSim.WeaponCast -= OnSimWeaponCast;
            _boundSim.ConeTriggered -= OnSimConeTriggered;
            _boundSim.WeaponHit -= OnSimWeaponHit;
            _boundSim.HitConfirmed -= OnSimHitConfirmed;
            _boundSim.Tagged -= OnSimTagged;
            _boundSim.PlayerDamaged -= OnSimPlayerDamaged;
            _boundSim = null;
        }

        /// <summary>Aligns the reticle to exact camera viewport (0.50, 0.54) even when parented inside an asymmetric notched SafeArea.</summary>
        public void RefreshLayout()
        {
            if (_root == null || _reticleCenter == null || _updatingLayout) return;
            _updatingLayout = true;
            try
            {
                Canvas canvas = _root.GetComponentInParent<Canvas>();
                Vector2Int screenPx = ScreenFitMetrics.ScreenSizePx;
                Vector2 targetScreenPx = new Vector2(screenPx.x * ReticleViewport.x, screenPx.y * ReticleViewport.y);
                Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

                if (!ScreenFitMetrics.HasSimulatedViewport &&
                    _root.rect.width > 1f &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, targetScreenPx, cam, out Vector2 localPt))
                {
                    _reticleCenter.anchorMin = _reticleCenter.anchorMax = new Vector2(0.5f, 0.5f);
                    _reticleCenter.anchoredPosition = localPt - _root.rect.center;
                }
                else
                {
                    // Exact analytic viewport placement (works headlessly in EditMode and with ScreenFitMetrics.SetSimulatedViewport)
                    Vector4 insetsPx = ScreenFitMetrics.ResolveSafeInsetsPx(true);
                    bool underSafeArea = _root.GetComponentInParent<SafeArea>() != null;
                    if (underSafeArea && screenPx.x > 0 && screenPx.y > 0)
                    {
                        Rect safePx = Rect.MinMaxRect(insetsPx.x, insetsPx.y, Mathf.Max(insetsPx.x + 1f, screenPx.x - insetsPx.z), Mathf.Max(insetsPx.y + 1f, screenPx.y - insetsPx.w));
                        float ax = Mathf.Clamp01((targetScreenPx.x - safePx.xMin) / safePx.width);
                        float ay = Mathf.Clamp01((targetScreenPx.y - safePx.yMin) / safePx.height);
                        _reticleCenter.anchorMin = _reticleCenter.anchorMax = new Vector2(ax, ay);
                        _reticleCenter.anchoredPosition = Vector2.zero;
                    }
                    else
                    {
                        _reticleCenter.anchorMin = _reticleCenter.anchorMax = ReticleViewport;
                        _reticleCenter.anchoredPosition = Vector2.zero;
                    }
                }

                // Keep peripheral damage arcs outside the central 45%x50% sightbox (sightbox half-height is 0.25 * 720 = 180 canvas px)
                Vector2 safeCanvas = ScreenFitMetrics.SafeCanvasSize(canvas);
                float peripheralRadius = Mathf.Clamp(Mathf.Min(safeCanvas.x * 0.26f, 212f), 192f, 224f);
                for (int i = 0; i < DamageArcPoolSize; i++)
                {
                    if (_damageArcs[i]?.Arc != null)
                        _damageArcs[i].Arc.rectTransform.anchoredPosition = new Vector2(0f, peripheralRadius);
                }
            }
            finally
            {
                _updatingLayout = false;
            }
        }

        /// <summary>Triggers the crisp white body X-tick or bold golden TAYA! tag stamp.</summary>
        public void FlashHit(bool isTag = false) => FlashHit(isHeadshot: false, isTag: isTag);

        /// <summary>Triggers 3-tier Valorant/CODM hitmarker: white X (body), gold X (headshot), and bold TAYA! stamp (tag).</summary>
        public void FlashHit(bool isHeadshot, bool isTag)
        {
            if (!isTag && !isHeadshot && _hitIsTag && _hitTimer > HitConfirmSeconds)
                return; // never cut an active tag sting short with a minor pellet tick
            if (!isTag && !isHeadshot && _hitIsHeadshot && _hitTimer > HitConfirmSeconds * 0.8f)
                return;

            _hitIsHeadshot = isHeadshot || (_hitIsHeadshot && isTag && _hitTimer > 0.01f);
            _hitIsTag = isTag;
            _hitDuration = isTag ? TagStingSeconds : (isHeadshot ? HeadshotConfirmSeconds : HitConfirmSeconds);
            _hitTimer = _hitDuration;
            if (isTag)
            {
                _tagStampTimer = TagStingSeconds;
                Haptics.HeavyImpact();
            }
            else if (isHeadshot)
            {
                Haptics.HeavyImpact();
            }
            else
            {
                Haptics.HitConfirm();
            }
        }

        public void KickRecoil(float amount = 1f)
        {
            _recoil = Mathf.Clamp(_recoil + Mathf.Max(0f, amount) * 0.55f, 0f, 1.4f);
        }

        /// <summary>Displays a peripheral incoming-damage directional arc pointing toward attackerSimPos relative to cameraYawDegrees.</summary>
        public void ShowIncomingDamage(SVector2 attackerSimPos, SVector2 localFeetSim, float cameraYawDegrees, bool heavy = false)
        {
            SVector2 delta = attackerSimPos - localFeetSim;
            if (delta.LengthSquared() <= 0.0001f)
            {
                ShowIncomingDamageAngle(180f, heavy);
                return;
            }
            var (right, down) = GodotSpace.GroundAxes(cameraYawDegrees);
            float screenRight = SVector2.Dot(delta, right);
            float screenUp = -SVector2.Dot(delta, down);
            float angleDeg = Mathf.Atan2(screenRight, screenUp) * Mathf.Rad2Deg;
            ShowIncomingDamageAngle(angleDeg, heavy);
        }

        /// <summary>Displays a peripheral incoming-damage arc at screenAngleDegrees (0 = ahead/up, +90 = right, +-180 = behind/down).</summary>
        public void ShowIncomingDamageAngle(float screenAngleDegrees, bool heavy = false)
        {
            // Merge into an existing arc within 28 degrees, or claim the oldest/free slot
            DamageArcSlot chosen = _damageArcs[0];
            for (int i = 0; i < DamageArcPoolSize; i++)
            {
                DamageArcSlot s = _damageArcs[i];
                if (s.TimeLeft > 0.01f && Mathf.Abs(Mathf.DeltaAngle(s.ScreenAngleDeg, screenAngleDegrees)) <= 28f)
                {
                    chosen = s;
                    break;
                }
                if (s.TimeLeft < chosen.TimeLeft)
                    chosen = s;
            }

            chosen.ScreenAngleDeg = screenAngleDegrees;
            chosen.Heavy = heavy || (chosen.TimeLeft > 0.05f && chosen.Heavy);
            chosen.Duration = chosen.Heavy ? DamageArcSeconds * 1.25f : DamageArcSeconds;
            chosen.TimeLeft = chosen.Duration;
            chosen.Pivot.localRotation = Quaternion.Euler(0f, 0f, -screenAngleDegrees);
            chosen.Arc.enabled = true;
        }

        public void Sync(SimBridge sim, float dt, float cameraYawDegrees = 0f, bool isAimingAbility = false, bool isCancelHovered = false)
            => Sync(sim, dt, cameraYawDegrees, isAimingAbility, isCancelHovered, null, 0f, null);

        public void Sync(
            SimBridge sim,
            float dt,
            float cameraYawDegrees,
            bool isAimingAbility,
            bool isCancelHovered,
            Camera worldCamera,
            float adsBlend,
            MapLayoutData layout = null)
        {
            _lastCameraYawDeg = cameraYawDegrees;
            KidSnapshot? localOpt = sim?.LocalKid;

            bool showReticle = localOpt == null || (!localOpt.Value.Tagged && (sim == null || sim.Phase == MatchPhaseKind.Playing || sim.Phase == MatchPhaseKind.Intro));
            if (_reticleCenter != null && _reticleCenter.gameObject.activeSelf != showReticle)
                _reticleCenter.gameObject.SetActive(showReticle);

            float clampedAds = Mathf.Clamp01(adsBlend);
            bool sprinting = localOpt.HasValue && (localOpt.Value.Sprinting || localOpt.Value.Dashing);
            bool charging = localOpt.HasValue && localOpt.Value.TiradorCharge01 >= 0f;
            float hipBloom = (sprinting ? 3.2f : 0f) + _recoil * 6.5f - (isAimingAbility ? 1.6f : 0f) - (charging ? localOpt.Value.TiradorCharge01 * 2.2f : 0f);
            float targetBloom = Mathf.Lerp(Mathf.Clamp(hipBloom, -2.0f, 12f), -3.2f, clampedAds);

            if (_reduceMotion)
            {
                _bloom = targetBloom;
                _recoil = Mathf.Max(0f, _recoil - dt * 6f);
            }
            else
            {
                _bloom = Mathf.MoveTowards(_bloom, targetBloom, dt * 48f);
                _recoil = Mathf.Max(0f, _recoil - dt * 5f);
            }

            _isOverEnemy = !isCancelHovered && showReticle && CheckEnemyUnderReticle(sim, worldCamera, layout);

            UpdateReach(sim, localOpt, showReticle, worldCamera, layout);

            Color coreCol;
            Color ringCol;
            if (isCancelHovered)
            {
                coreCol = CancelAimColor;
                ringCol = CancelAimColor;
            }
            else if (_isOverEnemy)
            {
                Color hl = KalyeahResources.ResolveEnemyHighlightColor(SettingsStore.Current.ResolvedEnemyHighlightMode);
                coreCol = new Color(hl.r, hl.g, hl.b, 0.98f);
                ringCol = new Color(hl.r, hl.g, hl.b, 0.68f);
            }
            else if (isAimingAbility)
            {
                coreCol = AimHeldColor;
                ringCol = AimHeldColor;
            }
            else if (localOpt.HasValue && localOpt.Value.EmpoweredShotReady)
            {
                coreCol = EmpoweredColor;
                ringCol = new Color(EmpoweredColor.r, EmpoweredColor.g, EmpoweredColor.b, 0.65f);
            }
            else
            {
                coreCol = IdleColor;
                ringCol = ReachState == 2 ? new Color(ReachTooFarColor.r, ReachTooFarColor.g, ReachTooFarColor.b, 0.55f) : IdleRingColor;
            }

            ApplyBloomPositions(_bloom, coreCol, ringCol, clampedAds);

            // Tirador hold-to-charge range bar (18m -> 30m) or reload progress bar
            bool showCharge = false;
            if (_chargeBarRoot != null && localOpt.HasValue && !localOpt.Value.Tagged)
            {
                KidSnapshot kid = localOpt.Value;
                if (kid.TiradorReloadLeft > 0.01f && kid.TiradorReloadTotal > 0.01f)
                {
                    showCharge = true;
                    float prog = Mathf.Clamp01(1f - kid.TiradorReloadLeft / kid.TiradorReloadTotal);
                    _chargeBarFill.fillAmount = prog;
                    _chargeBarFill.color = ReloadBarColor;
                    HudIconCache.SetTextIfChanged(_chargeRangeText, "RELOAD");
                    _chargeRangeText.color = ReloadBarColor;
                }
                else if (kid.TiradorCharge01 >= 0f)
                {
                    showCharge = true;
                    float charge = Mathf.Clamp01(kid.TiradorCharge01);
                    float rangeMeters = kid.TiradorCurrentRange > 0.5f
                        ? kid.TiradorCurrentRange
                        : Mathf.Lerp(sim != null && sim.TiradorMinRange > 0.5f ? sim.TiradorMinRange : 18f,
                                     sim != null && sim.TiradorMaxRange > 0.5f ? sim.TiradorMaxRange : 30f,
                                     charge);
                    _chargeBarFill.fillAmount = Mathf.Max(0.06f, charge);
                    _chargeBarFill.color = Color.Lerp(ChargeStartColor, ChargeMaxColor, charge);
                    int roundedMeters = Mathf.Clamp(Mathf.RoundToInt(rangeMeters), 0, MeterStrings.Length - 1);
                    HudIconCache.SetTextIfChanged(_chargeRangeText, MeterStrings[roundedMeters]);
                    _chargeRangeText.color = Color.Lerp(Ui.Cream, ChargeMaxColor, charge);
                }
                if (_chargeBarRoot.gameObject.activeSelf != showCharge)
                    _chargeBarRoot.gameObject.SetActive(showCharge);
            }

            // Primary cooldown pip (hidden while charge/reload bar occupies the same under-reticle slot)
            if (_primaryPipRoot != null)
            {
                float pCd = localOpt.HasValue ? localOpt.Value.PrimaryCooldown : 0f;
                float pMax = localOpt.HasValue && localOpt.Value.PrimaryMaxCooldown > 0.01f
                    ? localOpt.Value.PrimaryMaxCooldown
                    : (sim != null && sim.PrimaryWeaponInfo.HasValue ? Mathf.Max(0.1f, sim.PrimaryWeaponInfo.Value.Cooldown) : 0.48f);
                bool cooling = !showCharge && pCd > 0.02f;
                if (_primaryPipRoot.gameObject.activeSelf != cooling)
                    _primaryPipRoot.gameObject.SetActive(cooling);
                if (cooling)
                    _primaryPipFill.fillAmount = Mathf.Clamp01(1f - pCd / pMax);
            }

            // 3-tier hit-confirm X-tick + bold TAYA! tag stamp
            StepHitConfirm(dt);

            // Peripheral incoming-damage directional arcs
            StepDamageArcs(dt);
        }

        void ApplyBloomPositions(float bloom, Color coreCol, Color ringCol, float adsBlend = 0f)
        {
            float ringAlphaScale = 1f - adsBlend * 0.78f;
            float ringSize = Mathf.Lerp(Mathf.Max(18f, BaseRingSize + bloom * 1.3f), 11f, adsBlend);
            _outerRing.rectTransform.sizeDelta = new Vector2(ringSize, ringSize);
            _outerRing.color = new Color(ringCol.r, ringCol.g, ringCol.b, ringCol.a * ringAlphaScale);

            float dotSize = Mathf.Lerp(3.6f, 4.4f, adsBlend);
            _centerDot.rectTransform.sizeDelta = new Vector2(dotSize, dotSize);
            _centerDot.color = coreCol;

            float hairAlpha = coreCol.a * (1f - adsBlend * 0.92f);
            Color hairCol = new Color(coreCol.r, coreCol.g, coreCol.b, hairAlpha);
            float dist = Mathf.Lerp(Mathf.Max(3.5f, BaseHairGap + bloom * 0.55f), 2.2f, adsBlend) + HairLength * 0.5f;
            _cardinalTicks[0].anchoredPosition = new Vector2(0f, dist);
            _cardinalTicks[1].anchoredPosition = new Vector2(dist, 0f);
            _cardinalTicks[2].anchoredPosition = new Vector2(0f, -dist);
            _cardinalTicks[3].anchoredPosition = new Vector2(-dist, 0f);
            for (int i = 0; i < 4; i++)
            {
                _cardinalImages[i].color = hairCol;
                _cardinalImages[i].enabled = hairAlpha > 0.02f;
            }
        }

        bool CheckEnemyUnderReticle(SimBridge sim, Camera worldCamera, MapLayoutData layout)
        {
            if (sim == null || worldCamera == null) return false;
            IReadOnlyList<KidSnapshot> kids = sim.Kids;
            int count = kids.Count;
            if (count == 0) return false;

            int localTeam = sim.LocalPlayerTeam;
            float maxDist = Mathf.Max(sim.TiradorMaxRange > 1f ? sim.TiradorMaxRange : 30f, 34f);
            float halfTanFov = Mathf.Max(0.15f, Mathf.Tan(worldCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float aspect = Mathf.Max(0.5f, worldCamera.aspect);
            Vector3 camPos = worldCamera.transform.position;
            KidSnapshot? localOpt = sim.LocalKid;
            SVector2 localSimPos = localOpt.HasValue
                ? localOpt.Value.Position
                : new SVector2(camPos.x, -camPos.z);
            IReadOnlyList<WallSnapshot> walls = sim.Walls;

            for (int i = 0; i < count; i++)
            {
                KidSnapshot k = kids[i];
                if (k.Team == localTeam || k.Tagged) continue;
                if (k.Invisible && k.RevealedOutlineLeft <= 0.01f) continue;

                float yChest = (k.Crouching ? 0.68f : 0.92f) + k.AirY;
                Vector3 chestWorld = KalyeahResources.World(k.Position, yChest);
                Vector3 vp = worldCamera.WorldToViewportPoint(chestWorld);
                if (vp.z <= 0.5f || vp.z > maxDist) continue;

                float halfH = Mathf.Clamp(0.74f / (vp.z * halfTanFov), 0.016f, 0.085f);
                float halfW = Mathf.Clamp((0.52f / (vp.z * halfTanFov)) / aspect, 0.012f, 0.060f);
                float dx = Mathf.Abs(vp.x - ReticleViewport.x);
                float dy = Mathf.Abs(vp.y - ReticleViewport.y);
                if (dx <= halfW && dy <= halfH)
                {
                    // Always gate reticle highlight behind solid LOS (even if enemy is outlined, stones cannot pierce walls)
                    if (IsReticleShotBlocked(camPos, chestWorld, localSimPos, k.Position, yChest, layout, walls))
                        continue;
                    return true;
                }
            }
            return false;
        }

        static bool IsReticleShotBlocked(
            Vector3 camPos,
            Vector3 targetChestWorld,
            SVector2 originSim,
            SVector2 targetSim,
            float targetY,
            MapLayoutData layout,
            IReadOnlyList<WallSnapshot> walls)
        {
            // 1. Unity Physics collider raycast (ignores floor planes)
            Vector3 seg = targetChestWorld - camPos;
            float len = seg.magnitude;
            if (len > 1.2f)
            {
                Vector3 dir = seg / len;
                if (Physics.Raycast(camPos + dir * 0.5f, dir, out RaycastHit hit, len - 1.0f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.normal.y < 0.85f)
                        return true;
                }
            }

            // 2. Live destructible walls (Bahay-Bahayan, Tinik Wall) in sim XZ
            if (walls != null && walls.Count > 0)
            {
                for (int w = 0; w < walls.Count; w++)
                {
                    WallSnapshot wall = walls[w];
                    SVector2 a = wall.Center - wall.Along * wall.HalfWidth;
                    SVector2 b = wall.Center + wall.Along * wall.HalfWidth;
                    if (SegmentsIntersect2D(originSim, targetSim, a, b))
                        return true;
                }
            }

            // 3. Analytic MapLayoutData oriented building/cover OBB check (works headlessly in EditMode too)
            if (layout != null && layout.Obstacles != null)
            {
                int parcels = layout.ParcelCount;
                bool oriented = layout.HasOrientedParcels;
                float originY = Mathf.Max(0.85f, camPos.y * 0.65f);
                for (int p = 0; p < parcels; p++)
                {
                    GroundRect r = layout.ParcelRect(p);
                    string prop = layout.ParcelProp(p);
                    float obsHeight = (prop == "cover_wall" || prop == "bench")
                        ? Mathf.Max(0.9f, layout.CoverHeight)
                        : 4.8f;
                    if (originY > obsHeight && targetY > obsHeight)
                        continue;

                    float yaw = oriented ? layout.ParcelYawRad(p) : 0f;
                    if (SegmentIntersectsObb2D(originSim, targetSim, r, yaw))
                        return true;
                }
            }

            return false;
        }

        static bool SegmentIntersectsObb2D(SVector2 fromSim, SVector2 toSim, GroundRect rect, float yawRad)
        {
            SVector2 center = rect.Center;
            float halfW = rect.W * 0.5f;
            float halfH = rect.H * 0.5f;
            if (halfW <= 0.01f || halfH <= 0.01f) return false;

            float cos = MathF.Cos(yawRad);
            float sin = MathF.Sin(yawRad);
            float fx = fromSim.X - center.X;
            float fz = fromSim.Y - center.Y;
            float lsx = fx * cos - fz * sin;
            float lsz = fx * sin + fz * cos;

            if (MathF.Abs(lsx) <= halfW && MathF.Abs(lsz) <= halfH)
                return false;

            float tx = toSim.X - center.X;
            float tz = toSim.Y - center.Y;
            float lex = tx * cos - tz * sin;
            float lez = tx * sin + tz * cos;

            float dx = lex - lsx;
            float dz = lez - lsz;
            float tMin = 0.02f;
            float tMax = 0.96f;
            if (!ClipSlab2D(lsx, dx, -halfW, halfW, ref tMin, ref tMax)) return false;
            if (!ClipSlab2D(lsz, dz, -halfH, halfH, ref tMin, ref tMax)) return false;
            return tMax >= tMin;
        }

        static bool ClipSlab2D(float origin, float dir, float minBound, float maxBound, ref float tMin, ref float tMax)
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

        static bool SegmentsIntersect2D(SVector2 a, SVector2 b, SVector2 c, SVector2 d)
        {
            float abx = b.X - a.X;
            float aby = b.Y - a.Y;
            float cdx = d.X - c.X;
            float cdy = d.Y - c.Y;
            float denom = abx * cdy - aby * cdx;
            if (MathF.Abs(denom) < 1e-6f) return false;
            float acx = c.X - a.X;
            float acy = c.Y - a.Y;
            float t = (acx * cdy - acy * cdx) / denom;
            float u = (acx * aby - acy * abx) / denom;
            return t >= 0.02f && t <= 0.98f && u >= 0f && u <= 1f;
        }

        void StepHitConfirm(float dt)
        {
            if (_hitTimer <= 0f)
            {
                if (_hitTickImages[0].enabled)
                {
                    for (int i = 0; i < 4; i++) _hitTickImages[i].enabled = false;
                    _tagStingRing.enabled = false;
                    _hitIsHeadshot = false;
                    _hitIsTag = false;
                }
            }
            else
            {
                _hitTimer = Mathf.Max(0f, _hitTimer - Mathf.Max(0f, dt));
                float progress = 1f - Mathf.Clamp01(_hitTimer / Mathf.Max(0.001f, _hitDuration));
                float alpha = Mathf.Clamp01(1f - progress * progress);
                bool big = _hitIsTag || _hitIsHeadshot;
                float spread = _reduceMotion ? 0f : Mathf.Sin(progress * Mathf.PI) * (_hitIsTag ? 5.2f : (_hitIsHeadshot ? 3.4f : 2.0f));
                float radial = (_hitIsTag ? 12.2f : (_hitIsHeadshot ? 10.2f : 8.2f)) + spread;
                Vector2 tickSize = _hitIsTag
                    ? new Vector2(3.4f, 13.0f)
                    : (_hitIsHeadshot ? new Vector2(3.2f, 11.5f) : new Vector2(2.4f, 8.8f));

                // Option A3 G8: warm Coral-Gold (#FF9B6A) hit-confirm X; luminous Coral-Gold-White + sting ring on headshot/tag
                Color baseHit = big ? HeadshotCoralBright : BodyHitCoralGold;
                var col = new Color(baseHit.r, baseHit.g, baseHit.b, alpha);

                const float invSqrt2 = 0.70710678f;
                _hitTicks[0].anchoredPosition = new Vector2(-invSqrt2 * radial, invSqrt2 * radial);
                _hitTicks[1].anchoredPosition = new Vector2(-invSqrt2 * radial, -invSqrt2 * radial);
                _hitTicks[2].anchoredPosition = new Vector2(invSqrt2 * radial, -invSqrt2 * radial);
                _hitTicks[3].anchoredPosition = new Vector2(invSqrt2 * radial, invSqrt2 * radial);

                for (int i = 0; i < 4; i++)
                {
                    _hitTicks[i].sizeDelta = tickSize;
                    _hitTickImages[i].color = col;
                    _hitTickImages[i].enabled = alpha > 0.01f;
                }

                if (_hitIsTag || _hitIsHeadshot)
                {
                    _tagStingRing.enabled = alpha > 0.01f;
                    float ringExpand = _hitIsTag ? 20f : 10f;
                    float stingSize = BaseRingSize + 8f + (_reduceMotion ? 4f : progress * ringExpand);
                    _tagStingRing.rectTransform.sizeDelta = new Vector2(stingSize, stingSize);
                    _tagStingRing.color = new Color(TagStingCoralGold.r, TagStingCoralGold.g, TagStingCoralGold.b, alpha * (_hitIsTag ? 0.84f : 0.60f));
                }
                else
                {
                    _tagStingRing.enabled = false;
                }
            }

            if (_tagStampRoot != null)
            {
                if (_tagStampTimer > 0f)
                {
                    _tagStampTimer = Mathf.Max(0f, _tagStampTimer - Mathf.Max(0f, dt));
                    float p = 1f - Mathf.Clamp01(_tagStampTimer / TagStingSeconds);
                    float a = p < 0.7f ? 1f : Mathf.Clamp01((1f - p) / 0.3f);
                    bool active = _tagStampTimer > 0.001f && a > 0.01f;
                    if (_tagStampRoot.gameObject.activeSelf != active)
                        _tagStampRoot.gameObject.SetActive(active);
                    if (active)
                    {
                        _tagStampGroup.alpha = a;
                        float pop = _reduceMotion ? 1f : (1f + Mathf.Sin(Mathf.Clamp01(p * 4f) * Mathf.PI) * 0.14f);
                        _tagStampRoot.localScale = new Vector3(pop, pop, 1f);
                        float yOff = (_chargeBarRoot != null && _chargeBarRoot.gameObject.activeSelf) ? -48f : -34f;
                        _tagStampRoot.anchoredPosition = new Vector2(0f, yOff);
                    }
                }
                else if (_tagStampRoot.gameObject.activeSelf)
                {
                    _tagStampRoot.gameObject.SetActive(false);
                }
            }
        }

        void StepDamageArcs(float dt)
        {
            Color baseDanger = _colorblindMode != 0
                ? new Color(1f, 0.56f, 0.12f, 1f)
                : new Color(0.96f, 0.18f, 0.16f, 1f);

            for (int i = 0; i < DamageArcPoolSize; i++)
            {
                DamageArcSlot s = _damageArcs[i];
                if (s.TimeLeft <= 0f)
                {
                    if (s.Arc.enabled) s.Arc.enabled = false;
                    continue;
                }
                s.TimeLeft = Mathf.Max(0f, s.TimeLeft - Mathf.Max(0f, dt));
                float t = Mathf.Clamp01(s.TimeLeft / Mathf.Max(0.01f, s.Duration));
                float peak = s.Heavy ? 0.88f : 0.62f;
                if (_reduceMotion) peak *= 0.75f;
                s.Arc.color = new Color(baseDanger.r, baseDanger.g, baseDanger.b, peak * t);
                s.Arc.rectTransform.sizeDelta = s.Heavy ? new Vector2(148f, 34f) : new Vector2(120f, 26f);
                s.Arc.enabled = s.TimeLeft > 0.001f;
            }
        }

        void OnSimWeaponCast(int casterId, string weaponId)
        {
            if (_boundSim == null || string.IsNullOrEmpty(weaponId)) return;
            _lastCasterByWeapon[weaponId] = casterId;
            if (casterId == _boundSim.LocalPlayerId)
            {
                KickRecoil(weaponId == "tirador" ? 0.45f : 0.85f);
            }
            else
            {
                KidSnapshot? caster = _boundSim.Find(casterId);
                if (caster.HasValue && caster.Value.Team != _boundSim.LocalPlayerTeam)
                    _lastEnemyCasterId = casterId;
            }
        }

        void OnSimConeTriggered(int ownerId, string weaponId, SVector2 origin, SVector2 dir, bool isWindup)
        {
            if (!isWindup)
            {
                _coneCasterId = ownerId;
                _coneWeaponId = weaponId ?? "";
            }
        }

        void OnSimHitConfirmed(HitConfirmInfo info)
        {
            if (_boundSim == null || info.AttackerId != _boundSim.LocalPlayerId || info.TargetId == info.AttackerId)
                return;

            _lastHitConfirmTick = _boundSim.Tick;
            FlashHit(isHeadshot: info.IsHeadshot, isTag: info.IsTag);
            Audio?.PlayHitConfirm(info.IsHeadshot, info.IsTag);
        }

        void OnSimWeaponHit(int targetId, string weaponId)
        {
            if (_boundSim == null || targetId == _boundSim.LocalPlayerId) return;
            if (_lastHitConfirmTick == _boundSim.Tick) return;
            KidSnapshot? target = _boundSim.Find(targetId);
            if (!target.HasValue || target.Value.Team == _boundSim.LocalPlayerTeam) return;

            if (IsLocalWeaponHit(target.Value, weaponId))
            {
                FlashHit(isHeadshot: false, isTag: false);
                Audio?.PlayHitConfirm(isHeadshot: false, isTag: false);
            }
        }

        bool IsLocalWeaponHit(KidSnapshot target, string weaponId)
        {
            int localId = _boundSim.LocalPlayerId;
            if (_coneCasterId >= 0 && string.Equals(_coneWeaponId, weaponId, StringComparison.Ordinal))
                return _coneCasterId == localId;

            bool matchedAny = false;
            IReadOnlyList<WorldEffectSnapshot> effects = _boundSim.WorldEffects;
            for (int i = 0; i < effects.Count; i++)
            {
                WorldEffectSnapshot ef = effects[i];
                if (!string.Equals(ef.WeaponId, weaponId, StringComparison.Ordinal)) continue;
                float reach = Mathf.Max(ef.Radius, 0.5f) + _boundSim.PlayerRadius + 1.6f;
                if (SVector2.DistanceSquared(ef.Position, target.Position) <= reach * reach)
                {
                    matchedAny = true;
                    if (ef.OwnerId == localId) return true;
                }
            }
            if (matchedAny) return false;
            return _lastCasterByWeapon.TryGetValue(weaponId ?? "", out int lastCaster) && lastCaster == localId;
        }

        void OnSimTagged(int victimId, int byId)
        {
            if (_boundSim == null) return;
            int localId = _boundSim.LocalPlayerId;
            if (byId == localId && victimId != localId)
            {
                if (_lastHitConfirmTick != _boundSim.Tick)
                {
                    FlashHit(isHeadshot: false, isTag: true);
                    Audio?.PlayHitConfirm(isHeadshot: false, isTag: true);
                }
                return;
            }
            if (victimId == localId)
            {
                KidSnapshot? me = _boundSim.LocalKid;
                KidSnapshot? attacker = byId > 0 ? _boundSim.Find(byId) : null;
                if (me.HasValue && attacker.HasValue)
                    ShowIncomingDamage(attacker.Value.Position, me.Value.Position, _lastCameraYawDeg, heavy: true);
                else
                    ShowIncomingDamageAngle(180f, heavy: true);
            }
        }

        void OnSimPlayerDamaged(int victimId, int amount)
        {
            if (_boundSim == null || victimId != _boundSim.LocalPlayerId) return;
            KidSnapshot? me = _boundSim.LocalKid;
            if (!me.HasValue) return;

            bool heavy = amount >= Mathf.Max(1, _boundSim.MaxHp) * 0.30f;
            SVector2 myPos = me.Value.Position;

            // Locate nearest enemy effect or enemy kid responsible for the hit
            IReadOnlyList<WorldEffectSnapshot> effects = _boundSim.WorldEffects;
            float bestDistSq = float.MaxValue;
            int attackerId = -1;
            SVector2 sourcePos = myPos;

            for (int i = 0; i < effects.Count; i++)
            {
                WorldEffectSnapshot ef = effects[i];
                if (ef.Team == _boundSim.LocalPlayerTeam) continue;
                float dSq = SVector2.DistanceSquared(ef.Position, myPos);
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    attackerId = ef.OwnerId;
                    sourcePos = ef.Position;
                }
            }

            if (attackerId > 0)
            {
                KidSnapshot? ownerKid = _boundSim.Find(attackerId);
                if (ownerKid.HasValue) sourcePos = ownerKid.Value.Position;
                ShowIncomingDamage(sourcePos, myPos, _lastCameraYawDeg, heavy);
                return;
            }

            if (_lastEnemyCasterId > 0)
            {
                KidSnapshot? lastEnemy = _boundSim.Find(_lastEnemyCasterId);
                if (lastEnemy.HasValue)
                {
                    ShowIncomingDamage(lastEnemy.Value.Position, myPos, _lastCameraYawDeg, heavy);
                    return;
                }
            }

            IReadOnlyList<KidSnapshot> kids = _boundSim.Kids;
            for (int i = 0; i < kids.Count; i++)
            {
                if (kids[i].Team == _boundSim.LocalPlayerTeam || kids[i].Tagged) continue;
                float dSq = SVector2.DistanceSquared(kids[i].Position, myPos);
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    sourcePos = kids[i].Position;
                }
            }
            if (bestDistSq < float.MaxValue)
                ShowIncomingDamage(sourcePos, myPos, _lastCameraYawDeg, heavy);
        }

        static string[] BuildMeterStrings(string prefix = "")
        {
            var arr = new string[65];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = prefix + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "m";
            return arr;
        }

        /// <summary>0 = the shot reaches now, 1 = only a charged shot reaches, 2 = out of reach.</summary>
        public static int ClassifyReach(float distance, float reachNow, float reachFull)
            => distance <= reachNow ? 0 : (distance <= reachFull ? 1 : 2);

        /// <summary>
        /// Measures the ground distance to what the crosshair is on and compares it with the slingshot's reach: a tap
        /// (Tirador min range, or the primary weapon's range without spec kits), up to a full charge (Tirador max range).
        /// </summary>
        void UpdateReach(SimBridge sim, KidSnapshot? localOpt, bool showReticle, Camera worldCamera, MapLayoutData layout)
        {
            ReachState = -1;
            if (_reachText == null) return;
            if (showReticle && sim != null && worldCamera != null && localOpt.HasValue)
            {
                KidSnapshot me = localOpt.Value;
                AimSolution probe = AimSolver.Solve(worldCamera, me.Position, me.Facing, ReachProbeMeters, layout);
                Vector3 hit = probe.CameraRayPoint;
                float dist = (GodotSpace.Ground(new System.Numerics.Vector3(hit.x, hit.y, hit.z)) - me.Position).Length();
                if (dist < ReachProbeMeters - 1f)
                {
                    bool spec = sim.SpecKitsActive;
                    float tap = spec ? sim.TiradorMinRange : (sim.PrimaryWeaponInfo.HasValue ? sim.PrimaryWeaponInfo.Value.MaxRange : sim.TiradorMinRange);
                    float full = spec ? sim.TiradorMaxRange : tap;
                    float now = spec && me.TiradorCharge01 >= 0f && me.TiradorCurrentRange > 0.5f ? me.TiradorCurrentRange : tap;
                    ReachState = ClassifyReach(dist, now, full);
                    int m = Mathf.Clamp(Mathf.RoundToInt(dist), 0, MeterStrings.Length - 1);
                    HudIconCache.SetTextIfChanged(_reachText, ReachState == 1 ? HoldMeterStrings[m] : MeterStrings[m]);
                    _reachText.color = ReachState == 0 ? MenuArt.PaperWhite : (ReachState == 1 ? ReachNeedsChargeColor : ReachTooFarColor);
                }
            }
            bool show = ReachState >= 0;
            if (_reachText.gameObject.activeSelf != show) _reachText.gameObject.SetActive(show);
        }

        static Sprite DamageArcSprite()
        {
            if (_damageArcSprite != null) return _damageArcSprite;
            const int w = 128;
            const int h = 36;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "KalyeahDamageArc",
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[w * h];
            const float rCenter = 160f;
            float cx = w * 0.5f;
            float cy = -(rCenter - h * 0.72f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float radial = Mathf.Clamp01(1f - Mathf.Abs(dist - rCenter) / 5.5f);
                    float horiz = Mathf.Clamp01(1f - Mathf.Abs((x + 0.5f - cx) / (w * 0.46f)));
                    // Sharp center tip pointing outward along bearing
                    float tip = y > h * 0.58f ? Mathf.Clamp01(1f - Mathf.Abs(dx) / 7f) * Mathf.Clamp01((h - y) / (h * 0.36f)) : 0f;
                    float a = Mathf.Clamp01(Mathf.Max(radial * Mathf.Pow(horiz, 0.65f), tip * 0.85f));
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _damageArcSprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
            _damageArcSprite.name = "KalyeahDamageArc";
            return _damageArcSprite;
        }
    }
}
