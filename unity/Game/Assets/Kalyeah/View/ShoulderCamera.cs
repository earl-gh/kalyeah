using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SVector2 = System.Numerics.Vector2;
using SVector3 = System.Numerics.Vector3;

namespace Kalyeah.Game
{
    /// <summary>Per-skill camera framing mode during ability drag-aim (docs/KITS.md L44-51).</summary>
    public enum SkillAimCameraMode
    {
        Normal = 0,
        Ads = 1,
        ZoomOut = 2,
        MapCast = 3,
    }

    /// <summary>
    /// AAA third-person right-shoulder camera rig with orthographic top-down follow fallback
    /// (docs/GDD.md "Shoulder view"; scripts/view/arena_camera.gd &amp; camera_rules.gd).
    /// Owns whisker spring-arm wall pull-in (&lt;=40 ms pull-in, 150 ms critically-damped ease-out across both
    /// Physics.SphereCast and analytic MapLayoutData bounds), &lt;0.75 m kid-proximity flag, building Occluder
    /// renderer fade, lateral-orbit auto-follow, sprint/dash FOV kick, skill-aiming ADS/ZoomOut transitions,
    /// and &gt;21.5:9 horizontal FOV fairness cap.
    /// </summary>
    public sealed class ShoulderCamera
    {
        const float Far = 450f;
        const float BaseHeight = 720f;
        const float Standoff = 150f;
        const float OrthoNearMargin = 70f;
        const float OrthoFarMargin = 100f;
        const float FollowClose = 3f;
        const float SnapDistance = 4f;
        const float CrouchPivotOffset = -0.42f;
        const float AirPivotShare = 0.65f;
        const float PosturePivotLerp = 14f;
        const float HardSurfacePad = 0.05f;

        /// <summary>Maximum competitive aspect ratio (21.5:9) before horizontal FOV is capped.</summary>
        public const float MaxFairAspect = 21.5f / 9f;
        public const float DefaultTooCloseDistance = 0.75f;

        static readonly HashSet<string> NonOccluderKits = new HashSet<string>(StringComparer.Ordinal)
        {
            BarangayPlan.PostBlue,
            BarangayPlan.PostRed,
            BarangayPlan.PowerupStand,
            "brgy_chb_wall",
            "brgy_mango_tree",
            "brgy_coconut_palm",
            "brgy_banana_clump",
        };

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColorId = Shader.PropertyToID("_Color");

        sealed class OccluderEntry
        {
            public GameObject Root;
            public Bounds Bounds;
            public Renderer[] Renderers;
            public ShadowCastingMode[] OriginalShadows;
            public Color[] BaseColors;
            public float Alpha = 1f;
            public bool Occluded;
        }

        public readonly CameraRulesData Rules;

        /// <summary>Degrees: 0 looks down the sim's -Z. Pitch is down-positive.</summary>
        public float Yaw, Pitch;

        [NonSerialized] public MapLayoutData Layout;
        public int CollisionMask = Physics.DefaultRaycastLayers;
        public Transform IgnoreCollisionRoot;

        public bool ThirdPerson = true;
        public bool Follow = true;
        public bool ReduceMotion;

        public float FollowViewSize = 20f;
        public float FollowLag = 0.15f;
        public float FollowEdge = 4f;
        public float FollowPitchDegrees = 45f;
        public float FollowYawOffsetDegrees = 90f;

        public float ShoulderClearReach = 2.2f;
        public float AimReach = 25f;
        public float ShoulderBoundsEdge = 4f;

        public float WhiskerRadius = 0.24f;
        public float WhiskerWallPad = 0.48f;
        public float ArenaWallPad = 0.60f;
        public float PullInSmoothTime = 0.018f;
        public float EaseOutSmoothTime = 0.15f;
        public float TooCloseDistance = DefaultTooCloseDistance;

        public float AutoFollowSmoothTime = 0.22f;
        public float AutoFollowMaxSpeed = 160f;
        public float AutoFollowLateralGain = 1f;
        public float ManualOverrideCooldown = 1.1f;
        public float ManualOverrideBlendTime = 0.45f;
        public float PivotSmoothTime = 0.007f;
        public float FacingSmoothTime = 0.045f;

        public float SprintFovKick = 4.5f;
        /// <summary>Aiming down sight: the camera moves in along its view to `ads_distance` and narrows to `ads_fov`.</summary>
        public bool AimingDown;
        /// <summary>Active skill aiming camera override (Normal, Ads, ZoomOut, MapCast per docs/KITS.md).</summary>
        public SkillAimCameraMode SkillAimMode = SkillAimCameraMode.Normal;
        /// <summary>Additional vertical pivot lift in metres (e.g. Inggo's Tiyakad stilts seeing over cover).</summary>
        public float ExtraPivotHeight;
        public float AdsLerp = 14f;
        public float ZoomOutBackMeters = 2.8f;
        public float ZoomOutPitchAddDeg = 18f;
        public float ZoomOutMaxPitchDeg = 54f;
        public float ZoomOutFovAddDeg = 3.5f;
        public float ZoomOutEnterSmoothTime = 0.14f;
        public float ZoomOutExitSmoothTime = 0.085f;
        public float MapCastBackMeters = 15.0f;
        public float MapCastPitchDeg = 70f;
        public float MapCastFovAddDeg = 5.0f;
        public float MapCastEnterSmoothTime = 0.16f;
        public float MapCastExitSmoothTime = 0.095f;
        public float SprintFovLerp = 8f;

        public float OccluderOccludedAlpha = 0.18f;
        public float OccluderFadeRate = 8f;

        // Tactile kinetic channels (strictly ReduceMotion-safe, EditMode-inert, zero GC).
        public float RecoilReturnSmoothTime = 0.085f;
        public float MaxRecoilPitchDeg = 6.5f;
        public float MaxRecoilYawDeg = 2.2f;
        public float SlideDutchRollDeg = 2.5f;
        public float SlideRollSmoothTime = 0.085f;
        public float LandDipMaxMeters = 0.14f;
        public float LandDipReturnSmoothTime = 0.11f;
        public float MaxHitStopSeconds = 0.065f;

        readonly RaycastHit[] _sphereHits = new RaycastHit[8];
        readonly List<OccluderEntry> _occluders = new List<OccluderEntry>();
        MaterialPropertyBlock _propBlock;

        int _team;
        SVector2 _pivotGround;
        SVector2 _pivotVelocity;
        SVector2 _followAim;
        bool _pivotReady;
        float _verticalOffset;
        float _armT = 1f;
        float _armVelocity;
        bool _armReady;

        float _manualLookHold;
        float _manualBlend = 1f;
        float _yawVelocity;
        float _smoothedMoveYaw;
        float _moveYawVelocity;
        bool _moveYawReady;

        bool _speedBoosting;
        float _fovBoost;
        float _adsBlend;
        float _zoomOutBlend;
        float _zoomOutVelocity;
        float _mapCastBlend;
        float _mapCastVelocity;
        float _hitShakeTimer;
        float _hitShakeDuration;
        float _hitShakeStrength;
        float _hitShakePhase;

        float _recoilPitchDeg;
        float _recoilPitchVel;
        float _recoilYawDeg;
        float _recoilYawVel;
        bool _sliding;
        float _slideRollSign = 1f;
        float _slideRollDeg;
        float _slideRollVel;
        bool _wasAirborne;
        float _airPeakY;
        float _landDipOffset;
        float _landDipVel;
        float _hitStopLeft;
        int _recoilSerial;
        SimBridge _boundSim;

        /// <summary>0 = hip view, 1 = fully aimed down sight.</summary>
        public float AdsBlend => _adsBlend;
        /// <summary>0 = standard shoulder view, 1 = elevated tactical pull-back view for zone/wall skills.</summary>
        public float ZoomOutBlend => _zoomOutBlend;
        /// <summary>0 = standard shoulder view, 1 = elevated sky-cast view for map-wide skills (Kikay Saranggola).</summary>
        public float MapCastBlend => _mapCastBlend;
        /// <summary>Transient upward recoil pitch offset in degrees (0 when ReduceMotion is enabled).</summary>
        public float CurrentRecoilPitchDeg => ReduceMotion ? 0f : _recoilPitchDeg;
        /// <summary>Transient horizontal recoil yaw jitter in degrees (0 when ReduceMotion is enabled).</summary>
        public float CurrentRecoilYawDeg => ReduceMotion ? 0f : _recoilYawDeg;
        /// <summary>Current slide Dutch roll tilt in degrees (0 when ReduceMotion is enabled).</summary>
        public float CurrentSlideRollDeg => ReduceMotion ? 0f : _slideRollDeg;
        /// <summary>Current jump-landing vertical spring dip in metres (negative on impact, 0 at rest).</summary>
        public float CurrentLandDipOffset => ReduceMotion ? 0f : _landDipOffset;
        /// <summary>True while a view-only local hit-stop window (45-50 ms) is active; never stalls the 30 Hz sim.</summary>
        public bool IsInHitStop => !ReduceMotion && _hitStopLeft > 0f;
        /// <summary>Remaining view-only local hit-stop duration in seconds.</summary>
        public float HitStopTimeLeft => ReduceMotion ? 0f : _hitStopLeft;

        /// <summary>Maps a raw frame delta into a view-only hit-stop scaled delta (never touches Time.timeScale).</summary>
        public float EffectiveViewDeltaTime(float rawDt)
        {
            if (rawDt <= 0f) return 0f;
            return IsInHitStop ? rawDt * 0.06f : rawDt;
        }

        /// <summary>Maps SimBridge's <see cref="AimCameraKind"/> directly onto <see cref="SkillAimMode"/>.</summary>
        public void SetSkillAimMode(AimCameraKind kind)
        {
            SkillAimMode = (SkillAimCameraMode)(int)kind;
        }

        /// <summary>Current 3D distance (metres) from the camera socket to the kid's shoulder pivot.</summary>
        public float CurrentDistance { get; private set; }

        /// <summary>True when wall whisker compression pulls the camera within 0.75 m of the kid.</summary>
        public bool TooCloseToKid => ThirdPerson && CurrentDistance < TooCloseDistance;

        /// <summary>Number of registered building occluders currently faded/hidden between camera and kid.</summary>
        public int OccludedCount { get; private set; }

        public float CurrentFovBoost => ReduceMotion ? 0f : _fovBoost;

        public ShoulderCamera(CameraRulesData rules, int team)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _team = team;
            CurrentDistance = Mathf.Sqrt(rules.ShoulderDistance * rules.ShoulderDistance + rules.ShoulderOffset * rules.ShoulderOffset);
            ResetLook();
        }

        public ShoulderCamera(CameraRulesData rules, int team, DataSource data, MapLayoutData layout = null)
            : this(rules, team)
        {
            Configure(data, layout);
        }

        /// <summary>Loads extended camera_rules.tres fields (third_person, follow, auto-follow springs) without altering CameraRulesData.</summary>
        public void Configure(DataSource data, MapLayoutData layout = null)
        {
            if (layout != null) Layout = layout;
            if (data == null) return;
            Dictionary<string, object> p;
            try { p = data.Props("rules__camera_rules"); }
            catch { return; }

            ThirdPerson = BoolOr(p, "third_person", ThirdPerson);
            Follow = BoolOr(p, "follow", Follow);
            FollowViewSize = FloatOr(p, "follow_view_size", FollowViewSize);
            FollowLag = FloatOr(p, "follow_lag", FollowLag);
            FollowEdge = FloatOr(p, "follow_edge", FollowEdge);
            FollowPitchDegrees = FloatOr(p, "pitch_degrees", FollowPitchDegrees);
            FollowYawOffsetDegrees = FloatOr(p, "yaw_offset_degrees", FollowYawOffsetDegrees);
            ShoulderClearReach = FloatOr(p, "shoulder_clear_reach", ShoulderClearReach);
            AimReach = FloatOr(p, "aim_reach", AimReach);
            ShoulderBoundsEdge = FloatOr(p, "shoulder_bounds_edge", ShoulderBoundsEdge);
            AutoFollowSmoothTime = FloatOr(p, "auto_follow_smooth_time", AutoFollowSmoothTime);
            AutoFollowMaxSpeed = FloatOr(p, "auto_follow_max_speed", AutoFollowMaxSpeed);
            AutoFollowLateralGain = FloatOr(p, "auto_follow_lateral_gain", AutoFollowLateralGain);
            ManualOverrideCooldown = FloatOr(p, "manual_override_cooldown", ManualOverrideCooldown);
            ManualOverrideBlendTime = FloatOr(p, "manual_override_blend_time", ManualOverrideBlendTime);
            PivotSmoothTime = FloatOr(p, "pivot_smooth_time", PivotSmoothTime);
            FacingSmoothTime = FloatOr(p, "facing_smooth_time", FacingSmoothTime);
        }

        public void SetTeam(int team, bool resetLook = true)
        {
            _team = team;
            if (resetLook) ResetLook();
        }

        public void ResetLook()
        {
            Yaw = _team == SimBridge.LocalTeam ? 0f : 180f;
            Pitch = Rules.LookPitchDegrees;
            _manualLookHold = 0f;
            _manualBlend = 1f;
            _yawVelocity = 0f;
            _moveYawVelocity = 0f;
            _moveYawReady = false;
            SkillAimMode = SkillAimCameraMode.Normal;
            _zoomOutBlend = 0f;
            _zoomOutVelocity = 0f;
            _mapCastBlend = 0f;
            _mapCastVelocity = 0f;
            _hitShakeTimer = 0f;
            _hitShakeStrength = 0f;
            _recoilPitchDeg = 0f;
            _recoilPitchVel = 0f;
            _recoilYawDeg = 0f;
            _recoilYawVel = 0f;
            _sliding = false;
            _slideRollSign = 1f;
            _slideRollDeg = 0f;
            _slideRollVel = 0f;
            _wasAirborne = false;
            _airPeakY = 0f;
            _landDipOffset = 0f;
            _landDipVel = 0f;
            _hitStopLeft = 0f;
        }

        /// <summary>Subscribes to SimBridge local combat events for automatic recoil kicks, headshot/tag confirm shakes, and view-only hit-stop.</summary>
        public void Bind(SimBridge sim)
        {
            if (ReferenceEquals(_boundSim, sim)) return;
            Unbind();
            _boundSim = sim;
            if (_boundSim == null) return;
            _boundSim.WeaponCast += OnSimWeaponCast;
            _boundSim.HitConfirmed += OnSimHitConfirmed;
            _boundSim.UltimateCast += OnSimUltimateCast;
        }

        public void Unbind()
        {
            if (_boundSim == null) return;
            _boundSim.WeaponCast -= OnSimWeaponCast;
            _boundSim.HitConfirmed -= OnSimHitConfirmed;
            _boundSim.UltimateCast -= OnSimUltimateCast;
            _boundSim = null;
        }

        void OnSimWeaponCast(int casterId, string weaponId)
        {
            if (_boundSim == null || casterId != _boundSim.LocalPlayerId) return;
            _recoilSerial = (_recoilSerial + 1) & 0x7FFF;
            float sign = (_recoilSerial & 1) == 0 ? 1f : -1f;
            bool isTirador = string.IsNullOrEmpty(weaponId)
                || string.Equals(weaponId, "tirador", StringComparison.OrdinalIgnoreCase);
            float pitchKick = isTirador ? (AimingDown ? 1.15f : 1.65f) : 2.25f;
            float yawJitter = sign * (isTirador ? 0.24f : 0.36f);
            AddRecoilKick(pitchKick, yawJitter);
        }

        void OnSimHitConfirmed(HitConfirmInfo info)
        {
            if (_boundSim == null || info.AttackerId != _boundSim.LocalPlayerId || info.TargetId == info.AttackerId) return;
            AddCombatConfirmShake(info.IsHeadshot, info.IsTag);
            if (info.IsTag)
                TriggerHitStop(0.050f);
            else if (info.IsHeadshot)
                TriggerHitStop(0.045f);
        }

        void OnSimUltimateCast(int casterId)
        {
            if (_boundSim == null || casterId != _boundSim.LocalPlayerId) return;
            AddRecoilKick(2.8f, 0f);
            TriggerHitStop(0.048f);
        }

        /// <summary>A drag of `screenPixels` (Unity screen pixels, y up) turns the view: right turns right, up looks up.</summary>
        public void Look(Vector2 screenPixels, float screenHeight)
        {
            if (!ThirdPerson) return;
            if (screenPixels.sqrMagnitude > 0.0001f)
            {
                _manualLookHold = ManualOverrideCooldown;
                _manualBlend = 0f;
                _yawVelocity = 0f;
                _moveYawReady = false;
            }
            float toBase = BaseHeight / Mathf.Max(screenHeight, 1f);
            Yaw = Mathf.Repeat(Yaw - screenPixels.x * toBase * Rules.LookSensitivity, 360f);
            Pitch = Rules.ClampPitch(Pitch - screenPixels.y * toBase * Rules.LookSensitivity);
        }

        /// <summary>The joystick (x right, y up, length 0..1) as a way on the sim's ground for the active camera mode.</summary>
        public SVector2 GroundWay(Vector2 stick)
        {
            float controlYaw = ThirdPerson ? Yaw : FollowYawDegrees();
            return GodotSpace.GroundWay(new SVector2(stick.x, -stick.y), controlYaw);
        }

        /// <summary>Enables subtle sprint/dash FOV kick (automatically suppressed when ReduceMotion is true).</summary>
        public void SetSpeedBoost(bool boosting)
        {
            _speedBoosting = boosting;
            if (ReduceMotion) _fovBoost = 0f;
        }

        /// <summary>Triggers a subtle positional hit impulse (strictly zeroed when ReduceMotion is true; never rolls camera or moves aim).</summary>
        public void AddHitShake(float intensity01 = 0.6f)
        {
            if (ReduceMotion)
            {
                _hitShakeTimer = 0f;
                return;
            }
            float clamped = Mathf.Clamp01(intensity01);
            if (clamped <= 0.01f) return;
            _hitShakeDuration = 0.12f;
            _hitShakeTimer = _hitShakeDuration;
            _hitShakeStrength = Mathf.Max(_hitShakeStrength, clamped * 0.055f);
            _hitShakePhase += 2.3f;
        }

        /// <summary>
        /// Applies a transient outgoing weapon recoil kick (upward pitch + subtle yaw jitter) with snappy spring recovery.
        /// Strictly suppressed under <see cref="ReduceMotion"/> and never mutates persistent <see cref="Yaw"/> or <see cref="Pitch"/>.
        /// </summary>
        public void AddRecoilKick(float pitchDeg, float yawJitterDeg = 0f)
        {
            if (ReduceMotion)
            {
                _recoilPitchDeg = 0f;
                _recoilPitchVel = 0f;
                _recoilYawDeg = 0f;
                _recoilYawVel = 0f;
                return;
            }
            if (Mathf.Abs(pitchDeg) > 0.001f)
            {
                _recoilPitchDeg = Mathf.Clamp(_recoilPitchDeg + Mathf.Abs(pitchDeg), 0f, MaxRecoilPitchDeg);
                _recoilPitchVel = 0f;
            }
            if (Mathf.Abs(yawJitterDeg) > 0.001f)
            {
                _recoilYawDeg = Mathf.Clamp(_recoilYawDeg + yawJitterDeg, -MaxRecoilYawDeg, MaxRecoilYawDeg);
                _recoilYawVel = 0f;
            }
        }

        /// <summary>Triggers a crisp outgoing combat-confirm micro-shake on body hit, headshot, or TAYA! tag (zeroed under ReduceMotion).</summary>
        public void AddCombatConfirmShake(bool headshot, bool tag)
        {
            if (ReduceMotion)
            {
                _hitShakeTimer = 0f;
                _hitShakeStrength = 0f;
                return;
            }
            float dur = tag ? 0.135f : (headshot ? 0.11f : 0.075f);
            float strength = tag ? 0.052f : (headshot ? 0.038f : 0.018f);
            _hitShakeDuration = dur;
            _hitShakeTimer = Mathf.Max(_hitShakeTimer, dur);
            _hitShakeStrength = Mathf.Max(_hitShakeStrength, strength);
            _hitShakePhase += 2.7f;
            if (headshot || tag)
                AddRecoilKick(tag ? 1.10f : 0.72f, 0f);
        }

        /// <summary>
        /// Triggers a view-only local hit-stop (45-50 ms sweet spot on headshots/tags/ults) that briefly holds visual camera spring
        /// motion for crisp impact feel without EVER touching <see cref="Time.timeScale"/> or stalling the 30 Hz authoritative sim.
        /// </summary>
        public void TriggerHitStop(float seconds = 0.048f)
        {
            if (ReduceMotion)
            {
                _hitStopLeft = 0f;
                return;
            }
            if (seconds <= 0f) return;
            float clamped = Mathf.Clamp(seconds, 0.015f, MaxHitStopSeconds);
            if (clamped > _hitStopLeft) _hitStopLeft = clamped;
        }

        /// <summary>Sets active slide state for subtle 2.5 deg Dutch roll tilt (zeroed under ReduceMotion).</summary>
        public void SetSliding(bool sliding, float lateralSign = 1f)
        {
            _sliding = sliding;
            if (sliding && Mathf.Abs(lateralSign) > 0.01f)
                _slideRollSign = lateralSign >= 0f ? 1f : -1f;
            if (ReduceMotion)
            {
                _slideRollDeg = 0f;
                _slideRollVel = 0f;
            }
        }

        /// <summary>Decoupled vertical posture settle for crouch/slide (-0.42 m), jump (+0.65 * airY), landing dip, and stilt lift; never alters Yaw or Pitch.</summary>
        public void SyncPosture(bool crouchingOrSliding, float airY, float dt)
        {
            if (airY > 0.05f)
            {
                _wasAirborne = true;
                if (airY > _airPeakY) _airPeakY = airY;
            }
            else if (_wasAirborne && airY <= 0.01f)
            {
                _wasAirborne = false;
                if (!ReduceMotion && dt > 0f)
                {
                    _landDipOffset = -Mathf.Clamp(_airPeakY * 0.18f, 0.045f, LandDipMaxMeters);
                    _landDipVel = 0f;
                }
                _airPeakY = 0f;
            }

            float target = (crouchingOrSliding ? CrouchPivotOffset : 0f)
                + Mathf.Max(airY, 0f) * AirPivotShare
                + Mathf.Max(ExtraPivotHeight, 0f);
            if (ReduceMotion || dt <= 0f || Mathf.Approximately(_verticalOffset, target))
            {
                _verticalOffset = target;
                if (ReduceMotion)
                {
                    _landDipOffset = 0f;
                    _landDipVel = 0f;
                }
                return;
            }
            _verticalOffset = Mathf.Lerp(_verticalOffset, target, 1f - Mathf.Exp(-dt * PosturePivotLerp));
        }

        /// <summary>Gentle lateral-orbit yaw follow behind a moving kid when the player is not dragging LookPad.</summary>
        public void SyncBehindCharacter(SVector2 facing, bool moving, float dt, float speedRatio = 1f)
        {
            if (!ThirdPerson) return;
            float stepDt = Mathf.Max(dt, 0f);
            if (_manualLookHold > 0f)
            {
                _manualLookHold = Mathf.Max(0f, _manualLookHold - stepDt);
                _manualBlend = 0f;
                _yawVelocity = 0f;
                return;
            }
            if (stepDt <= 0f) return;

            if (!moving || facing.LengthSquared() <= 0.0001f)
            {
                _moveYawReady = false;
                _moveYawVelocity = 0f;
                if (Mathf.Abs(_yawVelocity) > 0.01f && !ReduceMotion)
                    Yaw = SmoothDampAngle(Yaw, Yaw, ref _yawVelocity, AutoFollowSmoothTime, AutoFollowMaxSpeed, stepDt);
                else
                    _yawVelocity = 0f;
                return;
            }

            float blendWindow = Mathf.Max(ManualOverrideBlendTime, 0.001f);
            _manualBlend = ReduceMotion ? 1f : Mathf.MoveTowards(_manualBlend, 1f, stepDt / blendWindow);
            float easeBlend = Mathf.SmoothStep(0f, 1f, _manualBlend);

            float rawMoveYaw = ShoulderRig.YawToward(SVector2.Zero, facing);
            if (!_moveYawReady || ReduceMotion || FacingSmoothTime <= 0f)
            {
                _smoothedMoveYaw = rawMoveYaw;
                _moveYawVelocity = 0f;
                _moveYawReady = true;
            }
            else
            {
                _smoothedMoveYaw = SmoothDampAngle(_smoothedMoveYaw, rawMoveYaw, ref _moveYawVelocity, FacingSmoothTime, 720f, stepDt);
            }

            float lateralWeight = LateralOrbitWeight(Yaw, _smoothedMoveYaw, speedRatio) * AutoFollowLateralGain * easeBlend;
            if (lateralWeight <= 0.0005f)
            {
                if (Mathf.Abs(_yawVelocity) > 0.01f && !ReduceMotion)
                    Yaw = SmoothDampAngle(Yaw, Yaw, ref _yawVelocity, AutoFollowSmoothTime, AutoFollowMaxSpeed, stepDt);
                else
                    _yawVelocity = 0f;
                return;
            }

            if (ReduceMotion)
            {
                _yawVelocity = 0f;
                float snapRate = 4.6f * Mathf.Clamp(lateralWeight, 0.15f, 1f);
                Yaw = Mathf.Repeat(Mathf.LerpAngle(Yaw, _smoothedMoveYaw, 1f - Mathf.Exp(-stepDt * snapRate)), 360f);
            }
            else
            {
                float effMaxSpeed = Mathf.Max(4f, AutoFollowMaxSpeed * lateralWeight);
                float effSmooth = Mathf.Lerp(AutoFollowSmoothTime * 1.55f, AutoFollowSmoothTime, Mathf.Clamp01(lateralWeight));
                Yaw = SmoothDampAngle(Yaw, _smoothedMoveYaw, ref _yawVelocity, effSmooth, effMaxSpeed, stepDt);
            }
        }

        /// <summary>One-call per-frame helper syncing sprint/dash FOV kick, crouch/jump vertical pivot, slide Dutch roll, and auto-follow yaw.</summary>
        public void SyncLocalKid(SVector2 facing, float speed01, bool sprintingOrDashing, bool crouchingOrSliding, float airY, float dt)
        {
            SetSpeedBoost(sprintingOrDashing);
            SyncPosture(crouchingOrSliding, airY, dt);
            bool inferredSlide = crouchingOrSliding && speed01 >= 0.72f && airY <= 0.02f;
            float rollSign = 1f;
            if (inferredSlide && facing.LengthSquared() > 0.0001f)
            {
                var (camRight, _) = GodotSpace.GroundAxes(Yaw);
                float rightDot = SVector2.Dot(facing, camRight);
                if (Mathf.Abs(rightDot) > 0.15f)
                    rollSign = rightDot > 0f ? -1f : 1f;
            }
            SetSliding(inferredSlide, rollSign);
            SyncBehindCharacter(facing, speed01 > 0.05f, dt, speed01);
        }

        public void RegisterOccluder(GameObject root)
        {
            if (root == null) return;
            for (int i = 0; i < _occluders.Count; i++)
                if (_occluders[i].Root == root) return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            var shadows = new ShadowCastingMode[renderers.Length];
            var colors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                shadows[i] = renderers[i].shadowCastingMode;
                Material mat = renderers[i].sharedMaterial;
                Color c = Color.white;
                if (mat != null)
                {
                    if (mat.HasProperty(BaseColorId)) c = mat.GetColor(BaseColorId);
                    else if (mat.HasProperty(LegacyColorId)) c = mat.GetColor(LegacyColorId);
                }
                colors[i] = c;
            }

            _occluders.Add(new OccluderEntry
            {
                Root = root,
                Bounds = KalyeahResources.RenderBounds(root),
                Renderers = renderers,
                OriginalShadows = shadows,
                BaseColors = colors,
                Alpha = 1f,
            });
        }

        public void RegisterOccluders(IEnumerable<GameObject> roots)
        {
            if (roots == null) return;
            foreach (GameObject root in roots) RegisterOccluder(root);
        }

        /// <summary>Scans a Barangay kit container and registers every building kit marked as an occluder in BarangayPlan.</summary>
        public void CollectOccludersFrom(Transform barangayRoot)
        {
            if (barangayRoot == null) return;
            for (int i = 0; i < barangayRoot.childCount; i++)
            {
                Transform child = barangayRoot.GetChild(i);
                if (child != null && !NonOccluderKits.Contains(child.name))
                    RegisterOccluder(child.gameObject);
            }
        }

        public void ClearOccluders()
        {
            RestoreAllOccluders();
            _occluders.Clear();
            OccludedCount = 0;
        }

        public bool IsOccluded(GameObject root)
        {
            for (int i = 0; i < _occluders.Count; i++)
                if (_occluders[i].Root == root) return _occluders[i].Occluded;
            return false;
        }

        public float GetOccluderAlpha(GameObject root)
        {
            for (int i = 0; i < _occluders.Count; i++)
                if (_occluders[i].Root == root) return _occluders[i].Alpha;
            return 1f;
        }

        public void Apply(Camera camera, SVector2 feet)
        {
            Apply(camera, feet, Application.isPlaying ? Time.deltaTime : 0f);
        }

        public void Apply(Camera camera, SVector2 feet, float dt)
        {
            if (camera == null) return;
            float stepDt = Mathf.Max(dt, 0f);
            bool inHitStop = false;
            if (ReduceMotion)
            {
                _hitStopLeft = 0f;
                _recoilPitchDeg = 0f;
                _recoilPitchVel = 0f;
                _recoilYawDeg = 0f;
                _recoilYawVel = 0f;
                _slideRollDeg = 0f;
                _slideRollVel = 0f;
                _landDipOffset = 0f;
                _landDipVel = 0f;
            }
            else if (stepDt > 0f)
            {
                if (_hitStopLeft > 0f)
                {
                    inHitStop = true;
                    _hitStopLeft = Mathf.Max(0f, _hitStopLeft - stepDt);
                }
                float springDt = inHitStop ? stepDt * 0.08f : stepDt;
                if (Mathf.Abs(_recoilPitchDeg) > 0.0005f || Mathf.Abs(_recoilPitchVel) > 0.0005f)
                    _recoilPitchDeg = SmoothDampScalar(_recoilPitchDeg, 0f, ref _recoilPitchVel, RecoilReturnSmoothTime, 0f, springDt);
                else { _recoilPitchDeg = 0f; _recoilPitchVel = 0f; }

                if (Mathf.Abs(_recoilYawDeg) > 0.0005f || Mathf.Abs(_recoilYawVel) > 0.0005f)
                    _recoilYawDeg = SmoothDampScalar(_recoilYawDeg, 0f, ref _recoilYawVel, RecoilReturnSmoothTime, 0f, springDt);
                else { _recoilYawDeg = 0f; _recoilYawVel = 0f; }

                float targetRoll = _sliding ? (SlideDutchRollDeg * _slideRollSign) : 0f;
                if (Mathf.Abs(_slideRollDeg - targetRoll) > 0.0005f || Mathf.Abs(_slideRollVel) > 0.0005f)
                    _slideRollDeg = SmoothDampScalar(_slideRollDeg, targetRoll, ref _slideRollVel, SlideRollSmoothTime, 0f, springDt);
                else { _slideRollDeg = targetRoll; _slideRollVel = 0f; }

                if (Mathf.Abs(_landDipOffset) > 0.0003f || Mathf.Abs(_landDipVel) > 0.0003f)
                    _landDipOffset = SmoothDampScalar(_landDipOffset, 0f, ref _landDipVel, LandDipReturnSmoothTime, 0f, springDt);
                else { _landDipOffset = 0f; _landDipVel = 0f; }
            }

            float motionDt = inHitStop ? stepDt * 0.08f : stepDt;
            StepFovBoost(motionDt);

            if (!ThirdPerson)
            {
                ApplyTopDownFallback(camera, feet, motionDt);
                RestoreAllOccluders();
                return;
            }

            camera.orthographic = false;
            StepPivot(feet, motionDt);

            bool wantMapCast = SkillAimMode == SkillAimCameraMode.MapCast;
            bool wantZoomOut = !wantMapCast && SkillAimMode == SkillAimCameraMode.ZoomOut;
            bool wantAds = !wantMapCast && !wantZoomOut && (AimingDown || SkillAimMode == SkillAimCameraMode.Ads);

            if (ReduceMotion || motionDt <= 0f)
            {
                _adsBlend = wantAds ? 1f : 0f;
                _zoomOutBlend = wantZoomOut ? 1f : 0f;
                _zoomOutVelocity = 0f;
                _mapCastBlend = wantMapCast ? 1f : 0f;
                _mapCastVelocity = 0f;
            }
            else
            {
                _adsBlend = Mathf.Lerp(_adsBlend, wantAds ? 1f : 0f, 1f - Mathf.Exp(-motionDt * AdsLerp));
                float zoomSmooth = wantZoomOut ? ZoomOutEnterSmoothTime : ZoomOutExitSmoothTime;
                _zoomOutBlend = Mathf.Clamp01(SmoothDampScalar(_zoomOutBlend, wantZoomOut ? 1f : 0f, ref _zoomOutVelocity, zoomSmooth, 0f, motionDt));
                float mapSmooth = wantMapCast ? MapCastEnterSmoothTime : MapCastExitSmoothTime;
                _mapCastBlend = Mathf.Clamp01(SmoothDampScalar(_mapCastBlend, wantMapCast ? 1f : 0f, ref _mapCastVelocity, mapSmooth, 0f, motionDt));
            }

            float targetZoomPitch = Mathf.Clamp(Pitch + ZoomOutPitchAddDeg, Rules.LookPitchMin, ZoomOutMaxPitchDeg);
            float effectivePitch = _zoomOutBlend > 0.0001f ? Mathf.Lerp(Pitch, targetZoomPitch, _zoomOutBlend) : Pitch;
            if (_mapCastBlend > 0.0001f)
            {
                float targetMapPitch = Mathf.Clamp(MapCastPitchDeg, Rules.LookPitchMin, 74f);
                effectivePitch = Mathf.Lerp(effectivePitch, targetMapPitch, _mapCastBlend);
            }

            // Transient upward recoil kick & micro-yaw jitter (never mutates persistent Yaw/Pitch).
            float visualPitch = Rules.ClampPitch(effectivePitch - (ReduceMotion ? 0f : _recoilPitchDeg));
            float visualYaw = Mathf.Repeat(Yaw + (ReduceMotion ? 0f : _recoilYawDeg), 360f);

            float pivotY = Mathf.Max(0.35f, Rules.ShoulderHeight + _verticalOffset + (ReduceMotion ? 0f : _landDipOffset));
            var (baseEyeS, forwardS) = ShoulderRig.Pose(Rules, _pivotGround, visualYaw, visualPitch);
            Vector3 forwardU = KalyeahResources.U(forwardS);
            Vector3 desiredEyeU = KalyeahResources.U(baseEyeS);
            desiredEyeU.y = Mathf.Max(0.5f, desiredEyeU.y + (pivotY - Rules.ShoulderHeight));

            // ADS moves the eye forward along its own view, so the kid keeps their place just left of the crosshair
            if (_adsBlend > 0.0001f)
            {
                float adsPush = Mathf.Max(0f, Rules.ShoulderDistance - Rules.AdsDistance) * _adsBlend;
                desiredEyeU += forwardU * adsPush;
            }

            // ZoomOut pulls smoothly back along the elevated pitch axis for clear wall/zone placement without FOV sickness
            if (_zoomOutBlend > 0.0001f)
                desiredEyeU -= forwardU * (ZoomOutBackMeters * _zoomOutBlend);

            // MapCast climbs high above the barangay lanes for sky-cast skills (docs/KITS.md L51: Kikay Saranggola)
            if (_mapCastBlend > 0.0001f)
                desiredEyeU -= forwardU * (MapCastBackMeters * _mapCastBlend);

            Vector3 pivotU = KalyeahResources.World(_pivotGround, pivotY);

            var (softTargetT, hardMaxT) = SolveWhiskerArm(pivotU, desiredEyeU);
            if (_mapCastBlend > 0.01f)
            {
                // Sky-cast sits high above rooftops; ease off ground-level wall pull-in while keeping arena rim clamp
                softTargetT = Mathf.Lerp(softTargetT, 1f, _mapCastBlend);
                hardMaxT = Mathf.Lerp(hardMaxT, 1f, _mapCastBlend);
            }
            if (!_armReady || ReduceMotion || motionDt <= 0f)
            {
                _armT = softTargetT;
                _armVelocity = 0f;
                _armReady = true;
            }
            else
            {
                float smoothTime = softTargetT < _armT ? PullInSmoothTime : EaseOutSmoothTime;
                _armT = SmoothDampScalar(_armT, softTargetT, ref _armVelocity, smoothTime, 0f, motionDt);
                if (_armT > hardMaxT)
                {
                    _armT = hardMaxT;
                    if (_armVelocity > 0f) _armVelocity = 0f;
                }
            }

            Vector3 eyeU = Vector3.Lerp(pivotU, desiredEyeU, Mathf.Clamp01(_armT));
            eyeU = ClampHardArenaRim(eyeU);
            eyeU.y = Mathf.Max(0.5f, eyeU.y);

            CurrentDistance = Vector3.Distance(eyeU, pivotU);

            Quaternion rot = Quaternion.LookRotation(forwardU, Vector3.up);
            if (!ReduceMotion && Mathf.Abs(_slideRollDeg) > 0.001f)
                rot = rot * Quaternion.Euler(0f, 0f, _slideRollDeg);
            if (!ReduceMotion && _hitShakeTimer > 0f)
            {
                _hitShakeTimer = Mathf.Max(0f, _hitShakeTimer - stepDt);
                float t = _hitShakeDuration > 0.001f ? (_hitShakeTimer / _hitShakeDuration) : 0f;
                float amp = _hitShakeStrength * t * t;
                Vector3 rightU = rot * Vector3.right;
                Vector3 upU = rot * Vector3.up;
                eyeU += rightU * (Mathf.Sin(_hitShakePhase + t * 28f) * amp)
                      + upU * (Mathf.Cos(_hitShakePhase + t * 34f) * amp * 0.65f);
                if (_hitShakeTimer <= 0f) _hitShakeStrength = 0f;
            }

            camera.transform.SetPositionAndRotation(eyeU, rot);

            float sprintBoost = (_adsBlend > 0.05f || _zoomOutBlend > 0.05f || _mapCastBlend > 0.05f) ? 0f : CurrentFovBoost;
            float zoomOutFov = ReduceMotion ? 0f : (ZoomOutFovAddDeg * _zoomOutBlend + MapCastFovAddDeg * _mapCastBlend);
            float desiredVerticalFov = Mathf.Lerp(Rules.ShoulderFov, Rules.AdsFov, _adsBlend) + zoomOutFov + sprintBoost;
            camera.fieldOfView = ClampFairVerticalFov(desiredVerticalFov, camera.aspect, MaxFairAspect);
            camera.nearClipPlane = Rules.ShoulderNear;
            camera.farClipPlane = Far;

            UpdateOccluders(eyeU, KalyeahResources.World(_pivotGround, 0f), stepDt);
        }

        /// <summary>
        /// Locks vertical FOV for all 4:3..21:9 screens (&lt;=21.5:9) and caps horizontal FOV on &gt;21.5:9 ultra-wide viewports.
        /// </summary>
        public static float ClampFairVerticalFov(float verticalFovDeg, float aspect, float maxAspect = MaxFairAspect)
        {
            if (aspect <= maxAspect || maxAspect <= 0.01f) return verticalFovDeg;
            float maxHFov = Camera.VerticalToHorizontalFieldOfView(verticalFovDeg, maxAspect);
            return Camera.HorizontalToVerticalFieldOfView(maxHFov, aspect);
        }

        void ApplyTopDownFallback(Camera camera, SVector2 feet, float dt)
        {
            float followYaw = FollowYawDegrees();
            SVector2 goal = SVector2.Zero;
            if (Follow)
            {
                SVector2 arenaSize = Layout != null && Layout.ArenaRadius > 0f
                    ? new SVector2(Layout.ArenaRadius * 2f, Layout.ArenaRadius * 2f)
                    : new SVector2(60f, 120f);
                goal = FollowAim(feet, arenaSize, FollowViewSize, Mathf.Max(camera.aspect, 0.5f), followYaw);
            }

            if (!_pivotReady || ReduceMotion || FollowLag <= 0f || dt <= 0f || SVector2.Distance(goal, _followAim) > SnapDistance * 3f)
            {
                _followAim = goal;
                _pivotReady = true;
            }
            else
            {
                float t = 1f - Mathf.Exp(-dt * FollowClose / Mathf.Max(FollowLag, 0.001f));
                _followAim = SVector2.Lerp(_followAim, goal, t);
            }

            Vector3 forwardU = KalyeahResources.U(GodotSpace.Forward(FollowPitchDegrees, followYaw));
            Vector3 targetU = KalyeahResources.World(_followAim, 0f);
            Vector3 eyeU = targetU - forwardU * Standoff;

            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(1f, FollowViewSize * 0.5f);
            camera.nearClipPlane = Mathf.Max(1f, Standoff - OrthoNearMargin);
            camera.farClipPlane = Standoff + OrthoFarMargin;
            camera.transform.SetPositionAndRotation(eyeU, Quaternion.LookRotation(forwardU, Vector3.up));
            CurrentDistance = Standoff;
        }

        float FollowYawDegrees() => FollowYawOffsetDegrees + (_team == SimBridge.LocalTeam ? 0f : 180f);

        SVector2 FollowAim(SVector2 point, SVector2 arena, float viewSize, float aspect, float yawDeg)
        {
            var (right, down) = GodotSpace.GroundAxes(yawDeg);
            SVector2 half = arena * 0.5f + new SVector2(FollowEdge, FollowEdge);
            float sinPitch = Mathf.Max(Mathf.Sin(FollowPitchDegrees * Mathf.Deg2Rad), 0.01f);
            float seenX = viewSize * aspect * 0.5f;
            float seenY = (viewSize / sinPitch) * 0.5f;

            float reachR = half.X * MathF.Abs(right.X) + half.Y * MathF.Abs(right.Y);
            float roomR = MathF.Max(0f, reachR - seenX);
            float dotR = Math.Clamp(SVector2.Dot(point, right), -roomR, roomR);

            float reachD = half.X * MathF.Abs(down.X) + half.Y * MathF.Abs(down.Y);
            float roomD = MathF.Max(0f, reachD - seenY);
            float dotD = Math.Clamp(SVector2.Dot(point, down), -roomD, roomD);

            return right * dotR + down * dotD;
        }

        void StepPivot(SVector2 feet, float dt)
        {
            if (!_pivotReady || ReduceMotion || dt <= 0f || PivotSmoothTime <= 0f || SVector2.Distance(feet, _pivotGround) > SnapDistance)
            {
                _pivotGround = feet;
                _pivotVelocity = SVector2.Zero;
                _pivotReady = true;
                return;
            }
            if (SVector2.DistanceSquared(feet, _pivotGround) <= 1e-10f)
            {
                _pivotGround = feet;
                _pivotVelocity = SVector2.Zero;
                return;
            }
            _pivotGround = SmoothDampVec2(_pivotGround, feet, ref _pivotVelocity, PivotSmoothTime, 0f, dt);
        }

        void StepFovBoost(float dt)
        {
            if (ReduceMotion)
            {
                _fovBoost = 0f;
                return;
            }
            float target = _speedBoosting ? SprintFovKick : 0f;
            if (Mathf.Approximately(_fovBoost, target) || dt <= 0f)
            {
                _fovBoost = target;
                return;
            }
            _fovBoost = Mathf.Lerp(_fovBoost, target, 1f - Mathf.Exp(-dt * SprintFovLerp));
        }

        (float softT, float hardT) SolveWhiskerArm(Vector3 pivotU, Vector3 desiredEyeU)
        {
            Vector3 arm = desiredEyeU - pivotU;
            float fullLen = arm.magnitude;
            if (fullLen <= 0.0005f) return (1f, 1f);

            float softT = 1f;
            float hardT = 1f;

            if (CollisionMask != 0)
            {
                Vector3 dir = arm / fullLen;
                int count = Physics.SphereCastNonAlloc(pivotU, Mathf.Max(0.05f, WhiskerRadius), dir, _sphereHits, fullLen, CollisionMask, QueryTriggerInteraction.Ignore);
                float nearestDist = fullLen + 1f;
                for (int i = 0; i < count; i++)
                {
                    Transform hitT = _sphereHits[i].transform;
                    if (hitT == null) continue;
                    if (IgnoreCollisionRoot != null && (hitT == IgnoreCollisionRoot || hitT.IsChildOf(IgnoreCollisionRoot)))
                        continue;
                    if (_sphereHits[i].distance < nearestDist)
                        nearestDist = _sphereHits[i].distance;
                }
                if (nearestDist <= fullLen)
                {
                    softT = Mathf.Min(softT, Mathf.Clamp01((nearestDist - 0.02f) / fullLen));
                    hardT = Mathf.Min(hardT, Mathf.Clamp01((nearestDist + WhiskerRadius * 0.5f) / fullLen));
                }
            }

            if (Layout != null)
            {
                SVector2 p0 = GodotSpace.Ground(new SVector3(pivotU.x, pivotU.y, pivotU.z));
                SVector2 p1 = GodotSpace.Ground(new SVector3(desiredEyeU.x, desiredEyeU.y, desiredEyeU.z));
                softT = Mathf.Min(softT, ClampAnalyticFraction(p0, p1, WhiskerWallPad, ArenaWallPad));
                hardT = Mathf.Min(hardT, ClampAnalyticFraction(p0, p1, HardSurfacePad, HardSurfacePad));
            }

            return (softT, Mathf.Max(softT, hardT));
        }

        float ClampAnalyticFraction(SVector2 p0, SVector2 p1, float wallPad, float arenaPad)
        {
            SVector2 delta = p1 - p0;
            if (delta.LengthSquared() <= 1e-6f) return 1f;

            float bestT = 1f;
            int count = Layout.ParcelCount;
            for (int i = 0; i < count; i++)
            {
                GroundRect raw = Layout.ParcelRect(i);
                var padded = new GroundRect(raw.X - wallPad, raw.Y - wallPad, raw.W + wallPad * 2f, raw.H + wallPad * 2f);
                var (enter, leave) = SlabRayRect(p0, delta, padded);
                if (enter > leave || leave <= 0f || enter >= bestT) continue;
                if (enter >= 0f)
                {
                    bestT = enter;
                }
                else
                {
                    var (sEnter, sLeave) = SlabRayRect(p0, delta, raw);
                    if (leave >= bestT || (sEnter <= sLeave && sLeave > 0f && sEnter < bestT))
                        bestT = 0f;
                }
            }

            if (Layout.ArenaRadius > 0f)
            {
                float maxR = Mathf.Max(0f, Layout.ArenaRadius - arenaPad);
                SVector2 endAt = p0 + delta * bestT;
                if (endAt.LengthSquared() > maxR * maxR)
                {
                    float a = delta.LengthSquared();
                    float b = SVector2.Dot(p0, delta);
                    float c = p0.LengthSquared() - maxR * maxR;
                    if (c <= 0f)
                    {
                        float disc = Mathf.Max(0f, b * b - a * c);
                        float tCircle = (-b + Mathf.Sqrt(disc)) / a;
                        bestT = Mathf.Min(bestT, Mathf.Clamp01(tCircle));
                    }
                    else
                    {
                        bestT = 0f;
                    }
                }
            }

            return bestT;
        }

        Vector3 ClampHardArenaRim(Vector3 eyeU)
        {
            if (Layout == null || Layout.ArenaRadius <= 0f) return eyeU;
            float maxR = Mathf.Max(0f, Layout.ArenaRadius - ArenaWallPad);
            SVector2 xz = GodotSpace.Ground(new SVector3(eyeU.x, eyeU.y, eyeU.z));
            float lenSq = xz.LengthSquared();
            if (lenSq > maxR * maxR && lenSq > 1e-6f)
            {
                xz *= maxR / MathF.Sqrt(lenSq);
                Vector3 clamped = KalyeahResources.World(xz, eyeU.y);
                eyeU.x = clamped.x;
                eyeU.z = clamped.z;
            }
            return eyeU;
        }

        void UpdateOccluders(Vector3 eyeU, Vector3 feetU, float dt)
        {
            if (_occluders.Count == 0)
            {
                OccludedCount = 0;
                return;
            }

            float kidHeight = Mathf.Max(0.8f, KidView.KidHeight * Mathf.Max(Rules.KidViewScale, 0.5f));
            Vector3 tLow = feetU + Vector3.up * (kidHeight * 0.15f);
            Vector3 tMid = feetU + Vector3.up * (kidHeight * 0.50f);
            Vector3 tTop = feetU + Vector3.up * kidHeight;
            float clearSq = ShoulderClearReach * ShoulderClearReach;

            int occludedNow = 0;
            for (int i = _occluders.Count - 1; i >= 0; i--)
            {
                OccluderEntry entry = _occluders[i];
                if (entry.Root == null)
                {
                    _occluders.RemoveAt(i);
                    continue;
                }

                bool blocking = entry.Bounds.SqrDistance(eyeU) < clearSq
                    || SegmentHitsBounds(eyeU, tLow, entry.Bounds)
                    || SegmentHitsBounds(eyeU, tMid, entry.Bounds)
                    || SegmentHitsBounds(eyeU, tTop, entry.Bounds);

                entry.Occluded = blocking;
                if (blocking) occludedNow++;

                float targetAlpha = blocking ? Mathf.Clamp01(OccluderOccludedAlpha) : 1f;
                float nextAlpha = (ReduceMotion || dt <= 0f)
                    ? targetAlpha
                    : Mathf.MoveTowards(entry.Alpha, targetAlpha, dt * Mathf.Max(OccluderFadeRate, 0.1f));

                if (!Mathf.Approximately(nextAlpha, entry.Alpha) || (blocking && entry.Alpha >= 0.999f))
                {
                    entry.Alpha = nextAlpha;
                    ApplyOccluderAlpha(entry);
                }
            }
            OccludedCount = occludedNow;
        }

        void ApplyOccluderAlpha(OccluderEntry entry)
        {
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            bool fullyVisible = entry.Alpha >= 0.995f;
            bool hideMesh = entry.Alpha <= 0.55f;

            for (int i = 0; i < entry.Renderers.Length; i++)
            {
                Renderer r = entry.Renderers[i];
                if (r == null) continue;
                if (fullyVisible)
                {
                    r.enabled = true;
                    r.shadowCastingMode = entry.OriginalShadows[i];
                    r.SetPropertyBlock(null);
                }
                else
                {
                    _propBlock.Clear();
                    Color c = entry.BaseColors[i];
                    c.a *= entry.Alpha;
                    _propBlock.SetColor(BaseColorId, c);
                    _propBlock.SetColor(LegacyColorId, c);
                    r.SetPropertyBlock(_propBlock);
                    r.enabled = !hideMesh;
                }
            }
        }

        void RestoreAllOccluders()
        {
            for (int i = 0; i < _occluders.Count; i++)
            {
                OccluderEntry entry = _occluders[i];
                if (!entry.Occluded && entry.Alpha >= 0.995f) continue;
                entry.Occluded = false;
                entry.Alpha = 1f;
                ApplyOccluderAlpha(entry);
            }
            OccludedCount = 0;
        }

        static (float enter, float leave) SlabRayRect(SVector2 origin, SVector2 delta, GroundRect box)
        {
            float tMin = float.NegativeInfinity;
            float tMax = float.PositiveInfinity;

            if (MathF.Abs(delta.X) <= 1e-6f)
            {
                if (origin.X < box.X || origin.X > box.X + box.W) return (1f, -1f);
            }
            else
            {
                float invX = 1f / delta.X;
                float tx1 = (box.X - origin.X) * invX;
                float tx2 = (box.X + box.W - origin.X) * invX;
                tMin = MathF.Max(tMin, MathF.Min(tx1, tx2));
                tMax = MathF.Min(tMax, MathF.Max(tx1, tx2));
            }

            if (MathF.Abs(delta.Y) <= 1e-6f)
            {
                if (origin.Y < box.Y || origin.Y > box.Y + box.H) return (1f, -1f);
            }
            else
            {
                float invY = 1f / delta.Y;
                float ty1 = (box.Y - origin.Y) * invY;
                float ty2 = (box.Y + box.H - origin.Y) * invY;
                tMin = MathF.Max(tMin, MathF.Min(ty1, ty2));
                tMax = MathF.Min(tMax, MathF.Max(ty1, ty2));
            }

            return (tMin, tMax);
        }

        internal static bool SegmentHitsBounds(Vector3 from, Vector3 to, Bounds box)
        {
            Vector3 way = to - from;
            Vector3 min = box.min;
            Vector3 max = box.max;
            float enter = 0f;
            float leave = 1f;

            for (int axis = 0; axis < 3; axis++)
            {
                float o = from[axis];
                float d = way[axis];
                float lo = min[axis];
                float hi = max[axis];
                if (Mathf.Abs(d) < 1e-7f)
                {
                    if (o < lo || o > hi) return false;
                    continue;
                }
                float inv = 1f / d;
                float t0 = (lo - o) * inv;
                float t1 = (hi - o) * inv;
                enter = Mathf.Max(enter, Mathf.Min(t0, t1));
                leave = Mathf.Min(leave, Mathf.Max(t0, t1));
                if (enter > leave) return false;
            }
            return enter <= leave;
        }

        static float SmoothDampScalar(float current, float target, ref float velocity, float smoothTime, float maxSpeed, float dt)
        {
            if (dt <= 1e-6f) return current;
            float st = Mathf.Max(0.0001f, smoothTime);
            float omega = 2f / st;
            float x = omega * dt;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float originalTo = target;
            if (maxSpeed > 0f)
            {
                float maxChange = maxSpeed * st;
                change = Mathf.Clamp(change, -maxChange, maxChange);
            }
            float clampedTarget = current - change;
            float temp = (velocity + omega * change) * dt;
            velocity = (velocity - omega * temp) * exp;
            float output = clampedTarget + (change + temp) * exp;
            if ((originalTo - current > 0f) == (output > originalTo))
            {
                output = originalTo;
                velocity = 0f;
            }
            return output;
        }

        static float SmoothDampAngle(float currentDeg, float targetDeg, ref float velocityDeg, float smoothTime, float maxSpeedDeg, float dt)
        {
            float deltaDeg = Mathf.DeltaAngle(currentDeg, targetDeg);
            float stepped = SmoothDampScalar(currentDeg, currentDeg + deltaDeg, ref velocityDeg, smoothTime, maxSpeedDeg, dt);
            return Mathf.Repeat(stepped, 360f);
        }

        static SVector2 SmoothDampVec2(SVector2 current, SVector2 target, ref SVector2 velocity, float smoothTime, float maxSpeed, float dt)
        {
            if (dt <= 1e-6f) return current;
            float st = Mathf.Max(0.0001f, smoothTime);
            float omega = 2f / st;
            float x = omega * dt;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            SVector2 change = current - target;
            SVector2 originalTo = target;
            if (maxSpeed > 0f)
            {
                float maxChange = maxSpeed * st;
                if (change.LengthSquared() > maxChange * maxChange)
                    change = SVector2.Normalize(change) * maxChange;
            }
            SVector2 clampedTarget = current - change;
            SVector2 temp = (velocity + change * omega) * dt;
            velocity = (velocity - temp * omega) * exp;
            SVector2 output = clampedTarget + (change + temp) * exp;
            if (SVector2.Dot(originalTo - current, output - originalTo) > 0f)
            {
                output = originalTo;
                velocity = SVector2.Zero;
            }
            return output;
        }

        static float LateralOrbitWeight(float lookYawDeg, float moveYawDeg, float speedRatio)
        {
            float diffRad = Mathf.DeltaAngle(lookYawDeg, moveYawDeg) * Mathf.Deg2Rad;
            float absDiff = Mathf.Abs(diffRad);
            float hi = 176f * Mathf.Deg2Rad;
            float lo = 142f * Mathf.Deg2Rad;
            float t = Mathf.Clamp01((hi - absDiff) / (hi - lo));
            float backpedalGate = t * t * (3f - 2f * t);
            return Mathf.Abs(Mathf.Sin(diffRad)) * backpedalGate * Mathf.Clamp(speedRatio, 0f, 1.25f);
        }

        static float FloatOr(Dictionary<string, object> p, string key, float fallback)
            => p != null && p.ContainsKey(key) ? DataSource.F(p, key) : fallback;

        static bool BoolOr(Dictionary<string, object> p, string key, bool fallback)
            => p != null && p.ContainsKey(key) ? DataSource.B(p, key) : fallback;
    }
}
