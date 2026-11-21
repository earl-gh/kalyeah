using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// Prewarmed, zero-GC pool of sunlit 90s Filipino Komiks / Arcane combat bursts powered by <c>Kalyeah/ArcaneFx</c>.
    /// Guarantees zero mid-round Instantiate/Destroy/LINQ, a hard active-particle cap, &lt;= 3 local alpha-overdraw layers,
    /// and &lt; 150 ms street-komiks telegraph/hit-confirm bursts.
    /// </summary>
    public sealed class FxPool : IDisposable
    {
        public const string ShaderName = "Kalyeah/ArcaneFx";
        public const int DefaultCapacity = 64;
        public const int MaxOverdrawLayers = 3;
        public const float MaxBurstSeconds = 0.145f;

        const byte PriorityDecorative = 0; // footstep / sprint dust puffs
        const byte PrioritySecondary = 1;  // muzzle snaps, overhead heal cross
        const byte PriorityCombat = 2;     // hit confirms, shockwaves, slash arcs, polymorph, cleanse

        // Cached Shader property IDs — zero string hashing in Spawn* or Tick.
        static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int PropTint = Shader.PropertyToID("_Tint");
        static readonly int PropInkColor = Shader.PropertyToID("_InkColor");
        static readonly int PropPhase = Shader.PropertyToID("_Phase");
        static readonly int PropSoftFade = Shader.PropertyToID("_SoftFade");
        static readonly int PropFresnel = Shader.PropertyToID("_FresnelSoftness");
        static readonly int PropSunlit = Shader.PropertyToID("_SunlitLift");
        static readonly int PropScroll = Shader.PropertyToID("_ScrollSpeed");
        static readonly int PropHardEdgeWidth = Shader.PropertyToID("_HardEdgeWidth");
        static readonly int PropDashCount = Shader.PropertyToID("_DashCount");
        static readonly int PropEdgeBoost = Shader.PropertyToID("_EdgeBoost");
        static readonly int PropInteriorAlpha = Shader.PropertyToID("_InteriorAlpha");

        // Sunlit 90s province komiks palette (matching scripts/view/fx_hero_actions.gd & fx_heals.gd).
        static readonly Color InkEspresso = new Color(0.20f, 0.15f, 0.14f, 0.94f);
        static readonly Color HitAmber = new Color(0.99f, 0.76f, 0.24f, 0.98f);
        static readonly Color MuzzleGold = new Color(1.00f, 0.92f, 0.62f, 0.96f);
        static readonly Color BagsakGold = new Color(0.98f, 0.78f, 0.24f, 0.96f);
        static readonly Color BatoTeamDefault = new Color(0.24f, 0.56f, 1.00f, 0.94f);
        static readonly Color HealEmerald = new Color(0.24f, 0.92f, 0.56f, 0.96f);
        static readonly Color HealCrossJade = new Color(0.42f, 0.98f, 0.72f, 0.98f);
        static readonly Color ChalkDust = new Color(0.92f, 0.86f, 0.74f, 0.66f);
        static readonly Color LataTinPuff = new Color(0.84f, 0.92f, 0.98f, 0.94f);
        static readonly Color CleanseCyan = new Color(0.32f, 0.88f, 0.92f, 0.98f);

        static readonly Color32 VertexHotCore = new Color32(255, 252, 240, 255);
        static readonly Color32 VertexBody = new Color32(236, 232, 224, 242);
        static readonly Color32 VertexSoftInner = new Color32(245, 242, 232, 32);
        static readonly Color32 VertexInkEdge = new Color32(42, 30, 28, 245);

        sealed class FxNode
        {
            public GameObject Go;
            public Transform Tr;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public MaterialPropertyBlock Block;

            public Vector3 StartPos;
            public Vector3 Velocity;
            public Vector3 StartScale;
            public Vector3 EndScale;
            public Vector2 PlanarPos;
            public float PlanarRadius;
            public float Elapsed;
            public float Duration;
            public Color ColorTint;
            public float SunlitLift;
            public float ScrollSpeed;
            public float FresnelSoftness;
            public float HardEdgeWidth;
            public float DashCount;
            public float EdgeBoost;
            public float InteriorAlpha;
            public bool Billboard;
            public float SpinSpeedDeg;
            public float BaseRollDeg;
            public byte Priority;
        }

        readonly Transform _root;
        readonly int _maxActive;
        readonly Stack<FxNode> _freePool;
        readonly FxNode[] _active;
        int _activeCount;
        int _spawnSerial;
        int _cosmeticSpawnSerial;

        readonly Material _sharedMaterial;
        readonly bool _ownsMaterial;
        readonly Mesh _ringMesh;
        readonly Mesh _starMesh;
        readonly Mesh _slashArcMesh;
        readonly Mesh _streakMesh;
        readonly Mesh _puffMesh;
        readonly Mesh _plusMesh;
        readonly Mesh _sparkMesh;

        Camera _cachedCamera;
        bool _disposed;

        public Transform Root => _root;
        public int Capacity => _maxActive;
        public int ActiveCount => _activeCount;
        public int FreeCount => _freePool.Count;
        public float ActiveParticleBudgetScale => SettingsStore.Current.GraphicsProfile.ParticleBudgetScale;
        public int EffectiveDecorativeCap => ComputeDecorativeCap(SettingsStore.Current.GraphicsProfile);
        public int EffectiveOverdrawCap => SettingsStore.Current.GraphicsProfile.QualityIndex == SettingsStore.GraphicsLow ? 2 : MaxOverdrawLayers;
        public bool ReduceMotion { get; set; }
        public Camera ViewCamera { get; set; }

        public FxPool(Transform parent = null, int capacity = DefaultCapacity, bool reduceMotion = false)
        {
            _maxActive = Mathf.Clamp(capacity, 8, 256);
            ReduceMotion = reduceMotion;

            var rootGo = new GameObject("FxPool");
            _root = rootGo.transform;
            if (parent != null) _root.SetParent(parent, false);

            Material loaded = KalyeahResources.Material("ArcaneFx", ShaderName);
            if (loaded != null)
            {
                _sharedMaterial = loaded;
                _ownsMaterial = Resources.Load<Material>("KalyeahMaterials/ArcaneFx") == null;
            }
            else
            {
                Shader fallback = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (fallback != null)
                {
                    _sharedMaterial = new Material(fallback);
                    _ownsMaterial = true;
                }
            }
            if (_sharedMaterial != null) _sharedMaterial.enableInstancing = true;

            _ringMesh = BuildRingMesh();
            _starMesh = BuildStarMesh(4, 0.36f, 1.0f);
            _slashArcMesh = BuildSlashArcMesh();
            _streakMesh = BuildStreakMesh();
            _puffMesh = BuildPuffMesh();
            _plusMesh = BuildPlusMesh();
            _sparkMesh = BuildSparkMesh();

            _freePool = new Stack<FxNode>(_maxActive);
            _active = new FxNode[_maxActive];
            Prewarm();
        }

        void Prewarm()
        {
            for (int i = 0; i < _maxActive; i++)
            {
                var go = new GameObject($"FxSlot_{i:D2}");
                Transform tr = go.transform;
                tr.SetParent(_root, false);

                var filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = _starMesh;

                var renderer = go.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                if (_sharedMaterial != null) renderer.sharedMaterial = _sharedMaterial;

                go.SetActive(false);

                _freePool.Push(new FxNode
                {
                    Go = go,
                    Tr = tr,
                    Filter = filter,
                    Renderer = renderer,
                    Block = new MaterialPropertyBlock()
                });
            }
        }

        /// <summary>Waist-height 90s komiks 4-point star impact confirm (&lt; 150 ms; never draws a ground ring).</summary>
        public void SpawnHitConfirm(Vector3 worldPos, Color? tint = null, bool heavy = false)
        {
            Color col = tint ?? HitAmber;
            float size = heavy ? 1.38f : 1.06f;
            float dur = heavy ? 0.14f : 0.12f;
            float roll = NextAngleDeg();
            SpawnNode(
                mesh: _starMesh,
                pos: worldPos,
                rot: Quaternion.identity,
                velocity: ReduceMotion ? Vector3.zero : new Vector3(0f, heavy ? 1.1f : 0.65f, 0f),
                startScale: new Vector3(size * 0.52f, size * 0.52f, 1f),
                endScale: new Vector3(size, size, 1f),
                planarRadius: size * 0.55f,
                duration: dur,
                tint: col,
                sunlitLift: 0.16f,
                scrollSpeed: 1.2f,
                fresnel: 0f,
                billboard: true,
                baseRollDeg: roll,
                spinSpeedDeg: ReduceMotion ? 0f : 95f,
                priority: PriorityCombat);
        }

        public void SpawnHitConfirm(SVector2 groundPos, Color? tint = null, bool heavy = false)
            => SpawnHitConfirm(KalyeahResources.World(groundPos, 0.90f), tint, heavy);

        /// <summary>Snappy directional slingshot / cast muzzle streak (&lt; 115 ms).</summary>
        public void SpawnMuzzleSnap(Vector3 origin, Vector3 direction, Color? teamColor = null)
        {
            Color col = Color.Lerp(teamColor ?? BatoTeamDefault, MuzzleGold, 0.45f);
            col.a = 0.96f;
            Quaternion rot = PlanarOr3DLookRotation(direction);
            SpawnNode(
                mesh: _streakMesh,
                pos: origin,
                rot: rot,
                velocity: Vector3.zero,
                startScale: new Vector3(0.42f, 1f, 0.58f),
                endScale: new Vector3(0.68f, 1f, 1.32f),
                planarRadius: 0.65f,
                duration: 0.11f,
                tint: col,
                sunlitLift: 0.18f,
                scrollSpeed: 2.2f,
                fresnel: 0.08f,
                billboard: false,
                baseRollDeg: 0f,
                spinSpeedDeg: 0f,
                priority: PrioritySecondary);
        }

        public void SpawnMuzzleSnap(SVector2 groundOrigin, SVector2 groundDir, Color? teamColor = null)
            => SpawnMuzzleSnap(KalyeahResources.World(groundOrigin, 0.95f), KalyeahResources.World(groundDir, 0f), teamColor);

        /// <summary>Hollow radial street shockwave ring for <c>Bagsak ng Kalye</c> and <c>Bato (Heavy)</c> (&lt; 150 ms).</summary>
        public void SpawnGroundShockwave(Vector3 center, float radius = 2.4f, Color? teamColor = null, bool isBagsak = false, bool isEnemy = false)
        {
            float clampedRadius = Mathf.Max(0.45f, radius);
            Color baseCol = teamColor ?? BatoTeamDefault;
            Color col = isBagsak ? Color.Lerp(baseCol, BagsakGold, 0.65f) : baseCol;
            col.a = 0.96f;
            Vector3 at = new Vector3(center.x, center.y + 0.03f, center.z);
            float startR = clampedRadius * (isBagsak ? 0.36f : 0.44f);

            SpawnNode(
                mesh: _ringMesh,
                pos: at,
                rot: Quaternion.identity,
                velocity: Vector3.zero,
                startScale: new Vector3(startR, 1f, startR),
                endScale: new Vector3(clampedRadius, 1f, clampedRadius),
                planarRadius: clampedRadius,
                duration: isBagsak ? MaxBurstSeconds : 0.135f,
                tint: col,
                sunlitLift: isBagsak ? 0.20f : 0.12f,
                scrollSpeed: 1.5f,
                fresnel: 0f,
                billboard: false,
                baseRollDeg: 0f,
                spinSpeedDeg: 0f,
                priority: PriorityCombat,
                hardEdgeWidth: 0.30f,
                dashCount: isEnemy ? 16f : 0f,
                edgeBoost: 0.32f,
                interiorAlpha: 0.22f);
        }

        public void SpawnGroundShockwave(SVector2 groundCenter, float radius = 2.4f, Color? teamColor = null, bool isBagsak = false, bool isEnemy = false)
            => SpawnGroundShockwave(KalyeahResources.World(groundCenter, 0f), radius, teamColor, isBagsak, isEnemy);

        /// <summary>Expanding friendly emerald heal ring + rising komiks heal cross (&lt; 150 ms).</summary>
        public void SpawnHealRing(Vector3 center, float radius = 1.6f)
        {
            float r = Mathf.Max(0.6f, radius);
            Vector3 groundAt = new Vector3(center.x, center.y + 0.035f, center.z);
            SpawnNode(
                mesh: _ringMesh,
                pos: groundAt,
                rot: Quaternion.identity,
                velocity: Vector3.zero,
                startScale: new Vector3(r * 0.5f, 1f, r * 0.5f),
                endScale: new Vector3(r, 1f, r),
                planarRadius: r,
                duration: 0.14f,
                tint: HealEmerald,
                sunlitLift: 0.16f,
                scrollSpeed: 1.1f,
                fresnel: 0f,
                billboard: false,
                baseRollDeg: 0f,
                spinSpeedDeg: 0f,
                priority: PriorityCombat,
                hardEdgeWidth: 0.30f,
                dashCount: 0f,
                edgeBoost: 0.28f,
                interiorAlpha: 0.20f);

            Vector3 crossPos = new Vector3(center.x, center.y + (ReduceMotion ? 1.55f : 1.25f), center.z);
            Vector3 crossVel = ReduceMotion ? Vector3.zero : new Vector3(0f, 2.4f, 0f);
            SpawnNode(
                mesh: _plusMesh,
                pos: crossPos,
                rot: Quaternion.identity,
                velocity: crossVel,
                startScale: new Vector3(0.38f, 0.38f, 1f),
                endScale: new Vector3(0.72f, 0.72f, 1f),
                planarRadius: 0.45f,
                duration: 0.14f,
                tint: HealCrossJade,
                sunlitLift: 0.22f,
                scrollSpeed: 0.8f,
                fresnel: 0f,
                billboard: true,
                baseRollDeg: 0f,
                spinSpeedDeg: 0f,
                priority: PrioritySecondary);
        }

        public void SpawnHealRing(SVector2 groundCenter, float radius = 1.6f)
            => SpawnHealRing(KalyeahResources.World(groundCenter, 0f), radius);

        /// <summary>Curved komiks scissor-snip crescent sweep for <c>Gunting</c> light &amp; heavy (&lt; 150 ms).</summary>
        public void SpawnSlashArc(Vector3 origin, Vector3 direction, float range = 3.2f, float angleDeg = 55f, Color? teamColor = null, bool heavy = false, bool isEnemy = false)
        {
            float reach = Mathf.Max(0.8f, range);
            float halfRad = Mathf.Clamp(angleDeg, 20f, 130f) * 0.5f * Mathf.Deg2Rad;
            // Canonical mesh spans 60 deg (half-angle 30 deg, tan = 0.57735f).
            float widthFactor = Mathf.Clamp(Mathf.Tan(halfRad) / 0.57735f, 0.35f, 2.25f);
            float lateral = reach * widthFactor;

            Color col = teamColor ?? BatoTeamDefault;
            if (heavy) col = Color.Lerp(col, HitAmber, 0.30f);
            col.a = 0.96f;

            Quaternion rot = PlanarLookRotation(direction);
            SpawnNode(
                mesh: _slashArcMesh,
                pos: origin,
                rot: rot,
                velocity: Vector3.zero,
                startScale: new Vector3(lateral * 0.72f, 1f, reach * 0.68f),
                endScale: new Vector3(lateral, 1f, reach),
                planarRadius: reach * 0.75f,
                duration: heavy ? 0.14f : 0.115f,
                tint: col,
                sunlitLift: heavy ? 0.20f : 0.14f,
                scrollSpeed: 2.4f,
                fresnel: 0f,
                billboard: false,
                baseRollDeg: 0f,
                spinSpeedDeg: 0f,
                priority: PriorityCombat,
                hardEdgeWidth: 0.28f,
                dashCount: isEnemy ? 8f : 0f,
                edgeBoost: 0.30f,
                interiorAlpha: 0.26f);
        }

        public void SpawnSlashArc(SVector2 groundOrigin, SVector2 groundDir, float range = 3.2f, float angleDeg = 55f, Color? teamColor = null, bool heavy = false, bool isEnemy = false)
            => SpawnSlashArc(KalyeahResources.World(groundOrigin, 0.90f), KalyeahResources.World(groundDir, 0f), range, angleDeg, teamColor, heavy, isEnemy);

        /// <summary>Warm provincial street dust puff (&lt; 140 ms; skipped under ReduceMotion or local overdraw saturation).</summary>
        public void SpawnDustPuff(Vector3 feetPos, float scale = 0.9f)
        {
            if (ReduceMotion) return;
            float s = Mathf.Max(0.25f, scale);
            Vector3 at = new Vector3(feetPos.x, feetPos.y + 0.28f, feetPos.z);
            SpawnNode(
                mesh: _puffMesh,
                pos: at,
                rot: Quaternion.identity,
                velocity: new Vector3(0f, 0.85f, 0f),
                startScale: new Vector3(s * 0.5f, s * 0.45f, 1f),
                endScale: new Vector3(s * 1.1f, s * 0.95f, 1f),
                planarRadius: s * 0.65f,
                duration: 0.135f,
                tint: ChalkDust,
                sunlitLift: 0.08f,
                scrollSpeed: 0.7f,
                fresnel: 0f,
                billboard: true,
                baseRollDeg: NextAngleDeg(),
                spinSpeedDeg: 35f,
                priority: PriorityDecorative);
        }

        public void SpawnDustPuff(SVector2 groundPos, float scale = 0.9f)
            => SpawnDustPuff(KalyeahResources.World(groundPos, 0f), scale);

        /// <summary>Comic tin-can transformation puff for <c>Lata</c> polymorph (&lt; 145 ms).</summary>
        public void SpawnPolymorphPuff(Vector3 worldPos, Color? teamColor = null)
        {
            Color col = teamColor.HasValue ? Color.Lerp(LataTinPuff, teamColor.Value, 0.35f) : LataTinPuff;
            col.a = 0.96f;
            SpawnNode(
                mesh: _puffMesh,
                pos: worldPos,
                rot: Quaternion.identity,
                velocity: ReduceMotion ? Vector3.zero : new Vector3(0f, 1.15f, 0f),
                startScale: new Vector3(0.62f, 0.62f, 1f),
                endScale: new Vector3(1.48f, 1.42f, 1f),
                planarRadius: 0.95f,
                duration: 0.14f,
                tint: col,
                sunlitLift: 0.20f,
                scrollSpeed: 1.4f,
                fresnel: 0f,
                billboard: true,
                baseRollDeg: NextAngleDeg(),
                spinSpeedDeg: ReduceMotion ? 0f : -65f,
                priority: PriorityCombat);
        }

        public void SpawnPolymorphPuff(SVector2 groundPos, Color? teamColor = null)
            => SpawnPolymorphPuff(KalyeahResources.World(groundPos, 0.80f), teamColor);

        /// <summary>Sunlit cyan spark burst for <c>Luksong-Tinik</c> vault + <c>SLOW</c> cleanse (&lt; 140 ms).</summary>
        public void SpawnCleanseSpark(Vector3 worldPos, Vector3 moveDir = default)
        {
            Vector3 drift = (!ReduceMotion && moveDir.sqrMagnitude > 0.001f)
                ? -moveDir.normalized * 1.1f + new Vector3(0f, 1.0f, 0f)
                : (ReduceMotion ? Vector3.zero : new Vector3(0f, 1.0f, 0f));

            SpawnNode(
                mesh: _sparkMesh,
                pos: worldPos,
                rot: Quaternion.identity,
                velocity: drift,
                startScale: new Vector3(0.55f, 0.65f, 1f),
                endScale: new Vector3(1.32f, 1.52f, 1f),
                planarRadius: 0.80f,
                duration: 0.13f,
                tint: CleanseCyan,
                sunlitLift: 0.24f,
                scrollSpeed: 1.8f,
                fresnel: 0f,
                billboard: true,
                baseRollDeg: 0f,
                spinSpeedDeg: ReduceMotion ? 0f : 110f,
                priority: PriorityCombat);
        }

        public void SpawnCleanseSpark(SVector2 groundPos, SVector2 groundDir = default)
            => SpawnCleanseSpark(KalyeahResources.World(groundPos, 0.85f), KalyeahResources.World(groundDir, 0f));

        /// <summary>Advances all live bursts in O(active) with swap-remove recycling and zero heap allocations.</summary>
        public void Tick(float dt)
        {
            if (_disposed || _activeCount == 0) return;
            float step = dt > 0f ? dt : 0f;

            Quaternion billboardRot = ResolveBillboardRotation();

            for (int i = _activeCount - 1; i >= 0; i--)
            {
                FxNode node = _active[i];
                node.Elapsed += step;
                if (node.Elapsed >= node.Duration)
                {
                    RecycleAt(i);
                    continue;
                }

                ApplyVisualState(node, billboardRot);
            }
        }

        /// <summary>Counts active overlapping alpha layers at <paramref name="worldPos"/> (guaranteed &lt;= <see cref="MaxOverdrawLayers"/>).</summary>
        public int CountOverlappingLayers(Vector3 worldPos, float probeRadius = 0.2f)
        {
            Vector2 p = new Vector2(worldPos.x, worldPos.z);
            float r = Mathf.Max(0.05f, probeRadius);
            int overlaps = 0;
            for (int i = 0; i < _activeCount; i++)
            {
                FxNode a = _active[i];
                float reach = (a.PlanarRadius + r) * 0.55f;
                if ((a.PlanarPos - p).sqrMagnitude <= reach * reach)
                    overlaps++;
            }
            return overlaps;
        }

        /// <summary>Immediately deactivates all live bursts and returns them to the prewarmed stack.</summary>
        public void ClearAll()
        {
            for (int i = _activeCount - 1; i >= 0; i--)
                RecycleAt(i);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ClearAll();
            if (_root != null) KalyeahResources.Kill(_root.gameObject);
            KalyeahResources.Kill(_ringMesh);
            KalyeahResources.Kill(_starMesh);
            KalyeahResources.Kill(_slashArcMesh);
            KalyeahResources.Kill(_streakMesh);
            KalyeahResources.Kill(_puffMesh);
            KalyeahResources.Kill(_plusMesh);
            KalyeahResources.Kill(_sparkMesh);
            if (_ownsMaterial && _sharedMaterial != null) KalyeahResources.Kill(_sharedMaterial);
        }

        void SpawnNode(
            Mesh mesh,
            Vector3 pos,
            Quaternion rot,
            Vector3 velocity,
            Vector3 startScale,
            Vector3 endScale,
            float planarRadius,
            float duration,
            Color tint,
            float sunlitLift,
            float scrollSpeed,
            float fresnel,
            bool billboard,
            float baseRollDeg,
            float spinSpeedDeg,
            byte priority,
            float hardEdgeWidth = 0f,
            float dashCount = 0f,
            float edgeBoost = 0.12f,
            float interiorAlpha = 0.16f)
        {
            if (_disposed || mesh == null) return;

            // Riot VFX minspec fairness rule: halve purely cosmetic secondary/decorative spawns on Low
            // while keeping EVERY gameplay-impacting combat burst (PriorityCombat) 100% identical in size, shape & timing.
            GraphicsTierProfile profile = SettingsStore.Current.GraphicsProfile;
            if (ShouldThrottleCosmetic(priority, profile))
                return;

            Vector2 planar = new Vector2(pos.x, pos.z);
            if (!EnforceOverdrawCap(planar, planarRadius, priority, profile))
                return;

            if (_freePool.Count == 0)
            {
                if (!EvictGlobalOldestFor(priority))
                    return;
            }

            FxNode node = _freePool.Pop();
            node.Filter.sharedMesh = mesh;
            node.StartPos = pos;
            node.Velocity = velocity;
            node.StartScale = startScale;
            node.EndScale = endScale;
            node.PlanarPos = planar;
            node.PlanarRadius = Mathf.Max(0.15f, planarRadius);
            node.Elapsed = 0f;
            node.Duration = Mathf.Clamp(duration, 0.04f, MaxBurstSeconds);
            node.ColorTint = tint;
            node.SunlitLift = sunlitLift;
            node.ScrollSpeed = scrollSpeed;
            node.FresnelSoftness = fresnel;
            node.HardEdgeWidth = hardEdgeWidth;
            node.DashCount = dashCount;
            node.EdgeBoost = edgeBoost;
            node.InteriorAlpha = interiorAlpha;
            node.Billboard = billboard;
            node.BaseRollDeg = baseRollDeg;
            node.SpinSpeedDeg = spinSpeedDeg;
            node.Priority = priority;

            if (!billboard)
                node.Tr.localRotation = rot;

            _active[_activeCount++] = node;
            node.Go.SetActive(true);
            ApplyVisualState(node, ResolveBillboardRotation());
        }

        bool ShouldThrottleCosmetic(byte priority, in GraphicsTierProfile profile)
        {
            if (priority >= PriorityCombat) return false;
            if (profile.QualityIndex == SettingsStore.GraphicsLow || profile.ParticleBudgetScale <= 0.55f)
            {
                _cosmeticSpawnSerial = (_cosmeticSpawnSerial + 1) & 0x7FFF;
                if ((_cosmeticSpawnSerial & 1) == 0)
                    return true;
            }
            return CountCosmeticActive() >= ComputeDecorativeCap(profile);
        }

        int CountCosmeticActive()
        {
            int count = 0;
            for (int i = 0; i < _activeCount; i++)
            {
                if (_active[i].Priority < PriorityCombat) count++;
            }
            return count;
        }

        int ComputeDecorativeCap(in GraphicsTierProfile profile)
        {
            float scale = Mathf.Clamp(profile.ParticleBudgetScale, 0.25f, 1f);
            return Mathf.Max(4, Mathf.RoundToInt(_maxActive * 0.40f * scale));
        }

        /// <summary>Ensures at most <see cref="MaxOverdrawLayers"/> (3, or 2 for cosmetic FX on Low) overlap any street cell.</summary>
        bool EnforceOverdrawCap(Vector2 planar, float radius, byte incomingPriority, in GraphicsTierProfile profile)
        {
            int overlaps = 0;
            int victimIndex = -1;
            byte victimPriority = 255;
            float victimAgeShare = -1f;

            for (int i = 0; i < _activeCount; i++)
            {
                FxNode a = _active[i];
                float reach = (a.PlanarRadius + radius) * 0.55f;
                if ((a.PlanarPos - planar).sqrMagnitude > reach * reach)
                    continue;

                overlaps++;
                float ageShare = a.Duration > 0f ? (a.Elapsed / a.Duration) : 1f;
                if (a.Priority < victimPriority || (a.Priority == victimPriority && ageShare > victimAgeShare))
                {
                    victimPriority = a.Priority;
                    victimAgeShare = ageShare;
                    victimIndex = i;
                }
            }

            int cap = (incomingPriority < PriorityCombat && profile.QualityIndex == SettingsStore.GraphicsLow)
                ? 2
                : MaxOverdrawLayers;
            if (overlaps < cap)
                return true;

            if (incomingPriority < PriorityCombat && victimPriority > incomingPriority)
                return false;

            if (victimIndex >= 0)
                RecycleAt(victimIndex);

            return true;
        }

        bool EvictGlobalOldestFor(byte incomingPriority)
        {
            int victimIndex = -1;
            byte victimPriority = 255;
            float victimAgeShare = -1f;

            for (int i = 0; i < _activeCount; i++)
            {
                FxNode a = _active[i];
                float ageShare = a.Duration > 0f ? (a.Elapsed / a.Duration) : 1f;
                if (a.Priority < victimPriority || (a.Priority == victimPriority && ageShare > victimAgeShare))
                {
                    victimPriority = a.Priority;
                    victimAgeShare = ageShare;
                    victimIndex = i;
                }
            }

            if (victimIndex < 0) return false;
            if (incomingPriority == PriorityDecorative && victimPriority > PriorityDecorative) return false;

            RecycleAt(victimIndex);
            return true;
        }

        void RecycleAt(int index)
        {
            FxNode finished = _active[index];
            int last = _activeCount - 1;
            if (index != last)
                _active[index] = _active[last];
            _active[last] = null;
            _activeCount = last;

            finished.Go.SetActive(false);
            _freePool.Push(finished);
        }

        void ApplyVisualState(FxNode node, Quaternion billboardRot)
        {
            float phase = Mathf.Clamp01(node.Elapsed / node.Duration);
            float oneMinus = 1f - phase;
            float easeOutCubic = 1f - oneMinus * oneMinus * oneMinus;

            Vector3 pos = node.StartPos + node.Velocity * node.Elapsed;
            node.PlanarPos = new Vector2(pos.x, pos.z);
            node.Tr.localPosition = pos;
            node.Tr.localScale = Vector3.LerpUnclamped(node.StartScale, node.EndScale, easeOutCubic);

            if (node.Billboard)
            {
                float roll = node.BaseRollDeg + node.SpinSpeedDeg * node.Elapsed;
                node.Tr.localRotation = billboardRot * Quaternion.Euler(0f, 0f, roll);
            }

            // Crisp frame-0 punch, holding strong readability through 62% of lifetime before painterly fade-out.
            float softFade = phase < 0.62f ? 1f : Mathf.Clamp01((1f - phase) / 0.38f);
            Color tint = node.ColorTint;

            MaterialPropertyBlock block = node.Block;
            block.SetColor(PropBaseColor, tint);
            block.SetColor(PropTint, Color.white);
            block.SetColor(PropInkColor, InkEspresso);
            block.SetFloat(PropPhase, phase);
            block.SetFloat(PropSoftFade, softFade);
            block.SetFloat(PropFresnel, node.FresnelSoftness);
            block.SetFloat(PropSunlit, node.SunlitLift);
            block.SetFloat(PropScroll, node.ScrollSpeed);
            block.SetFloat(PropHardEdgeWidth, node.HardEdgeWidth);
            block.SetFloat(PropDashCount, node.DashCount);
            block.SetFloat(PropEdgeBoost, node.EdgeBoost);
            block.SetFloat(PropInteriorAlpha, node.InteriorAlpha);
            node.Renderer.SetPropertyBlock(block);
        }

        Quaternion ResolveBillboardRotation()
        {
            Camera cam = ViewCamera;
            if (cam == null)
            {
                if (_cachedCamera == null) _cachedCamera = Camera.main;
                cam = _cachedCamera;
            }
            return cam != null ? cam.transform.rotation : Quaternion.identity;
        }

        float NextAngleDeg()
        {
            _spawnSerial = (_spawnSerial + 1) & 0x7FFF;
            return (_spawnSerial * 67) % 360;
        }

        static Quaternion PlanarLookRotation(Vector3 dir)
        {
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 0.0001f) return Quaternion.identity;
            return Quaternion.LookRotation(flat.normalized, Vector3.up);
        }

        static Quaternion PlanarOr3DLookRotation(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return Quaternion.identity;
            Vector3 n = dir.normalized;
            Vector3 up = Mathf.Abs(n.y) > 0.96f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(n, up);
        }

        // -------------------------------------------------------------------------
        // One-time procedural 90s Komiks meshes (ink-rimmed + sunlit gouache core)
        // -------------------------------------------------------------------------

        static Mesh BuildRingMesh()
        {
            const int segs = 24;
            const float rInner = 0.72f;
            const float rMid = 0.90f;
            const float rOuter = 1.00f;

            // (segs + 1) seam vertex reaches uv.x = 1.0 so ArcaneFx dashed enemy shockwaves never smear across the closing wedge.
            int vertCount = (segs + 1) * 3;
            var verts = new Vector3[vertCount];
            var norms = new Vector3[vertCount];
            var cols = new Color32[vertCount];
            var uvs = new Vector2[vertCount];
            var tris = new int[segs * 12];

            for (int i = 0; i <= segs; i++)
            {
                float u = i / (float)segs;
                float a = u * Mathf.PI * 2f;
                float c = Mathf.Cos(a);
                float s = Mathf.Sin(a);

                int b = i * 3;
                verts[b] = new Vector3(c * rInner, 0f, s * rInner);
                verts[b + 1] = new Vector3(c * rMid, 0f, s * rMid);
                verts[b + 2] = new Vector3(c * rOuter, 0f, s * rOuter);

                norms[b] = Vector3.up;
                norms[b + 1] = Vector3.up;
                norms[b + 2] = Vector3.up;

                cols[b] = VertexSoftInner;
                cols[b + 1] = VertexHotCore;
                cols[b + 2] = VertexHotCore;

                uvs[b] = new Vector2(u, 0f);
                uvs[b + 1] = new Vector2(u, 0.7f);
                uvs[b + 2] = new Vector2(u, 1f);
            }

            int t = 0;
            for (int i = 0; i < segs; i++)
            {
                int b0 = i * 3;
                int b1 = (i + 1) * 3;

                // Inner feather -> sunlit core band (+Y clockwise winding)
                tris[t++] = b0;
                tris[t++] = b1 + 1;
                tris[t++] = b0 + 1;
                tris[t++] = b0;
                tris[t++] = b1;
                tris[t++] = b1 + 1;

                // Core band -> hard-edge outer gameplay perimeter
                tris[t++] = b0 + 1;
                tris[t++] = b1 + 2;
                tris[t++] = b0 + 2;
                tris[t++] = b0 + 1;
                tris[t++] = b1 + 1;
                tris[t++] = b1 + 2;
            }

            return MakeMesh("KomiksRing", verts, norms, cols, uvs, tris);
        }

        static Mesh BuildStarMesh(int spikes, float valleyRadius, float tipRadius)
        {
            int points = spikes * 2;
            var verts = new Vector3[1 + points * 2];
            var norms = new Vector3[verts.Length];
            var cols = new Color32[verts.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new int[points * 9];

            verts[0] = Vector3.zero;
            norms[0] = Vector3.back;
            cols[0] = VertexHotCore;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < points; i++)
            {
                float a = (i / (float)points) * Mathf.PI * 2f;
                float rOuter = (i % 2 == 0) ? tipRadius : valleyRadius;
                float rCore = rOuter * 0.80f;
                float c = Mathf.Cos(a);
                float s = Mathf.Sin(a);

                int idxCore = 1 + i * 2;
                int idxEdge = idxCore + 1;

                verts[idxCore] = new Vector3(c * rCore, s * rCore, 0f);
                verts[idxEdge] = new Vector3(c * rOuter, s * rOuter, 0f);
                norms[idxCore] = Vector3.back;
                norms[idxEdge] = Vector3.back;
                cols[idxCore] = VertexHotCore;
                cols[idxEdge] = VertexInkEdge;
                uvs[idxCore] = new Vector2(0.5f + c * 0.4f, 0.5f + s * 0.4f);
                uvs[idxEdge] = new Vector2(0.5f + c * 0.5f, 0.5f + s * 0.5f);
            }

            int t = 0;
            for (int i = 0; i < points; i++)
            {
                int next = (i + 1) % points;
                int c0 = 1 + i * 2;
                int e0 = c0 + 1;
                int c1 = 1 + next * 2;
                int e1 = c1 + 1;

                tris[t++] = 0;
                tris[t++] = c1;
                tris[t++] = c0;

                tris[t++] = c0;
                tris[t++] = e1;
                tris[t++] = e0;

                tris[t++] = c0;
                tris[t++] = c1;
                tris[t++] = e1;
            }

            return MakeMesh("KomiksStar", verts, norms, cols, uvs, tris);
        }

        static Mesh BuildSlashArcMesh()
        {
            const int segs = 12;
            const float halfArc = 30f * Mathf.Deg2Rad;

            var verts = new Vector3[(segs + 1) * 3];
            var norms = new Vector3[verts.Length];
            var cols = new Color32[verts.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new int[segs * 12];

            for (int i = 0; i <= segs; i++)
            {
                float u = i / (float)segs;
                float a = Mathf.Lerp(-halfArc, halfArc, u);
                // Taper crescent thickness smoothly to zero at both tips.
                float taper = Mathf.Sin(u * Mathf.PI);
                float rInner = Mathf.Lerp(0.92f, 0.58f, taper);
                float rCore = Mathf.Lerp(0.96f, 0.88f, taper);
                float rOuter = 1.00f;

                float s = Mathf.Sin(a);
                float c = Mathf.Cos(a);
                int b = i * 3;

                verts[b] = new Vector3(s * rInner, 0f, c * rInner);
                verts[b + 1] = new Vector3(s * rCore, 0f, c * rCore);
                verts[b + 2] = new Vector3(s * rOuter, 0f, c * rOuter);

                norms[b] = Vector3.up;
                norms[b + 1] = Vector3.up;
                norms[b + 2] = Vector3.up;

                cols[b] = VertexSoftInner;
                cols[b + 1] = VertexHotCore;
                cols[b + 2] = VertexHotCore;

                uvs[b] = new Vector2(u, 0f);
                uvs[b + 1] = new Vector2(u, 0.7f);
                uvs[b + 2] = new Vector2(u, 1f);
            }

            int t = 0;
            for (int i = 0; i < segs; i++)
            {
                int b0 = i * 3;
                int b1 = (i + 1) * 3;

                tris[t++] = b0;
                tris[t++] = b0 + 1;
                tris[t++] = b1 + 1;
                tris[t++] = b0;
                tris[t++] = b1 + 1;
                tris[t++] = b1;

                tris[t++] = b0 + 1;
                tris[t++] = b0 + 2;
                tris[t++] = b1 + 2;
                tris[t++] = b0 + 1;
                tris[t++] = b1 + 2;
                tris[t++] = b1 + 1;
            }

            return MakeMesh("KomiksSlashArc", verts, norms, cols, uvs, tris);
        }

        static Mesh BuildStreakMesh()
        {
            var verts = new Vector3[]
            {
                new Vector3(0f, 0f, 0.0f),     // 0 tail ink
                new Vector3(0f, 0f, 0.32f),    // 1 core center
                new Vector3(-0.28f, 0f, 0.28f),// 2 left wing ink
                new Vector3(0.28f, 0f, 0.28f), // 3 right wing ink
                new Vector3(-0.14f, 0f, 0.34f),// 4 left core
                new Vector3(0.14f, 0f, 0.34f), // 5 right core
                new Vector3(0f, 0f, 0.86f),    // 6 tip core
                new Vector3(0f, 0f, 1.00f),    // 7 tip ink
            };
            var norms = new Vector3[verts.Length];
            for (int i = 0; i < norms.Length; i++) norms[i] = Vector3.up;

            var cols = new Color32[]
            {
                VertexInkEdge, VertexHotCore, VertexInkEdge, VertexInkEdge,
                VertexHotCore, VertexHotCore, VertexHotCore, VertexInkEdge
            };
            var uvs = new Vector2[]
            {
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0.35f), new Vector2(0f, 0.3f), new Vector2(1f, 0.3f),
                new Vector2(0.25f, 0.35f), new Vector2(0.75f, 0.35f), new Vector2(0.5f, 0.86f), new Vector2(0.5f, 1f)
            };
            var tris = new int[]
            {
                0, 2, 4,  0, 4, 1,  0, 1, 5,  0, 5, 3,
                4, 6, 1,  1, 6, 5,
                2, 7, 6,  2, 6, 4,  5, 6, 7,  5, 7, 3
            };
            return MakeMesh("KomiksStreak", verts, norms, cols, uvs, tris);
        }

        static Mesh BuildPuffMesh()
        {
            const int segs = 12;
            var verts = new Vector3[1 + segs * 2];
            var norms = new Vector3[verts.Length];
            var cols = new Color32[verts.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new int[segs * 9];

            verts[0] = Vector3.zero;
            norms[0] = Vector3.back;
            cols[0] = VertexHotCore;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                float lobe = 0.44f + 0.08f * Mathf.Abs(Mathf.Cos(a * 2.5f));
                float core = lobe * 0.82f;
                float c = Mathf.Cos(a);
                float s = Mathf.Sin(a);

                int cIdx = 1 + i * 2;
                int eIdx = cIdx + 1;

                verts[cIdx] = new Vector3(c * core, s * core, 0f);
                verts[eIdx] = new Vector3(c * lobe, s * lobe, 0f);
                norms[cIdx] = Vector3.back;
                norms[eIdx] = Vector3.back;
                cols[cIdx] = VertexBody;
                cols[eIdx] = VertexInkEdge;
                uvs[cIdx] = new Vector2(0.5f + c * 0.4f, 0.5f + s * 0.4f);
                uvs[eIdx] = new Vector2(0.5f + c * 0.5f, 0.5f + s * 0.5f);
            }

            int t = 0;
            for (int i = 0; i < segs; i++)
            {
                int next = (i + 1) % segs;
                int c0 = 1 + i * 2;
                int e0 = c0 + 1;
                int c1 = 1 + next * 2;
                int e1 = c1 + 1;

                tris[t++] = 0;
                tris[t++] = c1;
                tris[t++] = c0;

                tris[t++] = c0;
                tris[t++] = e1;
                tris[t++] = e0;

                tris[t++] = c0;
                tris[t++] = c1;
                tris[t++] = e1;
            }

            return MakeMesh("KomiksPuff", verts, norms, cols, uvs, tris);
        }

        static Mesh BuildPlusMesh()
        {
            const float arm = 0.48f;
            const float halfW = 0.17f;
            // Two perpendicular inked bars forming a crisp komiks heal cross.
            var verts = new Vector3[]
            {
                new Vector3(-halfW, -arm, 0f), new Vector3(halfW, -arm, 0f), new Vector3(halfW, arm, 0f), new Vector3(-halfW, arm, 0f),
                new Vector3(-arm, -halfW, 0f), new Vector3(arm, -halfW, 0f), new Vector3(arm, halfW, 0f), new Vector3(-arm, halfW, 0f),
                new Vector3(0f, 0f, 0f)
            };
            var norms = new Vector3[verts.Length];
            for (int i = 0; i < norms.Length; i++) norms[i] = Vector3.back;

            var cols = new Color32[]
            {
                VertexInkEdge, VertexInkEdge, VertexInkEdge, VertexInkEdge,
                VertexInkEdge, VertexInkEdge, VertexInkEdge, VertexInkEdge,
                VertexHotCore
            };
            var uvs = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
                uvs[i] = new Vector2(verts[i].x + 0.5f, verts[i].y + 0.5f);

            var tris = new int[]
            {
                8, 1, 0,  8, 2, 1,  8, 3, 2,  8, 0, 3,
                8, 5, 4,  8, 6, 5,  8, 7, 6,  8, 4, 7
            };
            return MakeMesh("KomiksPlus", verts, norms, cols, uvs, tris);
        }

        static Mesh BuildSparkMesh() => BuildStarMesh(4, 0.22f, 0.95f);

        static Mesh MakeMesh(string name, Vector3[] verts, Vector3[] norms, Color32[] cols, Vector2[] uvs, int[] tris)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.colors32 = cols;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
