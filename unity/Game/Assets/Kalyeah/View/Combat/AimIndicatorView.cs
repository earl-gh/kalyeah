using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// Zero-GC world-space ground aim preview (<c>Cone</c>, <c>Line</c>, <c>GroundAoE</c> ring + landing disc,
    /// <c>Shield</c> + second-form outline, <c>Targeted</c>/<c>Heal</c> range circle &amp; reach rings) clamped to
    /// <see cref="WeaponInfo.MaxRange"/>, turning unmistakable red on <c>isCancelHovered</c> or <c>castingBlocked</c>,
    /// plus the shrinking <c>MoveMarker</c> ground tap ring (<c>scripts/view/aim_indicator.gd</c>, <c>scripts/ui/hud/move_marker.gd</c>).
    /// </summary>
    public sealed class AimIndicatorView : IDisposable
    {
        public const float AimHeight = 0.055f;
        public const float MoveMarkerHeight = 0.062f;
        public const float LineWidth = 0.075f;
        public const float ReachRingWidth = 0.105f;
        public const float ShieldThickness = 0.40f;
        public const float ReachPad = 0.25f;
        public const float HealReachPad = 0.35f;
        public const float NoDirectionDeadzone = 0.10f;

        // MoveMarker constants matching scripts/ui/hud/move_marker.gd.
        public const float MoveStartRadius = 1.40f;
        public const float MoveDotRadius = 0.22f;
        public const float MoveShrinkDuration = 0.45f;
        public const float MoveCloseThreshold = 0.90f;
        public const float MoveArrivalRadius = 0.48f;
        public const float MoveTapHoldSeconds = 0.50f;

        // Papel shield & Langit Lupa second-form motion constants (data/weapons/*.tres & WallMotion).
        const float PapelPushDistance = 1.50f;
        const float LangitExpandFactor = 1.80f;

        const int CircleSegments = 36;
        const int WedgeArcSegments = 16;
        const int MaxAimVertices = 1536;
        const int MaxAimIndices = 4608;
        const int MoveRingSegments = 28;
        const int MaxMoveVertices = 256;
        const int MaxMoveIndices = 768;

        public static readonly Color ValidOutlineColor = new Color(1.00f, 1.00f, 1.00f, 0.95f);
        public static readonly Color CancelRedColor = new Color(1.00f, 0.20f, 0.18f, 0.96f);
        public static readonly Color BlockedRedColor = new Color(1.00f, 0.24f, 0.20f, 0.94f);
        public static readonly Color NoCastMutedColor = new Color(0.80f, 0.82f, 0.88f, 0.42f);
        public static readonly Color ReachEnemyColor = new Color(1.00f, 0.30f, 0.25f, 0.92f);
        public static readonly Color ReachFriendColor = new Color(0.46f, 1.00f, 0.52f, 0.92f);
        static readonly Color MoveRingColor = new Color(1.00f, 1.00f, 1.00f, 0.88f);
        static readonly Color MoveDotColor = new Color(1.00f, 1.00f, 1.00f, 1.00f);
        static readonly Color MoveShadowColor = new Color(0.02f, 0.04f, 0.10f, 0.52f);

        const float FillAlphaShare = 0.16f;
        const float RangeRingAlphaShare = 0.44f;
        const int RangeRingDashCount = 18;
        const int EnemyReachDashCount = 12;

        static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int PropTint = Shader.PropertyToID("_Tint");
        static readonly int PropInkColor = Shader.PropertyToID("_InkColor");
        static readonly int PropPhase = Shader.PropertyToID("_Phase");
        static readonly int PropSoftFade = Shader.PropertyToID("_SoftFade");
        static readonly int PropFresnel = Shader.PropertyToID("_FresnelSoftness");
        static readonly int PropSunlit = Shader.PropertyToID("_SunlitLift");
        static readonly int PropHardEdgeWidth = Shader.PropertyToID("_HardEdgeWidth");
        static readonly int PropDashCount = Shader.PropertyToID("_DashCount");
        static readonly int PropEdgeBoost = Shader.PropertyToID("_EdgeBoost");
        static readonly int PropInteriorAlpha = Shader.PropertyToID("_InteriorAlpha");

        static readonly SVector2[] UnitCircle = BuildUnitCircle(CircleSegments);
        static readonly SVector2[] MoveUnitCircle = BuildUnitCircle(MoveRingSegments);

        readonly Transform _root;
        readonly GameObject _aimObject;
        readonly MeshRenderer _aimRenderer;
        readonly Mesh _aimMesh;
        readonly Vector3[] _aimVerts = new Vector3[MaxAimVertices];
        readonly Vector2[] _aimUvs = new Vector2[MaxAimVertices];
        readonly Color32[] _aimColors = new Color32[MaxAimVertices];
        readonly int[] _aimTris = new int[MaxAimIndices];
        int _aimVertCount;
        int _aimTriCount;

        readonly GameObject _moveObject;
        readonly MeshRenderer _moveRenderer;
        readonly Mesh _moveMesh;
        readonly Vector3[] _moveVerts = new Vector3[MaxMoveVertices];
        readonly Vector2[] _moveUvs = new Vector2[MaxMoveVertices];
        readonly Color32[] _moveColors = new Color32[MaxMoveVertices];
        readonly int[] _moveTris = new int[MaxMoveIndices];
        int _moveVertCount;
        int _moveTriCount;

        readonly Material _sharedMaterial;
        readonly bool _ownsMaterial;
        readonly SVector2[] _scratchPoly = new SVector2[CircleSegments + 4];

        SVector2 _moveTargetSim;
        bool _hasMoveTarget;
        float _movePopLeft;
        float _moveHoldLeft;
        bool _disposed;

        public Transform Root => _root;
        public bool AimGuideEnabled { get; set; } = true;
        public bool ReduceMotion { get; set; }
        public float GroundY { get; set; }
        public bool IsAimVisible => _aimObject != null && _aimObject.activeSelf && _aimTriCount > 0;
        public bool IsMoveMarkerVisible => _moveObject != null && _moveObject.activeSelf && _moveTriCount > 0;
        public Color CurrentAimColor { get; private set; } = ValidOutlineColor;
        public SVector2 ClampedAimPoint { get; private set; }
        public SVector2 ClampedAimDirection { get; private set; } = new SVector2(0f, -1f);
        public bool IsRedWarning { get; private set; }

        public float CurrentMoveMarkerRadius
        {
            get
            {
                if (!_hasMoveTarget) return 0f;
                return Mathf.Lerp(MoveStartRadius, MoveDotRadius, MoveShrinkProgress());
            }
        }

        public AimIndicatorView(Transform parent = null, bool aimGuideEnabled = true, bool reduceMotion = false)
        {
            AimGuideEnabled = aimGuideEnabled;
            ReduceMotion = reduceMotion;

            var rootGo = new GameObject("AimIndicatorView");
            _root = rootGo.transform;
            if (parent != null) _root.SetParent(parent, false);

            _sharedMaterial = CreateOverlayMaterial(out _ownsMaterial);

            _aimMesh = CreateDynamicMesh("AimPreviewMesh");
            _aimObject = CreateLayerObject("AimPreview", _aimMesh, _sharedMaterial, out _aimRenderer);
            _aimObject.SetActive(false);

            _moveMesh = CreateDynamicMesh("MoveMarkerMesh");
            _moveObject = CreateLayerObject("MoveMarker", _moveMesh, _sharedMaterial, out _moveRenderer);
            _moveObject.SetActive(false);
        }

        static SVector2[] BuildUnitCircle(int segments)
        {
            var pts = new SVector2[segments + 1];
            float step = MathF.PI * 2f / segments;
            for (int i = 0; i <= segments; i++)
            {
                float a = (i % segments) * step;
                pts[i] = new SVector2(MathF.Cos(a), MathF.Sin(a));
            }
            return pts;
        }

        static Material CreateOverlayMaterial(out bool owns)
        {
            Material arcane = KalyeahResources.Material("ArcaneFx", FxPool.ShaderName);
            if (arcane != null)
            {
                var mat = new Material(arcane) { name = "AimIndicatorOverlay" };
                if (mat.HasProperty(PropBaseColor)) mat.SetColor(PropBaseColor, Color.white);
                if (mat.HasProperty(PropTint)) mat.SetColor(PropTint, Color.white);
                if (mat.HasProperty(PropInkColor)) mat.SetColor(PropInkColor, Color.clear);
                if (mat.HasProperty(PropPhase)) mat.SetFloat(PropPhase, 0f);
                if (mat.HasProperty(PropSoftFade)) mat.SetFloat(PropSoftFade, 1f);
                if (mat.HasProperty(PropFresnel)) mat.SetFloat(PropFresnel, 0f);
                if (mat.HasProperty(PropSunlit)) mat.SetFloat(PropSunlit, 0f);
                if (mat.HasProperty(PropHardEdgeWidth)) mat.SetFloat(PropHardEdgeWidth, 0.5f);
                if (mat.HasProperty(PropDashCount)) mat.SetFloat(PropDashCount, 0f);
                if (mat.HasProperty(PropEdgeBoost)) mat.SetFloat(PropEdgeBoost, 0.24f);
                if (mat.HasProperty(PropInteriorAlpha)) mat.SetFloat(PropInteriorAlpha, 1f);
                mat.renderQueue = (int)RenderQueue.Transparent + 12;
                owns = true;
                return mat;
            }

            Shader shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                var mat = new Material(shader) { name = "AimIndicatorOverlay" };
                if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)CullMode.Off);
                if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                mat.renderQueue = (int)RenderQueue.Transparent + 12;
                owns = true;
                return mat;
            }
            owns = false;
            return null;
        }

        static Mesh CreateDynamicMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            return mesh;
        }

        GameObject CreateLayerObject(string name, Mesh mesh, Material material, out MeshRenderer renderer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            if (material != null) renderer.sharedMaterial = material;
            return go;
        }

        /// <summary>Convenience overload reading local caster state, weapon metadata, and settings directly from <see cref="SimBridge"/>.</summary>
        public void ShowAim(
            SimBridge bridge,
            string weaponId,
            SVector2 rawAimPoint,
            bool isCancelHovered = false,
            bool manualDrag = true,
            float holdSeconds = 0f)
        {
            if (bridge == null || string.IsNullOrEmpty(weaponId))
            {
                HideAim();
                return;
            }

            SettingsStore settings = SettingsStore.Current;
            if (settings != null)
            {
                AimGuideEnabled = settings.AimGuide;
                ReduceMotion = settings.ReduceMotion;
            }

            KidSnapshot? local = bridge.LocalKid;
            WeaponInfo? weapon = bridge.GetWeaponInfo(weaponId);
            if (!local.HasValue || !weapon.HasValue || local.Value.Tagged)
            {
                HideAim();
                return;
            }

            KidSnapshot me = local.Value;
            WeaponInfo info = weapon.Value;
            bool affordable = info.ElixirCost <= 0 || me.Elixir + 0.001f >= info.ElixirCost;
            bool statusLocked = me.StatusKind == StatusKind.Stun
                || me.StatusKind == StatusKind.Knockout
                || me.StatusKind == StatusKind.Polymorph
                || me.StatusKind == StatusKind.Tripped
                || me.StatusKind == StatusKind.Airborne;
            bool canCast = affordable && !statusLocked && !me.CastingBlocked;

            ShowAim(
                info,
                me.Position,
                me.Facing,
                rawAimPoint,
                isCancelHovered: isCancelHovered,
                castingBlocked: me.CastingBlocked,
                canCast: canCast,
                manualDrag: manualDrag,
                holdSeconds: holdSeconds,
                kids: bridge.Kids,
                localTeam: bridge.LocalPlayerTeam,
                casterId: me.Id,
                playerRadius: bridge.PlayerRadius > 0f ? bridge.PlayerRadius : AimSolver.DefaultPlayerRadius);
        }

        /// <summary>Overload accepting a pre-solved 3P <see cref="AimSolution"/> directly from <see cref="AimSolver"/>.</summary>
        public void ShowAim(
            WeaponInfo weapon,
            AimSolution solution,
            SVector2 casterPos,
            SVector2 casterFacing,
            bool isCancelHovered = false,
            bool castingBlocked = false,
            bool canCast = true,
            bool manualDrag = true,
            float holdSeconds = 0f,
            IReadOnlyList<KidSnapshot> kids = null,
            int localTeam = SimBridge.LocalTeam,
            int casterId = SimBridge.LocalId,
            float playerRadius = AimSolver.DefaultPlayerRadius)
        {
            ShowAim(
                weapon,
                casterPos,
                casterFacing,
                solution.SimAimPoint,
                isCancelHovered,
                castingBlocked,
                canCast,
                manualDrag,
                holdSeconds,
                kids,
                localTeam,
                casterId,
                playerRadius);
        }

        /// <summary>
        /// Updates the world-space ground aim preview clamped to <see cref="WeaponInfo.MaxRange"/>.
        /// Turns unmistakable red whenever <paramref name="isCancelHovered"/> or <paramref name="castingBlocked"/> is true.
        /// </summary>
        public void ShowAim(
            WeaponInfo weapon,
            SVector2 casterPos,
            SVector2 casterFacing,
            SVector2 rawAimPoint,
            bool isCancelHovered = false,
            bool castingBlocked = false,
            bool canCast = true,
            bool manualDrag = true,
            float holdSeconds = 0f,
            IReadOnlyList<KidSnapshot> kids = null,
            int localTeam = SimBridge.LocalTeam,
            int casterId = SimBridge.LocalId,
            float playerRadius = AimSolver.DefaultPlayerRadius)
        {
            if (_disposed) return;
            if (!AimGuideEnabled)
            {
                HideAim();
                return;
            }

            SVector2 safeFacing = NormalizeOrFallback(casterFacing, new SVector2(0f, -1f));
            float maxRange = Mathf.Max(0f, weapon.MaxRange);
            SVector2 rawOffset = rawAimPoint - casterPos;
            float rawDist = rawOffset.Length();

            SVector2 aimDir = rawDist > NoDirectionDeadzone
                ? rawOffset / rawDist
                : safeFacing;

            SVector2 clampedTarget = maxRange <= 0f
                ? casterPos
                : (rawDist > maxRange ? casterPos + aimDir * maxRange : rawAimPoint);

            ClampedAimPoint = clampedTarget;
            ClampedAimDirection = aimDir;

            IsRedWarning = isCancelHovered || castingBlocked;
            Color outlineColor = isCancelHovered
                ? CancelRedColor
                : (castingBlocked ? BlockedRedColor : (canCast ? ValidOutlineColor : NoCastMutedColor));
            CurrentAimColor = outlineColor;

            Color fillColor = WithAlpha(outlineColor, outlineColor.a * FillAlphaShare);
            Color rangeRingColor = WithAlpha(outlineColor, outlineColor.a * RangeRingAlphaShare);
            float y = GroundY + AimHeight;

            _aimVertCount = 0;
            _aimTriCount = 0;

            if (manualDrag)
            {
                BuildWeaponPreviewGeometry(
                    weapon,
                    casterPos,
                    aimDir,
                    clampedTarget,
                    maxRange,
                    holdSeconds,
                    y,
                    outlineColor,
                    fillColor,
                    rangeRingColor);
            }

            if (kids != null && kids.Count > 0)
            {
                AppendReachRings(
                    weapon,
                    casterPos,
                    aimDir,
                    clampedTarget,
                    maxRange,
                    kids,
                    localTeam,
                    casterId,
                    playerRadius,
                    IsRedWarning,
                    outlineColor,
                    y);
            }

            CommitMesh(_aimMesh, _aimObject, _aimVerts, _aimUvs, _aimColors, _aimTris, _aimVertCount, _aimTriCount);
        }

        public void HideAim()
        {
            _aimVertCount = 0;
            _aimTriCount = 0;
            IsRedWarning = false;
            if (_aimObject != null && _aimObject.activeSelf)
                _aimObject.SetActive(false);
        }

        /// <summary>
        /// Triggers or updates the shrinking ground <c>MoveMarker</c> at <paramref name="simPoint"/>.
        /// Under <see cref="ReduceMotion"/>, renders a calm static target dot without shrink motion.
        /// </summary>
        public void PingMoveMarker(SVector2 simPoint, bool fresh = true)
        {
            if (_disposed) return;
            _moveTargetSim = simPoint;
            _hasMoveTarget = true;
            _moveHoldLeft = MoveTapHoldSeconds;
            if (fresh)
                _movePopLeft = ReduceMotion ? 0f : MoveShrinkDuration;

            RebuildMoveMarkerGeometry();
        }

        public void ClearMoveMarker()
        {
            _hasMoveTarget = false;
            _movePopLeft = 0f;
            _moveHoldLeft = 0f;
            _moveVertCount = 0;
            _moveTriCount = 0;
            if (_moveObject != null && _moveObject.activeSelf)
                _moveObject.SetActive(false);
        }

        public void TickMoveMarker(float dt, KidSnapshot? localKid = null, MatchPhaseKind phase = MatchPhaseKind.Playing)
        {
            if (_disposed || !_hasMoveTarget) return;

            if (phase != MatchPhaseKind.Playing || (localKid.HasValue && localKid.Value.Tagged))
            {
                ClearMoveMarker();
                return;
            }

            float step = Mathf.Max(0f, dt);
            _moveHoldLeft = Mathf.Max(0f, _moveHoldLeft - step);
            _movePopLeft = ReduceMotion ? 0f : Mathf.Max(0f, _movePopLeft - step);

            if (localKid.HasValue)
            {
                float distSq = SVector2.DistanceSquared(localKid.Value.Position, _moveTargetSim);
                if (distSq <= MoveArrivalRadius * MoveArrivalRadius && _moveHoldLeft <= 0.1f)
                {
                    ClearMoveMarker();
                    return;
                }
            }

            RebuildMoveMarkerGeometry();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_root != null) KalyeahResources.Kill(_root.gameObject);
            KalyeahResources.Kill(_aimMesh);
            KalyeahResources.Kill(_moveMesh);
            if (_ownsMaterial && _sharedMaterial != null) KalyeahResources.Kill(_sharedMaterial);
        }

        void BuildWeaponPreviewGeometry(
            WeaponInfo weapon,
            SVector2 origin,
            SVector2 aimDir,
            SVector2 clampedTarget,
            float maxRange,
            float holdSeconds,
            float y,
            Color outlineColor,
            Color fillColor,
            Color rangeRingColor)
        {
            switch (weapon.Shape)
            {
                case WeaponShapeKind.Cone:
                {
                    float reach = maxRange > 0.05f ? maxRange : Mathf.Max(1.5f, weapon.Radius);
                    float angleDeg = weapon.AngleDegrees > 1f ? weapon.AngleDegrees : 55f;
                    AppendConeWedge(origin, aimDir, reach, angleDeg, y, outlineColor, fillColor);
                    if (weapon.Id == "gunting_light" || holdSeconds > 0.01f)
                        AppendConeChargePips(origin, aimDir, reach, angleDeg, holdSeconds, y, outlineColor);
                    break;
                }

                case WeaponShapeKind.GroundAoe:
                case WeaponShapeKind.Trap:
                case WeaponShapeKind.Field:
                {
                    if (maxRange > 0.25f)
                        AppendDashedCircleRing(origin, maxRange, LineWidth * 0.85f, y, rangeRingColor, RangeRingDashCount);

                    float aoeRadius = weapon.Radius > 0.05f
                        ? weapon.Radius
                        : (weapon.TriggerRadius > 0.05f ? weapon.TriggerRadius : 1.5f);
                    // Inset clean interior fill inside the exact-radius hard-edge ring (zero coplanar overdraw).
                    AppendDiscFill(clampedTarget, Mathf.Max(0.01f, aoeRadius - LineWidth), y, fillColor);
                    AppendCircleRing(clampedTarget, aoeRadius, LineWidth, y + 0.002f, outlineColor);
                    break;
                }

                case WeaponShapeKind.Bouncer:
                {
                    // Bola marble bounce rings along aimDir (no connecting line per scripts/view/aim_indicator.gd).
                    int bounces = Mathf.Max(1, weapon.Count);
                    float reach = maxRange > 0.25f ? maxRange : 9f;
                    float bounceR = weapon.Radius > 0.05f ? weapon.Radius : 0.9f;
                    for (int i = 0; i < bounces; i++)
                    {
                        SVector2 center = origin + aimDir * (reach * (i + 1) / bounces);
                        AppendDiscFill(center, Mathf.Max(0.01f, bounceR - LineWidth), y, fillColor);
                        AppendCircleRing(center, bounceR, LineWidth, y + 0.002f, outlineColor);
                    }
                    break;
                }

                case WeaponShapeKind.Shield:
                {
                    float offset = weapon.Offset > 0.01f ? weapon.Offset : 1.6f;
                    float halfWidth = Mathf.Max(0.6f, (weapon.Width > 0.05f ? weapon.Width : 3.2f) * 0.5f);
                    SVector2 wallCenter = origin + aimDir * offset;
                    AppendWallBox(wallCenter, aimDir, halfWidth, ShieldThickness, y, outlineColor, fillColor, drawFill: true);

                    // Second form preview (Papel Shield forward glide; Langit Lupa earth widening).
                    bool isSoil = weapon.Look == "soil" || weapon.Id == "langit_lupa";
                    if (isSoil)
                    {
                        AppendWallBox(
                            wallCenter,
                            aimDir,
                            halfWidth * LangitExpandFactor,
                            ShieldThickness,
                            y,
                            WithAlpha(outlineColor, outlineColor.a * 0.75f),
                            Color.clear,
                            drawFill: false);
                    }
                    else if (weapon.Id == "papel_shield")
                    {
                        AppendWallBox(
                            origin + aimDir * (offset + PapelPushDistance),
                            aimDir,
                            halfWidth,
                            ShieldThickness,
                            y,
                            WithAlpha(outlineColor, outlineColor.a * 0.75f),
                            Color.clear,
                            drawFill: false);
                    }
                    break;
                }

                case WeaponShapeKind.Targeted:
                case WeaponShapeKind.Heal:
                {
                    float rangeR = maxRange > 0.25f ? maxRange : Mathf.Max(2.0f, weapon.Radius);
                    if (weapon.Shape == WeaponShapeKind.Heal && maxRange <= 0.05f && weapon.Radius > 0.05f)
                        AppendDiscFill(origin, Mathf.Max(0.01f, rangeR - LineWidth), y, fillColor);
                    AppendCircleRing(origin, rangeR, LineWidth, y + 0.002f, outlineColor);
                    break;
                }

                case WeaponShapeKind.Boomerang:
                case WeaponShapeKind.Spinner:
                case WeaponShapeKind.Skillshot:
                default:
                {
                    float length = maxRange > 0.25f ? maxRange : 8f;
                    float halfW = Mathf.Max(
                        weapon.ProjectileRadius,
                        weapon.Shape == WeaponShapeKind.Spinner ? weapon.Radius : 0f);
                    halfW = Mathf.Max(0.16f, halfW);
                    AppendLaneGuide(origin, aimDir, length, halfW, y, outlineColor, fillColor);
                    break;
                }
            }
        }

        void AppendConeWedge(
            SVector2 origin,
            SVector2 aimDir,
            float reach,
            float angleDeg,
            float y,
            Color outlineColor,
            Color fillColor)
        {
            float halfRad = Mathf.Clamp(angleDeg, 10f, 175f) * 0.5f * Mathf.Deg2Rad;
            int steps = WedgeArcSegments;
            _scratchPoly[0] = origin;
            for (int i = 0; i <= steps; i++)
            {
                float a = -halfRad + (2f * halfRad * i) / steps;
                _scratchPoly[i + 1] = origin + Rotate(aimDir, a) * reach;
            }
            int polyCount = steps + 2;
            AppendConvexFan(_scratchPoly, polyCount, y, fillColor);

            _scratchPoly[polyCount] = origin;
            AppendClosedPolyline(_scratchPoly, polyCount + 1, LineWidth, y + 0.002f, outlineColor);
        }

        void AppendConeChargePips(
            SVector2 origin,
            SVector2 aimDir,
            float reach,
            float angleDeg,
            float holdSeconds,
            float y,
            Color outlineColor)
        {
            const int maxSnips = 4;
            int lit = Mathf.Clamp(1 + Mathf.FloorToInt(holdSeconds / 0.28f), 1, maxSnips);
            float spread = Mathf.Clamp(angleDeg, 15f, 160f) * 0.5f * Mathf.Deg2Rad * 0.78f;
            Color dim = WithAlpha(outlineColor, outlineColor.a * 0.35f);

            for (int i = 0; i < maxSnips; i++)
            {
                float a = -spread + (2f * spread * i) / (maxSnips - 1);
                SVector2 ray = Rotate(aimDir, a);
                AppendSegment(origin + ray * (reach * 0.64f), origin + ray * (reach * 0.90f), LineWidth * 0.85f, y, i < lit ? outlineColor : dim);
            }
        }

        void AppendLaneGuide(
            SVector2 origin,
            SVector2 dir,
            float length,
            float halfWidth,
            float y,
            Color outlineColor,
            Color fillColor)
        {
            SVector2 side = Perp(dir) * halfWidth;
            SVector2 end = origin + dir * length;

            _scratchPoly[0] = origin + side;
            _scratchPoly[1] = end + side;
            _scratchPoly[2] = end - side;
            _scratchPoly[3] = origin - side;
            AppendConvexFan(_scratchPoly, 4, y, WithAlpha(fillColor, fillColor.a * 0.55f));

            _scratchPoly[4] = _scratchPoly[0];
            AppendClosedPolyline(_scratchPoly, 5, LineWidth, y + 0.002f, outlineColor);
            AppendCircleRing(end, halfWidth, LineWidth * 0.85f, y + 0.002f, outlineColor);
        }

        void AppendWallBox(
            SVector2 center,
            SVector2 facing,
            float halfWidth,
            float thickness,
            float y,
            Color outlineColor,
            Color fillColor,
            bool drawFill)
        {
            SVector2 fwd = facing * (thickness * 0.5f);
            SVector2 side = Perp(facing) * halfWidth;

            _scratchPoly[0] = center - fwd + side;
            _scratchPoly[1] = center + fwd + side;
            _scratchPoly[2] = center + fwd - side;
            _scratchPoly[3] = center - fwd - side;
            if (drawFill)
                AppendConvexFan(_scratchPoly, 4, y, fillColor);

            _scratchPoly[4] = _scratchPoly[0];
            AppendClosedPolyline(_scratchPoly, 5, LineWidth, y + 0.002f, outlineColor);
        }

        void AppendReachRings(
            WeaponInfo weapon,
            SVector2 casterPos,
            SVector2 aimDir,
            SVector2 clampedTarget,
            float maxRange,
            IReadOnlyList<KidSnapshot> kids,
            int localTeam,
            int casterId,
            float playerRadius,
            bool isRedWarning,
            Color warningColor,
            float y)
        {
            if (weapon.Shape == WeaponShapeKind.Shield) return;

            bool isHeal = weapon.Shape == WeaponShapeKind.Heal || weapon.Kind == WeaponRoleKind.Heal;
            int bestTargetId = -1;
            float bestScore = float.MaxValue;

            if (weapon.Shape == WeaponShapeKind.Targeted || weapon.Shape == WeaponShapeKind.Heal)
            {
                float reach = (maxRange > 0.05f ? maxRange : weapon.Radius) + playerRadius;
                for (int i = 0; i < kids.Count; i++)
                {
                    KidSnapshot k = kids[i];
                    if (k.Tagged) continue;
                    if (isHeal && k.Team != localTeam) continue;
                    if (!isHeal && (k.Team == localTeam || k.Id == casterId)) continue;

                    float dist = SVector2.Distance(casterPos, k.Position);
                    if (dist > reach) continue;

                    float distToAim = SVector2.Distance(clampedTarget, k.Position);
                    if (distToAim < bestScore)
                    {
                        bestScore = distToAim;
                        bestTargetId = k.Id;
                    }
                }
            }

            for (int i = 0; i < kids.Count; i++)
            {
                KidSnapshot k = kids[i];
                if (k.Tagged) continue;
                if (isHeal && k.Team != localTeam) continue;
                if (!isHeal && (k.Team == localTeam || k.Id == casterId)) continue;

                bool hit = false;
                switch (weapon.Shape)
                {
                    case WeaponShapeKind.Targeted:
                    case WeaponShapeKind.Heal:
                        hit = k.Id == bestTargetId;
                        break;

                    case WeaponShapeKind.Cone:
                    {
                        float reach = (maxRange > 0.05f ? maxRange : weapon.Radius) + playerRadius;
                        SVector2 toKid = k.Position - casterPos;
                        float d = toKid.Length();
                        if (d <= reach && d > 1e-4f)
                        {
                            float cosHalf = MathF.Cos(Mathf.Clamp(weapon.AngleDegrees, 10f, 175f) * 0.5f * Mathf.Deg2Rad);
                            hit = SVector2.Dot(toKid / d, aimDir) >= cosHalf;
                        }
                        break;
                    }

                    case WeaponShapeKind.GroundAoe:
                    case WeaponShapeKind.Trap:
                    case WeaponShapeKind.Field:
                    {
                        float r = (weapon.Radius > 0.05f ? weapon.Radius : weapon.TriggerRadius) + playerRadius;
                        hit = SVector2.DistanceSquared(clampedTarget, k.Position) <= r * r;
                        break;
                    }

                    case WeaponShapeKind.Bouncer:
                    {
                        int bounces = Mathf.Max(1, weapon.Count);
                        float reach = maxRange > 0.25f ? maxRange : 9f;
                        float r = (weapon.Radius > 0.05f ? weapon.Radius : 0.9f) + playerRadius;
                        for (int b = 0; b < bounces; b++)
                        {
                            SVector2 c = casterPos + aimDir * (reach * (b + 1) / bounces);
                            if (SVector2.DistanceSquared(c, k.Position) <= r * r)
                            {
                                hit = true;
                                break;
                            }
                        }
                        break;
                    }

                    default:
                    {
                        float reach = maxRange > 0.25f ? maxRange : 8f;
                        float halfW = Mathf.Max(0.16f, Mathf.Max(weapon.ProjectileRadius, weapon.Shape == WeaponShapeKind.Spinner ? weapon.Radius : 0f)) + playerRadius;
                        SVector2 rel = k.Position - casterPos;
                        float along = SVector2.Dot(rel, aimDir);
                        if (along >= 0f && along <= reach)
                        {
                            SVector2 closest = casterPos + aimDir * along;
                            hit = SVector2.DistanceSquared(k.Position, closest) <= halfW * halfW;
                        }
                        break;
                    }
                }

                if (!hit) continue;

                Color ringCol = isRedWarning
                    ? warningColor
                    : (isHeal ? ReachFriendColor : ResolveEnemyReachColor());
                float pad = isHeal ? HealReachPad : ReachPad;
                if (isHeal || isRedWarning)
                    AppendCircleRing(k.Position, playerRadius + pad, ReachRingWidth, y + 0.004f, ringCol);
                else
                    AppendDashedCircleRing(k.Position, playerRadius + pad, ReachRingWidth, y + 0.004f, ringCol, EnemyReachDashCount);
            }
        }

        static Color ResolveEnemyReachColor()
        {
            SettingsStore settings = SettingsStore.Current;
            if (settings != null && settings.ResolvedEnemyHighlightMode != SettingsStore.EnemyHighlightRed)
            {
                Color c = KalyeahResources.ResolveEnemyHighlightColor(settings.ResolvedEnemyHighlightMode);
                return new Color(c.r, c.g, c.b, ReachEnemyColor.a);
            }
            return ReachEnemyColor;
        }

        void AppendDiscFill(SVector2 center, float radius, float y, Color color)
        {
            if (radius <= 0.01f || color.a <= 0.002f) return;
            Color32 c32 = color;
            int centerIdx = PushAimVert(KalyeahResources.World(center, y), c32, isEdge: false);
            if (centerIdx < 0) return;

            int firstOuter = _aimVertCount;
            if (firstOuter + CircleSegments >= MaxAimVertices) return;

            for (int i = 0; i < CircleSegments; i++)
                PushAimVert(KalyeahResources.World(center + UnitCircle[i] * radius, y), c32, isEdge: false);

            for (int i = 0; i < CircleSegments; i++)
            {
                int a = firstOuter + i;
                int b = firstOuter + ((i + 1) % CircleSegments);
                PushAimTri(centerIdx, b, a);
            }
        }

        void AppendCircleRing(SVector2 center, float radius, float width, float y, Color color)
        {
            if (radius <= 0.01f || width <= 0.002f || color.a <= 0.002f) return;
            // Align outer edge strictly to exact gameplay radius (no outward over-inflation).
            float outer = radius;
            float inner = Mathf.Max(0.005f, radius - width);
            AppendAnnulus(_aimVerts, _aimUvs, _aimColors, _aimTris, ref _aimVertCount, ref _aimTriCount, MaxAimVertices, MaxAimIndices, UnitCircle, CircleSegments, center, inner, outer, y, color);
        }

        void AppendDashedCircleRing(SVector2 center, float radius, float width, float y, Color color, int dashCount)
        {
            if (radius <= 0.01f || width <= 0.002f || color.a <= 0.002f) return;
            int dashes = Mathf.Clamp(dashCount, 4, CircleSegments);
            float outer = radius;
            float inner = Mathf.Max(0.005f, radius - width);
            float step = MathF.PI * 2f / dashes;
            float span = step * 0.58f;
            Color32 c32 = color;

            for (int i = 0; i < dashes; i++)
            {
                if (_aimVertCount + 4 >= MaxAimVertices || _aimTriCount + 6 >= MaxAimIndices) break;
                float a0 = i * step;
                float a1 = a0 + span;
                var d0 = new SVector2(MathF.Cos(a0), MathF.Sin(a0));
                var d1 = new SVector2(MathF.Cos(a1), MathF.Sin(a1));

                int i0 = PushAimVert(KalyeahResources.World(center + d0 * inner, y), c32);
                int o0 = PushAimVert(KalyeahResources.World(center + d0 * outer, y), c32);
                int o1 = PushAimVert(KalyeahResources.World(center + d1 * outer, y), c32);
                int i1 = PushAimVert(KalyeahResources.World(center + d1 * inner, y), c32);
                PushAimTri(i0, o0, o1);
                PushAimTri(i0, o1, i1);
            }
        }

        void AppendConvexFan(SVector2[] poly, int count, float y, Color color)
        {
            if (count < 3 || color.a <= 0.002f) return;
            if (_aimVertCount + count >= MaxAimVertices) return;
            Color32 c32 = color;
            int baseIdx = _aimVertCount;
            for (int i = 0; i < count; i++)
                PushAimVert(KalyeahResources.World(poly[i], y), c32, isEdge: false);

            for (int i = 1; i < count - 1; i++)
                PushAimTri(baseIdx, baseIdx + i + 1, baseIdx + i);
        }

        void AppendClosedPolyline(SVector2[] pts, int count, float width, float y, Color color)
        {
            if (count < 2 || color.a <= 0.002f) return;
            for (int i = 0; i < count - 1; i++)
                AppendSegment(pts[i], pts[i + 1], width, y, color);
        }

        void AppendSegment(SVector2 a, SVector2 b, float width, float y, Color color)
        {
            SVector2 diff = b - a;
            float lenSq = diff.LengthSquared();
            if (lenSq < 1e-6f || color.a <= 0.002f) return;
            if (_aimVertCount + 4 >= MaxAimVertices || _aimTriCount + 6 >= MaxAimIndices) return;

            SVector2 dir = diff / MathF.Sqrt(lenSq);
            SVector2 side = Perp(dir) * (width * 0.5f);
            Color32 c32 = color;

            int v0 = PushAimVert(KalyeahResources.World(a + side, y), c32);
            int v1 = PushAimVert(KalyeahResources.World(b + side, y), c32);
            int v2 = PushAimVert(KalyeahResources.World(b - side, y), c32);
            int v3 = PushAimVert(KalyeahResources.World(a - side, y), c32);
            PushAimTri(v0, v1, v2);
            PushAimTri(v0, v2, v3);
        }

        void RebuildMoveMarkerGeometry()
        {
            _moveVertCount = 0;
            _moveTriCount = 0;

            if (!_hasMoveTarget)
            {
                CommitMesh(_moveMesh, _moveObject, _moveVerts, _moveUvs, _moveColors, _moveTris, 0, 0);
                return;
            }

            float prog = MoveShrinkProgress();
            float radius = Mathf.Lerp(MoveStartRadius, MoveDotRadius, prog);
            float shut = SmoothStep(MoveCloseThreshold, 1f, prog);
            float y = GroundY + MoveMarkerHeight;

            if (shut >= 0.995f)
            {
                AppendDiscToMove(_moveTargetSim, MoveDotRadius + 0.035f, y - 0.002f, MoveShadowColor);
                AppendDiscToMove(_moveTargetSim, MoveDotRadius, y, MoveDotColor);
            }
            else
            {
                float ringWidth = Mathf.Lerp(0.095f, 0.042f, prog);
                float inner = Mathf.Lerp(Mathf.Max(0.01f, radius - ringWidth * 0.5f), 0f, shut);
                float outer = radius + ringWidth * 0.5f;

                Color shadow = WithAlpha(MoveShadowColor, MoveShadowColor.a * (1f - shut * 0.6f));
                AppendAnnulus(
                    _moveVerts, _moveUvs, _moveColors, _moveTris,
                    ref _moveVertCount, ref _moveTriCount,
                    MaxMoveVertices, MaxMoveIndices,
                    MoveUnitCircle, MoveRingSegments,
                    _moveTargetSim, Mathf.Max(0f, inner - 0.025f), outer + 0.025f, y - 0.002f, shadow);

                Color ringCol = Color.Lerp(MoveRingColor, MoveDotColor, shut);
                AppendAnnulus(
                    _moveVerts, _moveUvs, _moveColors, _moveTris,
                    ref _moveVertCount, ref _moveTriCount,
                    MaxMoveVertices, MaxMoveIndices,
                    MoveUnitCircle, MoveRingSegments,
                    _moveTargetSim, inner, outer, y, ringCol);
            }

            CommitMesh(_moveMesh, _moveObject, _moveVerts, _moveUvs, _moveColors, _moveTris, _moveVertCount, _moveTriCount);
        }

        void AppendDiscToMove(SVector2 center, float radius, float y, Color color)
        {
            if (_moveVertCount + MoveRingSegments + 1 >= MaxMoveVertices || _moveTriCount + MoveRingSegments * 3 >= MaxMoveIndices)
                return;

            Color32 c32 = color;
            int cIdx = _moveVertCount;
            _moveVerts[_moveVertCount] = KalyeahResources.World(center, y);
            _moveUvs[_moveVertCount] = Vector2.up;
            _moveColors[_moveVertCount++] = c32;

            int first = _moveVertCount;
            for (int i = 0; i < MoveRingSegments; i++)
            {
                _moveVerts[_moveVertCount] = KalyeahResources.World(center + MoveUnitCircle[i] * radius, y);
                _moveUvs[_moveVertCount] = Vector2.up;
                _moveColors[_moveVertCount++] = c32;
            }

            for (int i = 0; i < MoveRingSegments; i++)
            {
                _moveTris[_moveTriCount++] = cIdx;
                _moveTris[_moveTriCount++] = first + ((i + 1) % MoveRingSegments);
                _moveTris[_moveTriCount++] = first + i;
            }
        }

        static void AppendAnnulus(
            Vector3[] verts,
            Vector2[] uvs,
            Color32[] colors,
            int[] tris,
            ref int vertCount,
            ref int triCount,
            int maxVerts,
            int maxTris,
            SVector2[] unit,
            int segments,
            SVector2 center,
            float innerRadius,
            float outerRadius,
            float y,
            Color color)
        {
            if (vertCount + segments * 2 >= maxVerts || triCount + segments * 6 >= maxTris) return;
            Color32 c32 = color;
            int start = vertCount;

            for (int i = 0; i < segments; i++)
            {
                SVector2 u = unit[i];
                float ux = i / (float)segments;
                verts[vertCount] = KalyeahResources.World(center + u * innerRadius, y);
                uvs[vertCount] = new Vector2(ux, 1f);
                colors[vertCount++] = c32;
                verts[vertCount] = KalyeahResources.World(center + u * outerRadius, y);
                uvs[vertCount] = new Vector2(ux, 1f);
                colors[vertCount++] = c32;
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int i0 = start + i * 2;
                int o0 = i0 + 1;
                int i1 = start + next * 2;
                int o1 = i1 + 1;

                tris[triCount++] = i0;
                tris[triCount++] = o0;
                tris[triCount++] = o1;
                tris[triCount++] = i0;
                tris[triCount++] = o1;
                tris[triCount++] = i1;
            }
        }

        static void CommitMesh(Mesh mesh, GameObject go, Vector3[] verts, Vector2[] uvs, Color32[] colors, int[] tris, int vertCount, int triCount)
        {
            if (mesh == null || go == null) return;
            if (vertCount <= 0 || triCount <= 0)
            {
                mesh.Clear();
                if (go.activeSelf) go.SetActive(false);
                return;
            }

            mesh.Clear();
            mesh.SetVertices(verts, 0, vertCount);
            mesh.SetUVs(0, uvs, 0, vertCount);
            mesh.SetColors(colors, 0, vertCount);
            mesh.SetTriangles(tris, 0, triCount, 0, calculateBounds: true, baseVertex: 0);
            if (!go.activeSelf) go.SetActive(true);
        }

        int PushAimVert(Vector3 world, Color32 color, bool isEdge = true)
        {
            if (_aimVertCount >= MaxAimVertices) return -1;
            int idx = _aimVertCount;
            _aimVerts[_aimVertCount] = world;
            _aimUvs[_aimVertCount] = isEdge ? Vector2.up : Vector2.zero;
            _aimColors[_aimVertCount++] = color;
            return idx;
        }

        void PushAimTri(int a, int b, int c)
        {
            if (_aimTriCount + 3 > MaxAimIndices || a < 0 || b < 0 || c < 0) return;
            _aimTris[_aimTriCount++] = a;
            _aimTris[_aimTriCount++] = b;
            _aimTris[_aimTriCount++] = c;
        }

        float MoveShrinkProgress()
        {
            if (ReduceMotion || MoveShrinkDuration <= 0.001f) return 1f;
            float left = Mathf.Clamp01(_movePopLeft / MoveShrinkDuration);
            return 1f - left * left;
        }

        static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        static SVector2 NormalizeOrFallback(SVector2 v, SVector2 fallback)
        {
            float lenSq = v.LengthSquared();
            if (lenSq > 1e-6f) return v / MathF.Sqrt(lenSq);
            float fbSq = fallback.LengthSquared();
            return fbSq > 1e-6f ? fallback / MathF.Sqrt(fbSq) : new SVector2(0f, -1f);
        }

        static SVector2 Perp(SVector2 dir) => new SVector2(-dir.Y, dir.X);

        static SVector2 Rotate(SVector2 v, float rad)
        {
            float c = MathF.Cos(rad);
            float s = MathF.Sin(rad);
            return new SVector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
    }
}
