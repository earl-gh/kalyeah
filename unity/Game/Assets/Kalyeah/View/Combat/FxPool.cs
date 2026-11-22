using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// Prewarmed, zero-GC pool of sunlit 90s Filipino Komiks / Arcane combat bursts &amp; 3D floating damage numbers powered by
    /// <c>Kalyeah/ArcaneFx</c> + pooled <see cref="TextMeshPro"/> (<c>Kalyeah_Hud_HighContrast</c>).
    /// Guarantees zero mid-round Instantiate/Destroy/LINQ, a hard active-particle cap, &lt;= 3 local alpha-overdraw layers,
    /// and crisp &lt;= 0.42 s A3 warm coral-gold &amp; sunlit chalk bursts that keep 5v5 sightlines razor-clear.
    /// </summary>
    public sealed class FxPool : IDisposable
    {
        public const string ShaderName = "Kalyeah/ArcaneFx";
        public const int DefaultCapacity = 64;
        public const int DefaultDamageNumberCapacity = 20;
        public const int MaxOverdrawLayers = 3;
        public const float MaxBurstSeconds = 0.42f;
        public const float DamageNumberDuration = 0.68f;
        const int MaxPrebuiltNumber = 250;

        const byte PriorityDecorative = 0; // footstep / sprint dust puffs, ambient motes, vendor steam
        const byte PrioritySecondary = 1;  // muzzle snaps, overhead heal cross, chalk rings, water splash
        const byte PriorityCombat = 2;     // hit confirms, starbursts, shockwaves, slash arcs, polymorph, cleanse

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

        // Option A3 sunlit warm coral-gold & komiks palette (matching Manila tactical reference + scripts/view/fx_*).
        static readonly Color InkEspresso = new Color(0.20f, 0.15f, 0.14f, 0.94f);
        static readonly Color CoralGoldImpact = new Color(1.00f, 0.608f, 0.416f, 0.98f); // #FF9B6A
        static readonly Color SunlitWhiteSpike = new Color(1.00f, 0.988f, 0.955f, 0.99f);
        static readonly Color ChalkRingWarm = new Color(0.96f, 0.90f, 0.79f, 0.78f);
        static readonly Color HeadshotNumColor = new Color(1.00f, 0.635f, 0.365f, 1.00f);
        static readonly Color BodyNumColor = new Color(0.99f, 0.98f, 0.96f, 1.00f);
        static readonly Color HealNumColor = new Color(0.243f, 0.922f, 0.561f, 1.00f);
        static readonly Color EsteroSplashColor = new Color(0.32f, 0.89f, 0.88f, 0.92f);
        static readonly Color EsteroFoamColor = new Color(0.94f, 0.99f, 0.98f, 0.88f);
        static readonly Color VendorSteamColor = new Color(0.98f, 0.95f, 0.89f, 0.44f);
        static readonly Color SunlitMoteColor = new Color(1.00f, 0.94f, 0.76f, 0.42f);
        static readonly Color HitAmber = new Color(0.99f, 0.76f, 0.24f, 0.98f);
        static readonly Color MuzzleGold = new Color(1.00f, 0.92f, 0.62f, 0.96f);
        static readonly Color BagsakGold = new Color(0.98f, 0.78f, 0.24f, 0.96f);
        static readonly Color BatoTeamDefault = new Color(0.24f, 0.56f, 1.00f, 0.94f);
        static readonly Color HealEmerald = new Color(0.24f, 0.92f, 0.56f, 0.96f);
        static readonly Color HealCrossJade = new Color(0.42f, 0.98f, 0.72f, 0.98f);
        static readonly Color ChalkDust = new Color(0.92f, 0.86f, 0.74f, 0.66f);
        static readonly Color LataTinPuff = new Color(0.84f, 0.92f, 0.98f, 0.94f);
        static readonly Color CleanseCyan = new Color(0.32f, 0.88f, 0.92f, 0.98f);

        static readonly string[] PrebuiltBodyNumbers = BuildNumberStrings("", "");
        static readonly string[] PrebuiltHeadshotNumbers = BuildNumberStrings("", "!");
        static readonly string[] PrebuiltHealNumbers = BuildNumberStrings("+", "");

        internal static FxPool ActiveInstance { get; private set; }

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

        sealed class DamageNumberNode
        {
            public GameObject Go;
            public Transform Tr;
            public TextMeshPro Label;
            public Vector3 StartPos;
            public Vector3 Velocity;
            public Color BaseColor;
            public float BaseScale;
            public float Elapsed;
            public float Duration;
        }

        readonly Transform _root;
        readonly int _maxActive;
        readonly Stack<FxNode> _freePool;
        readonly FxNode[] _active;
        int _activeCount;
        readonly int _maxDamageNumbers;
        readonly Stack<DamageNumberNode> _dmgFreePool;
        readonly DamageNumberNode[] _dmgActive;
        int _activeDmgCount;
        int _spawnSerial;
        int _cosmeticSpawnSerial;
        float _moteSpawnTimer;
        SimBridge _boundSim;

        readonly Material _sharedMaterial;
        readonly bool _ownsMaterial;
        readonly Mesh _ringMesh;
        readonly Mesh _starMesh;
        readonly Mesh _starburstMesh;
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
        public int ActiveDamageNumberCount => _activeDmgCount;
        public int DamageNumberCapacity => _maxDamageNumbers;
        public float ActiveParticleBudgetScale => SettingsStore.Current.GraphicsProfile.ParticleBudgetScale;
        public int EffectiveDecorativeCap => ComputeDecorativeCap(SettingsStore.Current.GraphicsProfile);
        public int EffectiveOverdrawCap => SettingsStore.Current.GraphicsProfile.QualityIndex == SettingsStore.GraphicsLow ? 2 : MaxOverdrawLayers;
        public bool ReduceMotion { get; set; }
        public Camera ViewCamera { get; set; }

        public FxPool(Transform parent = null, int capacity = DefaultCapacity, bool reduceMotion = false)
        {
            _maxActive = Mathf.Clamp(capacity, 8, 256);
            _maxDamageNumbers = DefaultDamageNumberCapacity;
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
            _starburstMesh = BuildStarburstMesh();
            _slashArcMesh = BuildSlashArcMesh();
            _streakMesh = BuildStreakMesh();
            _puffMesh = BuildPuffMesh();
            _plusMesh = BuildPlusMesh();
            _sparkMesh = BuildSparkMesh();

            _freePool = new Stack<FxNode>(_maxActive);
            _active = new FxNode[_maxActive];
            Prewarm();

            _dmgFreePool = new Stack<DamageNumberNode>(_maxDamageNumbers);
            _dmgActive = new DamageNumberNode[_maxDamageNumbers];
            PrewarmDamageNumbers();

            if (Application.isPlaying)
                ActiveInstance = this;
        }

        /// <summary>Subscribes to SimBridge combat events to spawn A3 coral-gold starbursts and 3D floating damage/heal numbers automatically.</summary>
        public void Bind(SimBridge sim)
        {
            if (ReferenceEquals(_boundSim, sim)) return;
            Unbind();
            _boundSim = sim;
            if (_boundSim == null) return;
            _boundSim.HitConfirmed += OnSimHitConfirmed;
            _boundSim.PlayerHealed += OnSimPlayerHealed;
        }

        public void Unbind()
        {
            if (_boundSim == null) return;
            _boundSim.HitConfirmed -= OnSimHitConfirmed;
            _boundSim.PlayerHealed -= OnSimPlayerHealed;
            _boundSim = null;
        }

        void OnSimHitConfirmed(HitConfirmInfo info)
        {
            Vector3 hitPos = KalyeahResources.World(info.WorldPos, info.IsHeadshot ? 1.42f : 0.96f);
            SpawnHitStarburst(hitPos, info.IsHeadshot, info.IsTag);
            if (info.Damage > 0)
                SpawnDamageNumber(hitPos + Vector3.up * 0.28f, info.Damage, info.IsHeadshot, heal: false);
        }

        void OnSimPlayerHealed(int playerId, int amount)
        {
            if (_boundSim == null || amount <= 0) return;
            KidSnapshot? who = _boundSim.Find(playerId);
            if (!who.HasValue) return;
            Vector3 pos = KalyeahResources.World(who.Value.Position, 1.48f);
            SpawnDamageNumber(pos, amount, headshot: false, heal: true);
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

        void PrewarmDamageNumbers()
        {
            TMP_FontAsset displayFont = KalyeahResources.TmpDisplayFont();
            Material highContrastMat = displayFont != null
                ? KalyeahResources.TmpHighContrastMaterial(displayFont, isDisplay: true)
                : null;

            for (int i = 0; i < _maxDamageNumbers; i++)
            {
                var go = new GameObject($"DmgNumSlot_{i:D2}");
                go.SetActive(false);
                Transform tr = go.transform;
                tr.SetParent(_root, false);

                TextMeshPro label = go.AddComponent<TextMeshPro>();
                if (displayFont != null)
                {
                    label.font = displayFont;
                    if (highContrastMat != null)
                        label.fontSharedMaterial = highContrastMat;
                }
                label.fontSize = 4.6f;
                label.enableAutoSizing = false;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
                label.color = BodyNumColor;
                label.text = "";
                if (label.rectTransform != null)
                    label.rectTransform.sizeDelta = new Vector2(4.2f, 1.4f);

                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    mr.lightProbeUsage = LightProbeUsage.Off;
                    mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                }

                _dmgFreePool.Push(new DamageNumberNode
                {
                    Go = go,
                    Tr = tr,
                    Label = label,
                });
            }
        }

        /// <summary>
        /// Option A3 warm coral-gold (#FF9B6A) &amp; sunlit white chalk starburst impact burst (&lt;= 0.42 s).
        /// Matches the sunlit Manila street reference with razor-clear 5v5 sightlines and zero GC.
        /// </summary>
        public void SpawnHitStarburst(Vector3 worldPos, bool headshot, bool tag)
        {
            float size = tag ? 1.85f : (headshot ? 1.52f : 1.18f);
            float dur = tag ? 0.40f : (headshot ? 0.36f : 0.28f);
            float roll = NextAngleDeg();
            Vector3 riseVel = ReduceMotion ? Vector3.zero : new Vector3(0f, tag ? 0.85f : 0.55f, 0f);

            // Outer warm coral-gold (#FF9B6A) radial starburst
            SpawnNode(
                mesh: _starburstMesh,
                pos: worldPos,
                rot: Quaternion.identity,
                velocity: riseVel,
                startScale: new Vector3(size * 0.42f, size * 0.42f, 1f),
                endScale: new Vector3(size, size, 1f),
                planarRadius: size * 0.52f,
                duration: dur,
                tint: CoralGoldImpact,
                sunlitLift: headshot || tag ? 0.24f : 0.18f,
                scrollSpeed: 1.4f,
                fresnel: 0f,
                billboard: true,
                baseRollDeg: roll,
                spinSpeedDeg: ReduceMotion ? 0f : (headshot || tag ? 72f : 48f),
                priority: PriorityCombat);

            // Inner crisp sunlit white spike flash (slightly offset rotation & tighter lifetime for punchy readability)
            float coreSize = size * (headshot || tag ? 0.82f : 0.72f);
            SpawnNode(
                mesh: _starburstMesh,
                pos: worldPos,
                rot: Quaternion.identity,
                velocity: riseVel * 0.6f,
                startScale: new Vector3(coreSize * 0.48f, coreSize * 0.48f, 1f),
                endScale: new Vector3(coreSize, coreSize, 1f),
                planarRadius: coreSize * 0.40f,
                duration: dur * 0.68f,
                tint: SunlitWhiteSpike,
                sunlitLift: 0.30f,
                scrollSpeed: 1.8f,
                fresnel: 0f,
                billboard: true,
                baseRollDeg: roll + 22f,
                spinSpeedDeg: ReduceMotion ? 0f : -55f,
                priority: PriorityCombat);

            // Warm chalk impact dust ring (secondary priority; skipped under ReduceMotion)
            if (!ReduceMotion)
            {
                float ringR = tag ? 1.45f : (headshot ? 1.18f : 0.86f);
                SpawnNode(
                    mesh: _ringMesh,
                    pos: new Vector3(worldPos.x, Mathf.Max(0.06f, worldPos.y - 0.18f), worldPos.z),
                    rot: Quaternion.identity,
                    velocity: new Vector3(0f, 0.30f, 0f),
                    startScale: new Vector3(ringR * 0.35f, 1f, ringR * 0.35f),
                    endScale: new Vector3(ringR, 1f, ringR),
                    planarRadius: ringR * 0.65f,
                    duration: tag ? 0.42f : 0.34f,
                    tint: ChalkRingWarm,
                    sunlitLift: 0.14f,
                    scrollSpeed: 0.9f,
                    fresnel: 0f,
                    billboard: false,
                    baseRollDeg: 0f,
                    spinSpeedDeg: 0f,
                    priority: PrioritySecondary,
                    hardEdgeWidth: 0.24f,
                    dashCount: 0f,
                    edgeBoost: 0.20f,
                    interiorAlpha: 0.12f);
            }
        }

        public void SpawnHitStarburst(SVector2 groundPos, bool headshot, bool tag)
            => SpawnHitStarburst(KalyeahResources.World(groundPos, headshot ? 1.42f : 0.96f), headshot, tag);

        /// <summary>
        /// Spawns a pooled, zero-GC 3D billboarded floating damage, headshot, or heal number with pop scale curve
        /// and gentle upward float using <c>Kalyeah_Hud_HighContrast</c> SDF underlay.
        /// </summary>
        public void SpawnDamageNumber(Vector3 worldPos, int amount, bool headshot = false, bool heal = false)
        {
            if (_disposed || amount <= 0) return;

            DamageNumberNode node;
            if (_dmgFreePool.Count > 0)
            {
                node = _dmgFreePool.Pop();
            }
            else if (_activeDmgCount > 0)
            {
                // Recycle the oldest active number cleanly when saturated (zero heap allocation).
                int oldest = 0;
                float oldestElapsed = _dmgActive[0].Elapsed;
                for (int i = 1; i < _activeDmgCount; i++)
                {
                    if (_dmgActive[i].Elapsed > oldestElapsed)
                    {
                        oldestElapsed = _dmgActive[i].Elapsed;
                        oldest = i;
                    }
                }
                RecycleDamageNumberAt(oldest);
                node = _dmgFreePool.Pop();
            }
            else
            {
                return;
            }

            _spawnSerial = (_spawnSerial + 1) & 0x7FFF;
            float scatterX = (((_spawnSerial % 7) - 3) * 0.12f);
            float scatterZ = ((((_spawnSerial / 7) % 5) - 2) * 0.08f);

            node.StartPos = worldPos + new Vector3(scatterX, 0.12f, scatterZ);
            node.Velocity = ReduceMotion
                ? new Vector3(0f, 0.28f, 0f)
                : new Vector3(scatterX * 0.45f, headshot ? 1.35f : 1.10f, scatterZ * 0.45f);
            node.BaseColor = heal ? HealNumColor : (headshot ? HeadshotNumColor : BodyNumColor);
            node.BaseScale = headshot ? 1.38f : (heal ? 1.08f : 1.00f);
            node.Elapsed = 0f;
            node.Duration = headshot ? 0.78f : DamageNumberDuration;

            if (node.Label != null)
            {
                node.Label.text = FormatNumberZeroGc(amount, headshot, heal);
                node.Label.color = node.BaseColor;
            }

            _dmgActive[_activeDmgCount++] = node;
            node.Go.SetActive(true);
            ApplyDamageNumberState(node, ResolveBillboardRotation());
        }

        public void SpawnDamageNumber(SVector2 groundPos, int amount, bool headshot = false, bool heal = false)
            => SpawnDamageNumber(KalyeahResources.World(groundPos, headshot ? 1.62f : 1.32f), amount, headshot, heal);

        /// <summary>Waist-height komiks starburst impact confirm (&lt;= 0.42 s; delegates default hits to Option A3 coral-gold starburst).</summary>
        public void SpawnHitConfirm(Vector3 worldPos, Color? tint = null, bool heavy = false)
        {
            if (!tint.HasValue)
            {
                SpawnHitStarburst(worldPos, headshot: heavy, tag: false);
                return;
            }

            Color col = tint.Value;
            float size = heavy ? 1.56f : 1.18f;
            float dur = heavy ? 0.36f : 0.26f;
            float roll = NextAngleDeg();
            Vector3 rise = ReduceMotion ? Vector3.zero : new Vector3(0f, heavy ? 0.90f : 0.55f, 0f);
            SpawnNode(
                mesh: _starburstMesh,
                pos: worldPos,
                rot: Quaternion.identity,
                velocity: rise,
                startScale: new Vector3(size * 0.45f, size * 0.45f, 1f),
                endScale: new Vector3(size, size, 1f),
                planarRadius: size * 0.55f,
                duration: dur,
                tint: col,
                sunlitLift: 0.20f,
                scrollSpeed: 1.3f,
                fresnel: 0f,
                billboard: true,
                baseRollDeg: roll,
                spinSpeedDeg: ReduceMotion ? 0f : 80f,
                priority: PriorityCombat);

            if (heavy)
            {
                float core = size * 0.76f;
                SpawnNode(
                    mesh: _starMesh,
                    pos: worldPos,
                    rot: Quaternion.identity,
                    velocity: rise * 0.6f,
                    startScale: new Vector3(core * 0.5f, core * 0.5f, 1f),
                    endScale: new Vector3(core, core, 1f),
                    planarRadius: core * 0.42f,
                    duration: dur * 0.65f,
                    tint: SunlitWhiteSpike,
                    sunlitLift: 0.28f,
                    scrollSpeed: 1.6f,
                    fresnel: 0f,
                    billboard: true,
                    baseRollDeg: roll + 25f,
                    spinSpeedDeg: ReduceMotion ? 0f : -65f,
                    priority: PriorityCombat);
            }
        }

        public void SpawnHitConfirm(SVector2 groundPos, Color? tint = null, bool heavy = false)
            => SpawnHitConfirm(KalyeahResources.World(groundPos, 0.90f), tint, heavy);

        /// <summary>Snappy directional slingshot / cast muzzle streak (&lt;= 0.18 s).</summary>
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
                endScale: new Vector3(0.72f, 1f, 1.38f),
                planarRadius: 0.65f,
                duration: 0.16f,
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

        /// <summary>Hollow radial street shockwave ring for <c>Bagsak ng Kalye</c> and <c>Bato (Heavy)</c> (&lt;= 0.42 s).</summary>
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
                duration: isBagsak ? MaxBurstSeconds : 0.30f,
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

        /// <summary>Expanding friendly emerald heal ring + rising komiks heal cross (&lt;= 0.40 s).</summary>
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
                duration: 0.36f,
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
            Vector3 crossVel = ReduceMotion ? Vector3.zero : new Vector3(0f, 1.45f, 0f);
            SpawnNode(
                mesh: _plusMesh,
                pos: crossPos,
                rot: Quaternion.identity,
                velocity: crossVel,
                startScale: new Vector3(0.38f, 0.38f, 1f),
                endScale: new Vector3(0.76f, 0.76f, 1f),
                planarRadius: 0.45f,
                duration: 0.38f,
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

        /// <summary>Curved komiks scissor-snip crescent sweep for <c>Gunting</c> light &amp; heavy (&lt;= 0.32 s).</summary>
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
                duration: heavy ? 0.30f : 0.22f,
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

        /// <summary>Warm provincial street dust puff (&lt;= 0.34 s; skipped under ReduceMotion or local overdraw saturation).</summary>
        public void SpawnDustPuff(Vector3 feetPos, float scale = 0.9f)
        {
            if (ReduceMotion) return;
            float s = Mathf.Max(0.25f, scale);
            Vector3 at = new Vector3(feetPos.x, feetPos.y + 0.26f, feetPos.z);
            SpawnNode(
                mesh: _puffMesh,
                pos: at,
                rot: Quaternion.identity,
                velocity: new Vector3(0f, 0.72f, 0f),
                startScale: new Vector3(s * 0.48f, s * 0.42f, 1f),
                endScale: new Vector3(s * 1.15f, s * 0.98f, 1f),
                planarRadius: s * 0.65f,
                duration: 0.32f,
                tint: ChalkDust,
                sunlitLift: 0.08f,
                scrollSpeed: 0.7f,
                fresnel: 0f,
                billboard: true,
                baseRollDeg: NextAngleDeg(),
                spinSpeedDeg: 32f,
                priority: PriorityDecorative);
        }

        public void SpawnDustPuff(SVector2 groundPos, float scale = 0.9f)
            => SpawnDustPuff(KalyeahResources.World(groundPos, 0f), scale);

        /// <summary>Turquoise Estero canal water splash ring + sunlit spray droplets (&lt;= 0.34 s; ReduceMotion-safe).</summary>
        public void SpawnWaterSplash(Vector3 worldPos, float scale = 0.95f)
        {
            if (ReduceMotion) return;
            float s = Mathf.Max(0.30f, scale);
            Vector3 waterSurface = new Vector3(worldPos.x, worldPos.y + 0.05f, worldPos.z);
            SpawnNode(
                mesh: _ringMesh,
                pos: waterSurface,
                rot: Quaternion.identity,
                velocity: Vector3.zero,
                startScale: new Vector3(s * 0.35f, 1f, s * 0.35f),
                endScale: new Vector3(s * 1.15f, 1f, s * 1.15f),
                planarRadius: s * 0.85f,
                duration: 0.34f,
                tint: EsteroSplashColor,
                sunlitLift: 0.22f,
                scrollSpeed: 1.4f,
                fresnel: 0f,
                billboard: false,
                baseRollDeg: 0f,
                spinSpeedDeg: 0f,
                priority: PrioritySecondary,
                hardEdgeWidth: 0.26f,
                dashCount: 0f,
                edgeBoost: 0.24f,
                interiorAlpha: 0.14f);

            SpawnNode(
                mesh: _sparkMesh,
                pos: new Vector3(worldPos.x, worldPos.y + 0.24f, worldPos.z),
                rot: Quaternion.identity,
                velocity: new Vector3(0f, 1.25f, 0f),
                startScale: new Vector3(s * 0.32f, s * 0.40f, 1f),
                endScale: new Vector3(s * 0.78f, s * 0.92f, 1f),
                planarRadius: s * 0.45f,
                duration: 0.26f,
                tint: EsteroFoamColor,
                sunlitLift: 0.26f,
                scrollSpeed: 1.6f,
                fresnel: 0f,
                billboard: true,
                baseRollDeg: NextAngleDeg(),
                spinSpeedDeg: 45f,
                priority: PriorityDecorative);
        }

        public void SpawnWaterSplash(SVector2 groundPos, float scale = 0.95f)
            => SpawnWaterSplash(KalyeahResources.World(groundPos, 0f), scale);

        /// <summary>Warm rising steam puff for Mang Taho &amp; Fishball Cart street vendors (decorative budget, ReduceMotion-safe).</summary>
        public void SpawnVendorSteam(Vector3 worldPos, float scale = 0.75f)
        {
            if (ReduceMotion) return;
            float s = Mathf.Max(0.25f, scale);
            SpawnNode(
                mesh: _puffMesh,
                pos: new Vector3(worldPos.x, worldPos.y + 0.95f, worldPos.z),
                rot: Quaternion.identity,
                velocity: new Vector3(0.18f, 0.72f, 0.10f),
                startScale: new Vector3(s * 0.38f, s * 0.38f, 1f),
                endScale: new Vector3(s * 1.05f, s * 1.15f, 1f),
                planarRadius: s * 0.55f,
                duration: 0.40f,
                tint: VendorSteamColor,
                sunlitLift: 0.14f,
                scrollSpeed: 0.5f,
                fresnel: 0f,
                billboard: true,
                baseRollDeg: NextAngleDeg(),
                spinSpeedDeg: 18f,
                priority: PriorityDecorative);
        }

        public void SpawnVendorSteam(SVector2 groundPos, float scale = 0.75f)
            => SpawnVendorSteam(KalyeahResources.World(groundPos, 0f), scale);

        /// <summary>Single sunlit golden street mote drifting along the afternoon light (decorative budget, ReduceMotion-safe).</summary>
        public void SpawnAmbientMote(Vector3 worldPos, float scale = 0.30f)
        {
            if (ReduceMotion) return;
            float s = Mathf.Clamp(scale, 0.14f, 0.65f);
            SpawnNode(
                mesh: _puffMesh,
                pos: worldPos,
                rot: Quaternion.identity,
                velocity: new Vector3(0.45f, 0.22f, 0.30f),
                startScale: new Vector3(s * 0.65f, s * 0.65f, 1f),
                endScale: new Vector3(s, s, 1f),
                planarRadius: 0.22f,
                duration: MaxBurstSeconds,
                tint: SunlitMoteColor,
                sunlitLift: 0.24f,
                scrollSpeed: 0.3f,
                fresnel: 0f,
                billboard: true,
                baseRollDeg: NextAngleDeg(),
                spinSpeedDeg: 12f,
                priority: PriorityDecorative);
        }

        /// <summary>
        /// Emits budgeted sunlit golden dust motes near <paramref name="focusWorldPos"/> for warm inhabited barangay air.
        /// Automatically skipped under ReduceMotion or Low graphics tier to preserve 60 FPS Android headroom.
        /// </summary>
        public void TickStreetAmbience(Vector3 focusWorldPos, float dt)
        {
            if (_disposed || ReduceMotion || dt <= 0f) return;
            GraphicsTierProfile profile = SettingsStore.Current.GraphicsProfile;
            if (profile.QualityIndex == SettingsStore.GraphicsLow || profile.ParticleBudgetScale <= 0.55f)
                return;

            _moteSpawnTimer -= dt;
            if (_moteSpawnTimer > 0f) return;
            _moteSpawnTimer = 0.26f;

            _spawnSerial = (_spawnSerial + 1) & 0x7FFF;
            float ox = (((_spawnSerial * 37) % 100) / 100f - 0.5f) * 14f;
            float oy = 1.2f + (((_spawnSerial * 53) % 100) / 100f) * 3.2f;
            float oz = (((_spawnSerial * 71) % 100) / 100f - 0.5f) * 14f;
            SpawnAmbientMote(focusWorldPos + new Vector3(ox, oy, oz), 0.28f);
        }

        /// <summary>Comic tin-can transformation puff for <c>Lata</c> polymorph (&lt;= 0.38 s).</summary>
        public void SpawnPolymorphPuff(Vector3 worldPos, Color? teamColor = null)
        {
            Color col = teamColor.HasValue ? Color.Lerp(LataTinPuff, teamColor.Value, 0.35f) : LataTinPuff;
            col.a = 0.96f;
            SpawnNode(
                mesh: _puffMesh,
                pos: worldPos,
                rot: Quaternion.identity,
                velocity: ReduceMotion ? Vector3.zero : new Vector3(0f, 1.05f, 0f),
                startScale: new Vector3(0.62f, 0.62f, 1f),
                endScale: new Vector3(1.52f, 1.46f, 1f),
                planarRadius: 0.95f,
                duration: 0.36f,
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

        /// <summary>Sunlit cyan spark burst for <c>Luksong-Tinik</c> vault + <c>SLOW</c> cleanse (&lt;= 0.30 s).</summary>
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
                endScale: new Vector3(1.36f, 1.56f, 1f),
                planarRadius: 0.80f,
                duration: 0.28f,
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

        /// <summary>Advances all live bursts &amp; 3D floating damage numbers in O(active) with swap-remove recycling and zero heap allocations.</summary>
        public void Tick(float dt)
        {
            if (_disposed) return;
            float step = dt > 0f ? dt : 0f;

            if (Application.isPlaying && step > 0f && !ReduceMotion)
            {
                Camera cam = ViewCamera != null ? ViewCamera : (_cachedCamera != null ? _cachedCamera : Camera.main);
                if (cam != null)
                {
                    _cachedCamera = cam;
                    TickStreetAmbience(cam.transform.position + cam.transform.forward * 7f, step);
                }
            }

            if (_activeCount == 0 && _activeDmgCount == 0) return;

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

            for (int i = _activeDmgCount - 1; i >= 0; i--)
            {
                DamageNumberNode dmg = _dmgActive[i];
                dmg.Elapsed += step;
                if (dmg.Elapsed >= dmg.Duration)
                {
                    RecycleDamageNumberAt(i);
                    continue;
                }

                ApplyDamageNumberState(dmg, billboardRot);
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

        /// <summary>Immediately deactivates all live bursts and 3D floating numbers and returns them to the prewarmed stacks.</summary>
        public void ClearAll()
        {
            for (int i = _activeCount - 1; i >= 0; i--)
                RecycleAt(i);
            for (int i = _activeDmgCount - 1; i >= 0; i--)
                RecycleDamageNumberAt(i);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Unbind();
            if (ReferenceEquals(ActiveInstance, this))
                ActiveInstance = null;
            ClearAll();
            if (_root != null) KalyeahResources.Kill(_root.gameObject);
            KalyeahResources.Kill(_ringMesh);
            KalyeahResources.Kill(_starMesh);
            KalyeahResources.Kill(_starburstMesh);
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

        void RecycleDamageNumberAt(int index)
        {
            DamageNumberNode finished = _dmgActive[index];
            int last = _activeDmgCount - 1;
            if (index != last)
                _dmgActive[index] = _dmgActive[last];
            _dmgActive[last] = null;
            _activeDmgCount = last;

            finished.Go.SetActive(false);
            _dmgFreePool.Push(finished);
        }

        void ApplyDamageNumberState(DamageNumberNode node, Quaternion billboardRot)
        {
            float phase = Mathf.Clamp01(node.Elapsed / Mathf.Max(0.05f, node.Duration));
            float oneMinus = 1f - phase;
            float riseEase = 1f - oneMinus * oneMinus;

            node.Tr.localPosition = node.StartPos + node.Velocity * (node.Duration * riseEase * 0.72f);
            node.Tr.localRotation = billboardRot;

            // Crisp OutBack pop-in (0 -> 1.28 -> 1.0 over first 22%), hold steady, gentle ease-out scale at end.
            float scaleMult;
            if (ReduceMotion)
            {
                scaleMult = 1f;
            }
            else if (phase < 0.22f)
            {
                float p = phase / 0.22f;
                float u = p - 1f;
                const float c1 = 1.70158f;
                const float c3 = c1 + 1f;
                scaleMult = Mathf.Max(0.2f, 1f + c3 * u * u * u + c1 * u * u);
            }
            else if (phase > 0.78f)
            {
                float fadeOut = (phase - 0.78f) / 0.22f;
                scaleMult = Mathf.Lerp(1f, 0.84f, fadeOut);
            }
            else
            {
                scaleMult = 1f;
            }

            float finalScale = node.BaseScale * scaleMult;
            node.Tr.localScale = new Vector3(finalScale, finalScale, finalScale);

            if (node.Label != null)
            {
                float alpha = phase < 0.65f ? 1f : Mathf.Clamp01((1f - phase) / 0.35f);
                Color c = node.BaseColor;
                c.a = alpha;
                node.Label.color = c;
            }
        }

        static string[] BuildNumberStrings(string prefix, string suffix)
        {
            var table = new string[MaxPrebuiltNumber + 1];
            for (int i = 0; i <= MaxPrebuiltNumber; i++)
                table[i] = prefix + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + suffix;
            return table;
        }

        static string FormatNumberZeroGc(int amount, bool headshot, bool heal)
        {
            int clamped = Mathf.Clamp(amount, 0, MaxPrebuiltNumber);
            if (heal) return PrebuiltHealNumbers[clamped];
            if (headshot) return PrebuiltHeadshotNumbers[clamped];
            return PrebuiltBodyNumbers[clamped];
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

        /// <summary>
        /// 9-spike asymmetric sunlit komiks/Valorant starburst mesh with alternating long/medium razor tips
        /// matching the Manila street impact reference.
        /// </summary>
        static Mesh BuildStarburstMesh()
        {
            const int spikes = 9;
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
                bool isTip = (i % 2) == 0;
                float rOuter = isTip
                    ? ((i % 4 == 0) ? 1.06f : 0.78f)
                    : 0.26f;
                float rCore = rOuter * 0.84f;
                float c = Mathf.Cos(a);
                float s = Mathf.Sin(a);

                int idxCore = 1 + i * 2;
                int idxEdge = idxCore + 1;

                verts[idxCore] = new Vector3(c * rCore, s * rCore, 0f);
                verts[idxEdge] = new Vector3(c * rOuter, s * rOuter, 0f);
                norms[idxCore] = Vector3.back;
                norms[idxEdge] = Vector3.back;
                cols[idxCore] = VertexHotCore;
                cols[idxEdge] = VertexBody;
                uvs[idxCore] = new Vector2(0.5f + c * 0.42f, 0.5f + s * 0.42f);
                uvs[idxEdge] = new Vector2(0.5f + c * 0.50f, 0.5f + s * 0.50f);
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

            return MakeMesh("KomiksStarburst", verts, norms, cols, uvs, tris);
        }

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
