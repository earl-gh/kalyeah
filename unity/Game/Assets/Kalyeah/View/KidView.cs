using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// Read-only visual puppet for one kid in the street: skinned GLB (<c>assets/models/characters/&lt;id&gt;.glb</c>, imported by glTFast
    /// with legacy clips), scaled to <see cref="KidHeight"/> * <c>viewScale</c> like <c>scripts/view/art/kid_model.gd</c>, silhouette-outlined
    /// in team blue/red via <c>Kalyeah/TeamOutline</c> (ZERO ground rings under feet per binding GDD rule), with full locomotion/status clips,
    /// procedural root postures, Lata tin-can polymorph swap, Merienda aura tint, hit flash, and &lt;0.75 m shoulder-camera proximity hide.
    /// </summary>
    public sealed class KidView
    {
        /// <summary>kid_model.gd KID_HEIGHT: standing height before camera kid_view_scale (0.82 -> ~1.52 m).</summary>
        public const float KidHeight = 1.85f;

        const float PolymorphCanHeight = 1.30f;
        const string FallbackSet = "narda";
        const string PolymorphCanId = "can";
        const float Blend = 0.14f;
        const float RunShare = 0.08f;
        const float WalkRunSplit = 0.62f;
        const float TurnRateDegPerSec = 22f * Mathf.Rad2Deg; // kid_model.gd TURN_RATE = 22 rad/s (~0.14s 180-deg turn)
        const float LandDuration = 0.18f;
        const float PopInTime = 0.24f;
        const float PopOutTime = 0.18f;
        const float PopBack = 1.70158f;
        const float GoneScale = 0.001f;
        const float DefaultOutlineWidth = 0.014f;

        // StatusEffects.Type integer values from Kalyeah.Sim.StatusEffects
        const int StatusStun = 1;
        const int StatusSlow = 2;
        const int StatusAirborne = 3;
        const int StatusPolymorph = 4;
        const int StatusBounce = 5;
        const int StatusKnockout = 6;
        const int StatusTripped = 7;

        static readonly string[] IdleNames = { "Idle", "idle" };
        static readonly string[] WalkNames = { "walk", "Walk", "Exhausted_Run" };
        static readonly string[] RunNames = { "Run", "run" };
        static readonly string[] SprintNames = { "sprint", "Sprint", "Run", "run" };
        static readonly string[] JumpNames = { "jump", "Jump", "Airborne", "airborne" };
        static readonly string[] SlideNames = { "slide", "Slide", "Airborne" };
        static readonly string[] DashNames = { "Dash", "dash", "sprint", "Sprint", "Run" };
        static readonly string[] CastNames = { "Cast", "Cast_OneHand", "throw", "cast_release_right" };
        static readonly string[] CastRunNames = { "Cast_Run", "Cast", "Cast_OneHand", "throw" };
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
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int GltfBaseColorId = Shader.PropertyToID("baseColorFactor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int GltfEmissiveId = Shader.PropertyToID("emissiveFactor");

        static readonly Color PalamigAura = new Color(0.28f, 0.88f, 1.00f, 1f);
        static readonly Color TeksAura = new Color(1.00f, 0.80f, 0.22f, 1f);
        static readonly Color TuronAura = new Color(0.46f, 0.92f, 0.42f, 1f);
        static readonly Color GenericAura = new Color(1.00f, 0.88f, 0.45f, 1f);

        static Material s_outlineMaterial;

        public readonly int Id;
        public readonly GameObject Root;
        public string CurrentPose { get; private set; } = "idle";
        public bool HasTeamOutline { get; private set; }
        public bool IsPolymorphed => _isPolymorphed;

        readonly float _viewScale;
        readonly Transform _bodyPivot;
        readonly GameObject _model;
        readonly Transform _canPivot;
        readonly Animation _animation;
        readonly List<Renderer> _kidBaseRenderers = new List<Renderer>(4);
        readonly List<Renderer> _kidOutlineRenderers = new List<Renderer>(4);
        readonly List<Renderer> _canBaseRenderers = new List<Renderer>(2);
        readonly List<Renderer> _canOutlineRenderers = new List<Renderer>(2);
        readonly MaterialPropertyBlock _outlineBlock = new MaterialPropertyBlock();
        readonly MaterialPropertyBlock _surfaceBlock = new MaterialPropertyBlock();

        readonly string _idle, _walk, _run, _sprint, _jump, _slide, _dash, _cast, _castRun, _hit, _sit, _crouch, _stun, _airborne, _trip;
        readonly bool _hasNativeCrouch, _hasNativeSlide, _hasNativeSit;

        Color _teamColor;
        string _playing;
        float _playingSpeed = 1f;
        float _clock;

        float _currentYawDeg;
        bool _yawInitialized;
        Vector3 _lastGroundWorld;
        bool _hasPlacedOnce;

        bool _wasJumping;
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

        bool _materialStateDirty = true;
        int _appliedFlashStep = -1;
        int _appliedAuraStep = -1;
        string _appliedBuffId = "";
        bool _appliedTaggedDim;

        /// <summary>
        /// Builds the kid puppet. Note: <paramref name="disc"/> is kept strictly for signature compatibility with existing callers;
        /// per binding GDD rule, no ground ring or cylinder is ever created under any kid's feet.
        /// </summary>
        public KidView(Transform parent, KidSnapshot kid, float viewScale, Color team, Material disc)
        {
            Id = kid.Id;
            _viewScale = Mathf.Max(viewScale, 0.1f);
            _teamColor = team;

            Root = new GameObject($"Kid_{kid.Id}_{kid.CharacterId}");
            Root.transform.SetParent(parent, false);

            _bodyPivot = new GameObject("Body").transform;
            _bodyPivot.SetParent(Root.transform, false);

            GameObject prefab = KalyeahResources.Model("characters", kid.CharacterId);
            if (prefab == null) prefab = KalyeahResources.Model("characters", FallbackSet);
            if (prefab != null)
            {
                _model = Object.Instantiate(prefab, _bodyPivot, false);
                _model.name = "Model";
                _model.transform.localRotation = Quaternion.identity;
                SanitizeGltfMaterials(_model);
                float natural = Mathf.Max(KalyeahResources.RenderBounds(_model).size.y, 0.1f);
                _model.transform.localScale = Vector3.one * (KidHeight * _viewScale / natural);
                _animation = _model.GetComponentInChildren<Animation>(true);
                if (_animation != null)
                {
                    foreach (AnimationState state in _animation) state.wrapMode = WrapMode.Loop;
                    _idle = Clip(IdleNames);
                    _walk = Clip(WalkNames);
                    _run = Clip(RunNames);
                    _sprint = Clip(SprintNames);
                    _jump = Clip(JumpNames);
                    _slide = Clip(SlideNames);
                    _dash = Clip(DashNames);
                    _cast = Clip(CastNames);
                    _castRun = Clip(CastRunNames);
                    _hit = Clip(HitNames);
                    _sit = Clip(SitNames);
                    _crouch = Clip(CrouchNames);
                    _stun = Clip(StunNames);
                    _airborne = Clip(AirborneNames);
                    _trip = Clip(TripNames);

                    _hasNativeCrouch = HasExactClip("crouch", "Crouch");
                    _hasNativeSlide = HasExactClip("slide", "Slide");
                    _hasNativeSit = HasExactClip("Sit", "sit");

                    ConfigureClampClip(_jump);
                    ConfigureClampClip(_slide);
                    ConfigureClampClip(_cast);
                    ConfigureClampClip(_castRun);
                    ConfigureClampClip(_hit);
                    ConfigureClampClip(_trip);

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

            Material outlineMat = ResolveOutlineMaterial();
            HasTeamOutline = outlineMat != null;
            CollectAndAttachOutlines(_model, outlineMat, _kidBaseRenderers, _kidOutlineRenderers);
            CollectAndAttachOutlines(_canPivot.gameObject, outlineMat, _canBaseRenderers, _canOutlineRenderers);

            SetRendererGroupEnabled(_canBaseRenderers, false);
            SetRendererGroupEnabled(_canOutlineRenderers, false);
            ApplyMaterialBlocks(0f, "", kid.Tagged);

            Place(kid.Position, kid.Facing);
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
                // Upright tin-can body stand-in (elevated cylinder body, never a flat ground disc).
                GameObject canStandIn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                canStandIn.name = "CanStandIn";
                KalyeahResources.Kill(canStandIn.GetComponent<Collider>());
                canStandIn.transform.SetParent(pivot, false);
                canStandIn.transform.localScale = new Vector3(targetHeight * 0.52f, targetHeight * 0.5f, targetHeight * 0.52f);
                canStandIn.transform.localPosition = Vector3.up * (targetHeight * 0.5f);
            }
            return pivot;
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

        void ConfigureClampClip(string clipName)
        {
            if (string.IsNullOrEmpty(clipName) || _animation == null) return;
            // Never clamp a shared looping fallback clip.
            if (clipName == _idle || clipName == _walk || clipName == _run || clipName == _sprint
                || clipName == _crouch || clipName == _stun || clipName == _airborne || clipName == _sit)
                return;
            AnimationState state = _animation[clipName];
            if (state != null) state.wrapMode = WrapMode.ClampForever;
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

        string Clip(string[] names)
        {
            for (int i = 0; i < names.Length; i++)
                if (_animation.GetClip(names[i]) != null) return names[i];
            foreach (AnimationState state in _animation)
                for (int i = 0; i < names.Length; i++)
                    if (string.Equals(state.name, names[i], StringComparison.OrdinalIgnoreCase)) return state.name;
            return null;
        }

        /// <summary>Updates team outline color (ally blue / enemy red) via MaterialPropertyBlock.</summary>
        public void SetTeamColor(Color team)
        {
            if (_teamColor == team) return;
            _teamColor = team;
            _materialStateDirty = true;
        }

        /// <summary>Triggers a one-shot cast clip (<c>Cast_OneHand</c> standing or <c>Cast_Run</c> moving) without mutating sim state.</summary>
        public void TriggerCast(bool moving = false, float duration = 0.36f)
        {
            _castMoving = moving;
            _castTimer = Mathf.Max(_castTimer, Mathf.Max(0.08f, duration));
            _playing = null; // allow immediate re-trigger on rapidTirador shots
        }

        /// <summary>Toggles &lt;0.75 m shoulder-camera proximity hide across all kid/outline/can renderers with zero GameObject churn.</summary>
        public void SetHiddenNearCamera(bool hidden)
        {
            if (_hiddenNearCamera == hidden) return;
            _hiddenNearCamera = hidden;
            RefreshRendererVisibility();
        }

        /// <summary>Feet at <paramref name="ground"/> (sim XZ), facing <paramref name="facing"/> (sim XZ), matching kid_model.gd.</summary>
        public void Place(SVector2 ground, SVector2 facing)
        {
            Vector3 world = KalyeahResources.World(ground);
            Root.transform.position = world;
            _lastGroundWorld = world;
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
            string want = speed01 >= WalkRunSplit ? (_run ?? _idle)
                : (speed01 > RunShare ? (_walk ?? _run ?? _idle) : _idle);
            float rate = (speed01 > RunShare && speed01 < WalkRunSplit) ? 0.84f : 1f;
            CurrentPose = speed01 >= WalkRunSplit ? "run" : (speed01 > RunShare ? "walk" : "idle");
            DriveClip(want, rate, Mathf.Max(0f, dt), clockAlreadyStepped: false);
        }

        /// <summary>
        /// Full read-only puppet sync from interpolated sim state: ground XZ + <paramref name="airY"/> jump lift, smooth facing turn,
        /// locomotion/status clips (<c>Jump</c>, <c>Slide</c>, <c>Dash</c>, <c>Cast</c>, <c>Hit</c>, <c>Sit</c>, <c>Stunned</c>, <c>Tripped</c>),
        /// procedural crouch/slide/dash/stumble/Lata-polymorph/tagged poses, hit flash, Merienda aura tint, and &lt;0.75 m camera hide.
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
            _hiddenNearCamera = hideNearCamera;

            Vector3 targetWorld = KalyeahResources.World(ground);
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
            _landTimer = Mathf.Max(0f, _landTimer - dt);

            bool crouchActive = !blocked && !jumping && !sliding && !dashing && crouching;
            bool slideActive = !blocked && !jumping && sliding;
            bool dashActive = !blocked && dashing;

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

            // Select clip + CurrentPose without mutating any sim state.
            string wantClip = _idle;
            float clipRate = 1f;
            if (isPolymorph)
            {
                CurrentPose = "can";
                wantClip = _idle;
            }
            else if (tagged)
            {
                CurrentPose = "tagged";
                wantClip = _sit ?? _crouch ?? _idle;
                clipRate = 0.72f;
            }
            else if (isStatusAir)
            {
                CurrentPose = "airborne";
                wantClip = _airborne ?? _jump ?? _idle;
            }
            else if (isStun)
            {
                CurrentPose = "stunned";
                wantClip = _stun ?? _idle;
            }
            else if (isTripped)
            {
                CurrentPose = "tripped";
                wantClip = _trip ?? _idle;
            }
            else if (dashActive)
            {
                CurrentPose = "dash";
                wantClip = _dash ?? _sprint ?? _run ?? _idle;
                clipRate = 1.35f;
            }
            else if (jumping)
            {
                CurrentPose = "jump";
                wantClip = _jump ?? _airborne ?? _run ?? _idle;
            }
            else if (slideActive)
            {
                CurrentPose = "slide";
                wantClip = _slide ?? _airborne ?? _run ?? _idle;
            }
            else if (_castTimer > 0f)
            {
                CurrentPose = "cast";
                wantClip = (_castMoving || speed01 > RunShare ? _castRun : _cast) ?? _cast ?? _idle;
            }
            else if (_hitTimer > 0f && speed01 <= RunShare)
            {
                CurrentPose = "hit";
                wantClip = _hit ?? _idle;
            }
            else if (crouchActive)
            {
                CurrentPose = "crouch";
                wantClip = _hasNativeCrouch
                    ? _crouch
                    : (speed01 > RunShare ? (_walk ?? _run ?? _idle) : _idle);
                clipRate = speed01 > RunShare ? 0.78f : (_hasNativeCrouch ? 0.65f : 1f);
            }
            else if (speed01 >= WalkRunSplit)
            {
                CurrentPose = "run";
                wantClip = _run ?? _idle;
                clipRate = isSlow ? 0.68f : 1f;
            }
            else if (speed01 > RunShare)
            {
                CurrentPose = "walk";
                wantClip = _walk ?? _run ?? _idle;
                clipRate = isSlow ? 0.62f : 0.84f;
            }
            else
            {
                CurrentPose = "idle";
                wantClip = _idle;
            }

            DriveClip(wantClip, clipRate, dt, clockAlreadyStepped: true);
            ApplyProceduralBodyPose(airY, tagged, isStun, isStatusAir, isTripped, jumping, clampedFlash);
            RefreshRendererVisibility();
            ApplyMaterialBlocks(clampedFlash, buffId ?? "", tagged);
        }

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

        void DriveClip(string want, float speedScale, float dt, bool clockAlreadyStepped)
        {
            if (_animation == null || string.IsNullOrEmpty(want)) return;
            bool first = _playing == null;
            bool changed = !string.Equals(want, _playing, StringComparison.Ordinal);
            if (!first && changed) _clock = 0f;
            else if (!clockAlreadyStepped) _clock += dt;

            if (Application.isPlaying)
            {
                AnimationState state = _animation[want];
                if (state != null && !Mathf.Approximately(_playingSpeed, speedScale))
                {
                    state.speed = speedScale;
                    _playingSpeed = speedScale;
                }
                if (changed)
                {
                    if (state != null) state.speed = speedScale;
                    _playingSpeed = speedScale;
                    _animation.CrossFade(want, Blend);
                }
            }
            else
            {
                AnimationClip clip = _animation.GetClip(want);
                if (clip != null)
                {
                    float sampleT = clip.length > 0f ? (_clock * speedScale) % clip.length : 0f;
                    clip.SampleAnimation(_animation.gameObject, sampleT);
                }
            }
            _playing = want;
        }

        void RefreshRendererVisibility()
        {
            bool showKid = !_hiddenNearCamera && _kidPop > 0.001f;
            bool showCan = !_hiddenNearCamera && _canPop > 0.001f;
            if (showKid != _kidRenderersVisible)
            {
                _kidRenderersVisible = showKid;
                SetRendererGroupEnabled(_kidBaseRenderers, showKid);
                SetRendererGroupEnabled(_kidOutlineRenderers, showKid);
            }
            if (showCan != _canRenderersVisible)
            {
                _canRenderersVisible = showCan;
                SetRendererGroupEnabled(_canBaseRenderers, showCan);
                SetRendererGroupEnabled(_canOutlineRenderers, showCan);
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

            if (!_materialStateDirty
                && flashStep == _appliedFlashStep
                && auraStep == _appliedAuraStep
                && tagged == _appliedTaggedDim
                && string.Equals(buffId, _appliedBuffId, StringComparison.Ordinal))
                return;

            _materialStateDirty = false;
            _appliedFlashStep = flashStep;
            _appliedAuraStep = auraStep;
            _appliedTaggedDim = tagged;
            _appliedBuffId = buffId;

            float flash = flashStep / 24f;
            float auraPulse = hasBuff ? (0.45f + 0.30f * (auraStep / 12f)) : 0f;

            if (HasTeamOutline)
            {
                Color outlineCol = tagged ? new Color(_teamColor.r * 0.75f, _teamColor.g * 0.75f, _teamColor.b * 0.75f, 0.72f) : _teamColor;
                _outlineBlock.Clear();
                _outlineBlock.SetColor(OutlineColorId, outlineCol);
                _outlineBlock.SetFloat(OutlineWidthId, DefaultOutlineWidth);
                _outlineBlock.SetFloat(HitFlashId, flash);
                _outlineBlock.SetColor(AuraColorId, hasBuff ? new Color(auraRgb.r, auraRgb.g, auraRgb.b, auraPulse) : Color.clear);
                ApplyBlockToGroup(_kidOutlineRenderers, _outlineBlock);
                ApplyBlockToGroup(_canOutlineRenderers, _outlineBlock);
            }

            // Keep base renderers on pure SRP Batcher fast-path when no flash, Merienda aura, or tagged dim is active.
            if (flash <= 0.001f && !hasBuff && !tagged)
            {
                ApplyBlockToGroup(_kidBaseRenderers, null);
                ApplyBlockToGroup(_canBaseRenderers, null);
                return;
            }

            Color baseTint = tagged ? new Color(0.76f, 0.78f, 0.84f, 1f) : Color.white;
            if (hasBuff) baseTint = Color.Lerp(baseTint, auraRgb, 0.22f * auraPulse);
            if (flash > 0.001f) baseTint = Color.Lerp(baseTint, Color.white, flash * 0.65f);

            Color emissive = Color.black;
            if (hasBuff) emissive += auraRgb * (0.24f * auraPulse);
            if (flash > 0.001f) emissive += new Color(0.85f, 0.80f, 0.68f, 1f) * (flash * 0.75f);

            _surfaceBlock.Clear();
            _surfaceBlock.SetColor(BaseColorId, baseTint);
            _surfaceBlock.SetColor(GltfBaseColorId, baseTint);
            _surfaceBlock.SetColor(EmissionColorId, emissive);
            _surfaceBlock.SetColor(GltfEmissiveId, emissive);
            ApplyBlockToGroup(_kidBaseRenderers, _surfaceBlock);
            ApplyBlockToGroup(_canBaseRenderers, _surfaceBlock);
        }

        static void ApplyBlockToGroup(List<Renderer> list, MaterialPropertyBlock block)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) list[i].SetPropertyBlock(block);
        }

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
