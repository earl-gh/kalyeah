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
        public const float HitConfirmSeconds = 0.075f;
        public const float TagStingSeconds = 0.28f;
        public const float DamageArcSeconds = 0.65f;

        const int DamageArcPoolSize = 4;
        const float BaseRingSize = 26f;
        const float BaseHairGap = 6f;
        const float HairLength = 6f;

        static readonly Color IdleColor = new Color(1f, 1f, 1f, 0.84f);
        static readonly Color IdleRingColor = new Color(1f, 1f, 1f, 0.46f);
        static readonly Color AimHeldColor = new Color(1f, 0.92f, 0.62f, 0.96f);
        static readonly Color CancelAimColor = new Color(1f, 0.30f, 0.28f, 0.96f);
        static readonly Color HitCoralColor = new Color(0.98f, 0.36f, 0.26f, 0.98f);
        static readonly Color HitColorblindColor = new Color(1f, 0.68f, 0.16f, 0.98f);
        static readonly Color TagStingGold = new Color(1f, 0.86f, 0.28f, 1f);

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

        SimBridge _boundSim;
        int _coneCasterId = -1;
        string _coneWeaponId = "";
        int _lastEnemyCasterId = -1;
        float _lastCameraYawDeg;

        float _bloom;
        float _recoil;
        float _hitTimer;
        float _hitDuration = HitConfirmSeconds;
        bool _hitIsTag;
        bool _reduceMotion;
        int _colorblindMode;
        bool _updatingLayout;

        public RectTransform Root => _root;
        public RectTransform ReticleRect => _reticleCenter;
        public bool IsShowingHitConfirm => _hitTimer > 0.0001f;
        public bool IsShowingTagSting => _hitTimer > 0.0001f && _hitIsTag;

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

            _tagStingRing = Ui.MakeImage("TagStingRing", _reticleCenter, new Color(1f, 0.86f, 0.28f, 0f), round: false, raycast: false);
            _tagStingRing.sprite = HudIconCache.RingSprite();
            _tagStingRing.rectTransform.sizeDelta = new Vector2(BaseRingSize + 10f, BaseRingSize + 10f);
            _tagStingRing.enabled = false;

            // 4 cardinal hairlines (0=Up, 1=Right, 2=Down, 3=Left)
            for (int i = 0; i < 4; i++)
            {
                Image tick = CreateStrokedTick($"Hair_{i}", _reticleCenter, IdleColor, new Vector2(2f, HairLength));
                tick.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -i * 90f);
                _cardinalTicks[i] = tick.rectTransform;
                _cardinalImages[i] = tick;
            }

            _centerDot = Ui.MakeImage("CenterDot", _reticleCenter, IdleColor, round: true, raycast: false);
            _centerDot.rectTransform.sizeDelta = new Vector2(3.6f, 3.6f);
            var dotShadow = _centerDot.gameObject.AddComponent<Outline>();
            dotShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            dotShadow.effectDistance = new Vector2(1f, -1f);

            // 4 diagonal X-ticks for 75 ms hit confirm & heavy tag sting (45, 135, 225, 315 deg)
            for (int i = 0; i < 4; i++)
            {
                Image xTick = CreateStrokedTick($"HitX_{i}", _reticleCenter, HitCoralColor, new Vector2(2.4f, 8.5f));
                xTick.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f + i * 90f);
                xTick.enabled = false;
                _hitTicks[i] = xTick.rectTransform;
                _hitTickImages[i] = xTick;
            }

            // Compact primary fire reload pip tucked 18 px below reticle (hidden at 0 cooldown)
            _primaryPipRoot = Ui.Panel("PrimaryCooldownPip", _reticleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -19f), new Vector2(22f, 4.5f));
            Image pipBg = Ui.MakeImage("Track", _primaryPipRoot, new Color(0.04f, 0.07f, 0.14f, 0.72f), round: false, raycast: false);
            pipBg.rectTransform.anchorMin = Vector2.zero;
            pipBg.rectTransform.anchorMax = Vector2.one;
            pipBg.rectTransform.sizeDelta = Vector2.zero;

            _primaryPipFill = Ui.MakeImage("Fill", _primaryPipRoot, new Color(1f, 0.96f, 0.82f, 0.92f), round: false, raycast: false);
            _primaryPipFill.type = Image.Type.Filled;
            _primaryPipFill.fillMethod = Image.FillMethod.Horizontal;
            _primaryPipFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _primaryPipFill.fillAmount = 1f;
            _primaryPipFill.rectTransform.anchorMin = Vector2.zero;
            _primaryPipFill.rectTransform.anchorMax = Vector2.one;
            _primaryPipFill.rectTransform.offsetMin = new Vector2(0.8f, 0.8f);
            _primaryPipFill.rectTransform.offsetMax = new Vector2(-0.8f, -0.8f);
            _primaryPipRoot.gameObject.SetActive(false);

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
            _boundSim.Tagged += OnSimTagged;
            _boundSim.PlayerDamaged += OnSimPlayerDamaged;
        }

        public void Unbind()
        {
            if (_boundSim == null) return;
            _boundSim.WeaponCast -= OnSimWeaponCast;
            _boundSim.ConeTriggered -= OnSimConeTriggered;
            _boundSim.WeaponHit -= OnSimWeaponHit;
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

        /// <summary>Triggers the crisp 75 ms hit-confirm X-tick (or heavy golden/coral tag sting when isTag is true).</summary>
        public void FlashHit(bool isTag = false)
        {
            if (!isTag && _hitIsTag && _hitTimer > HitConfirmSeconds)
                return; // never cut an active tag sting short with a minor pellet tick

            _hitIsTag = isTag;
            _hitDuration = isTag ? TagStingSeconds : HitConfirmSeconds;
            _hitTimer = _hitDuration;
            if (isTag) Haptics.HeavyImpact();
            else Haptics.HitConfirm();
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
        {
            _lastCameraYawDeg = cameraYawDegrees;
            KidSnapshot? localOpt = sim?.LocalKid;

            bool showReticle = localOpt == null || (!localOpt.Value.Tagged && (sim == null || sim.Phase == MatchPhaseKind.Playing || sim.Phase == MatchPhaseKind.Intro));
            if (_reticleCenter != null && _reticleCenter.gameObject.activeSelf != showReticle)
                _reticleCenter.gameObject.SetActive(showReticle);

            bool sprinting = localOpt.HasValue && (localOpt.Value.Sprinting || localOpt.Value.Dashing);
            float targetBloom = Mathf.Clamp((sprinting ? 3.2f : 0f) + _recoil * 6.5f - (isAimingAbility ? 1.6f : 0f), -1.6f, 12f);

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

            Color coreCol = isCancelHovered ? CancelAimColor : (isAimingAbility ? AimHeldColor : IdleColor);
            Color ringCol = isCancelHovered ? CancelAimColor : (isAimingAbility ? AimHeldColor : IdleRingColor);
            ApplyBloomPositions(_bloom, coreCol, ringCol);

            // Primary cooldown pip
            if (_primaryPipRoot != null)
            {
                float pCd = localOpt.HasValue ? localOpt.Value.PrimaryCooldown : 0f;
                float pMax = localOpt.HasValue && localOpt.Value.PrimaryMaxCooldown > 0.01f
                    ? localOpt.Value.PrimaryMaxCooldown
                    : (sim != null && sim.PrimaryWeaponInfo.HasValue ? Mathf.Max(0.1f, sim.PrimaryWeaponInfo.Value.Cooldown) : 0.48f);
                bool cooling = pCd > 0.02f;
                if (_primaryPipRoot.gameObject.activeSelf != cooling)
                    _primaryPipRoot.gameObject.SetActive(cooling);
                if (cooling)
                    _primaryPipFill.fillAmount = Mathf.Clamp01(1f - pCd / pMax);
            }

            // 75 ms hit-confirm X-tick + heavy tag sting
            StepHitConfirm(dt);

            // Peripheral incoming-damage directional arcs
            StepDamageArcs(dt);
        }

        void ApplyBloomPositions(float bloom, Color coreCol, Color ringCol)
        {
            float ringSize = Mathf.Max(18f, BaseRingSize + bloom * 1.3f);
            _outerRing.rectTransform.sizeDelta = new Vector2(ringSize, ringSize);
            _outerRing.color = ringCol;
            _centerDot.color = coreCol;

            float dist = Mathf.Max(3.5f, BaseHairGap + bloom * 0.55f) + HairLength * 0.5f;
            _cardinalTicks[0].anchoredPosition = new Vector2(0f, dist);
            _cardinalTicks[1].anchoredPosition = new Vector2(dist, 0f);
            _cardinalTicks[2].anchoredPosition = new Vector2(0f, -dist);
            _cardinalTicks[3].anchoredPosition = new Vector2(-dist, 0f);
            for (int i = 0; i < 4; i++)
                _cardinalImages[i].color = coreCol;
        }

        void StepHitConfirm(float dt)
        {
            if (_hitTimer <= 0f)
            {
                if (_hitTickImages[0].enabled)
                {
                    for (int i = 0; i < 4; i++) _hitTickImages[i].enabled = false;
                    _tagStingRing.enabled = false;
                }
                return;
            }

            _hitTimer = Mathf.Max(0f, _hitTimer - Mathf.Max(0f, dt));
            float progress = 1f - Mathf.Clamp01(_hitTimer / Mathf.Max(0.001f, _hitDuration));
            float alpha = Mathf.Clamp01(1f - progress * progress);
            float spread = _reduceMotion ? 0f : Mathf.Sin(progress * Mathf.PI) * (_hitIsTag ? 4.5f : 2.2f);
            float radial = (_hitIsTag ? 11.5f : 8.5f) + spread;
            Vector2 tickSize = _hitIsTag ? new Vector2(3.2f, 12.5f) : new Vector2(2.4f, 8.5f);

            Color baseHit = _hitIsTag
                ? TagStingGold
                : (_colorblindMode != 0 ? HitColorblindColor : HitCoralColor);
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

            if (_hitIsTag)
            {
                _tagStingRing.enabled = alpha > 0.01f;
                float stingSize = BaseRingSize + 8f + (_reduceMotion ? 4f : progress * 18f);
                _tagStingRing.rectTransform.sizeDelta = new Vector2(stingSize, stingSize);
                _tagStingRing.color = new Color(TagStingGold.r, TagStingGold.g, TagStingGold.b, alpha * 0.72f);
            }
            else
            {
                _tagStingRing.enabled = false;
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

        void OnSimWeaponHit(int targetId, string weaponId)
        {
            if (_boundSim == null || targetId == _boundSim.LocalPlayerId) return;
            KidSnapshot? target = _boundSim.Find(targetId);
            if (!target.HasValue || target.Value.Team == _boundSim.LocalPlayerTeam) return;

            if (IsLocalWeaponHit(target.Value, weaponId))
                FlashHit(isTag: false);
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
                FlashHit(isTag: true);
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
