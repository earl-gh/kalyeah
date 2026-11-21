using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// Internal MonoBehaviour attached to <see cref="KidView.Root"/> so Unity runs the deterministic A6 LateUpdate pass
    /// immediately after legacy <see cref="Animation"/> sampling and cleans up global settings/quality subscriptions on destroy.
    /// </summary>
    internal sealed class KidRuntimeDriver : MonoBehaviour
    {
        KidView _owner;

        internal void Bind(KidView owner) => _owner = owner;

        void LateUpdate()
        {
            if (_owner != null && Application.isPlaying)
                _owner.OnDriverLateUpdate(Time.deltaTime);
        }

        void OnDestroy()
        {
            if (_owner != null)
            {
                _owner.OnDriverDestroyed();
                _owner = null;
            }
        }
    }

    /// <summary>
    /// Read-only visual puppet for one kid in the street: skinned GLB (<c>assets/models/characters/&lt;id&gt;.glb</c> plus optional
    /// <c>_lod1.glb</c> / <c>_lod2.glb</c> sharing one skeleton), Valorant soft-painted shading + enemy highlight, speed-matched
    /// A4/A6 legacy <see cref="Animation"/> playback with 8-way blending &amp; upper-body <c>Spine1</c> mask, deterministic A6 LateUpdate
    /// (aim offset, 2-bone Foot IK, swing-twist limit clamping, capsule push-out, helper/twist re-eval, High/Ultra Verlet springs,
    /// pose-space correctives, face blink/expressions), update-rate LOD, Low/jump blob shadow, and live 4-tier graphics switching.
    /// </summary>
    public sealed class KidView
    {
        /// <summary>kid_model.gd KID_HEIGHT: standing height before camera kid_view_scale (0.82 -> ~1.52 m).</summary>
        public const float KidHeight = 1.85f;

        const float PolymorphCanHeight = 1.30f;
        const string FallbackSet = "narda";
        const string PolymorphCanId = "can";
        const float RunShare = 0.08f;
        const float WalkRunSplit = 0.62f;
        const float SprintSpeedSplit = 0.94f;
        const float BaseRunSpeedMps = 5.25f;
        const float TurnRateDegPerSec = 22f * Mathf.Rad2Deg; // kid_model.gd TURN_RATE = 22 rad/s (~0.14s 180-deg turn)
        const float LandDuration = 0.18f;
        const float PopInTime = 0.24f;
        const float PopOutTime = 0.18f;
        const float PopBack = 1.70158f;
        const float GoneScale = 0.001f;
        const float DefaultOutlineWidth = 0.014f;
        const float LodHysteresisMeters = 1.25f;
        const float PushOutEaseTime = 0.10f;
        const float MaxIkPelvisDrop = 0.24f;
        const float SpringResetTeleportSq = 2.25f; // 1.5 m squared

        // StatusEffects.Type integer values from Kalyeah.Sim.StatusEffects
        const int StatusStun = 1;
        const int StatusSlow = 2;
        const int StatusAirborne = 3;
        const int StatusPolymorph = 4;
        const int StatusBounce = 5;
        const int StatusKnockout = 6;
        const int StatusTripped = 7;

        enum ClipCategory
        {
            Default = 0,
            Locomotion = 1,
            Cast = 2,
            Hit = 3,
            Stun = 4,
            Air = 5,
        }

        static readonly string[] IdleNames = { "Idle", "idle" };
        static readonly string[] WalkNames = { "walk", "Walk", "Exhausted_Run" };
        static readonly string[] RunNames = { "Run", "run" };
        static readonly string[] SprintNames = { "sprint", "Sprint", "Run", "run" };
        static readonly string[] ExhaustedRunNames = { "Exhausted_Run", "walk", "Walk" };
        static readonly string[] StrafeLeftNames = { "Strafe_L", "strafe_l" };
        static readonly string[] StrafeRightNames = { "Strafe_R", "strafe_r" };
        static readonly string[] BackpedalNames = { "Backpedal", "backpedal" };
        static readonly string[] JumpNames = { "jump", "Jump", "Airborne", "airborne" };
        static readonly string[] SlideNames = { "slide", "Slide", "Airborne" };
        static readonly string[] DashNames = { "Dash", "dash", "sprint", "Sprint", "Run" };
        static readonly string[] CastNames = { "Cast", "Cast_OneHand", "throw", "cast_release_right" };
        static readonly string[] CastRunNames = { "Cast_Run", "Cast", "Cast_OneHand", "throw" };
        static readonly string[] AimHoldNames = { "Aim_Hold", "aim_hold" };
        static readonly string[] FireNames = { "Fire", "fire", "throw" };
        static readonly string[] ReloadNames = { "Reload", "reload" };
        static readonly string[] HitNames = { "hit", "Hit" };
        static readonly string[] SitNames = { "Sit", "sit" };
        static readonly string[] CrouchNames = { "crouch", "Crouch" };
        static readonly string[] StunNames = { "Stunned", "stunned", "Stun" };
        static readonly string[] AirborneNames = { "Airborne", "airborne", "jump", "Jump" };
        static readonly string[] TripNames = { "trip", "Tripped", "tripped" };

        // Precomputed Shader.PropertyToID handles (zero per-frame string lookups).
        static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        static readonly int HitFlashId = Shader.PropertyToID("_HitFlash");
        static readonly int AuraColorId = Shader.PropertyToID("_AuraColor");
        static readonly int EnemyHighlightId = Shader.PropertyToID("_EnemyHighlight");
        static readonly int EnemyHighlightColorId = Shader.PropertyToID("_EnemyHighlightColor");
        static readonly int AllyFresnelColorId = Shader.PropertyToID("_AllyFresnelColor");
        static readonly int AllyFresnelStrengthId = Shader.PropertyToID("_AllyFresnelStrength");
        static readonly int TaggedDimId = Shader.PropertyToID("_TaggedDim");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int GltfBaseColorId = Shader.PropertyToID("baseColorFactor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int GltfEmissiveId = Shader.PropertyToID("emissiveFactor");

        static readonly Color PalamigAura = new Color(0.28f, 0.88f, 1.00f, 1f);
        static readonly Color TeksAura = new Color(1.00f, 0.80f, 0.22f, 1f);
        static readonly Color TuronAura = new Color(0.46f, 0.92f, 0.42f, 1f);
        static readonly Color GenericAura = new Color(1.00f, 0.88f, 0.45f, 1f);

        static Material s_outlineMaterial;
        static Camera s_activeCamera;

        struct CachedClip
        {
            public string Name;
            public AnimationState State;
            public AnimationClip Clip;
            public float StrideSpeedMps;
            public float ReleaseTimeSec;
            public bool IsLocomotion;
            public bool IsUpperBodyMasked;
            public bool IsValid => State != null && Clip != null;
        }

        struct LodBinding
        {
            public SkinnedMeshRenderer Target;
            public SkinnedMeshRenderer HullTarget;
            public Mesh Lod0Mesh;
            public Transform[] Lod0Bones;
            public Transform Lod0RootBone;
            public Mesh Lod1Mesh;
            public Transform[] Lod1Bones;
            public Transform Lod1RootBone;
            public Mesh Lod2Mesh;
            public Transform[] Lod2Bones;
            public Transform Lod2RootBone;
        }

        struct BoneCache
        {
            public Transform Transform;
            public Vector3 RestLocalPos;
            public Quaternion RestLocalRot;
            public Quaternion InvRestLocalRot;
            public bool HasLimit;
            public JointLimitEntry Limit;
            public bool Valid => Transform != null;
        }

        struct CorrectiveBinding
        {
            public SkinnedMeshRenderer Mesh;
            public Mesh Lod0Mesh;
            public Mesh Lod1Mesh;
            public int ShapeIndexLod0;
            public int ShapeIndexLod1;
            public CorrectiveDriverDef Def;
            public Transform Bone;
            public Transform Parent;
            public Quaternion BoneRestLocalRot;
            public Vector3 LocalAxis;
            public Vector3 LocalTargetDir;
            public Vector3 LocalBoneRestDir;
            public float RestAngleToTargetDeg;
            public int ComboIndexA;
            public int ComboIndexB;
            public float NormalizedWeight;
        }

        struct SpringNode
        {
            public Transform Bone;
            public Transform Parent;
            public Quaternion RestLocalRot;
            public Vector3 RestLocalDir;
            public float Length;
            public Vector3 CurTipWS;
            public Vector3 PrevTipWS;
        }

        sealed class RuntimeSpringChain
        {
            public SpringNode[] Nodes;
            public float Stiffness;
            public float Damping;
            public float Gravity;
            public int[] ColliderIndices;
            public bool Initialized;
        }

        struct RuntimeCapsule
        {
            public string Name;
            public Transform Bone;
            public Vector3 LocalA;
            public Vector3 LocalB;
            public float Radius;
            public bool Valid => Bone != null && Radius > 0.001f;
        }

        public readonly int Id;
        public readonly GameObject Root;
        public readonly string CharacterId;
        public string CurrentPose { get; private set; } = "idle";
        public bool HasTeamOutline { get; private set; }
        public bool IsPolymorphed => _isPolymorphed;
        public bool HasEightWayClips => _hasEightWayClips;
        public int ActiveLodIndex { get; private set; }
        public int ActiveAnimUpdateInterval { get; private set; } = 1;
        public GraphicsTierProfile ActiveGraphicsProfile => _graphicsProfile;
        public bool IsLocalKid { get; set; }

        readonly float _viewScale;
        readonly float _modelUniformScale = 1f;
        readonly Transform _bodyPivot;
        readonly GameObject _model;
        readonly Transform _canPivot;
        readonly Transform _blobShadowTransform;
        readonly Renderer _blobShadowRenderer;
        readonly Animation _animation;
        readonly List<Renderer> _kidBaseRenderers = new List<Renderer>(4);
        readonly List<Renderer> _kidOutlineRenderers = new List<Renderer>(4);
        readonly List<Renderer> _canBaseRenderers = new List<Renderer>(2);
        readonly List<Renderer> _canOutlineRenderers = new List<Renderer>(2);
        readonly List<LodBinding> _lodBindings = new List<LodBinding>(2);
        readonly MaterialPropertyBlock _outlineBlock = new MaterialPropertyBlock();
        readonly MaterialPropertyBlock _surfaceBlock = new MaterialPropertyBlock();

        readonly CharacterRigData _rigData;
        readonly RigJointLimitsData _jointLimits;
        readonly AnimClipMetricsData _clipMetrics;
        readonly KidAnimAdaptation _kidAdaptation;

        // Cached AnimationState/Clip handles (zero per-frame string lookups).
        readonly CachedClip _idle, _walk, _run, _sprint, _exhaustedRun;
        readonly CachedClip _strafeL, _strafeR, _backpedal;
        readonly CachedClip _jump, _slide, _dash, _cast, _castRun, _aimHold, _fire, _reload;
        readonly CachedClip _hit, _sit, _crouch, _stun, _airborne, _trip;
        readonly bool _hasNativeCrouch, _hasNativeSlide, _hasNativeSit, _hasEightWayClips;

        // Cached skeleton transforms & rest frames.
        BoneCache _bHips, _bSpine, _bSpine1, _bSpine2, _bNeck, _bHead;
        BoneCache _bLArm, _bLForeArm, _bLHand;
        BoneCache _bRArm, _bRForeArm, _bRHand;
        BoneCache _bLUpLeg, _bLLeg, _bLFoot;
        BoneCache _bRUpLeg, _bRLeg, _bRFoot;

        // Cached R2 twist & half-angle helper bones.
        BoneCache _bLArmTwist, _bRArmTwist;
        BoneCache _bLForeArmTwist1, _bLForeArmTwist2, _bRForeArmTwist1, _bRForeArmTwist2;
        BoneCache _bLForeArmHelper, _bRForeArmHelper, _bLLegHelper, _bRLegHelper, _bLHandHelper, _bRHandHelper;

        readonly CorrectiveBinding[] _correctives;
        readonly RuntimeSpringChain[] _springChains;
        readonly RuntimeCapsule[] _capsules;
        readonly int _torsoCapsuleIdx = -1;
        readonly int _lArmCapsuleIdx = -1, _rArmCapsuleIdx = -1;
        readonly int _lForeArmCapsuleIdx = -1, _rForeArmCapsuleIdx = -1;
        readonly int _lUpLegCapsuleIdx = -1, _rUpLegCapsuleIdx = -1;

        // Cached face blendshape indices (R6, bound to LOD0 mesh).
        readonly SkinnedMeshRenderer _faceMesh;
        readonly Mesh _faceSourceMesh;
        readonly int _bsBlinkL = -1, _bsBlinkR = -1, _bsSmile = -1, _bsOpen = -1, _bsAngry = -1, _bsSurprised = -1, _bsHurt = -1;

        readonly float _legLengthRatio = 1f;
        readonly float _girthArmAbductDeg;
        readonly float _girthHipSpreadDeg;

        GraphicsTierProfile _graphicsProfile;
        bool _subscribedGlobal;
        bool _destroyed;

        Color _teamColor;
        bool _hasExplicitEnemyFlag;
        bool _isEnemy;
        Color _explicitEnemyColor;

        CachedClip _playingClip;
        ClipCategory _playingCategory;
        float _playingSpeed = 1f;
        bool _upperCastActive;
        CachedClip _activeUpperClip;
        float _clock;
        float _manualSampleClock;
        int _frameCounter;
        bool _stateChangedSinceSample = true;

        float _currentYawDeg;
        bool _yawInitialized;
        Vector3 _lastGroundWorld;
        Vector3 _prevSpeedSamplePos;
        Vector3 _smoothedVelWS;
        float _smoothedGroundSpeedMps;
        bool _hasPlacedOnce;

        bool _wasJumping;
        bool _lastJumping;
        bool _lastBlocked;
        bool _lastSliding;
        float _lastAirY;
        float _landTimer;
        float _crouchBlend;
        float _slideBlend;
        float _dashBlend;
        float _castTimer;
        bool _castMoving;
        float _hitTimer;
        float _prevHitFlash;
        bool _wasTripped;
        float _tripTimer;

        bool _isPolymorphed;
        float _kidPop = 1f;
        bool _kidPopUp = true;
        float _canPop;
        bool _canPopUp = true;

        bool _wasTagged;
        int _vanishStage; // 0 = none, 1 = shrink at tag spot, 2 = grow in sidewalk waiting area
        float _vanishProgress;
        Vector3 _vanishWorldPos;
        float _tagPopScale = 1f;

        bool _hiddenNearCamera;
        bool _kidRenderersVisible = true;
        bool _canRenderersVisible;
        bool _lastVisibleToCamera = true;
        float _lastCamDistance = 5f;
        Vector3 _lastLatePosWS;
        bool _hasLastLatePos;

        // Smooth runtime IK / push-out / aim / face state (zero GC).
        Vector3 _aimTargetWS;
        bool _hasAimTarget;
        float _pushLArmDeg, _pushRArmDeg, _pushLLegDeg, _pushRLegDeg;
        float _ikPelvisDrop;
        float _blinkTimer;
        float _blinkPhase;
        uint _viewRngState;

        bool _materialStateDirty = true;
        int _appliedFlashStep = -1;
        int _appliedAuraStep = -1;
        string _appliedBuffId = "";
        bool _appliedTaggedDim;
        int _appliedHighlightMode = -1;

        public static void SetActiveCamera(Camera cam) => s_activeCamera = cam;

        public static void ClearActiveCamera(Camera cam)
        {
            if (ReferenceEquals(s_activeCamera, cam)) s_activeCamera = null;
        }

        /// <summary>
        /// Builds the kid puppet. Note: <paramref name="disc"/> is kept strictly for signature compatibility with existing callers;
        /// no team ring is created under any kid's feet (only a soft neutral drop shadow on Low preset or mid-jump per SPEC §10).
        /// </summary>
        public KidView(Transform parent, KidSnapshot kid, float viewScale, Color team, Material disc)
        {
            Id = kid.Id;
            CharacterId = string.IsNullOrWhiteSpace(kid.CharacterId) ? FallbackSet : kid.CharacterId.Trim().ToLowerInvariant();
            _viewScale = Mathf.Max(viewScale, 0.1f);
            _teamColor = team;
            _graphicsProfile = SettingsStore.Current.GraphicsProfile;
            _viewRngState = (uint)(kid.Id * 747796405 + 2891336453);
            _blinkTimer = NextBlinkInterval();

            _jointLimits = KalyeahResources.JointLimits();
            _clipMetrics = KalyeahResources.ClipMetrics();
            _rigData = KalyeahResources.CharacterRig(CharacterId);
            _kidAdaptation = _clipMetrics.GetKidAdaptation(CharacterId);

            _legLengthRatio = _rigData != null && _rigData.LegLengthRatio > 0.25f
                ? _rigData.LegLengthRatio
                : (_jointLimits != null && _jointLimits.TryGetKidLegLengthRatio(CharacterId, out float limLegRatio)
                    ? limLegRatio
                    : _kidAdaptation.LegLengthRatio);
            _girthArmAbductDeg = _rigData != null && Mathf.Abs(_rigData.ArmAbductOffsetDeg) > 0.01f
                ? _rigData.ArmAbductOffsetDeg
                : _kidAdaptation.GirthArmAbductDeg;
            _girthHipSpreadDeg = _rigData != null && Mathf.Abs(_rigData.HipSpreadOffsetDeg) > 0.01f
                ? _rigData.HipSpreadOffsetDeg
                : _kidAdaptation.GirthHipSpreadDeg;

            Root = new GameObject($"Kid_{kid.Id}_{CharacterId}");
            Root.transform.SetParent(parent, false);

            var driver = Root.AddComponent<KidRuntimeDriver>();
            driver.Bind(this);

            _bodyPivot = new GameObject("Body").transform;
            _bodyPivot.SetParent(Root.transform, false);

            GameObject prefab = KalyeahResources.CharacterModel(CharacterId, 0);
            string resolvedModelId = CharacterId;
            if (prefab == null)
            {
                prefab = KalyeahResources.CharacterModel(FallbackSet, 0);
                resolvedModelId = FallbackSet;
            }

            if (prefab != null)
            {
                _model = Object.Instantiate(prefab, _bodyPivot, false);
                _model.name = "Model";
                _model.transform.localRotation = Quaternion.identity;
                KalyeahResources.UpgradeKidMaterials(_model, resolvedModelId, _rigData);

                float natural = Mathf.Max(KalyeahResources.RenderBounds(_model).size.y, 0.1f);
                _modelUniformScale = KidHeight * _viewScale / natural;
                _model.transform.localScale = Vector3.one * _modelUniformScale;

                _animation = _model.GetComponentInChildren<Animation>(true);
                CacheSkeletonBones(_model.transform);

                if (_animation != null)
                {
                    foreach (AnimationState state in _animation) state.wrapMode = WrapMode.Loop;

                    _idle = ResolveClip(IdleNames, isLocomotion: false, upperBody: false);
                    _walk = ResolveClip(WalkNames, isLocomotion: true, upperBody: false, defaultStrideSpeed: 3.6f);
                    _run = ResolveClip(RunNames, isLocomotion: true, upperBody: false, defaultStrideSpeed: BaseRunSpeedMps);
                    _sprint = ResolveClip(SprintNames, isLocomotion: true, upperBody: false, defaultStrideSpeed: 6.0f);
                    _exhaustedRun = ResolveClip(ExhaustedRunNames, isLocomotion: true, upperBody: false, defaultStrideSpeed: 4.0f);
                    _strafeL = ResolveClip(StrafeLeftNames, isLocomotion: true, upperBody: false, defaultStrideSpeed: 3.8f, allowStandIn: false);
                    _strafeR = ResolveClip(StrafeRightNames, isLocomotion: true, upperBody: false, defaultStrideSpeed: 3.8f, allowStandIn: false);
                    _backpedal = ResolveClip(BackpedalNames, isLocomotion: true, upperBody: false, defaultStrideSpeed: 3.5f, allowStandIn: false);
                    _jump = ResolveClip(JumpNames, isLocomotion: false, upperBody: false);
                    _slide = ResolveClip(SlideNames, isLocomotion: false, upperBody: false);
                    _dash = ResolveClip(DashNames, isLocomotion: true, upperBody: false, defaultStrideSpeed: 8.5f);
                    _cast = ResolveClip(CastNames, isLocomotion: false, upperBody: true, defaultReleaseTime: 0.30f);
                    _castRun = ResolveClip(CastRunNames, isLocomotion: false, upperBody: true, defaultReleaseTime: 0.30f);
                    _aimHold = ResolveClip(AimHoldNames, isLocomotion: false, upperBody: true, allowStandIn: false);
                    _fire = ResolveClip(FireNames, isLocomotion: false, upperBody: true, defaultReleaseTime: 0.12f, allowStandIn: false);
                    _reload = ResolveClip(ReloadNames, isLocomotion: false, upperBody: true, allowStandIn: false);
                    _hit = ResolveClip(HitNames, isLocomotion: false, upperBody: false);
                    _sit = ResolveClip(SitNames, isLocomotion: false, upperBody: false);
                    _crouch = ResolveClip(CrouchNames, isLocomotion: false, upperBody: false);
                    _stun = ResolveClip(StunNames, isLocomotion: false, upperBody: false);
                    _airborne = ResolveClip(AirborneNames, isLocomotion: false, upperBody: false);
                    _trip = ResolveClip(TripNames, isLocomotion: false, upperBody: false);

                    _hasNativeCrouch = HasExactClip("crouch", "Crouch");
                    _hasNativeSlide = HasExactClip("slide", "Slide");
                    _hasNativeSit = HasExactClip("Sit", "sit");
                    _hasEightWayClips = AreDistinctEightWayStates(_walk.State, _run.State, _strafeL.State, _strafeR.State, _backpedal.State);

                    ConfigureClampClip(_jump);
                    ConfigureClampClip(_slide);
                    ConfigureClampClip(_cast);
                    ConfigureClampClip(_castRun);
                    ConfigureClampClip(_fire);
                    ConfigureClampClip(_reload);
                    ConfigureClampClip(_hit);
                    ConfigureClampClip(_trip);

                    ConfigureUpperBodyMask(_cast);
                    ConfigureUpperBodyMask(_castRun);
                    ConfigureUpperBodyMask(_aimHold);
                    ConfigureUpperBodyMask(_fire);
                    ConfigureUpperBodyMask(_reload);

                    _clock = kid.Id * 0.37f; // kids do not breathe in step
                }
            }
            else
            {
                _model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                _model.name = "Model (no GLB: run tools/unity_export/sync_assets.sh)";
                KalyeahResources.Kill(_model.GetComponent<Collider>());
                _model.transform.SetParent(_bodyPivot, false);
                float tall = KidHeight * _viewScale;
                _model.transform.localScale = new Vector3(tall * 0.3f, tall * 0.5f, tall * 0.3f);
                _model.transform.localPosition = Vector3.up * (tall * 0.5f);
            }

            // Pre-instantiate Lata polymorph tin-can visual at load time (zero mid-round Instantiate/Destroy).
            _canPivot = BuildPolymorphCan();

            // Soft neutral drop-shadow quad (active on Low preset blob shadows OR mid-jump height cue).
            BuildBlobShadow(out _blobShadowTransform, out _blobShadowRenderer);

            Material outlineMat = ResolveOutlineMaterial();
            HasTeamOutline = outlineMat != null;
            CollectAndAttachOutlines(_model, outlineMat, _kidBaseRenderers, _kidOutlineRenderers);
            CollectAndAttachOutlines(_canPivot.gameObject, outlineMat, _canBaseRenderers, _canOutlineRenderers);

            // Bind optional LOD1 & LOD2 meshes directly onto LOD0's live skeleton (zero duplicate skeletons/animations).
            BuildLodBindings(resolvedModelId);

            // Build zero-GC runtime tables for capsules, springs, correctives, and face shapes.
            _capsules = BuildRuntimeCapsules(
                out _torsoCapsuleIdx,
                out _lArmCapsuleIdx, out _rArmCapsuleIdx,
                out _lForeArmCapsuleIdx, out _rForeArmCapsuleIdx,
                out _lUpLegCapsuleIdx, out _rUpLegCapsuleIdx);
            _springChains = BuildSpringChains();
            _correctives = BuildCorrectiveBindings();
            ResolveFaceShapes(out _faceMesh, out _faceSourceMesh, out _bsBlinkL, out _bsBlinkR, out _bsSmile, out _bsOpen, out _bsAngry, out _bsSurprised, out _bsHurt);

            SetRendererGroupEnabled(_canBaseRenderers, false);
            SetRendererGroupEnabled(_canOutlineRenderers, false);

            SubscribeGlobalEvents();
            ApplyGraphicsProfile(_graphicsProfile, _lastCamDistance, visible: true);
            ApplyMaterialBlocks(0f, "", kid.Tagged);
            Place(kid.Position, kid.Facing);
        }

        void SubscribeGlobalEvents()
        {
            if (_subscribedGlobal) return;
            _subscribedGlobal = true;
            MatchBoot.GraphicsProfileChanged += OnGlobalGraphicsProfileChanged;
            SettingsStore.Current.Changed += OnGlobalSettingsChanged;
        }

        internal void OnDriverDestroyed()
        {
            if (_destroyed) return;
            _destroyed = true;
            if (_subscribedGlobal)
            {
                _subscribedGlobal = false;
                MatchBoot.GraphicsProfileChanged -= OnGlobalGraphicsProfileChanged;
                SettingsStore.Current.Changed -= OnGlobalSettingsChanged;
            }
        }

        void OnGlobalGraphicsProfileChanged(GraphicsTierProfile profile)
        {
            if (_destroyed || Root == null) return;
            ApplyGraphicsProfile(profile, _lastCamDistance, _lastVisibleToCamera);
        }

        void OnGlobalSettingsChanged()
        {
            if (_destroyed || Root == null) return;
            _materialStateDirty = true;
            GraphicsTierProfile profile = SettingsStore.Current.GraphicsProfile;
            if (profile.QualityIndex != _graphicsProfile.QualityIndex)
                ApplyGraphicsProfile(profile, _lastCamDistance, _lastVisibleToCamera);
        }

        Transform BuildPolymorphCan()
        {
            Transform pivot = new GameObject("PolymorphCan").transform;
            pivot.SetParent(Root.transform, false);
            pivot.localScale = Vector3.one * GoneScale;

            float targetHeight = PolymorphCanHeight * _viewScale;
            GameObject canPrefab = KalyeahResources.Model("weapons", PolymorphCanId);
            if (canPrefab != null)
            {
                GameObject canObj = Object.Instantiate(canPrefab, pivot, false);
                canObj.name = "CanModel";
                canObj.transform.localRotation = Quaternion.identity;
                SanitizeGltfMaterials(canObj);
                Bounds bounds = KalyeahResources.RenderBounds(canObj);
                float natural = Mathf.Max(bounds.size.y, 0.05f);
                float scale = targetHeight / natural;
                canObj.transform.localScale = Vector3.one * scale;
                Bounds scaled = KalyeahResources.RenderBounds(canObj);
                canObj.transform.localPosition += Vector3.up * (pivot.position.y - scaled.min.y);
            }
            else
            {
                GameObject canStandIn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                canStandIn.name = "CanStandIn";
                KalyeahResources.Kill(canStandIn.GetComponent<Collider>());
                canStandIn.transform.SetParent(pivot, false);
                canStandIn.transform.localScale = new Vector3(targetHeight * 0.52f, targetHeight * 0.5f, targetHeight * 0.52f);
                canStandIn.transform.localPosition = Vector3.up * (targetHeight * 0.5f);
            }
            return pivot;
        }

        void BuildBlobShadow(out Transform shadowTr, out Renderer shadowRend)
        {
            Material blobMat = KalyeahResources.SharedBlobShadowMaterial();
            if (blobMat == null)
            {
                shadowTr = null;
                shadowRend = null;
                return;
            }

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "BlobShadow";
            KalyeahResources.Kill(quad.GetComponent<Collider>());
            shadowTr = quad.transform;
            shadowTr.SetParent(Root.transform, false);
            shadowTr.localPosition = new Vector3(0f, 0.025f, 0f);
            shadowTr.localRotation = Quaternion.Euler(90f, 0f, 0f);
            float diameter = 0.92f * _viewScale;
            shadowTr.localScale = new Vector3(diameter, diameter, 1f);

            shadowRend = quad.GetComponent<Renderer>();
            shadowRend.sharedMaterial = blobMat;
            shadowRend.shadowCastingMode = ShadowCastingMode.Off;
            shadowRend.receiveShadows = false;
            shadowRend.lightProbeUsage = LightProbeUsage.Off;
            shadowRend.reflectionProbeUsage = ReflectionProbeUsage.Off;
            shadowRend.enabled = false;
        }

        // ---- Skeleton, LOD & Rig Metadata Caching (Init-Only, Zero Runtime Allocations) -----------------------------------------

        void CacheSkeletonBones(Transform modelRoot)
        {
            var lookup = new Dictionary<string, Transform>(96, StringComparer.OrdinalIgnoreCase);
            Transform[] all = modelRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null) continue;
                string shortName = RigJointLimitsData.StripMixamoPrefix(t.name);
                if (!lookup.ContainsKey(shortName))
                    lookup[shortName] = t;
            }

            _bHips = MakeBoneCache(lookup, "Hips");
            _bSpine = MakeBoneCache(lookup, "Spine");
            _bSpine1 = MakeBoneCache(lookup, "Spine1");
            _bSpine2 = MakeBoneCache(lookup, "Spine2");
            _bNeck = MakeBoneCache(lookup, "Neck");
            _bHead = MakeBoneCache(lookup, "Head");

            _bLArm = MakeBoneCache(lookup, "LeftArm");
            _bLForeArm = MakeBoneCache(lookup, "LeftForeArm");
            _bLHand = MakeBoneCache(lookup, "LeftHand");

            _bRArm = MakeBoneCache(lookup, "RightArm");
            _bRForeArm = MakeBoneCache(lookup, "RightForeArm");
            _bRHand = MakeBoneCache(lookup, "RightHand");

            _bLUpLeg = MakeBoneCache(lookup, "LeftUpLeg");
            _bLLeg = MakeBoneCache(lookup, "LeftLeg");
            _bLFoot = MakeBoneCache(lookup, "LeftFoot");

            _bRUpLeg = MakeBoneCache(lookup, "RightUpLeg");
            _bRLeg = MakeBoneCache(lookup, "RightLeg");
            _bRFoot = MakeBoneCache(lookup, "RightFoot");

            _bLArmTwist = MakeBoneCache(lookup, "LeftArmTwist");
            _bRArmTwist = MakeBoneCache(lookup, "RightArmTwist");
            _bLForeArmTwist1 = MakeBoneCache(lookup, "LeftForeArmTwist1");
            _bLForeArmTwist2 = MakeBoneCache(lookup, "LeftForeArmTwist2");
            _bRForeArmTwist1 = MakeBoneCache(lookup, "RightForeArmTwist1");
            _bRForeArmTwist2 = MakeBoneCache(lookup, "RightForeArmTwist2");

            _bLForeArmHelper = MakeBoneCache(lookup, "LeftForeArmHelper");
            _bRForeArmHelper = MakeBoneCache(lookup, "RightForeArmHelper");
            _bLLegHelper = MakeBoneCache(lookup, "LeftLegHelper");
            _bRLegHelper = MakeBoneCache(lookup, "RightLegHelper");
            _bLHandHelper = MakeBoneCache(lookup, "LeftHandHelper");
            _bRHandHelper = MakeBoneCache(lookup, "RightHandHelper");
        }

        BoneCache MakeBoneCache(Dictionary<string, Transform> lookup, string shortName)
        {
            if (!lookup.TryGetValue(shortName, out Transform tr) || tr == null)
                return default;
            JointLimitEntry lim = default;
            bool hasLimit = _jointLimits != null && _jointLimits.TryGetLimit(shortName, CharacterId, out lim);
            Quaternion restRot = tr.localRotation;
            return new BoneCache
            {
                Transform = tr,
                RestLocalPos = tr.localPosition,
                RestLocalRot = restRot,
                InvRestLocalRot = Quaternion.Inverse(restRot),
                HasLimit = hasLimit,
                Limit = hasLimit ? lim : default,
            };
        }

        void BuildLodBindings(string resolvedModelId)
        {
            if (_model == null) return;
            var boneMap = new Dictionary<string, Transform>(96, StringComparer.OrdinalIgnoreCase);
            Transform[] allBones = _model.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allBones.Length; i++)
            {
                Transform t = allBones[i];
                if (t == null) continue;
                string key = RigJointLimitsData.StripMixamoPrefix(t.name);
                if (!boneMap.ContainsKey(key)) boneMap[key] = t;
            }

            GameObject lod1Prefab = KalyeahResources.CharacterModel(resolvedModelId, 1);
            GameObject lod2Prefab = KalyeahResources.CharacterModel(resolvedModelId, 2);
            SkinnedMeshRenderer[] lod1Smrs = lod1Prefab != null ? lod1Prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true) : Array.Empty<SkinnedMeshRenderer>();
            SkinnedMeshRenderer[] lod2Smrs = lod2Prefab != null ? lod2Prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true) : Array.Empty<SkinnedMeshRenderer>();

            int smrIdx = 0;
            for (int i = 0; i < _kidBaseRenderers.Count; i++)
            {
                if (!(_kidBaseRenderers[i] is SkinnedMeshRenderer smr) || smr.sharedMesh == null) continue;

                SkinnedMeshRenderer matchingHull = null;
                for (int h = 0; h < _kidOutlineRenderers.Count; h++)
                {
                    if (_kidOutlineRenderers[h] is SkinnedMeshRenderer hullSmr && hullSmr.transform.parent == smr.transform)
                    {
                        matchingHull = hullSmr;
                        break;
                    }
                }

                var binding = new LodBinding
                {
                    Target = smr,
                    HullTarget = matchingHull,
                    Lod0Mesh = smr.sharedMesh,
                    Lod0Bones = smr.bones,
                    Lod0RootBone = smr.rootBone,
                    Lod1Mesh = smr.sharedMesh,
                    Lod1Bones = smr.bones,
                    Lod1RootBone = smr.rootBone,
                    Lod2Mesh = smr.sharedMesh,
                    Lod2Bones = smr.bones,
                    Lod2RootBone = smr.rootBone,
                };

                if (smrIdx < lod1Smrs.Length && TryRemapLodSmr(lod1Smrs[smrIdx], boneMap, smr.rootBone, out Mesh m1, out Transform[] b1, out Transform r1))
                {
                    binding.Lod1Mesh = m1;
                    binding.Lod1Bones = b1;
                    binding.Lod1RootBone = r1;
                    // Fallback LOD2 to LOD1 if LOD2 prefab is absent.
                    binding.Lod2Mesh = m1;
                    binding.Lod2Bones = b1;
                    binding.Lod2RootBone = r1;
                }

                if (smrIdx < lod2Smrs.Length && TryRemapLodSmr(lod2Smrs[smrIdx], boneMap, smr.rootBone, out Mesh m2, out Transform[] b2, out Transform r2))
                {
                    binding.Lod2Mesh = m2;
                    binding.Lod2Bones = b2;
                    binding.Lod2RootBone = r2;
                }

                _lodBindings.Add(binding);
                smrIdx++;
            }
        }

        static bool TryRemapLodSmr(
            SkinnedMeshRenderer lodSmr,
            Dictionary<string, Transform> liveBoneMap,
            Transform fallbackRoot,
            out Mesh mesh,
            out Transform[] mappedBones,
            out Transform mappedRoot)
        {
            mesh = null;
            mappedBones = null;
            mappedRoot = fallbackRoot;
            if (lodSmr == null || lodSmr.sharedMesh == null) return false;

            Transform[] srcBones = lodSmr.bones;
            if (srcBones == null || srcBones.Length == 0) return false;

            var remapped = new Transform[srcBones.Length];
            for (int i = 0; i < srcBones.Length; i++)
            {
                Transform sb = srcBones[i];
                if (sb == null) return false;
                string key = RigJointLimitsData.StripMixamoPrefix(sb.name);
                if (!liveBoneMap.TryGetValue(key, out Transform live))
                    return false;
                remapped[i] = live;
            }

            if (lodSmr.rootBone != null)
            {
                string rootKey = RigJointLimitsData.StripMixamoPrefix(lodSmr.rootBone.name);
                if (liveBoneMap.TryGetValue(rootKey, out Transform liveRoot))
                    mappedRoot = liveRoot;
            }

            mesh = lodSmr.sharedMesh;
            mappedBones = remapped;
            return true;
        }

        RuntimeCapsule[] BuildRuntimeCapsules(
            out int torsoIdx,
            out int lArmIdx, out int rArmIdx,
            out int lForeIdx, out int rForeIdx,
            out int lLegIdx, out int rLegIdx)
        {
            torsoIdx = lArmIdx = rArmIdx = lForeIdx = rForeIdx = lLegIdx = rLegIdx = -1;
            if (_model == null) return Array.Empty<RuntimeCapsule>();

            var boneMap = BuildBoneLookup();
            var list = new List<RuntimeCapsule>(10);

            if (_rigData != null && _rigData.Capsules.Count > 0)
            {
                foreach (var kv in _rigData.Capsules)
                {
                    RigCapsuleDef def = kv.Value;
                    if (!boneMap.TryGetValue(def.Bone, out Transform tr)) continue;
                    ConvertModelCapsuleToBoneLocal(_model.transform, tr, def.A, def.B, out Vector3 localA, out Vector3 localB);
                    int idx = list.Count;
                    list.Add(new RuntimeCapsule
                    {
                        Name = def.Name,
                        Bone = tr,
                        LocalA = localA,
                        LocalB = localB,
                        Radius = def.Radius * _modelUniformScale,
                    });
                    AssignCapsuleIndex(def.Name, idx, ref torsoIdx, ref lArmIdx, ref rArmIdx, ref lForeIdx, ref rForeIdx, ref lLegIdx, ref rLegIdx);
                }
            }
            else
            {
                // Synthesize lightweight anatomical capsules from skeleton transforms when <kid>_rig.json is absent.
                float s = _modelUniformScale;
                int headIdx = -1;
                TryAddSynthCapsule(list, "Head", _bHead.Transform, _bNeck.Transform, 0.11f * s, ref headIdx);
                TryAddSynthCapsule(list, "Torso", _bSpine.Transform ?? _bHips.Transform, _bNeck.Transform ?? _bSpine2.Transform, 0.14f * s, ref torsoIdx);
                TryAddSynthCapsule(list, "LeftArm", _bLArm.Transform, _bLForeArm.Transform, 0.045f * s, ref lArmIdx);
                TryAddSynthCapsule(list, "RightArm", _bRArm.Transform, _bRForeArm.Transform, 0.045f * s, ref rArmIdx);
                TryAddSynthCapsule(list, "LeftForeArm", _bLForeArm.Transform, _bLHand.Transform, 0.038f * s, ref lForeIdx);
                TryAddSynthCapsule(list, "RightForeArm", _bRForeArm.Transform, _bRHand.Transform, 0.038f * s, ref rForeIdx);
                TryAddSynthCapsule(list, "LeftUpLeg", _bLUpLeg.Transform, _bLLeg.Transform, 0.075f * s, ref lLegIdx);
                TryAddSynthCapsule(list, "RightUpLeg", _bRUpLeg.Transform, _bRLeg.Transform, 0.075f * s, ref rLegIdx);
            }

            return list.Count > 0 ? list.ToArray() : Array.Empty<RuntimeCapsule>();
        }

        public static void ConvertModelCapsuleToBoneLocal(
            Transform modelRoot,
            Transform bone,
            Vector3 modelA,
            Vector3 modelB,
            out Vector3 localA,
            out Vector3 localB)
        {
            localA = bone.InverseTransformPoint(modelRoot.TransformPoint(modelA));
            localB = bone.InverseTransformPoint(modelRoot.TransformPoint(modelB));
        }

        public static void ConvertModelCorrectiveToBoneLocal(
            Transform modelRoot,
            Transform bone,
            Vector3 modelAxis,
            Vector3 modelTargetDir,
            Vector3 modelRestDir,
            out Vector3 localAxis,
            out Vector3 localTargetDir,
            out Vector3 localRestDir)
        {
            Quaternion boneRestModel = Quaternion.Inverse(modelRoot.rotation) * bone.rotation;
            Quaternion invBoneRestModel = Quaternion.Inverse(boneRestModel);
            Vector3 a = invBoneRestModel * modelAxis;
            Vector3 t = invBoneRestModel * modelTargetDir;
            Vector3 r = invBoneRestModel * modelRestDir;
            localAxis = a.sqrMagnitude > 1e-8f ? a.normalized : Vector3.right;
            localTargetDir = t.sqrMagnitude > 1e-8f ? t.normalized : Vector3.up;
            localRestDir = r.sqrMagnitude > 1e-8f ? r.normalized : Vector3.down;
        }

        public static bool AreDistinctEightWayStates(
            AnimationState walk,
            AnimationState run,
            AnimationState strafeL,
            AnimationState strafeR,
            AnimationState backpedal)
        {
            if (strafeL == null || strafeR == null || backpedal == null) return false;
            if (ReferenceEquals(strafeL, strafeR) || ReferenceEquals(strafeL, backpedal) || ReferenceEquals(strafeR, backpedal))
                return false;
            if (ReferenceEquals(strafeL, walk) || ReferenceEquals(strafeR, walk) || ReferenceEquals(backpedal, walk))
                return false;
            if (ReferenceEquals(strafeL, run) || ReferenceEquals(strafeR, run) || ReferenceEquals(backpedal, run))
                return false;
            return true;
        }

        static void AssignCapsuleIndex(
            string name, int idx,
            ref int torsoIdx, ref int lArmIdx, ref int rArmIdx,
            ref int lForeIdx, ref int rForeIdx, ref int lLegIdx, ref int rLegIdx)
        {
            if (string.Equals(name, "Torso", StringComparison.OrdinalIgnoreCase)) torsoIdx = idx;
            else if (string.Equals(name, "LeftArm", StringComparison.OrdinalIgnoreCase)) lArmIdx = idx;
            else if (string.Equals(name, "RightArm", StringComparison.OrdinalIgnoreCase)) rArmIdx = idx;
            else if (string.Equals(name, "LeftForeArm", StringComparison.OrdinalIgnoreCase)) lForeIdx = idx;
            else if (string.Equals(name, "RightForeArm", StringComparison.OrdinalIgnoreCase)) rForeIdx = idx;
            else if (string.Equals(name, "LeftUpLeg", StringComparison.OrdinalIgnoreCase)) lLegIdx = idx;
            else if (string.Equals(name, "RightUpLeg", StringComparison.OrdinalIgnoreCase)) rLegIdx = idx;
        }

        static void TryAddSynthCapsule(List<RuntimeCapsule> list, string name, Transform start, Transform end, float radius, ref int outIdx)
        {
            if (start == null || end == null) return;
            Vector3 localB = start.InverseTransformPoint(end.position);
            if (localB.sqrMagnitude < 1e-6f) localB = Vector3.up * 0.2f;
            outIdx = list.Count;
            list.Add(new RuntimeCapsule
            {
                Name = name,
                Bone = start,
                LocalA = Vector3.zero,
                LocalB = localB,
                Radius = radius,
            });
        }

        RuntimeSpringChain[] BuildSpringChains()
        {
            if (_model == null || _rigData == null || _rigData.SpringChains == null || _rigData.SpringChains.Length == 0)
                return Array.Empty<RuntimeSpringChain>();

            var boneMap = BuildBoneLookup();
            var chains = new List<RuntimeSpringChain>(_rigData.SpringChains.Length);

            for (int c = 0; c < _rigData.SpringChains.Length; c++)
            {
                RigSpringChainDef def = _rigData.SpringChains[c];
                if (def == null || def.Bones == null || def.Bones.Length == 0) continue;

                var nodes = new List<SpringNode>(def.Bones.Length);
                for (int b = 0; b < def.Bones.Length; b++)
                {
                    if (!boneMap.TryGetValue(def.Bones[b], out Transform tr) || tr == null || tr.parent == null) continue;
                    Vector3 childDir = Vector3.down * 0.12f;
                    if (b + 1 < def.Bones.Length && boneMap.TryGetValue(def.Bones[b + 1], out Transform nextTr) && nextTr != null)
                        childDir = tr.InverseTransformPoint(nextTr.position);
                    else if (tr.childCount > 0)
                        childDir = tr.InverseTransformPoint(tr.GetChild(0).position);

                    float len = Mathf.Max(0.04f, childDir.magnitude * _modelUniformScale);
                    Vector3 localDir = childDir.sqrMagnitude > 1e-6f ? childDir.normalized : Vector3.down;
                    Vector3 tipWS = tr.position + tr.TransformDirection(localDir) * len;

                    nodes.Add(new SpringNode
                    {
                        Bone = tr,
                        Parent = tr.parent,
                        RestLocalRot = tr.localRotation,
                        RestLocalDir = localDir,
                        Length = len,
                        CurTipWS = tipWS,
                        PrevTipWS = tipWS,
                    });
                }

                if (nodes.Count == 0) continue;

                var colIndices = new List<int>(4);
                if (def.Colliders != null && _capsules != null)
                {
                    for (int ci = 0; ci < def.Colliders.Length; ci++)
                    {
                        for (int k = 0; k < _capsules.Length; k++)
                        {
                            if (string.Equals(_capsules[k].Name, def.Colliders[ci], StringComparison.OrdinalIgnoreCase))
                            {
                                colIndices.Add(k);
                                break;
                            }
                        }
                    }
                }

                chains.Add(new RuntimeSpringChain
                {
                    Nodes = nodes.ToArray(),
                    Stiffness = def.Stiffness,
                    Damping = def.Damping,
                    Gravity = def.Gravity,
                    ColliderIndices = colIndices.ToArray(),
                    Initialized = false,
                });
            }

            return chains.Count > 0 ? chains.ToArray() : Array.Empty<RuntimeSpringChain>();
        }

        CorrectiveBinding[] BuildCorrectiveBindings()
        {
            if (_model == null || _rigData == null || _rigData.Correctives == null || _rigData.Correctives.Length == 0)
                return Array.Empty<CorrectiveBinding>();

            if (_lodBindings.Count == 0) return Array.Empty<CorrectiveBinding>();
            LodBinding primaryLod = _lodBindings[0];
            SkinnedMeshRenderer primarySmr = primaryLod.Target;
            if (primarySmr == null || primaryLod.Lod0Mesh == null || primaryLod.Lod0Mesh.blendShapeCount == 0)
                return Array.Empty<CorrectiveBinding>();

            var boneMap = BuildBoneLookup();
            Mesh lod0Mesh = primaryLod.Lod0Mesh;
            Mesh lod1Mesh = primaryLod.Lod1Mesh;
            var bindings = new List<CorrectiveBinding>(_rigData.Correctives.Length);
            var shapeToBindingIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _rigData.Correctives.Length; i++)
            {
                CorrectiveDriverDef def = _rigData.Correctives[i];
                if (def == null || string.IsNullOrEmpty(def.Shape)) continue;
                int shapeIdx0 = lod0Mesh.GetBlendShapeIndex(def.Shape);
                int shapeIdx1 = (lod1Mesh != null && lod1Mesh.blendShapeCount > 0)
                    ? lod1Mesh.GetBlendShapeIndex(def.Shape)
                    : -1;
                if (shapeIdx0 < 0 && shapeIdx1 < 0) continue;

                boneMap.TryGetValue(def.Bone, out Transform bone);
                boneMap.TryGetValue(def.Parent, out Transform parent);
                if (parent == null && bone != null) parent = bone.parent;

                Vector3 localAxis = def.Axis.sqrMagnitude > 1e-8f ? def.Axis.normalized : Vector3.right;
                Vector3 localTargetDir = def.TargetDir.sqrMagnitude > 1e-8f ? def.TargetDir.normalized : Vector3.up;
                Vector3 localBoneRestDir = def.BoneRestDir.sqrMagnitude > 1e-8f ? def.BoneRestDir.normalized : Vector3.down;
                if (bone != null)
                {
                    ConvertModelCorrectiveToBoneLocal(
                        _model.transform, bone, def.Axis, def.TargetDir, def.BoneRestDir,
                        out localAxis, out localTargetDir, out localBoneRestDir);
                }

                float restToTarget = 90f;
                if (def.Kind == "cone")
                {
                    restToTarget = Mathf.Acos(Mathf.Clamp(Vector3.Dot(localBoneRestDir, localTargetDir), -1f, 1f)) * Mathf.Rad2Deg;
                }

                shapeToBindingIdx[def.Shape] = bindings.Count;
                bindings.Add(new CorrectiveBinding
                {
                    Mesh = primarySmr,
                    Lod0Mesh = lod0Mesh,
                    Lod1Mesh = lod1Mesh,
                    ShapeIndexLod0 = shapeIdx0,
                    ShapeIndexLod1 = shapeIdx1,
                    Def = def,
                    Bone = bone,
                    Parent = parent,
                    BoneRestLocalRot = bone != null ? bone.localRotation : Quaternion.identity,
                    LocalAxis = localAxis,
                    LocalTargetDir = localTargetDir,
                    LocalBoneRestDir = localBoneRestDir,
                    RestAngleToTargetDeg = restToTarget,
                    ComboIndexA = -1,
                    ComboIndexB = -1,
                    NormalizedWeight = 0f,
                });
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                CorrectiveBinding b = bindings[i];
                if (b.Def.Kind == "combo" && b.Def.Drivers != null && b.Def.Drivers.Length >= 2)
                {
                    b.ComboIndexA = shapeToBindingIdx.TryGetValue(b.Def.Drivers[0], out int ia) ? ia : -1;
                    b.ComboIndexB = shapeToBindingIdx.TryGetValue(b.Def.Drivers[1], out int ib) ? ib : -1;
                    bindings[i] = b;
                }
            }

            return bindings.Count > 0 ? bindings.ToArray() : Array.Empty<CorrectiveBinding>();
        }

        void ResolveFaceShapes(
            out SkinnedMeshRenderer faceSmr,
            out Mesh sourceMesh,
            out int blinkL, out int blinkR, out int smile, out int open, out int angry, out int surprised, out int hurt)
        {
            faceSmr = null;
            sourceMesh = null;
            blinkL = blinkR = smile = open = angry = surprised = hurt = -1;
            if (_lodBindings.Count == 0) return;

            for (int i = 0; i < _lodBindings.Count; i++)
            {
                SkinnedMeshRenderer smr = _lodBindings[i].Target;
                if (smr == null || smr.sharedMesh == null || smr.sharedMesh.blendShapeCount == 0) continue;
                Mesh m = smr.sharedMesh;
                int bl = m.GetBlendShapeIndex("blink_L");
                int br = m.GetBlendShapeIndex("blink_R");
                int sm = m.GetBlendShapeIndex("smile");
                int op = m.GetBlendShapeIndex("open");
                int an = m.GetBlendShapeIndex("angry");
                int su = m.GetBlendShapeIndex("surprised");
                int hu = m.GetBlendShapeIndex("hurt");
                if (bl >= 0 || br >= 0 || sm >= 0 || op >= 0 || an >= 0 || su >= 0 || hu >= 0)
                {
                    faceSmr = smr;
                    sourceMesh = m;
                    blinkL = bl;
                    blinkR = br;
                    smile = sm;
                    open = op;
                    angry = an;
                    surprised = su;
                    hurt = hu;
                    return;
                }
            }
        }

        Dictionary<string, Transform> BuildBoneLookup()
        {
            var map = new Dictionary<string, Transform>(96, StringComparer.OrdinalIgnoreCase);
            if (_model == null) return map;
            Transform[] all = _model.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null) continue;
                string key = RigJointLimitsData.StripMixamoPrefix(t.name);
                if (!map.ContainsKey(key)) map[key] = t;
            }
            return map;
        }

        static Material ResolveOutlineMaterial()
        {
            if (s_outlineMaterial != null) return s_outlineMaterial;
            s_outlineMaterial = KalyeahResources.Material("TeamOutline", "Kalyeah/TeamOutline");
            return s_outlineMaterial;
        }

        /// <summary>
        /// Repairs any non-URP / Built-In <c>glTF/*</c> / <c>Standard</c> / <c>InternalErrorShader</c> materials on an instantiated
        /// <c>.glb</c> hierarchy before <c>TeamOutlineHull</c> attachment via the single shared <see cref="KalyeahResources.SanitizeModelMaterials"/> cache.
        /// </summary>
        public static void SanitizeGltfMaterials(GameObject root) => KalyeahResources.SanitizeModelMaterials(root);

        /// <summary>
        /// Attaches shared <c>Kalyeah/TeamOutline</c> hull renderers onto pre-sanitized base renderers (used by <see cref="ProjectileViews"/>
        /// so pooled 3D weapons get crisp silhouette outlines without mixing outline renderers into PBR tint arrays).
        /// </summary>
        internal static Renderer[] AttachWeaponOutlines(Renderer[] baseRenderers)
        {
            Material outlineMat = ResolveOutlineMaterial();
            if (outlineMat == null || baseRenderers == null || baseRenderers.Length == 0)
                return Array.Empty<Renderer>();

            var hulls = new List<Renderer>(baseRenderers.Length);
            for (int i = 0; i < baseRenderers.Length; i++)
                TryAttachOutlineHull(baseRenderers[i], outlineMat, hulls);
            return hulls.Count > 0 ? hulls.ToArray() : Array.Empty<Renderer>();
        }

        static void CollectAndAttachOutlines(GameObject root, Material outlineMat, List<Renderer> baseOut, List<Renderer> hullOut)
        {
            Renderer[] found = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < found.Length; i++)
            {
                Renderer r = found[i];
                if (r == null) continue;
                baseOut.Add(r);
                TryAttachOutlineHull(r, outlineMat, hullOut);
            }
        }

        static void TryAttachOutlineHull(Renderer r, Material outlineMat, List<Renderer> hullOut)
        {
            if (r == null || outlineMat == null) return;
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            {
                var go = new GameObject("TeamOutlineHull");
                go.transform.SetParent(smr.transform, false);
                var hull = go.AddComponent<SkinnedMeshRenderer>();
                hull.sharedMesh = smr.sharedMesh;
                hull.bones = smr.bones;
                hull.rootBone = smr.rootBone;
                hull.localBounds = smr.localBounds;
                ConfigureHullRenderer(hull, outlineMat, smr.sharedMesh.subMeshCount);
                hullOut.Add(hull);
            }
            else if (r is MeshRenderer mr)
            {
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) return;
                var go = new GameObject("TeamOutlineHull");
                go.transform.SetParent(mr.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var hull = go.AddComponent<MeshRenderer>();
                ConfigureHullRenderer(hull, outlineMat, mf.sharedMesh.subMeshCount);
                hullOut.Add(hull);
            }
        }

        static void ConfigureHullRenderer(Renderer hull, Material outlineMat, int subMeshCount)
        {
            hull.shadowCastingMode = ShadowCastingMode.Off;
            hull.receiveShadows = false;
            hull.lightProbeUsage = LightProbeUsage.Off;
            hull.reflectionProbeUsage = ReflectionProbeUsage.Off;
            int count = Mathf.Max(1, subMeshCount);
            if (count == 1)
            {
                hull.sharedMaterial = outlineMat;
            }
            else
            {
                var slots = new Material[count];
                for (int s = 0; s < count; s++) slots[s] = outlineMat;
                hull.sharedMaterials = slots;
            }
        }

        CachedClip ResolveClip(string[] names, bool isLocomotion, bool upperBody, float defaultStrideSpeed = 0f, float defaultReleaseTime = 0f, bool allowStandIn = true)
        {
            string found = FindClipName(names);
            if (string.IsNullOrEmpty(found) && allowStandIn && _clipMetrics != null)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    if (_clipMetrics.TryGetStandIn(names[i], out string standIn))
                    {
                        found = FindExactClipName(standIn);
                        if (!string.IsNullOrEmpty(found)) break;
                    }
                }
            }
            if (string.IsNullOrEmpty(found) || _animation == null) return default;

            AnimationState st = _animation[found];
            AnimationClip cl = _animation.GetClip(found);
            float strideSpeed = defaultStrideSpeed;
            float releaseSec = defaultReleaseTime;
            bool maskUpper = upperBody;

            if (_clipMetrics != null && _clipMetrics.TryGetClip(found, out ClipMetricEntry metric))
            {
                if (metric.StrideSpeedMps > 0.1f) strideSpeed = metric.StrideSpeedMps;
                if (metric.ReleaseTimeSec > 0.001f) releaseSec = metric.ReleaseTimeSec;
                if (metric.UpperBodyMask) maskUpper = true;
            }

            return new CachedClip
            {
                Name = found,
                State = st,
                Clip = cl,
                StrideSpeedMps = strideSpeed,
                ReleaseTimeSec = releaseSec,
                IsLocomotion = isLocomotion,
                IsUpperBodyMasked = maskUpper,
            };
        }

        void ConfigureClampClip(CachedClip c)
        {
            if (!c.IsValid) return;
            // Never clamp a shared looping fallback clip.
            if (c.Name == _idle.Name || c.Name == _walk.Name || c.Name == _run.Name || c.Name == _sprint.Name
                || c.Name == _crouch.Name || c.Name == _stun.Name || c.Name == _airborne.Name || c.Name == _sit.Name)
                return;
            c.State.wrapMode = WrapMode.ClampForever;
        }

        void ConfigureUpperBodyMask(CachedClip c)
        {
            if (!c.IsValid || !c.IsUpperBodyMasked || !_bSpine1.Valid) return;
            if (ReferenceEquals(c.State, _idle.State) || ReferenceEquals(c.State, _walk.State)
                || ReferenceEquals(c.State, _run.State) || ReferenceEquals(c.State, _sprint.State)
                || c.Name == _idle.Name || c.Name == _walk.Name || c.Name == _run.Name || c.Name == _sprint.Name)
                return;
            c.State.layer = 1;
            c.State.blendMode = AnimationBlendMode.Blend;
            c.State.AddMixingTransform(_bSpine1.Transform, recursive: true);
        }

        bool HasExactClip(string a, string b)
        {
            if (_animation == null) return false;
            if (_animation.GetClip(a) != null || _animation.GetClip(b) != null) return true;
            foreach (AnimationState state in _animation)
            {
                if (string.Equals(state.name, a, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(state.name, b, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        string FindExactClipName(string name)
        {
            if (_animation == null || string.IsNullOrEmpty(name)) return null;
            if (_animation.GetClip(name) != null) return name;
            foreach (AnimationState state in _animation)
            {
                if (string.Equals(state.name, name, StringComparison.OrdinalIgnoreCase))
                    return state.name;
            }
            return null;
        }

        string FindClipName(string[] names)
        {
            for (int i = 0; i < names.Length; i++)
                if (_animation.GetClip(names[i]) != null) return names[i];
            foreach (AnimationState state in _animation)
                for (int i = 0; i < names.Length; i++)
                    if (string.Equals(state.name, names[i], StringComparison.OrdinalIgnoreCase)) return state.name;
            return null;
        }

        // ---- Public Control API -------------------------------------------------------------------------------------------------

        /// <summary>Updates team color (ally blue / enemy red) via MaterialPropertyBlock.</summary>
        public void SetTeamColor(Color team)
        {
            if (_teamColor == team) return;
            _teamColor = team;
            _materialStateDirty = true;
        }

        /// <summary>Explicitly sets Valorant-style enemy highlight state and color (overrides team-color inference).</summary>
        public void SetEnemyHighlight(bool isEnemy, Color highlightColor)
        {
            _hasExplicitEnemyFlag = true;
            _isEnemy = isEnemy;
            _explicitEnemyColor = highlightColor;
            _materialStateDirty = true;
        }

        /// <summary>Sets world-space aim target for A6 LateUpdate 40/30/30 spine/neck aim offset.</summary>
        public void SetAimTarget(Vector3 worldAimPoint, bool hasAimTarget)
        {
            _aimTargetWS = worldAimPoint;
            _hasAimTarget = hasAimTarget;
        }

        /// <summary>Triggers a one-shot cast clip (<c>Cast_OneHand</c> standing or <c>Cast_Run</c> moving) without mutating sim state.</summary>
        public void TriggerCast(bool moving = false, float duration = 0.36f)
        {
            TriggerCast(moving, duration, elapsedSec: 0f);
        }

        /// <summary>
        /// Triggers a release-frame-aligned cast clip (A4): starts the clip at <c>releaseTime - windup + elapsed</c>
        /// so the hand hits its release frame on the authoritative cast tick.
        /// </summary>
        public void TriggerCast(bool moving, float duration, float elapsedSec)
        {
            _castMoving = moving;
            float dur = Mathf.Max(0.08f, duration);
            _castTimer = Mathf.Max(_castTimer, dur);
            _stateChangedSinceSample = true;

            CachedClip chosen = (moving ? _castRun : _cast);
            if (!chosen.IsValid) chosen = _cast.IsValid ? _cast : _castRun;
            if (chosen.IsValid && _animation != null)
            {
                float startTime = chosen.ReleaseTimeSec > 0.01f
                    ? Mathf.Clamp(chosen.ReleaseTimeSec - dur * 0.5f + elapsedSec, 0f, Mathf.Max(0f, chosen.Clip.length - 0.02f))
                    : Mathf.Max(0f, elapsedSec);
                chosen.State.time = startTime;

                if (Application.isPlaying && chosen.IsUpperBodyMasked && _bSpine1.Valid && chosen.State.layer == 1)
                {
                    _upperCastActive = true;
                    _activeUpperClip = chosen;
                    if (ActiveAnimUpdateInterval == 1)
                    {
                        _animation.CrossFade(chosen.Name, _clipMetrics.FadeIntoCast, PlayMode.StopSameLayer);
                    }
                    else
                    {
                        _animation.Play(chosen.Name, PlayMode.StopSameLayer);
                        chosen.State.enabled = true;
                        chosen.State.weight = 1f;
                    }
                }
                else
                {
                    _playingClip = default; // allow immediate re-trigger on rapid Tirador shots
                }
            }
            else
            {
                _playingClip = default;
            }
        }

        /// <summary>Plays the upper-body masked Tirador fire or reload clip over running legs when available.</summary>
        public void TriggerUpperBodyAction(bool isReload, float duration = 0.24f)
        {
            CachedClip clip = isReload ? _reload : _fire;
            if (!clip.IsValid || _animation == null) return;
            _castTimer = Mathf.Max(_castTimer, Mathf.Max(0.08f, duration));
            _stateChangedSinceSample = true;
            if (Application.isPlaying && clip.IsUpperBodyMasked && _bSpine1.Valid && clip.State.layer == 1)
            {
                clip.State.time = 0f;
                _upperCastActive = true;
                _activeUpperClip = clip;
                if (ActiveAnimUpdateInterval == 1)
                {
                    _animation.CrossFade(clip.Name, _clipMetrics.FadeIntoCast, PlayMode.StopSameLayer);
                }
                else
                {
                    _animation.Play(clip.Name, PlayMode.StopSameLayer);
                    clip.State.enabled = true;
                    clip.State.weight = 1f;
                }
            }
        }

        /// <summary>Blends the upper-body <c>Aim_Hold</c> mask when aiming while running (gracefully ignored if clip absent).</summary>
        public void SetAimHoldActive(bool active)
        {
            if (!Application.isPlaying || !_aimHold.IsValid || _animation == null || !_bSpine1.Valid) return;
            if (active && !_upperCastActive)
                _animation.Blend(_aimHold.Name, 1f, _clipMetrics.FadeIntoCast);
            else if (!active && !_upperCastActive)
                _animation.Blend(_aimHold.Name, 0f, _clipMetrics.FadeLocomotion);
        }

        /// <summary>Toggles &lt;0.75 m shoulder-camera proximity hide across all kid/outline/can renderers with zero GameObject churn.</summary>
        public void SetHiddenNearCamera(bool hidden)
        {
            if (hidden) IsLocalKid = true;
            if (_hiddenNearCamera == hidden) return;
            _hiddenNearCamera = hidden;
            RefreshRendererVisibility();
        }

        /// <summary>
        /// Applies a 4-tier <see cref="GraphicsTierProfile"/> immediately (zero restart): swaps character LOD0/LOD1/LOD2 meshes,
        /// skin weights quality, shadow casting, blob shadow visibility, update-rate LOD, and clears disabled correctives/springs.
        /// </summary>
        public void ApplyGraphicsProfile(GraphicsTierProfile profile, float cameraDistanceMeters = -1f, bool visible = true)
        {
            _graphicsProfile = profile;
            if (cameraDistanceMeters >= 0f)
                _lastCamDistance = cameraDistanceMeters;
            _lastVisibleToCamera = visible;

            int targetLod = profile.ResolveCharacterLod(_lastCamDistance, IsLocalKid);
            ApplyMeshLod(targetLod, force: true);

            ActiveAnimUpdateInterval = profile.ResolveAnimUpdateInterval(_lastCamDistance, _lastVisibleToCamera);
            SyncAnimationComponentMode();

            SkinQuality skinQual = profile.SkinWeightsCount <= 2 ? SkinQuality.Bone2 : SkinQuality.Bone4;
            ShadowCastingMode castMode = profile.RealTimeShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            for (int i = 0; i < _kidBaseRenderers.Count; i++)
            {
                Renderer r = _kidBaseRenderers[i];
                if (r == null) continue;
                r.shadowCastingMode = castMode;
                if (r is SkinnedMeshRenderer smr)
                    smr.quality = skinQual;
            }

            if (!profile.ShouldRunCorrectives(_lastCamDistance, IsLocalKid))
                ZeroCorrectiveBlendShapes();
            if (!profile.ShouldRunSprings(_lastCamDistance))
                ResetSpringChains();

            UpdateBlobShadow(_lastAirY);
            _materialStateDirty = true;
        }

        /// <summary>Feet at <paramref name="ground"/> (sim XZ), facing <paramref name="facing"/> (sim XZ), matching kid_model.gd.</summary>
        public void Place(SVector2 ground, SVector2 facing)
        {
            Vector3 world = KalyeahResources.World(ground);
            Root.transform.position = world;
            _lastGroundWorld = world;
            _prevSpeedSamplePos = world;
            _lastLatePosWS = world;
            _hasLastLatePos = true;
            _hasPlacedOnce = true;
            if (facing.LengthSquared() > 0.0001f)
            {
                _currentYawDeg = GodotSpace.ModelYawDegrees(GodotSpace.KidYawRad(facing));
                _yawInitialized = true;
                Root.transform.rotation = Quaternion.Euler(0f, _currentYawDeg, 0f);
            }
        }

        /// <summary>Idle/walk/run by speed. Preserved for existing callers; PlayMode crossfades, EditMode samples for screenshots.</summary>
        public void Animate(float speed01, float dt)
        {
            float clampedSpeed = Mathf.Clamp01(speed01);
            CachedClip want = clampedSpeed >= WalkRunSplit
                ? FirstValid(_run, _idle)
                : (clampedSpeed > RunShare ? FirstValid(_walk, _run, _idle) : _idle);
            float groundSpeed = clampedSpeed * BaseRunSpeedMps;
            float rate = ComputeSpeedMatchedRate(want, groundSpeed, slowed: false);
            CurrentPose = clampedSpeed >= WalkRunSplit ? "run" : (clampedSpeed > RunShare ? "walk" : "idle");
            DriveClip(want, ClipCategory.Locomotion, rate, Mathf.Max(0f, dt), clockAlreadyStepped: false);
        }

        /// <summary>
        /// Full read-only puppet sync from interpolated sim state: ground XZ + <paramref name="airY"/> jump lift, smooth facing turn,
        /// speed-matched locomotion + 8-way blend + A6 crossfades + upper-body cast mask, procedural postures, Lata polymorph swap,
        /// hit flash, Merienda aura tint, LOD/update-rate refresh, blob shadow, and &lt;0.75 m camera hide.
        /// </summary>
        public void SyncState(
            SVector2 ground,
            float airY,
            SVector2 facing,
            float speed01,
            bool crouching,
            bool sliding,
            bool dashing,
            int statusKind,
            bool tagged,
            float hitFlash01,
            string buffId,
            bool hideNearCamera,
            float dt)
        {
            dt = Mathf.Max(0f, dt);
            _clock += dt;
            _frameCounter++;
            if (hideNearCamera) IsLocalKid = true;
            _hiddenNearCamera = hideNearCamera;

            Vector3 targetWorld = KalyeahResources.World(ground);
            if (dt > 0.0001f && _hasPlacedOnce)
            {
                Vector3 deltaPos = targetWorld - _prevSpeedSamplePos;
                deltaPos.y = 0f;
                Vector3 instantVel = deltaPos / dt;
                float instantSpeed = instantVel.magnitude;
                if (instantSpeed < 22f) // ignore respawn/teleport spikes
                {
                    float alpha = 1f - Mathf.Exp(-dt * 14f);
                    _smoothedVelWS = Vector3.Lerp(_smoothedVelWS, instantVel, alpha);
                    _smoothedGroundSpeedMps = Mathf.Lerp(_smoothedGroundSpeedMps, instantSpeed, alpha);
                }
            }
            else if (dt <= 0.0001f)
            {
                _smoothedGroundSpeedMps = Mathf.Clamp01(speed01) * BaseRunSpeedMps;
            }
            _prevSpeedSamplePos = targetWorld;

            StepTaggedVanish(targetWorld, tagged, dt);

            if (facing.LengthSquared() > 0.0001f)
            {
                float targetYaw = GodotSpace.ModelYawDegrees(GodotSpace.KidYawRad(facing));
                if (!_yawInitialized || !Application.isPlaying || dt <= 0f)
                {
                    _currentYawDeg = targetYaw;
                    _yawInitialized = true;
                }
                else
                {
                    _currentYawDeg = Mathf.MoveTowardsAngle(_currentYawDeg, targetYaw, TurnRateDegPerSec * dt);
                }
                Root.transform.rotation = Quaternion.Euler(0f, _currentYawDeg, 0f);
            }

            bool isStun = statusKind == StatusStun || statusKind == StatusKnockout;
            bool isSlow = statusKind == StatusSlow;
            bool isStatusAir = statusKind == StatusAirborne || statusKind == StatusBounce;
            bool isPolymorph = statusKind == StatusPolymorph;
            bool isTripped = statusKind == StatusTripped;

            if (isTripped && !_wasTripped) _tripTimer = 0.85f;
            _wasTripped = isTripped;
            _tripTimer = Mathf.Max(0f, _tripTimer - dt);

            StepPolymorph(isPolymorph, speed01, dt);

            bool blocked = tagged || isStun || isStatusAir || isPolymorph || isTripped;
            bool jumping = !blocked && airY > 0.01f;
            if (_wasJumping && !jumping && !blocked && dt > 0f) _landTimer = LandDuration;
            else if (blocked) _landTimer = 0f;
            _wasJumping = jumping;
            _lastJumping = jumping;
            _lastBlocked = blocked;
            _lastAirY = airY;
            _landTimer = Mathf.Max(0f, _landTimer - dt);

            bool crouchActive = !blocked && !jumping && !sliding && !dashing && crouching;
            bool slideActive = !blocked && !jumping && sliding;
            bool dashActive = !blocked && dashing;
            _lastSliding = slideActive;

            if (!Application.isPlaying || dt <= 0f)
            {
                _crouchBlend = crouchActive ? 1f : 0f;
                _slideBlend = slideActive ? 1f : 0f;
                _dashBlend = dashActive ? 1f : 0f;
            }
            else
            {
                _crouchBlend = Mathf.MoveTowards(_crouchBlend, crouchActive ? 1f : 0f, dt * 10f);
                _slideBlend = Mathf.MoveTowards(_slideBlend, slideActive ? 1f : 0f, dt * 14f);
                _dashBlend = Mathf.MoveTowards(_dashBlend, dashActive ? 1f : 0f, dt * 16f);
            }

            float clampedFlash = Mathf.Clamp01(hitFlash01);
            if (clampedFlash > 0.55f && clampedFlash > _prevHitFlash + 0.15f && !blocked && !slideActive && !dashActive)
                _hitTimer = 0.24f;
            _prevHitFlash = clampedFlash;
            _hitTimer = Mathf.Max(0f, _hitTimer - dt);
            _castTimer = Mathf.Max(0f, _castTimer - dt);

            if (_upperCastActive && (_castTimer <= 0f || blocked))
            {
                _upperCastActive = false;
                if (Application.isPlaying && _animation != null && _activeUpperClip.IsValid)
                {
                    if (ActiveAnimUpdateInterval == 1)
                    {
                        _animation.Blend(_activeUpperClip.Name, 0f, _clipMetrics.FadeIntoCast);
                    }
                    else
                    {
                        _activeUpperClip.State.weight = 0f;
                        _activeUpperClip.State.enabled = false;
                        _stateChangedSinceSample = true;
                    }
                }
            }

            // Refresh camera distance, Mesh LOD, and animation update-rate LOD (zero GC).
            UpdateCameraLodMetrics();

            // Select sim-honest clip, category, and speed-matched rate.
            float effectiveSpeedMps = Mathf.Max(_smoothedGroundSpeedMps, Mathf.Clamp01(speed01) * BaseRunSpeedMps);
            CachedClip wantClip = _idle;
            ClipCategory wantCat = ClipCategory.Default;
            float clipRate = 1f;
            bool wantEightWay = false;

            if (isPolymorph)
            {
                CurrentPose = "can";
                wantClip = _idle;
                wantCat = ClipCategory.Default;
            }
            else if (tagged)
            {
                CurrentPose = "tagged";
                wantClip = FirstValid(_sit, _crouch, _idle);
                wantCat = ClipCategory.Default;
                clipRate = 0.72f;
            }
            else if (isStatusAir)
            {
                CurrentPose = "airborne";
                wantClip = FirstValid(_airborne, _jump, _idle);
                wantCat = ClipCategory.Air;
            }
            else if (isStun)
            {
                CurrentPose = "stunned";
                wantClip = FirstValid(_stun, _idle);
                wantCat = ClipCategory.Stun;
            }
            else if (isTripped)
            {
                CurrentPose = "tripped";
                wantClip = FirstValid(_trip, _idle);
                wantCat = ClipCategory.Hit;
            }
            else if (dashActive)
            {
                CurrentPose = "dash";
                wantClip = FirstValid(_dash, _sprint, _run, _idle);
                wantCat = ClipCategory.Locomotion;
                clipRate = 1.35f;
            }
            else if (jumping)
            {
                CurrentPose = "jump";
                wantClip = FirstValid(_jump, _airborne, _run, _idle);
                wantCat = ClipCategory.Air;
            }
            else if (slideActive)
            {
                CurrentPose = "slide";
                wantClip = FirstValid(_slide, _airborne, _run, _idle);
                wantCat = ClipCategory.Air;
            }
            else if (_castTimer > 0f && (!Application.isPlaying || !_upperCastActive))
            {
                CachedClip chosenCast = (_castMoving || speed01 > RunShare)
                    ? FirstValid(_castRun, _cast, _idle)
                    : FirstValid(_cast, _castRun, _idle);
                if (Application.isPlaying && chosenCast.IsValid && chosenCast.State.layer == 1)
                {
                    // Never push a layer-1 upper-body clip into layer-0 DriveClip in PlayMode; keep lower-body idle/locomotion clean.
                    CurrentPose = "cast";
                    wantClip = speed01 > RunShare ? FirstValid(_run, _walk, _idle) : _idle;
                    wantCat = ClipCategory.Locomotion;
                }
                else
                {
                    CurrentPose = "cast";
                    wantClip = chosenCast;
                    wantCat = ClipCategory.Cast;
                }
            }
            else if (_hitTimer > 0f && speed01 <= RunShare)
            {
                CurrentPose = "hit";
                wantClip = FirstValid(_hit, _idle);
                wantCat = ClipCategory.Hit;
            }
            else if (crouchActive)
            {
                CurrentPose = "crouch";
                wantClip = _hasNativeCrouch
                    ? _crouch
                    : (speed01 > RunShare ? FirstValid(_walk, _run, _idle) : _idle);
                wantCat = ClipCategory.Locomotion;
                clipRate = speed01 > RunShare ? 0.78f : (_hasNativeCrouch ? 0.65f : 1f);
            }
            else if (speed01 >= SprintSpeedSplit && _sprint.IsValid && !isSlow)
            {
                CurrentPose = "run";
                wantClip = _sprint;
                wantCat = ClipCategory.Locomotion;
                clipRate = ComputeSpeedMatchedRate(wantClip, effectiveSpeedMps, slowed: false);
            }
            else if (speed01 >= WalkRunSplit)
            {
                CurrentPose = "run";
                wantClip = isSlow && _exhaustedRun.IsValid ? _exhaustedRun : FirstValid(_run, _idle);
                wantCat = ClipCategory.Locomotion;
                clipRate = ComputeSpeedMatchedRate(wantClip, effectiveSpeedMps, isSlow);
                wantEightWay = _hasEightWayClips;
            }
            else if (speed01 > RunShare)
            {
                CurrentPose = "walk";
                wantClip = FirstValid(_walk, _run, _idle);
                wantCat = ClipCategory.Locomotion;
                clipRate = ComputeSpeedMatchedRate(wantClip, effectiveSpeedMps, isSlow);
                wantEightWay = _hasEightWayClips;
            }
            else
            {
                CurrentPose = "idle";
                wantClip = _idle;
                wantCat = ClipCategory.Locomotion;
            }

            if (_upperCastActive && !blocked)
            {
                CurrentPose = "cast";
                if (Application.isPlaying && ActiveAnimUpdateInterval != 1 && _activeUpperClip.IsValid)
                    _activeUpperClip.State.time = Mathf.Min(_activeUpperClip.State.time + dt, _activeUpperClip.Clip.length);
            }

            DriveClip(wantClip, wantCat, clipRate, dt, clockAlreadyStepped: true);
            if (wantEightWay)
                ApplyEightWayLocomotionBlend(wantClip, clipRate);

            ApplyProceduralBodyPose(airY, tagged, isStun, isStatusAir, isTripped, jumping, clampedFlash);
            UpdateBlobShadow(airY);
            RefreshRendererVisibility();
            ApplyMaterialBlocks(clampedFlash, buffId ?? "", tagged);

            // In EditMode (tests / CI screenshots), Unity does not tick MonoBehaviour.LateUpdate automatically.
            if (!Application.isPlaying)
                RunLateUpdatePass(dt);
        }

        /// <summary>Executes the deterministic A6 LateUpdate pass immediately (for EditMode tests and deterministic screenshots).</summary>
        public void EvaluateLateUpdateNow(float dt) => RunLateUpdatePass(Mathf.Max(0f, dt));

        internal void OnDriverLateUpdate(float dt) => RunLateUpdatePass(Mathf.Max(0f, dt));

        // ---- Mesh LOD & Update-Rate LOD -----------------------------------------------------------------------------------------

        void UpdateCameraLodMetrics()
        {
            Camera cam = s_activeCamera != null ? s_activeCamera : Camera.main;
            if (cam == null) return;

            Transform camTr = cam.transform;
            Vector3 toKid = Root.transform.position - camTr.position;
            float dist = toKid.magnitude;
            _lastCamDistance = dist;

            // Zero-GC frustum visibility approximation (dot vs camera half-FOV cone + close margin).
            bool visible;
            if (dist < 2.5f)
            {
                visible = true;
            }
            else
            {
                float dot = Vector3.Dot(toKid / dist, camTr.forward);
                visible = dot > 0.25f;
            }
            _lastVisibleToCamera = visible;

            // Apply hysteresis deadband around LOD thresholds so kids walking at 15m/35m never flicker.
            int desiredLod = _graphicsProfile.ResolveCharacterLod(dist, IsLocalKid);
            if (desiredLod != ActiveLodIndex)
            {
                float boundary = desiredLod > ActiveLodIndex
                    ? (desiredLod == 2 ? _graphicsProfile.Lod2Distance : _graphicsProfile.Lod1Distance)
                    : (ActiveLodIndex == 2 ? _graphicsProfile.Lod2Distance : _graphicsProfile.Lod1Distance);
                float hyst = desiredLod > ActiveLodIndex ? boundary + LodHysteresisMeters : boundary - LodHysteresisMeters;
                bool crossOk = _graphicsProfile.PreferLowerCharacterLod
                    || (desiredLod > ActiveLodIndex ? dist >= hyst : dist <= hyst);
                if (crossOk)
                    ApplyMeshLod(desiredLod, force: false);
            }

            int nextInterval = _graphicsProfile.ResolveAnimUpdateInterval(dist, visible);
            if (nextInterval != ActiveAnimUpdateInterval)
            {
                if (ActiveAnimUpdateInterval == 1 && nextInterval != 1 && _playingClip.IsValid)
                    _manualSampleClock = _playingClip.State.time;
                ActiveAnimUpdateInterval = nextInterval;
                SyncAnimationComponentMode();
            }
        }

        void ApplyMeshLod(int targetLod, bool force)
        {
            int clamped = Mathf.Clamp(targetLod, 0, 2);
            if (!force && clamped == ActiveLodIndex) return;
            ActiveLodIndex = clamped;

            for (int i = 0; i < _lodBindings.Count; i++)
            {
                LodBinding b = _lodBindings[i];
                if (b.Target == null) continue;

                Mesh nextMesh = clamped == 2 ? b.Lod2Mesh : (clamped == 1 ? b.Lod1Mesh : b.Lod0Mesh);
                Transform[] nextBones = clamped == 2 ? b.Lod2Bones : (clamped == 1 ? b.Lod1Bones : b.Lod0Bones);
                Transform nextRoot = clamped == 2 ? b.Lod2RootBone : (clamped == 1 ? b.Lod1RootBone : b.Lod0RootBone);

                if (nextMesh != null && !ReferenceEquals(b.Target.sharedMesh, nextMesh))
                {
                    b.Target.sharedMesh = nextMesh;
                    if (nextBones != null && nextBones.Length > 0) b.Target.bones = nextBones;
                    if (nextRoot != null) b.Target.rootBone = nextRoot;
                }
                if (b.HullTarget != null && nextMesh != null && !ReferenceEquals(b.HullTarget.sharedMesh, nextMesh))
                {
                    b.HullTarget.sharedMesh = nextMesh;
                    if (nextBones != null && nextBones.Length > 0) b.HullTarget.bones = nextBones;
                    if (nextRoot != null) b.HullTarget.rootBone = nextRoot;
                }
            }
        }

        void SyncAnimationComponentMode()
        {
            if (_animation == null) return;
            // Near kids (interval == 1) use Unity's native Animation tick; distant/off-screen kids disable Animation and Sample() on cadence.
            bool nativeAuto = Application.isPlaying && ActiveAnimUpdateInterval == 1;
            if (_animation.enabled != nativeAuto)
                _animation.enabled = nativeAuto;
        }

        void UpdateBlobShadow(float airY)
        {
            if (_blobShadowRenderer == null || _blobShadowTransform == null) return;
            bool showBlob = !_hiddenNearCamera
                && !_isPolymorphed
                && _kidPop > 0.1f
                && (_graphicsProfile.BlobShadowsOnly || airY > 0.08f);

            if (_blobShadowRenderer.enabled != showBlob)
                _blobShadowRenderer.enabled = showBlob;

            if (showBlob)
            {
                float heightFade = Mathf.Clamp(1f - Mathf.Max(0f, airY) * 0.22f, 0.45f, 1f);
                float diameter = 0.92f * _viewScale * heightFade;
                _blobShadowTransform.localScale = new Vector3(diameter, diameter, 1f);
            }
        }

        // ---- Sim-Honest Clip Playback & 8-Way Blending (A4 & A6) ---------------------------------------------------------------

        float ComputeSpeedMatchedRate(CachedClip clip, float groundSpeedMps, bool slowed)
        {
            if (!clip.IsValid) return 1f;
            float stride = clip.StrideSpeedMps > 0.1f ? clip.StrideSpeedMps : BaseRunSpeedMps;
            float rawRate = groundSpeedMps / stride;
            if (slowed) rawRate *= 0.85f;
            return Mathf.Clamp(rawRate, _clipMetrics.SpeedMatchMin, _clipMetrics.SpeedMatchMax);
        }

        float ResolveCrossfadeSeconds(ClipCategory fromCat, ClipCategory toCat)
        {
            if (toCat == ClipCategory.Hit) return _clipMetrics.FadeIntoHit;
            if (toCat == ClipCategory.Cast) return _clipMetrics.FadeIntoCast;
            if (toCat == ClipCategory.Air) return _clipMetrics.FadeIntoAir;
            if (fromCat == ClipCategory.Hit || fromCat == ClipCategory.Stun) return _clipMetrics.FadeOutOfHitOrStun;
            if (fromCat == ClipCategory.Locomotion && toCat == ClipCategory.Locomotion) return _clipMetrics.FadeLocomotion;
            return _clipMetrics.FadeDefault;
        }

        void DriveClip(CachedClip want, ClipCategory category, float speedScale, float dt, bool clockAlreadyStepped)
        {
            if (_animation == null || !want.IsValid) return;
            if (Application.isPlaying && want.State.layer != 0)
            {
                if (!_idle.IsValid || _idle.State.layer != 0) return;
                want = _idle;
            }
            bool first = !_playingClip.IsValid;
            bool changed = !string.Equals(want.Name, _playingClip.Name, StringComparison.Ordinal);

            if (!first && changed)
            {
                _stateChangedSinceSample = true;
                // A6 sync: walk/run/sprint carry normalizedTime across swaps so feet stay in phase.
                if (want.IsLocomotion && _playingClip.IsLocomotion && _playingClip.State != null)
                {
                    float phase = _playingClip.State.normalizedTime % 1f;
                    if (phase < 0f) phase += 1f;
                    want.State.normalizedTime = phase;
                    _manualSampleClock = phase * want.Clip.length;
                }
                else
                {
                    _clock = 0f;
                    _manualSampleClock = 0f;
                }
            }
            else if (!clockAlreadyStepped)
            {
                _clock += dt;
            }

            if (Application.isPlaying)
            {
                if (!Mathf.Approximately(_playingSpeed, speedScale))
                {
                    want.State.speed = speedScale;
                    _playingSpeed = speedScale;
                }

                if (first || (changed && ActiveAnimUpdateInterval != 1))
                {
                    want.State.speed = speedScale;
                    _playingSpeed = speedScale;
                    _animation.Play(want.Name, PlayMode.StopSameLayer);
                }
                else if (changed)
                {
                    want.State.speed = speedScale;
                    _playingSpeed = speedScale;
                    float fade = ResolveCrossfadeSeconds(_playingCategory, category);
                    _animation.CrossFade(want.Name, fade, PlayMode.StopSameLayer);
                }

                // Update-rate LOD (distant / off-screen kids sample manually on staggered cadence).
                if (ActiveAnimUpdateInterval != 1)
                {
                    _manualSampleClock += dt * speedScale;
                    bool shouldSample = false;
                    if (ActiveAnimUpdateInterval == 0)
                        shouldSample = _stateChangedSinceSample || first || changed;
                    else
                        shouldSample = _stateChangedSinceSample || ((_frameCounter + Id) % ActiveAnimUpdateInterval == 0);

                    if (shouldSample)
                    {
                        float len = want.Clip.length;
                        want.State.enabled = true;
                        want.State.weight = 1f;
                        want.State.time = len > 0.001f
                            ? (want.State.wrapMode == WrapMode.ClampForever ? Mathf.Min(_manualSampleClock, len) : _manualSampleClock % len)
                            : 0f;
                        _animation.Sample();
                        _stateChangedSinceSample = false;
                    }
                }
            }
            else
            {
                float sampleT = want.Clip.length > 0f ? (_clock * speedScale) % want.Clip.length : 0f;
                want.Clip.SampleAnimation(_animation.gameObject, sampleT);
            }

            _playingClip = want;
            _playingCategory = category;
        }

        void ApplyEightWayLocomotionBlend(CachedClip forwardClip, float rate)
        {
            if (!Application.isPlaying || !_hasEightWayClips || _animation == null || !forwardClip.IsValid) return;
            float speedSq = _smoothedVelWS.x * _smoothedVelWS.x + _smoothedVelWS.z * _smoothedVelWS.z;
            if (speedSq < 0.04f) return;

            // In Root's local frame the glTFast kid faces -Z.
            Vector3 moveDir = _smoothedVelWS / Mathf.Sqrt(speedSq);
            Vector3 fwdWS = -Root.transform.forward;
            Vector3 rightWS = -Root.transform.right;
            float fDot = Vector3.Dot(moveDir, fwdWS);
            float rDot = Vector3.Dot(moveDir, rightWS);

            float wFwd = Mathf.Max(0f, fDot);
            float wBack = Mathf.Max(0f, -fDot);
            float wRight = Mathf.Max(0f, rDot);
            float wLeft = Mathf.Max(0f, -rDot);
            float sum = wFwd + wBack + wRight + wLeft;
            if (sum < 0.001f) return;
            float inv = 1f / sum;
            wFwd *= inv;
            wBack *= inv;
            wRight *= inv;
            wLeft *= inv;

            float sharedNorm = forwardClip.State.normalizedTime % 1f;
            SyncBlendState(forwardClip, wFwd, rate, sharedNorm);
            SyncBlendState(_backpedal, wBack, rate, sharedNorm);
            SyncBlendState(_strafeR, wRight, rate, sharedNorm);
            SyncBlendState(_strafeL, wLeft, rate, sharedNorm);
        }

        static void SyncBlendState(CachedClip c, float weight, float speed, float sharedNormalizedTime)
        {
            if (!c.IsValid) return;
            c.State.enabled = weight > 0.01f;
            c.State.weight = weight;
            c.State.speed = speed;
            if (weight > 0.01f)
                c.State.normalizedTime = sharedNormalizedTime;
        }

        // ---- Deterministic A6 LateUpdate Pipeline (Zero GC, Gated by GraphicsTierProfile & Distance LOD) ------------------------

        void RunLateUpdatePass(float dt)
        {
            if (_destroyed || _model == null || _isPolymorphed || _hiddenNearCamera) return;
            if (ActiveAnimUpdateInterval == 0 && Application.isPlaying) return;

            bool lArmMod = false, rArmMod = false, lLegMod = false, rLegMod = false;

            // 0. A3 Hips leg-length height scaling + per-kid girth clearance offsets.
            ApplyHipsHeightScale();
            ApplyGirthOffsets(ref lArmMod, ref rArmMod, ref lLegMod, ref rLegMod);

            // 1. Aim offset: 40/30/30 split across Spine1 / Spine2 / Neck clamped via swing-twist.
            if (_hasAimTarget && !_lastBlocked)
                ApplyAimOffset();

            // 2. Analytic 2-bone Foot IK (grounded on slopes/curbs, pole plane preserved, clamped).
            bool runIk = _graphicsProfile.ShouldRunFootIk(_lastCamDistance, IsLocalKid)
                && !_lastBlocked && !_lastJumping && !_lastSliding && _lastAirY <= 0.02f;
            if (runIk)
                ApplyTwoBoneFootIk(dt, ref lLegMod, ref rLegMod);
            else
                _ikPelvisDrop = Mathf.MoveTowards(_ikPelvisDrop, 0f, dt * 1.5f);

            // 3. Swing-twist joint-limit clamp on every IK-touched bone.
            if (lLegMod) { ClampBoneLimit(ref _bLUpLeg); ClampBoneLimit(ref _bLLeg); ClampBoneLimit(ref _bLFoot); }
            if (rLegMod) { ClampBoneLimit(ref _bRUpLeg); ClampBoneLimit(ref _bRLeg); ClampBoneLimit(ref _bRFoot); }

            // 4. Body capsule push-out (arm/forearm vs torso, thigh vs thigh, eased smoothly over 0.10 s).
            if (!_lastBlocked)
                ApplyBodyCapsulePushOut(dt, ref lArmMod, ref rArmMod, ref lLegMod, ref rLegMod);

            if (lArmMod) { ClampBoneLimit(ref _bLArm); ClampBoneLimit(ref _bLForeArm); }
            if (rArmMod) { ClampBoneLimit(ref _bRArm); ClampBoneLimit(ref _bRForeArm); }

            // 5. Re-evaluate half-angle helpers and twist bones ONLY for modified limbs (exact sign lock #21).
            if (lArmMod) UpdateArmHelpersAndTwist(ref _bLArm, ref _bLForeArm, ref _bLHand, ref _bLArmTwist, ref _bLForeArmTwist1, ref _bLForeArmTwist2, ref _bLForeArmHelper, ref _bLHandHelper);
            if (rArmMod) UpdateArmHelpersAndTwist(ref _bRArm, ref _bRForeArm, ref _bRHand, ref _bRArmTwist, ref _bRForeArmTwist1, ref _bRForeArmTwist2, ref _bRForeArmHelper, ref _bRHandHelper);
            if (lLegMod) UpdateHalfAngleHelper(ref _bLLeg, ref _bLLegHelper);
            if (rLegMod) UpdateHalfAngleHelper(ref _bRLeg, ref _bRLegHelper);

            // 6. Verlet spring bones on hair/skirt/cape leaf chains (High & Ultra ONLY per docs/GRAPHICS.md).
            if (_graphicsProfile.ShouldRunSprings(_lastCamDistance) && _springChains.Length > 0 && dt > 0f)
                StepSpringChains(dt);

            // 7. Pose-space corrective blendshape driver + optional face blink/expression driver.
            if (_graphicsProfile.ShouldRunCorrectives(_lastCamDistance, IsLocalKid) && _correctives.Length > 0)
                EvaluateCorrectives();

            if (_faceMesh != null)
                StepFaceDriver(dt);

            _lastLatePosWS = Root.transform.position;
            _hasLastLatePos = true;
        }

        void ApplyHipsHeightScale()
        {
            if (!_bHips.Valid || Mathf.Abs(_legLengthRatio - 1f) < 0.002f) return;
            Vector3 lp = _bHips.Transform.localPosition;
            lp.y = _bHips.RestLocalPos.y + (lp.y - _bHips.RestLocalPos.y) * _legLengthRatio;
            _bHips.Transform.localPosition = lp;
        }

        void ApplyGirthOffsets(ref bool lArmMod, ref bool rArmMod, ref bool lLegMod, ref bool rLegMod)
        {
            if (_girthArmAbductDeg > 0.05f)
            {
                if (_bLArm.Valid) { ApplyAbductOffset(ref _bLArm, _girthArmAbductDeg); lArmMod = true; }
                if (_bRArm.Valid) { ApplyAbductOffset(ref _bRArm, _girthArmAbductDeg); rArmMod = true; }
            }
            if (_girthHipSpreadDeg > 0.05f)
            {
                if (_bLUpLeg.Valid) { ApplyAbductOffset(ref _bLUpLeg, _girthHipSpreadDeg); lLegMod = true; }
                if (_bRUpLeg.Valid) { ApplyAbductOffset(ref _bRUpLeg, _girthHipSpreadDeg); rLegMod = true; }
            }
        }

        static void ApplyAbductOffset(ref BoneCache bone, float deg)
        {
            Vector3 axis = bone.HasLimit ? bone.Limit.AbductAxis : Vector3.forward;
            bone.Transform.localRotation = bone.Transform.localRotation * Quaternion.AngleAxis(deg, axis);
        }

        public static void ComputeLocalAimAngles(
            Vector3 localDir,
            float maxYawDeg,
            float maxPitchDeg,
            out float yawDeg,
            out float pitchDeg)
        {
            // Model faces -Z, +X is kid's LEFT; Spine/Neck local_unity.twist = [0, -1, 0] turns toward +X (LEFT) for +yawDeg.
            yawDeg = Mathf.Clamp(Mathf.Atan2(localDir.x, -localDir.z) * Mathf.Rad2Deg, -maxYawDeg, maxYawDeg);
            float horiz = Mathf.Sqrt(localDir.x * localDir.x + localDir.z * localDir.z);
            pitchDeg = Mathf.Clamp(Mathf.Atan2(localDir.y, Mathf.Max(0.001f, horiz)) * Mathf.Rad2Deg, -maxPitchDeg, maxPitchDeg);
        }

        void ApplyAimOffset()
        {
            if (!_bSpine1.Valid && !_bSpine2.Valid && !_bNeck.Valid) return;

            Vector3 pivot = _bSpine2.Valid ? _bSpine2.Transform.position : Root.transform.position + Vector3.up * 1.1f;
            Vector3 toAim = _aimTargetWS - pivot;
            if (toAim.sqrMagnitude < 0.04f) return;

            // Convert world aim direction into Root local space (model faces -Z).
            Vector3 localDir = Root.transform.InverseTransformDirection(toAim.normalized);
            ComputeLocalAimAngles(localDir, _kidAdaptation.AimYawMaxDeg, _kidAdaptation.AimPitchMaxDeg, out float yawDeg, out float pitchDeg);

            ApplyAimShare(ref _bSpine1, yawDeg * 0.40f, pitchDeg * 0.40f);
            ApplyAimShare(ref _bSpine2, yawDeg * 0.30f, pitchDeg * 0.30f);
            ApplyAimShare(ref _bNeck, yawDeg * 0.30f, pitchDeg * 0.30f);
        }

        static void ApplyAimShare(ref BoneCache bone, float yawDeg, float pitchDeg)
        {
            if (!bone.Valid) return;
            Vector3 yawAxis = bone.HasLimit ? bone.Limit.TwistAxis : Vector3.up;
            Vector3 pitchAxis = bone.HasLimit ? bone.Limit.FlexAxis : Vector3.right;
            Quaternion offset = Quaternion.AngleAxis(yawDeg, yawAxis) * Quaternion.AngleAxis(-pitchDeg, pitchAxis);
            bone.Transform.localRotation = bone.Transform.localRotation * offset;
            ClampBoneLimit(ref bone);
        }

        void ApplyTwoBoneFootIk(float dt, ref bool lLegMod, ref bool rLegMod)
        {
            if (!_bLUpLeg.Valid || !_bLLeg.Valid || !_bLFoot.Valid || !_bRUpLeg.Valid || !_bRLeg.Valid || !_bRFoot.Valid)
                return;

            float groundY = Root.transform.position.y;
            bool hitL = ProbeFootGround(_bLFoot.Transform.position, groundY, out Vector3 targetL, out Vector3 normalL);
            bool hitR = ProbeFootGround(_bRFoot.Transform.position, groundY, out Vector3 targetR, out Vector3 normalR);
            if (!hitL && !hitR) return;

            float dropL = hitL ? Mathf.Max(0f, groundY - targetL.y) : 0f;
            float dropR = hitR ? Mathf.Max(0f, groundY - targetR.y) : 0f;
            float targetDrop = Mathf.Clamp(Mathf.Max(dropL, dropR), 0f, MaxIkPelvisDrop);
            _ikPelvisDrop = dt > 0f ? Mathf.MoveTowards(_ikPelvisDrop, targetDrop, dt * 2.2f) : targetDrop;

            if (_bHips.Valid && _ikPelvisDrop > 0.001f)
            {
                Vector3 hipWorld = _bHips.Transform.position;
                hipWorld.y -= _ikPelvisDrop;
                _bHips.Transform.position = hipWorld;
            }

            if (hitL && SolveTwoBoneIk(_bLUpLeg.Transform, _bLLeg.Transform, _bLFoot.Transform, targetL, normalL))
                lLegMod = true;
            if (hitR && SolveTwoBoneIk(_bRUpLeg.Transform, _bRLeg.Transform, _bRFoot.Transform, targetR, normalR))
                rLegMod = true;
        }

        static bool ProbeFootGround(Vector3 footPosWS, float baseGroundY, out Vector3 hitPoint, out Vector3 hitNormal)
        {
            // Only raycast when foot is near the ground stance window; avoids interfering with high-swing footsteps.
            if (footPosWS.y > baseGroundY + 0.35f)
            {
                hitPoint = footPosWS;
                hitNormal = Vector3.up;
                return false;
            }

            Vector3 origin = new Vector3(footPosWS.x, baseGroundY + 0.50f, footPosWS.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 0.90f, ~0, QueryTriggerInteraction.Ignore))
            {
                hitPoint = hit.point;
                hitNormal = hit.normal;
                return Mathf.Abs(hit.point.y - baseGroundY) > 0.005f || Vector3.Dot(hit.normal, Vector3.up) < 0.998f;
            }

            hitPoint = new Vector3(footPosWS.x, baseGroundY, footPosWS.z);
            hitNormal = Vector3.up;
            return false;
        }

        static bool SolveTwoBoneIk(Transform upper, Transform mid, Transform end, Vector3 targetWS, Vector3 groundNormal)
        {
            Vector3 pA = upper.position;
            Vector3 pB = mid.position;
            Vector3 pC = end.position;

            float lenA = Vector3.Distance(pA, pB);
            float lenB = Vector3.Distance(pB, pC);
            if (lenA < 0.02f || lenB < 0.02f) return false;

            // Preserve FK foot lift height above ground while adapting to slope delta.
            Vector3 toTarget = targetWS - pA;
            float maxReach = (lenA + lenB) * 0.998f; // A6.4: stop at full extension minus ~1 deg
            float minReach = Mathf.Abs(lenA - lenB) + 0.02f;
            float dist = Mathf.Clamp(toTarget.magnitude, minReach, maxReach);
            if (dist < 0.001f) return false;

            Vector3 ab = pB - pA;
            Vector3 ac = pC - pA;
            float acLen = ac.magnitude;
            if (acLen < 0.001f) return false;

            // Keep knee on the clip's existing pole plane.
            Vector3 poleNormal = Vector3.Cross(ac, ab);
            if (poleNormal.sqrMagnitude < 1e-6f) poleNormal = Vector3.Cross(ac, upper.right);
            if (poleNormal.sqrMagnitude < 1e-6f) return false;
            poleNormal.Normalize();

            float curAngleA = Mathf.Acos(Mathf.Clamp((lenA * lenA + acLen * acLen - lenB * lenB) / (2f * lenA * acLen), -1f, 1f));
            float wantAngleA = Mathf.Acos(Mathf.Clamp((lenA * lenA + dist * dist - lenB * lenB) / (2f * lenA * dist), -1f, 1f));
            float curAngleB = Mathf.Acos(Mathf.Clamp((lenA * lenA + lenB * lenB - acLen * acLen) / (2f * lenA * lenB), -1f, 1f));
            float wantAngleB = Mathf.Acos(Mathf.Clamp((lenA * lenA + lenB * lenB - dist * dist) / (2f * lenA * lenB), -1f, 1f));

            Quaternion swingUpper = Quaternion.FromToRotation(ac / acLen, toTarget / toTarget.magnitude);
            Quaternion bendUpper = Quaternion.AngleAxis((wantAngleA - curAngleA) * Mathf.Rad2Deg, poleNormal);
            upper.rotation = swingUpper * bendUpper * upper.rotation;

            Vector3 newPoleNormal = swingUpper * poleNormal;
            Quaternion bendMid = Quaternion.AngleAxis((curAngleB - wantAngleB) * Mathf.Rad2Deg, newPoleNormal);
            mid.rotation = bendMid * mid.rotation;

            // Align ankle gently to ground slope normal.
            if (groundNormal.sqrMagnitude > 0.5f)
            {
                Quaternion slopeAlign = Quaternion.FromToRotation(Vector3.up, groundNormal.normalized);
                end.rotation = Quaternion.Slerp(end.rotation, slopeAlign * end.rotation, 0.75f);
            }
            return true;
        }

        static void ClampBoneLimit(ref BoneCache bone)
        {
            if (!bone.Valid || !bone.HasLimit) return;
            Quaternion delta = bone.InvRestLocalRot * bone.Transform.localRotation;
            Quaternion clamped = RigJointLimitsData.ClampSwingTwist(delta, in bone.Limit);
            bone.Transform.localRotation = bone.RestLocalRot * clamped;
        }

        void ApplyBodyCapsulePushOut(float dt, ref bool lArmMod, ref bool rArmMod, ref bool lLegMod, ref bool rLegMod)
        {
            if (_capsules == null || _capsules.Length == 0) return;
            float maxStep = dt > 0f ? (dt / PushOutEaseTime) * 35f : 35f;

            // Left / Right Arm vs Torso
            if (_torsoCapsuleIdx >= 0)
            {
                RuntimeCapsule torso = _capsules[_torsoCapsuleIdx];
                if (_lArmCapsuleIdx >= 0 && _bLArm.Valid)
                {
                    float want = ComputeCapsulePushDeg(_capsules[_lArmCapsuleIdx], torso, _lForeArmCapsuleIdx >= 0 ? _capsules[_lForeArmCapsuleIdx] : default);
                    _pushLArmDeg = Mathf.MoveTowards(_pushLArmDeg, want, maxStep);
                    if (_pushLArmDeg > 0.05f) { ApplyAbductOffset(ref _bLArm, _pushLArmDeg); lArmMod = true; }
                }
                if (_rArmCapsuleIdx >= 0 && _bRArm.Valid)
                {
                    float want = ComputeCapsulePushDeg(_capsules[_rArmCapsuleIdx], torso, _rForeArmCapsuleIdx >= 0 ? _capsules[_rForeArmCapsuleIdx] : default);
                    _pushRArmDeg = Mathf.MoveTowards(_pushRArmDeg, want, maxStep);
                    if (_pushRArmDeg > 0.05f) { ApplyAbductOffset(ref _bRArm, _pushRArmDeg); rArmMod = true; }
                }
            }

            // Left Thigh vs Right Thigh
            if (_lUpLegCapsuleIdx >= 0 && _rUpLegCapsuleIdx >= 0 && _bLUpLeg.Valid && _bRUpLeg.Valid)
            {
                float overlap = CapsuleOverlapDepth(_capsules[_lUpLegCapsuleIdx], _capsules[_rUpLegCapsuleIdx]);
                float want = overlap > 0.001f ? Mathf.Clamp(overlap * 140f, 0f, 22f) : 0f;
                _pushLLegDeg = Mathf.MoveTowards(_pushLLegDeg, want * 0.5f, maxStep);
                _pushRLegDeg = Mathf.MoveTowards(_pushRLegDeg, want * 0.5f, maxStep);
                if (_pushLLegDeg > 0.05f) { ApplyAbductOffset(ref _bLUpLeg, _pushLLegDeg); lLegMod = true; }
                if (_pushRLegDeg > 0.05f) { ApplyAbductOffset(ref _bRUpLeg, _pushRLegDeg); rLegMod = true; }
            }
        }

        static float ComputeCapsulePushDeg(RuntimeCapsule arm, RuntimeCapsule torso, RuntimeCapsule foreArm)
        {
            float d1 = CapsuleOverlapDepth(arm, torso);
            float d2 = foreArm.Valid ? CapsuleOverlapDepth(foreArm, torso) : 0f;
            float maxDepth = Mathf.Max(d1, d2);
            return maxDepth > 0.001f ? Mathf.Clamp(maxDepth * 190f, 0f, 32f) : 0f;
        }

        static float CapsuleOverlapDepth(RuntimeCapsule a, RuntimeCapsule b)
        {
            if (!a.Valid || !b.Valid) return 0f;
            Vector3 a0 = a.Bone.TransformPoint(a.LocalA);
            Vector3 a1 = a.Bone.TransformPoint(a.LocalB);
            Vector3 b0 = b.Bone.TransformPoint(b.LocalA);
            Vector3 b1 = b.Bone.TransformPoint(b.LocalB);
            float dist = SegmentSegmentDistance(a0, a1, b0, b1);
            return Mathf.Max(0f, (a.Radius + b.Radius) - dist);
        }

        static float SegmentSegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1;
            Vector3 d2 = q2 - p2;
            Vector3 r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);
            float s, t;

            if (a <= 1e-6f && e <= 1e-6f) return Vector3.Distance(p1, p2);
            if (a <= 1e-6f)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-6f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom != 0f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }

            Vector3 c1 = p1 + d1 * s;
            Vector3 c2 = p2 + d2 * t;
            return Vector3.Distance(c1, c2);
        }

        // Exact R2 / #21 sign lock for ArmTwist (-0.5), ForeArmTwist1/2 (+0.33 / +0.66), and half-angle helpers (0.5).
        static void UpdateArmHelpersAndTwist(
            ref BoneCache arm, ref BoneCache foreArm, ref BoneCache hand,
            ref BoneCache armTwist, ref BoneCache foreTwist1, ref BoneCache foreTwist2,
            ref BoneCache foreHelper, ref BoneCache handHelper)
        {
            if (arm.Valid && armTwist.Valid)
            {
                Quaternion armDelta = arm.InvRestLocalRot * arm.Transform.localRotation;
                Vector3 axis = arm.HasLimit ? arm.Limit.TwistAxis : Vector3.up;
                RigJointLimitsData.DecomposeSwingTwist(armDelta, axis, out _, out _, out float twistDeg);
                armTwist.Transform.localRotation = armTwist.RestLocalRot * Quaternion.AngleAxis(-0.50f * twistDeg, axis);
            }

            UpdateHalfAngleHelper(ref foreArm, ref foreHelper);
            UpdateHalfAngleHelper(ref hand, ref handHelper);

            if (hand.Valid && (foreTwist1.Valid || foreTwist2.Valid))
            {
                Quaternion handDelta = hand.InvRestLocalRot * hand.Transform.localRotation;
                Vector3 handAxis = hand.HasLimit ? hand.Limit.TwistAxis : Vector3.up;
                Vector3 foreAxis = foreArm.HasLimit ? foreArm.Limit.TwistAxis : Vector3.up;
                RigJointLimitsData.DecomposeSwingTwist(handDelta, handAxis, out _, out _, out float twistDeg);
                if (foreTwist1.Valid)
                    foreTwist1.Transform.localRotation = foreTwist1.RestLocalRot * Quaternion.AngleAxis(0.33f * twistDeg, foreAxis);
                if (foreTwist2.Valid)
                    foreTwist2.Transform.localRotation = foreTwist2.RestLocalRot * Quaternion.AngleAxis(0.66f * twistDeg, foreAxis);
            }
        }

        static void UpdateHalfAngleHelper(ref BoneCache source, ref BoneCache helper)
        {
            if (!source.Valid || !helper.Valid) return;
            Quaternion childDelta = source.InvRestLocalRot * source.Transform.localRotation;
            if (childDelta.w < 0f)
                childDelta = new Quaternion(-childDelta.x, -childDelta.y, -childDelta.z, -childDelta.w);
            helper.Transform.localRotation = helper.RestLocalRot * Quaternion.Slerp(Quaternion.identity, childDelta, 0.5f);
        }

        // ---- Zero-GC Verlet Spring Bones (High & Ultra ONLY per docs/GRAPHICS.md) -----------------------------------------------

        void ResetSpringChains()
        {
            for (int c = 0; c < _springChains.Length; c++)
                _springChains[c].Initialized = false;
        }

        void StepSpringChains(float dt)
        {
            float clampedDt = Mathf.Min(dt, 0.04f);
            bool teleported = _hasLastLatePos && (Root.transform.position - _lastLatePosWS).sqrMagnitude > SpringResetTeleportSq;

            for (int c = 0; c < _springChains.Length; c++)
            {
                RuntimeSpringChain chain = _springChains[c];
                SpringNode[] nodes = chain.Nodes;
                if (nodes == null || nodes.Length == 0) continue;

                if (!chain.Initialized || teleported)
                {
                    for (int n = 0; n < nodes.Length; n++)
                    {
                        Transform tr = nodes[n].Bone;
                        if (tr == null) continue;
                        Vector3 tip = tr.position + tr.TransformDirection(nodes[n].RestLocalDir) * nodes[n].Length;
                        nodes[n].CurTipWS = tip;
                        nodes[n].PrevTipWS = tip;
                    }
                    chain.Initialized = true;
                    continue;
                }

                float drag = Mathf.Clamp01(1f - chain.Damping);
                Vector3 gravStep = Vector3.down * (chain.Gravity * clampedDt * clampedDt * 9.81f);

                for (int n = 0; n < nodes.Length; n++)
                {
                    Transform tr = nodes[n].Bone;
                    Transform parent = nodes[n].Parent;
                    if (tr == null || parent == null) continue;

                    Vector3 origin = tr.position;
                    Vector3 restDirWS = (parent.rotation * nodes[n].RestLocalRot) * nodes[n].RestLocalDir;
                    Vector3 restTipWS = origin + restDirWS * nodes[n].Length;

                    // Verlet integration
                    Vector3 vel = (nodes[n].CurTipWS - nodes[n].PrevTipWS) * drag;
                    nodes[n].PrevTipWS = nodes[n].CurTipWS;
                    Vector3 next = nodes[n].CurTipWS + vel + gravStep;
                    next = Vector3.Lerp(next, restTipWS, chain.Stiffness);

                    // 2 constraint + capsule collision iterations (A6.7).
                    for (int iter = 0; iter < 2; iter++)
                    {
                        Vector3 offset = next - origin;
                        float mag = offset.magnitude;
                        next = mag > 1e-5f ? origin + (offset / mag) * nodes[n].Length : restTipWS;

                        if (chain.ColliderIndices != null && _capsules != null)
                        {
                            for (int ci = 0; ci < chain.ColliderIndices.Length; ci++)
                            {
                                int capIdx = chain.ColliderIndices[ci];
                                if (capIdx < 0 || capIdx >= _capsules.Length) continue;
                                PushPointOutsideCapsule(ref next, _capsules[capIdx], margin: 0.015f);
                            }
                        }
                    }

                    nodes[n].CurTipWS = next;

                    Vector3 finalDirWS = next - origin;
                    if (finalDirWS.sqrMagnitude > 1e-6f)
                    {
                        Quaternion aim = Quaternion.FromToRotation(restDirWS, finalDirWS.normalized);
                        tr.rotation = aim * (parent.rotation * nodes[n].RestLocalRot);
                    }
                }
            }
        }

        static void PushPointOutsideCapsule(ref Vector3 pointWS, RuntimeCapsule cap, float margin)
        {
            if (!cap.Valid) return;
            Vector3 a = cap.Bone.TransformPoint(cap.LocalA);
            Vector3 b = cap.Bone.TransformPoint(cap.LocalB);
            Vector3 ab = b - a;
            float abSq = Vector3.Dot(ab, ab);
            float t = abSq > 1e-6f ? Mathf.Clamp01(Vector3.Dot(pointWS - a, ab) / abSq) : 0f;
            Vector3 closest = a + ab * t;
            Vector3 diff = pointWS - closest;
            float distSq = diff.sqrMagnitude;
            float minR = cap.Radius + margin;
            if (distSq < minR * minR)
            {
                float dist = Mathf.Sqrt(distSq);
                Vector3 dir = dist > 1e-5f ? diff / dist : Vector3.forward;
                pointWS = closest + dir * minR;
            }
        }

        // ---- Pose-Space Corrective Blendshapes (R5 / A6.8) & Face Driver (R6) ---------------------------------------------------

        static int ResolveActiveCorrectiveIndex(in CorrectiveBinding b)
        {
            if (b.Mesh == null || b.Mesh.sharedMesh == null) return -1;
            Mesh cur = b.Mesh.sharedMesh;
            if (ReferenceEquals(cur, b.Lod0Mesh)) return b.ShapeIndexLod0;
            if (ReferenceEquals(cur, b.Lod1Mesh)) return b.ShapeIndexLod1;
            return -1;
        }

        void ZeroCorrectiveBlendShapes()
        {
            for (int i = 0; i < _correctives.Length; i++)
            {
                CorrectiveBinding b = _correctives[i];
                int shapeIdx = ResolveActiveCorrectiveIndex(in b);
                if (shapeIdx >= 0 && shapeIdx < b.Mesh.sharedMesh.blendShapeCount)
                    b.Mesh.SetBlendShapeWeight(shapeIdx, 0f);
                b.NormalizedWeight = 0f;
                _correctives[i] = b;
            }
        }

        void EvaluateCorrectives()
        {
            // Pass 1: single-joint hinge, cone, and squash shapes.
            for (int i = 0; i < _correctives.Length; i++)
            {
                CorrectiveBinding b = _correctives[i];
                if (b.Def == null || b.Def.Kind == "combo") continue;
                int shapeIdx = ResolveActiveCorrectiveIndex(in b);
                if (shapeIdx < 0 || shapeIdx >= b.Mesh.sharedMesh.blendShapeCount) continue;

                float angleDeg = 0f;
                if (b.Bone != null)
                {
                    if (b.Def.Kind == "cone")
                    {
                        Quaternion delta = Quaternion.Inverse(b.BoneRestLocalRot) * b.Bone.localRotation;
                        Vector3 curDir = delta * b.LocalBoneRestDir;
                        float dot = Mathf.Clamp(Vector3.Dot(curDir.normalized, b.LocalTargetDir), -1f, 1f);
                        float curToTarget = Mathf.Acos(dot) * Mathf.Rad2Deg;
                        angleDeg = Mathf.Max(0f, b.RestAngleToTargetDeg - curToTarget);
                    }
                    else
                    {
                        // Hinge & squash: signed angle about stored bone-local hinge axis relative to rest.
                        Quaternion delta = Quaternion.Inverse(b.BoneRestLocalRot) * b.Bone.localRotation;
                        RigJointLimitsData.DecomposeSwingTwist(delta, b.LocalAxis, out _, out _, out float signedDeg);
                        angleDeg = Mathf.Abs(signedDeg - b.Def.RestAngleDeg);
                    }
                }

                float span = Mathf.Max(1f, b.Def.FullAngleDeg - b.Def.StartAngleDeg);
                float w01 = Mathf.Clamp01((angleDeg - b.Def.StartAngleDeg) / span);
                b.NormalizedWeight = w01;
                _correctives[i] = b;
                b.Mesh.SetBlendShapeWeight(shapeIdx, w01 * 100f);
            }

            // Pass 2: combination shapes (product of two single corrective weights).
            for (int i = 0; i < _correctives.Length; i++)
            {
                CorrectiveBinding b = _correctives[i];
                if (b.Def == null || b.Def.Kind != "combo") continue;
                int shapeIdx = ResolveActiveCorrectiveIndex(in b);
                if (shapeIdx < 0 || shapeIdx >= b.Mesh.sharedMesh.blendShapeCount) continue;

                float wA = (b.ComboIndexA >= 0 && b.ComboIndexA < _correctives.Length) ? _correctives[b.ComboIndexA].NormalizedWeight : 0f;
                float wB = (b.ComboIndexB >= 0 && b.ComboIndexB < _correctives.Length) ? _correctives[b.ComboIndexB].NormalizedWeight : 0f;
                float prod = Mathf.Clamp01(wA * wB);
                b.NormalizedWeight = prod;
                _correctives[i] = b;
                b.Mesh.SetBlendShapeWeight(shapeIdx, prod * 100f);
            }
        }

        void StepFaceDriver(float dt)
        {
            Mesh m = _faceMesh.sharedMesh;
            if (m == null || !ReferenceEquals(m, _faceSourceMesh) || m.blendShapeCount == 0) return;

            // Randomized 2-6s blink cycle (0.14 s smooth close/open).
            float blinkW = 0f;
            if (_bsBlinkL >= 0 || _bsBlinkR >= 0)
            {
                if (_blinkPhase > 0f)
                {
                    _blinkPhase += dt / 0.14f;
                    if (_blinkPhase >= 1f)
                    {
                        _blinkPhase = 0f;
                        _blinkTimer = NextBlinkInterval();
                    }
                    else
                    {
                        blinkW = Mathf.Sin(_blinkPhase * Mathf.PI) * 100f;
                    }
                }
                else
                {
                    _blinkTimer -= dt;
                    if (_blinkTimer <= 0f) _blinkPhase = 0.001f;
                }
                SetFaceWeight(m, _bsBlinkL, blinkW);
                SetFaceWeight(m, _bsBlinkR, blinkW);
            }

            SetFaceWeight(m, _bsHurt, (_hitTimer > 0f || CurrentPose == "stunned" || CurrentPose == "tripped") ? 85f : 0f);
            SetFaceWeight(m, _bsAngry, (CurrentPose == "cast" || CurrentPose == "dash") ? 55f : 0f);
            SetFaceWeight(m, _bsSmile, (!string.IsNullOrEmpty(_appliedBuffId) && _appliedBuffId != "0" && !_appliedTaggedDim) ? 45f : 0f);
            SetFaceWeight(m, _bsSurprised, CurrentPose == "airborne" ? 60f : 0f);
            SetFaceWeight(m, _bsOpen, CurrentPose == "cast" ? 30f : 0f);
        }

        void SetFaceWeight(Mesh m, int shapeIndex, float weight)
        {
            if (shapeIndex >= 0 && shapeIndex < m.blendShapeCount)
                _faceMesh.SetBlendShapeWeight(shapeIndex, weight);
        }

        float NextBlinkInterval()
        {
            _viewRngState = _viewRngState * 1664525u + 1013904223u;
            float u = (_viewRngState & 0x00FFFFFFu) / 16777216f;
            return 2.0f + u * 4.0f; // 2..6 s per apex-anim-craft A6.8
        }

        // ---- Tagged Vanish, Polymorph & Procedural Postures ---------------------------------------------------------------------

        void StepTaggedVanish(Vector3 targetWorld, bool tagged, float dt)
        {
            if (tagged && !_wasTagged && _hasPlacedOnce && Application.isPlaying && dt > 0f)
            {
                _vanishStage = 1;
                _vanishProgress = 0f;
                _vanishWorldPos = _lastGroundWorld;
                _castTimer = 0f;
            }
            else if (!tagged)
            {
                _vanishStage = 0;
            }
            _wasTagged = tagged;
            _tagPopScale = 1f;

            if (_vanishStage == 1)
            {
                _vanishProgress += dt / PopOutTime;
                Root.transform.position = _vanishWorldPos;
                _tagPopScale = PopScale(1f - _vanishProgress, false);
                if (_vanishProgress >= 1f)
                {
                    _vanishStage = 2;
                    _vanishProgress = 0f;
                    _tagPopScale = GoneScale;
                    Root.transform.position = targetWorld;
                }
            }
            else if (_vanishStage == 2)
            {
                _vanishProgress += dt / PopInTime;
                Root.transform.position = targetWorld;
                _tagPopScale = PopScale(_vanishProgress, true);
                if (_vanishProgress >= 1f)
                {
                    _vanishStage = 0;
                    _tagPopScale = 1f;
                }
            }
            else
            {
                Root.transform.position = targetWorld;
            }

            _lastGroundWorld = targetWorld;
            _hasPlacedOnce = true;
        }

        void StepPolymorph(bool polymorphed, float speed01, float dt)
        {
            _isPolymorphed = polymorphed;
            if (!Application.isPlaying || dt <= 0f)
            {
                _kidPop = polymorphed ? 0f : 1f;
                _canPop = polymorphed ? 1f : 0f;
                _kidPopUp = !polymorphed;
                _canPopUp = polymorphed;
            }
            else
            {
                float kidTarget = (!polymorphed && _canPop <= 0.001f) ? 1f : 0f;
                float canTarget = (polymorphed && _kidPop <= 0.001f) ? 1f : 0f;
                _kidPopUp = kidTarget > _kidPop;
                _kidPop = MovePop(_kidPop, kidTarget, dt);
                _canPopUp = canTarget > _canPop;
                _canPop = MovePop(_canPop, canTarget, dt);
            }

            float canScale = Mathf.Max(GoneScale, PopScale(_canPop, _canPopUp));
            _canPivot.localScale = Vector3.one * canScale;
            if (_canPop > 0.001f)
            {
                float moveAmt = Mathf.Clamp01(speed01);
                float hopY = Mathf.Abs(Mathf.Sin(_clock * 10f)) * 0.08f * _viewScale * moveAmt;
                float wobbleDeg = Mathf.Sin(_clock * 10f) * 7.5f * moveAmt;
                _canPivot.localPosition = new Vector3(0f, hopY, 0f);
                _canPivot.localRotation = Quaternion.Euler(0f, 0f, wobbleDeg);
            }
            else
            {
                _canPivot.localPosition = Vector3.zero;
                _canPivot.localRotation = Quaternion.identity;
            }
        }

        void ApplyProceduralBodyPose(
            float airY,
            bool tagged,
            bool isStun,
            bool isStatusAir,
            bool isTripped,
            bool jumping,
            float hitFlash01)
        {
            float liftY = Mathf.Max(0f, airY);
            if (isStatusAir && liftY < 0.02f)
                liftY = (0.18f + 0.14f * Mathf.Abs(Mathf.Sin(_clock * 4.5f))) * _viewScale;

            // In Root's local frame the glTFast kid faces -Z, so negative X pitch leans forward and positive X leans back.
            float pitchDeg = 0f;
            float rollDeg = 0f;
            float squashY = 1f;

            if (tagged)
            {
                squashY = _hasNativeSit ? 0.94f : 0.82f;
                pitchDeg = -8f;
            }
            else if (isStun)
            {
                rollDeg = Mathf.Sin(_clock * 7f) * 5.5f;
                pitchDeg = Mathf.Cos(_clock * 5f) * 4.5f;
            }
            else if (isTripped)
            {
                float p = _tripTimer > 0f ? (1f - Mathf.Clamp01(_tripTimer / 0.85f)) : 0.5f;
                float recover = p > 0.62f ? (p - 0.62f) / 0.38f : 0f;
                pitchDeg = Mathf.Lerp(-26f, 0f, recover);
                squashY = Mathf.Lerp(0.78f, 1f, recover);
            }
            else
            {
                if (_dashBlend > 0.001f)
                {
                    pitchDeg = Mathf.Lerp(pitchDeg, -20f, _dashBlend);
                    squashY = Mathf.Lerp(squashY, 0.91f, _dashBlend);
                }
                if (_slideBlend > 0.001f)
                {
                    pitchDeg = Mathf.Lerp(pitchDeg, _hasNativeSlide ? 8f : 24f, _slideBlend);
                    squashY = Mathf.Lerp(squashY, _hasNativeSlide ? 0.86f : 0.64f, _slideBlend);
                }
                if (_crouchBlend > 0.001f)
                {
                    pitchDeg = Mathf.Lerp(pitchDeg, _hasNativeCrouch ? -4f : -13f, _crouchBlend);
                    squashY = Mathf.Lerp(squashY, _hasNativeCrouch ? 0.90f : 0.72f, _crouchBlend);
                }
                if (jumping)
                {
                    squashY *= 1.05f;
                    pitchDeg -= 5f;
                }
                else if (_landTimer > 0f)
                {
                    float lk = Mathf.Sin((_landTimer / LandDuration) * Mathf.PI);
                    squashY *= 1f - 0.12f * lk;
                    pitchDeg -= 4f * lk;
                }
            }

            if (hitFlash01 > 0.01f) pitchDeg += 8f * hitFlash01;

            float across = 1f / Mathf.Sqrt(Mathf.Max(squashY, 0.25f));
            float pop = Mathf.Max(GoneScale, _tagPopScale * PopScale(_kidPop, _kidPopUp));

            _bodyPivot.localPosition = new Vector3(0f, liftY, 0f);
            _bodyPivot.localRotation = Quaternion.Euler(pitchDeg, 0f, rollDeg);
            _bodyPivot.localScale = new Vector3(across * pop, squashY * pop, across * pop);
        }

        // ---- Visibility & MaterialPropertyBlock Sync (Valorant Enemy Highlight + SRP Batcher Fast Path) -------------------------

        bool ResolveIsEnemy()
        {
            if (_hasExplicitEnemyFlag) return _isEnemy;
            // MatchView passes enemy red/orange/yellow (high R relative to B) vs ally blue (high B).
            return _teamColor.r > _teamColor.b + 0.15f;
        }

        Color ResolveActiveHighlightColor(bool isEnemy)
        {
            if (!isEnemy) return _teamColor;
            if (_hasExplicitEnemyFlag) return _explicitEnemyColor;
            return KalyeahResources.ResolveEnemyHighlightColor(SettingsStore.Current.ResolvedEnemyHighlightMode);
        }

        void RefreshRendererVisibility()
        {
            bool showKid = !_hiddenNearCamera && _kidPop > 0.001f;
            bool showCan = !_hiddenNearCamera && _canPop > 0.001f;
            bool isEnemy = ResolveIsEnemy();

            if (showKid != _kidRenderersVisible)
            {
                _kidRenderersVisible = showKid;
                SetRendererGroupEnabled(_kidBaseRenderers, showKid);
                // docs/GRAPHICS.md: allies stay plain (subtle back-rim only); enemies wear the Valorant highlight.
                SetRendererGroupEnabled(_kidOutlineRenderers, showKid && isEnemy);
            }
            if (showCan != _canRenderersVisible)
            {
                _canRenderersVisible = showCan;
                SetRendererGroupEnabled(_canBaseRenderers, showCan);
                SetRendererGroupEnabled(_canOutlineRenderers, showCan && isEnemy);
            }
        }

        static void SetRendererGroupEnabled(List<Renderer> list, bool enabled)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) list[i].enabled = enabled;
        }

        void ApplyMaterialBlocks(float hitFlash01, string buffId, bool tagged)
        {
            int flashStep = Mathf.RoundToInt(hitFlash01 * 24f);
            bool hasBuff = TryResolveBuffAura(buffId, out Color auraRgb);
            int auraStep = hasBuff ? Mathf.RoundToInt((0.5f + 0.5f * Mathf.Sin(_clock * 5.5f)) * 12f) : 0;
            int highlightMode = SettingsStore.Current.ResolvedEnemyHighlightMode;

            if (!_materialStateDirty
                && flashStep == _appliedFlashStep
                && auraStep == _appliedAuraStep
                && tagged == _appliedTaggedDim
                && highlightMode == _appliedHighlightMode
                && string.Equals(buffId, _appliedBuffId, StringComparison.Ordinal))
                return;

            _materialStateDirty = false;
            _appliedFlashStep = flashStep;
            _appliedAuraStep = auraStep;
            _appliedTaggedDim = tagged;
            _appliedHighlightMode = highlightMode;
            _appliedBuffId = buffId;

            float flash = flashStep / 24f;
            float auraPulse = hasBuff ? (0.45f + 0.30f * (auraStep / 12f)) : 0f;
            bool isEnemy = ResolveIsEnemy();
            Color highlightCol = ResolveActiveHighlightColor(isEnemy);

            // Keep outline hulls active strictly for enemies (allies stay clean with Valorant back-rim light).
            SetRendererGroupEnabled(_kidOutlineRenderers, _kidRenderersVisible && isEnemy);
            SetRendererGroupEnabled(_canOutlineRenderers, _canRenderersVisible && isEnemy);

            if (HasTeamOutline && isEnemy)
            {
                Color outlineCol = tagged
                    ? new Color(highlightCol.r * 0.72f, highlightCol.g * 0.72f, highlightCol.b * 0.72f, 0.65f)
                    : highlightCol;
                _outlineBlock.Clear();
                _outlineBlock.SetColor(OutlineColorId, outlineCol);
                _outlineBlock.SetFloat(OutlineWidthId, DefaultOutlineWidth);
                _outlineBlock.SetFloat(HitFlashId, flash);
                _outlineBlock.SetColor(AuraColorId, hasBuff ? new Color(auraRgb.r, auraRgb.g, auraRgb.b, auraPulse) : Color.clear);
                ApplyBlockToGroup(_kidOutlineRenderers, _outlineBlock);
                ApplyBlockToGroup(_canOutlineRenderers, _outlineBlock);
            }

            // Pure SRP Batcher fast-path for clean allies with no flash, buff, or tagged dim
            // (base Kalyeah/Kid material already carries neutral-blue _AllyFresnelColor / _AllyFresnelStrength defaults).
            if (!isEnemy && flash <= 0.001f && !hasBuff && !tagged)
            {
                ApplyBlockToGroup(_kidBaseRenderers, null);
                ApplyBlockToGroup(_canBaseRenderers, null);
                return;
            }

            Color baseTint = tagged ? new Color(0.78f, 0.80f, 0.86f, 1f) : Color.white;
            if (hasBuff) baseTint = Color.Lerp(baseTint, auraRgb, 0.18f * auraPulse);
            if (flash > 0.001f) baseTint = Color.Lerp(baseTint, Color.white, flash * 0.55f);

            Color emissive = Color.black;
            if (hasBuff) emissive += auraRgb * (0.20f * auraPulse);
            if (flash > 0.001f) emissive += new Color(0.85f, 0.80f, 0.68f, 1f) * (flash * 0.65f);

            float enemyStrength = isEnemy ? (tagged ? 0.45f : 0.92f) : 0f;
            Color enemyRim = isEnemy
                ? new Color(highlightCol.r, highlightCol.g, highlightCol.b, enemyStrength)
                : Color.clear;
            float allyStrength = isEnemy
                ? 0f
                : (tagged ? KalyeahResources.DefaultAllyFresnelStrength * 0.5f : KalyeahResources.DefaultAllyFresnelStrength);

            _surfaceBlock.Clear();
            _surfaceBlock.SetColor(BaseColorId, baseTint);
            _surfaceBlock.SetColor(GltfBaseColorId, baseTint);
            _surfaceBlock.SetColor(EmissionColorId, emissive);
            _surfaceBlock.SetColor(GltfEmissiveId, emissive);
            _surfaceBlock.SetFloat(EnemyHighlightId, enemyStrength);
            _surfaceBlock.SetColor(EnemyHighlightColorId, enemyRim);
            _surfaceBlock.SetColor(AllyFresnelColorId, KalyeahResources.DefaultAllyFresnelColor);
            _surfaceBlock.SetFloat(AllyFresnelStrengthId, allyStrength);
            _surfaceBlock.SetFloat(HitFlashId, flash);
            _surfaceBlock.SetColor(AuraColorId, hasBuff ? new Color(auraRgb.r, auraRgb.g, auraRgb.b, auraPulse) : Color.clear);
            _surfaceBlock.SetFloat(TaggedDimId, tagged ? 1f : 0f);
            ApplyBlockToGroup(_kidBaseRenderers, _surfaceBlock);
            ApplyBlockToGroup(_canBaseRenderers, _surfaceBlock);
        }

        static void ApplyBlockToGroup(List<Renderer> list, MaterialPropertyBlock block)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) list[i].SetPropertyBlock(block);
        }

        static CachedClip FirstValid(CachedClip a, CachedClip b) => a.IsValid ? a : b;
        static CachedClip FirstValid(CachedClip a, CachedClip b, CachedClip c) => a.IsValid ? a : (b.IsValid ? b : c);
        static CachedClip FirstValid(CachedClip a, CachedClip b, CachedClip c, CachedClip d) => a.IsValid ? a : (b.IsValid ? b : (c.IsValid ? c : d));

        static bool TryResolveBuffAura(string buffId, out Color aura)
        {
            if (string.IsNullOrEmpty(buffId) || buffId == "0" || string.Equals(buffId, "none", StringComparison.OrdinalIgnoreCase))
            {
                aura = Color.clear;
                return false;
            }
            if (buffId == "1" || buffId.IndexOf("palamig", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                aura = PalamigAura;
                return true;
            }
            if (buffId == "2" || buffId.IndexOf("teks", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                aura = TeksAura;
                return true;
            }
            if (buffId == "3" || buffId.IndexOf("turon", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                aura = TuronAura;
                return true;
            }
            aura = GenericAura;
            return true;
        }

        static float MovePop(float p, float target, float dt)
        {
            return target > p
                ? Mathf.Min(p + dt / PopInTime, target)
                : Mathf.Max(p - dt / PopOutTime, target);
        }

        static float PopScale(float p, bool up)
        {
            float x = Mathf.Clamp01(p);
            const float c3 = PopBack + 1f;
            if (up)
            {
                float d = x - 1f;
                return 1f + c3 * d * d * d + PopBack * d * d;
            }
            float t = 1f - x;
            return 1f - (c3 * t * t * t - PopBack * t * t);
        }
    }
}
