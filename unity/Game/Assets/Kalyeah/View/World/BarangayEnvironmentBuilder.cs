using System;
using System.Collections.Generic;
using Kalyeah.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using SVector2 = System.Numerics.Vector2;
using SVector3 = System.Numerics.Vector3;

namespace Kalyeah.Game
{
    /// <summary>
    /// Handles and references produced by <see cref="BarangayEnvironmentBuilder.Build"/> so MatchView and ShoulderCamera
    /// can access the ground/water/sky materials, Uptown/Riverside landmarks, living-world neutrals/vendors, foot buff auras,
    /// and occludable buildings, and deterministically dispose all procedural meshes and material instances on match teardown.
    /// </summary>
    public sealed class BarangayEnvironment : IDisposable
    {
        static readonly int PropBaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int PropColorId = Shader.PropertyToID("_Color");
        static readonly Color CrocIdleColor = new Color(0.32f, 0.56f, 0.34f, 1f);
        static readonly Color CrocAngryColor = new Color(0.68f, 0.36f, 0.22f, 1f);
        static readonly Color EagleIdleColor = new Color(0.46f, 0.34f, 0.24f, 1f);
        static readonly Color EagleAngryColor = new Color(0.76f, 0.44f, 0.20f, 1f);
        static readonly Color DragonAuraColor = new Color(0.32f, 0.86f, 0.48f, 0.52f);
        static readonly Color BaronAuraColor = new Color(0.98f, 0.78f, 0.24f, 0.58f);
        static readonly Color SelectaAuraColor = new Color(0.30f, 0.88f, 0.94f, 0.52f);

        readonly List<GameObject> _bridges = new List<GameObject>();
        readonly List<GameObject> _occluders = new List<GameObject>();
        readonly List<Object> _ownedAssets = new List<Object>(48);
        bool _disposed;

        public GameObject Root { get; internal set; }
        public GameObject Ground { get; internal set; }
        public GameObject CreekBed { get; internal set; }
        public GameObject EsteroWater { get; internal set; }
        public GameObject Embankments { get; internal set; }
        public GameObject SouthCoveredCourt { get; internal set; }
        public GameObject NorthBarangayHall { get; internal set; }

        // Asymmetric Uptown vs Riverside landmarks & Living World handles (docs/MAPS.md, docs/GRAPHICS.md)
        public GameObject WaterTower { get; internal set; }
        public GameObject ChapelBellTower { get; internal set; }
        public GameObject HallFlagpole { get; internal set; }
        public GameObject SariSariRow { get; internal set; }
        public GameObject LivingWorldRoot { get; internal set; }
        public GameObject CrocodileVisual { get; internal set; }
        public GameObject EagleVisual { get; internal set; }
        public GameObject SelectaBikeVisual { get; internal set; }
        public GameObject MangTahoVisual { get; internal set; }
        public GameObject FishballCartVisual { get; internal set; }

        public Material GroundMaterial { get; internal set; }
        public Material CreekBedMaterial { get; internal set; }
        public Material WaterMaterial { get; internal set; }
        public Material SkyMaterial { get; internal set; }

        internal GameObject TahoStockProp;
        internal GameObject FishballStockProp;
        internal Transform HallBannerTransform;
        internal Transform SelectaBellTransform;
        internal Material CrocBodyMat;
        internal Material EagleBodyMat;
        internal Transform[] BuffAuraTransforms;
        internal MeshRenderer[] BuffAuraRenderers;
        internal MaterialPropertyBlock BuffAuraMpb;
        internal float EaglePerchHeight = 11.35f;

        public IReadOnlyList<GameObject> Bridges => _bridges;
        public IReadOnlyList<GameObject> Occluders => _occluders;

        internal void AddBridge(GameObject bridge)
        {
            if (bridge != null) _bridges.Add(bridge);
        }

        internal void AddOccluder(GameObject model)
        {
            if (model != null) _occluders.Add(model);
        }

        internal T Track<T>(T asset) where T : Object
        {
            if (asset != null && !_ownedAssets.Contains(asset))
            {
                _ownedAssets.Add(asset);
            }
            return asset;
        }

        /// <summary>
        /// Updates living-world props with 0 B GC: Crocodile &amp; Philippine Eagle cute-to-angry-to-flee scale/tint transitions,
        /// moving Selecta ice-cream bike along the river road, vendor restock empty states, and gentle Band-3 flagpole sway.
        /// </summary>
        public void SyncLivingWorld(BarangayNeutrals neutrals, float clockSeconds, bool reduceMotion = false)
        {
            if (_disposed) return;

            // Band-3 civic hall flag gently sways above head height (never in Band-2 combat lane)
            if (HallBannerTransform != null)
            {
                float sway = reduceMotion ? 0f : Mathf.Sin(clockSeconds * 2.6f) * 8f;
                HallBannerTransform.localEulerAngles = new Vector3(0f, sway, 0f);
            }

            if (neutrals == null) return;

            // Vendors: hide stock props while empty/restocking (30 s timer)
            if (TahoStockProp != null && neutrals.Vendors.Count > 0)
            {
                bool ready = neutrals.Vendors[0].IsReady;
                if (TahoStockProp.activeSelf != ready) TahoStockProp.SetActive(ready);
            }
            if (FishballStockProp != null && neutrals.Vendors.Count > 1)
            {
                bool ready = neutrals.Vendors[1].IsReady;
                if (FishballStockProp.activeSelf != ready) FishballStockProp.SetActive(ready);
            }

            // Selecta Ice-Cream Bike patrol along river road
            if (SelectaBikeVisual != null)
            {
                bool active = neutrals.Selecta.Active;
                if (SelectaBikeVisual.activeSelf != active) SelectaBikeVisual.SetActive(active);
                if (active)
                {
                    SVector2 pos = new SVector2(neutrals.Selecta.Position.X, neutrals.Selecta.Position.Y);
                    float bob = reduceMotion ? 0f : Mathf.Abs(Mathf.Sin(clockSeconds * 8f)) * 0.04f;
                    SelectaBikeVisual.transform.position = KalyeahResources.World(pos, bob);
                    float yawRad = MathF.Atan2(neutrals.Selecta.Facing.X, neutrals.Selecta.Facing.Y);
                    SelectaBikeVisual.transform.rotation = Quaternion.Euler(0f, GodotSpace.ModelYawDegrees(yawRad), 0f);
                    if (SelectaBellTransform != null)
                    {
                        float bellTilt = reduceMotion ? 0f : Mathf.Sin((float)(neutrals.Selecta.BellPhase * Math.PI * 2.0)) * 18f;
                        SelectaBellTransform.localEulerAngles = new Vector3(0f, 0f, bellTilt);
                    }
                }
            }

            // Crocodile (Dragon) on river sandbar under main bridge
            SyncBossVisual(
                CrocodileVisual,
                CrocBodyMat,
                neutrals.Dragon,
                baseY: -0.04f,
                idleColor: CrocIdleColor,
                angryColor: CrocAngryColor,
                clockSeconds: clockSeconds,
                reduceMotion: reduceMotion,
                hoverAmplitude: 0.03f);

            // Philippine Eagle (Baron) perched on creek-bank water tower
            SyncBossVisual(
                EagleVisual,
                EagleBodyMat,
                neutrals.Baron,
                baseY: EaglePerchHeight,
                idleColor: EagleIdleColor,
                angryColor: EagleAngryColor,
                clockSeconds: clockSeconds,
                reduceMotion: reduceMotion,
                hoverAmplitude: 0.14f);
        }

        /// <summary>
        /// Displays a soft wide foot aura ring around buffed kids (Dragon, Baron, or Selecta) so enemies can read who is buffed
        /// at a glance (docs/GRAPHICS.md L148 &amp; L176), strictly 0 B per-frame GC.
        /// </summary>
        public void SyncBuffAuras(IReadOnlyList<KidSnapshot> kids, BarangayNeutrals neutrals, int localTeam, int colorblindMode)
        {
            if (_disposed || BuffAuraTransforms == null || BuffAuraRenderers == null) return;
            int cap = BuffAuraTransforms.Length;
            int cursor = 0;

            if (kids != null && neutrals != null)
            {
                int n = kids.Count;
                for (int i = 0; i < n && cursor < cap; i++)
                {
                    KidSnapshot kid = kids[i];
                    if (kid.Tagged || !neutrals.HasFootAura(kid.Team)) continue;

                    TeamBuffState buffs = neutrals.GetTeamBuffs(kid.Team);
                    Color aura = buffs.HasBaron
                        ? BaronAuraColor
                        : (buffs.HasDragon ? DragonAuraColor : SelectaAuraColor);
                    // Subtle team tint bias in colorblind modes while staying clearly distinct from team rings
                    if (colorblindMode != 0 && kid.Team != localTeam)
                        aura = Color.Lerp(aura, KomiksUi.TeamEnemyColor(colorblindMode), 0.25f);

                    Transform auraT = BuffAuraTransforms[cursor];
                    MeshRenderer auraMr = BuffAuraRenderers[cursor];
                    if (!auraT.gameObject.activeSelf) auraT.gameObject.SetActive(true);
                    auraT.position = KalyeahResources.World(kid.Position, 0.03f);
                    if (BuffAuraMpb != null)
                    {
                        BuffAuraMpb.SetColor(PropBaseColorId, aura);
                        BuffAuraMpb.SetColor(PropColorId, aura);
                        auraMr.SetPropertyBlock(BuffAuraMpb);
                    }
                    cursor++;
                }
            }

            for (int i = cursor; i < cap; i++)
            {
                if (BuffAuraTransforms[i] != null && BuffAuraTransforms[i].gameObject.activeSelf)
                    BuffAuraTransforms[i].gameObject.SetActive(false);
            }
        }

        static void SyncBossVisual(
            GameObject visual,
            Material bodyMat,
            NeutralBossInstance boss,
            float baseY,
            Color idleColor,
            Color angryColor,
            float clockSeconds,
            bool reduceMotion,
            float hoverAmplitude)
        {
            if (visual == null || boss == null) return;
            bool visible = boss.State != NeutralBossState.Dormant;
            if (visual.activeSelf != visible) visual.SetActive(visible);
            if (!visible) return;

            float bob = reduceMotion
                ? 0f
                : Mathf.Sin(clockSeconds * (boss.State == NeutralBossState.Angry ? 6.5f : 2.2f)) * hoverAmplitude;
            SVector2 pos = new SVector2(boss.Position.X, boss.Position.Y);
            visual.transform.position = KalyeahResources.World(pos, baseY + bob);
            float yawRad = MathF.Atan2(boss.Facing.X, boss.Facing.Y);
            visual.transform.rotation = Quaternion.Euler(0f, GodotSpace.ModelYawDegrees(yawRad), 0f);
            float s = Mathf.Clamp((float)boss.VisualScale, 0.35f, 1.8f);
            visual.transform.localScale = new Vector3(s, s, s);

            if (bodyMat != null)
            {
                Color tint = boss.State == NeutralBossState.Angry ? angryColor : idleColor;
                if (bodyMat.HasProperty(PropBaseColorId)) bodyMat.SetColor(PropBaseColorId, tint);
                if (bodyMat.HasProperty(PropColorId)) bodyMat.SetColor(PropColorId, tint);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _bridges.Clear();
            _occluders.Clear();

            if (Ground != null)
            {
                KalyeahResources.Kill(Ground);
                Ground = null;
            }
            if (Root != null)
            {
                KalyeahResources.Kill(Root);
                Root = null;
            }

            for (int i = _ownedAssets.Count - 1; i >= 0; i--)
            {
                KalyeahResources.Kill(_ownedAssets[i]);
            }
            _ownedAssets.Clear();

            CreekBed = null;
            EsteroWater = null;
            Embankments = null;
            SouthCoveredCourt = null;
            NorthBarangayHall = null;
            WaterTower = null;
            ChapelBellTower = null;
            HallFlagpole = null;
            SariSariRow = null;
            LivingWorldRoot = null;
            CrocodileVisual = null;
            EagleVisual = null;
            SelectaBikeVisual = null;
            MangTahoVisual = null;
            FishballCartVisual = null;
            TahoStockProp = null;
            FishballStockProp = null;
            HallBannerTransform = null;
            SelectaBellTransform = null;
            CrocBodyMat = null;
            EagleBodyMat = null;
            BuffAuraTransforms = null;
            BuffAuraRenderers = null;
            BuffAuraMpb = null;
            GroundMaterial = null;
            CreekBedMaterial = null;
            WaterMaterial = null;
            SkyMaterial = null;
        }
    }

    /// <summary>
    /// Builds Kalyeah's stylized 3D Filipino barangay arena environment:
    /// - 3-band value-disciplined BarangayGround &amp; TropicalSky setup helpers
    /// - Sunken -0.30 m Estero creek depression, animated EsteroWater, mossy volcanic-tuff riprap embankments, river steps &amp; boulders
    /// - 3 flush walkable Estero avenue footbridges (West, Center, East) + 2 scenic outer canal-mouth footbridges
    /// - South Covered Court &amp; North Barangay Hall Plaza landmarks + NW/SE waiting sheds, kapilyas, hoops &amp; barrio orientation anchors
    /// </summary>
    public static class BarangayEnvironmentBuilder
    {
        public const string GroundMaterialName = "BarangayGround";
        public const string GroundShaderName = "Kalyeah/BarangayGround";
        public const string WaterMaterialName = "EsteroWater";
        public const string WaterShaderName = "Kalyeah/EsteroWater";
        public const string SkyMaterialName = "TropicalSky";
        public const string SkyShaderName = "Kalyeah/TropicalSky";

        // VALORANT clamped ambient readability bounds (soft bounce fill, zero pitch-black shadow pits).
        public const float AmbientLuminanceMin = 0.24f;
        public const float AmbientLuminanceMax = 0.92f;

        const float OuterGroundMargin = 1200f;
        const float Deg2Rad = MathF.PI / 180f;

        static readonly int PropArenaRadius = Shader.PropertyToID("_ArenaRadius");
        static readonly int PropArenaHalf = Shader.PropertyToID("_ArenaHalf");
        static readonly int PropOwnBaseCenter = Shader.PropertyToID("_OwnBaseCenter");
        static readonly int PropEnemyBaseCenter = Shader.PropertyToID("_EnemyBaseCenter");
        static readonly int PropWear = Shader.PropertyToID("_Wear");
        static readonly int PropShadowFloor = Shader.PropertyToID("_ShadowFloor");
        static readonly int PropAmbientMin = Shader.PropertyToID("_AmbientMin");
        static readonly int PropAmbientMax = Shader.PropertyToID("_AmbientMax");
        static readonly int PropDetailStrength = Shader.PropertyToID("_DetailStrength");
        static readonly int PropSpecStrength = Shader.PropertyToID("_SpecStrength");
        static readonly int PropCarveCreekSlot = Shader.PropertyToID("_CarveCreekSlot");
        static readonly int PropIsCreekBed = Shader.PropertyToID("_IsCreekBed");
        static readonly int PropParcelCount = Shader.PropertyToID("_ParcelCount");
        static readonly int PropParcelPose = Shader.PropertyToID("_ParcelPose");
        static readonly int PropParcelHalf = Shader.PropertyToID("_ParcelHalf");
        static readonly int PropCreekHalfWidth = Shader.PropertyToID("_CreekHalfWidth");
        static readonly int PropCurveScale = Shader.PropertyToID("_CurveScale");
        static readonly int PropFlowSpeed = Shader.PropertyToID("_FlowSpeed");
        static readonly int PropSunHotspotPower = Shader.PropertyToID("_SunHotspotPower");
        static readonly int PropSunHotspotStrength = Shader.PropertyToID("_SunHotspotStrength");
        static readonly int PropFresnelStrength = Shader.PropertyToID("_FresnelStrength");
        static readonly int PropSunDiscSize = Shader.PropertyToID("_SunDiscSize");
        static readonly int PropHorizonClarityBand = Shader.PropertyToID("_HorizonClarityBand");

        // Warm afternoon haze & muted trilight bounce (keeps team red/blue out of the environment).
        static readonly Color WarmHorizonFog = new Color(0.89f, 0.84f, 0.75f, 1f);
        static readonly Color WarmSkyAmbient = new Color(0.72f, 0.79f, 0.88f, 1f);
        static readonly Color WarmEquatorAmbient = new Color(0.78f, 0.73f, 0.65f, 1f);
        static readonly Color WarmGroundBounce = new Color(0.54f, 0.48f, 0.40f, 1f);
        static Material s_fallbackSky;

        /// <summary>
        /// Builds the Estero mid-canal depression + animated Estero water + 3 walkable lane footbridges (+ 2 outer mouth bridges),
        /// South Covered Court landmark, North Barangay Hall Plaza landmark, and district orientation anchors under <paramref name="parent"/>.
        /// </summary>
        public static BarangayEnvironment Build(Transform parent, MapLayoutData layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            var env = new BarangayEnvironment();
            var rootGo = new GameObject("BarangayEnvironment");
            if (parent != null) rootGo.transform.SetParent(parent, false);
            env.Root = rootGo;

            var naturalSizes = new Dictionary<string, Vector3>();
            Material litTemplate = ResolveLitTemplate(env);

            if (layout.ArenaRadius > 0f)
            {
                BuildEsteroAndBridges(rootGo.transform, layout, env, naturalSizes, litTemplate);
            }

            BuildLandmarks(rootGo.transform, layout, env, naturalSizes, litTemplate);
            if (layout.ArenaRadius > 0f)
            {
                BuildAsymmetricLandmarksAndLivingWorld(rootGo.transform, layout, env, litTemplate);
            }
            return env;
        }

        /// <summary>
        /// Builds the non-repeating BarangayGround plane (carving the Estero canal slot when <c>layout.ArenaRadius &gt; 0</c>
        /// and uploading all 32 oriented building parcel contact-AO footprints). Replaces MatchView's flat primitive Ground + Street cylinder.
        /// </summary>
        public static GameObject BuildGround(Transform parent, MapLayoutData layout, BarangayEnvironment env = null)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            Material groundMat = CreateShaderMaterial(env, GroundMaterialName, GroundShaderName, "BarangayGround_Instance")
                ?? MakeLitMaterial(env, ResolveLitTemplate(env), "BarangayGround_Fallback", new Color(0.55f, 0.50f, 0.43f), 0.14f, 0f);

            bool carveCreek = layout.ArenaRadius > 0f;
            ConfigureGroundMaterial(groundMat, layout, carveCreekSlot: carveCreek, isCreekBed: false);

            float span = (layout.ArenaRadius > 0f ? layout.ArenaRadius * 2f : 48f) + OuterGroundMargin;
            Mesh groundMesh = CreateGridMesh("BarangayGroundMesh", span, span, 20, 20);
            if (env != null) env.Track(groundMesh);
            GameObject go = CreateMeshObject("BarangayGround", parent, groundMesh, groundMat, Vector3.zero, receiveShadows: true);

            if (env != null)
            {
                env.Ground = go;
                env.GroundMaterial = groundMat;
            }
            return go;
        }

        /// <summary>
        /// Applies the sunlit Philippine golden-hour <c>Kalyeah/TropicalSky</c> skybox, serene 0-25 deg horizon clarity,
        /// warm provincial fog, and Valorant-clamped soft bounce ambient (zero pitch-black shadow pits).
        /// </summary>
        public static Material ApplySky()
        {
            Material sky = Resources.Load<Material>("KalyeahMaterials/" + SkyMaterialName);
            if (sky == null)
            {
                if (s_fallbackSky == null)
                {
                    Shader found = Shader.Find(SkyShaderName);
                    if (found != null) s_fallbackSky = new Material(found) { name = "TropicalSky_Runtime" };
                }
                sky = s_fallbackSky;
            }
            if (sky != null)
            {
                if (sky.HasProperty(PropSunDiscSize)) sky.SetFloat(PropSunDiscSize, 0.9978f);
                if (sky.HasProperty(PropHorizonClarityBand)) sky.SetFloat(PropHorizonClarityBand, 0.24f);
                RenderSettings.skybox = sky;
            }
            RenderSettings.fogColor = WarmHorizonFog;
            EnsureClampedAmbient();
            return sky;
        }

        /// <summary>
        /// Configures muted warm Valorant-style bounce lighting (soft sky + warm ground bounce, never pitch-black
        /// shadow pits and free of saturated team red/blue).
        /// </summary>
        public static void ApplyClampedAmbient(bool trilight = true)
        {
            if (trilight)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = ClampAmbientColor(WarmSkyAmbient);
                RenderSettings.ambientEquatorColor = ClampAmbientColor(WarmEquatorAmbient);
                RenderSettings.ambientGroundColor = ClampAmbientColor(WarmGroundBounce);
            }
            else
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = ClampAmbientColor(WarmEquatorAmbient);
            }
        }

        /// <summary>Clamps an environment ambient color into [<see cref="AmbientLuminanceMin"/>, <see cref="AmbientLuminanceMax"/>].</summary>
        public static Color ClampAmbientColor(Color c)
        {
            return new Color(
                Mathf.Clamp(c.r, AmbientLuminanceMin, AmbientLuminanceMax),
                Mathf.Clamp(c.g, AmbientLuminanceMin, AmbientLuminanceMax),
                Mathf.Clamp(c.b, AmbientLuminanceMin, AmbientLuminanceMax),
                1f);
        }

        static void EnsureClampedAmbient()
        {
            if (RenderSettings.ambientMode == AmbientMode.Skybox)
            {
                ApplyClampedAmbient(trilight: true);
                return;
            }
            if (RenderSettings.ambientMode == AmbientMode.Trilight)
            {
                RenderSettings.ambientSkyColor = ClampAmbientColor(RenderSettings.ambientSkyColor);
                RenderSettings.ambientEquatorColor = ClampAmbientColor(RenderSettings.ambientEquatorColor);
                RenderSettings.ambientGroundColor = ClampAmbientColor(RenderSettings.ambientGroundColor);
                return;
            }
            RenderSettings.ambientLight = ClampAmbientColor(RenderSettings.ambientLight);
        }

        /// <summary>
        /// Uploads arena bounds, base plaza centers, creek flags, Valorant ambient/detail clarity uniforms,
        /// and 32 oriented building parcel OBB uniforms onto a BarangayGround material.
        /// </summary>
        public static void ConfigureGroundMaterial(Material mat, MapLayoutData layout, bool carveCreekSlot, bool isCreekBed)
        {
            if (mat == null || layout == null) return;

            float halfExtent = layout.ArenaRadius > 0f ? layout.ArenaRadius + 3f : 24f;
            SVector2 ownBase = layout.BaseCenter(MapLayoutData.SideOwn);
            SVector2 eneBase = layout.BaseCenter(MapLayoutData.SideEnemy);

            mat.SetFloat(PropArenaRadius, layout.ArenaRadius);
            mat.SetVector(PropArenaHalf, new Vector4(halfExtent, halfExtent, 0f, 0f));
            mat.SetVector(PropOwnBaseCenter, new Vector4(ownBase.X, ownBase.Y, 0f, 0f));
            mat.SetVector(PropEnemyBaseCenter, new Vector4(eneBase.X, eneBase.Y, 0f, 0f));
            mat.SetFloat(PropWear, 1f);
            if (mat.HasProperty(PropShadowFloor)) mat.SetFloat(PropShadowFloor, 0.34f);
            if (mat.HasProperty(PropAmbientMin)) mat.SetFloat(PropAmbientMin, AmbientLuminanceMin);
            if (mat.HasProperty(PropAmbientMax)) mat.SetFloat(PropAmbientMax, AmbientLuminanceMax);
            if (mat.HasProperty(PropDetailStrength)) mat.SetFloat(PropDetailStrength, 0.72f);
            if (mat.HasProperty(PropSpecStrength)) mat.SetFloat(PropSpecStrength, 0.12f);
            mat.SetFloat(PropCarveCreekSlot, carveCreekSlot ? 1f : 0f);
            mat.SetFloat(PropIsCreekBed, isCreekBed ? 1f : 0f);

            int count = Math.Min(layout.ParcelCount, 32);
            var poses = new Vector4[32];
            var halves = new Vector4[32];
            var sizeCache = new Dictionary<string, Vector3>();

            for (int i = 0; i < count; i++)
            {
                GroundRect rect = layout.ParcelRect(i);
                SVector2 center = rect.Center;
                float yaw = layout.HasOrientedParcels ? layout.ParcelYawRad(i) : 0f;
                float u = Math.Clamp(layout.ParcelScale(i), 0.90f, 1.08f);

                string prop = layout.ParcelProp(i);
                string kitId = BarangayPlan.ObstacleKits.TryGetValue(prop, out string mapped) ? mapped : "brgy_cover_wall";
                Vector2 half = new Vector2(rect.W * 0.5f, rect.H * 0.5f);

                GameObject prefab = KalyeahResources.Model("barangay", kitId);
                if (prefab != null)
                {
                    Vector3 nat = MeasurePrefabSize(kitId, prefab, sizeCache);
                    if (nat.x > 0.2f && nat.z > 0.2f)
                    {
                        half = new Vector2(nat.x * u * 0.5f, nat.z * u * 0.5f);
                    }
                }

                poses[i] = new Vector4(center.X, center.Y, MathF.Cos(yaw), MathF.Sin(yaw));
                halves[i] = new Vector4(half.x, half.y, 0f, 0f);
            }

            mat.SetFloat(PropParcelCount, count);
            mat.SetVectorArray(PropParcelPose, poses);
            mat.SetVectorArray(PropParcelHalf, halves);
        }

        // ---- Estero Canal, Riprap Banks & 3 Footbridges -------------------------------------------------------------------------

        static void BuildEsteroAndBridges(
            Transform parent,
            MapLayoutData layout,
            BarangayEnvironment env,
            Dictionary<string, Vector3> naturalSizes,
            Material litTemplate)
        {
            var esteroRoot = new GameObject("EsteroCanal").transform;
            esteroRoot.SetParent(parent, false);

            float canalLen = layout.ArenaRadius * 2f + 8f;

            // 1. Sunken -0.30 m Tabing-Ilog channel bed stitched into BarangayGround (-6 mm vertical seat prevents rim Z-fighting)
            Material bedMat = CreateShaderMaterial(env, GroundMaterialName, GroundShaderName, "EsteroCreekBed_Instance")
                ?? MakeLitMaterial(env, litTemplate, "EsteroCreekBed_Fallback", new Color(0.28f, 0.26f, 0.21f), 0.25f, 0f);
            ConfigureGroundMaterial(bedMat, layout, carveCreekSlot: false, isCreekBed: true);

            Mesh bedMesh = env.Track(CreateGridMesh("TabingIlogChannelBedMesh", canalLen, 9.4f, 176, 32));
            env.CreekBed = CreateMeshObject("TabingIlogChannelBed", esteroRoot, bedMesh, bedMat, new Vector3(0f, -0.006f, 0f), receiveShadows: true);
            env.CreekBedMaterial = bedMat;

            // 2. Animated sunlit olive-jade / aqua-tea Estero water with edge foam (Y = -0.12 m, strictly below Y = 0)
            Material waterMat = CreateShaderMaterial(env, WaterMaterialName, WaterShaderName, "EsteroWater_Instance")
                ?? MakeLitMaterial(env, litTemplate, "EsteroWater_Fallback", new Color(0.18f, 0.31f, 0.24f, 0.80f), 0.72f, 0f);
            if (waterMat != null)
            {
                if (waterMat.HasProperty(PropCreekHalfWidth)) waterMat.SetFloat(PropCreekHalfWidth, 3.75f);
                if (waterMat.HasProperty(PropCurveScale)) waterMat.SetFloat(PropCurveScale, 1f);
                if (waterMat.HasProperty(PropFlowSpeed)) waterMat.SetFloat(PropFlowSpeed, 0.50f);
                if (waterMat.HasProperty(PropSunHotspotPower)) waterMat.SetFloat(PropSunHotspotPower, 38f);
                if (waterMat.HasProperty(PropSunHotspotStrength)) waterMat.SetFloat(PropSunHotspotStrength, 0.42f);
                if (waterMat.HasProperty(PropFresnelStrength)) waterMat.SetFloat(PropFresnelStrength, 0.24f);
                if (waterMat.HasProperty(PropAmbientMin)) waterMat.SetFloat(PropAmbientMin, 0.30f);
                if (waterMat.HasProperty(PropAmbientMax)) waterMat.SetFloat(PropAmbientMax, 0.96f);
            }

            Mesh waterMesh = env.Track(CreateGridMesh("EsteroWaterMesh", canalLen, 7.5f, 148, 12));
            env.EsteroWater = CreateMeshObject("EsteroWater", esteroRoot, waterMesh, waterMat, new Vector3(0f, -0.12f, 0f), receiveShadows: false);
            env.WaterMaterial = waterMat;

            // 3. Sculpted mossy volcanic-tuff riprap embankments, descending river steps, laundry stones & shoulder boulders
            BuildEmbankmentsAndSteps(esteroRoot, env, litTemplate);

            // 4. Three flush walkable Estero footbridges (West, Center, East avenues) + two scenic outer river-mouth bridges
            BuildThreeAvenueFootbridges(esteroRoot, env, litTemplate);
            BuildOuterMouthBridges(esteroRoot, env, naturalSizes);
        }

        static void BuildEmbankmentsAndSteps(
            Transform parent,
            BarangayEnvironment env,
            Material litTemplate)
        {
            var banksGo = new GameObject("TabingIlogBanks");
            banksGo.transform.SetParent(parent, false);
            env.Embankments = banksGo;

            var tuffBuilder = new MeshBatchBuilder();
            var mossBuilder = new MeshBatchBuilder();
            var cobbleBuilder = new MeshBatchBuilder();

            GameObject ilogStepsPrefab = KalyeahResources.Model("barangay", "brgy_ilog_steps");
            GameObject laundryPrefab = KalyeahResources.Model("barangay", "brgy_laundry_stones");
            bool hasStepsGlb = ilogStepsPrefab != null;
            bool hasLaundryGlb = laundryPrefab != null;

            Vector2[] bankSpans =
            {
                new Vector2(-82.5f, -43.5f),
                new Vector2(-29.2f, -8.0f),
                new Vector2(8.0f, 29.2f),
                new Vector2(43.5f, 82.5f),
            };

            float[] bankSigns = { -1f, 1f };

            foreach (float bankSign in bankSigns)
            {
                float stepLandingX = -19.2f * bankSign;
                foreach (Vector2 span in bankSpans)
                {
                    if (hasStepsGlb && stepLandingX > span.x + 2.4f && stepLandingX < span.y - 2.4f)
                    {
                        EmitBankSubSpan(tuffBuilder, mossBuilder, cobbleBuilder, span.x, stepLandingX - 1.95f, bankSign);
                        EmitBankSubSpan(tuffBuilder, mossBuilder, cobbleBuilder, stepLandingX + 1.95f, span.y, bankSign);
                    }
                    else
                    {
                        EmitBankSubSpan(tuffBuilder, mossBuilder, cobbleBuilder, span.x, span.y, bankSign);
                    }
                }
            }

            // Descending Tabing-Ilog stone steps & laundry stones on South (+1) and North (-1) banks
            foreach (float stepSide in bankSigns)
            {
                float sx = -19.2f * stepSide;
                float szC = MapLayoutData.CanalZ(sx);
                float slopeDz = MapLayoutData.CanalZ(sx + 1f) - MapLayoutData.CanalZ(sx - 1f);
                float bankYaw = MathF.Atan2(-slopeDz, 2f);

                if (hasStepsGlb)
                {
                    float stepsYaw = bankYaw + (stepSide > 0f ? MathF.PI : 0f);
                    PlaceKit(
                        banksGo.transform,
                        ilogStepsPrefab,
                        stepSide > 0f ? "TabingIlogStepsSouth" : "TabingIlogStepsNorth",
                        new SVector2(sx, szC + stepSide * 3.65f),
                        -0.28f,
                        stepsYaw,
                        new Vector3(0.95f, 0.95f, 0.95f));
                }
                else
                {
                    Vector3 stepTangent = new Vector3(2f, 0f, -slopeDz).normalized;
                    Vector3 outward = Vector3.Cross(Vector3.up, stepTangent).normalized * stepSide;
                    Quaternion stepRot = Quaternion.LookRotation(outward, Vector3.up);
                    for (int k = 0; k < 4; k++)
                    {
                        float t = k / 3f;
                        float stepY = Mathf.Lerp(0.06f, -0.14f, t);
                        float stepZOff = stepSide * Mathf.Lerp(4.42f, 3.18f, t);
                        tuffBuilder.AddChamferedBox(
                            KalyeahResources.World(new SVector2(sx, szC + stepZOff), stepY),
                            stepRot,
                            new Vector3(2.85f, 0.14f, 0.52f),
                            0.035f);
                    }
                }

                if (hasLaundryGlb)
                {
                    float lx = sx + 3.15f * stepSide;
                    float lz = MapLayoutData.CanalZ(lx) + stepSide * 3.02f;
                    PlaceKit(
                        banksGo.transform,
                        laundryPrefab,
                        stepSide > 0f ? "TabingIlogLaundryStonesSouth" : "TabingIlogLaundryStonesNorth",
                        new SVector2(lx, lz),
                        -0.16f,
                        bankYaw + (stepSide > 0f ? MathF.PI : 0f),
                        Vector3.one * 0.96f);
                }
                else
                {
                    for (int j = 0; j < 3; j++)
                    {
                        float lx = sx + (j - 1f) * 1.05f;
                        float lz = MapLayoutData.CanalZ(lx) + stepSide * (2.82f + 0.16f * (j % 2));
                        cobbleBuilder.AddChamferedBox(
                            KalyeahResources.World(new SVector2(lx, lz), -0.11f),
                            Quaternion.Euler(0f, j * 27f, 0f),
                            new Vector3(0.78f, 0.14f, 0.62f),
                            0.045f);
                    }
                }
            }

            // Basalt river boulders flanking the 3 avenue bridge shoulders (clear of bridge decks!)
            float[] crossingXs = { LaneWestX(0f), 0f, LaneEastX(0f) };
            for (int f = 0; f < crossingXs.Length; f++)
            {
                float fx = crossingXs[f];
                for (int b = 0; b < 8; b++)
                {
                    float seed = b * 2.39996f + f * 1.71f;
                    float sideSign = (b % 2 == 0) ? -1f : 1f;
                    float bxOff = sideSign * (6.1f + 1.8f * MathF.Abs(MathF.Sin(seed * 1.37f)));
                    float bzOff = 2.85f * MathF.Sin(seed * 1.73f + 0.4f);
                    float worldBx = fx + bxOff;
                    float worldBz = MapLayoutData.CanalZ(worldBx) + bzOff;
                    float bw = 0.62f + 0.42f * MathF.Abs(MathF.Cos(seed * 1.29f + 0.3f));
                    float bd = 0.50f + 0.38f * MathF.Abs(MathF.Sin(seed * 1.61f + 0.8f));
                    float bh = 0.11f + 0.035f * MathF.Abs(MathF.Sin(seed * 2.4f));
                    cobbleBuilder.AddChamferedBox(
                        KalyeahResources.World(new SVector2(worldBx, worldBz), -0.135f),
                        Quaternion.Euler(0f, seed * 58f, 0f),
                        new Vector3(bw, bh, bd),
                        0.04f);
                }
            }

            Material tuffMat = MakeLitMaterial(env, litTemplate, "EsteroTuffStone", new Color(0.35f, 0.33f, 0.29f), 0.12f, 0f);
            Material mossMat = MakeLitMaterial(env, litTemplate, "EsteroMossCoping", new Color(0.30f, 0.34f, 0.25f), 0.16f, 0f);
            Material cobbleMat = MakeLitMaterial(env, litTemplate, "EsteroWetCobble", new Color(0.26f, 0.27f, 0.25f), 0.46f, 0f);

            if (!tuffBuilder.IsEmpty)
                CreateMeshObject("RiprapWalls", banksGo.transform, env.Track(tuffBuilder.ToMesh("RiprapWallsMesh")), tuffMat, Vector3.zero, receiveShadows: true);
            if (!mossBuilder.IsEmpty)
                CreateMeshObject("BankCoping", banksGo.transform, env.Track(mossBuilder.ToMesh("BankCopingMesh")), mossMat, Vector3.zero, receiveShadows: true);
            if (!cobbleBuilder.IsEmpty)
                CreateMeshObject("RiverBoulders", banksGo.transform, env.Track(cobbleBuilder.ToMesh("RiverBouldersMesh")), cobbleMat, Vector3.zero, receiveShadows: true);
        }

        static void EmitBankSubSpan(
            MeshBatchBuilder tuffBuilder,
            MeshBatchBuilder mossBuilder,
            MeshBatchBuilder cobbleBuilder,
            float xStart,
            float xEnd,
            float bankSign)
        {
            float totalW = xEnd - xStart;
            if (totalW <= 0.6f) return;

            int ribbonSegs = Math.Max(6, Mathf.CeilToInt(totalW / 2.2f));
            tuffBuilder.AddCanalBankRibbon(xStart, xEnd, bankSign, ribbonSegs, isCoping: false);
            mossBuilder.AddCanalBankRibbon(xStart, xEnd, bankSign, ribbonSegs, isCoping: true);

            int boulderSegs = Math.Max(2, Mathf.CeilToInt(totalW / 3.8f));
            float segLen = totalW / boulderSegs;
            for (int s = 0; s < boulderSegs; s++)
            {
                float x0 = xStart + s * segLen;
                float x1 = x0 + segLen;
                float xm = (x0 + x1) * 0.5f;
                float toeHash = MathF.Sin(s * 2.73f + xm * 0.19f + bankSign * 1.7f);
                if (toeHash <= -0.25f) continue;

                Vector3 p0 = KalyeahResources.World(new SVector2(x0, MapLayoutData.CanalZ(x0)));
                Vector3 p1 = KalyeahResources.World(new SVector2(x1, MapLayoutData.CanalZ(x1)));
                Vector3 along = (p1 - p0).normalized;

                float zm = MapLayoutData.CanalZ(xm);
                float rad = 0.34f + 0.18f * MathF.Abs(MathF.Cos(s * 3.1f + bankSign));
                Vector3 toePos = KalyeahResources.World(
                    new SVector2(xm + 0.42f * toeHash, zm + bankSign * (3.24f + 0.16f * toeHash)),
                    -0.14f);
                Quaternion toeRot = Quaternion.LookRotation(along, Vector3.up) * Quaternion.Euler(0f, toeHash * 36f, 0f);
                cobbleBuilder.AddChamferedBox(toePos, toeRot, new Vector3(rad * 1.55f, 0.13f, rad * 2.05f), 0.045f);
            }
        }

        /// <summary>
        /// Builds the 3 flush walkable concrete &amp; welded GI-pipe Estero footbridges across the West, Center, and East avenues.
        /// Deck surface sits at Y = +0.012 m so kids at Y = 0 walk cleanly across with zero shoe clipping, while concrete piers
        /// plant into the -0.30 m canal bed through the -0.12 m Estero water and chunky 5.5 cm railings flank outside the lanes.
        /// </summary>
        static void BuildThreeAvenueFootbridges(Transform parent, BarangayEnvironment env, Material litTemplate)
        {
            Material deckMat = MakeLitMaterial(env, litTemplate, "BridgeConcreteDeck", new Color(0.36f, 0.34f, 0.30f), 0.14f, 0f);
            Material curbMat = MakeLitMaterial(env, litTemplate, "BridgeHazardCurb", new Color(0.48f, 0.39f, 0.23f), 0.18f, 0f);
            Material railMat = MakeLitMaterial(env, litTemplate, "BridgeSteelPipe", new Color(0.36f, 0.40f, 0.39f), 0.48f, 0.30f);

            string[] names = { "EsteroFootbridge_West", "EsteroFootbridge_Center", "EsteroFootbridge_East" };
            float[] clearWidths = { 9.2f, 10.4f, 9.2f };

            for (int laneIdx = 0; laneIdx < 3; laneIdx++)
            {
                float xGuess = LaneX(laneIdx, 0f);
                float xc = LaneX(laneIdx, MapLayoutData.CanalZ(xGuess));
                float zc = MapLayoutData.CanalZ(xc);

                Vector3 southPt = KalyeahResources.World(new SVector2(LaneX(laneIdx, zc + 4.5f), zc + 4.5f));
                Vector3 northPt = KalyeahResources.World(new SVector2(LaneX(laneIdx, zc - 4.5f), zc - 4.5f));
                Vector3 bridgeFwd = (northPt - southPt).normalized;
                Quaternion bridgeRot = Quaternion.LookRotation(bridgeFwd, Vector3.up);
                Vector3 centerWorld = KalyeahResources.World(new SVector2(xc, zc), 0f);

                var bridgeGo = new GameObject(names[laneIdx]);
                bridgeGo.transform.SetParent(parent, false);
                bridgeGo.transform.position = centerWorld;
                bridgeGo.transform.rotation = bridgeRot;

                var concreteBatch = new MeshBatchBuilder();
                var curbBatch = new MeshBatchBuilder();
                var railBatch = new MeshBatchBuilder();

                float clearW = clearWidths[laneIdx];
                float totalW = clearW + 0.84f;
                const float spanLen = 9.8f;

                // Main weathered concrete deck (top Y = +0.012 m, soffit Y = -0.108 m -> clear above -0.12 m water)
                concreteBatch.AddChamferedBox(new Vector3(0f, -0.048f, 0f), Quaternion.identity, new Vector3(totalW, 0.12f, spanLen), 0.03f);

                // Flush bank abutment transition aprons at North (+Z local) and South (-Z local) ends
                for (int endSign = -1; endSign <= 1; endSign += 2)
                {
                    concreteBatch.AddChamferedBox(
                        new Vector3(0f, -0.024f, endSign * 5.05f),
                        Quaternion.identity,
                        new Vector3(totalW, 0.06f, 0.65f),
                        0.02f);

                    // Bank abutment headwalls seated down to -0.34 m in the canal bed
                    concreteBatch.AddChamferedBox(
                        new Vector3(0f, -0.20f, endSign * 4.55f),
                        Quaternion.identity,
                        new Vector3(totalW + 0.24f, 0.28f, 0.56f),
                        0.035f);
                }

                // Mid-stream concrete bridge piers standing visibly in the Estero water
                float[] pierXs = { -totalW * 0.42f, 0f, totalW * 0.42f };
                float[] pierZs = { -1.85f, 1.85f };
                foreach (float px in pierXs)
                {
                    foreach (float pz in pierZs)
                    {
                        concreteBatch.AddChamferedBox(
                            new Vector3(px, -0.22f, pz),
                            Quaternion.identity,
                            new Vector3(0.58f, 0.26f, 0.68f),
                            0.04f);
                    }
                }

                // Chunky beveled side curbs + Band-2 low barrio steel-pipe handrails (outside navigation lane!)
                float railOffsetX = clearW * 0.5f + 0.22f;
                for (int sideSign = -1; sideSign <= 1; sideSign += 2)
                {
                    float rx = sideSign * railOffsetX;

                    //Segmented ochre/concrete side curb blocks (3-5 cm bevels)
                    for (int seg = 0; seg < 4; seg++)
                    {
                        float segZ = -3.6f + seg * 2.4f;
                        curbBatch.AddChamferedBox(
                            new Vector3(rx, 0.095f, segZ),
                            Quaternion.identity,
                            new Vector3(0.38f, 0.19f, 2.26f),
                            0.04f);
                    }

                    // 5 chunky 6 cm welded steel stanchions + top (0.86 m) and mid (0.52 m) rails
                    const int postCount = 5;
                    for (int p = 0; p < postCount; p++)
                    {
                        float pz = Mathf.Lerp(-4.35f, 4.35f, p / (float)(postCount - 1));
                        railBatch.AddChamferedBox(
                            new Vector3(rx, 0.52f, pz),
                            Quaternion.identity,
                            new Vector3(0.065f, 0.72f, 0.065f),
                            0.015f);
                    }

                    railBatch.AddChamferedBox(
                        new Vector3(rx, 0.86f, 0f),
                        Quaternion.identity,
                        new Vector3(0.068f, 0.068f, 8.95f),
                        0.018f);
                    railBatch.AddChamferedBox(
                        new Vector3(rx, 0.52f, 0f),
                        Quaternion.identity,
                        new Vector3(0.054f, 0.054f, 8.85f),
                        0.014f);
                }

                CreateMeshObject("DeckAndPiers", bridgeGo.transform, env.Track(concreteBatch.ToMesh($"{names[laneIdx]}_Deck")), deckMat, Vector3.zero, receiveShadows: true, castShadows: true);
                CreateMeshObject("SideCurbs", bridgeGo.transform, env.Track(curbBatch.ToMesh($"{names[laneIdx]}_Curbs")), curbMat, Vector3.zero, receiveShadows: true, castShadows: true);
                CreateMeshObject("PipeRailings", bridgeGo.transform, env.Track(railBatch.ToMesh($"{names[laneIdx]}_Rails")), railMat, Vector3.zero, receiveShadows: true, castShadows: true);

                env.AddBridge(bridgeGo);
            }
        }

        static void BuildOuterMouthBridges(Transform parent, BarangayEnvironment env, Dictionary<string, Vector3> naturalSizes)
        {
            GameObject bridgePrefab = FirstModel("barangay", "brgy_estero_bridge", "brgy_footbridge");
            if (bridgePrefab == null) return;

            Vector3 nat = MeasurePrefabSize("brgy_footbridge", bridgePrefab, naturalSizes);
            Vector3 scale = new Vector3(
                4.2f / MathF.Max(nat.x, 0.1f),
                0.92f,
                8.6f / MathF.Max(nat.z, 0.1f));

            float[] mouthSigns = { -1f, 1f };
            foreach (float sign in mouthSigns)
            {
                float mx = sign * 81.2f;
                float mz = MapLayoutData.CanalZ(mx);
                float mSlope = MapLayoutData.CanalZ(mx + 1f) - MapLayoutData.CanalZ(mx - 1f);
                GameObject mouthBridge = PlaceKit(
                    parent,
                    bridgePrefab,
                    sign < 0f ? "RiverMouthFootbridgeWest" : "RiverMouthFootbridgeEast",
                    new SVector2(mx, mz),
                    -0.06f,
                    MathF.Atan2(-mSlope, 2f),
                    scale);
                env.AddBridge(mouthBridge);
            }
        }

        // ---- South Covered Court, North Barangay Hall & Barrio Orientation Landmarks --------------------------------------------

        static void BuildLandmarks(
            Transform parent,
            MapLayoutData layout,
            BarangayEnvironment env,
            Dictionary<string, Vector3> naturalSizes,
            Material litTemplate)
        {
            var landmarksRoot = new GameObject("BarangayLandmarks").transform;
            landmarksRoot.SetParent(parent, false);

            // 1. South (+Z Sim) Own Base Landmark: Covered Barangay Basketball Court (warm ochre/terracotta anchor)
            SVector2 ownCenter = layout.BaseCenter(MapLayoutData.SideOwn);
            float courtZ = layout.ArenaRadius > 0f ? layout.ArenaRadius + 11.5f : MathF.Abs(ownCenter.Y) + 15.5f;
            float courtYaw = MathF.PI + 3.5f * Deg2Rad;
            GameObject courtPrefab = KalyeahResources.Model("barangay", "brgy_covered_court");
            if (courtPrefab != null)
            {
                env.SouthCoveredCourt = PlaceKit(
                    landmarksRoot,
                    courtPrefab,
                    "CoveredBasketballCourt",
                    new SVector2(ownCenter.X, courtZ),
                    0f,
                    courtYaw,
                    new Vector3(1.12f, 1.04f, 1.08f));
            }
            else
            {
                env.SouthCoveredCourt = BuildProceduralCourtFallback(landmarksRoot, env, new SVector2(ownCenter.X, courtZ), courtYaw, litTemplate);
            }
            env.AddOccluder(env.SouthCoveredCourt);

            // 2. North (-Z Sim) Enemy Base Landmark: Municipal Barangay Hall Plaza (cool teal/cream civic anchor)
            SVector2 enemyCenter = layout.BaseCenter(MapLayoutData.SideEnemy);
            float hallZ = layout.ArenaRadius > 0f ? -(layout.ArenaRadius + 5.2f) : -(MathF.Abs(enemyCenter.Y) + 11.5f);
            float hallYaw = -4.5f * Deg2Rad;
            GameObject hallPrefab = FirstModel("barangay", "brgy_barangay_hall", "brgy_hall_plaza");
            if (hallPrefab != null)
            {
                env.NorthBarangayHall = PlaceKit(
                    landmarksRoot,
                    hallPrefab,
                    "BarangayHall",
                    new SVector2(enemyCenter.X, hallZ),
                    0f,
                    hallYaw,
                    Vector3.one * 1.08f);
            }
            else
            {
                env.NorthBarangayHall = BuildProceduralHallFallback(landmarksRoot, env, new SVector2(enemyCenter.X, hallZ), hallYaw, litTemplate);
            }
            env.AddOccluder(env.NorthBarangayHall);

            if (layout.ArenaRadius <= 0f) return;

            // 3. Barrio Kapilya Chapels framing the SE & NW plaza horizons (reserved slots in BarangayPlan.cs)
            GameObject kapilyaPrefab = KalyeahResources.Model("barangay", "brgy_kapilya");
            if (kapilyaPrefab != null)
            {
                float[] signs = { 1f, -1f };
                foreach (float s in signs)
                {
                    var kapXz = new SVector2(21.5f * s, 88.2f * s);
                    float kapYaw = MathF.Atan2(-kapXz.X, -kapXz.Y) + 6f * s * Deg2Rad;
                    GameObject kap = PlaceKit(
                        landmarksRoot,
                        kapilyaPrefab,
                        s > 0f ? "BarrioKapilyaSouth" : "BarrioKapilyaNorth",
                        kapXz,
                        0f,
                        kapYaw,
                        Vector3.one * 1.02f);
                    env.AddOccluder(kap);
                }
            }

            // 4. NW & SE Rescue Patio Waiting Sheds framing the 5-slot tagged-player sidewalks
            GameObject shedPrefab = KalyeahResources.Model("barangay", "brgy_waiting_shed");
            if (shedPrefab != null)
            {
                int[] sides = { MapLayoutData.SideOwn, MapLayoutData.SideEnemy };
                foreach (int side in sides)
                {
                    GroundRect rRect = layout.RescueRect(side);
                    float midY = rRect.Center.Y;
                    float curbR = layout.ArenaRadius + MathF.Max(layout.BoundaryThickness, 1f);
                    float tagMidX = -side * MathF.Sqrt(MathF.Max(curbR * curbR - midY * midY, 0f));
                    float sx = tagMidX - side * 2.15f;
                    float yaw = side == MapLayoutData.SideOwn ? MathF.PI * 0.5f : -MathF.PI * 0.5f;
                    GameObject shed = PlaceKit(
                        landmarksRoot,
                        shedPrefab,
                        side == MapLayoutData.SideOwn ? "RescueWaitingShedOwn" : "RescueWaitingShedEnemy",
                        new SVector2(sx, midY),
                        0f,
                        yaw,
                        new Vector3(1.06f, 1.02f, 1.04f));
                    env.AddOccluder(shed);
                }
            }

            // 5. Makeshift 90s wooden basketball hoops court-side at South & North plazas
            GameObject hoopPrefab = KalyeahResources.Model("barangay", "brgy_wooden_hoop");
            if (hoopPrefab != null)
            {
                PlaceKit(landmarksRoot, hoopPrefab, "WoodenBasketballPostSouth", new SVector2(10.6f, 56.4f), 0f, -MathF.PI * 0.46f, Vector3.one * 0.98f);
                PlaceKit(landmarksRoot, hoopPrefab, "WoodenBasketballPostNorth", new SVector2(-10.6f, -56.4f), 0f, MathF.PI * 0.54f, Vector3.one * 0.98f);
            }

            // 6. Plaza shade mango trees, Mamang Sorbetero carts, breeze-block walls & barrio poso wells
            GameObject mangoPrefab = KalyeahResources.Model("barangay", "brgy_mango_tree");
            GameObject cartPrefab = KalyeahResources.Model("barangay", "brgy_sorbetes_cart");
            GameObject wallPrefab = KalyeahResources.Model("barangay", "brgy_cover_wall");
            GameObject posoPrefab = KalyeahResources.Model("barangay", "brgy_poso_well");

            float[] halfSigns = { 1f, -1f };
            foreach (float s in halfSigns)
            {
                if (wallPrefab != null)
                {
                    PlaceKit(
                        landmarksRoot,
                        wallPrefab,
                        s > 0f ? "PlazaBougainvilleaWallSouth" : "PlazaBougainvilleaWallNorth",
                        new SVector2(14.2f * s, 77.4f * s),
                        0f,
                        (s > 0f ? -18f : 162f) * Deg2Rad,
                        Vector3.one * 0.98f);
                }
                if (mangoPrefab != null)
                {
                    PlaceKit(
                        landmarksRoot,
                        mangoPrefab,
                        s > 0f ? "PlazaShadeMangoSouth" : "PlazaShadeMangoNorth",
                        new SVector2(15.2f * s, 62.8f * s),
                        0f,
                        0.75f * s,
                        Vector3.one * 1.04f);
                }
                if (cartPrefab != null)
                {
                    PlaceKit(
                        landmarksRoot,
                        cartPrefab,
                        s > 0f ? "SorbetesCartSouth" : "SorbetesCartNorth",
                        new SVector2(12.8f * s, 61.6f * s),
                        0f,
                        (s > 0f ? -72f : 108f) * Deg2Rad,
                        Vector3.one * 0.96f);
                }
                if (posoPrefab != null)
                {
                    PlaceKit(
                        landmarksRoot,
                        posoPrefab,
                        s > 0f ? "BarrioPosoSouth" : "BarrioPosoNorth",
                        new SVector2(-16.2f * s, 22.4f * s),
                        0f,
                        (s > 0f ? 28f : -152f) * Deg2Rad,
                        Vector3.one * 1.02f);
                }
            }

            // 7. TODA tricycle terminal queue & curbside provincial jeepneys (parked clear of lanes)
            GameObject trikePrefab = KalyeahResources.Model("barangay", "brgy_tricycle");
            if (trikePrefab != null)
            {
                Vector4[] trikeSpecs =
                {
                    new Vector4(-42.1f, 13.4f, 72f * Deg2Rad, 0.96f),
                    new Vector4(-41.6f, 10.6f, 84f * Deg2Rad, 0.96f),
                    new Vector4(42.1f, -13.4f, -108f * Deg2Rad, 0.96f),
                    new Vector4(41.6f, -10.6f, -96f * Deg2Rad, 0.96f),
                    new Vector4(14.8f, 46.8f, -54f * Deg2Rad, 0.96f),
                    new Vector4(-14.8f, -46.8f, 126f * Deg2Rad, 0.96f),
                };
                for (int i = 0; i < trikeSpecs.Length; i++)
                {
                    Vector4 t = trikeSpecs[i];
                    PlaceKit(landmarksRoot, trikePrefab, $"TodaTricycle_{i}", new SVector2(t.x, t.y), 0f, t.z, Vector3.one * t.w);
                }
            }

            GameObject jeepBase = KalyeahResources.Model("barangay", "brgy_jeepney");
            GameObject ownerJeep = FirstModel("barangay", "brgy_owner_jeep", "brgy_jeepney");
            GameObject passJeep = FirstModel("barangay", "brgy_passenger_jeepney", "brgy_jeepney");
            if (jeepBase != null || ownerJeep != null)
            {
                (GameObject prefab, Vector4 pose)[] jeeps =
                {
                    (ownerJeep, new Vector4(42.2f, 15.2f, -114f * Deg2Rad, 0.98f)),
                    (ownerJeep, new Vector4(-42.2f, -15.2f, 66f * Deg2Rad, 0.98f)),
                    (passJeep, new Vector4(41.2f, 40.2f, -169f * Deg2Rad, 0.96f)),
                    (passJeep, new Vector4(-41.2f, -40.2f, 11f * Deg2Rad, 0.96f)),
                    (jeepBase, new Vector4(-22.8f, 72.4f, 62f * Deg2Rad, 0.96f)),
                    (jeepBase, new Vector4(22.8f, -72.4f, -118f * Deg2Rad, 0.96f)),
                };
                for (int i = 0; i < jeeps.Length; i++)
                {
                    if (jeeps[i].prefab == null) continue;
                    Vector4 js = jeeps[i].pose;
                    PlaceKit(landmarksRoot, jeeps[i].prefab, $"ProvincialJeepney_{i}", new SVector2(js.x, js.y), 0f, js.z, Vector3.one * js.w);
                }
            }

            // 8. Standalone street-corner wayfinding notice posts
            GameObject posterPrefab = KalyeahResources.Model("barangay", "brgy_poster_cluster");
            if (posterPrefab != null)
            {
                SVector2[] corners =
                {
                    new SVector2(-8.4f, 23.2f),
                    new SVector2(8.4f, -23.2f),
                    new SVector2(41.8f, 24.4f),
                    new SVector2(-41.8f, -24.4f),
                };
                for (int i = 0; i < corners.Length; i++)
                {
                    SVector2 cs = corners[i];
                    float yaw = MathF.Atan2(-cs.X, -cs.Y) + 18f * Deg2Rad;
                    PlaceKit(landmarksRoot, posterPrefab, $"CornerWayfindingSign_{i}", cs, 0f, yaw, Vector3.one * 0.95f);
                }
            }
        }

        // ---- Asymmetric Uptown vs Riverside Silhouettes & Living World Props (docs/MAPS.md, docs/GRAPHICS.md) ----------------

        static void BuildAsymmetricLandmarksAndLivingWorld(
            Transform parent,
            MapLayoutData layout,
            BarangayEnvironment env,
            Material litTemplate)
        {
            var livingGo = new GameObject("BarangayLivingWorld");
            livingGo.transform.SetParent(parent, false);
            env.LivingWorldRoot = livingGo;
            Transform root = livingGo.transform;

            // Warm calm Filipino palette keeping saturated team red/blue out of the environment (docs/GRAPHICS.md L23)
            Material cementMat = MakeLitMaterial(env, litTemplate, "UptownCement", new Color(0.68f, 0.67f, 0.63f), 0.14f, 0f);
            Material sageTrimMat = MakeLitMaterial(env, litTemplate, "UptownSageTrim", new Color(0.45f, 0.54f, 0.53f), 0.24f, 0.08f);
            Material steelMat = MakeLitMaterial(env, litTemplate, "TowerGalvSteel", new Color(0.52f, 0.54f, 0.52f), 0.42f, 0.35f);
            Material creamStuccoMat = MakeLitMaterial(env, litTemplate, "RiversideStucco", new Color(0.82f, 0.76f, 0.65f), 0.14f, 0f);
            Material terracottaMat = MakeLitMaterial(env, litTemplate, "RiversideTerracotta", new Color(0.64f, 0.43f, 0.32f), 0.22f, 0.05f);
            Material narraWoodMat = MakeLitMaterial(env, litTemplate, "RiversideNarraWood", new Color(0.47f, 0.33f, 0.22f), 0.20f, 0f);
            Material tinRoofMat = MakeLitMaterial(env, litTemplate, "RiversideTinAwning", new Color(0.56f, 0.55f, 0.51f), 0.44f, 0.30f);
            Material ledgeTrimMat = MakeLitMaterial(env, litTemplate, "ClimbLedgeTrim", new Color(0.88f, 0.86f, 0.79f), 0.18f, 0f);
            Material warmGoldMat = MakeLitMaterial(env, litTemplate, "CivicBannerGold", new Color(0.84f, 0.68f, 0.34f), 0.22f, 0.05f);
            Material sandbarMat = MakeLitMaterial(env, litTemplate, "RiverSandbar", new Color(0.62f, 0.54f, 0.40f), 0.12f, 0f);
            Material foliageMat = MakeLitMaterial(env, litTemplate, "TallCoverFoliage", new Color(0.36f, 0.48f, 0.33f), 0.14f, 0f);
            Material laundryMat = MakeLitMaterial(env, litTemplate, "TallCoverLaundry", new Color(0.82f, 0.79f, 0.72f), 0.10f, 0f);
            Material aluminumMat = MakeLitMaterial(env, litTemplate, "VendorAluminum", new Color(0.76f, 0.78f, 0.79f), 0.62f, 0.55f);
            Material selectaCoolerMat = MakeLitMaterial(env, litTemplate, "SelectaCoolerCream", new Color(0.90f, 0.87f, 0.78f), 0.36f, 0.05f);

            // 1. UPTOWN (-Z): Barangay Hall steps + tall civic flagpole (Band-3 banner > 4 m)
            env.HallFlagpole = BuildUptownHallStepsAndFlagpole(root, env, layout, cementMat, sageTrimMat, steelMat, warmGoldMat);

            // 2. UPTOWN CREEK-BANK FLANK: Water Tower landmark (Eagle's Baron perch — seen from anywhere!)
            env.WaterTower = BuildCreekWaterTower(root, env, new SVector2(54f, -3.8f), steelMat, cementMat, sageTrimMat, ledgeTrimMat);
            env.AddOccluder(env.WaterTower);

            // 3. RIVERSIDE (+Z): Chapel Bell Tower landmark (opposing silhouette seen from anywhere!)
            env.ChapelBellTower = BuildRiversideChapelBellTower(root, env, new SVector2(21.5f, 88.2f), creamStuccoMat, terracottaMat, warmGoldMat);
            env.AddOccluder(env.ChapelBellTower);

            // 4. RIVERSIDE SARI-SARI ROW + 3 Climb-Spot lighter roof/ledge accents (docs/GRAPHICS.md L206)
            env.SariSariRow = BuildSariSariAwningAndClimbAccents(root, env, narraWoodMat, tinRoofMat, creamStuccoMat, warmGoldMat, ledgeTrimMat);

            // 5. MID-MAP SANDBAR under/beside Center Bridge + Crocodile (Dragon)
            BuildRiverSandbar(root, env, new SVector2(-7f, 0f), sandbarMat);
            env.CrocodileVisual = BuildCrocodileVisual(root, env, litTemplate, new SVector2(-7f, 0f), creamStuccoMat, warmGoldMat);

            // 6. PHILIPPINE EAGLE (Baron) perched atop the Creek-Bank Water Tower
            env.EagleVisual = BuildEagleVisual(root, env, litTemplate, new SVector2(54f, -3.8f), env.EaglePerchHeight, creamStuccoMat, warmGoldMat);

            // 7. TWO EQUIDISTANT VENDORS (Mang Taho on left alley, Fishball Cart on creek bank)
            env.MangTahoVisual = BuildMangTahoVendor(root, env, new SVector2(-42.5f, 2.6f), narraWoodMat, aluminumMat, warmGoldMat, creamStuccoMat);
            env.FishballCartVisual = BuildFishballCartVendor(root, env, new SVector2(42.5f, -2.6f), narraWoodMat, steelMat, terracottaMat, warmGoldMat);

            // 8. SELECTA ICE-CREAM BIKE patrolling the river road
            env.SelectaBikeVisual = BuildSelectaBikeVisual(root, env, new SVector2(-36.5f, -6.2f), selectaCoolerMat, warmGoldMat, steelMat);

            // 9. TALL-COVER STEALTH CLUMPS (6 soft-cover zones with visible edges & Band-2 calm palette)
            BuildTallCoverClumps(root, env, narraWoodMat, laundryMat, foliageMat);

            // 10. POOLED SOFT FOOT BUFF AURAS (10 zero-GC rings shown only on Dragon/Baron/Selecta-buffed kids)
            BuildPooledBuffAuras(root, env);
        }

        static GameObject BuildUptownHallStepsAndFlagpole(
            Transform parent,
            BarangayEnvironment env,
            MapLayoutData layout,
            Material cementMat,
            Material sageTrimMat,
            Material steelMat,
            Material bannerMat)
        {
            var go = new GameObject("UptownHallStepsAndFlagpole");
            go.transform.SetParent(parent, false);
            SVector2 hallBase = layout.BaseCenter(MapLayoutData.SideEnemy);

            // 3 wide civic concrete steps facing south toward the plaza (+Z sim -> -Z Unity)
            var steps = new MeshBatchBuilder();
            for (int k = 0; k < 3; k++)
            {
                float depth = 2.8f - k * 0.65f;
                float y = 0.08f + k * 0.14f;
                float zOff = -14.5f - k * 0.55f;
                steps.AddChamferedBox(
                    KalyeahResources.World(new SVector2(hallBase.X, hallBase.Y + zOff), y),
                    Quaternion.identity,
                    new Vector3(11.6f - k * 0.6f, 0.16f, depth),
                    0.03f);
            }
            CreateMeshObject("HallCivicSteps", go.transform, env.Track(steps.ToMesh("HallCivicStepsMesh")), cementMat, Vector3.zero, receiveShadows: true, castShadows: true);

            // Tall 10.8 m flagpole on the Uptown Hall plaza margin (Band-3 banner well above head height)
            var polePivot = new GameObject("HallFlagpolePivot");
            polePivot.transform.SetParent(go.transform, false);
            polePivot.transform.position = KalyeahResources.World(new SVector2(8.6f, hallBase.Y - 12.2f), 0f);

            var poleBatch = new MeshBatchBuilder();
            poleBatch.AddChamferedBox(new Vector3(0f, 0.25f, 0f), Quaternion.identity, new Vector3(0.95f, 0.50f, 0.95f), 0.05f);
            poleBatch.AddChamferedBox(new Vector3(0f, 5.45f, 0f), Quaternion.identity, new Vector3(0.14f, 10.6f, 0.14f), 0.025f);
            CreateMeshObject("FlagpoleShaft", polePivot.transform, env.Track(poleBatch.ToMesh("HallFlagpoleShaftMesh")), steelMat, Vector3.zero, receiveShadows: true, castShadows: true);

            var plinthBatch = new MeshBatchBuilder();
            plinthBatch.AddChamferedBox(new Vector3(0f, 0.52f, 0f), Quaternion.identity, new Vector3(0.68f, 0.12f, 0.68f), 0.03f);
            CreateMeshObject("FlagpoleTrim", polePivot.transform, env.Track(plinthBatch.ToMesh("HallFlagpoleTrimMesh")), sageTrimMat, Vector3.zero, receiveShadows: true);

            var bannerPivot = new GameObject("HallBannerPivot");
            bannerPivot.transform.SetParent(polePivot.transform, false);
            bannerPivot.transform.localPosition = new Vector3(0f, 9.55f, 0f);
            var bannerBatch = new MeshBatchBuilder();
            bannerBatch.AddChamferedBox(new Vector3(1.15f, 0f, 0f), Quaternion.identity, new Vector3(2.25f, 1.15f, 0.08f), 0.02f);
            CreateMeshObject("HallBanner", bannerPivot.transform, env.Track(bannerBatch.ToMesh("HallBannerMesh")), bannerMat, Vector3.zero, receiveShadows: false, castShadows: true);
            env.HallBannerTransform = bannerPivot.transform;
            return go;
        }

        static GameObject BuildCreekWaterTower(
            Transform parent,
            BarangayEnvironment env,
            SVector2 ground,
            Material steelMat,
            Material cementMat,
            Material sageTrimMat,
            Material ledgeTrimMat)
        {
            var go = new GameObject("UptownWaterTower");
            go.transform.SetParent(parent, false);
            go.transform.position = KalyeahResources.World(ground, 0f);

            var legs = new MeshBatchBuilder();
            // Wide 2.55 m leg spread so kids fight the Philippine Eagle cleanly from below without leg snags
            float legSpread = 2.55f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    legs.AddChamferedBox(new Vector3(sx * legSpread, 3.7f, sz * legSpread), Quaternion.identity, new Vector3(0.32f, 7.4f, 0.32f), 0.04f);
                }
            }
            // Mid & upper cross-braces cleanly above Band-2 combat height (Y = 3.2 m & 6.2 m)
            float[] braceYs = { 3.2f, 6.2f };
            for (int b = 0; b < braceYs.Length; b++)
            {
                float by = braceYs[b];
                legs.AddChamferedBox(new Vector3(0f, by, legSpread), Quaternion.identity, new Vector3(legSpread * 2.05f, 0.22f, 0.22f), 0.03f);
                legs.AddChamferedBox(new Vector3(0f, by, -legSpread), Quaternion.identity, new Vector3(legSpread * 2.05f, 0.22f, 0.22f), 0.03f);
                legs.AddChamferedBox(new Vector3(legSpread, by, 0f), Quaternion.identity, new Vector3(0.22f, 0.22f, legSpread * 2.05f), 0.03f);
                legs.AddChamferedBox(new Vector3(-legSpread, by, 0f), Quaternion.identity, new Vector3(0.22f, 0.22f, legSpread * 2.05f), 0.03f);
            }

            var tank = new MeshBatchBuilder();
            tank.AddChamferedBox(new Vector3(0f, 7.55f, 0f), Quaternion.identity, new Vector3(5.9f, 0.35f, 5.9f), 0.06f);
            tank.AddChamferedBox(new Vector3(0f, 9.40f, 0f), Quaternion.identity, new Vector3(4.2f, 3.35f, 4.2f), 0.35f);

            var trim = new MeshBatchBuilder();
            trim.AddChamferedBox(new Vector3(0f, 8.20f, 0f), Quaternion.identity, new Vector3(4.36f, 0.26f, 4.36f), 0.05f);
            trim.AddChamferedBox(new Vector3(0f, 10.55f, 0f), Quaternion.identity, new Vector3(4.36f, 0.26f, 4.36f), 0.05f);

            var cap = new MeshBatchBuilder();
            cap.AddChamferedBox(new Vector3(0f, 11.18f, 0f), Quaternion.identity, new Vector3(4.55f, 0.28f, 4.55f), 0.08f);

            CreateMeshObject("TowerSteelLegs", go.transform, env.Track(legs.ToMesh("WaterTowerLegsMesh")), steelMat, Vector3.zero, receiveShadows: true, castShadows: true);
            CreateMeshObject("TowerConcreteTank", go.transform, env.Track(tank.ToMesh("WaterTowerTankMesh")), cementMat, Vector3.zero, receiveShadows: true, castShadows: true);
            CreateMeshObject("TowerSageBands", go.transform, env.Track(trim.ToMesh("WaterTowerTrimMesh")), sageTrimMat, Vector3.zero, receiveShadows: true);
            CreateMeshObject("TowerRoofRim", go.transform, env.Track(cap.ToMesh("WaterTowerCapMesh")), ledgeTrimMat, Vector3.zero, receiveShadows: true);
            return go;
        }

        static GameObject BuildRiversideChapelBellTower(
            Transform parent,
            BarangayEnvironment env,
            SVector2 ground,
            Material stuccoMat,
            Material terracottaMat,
            Material bellBrassMat)
        {
            var go = new GameObject("RiversideChapelBellTower");
            go.transform.SetParent(parent, false);
            go.transform.position = KalyeahResources.World(ground + new SVector2(-6.4f, -1.8f), 0f);

            var body = new MeshBatchBuilder();
            body.AddChamferedBox(new Vector3(0f, 4.2f, 0f), Quaternion.identity, new Vector3(3.4f, 8.4f, 3.4f), 0.09f);
            // 4 belfry corner piers framing an open bell alcove (Y = 8.4..10.6 m)
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    body.AddChamferedBox(new Vector3(sx * 1.22f, 9.5f, sz * 1.22f), Quaternion.identity, new Vector3(0.62f, 2.2f, 0.62f), 0.05f);

            var roof = new MeshBatchBuilder();
            roof.AddChamferedBox(new Vector3(0f, 10.85f, 0f), Quaternion.identity, new Vector3(4.1f, 0.55f, 4.1f), 0.12f);
            roof.AddChamferedBox(new Vector3(0f, 11.65f, 0f), Quaternion.identity, new Vector3(2.8f, 1.05f, 2.8f), 0.25f);
            roof.AddChamferedBox(new Vector3(0f, 12.35f, 0f), Quaternion.identity, new Vector3(1.2f, 0.55f, 1.2f), 0.16f);

            var bell = new MeshBatchBuilder();
            bell.AddChamferedBox(new Vector3(0f, 9.45f, 0f), Quaternion.identity, new Vector3(0.88f, 0.95f, 0.88f), 0.14f);

            CreateMeshObject("BellTowerStucco", go.transform, env.Track(body.ToMesh("ChapelBellTowerStuccoMesh")), stuccoMat, Vector3.zero, receiveShadows: true, castShadows: true);
            CreateMeshObject("BellTowerRoof", go.transform, env.Track(roof.ToMesh("ChapelBellTowerRoofMesh")), terracottaMat, Vector3.zero, receiveShadows: true, castShadows: true);
            CreateMeshObject("ChapelBrassBell", go.transform, env.Track(bell.ToMesh("ChapelBellMesh")), bellBrassMat, Vector3.zero, receiveShadows: false, castShadows: true);
            return go;
        }

        static GameObject BuildSariSariAwningAndClimbAccents(
            Transform parent,
            BarangayEnvironment env,
            Material woodMat,
            Material tinMat,
            Material creamMat,
            Material goldMat,
            Material ledgeTrimMat)
        {
            var go = new GameObject("RiversideSariSariAndClimbSpots");
            go.transform.SetParent(parent, false);

            // 1. Riverside Sari-Sari Roof climb spot (-9.8, 16.4): Band-3 wood/tin awning + lighter climbable roof trim
            var sari = new GameObject("SariSariClimbAwning");
            sari.transform.SetParent(go.transform, false);
            sari.transform.position = KalyeahResources.World(new SVector2(-9.8f, 16.4f), 0f);

            var woodBatch = new MeshBatchBuilder();
            woodBatch.AddChamferedBox(new Vector3(-3.6f, 2.65f, 4.35f), Quaternion.identity, new Vector3(0.14f, 0.72f, 0.14f), 0.02f);
            woodBatch.AddChamferedBox(new Vector3(3.6f, 2.65f, 4.35f), Quaternion.identity, new Vector3(0.14f, 0.72f, 0.14f), 0.02f);

            var awningBatch = new MeshBatchBuilder();
            awningBatch.AddChamferedBox(new Vector3(0f, 2.75f, 4.15f), Quaternion.Euler(12f, 0f, 0f), new Vector3(7.8f, 0.14f, 1.45f), 0.03f);

            var trimBatch = new MeshBatchBuilder();
            trimBatch.AddChamferedBox(new Vector3(0f, 3.12f, 0f), Quaternion.identity, new Vector3(8.1f, 0.10f, 8.2f), 0.03f);

            CreateMeshObject("AwningStruts", sari.transform, env.Track(woodBatch.ToMesh("SariAwningStrutsMesh")), woodMat, Vector3.zero, receiveShadows: true);
            CreateMeshObject("TinAwning", sari.transform, env.Track(awningBatch.ToMesh("SariTinAwningMesh")), tinMat, Vector3.zero, receiveShadows: true, castShadows: true);
            CreateMeshObject("SariRoofClimbTrim", sari.transform, env.Track(trimBatch.ToMesh("SariRoofTrimMesh")), ledgeTrimMat, Vector3.zero, receiveShadows: true);

            // 2. Uptown Backboard Stand climb accent (-10.6, -56.4): lighter climbable stand rim
            var backboardTrim = new MeshBatchBuilder();
            backboardTrim.AddChamferedBox(
                KalyeahResources.World(new SVector2(-10.6f, -56.4f), 2.58f),
                Quaternion.identity,
                new Vector3(1.85f, 0.10f, 1.25f),
                0.03f);
            CreateMeshObject("BackboardClimbTrim", go.transform, env.Track(backboardTrim.ToMesh("BackboardClimbTrimMesh")), ledgeTrimMat, Vector3.zero, receiveShadows: true);

            // 3. Mid-Map Parked Jeepney climb spot on dry bridge apron (5.5, 12.0 — >= 6 m clear of Selecta river-road loop!)
            var jeep = new GameObject("BridgeClimbJeepney");
            jeep.transform.SetParent(go.transform, false);
            jeep.transform.position = KalyeahResources.World(new SVector2(5.5f, 12.0f), 0f);
            jeep.transform.rotation = Quaternion.Euler(0f, -14f, 0f);

            var bodyBatch = new MeshBatchBuilder();
            bodyBatch.AddChamferedBox(new Vector3(0f, 1.15f, -0.35f), Quaternion.identity, new Vector3(1.92f, 1.75f, 3.65f), 0.08f);
            bodyBatch.AddChamferedBox(new Vector3(0f, 0.85f, 2.05f), Quaternion.identity, new Vector3(1.74f, 1.15f, 1.35f), 0.08f);

            var accentBatch = new MeshBatchBuilder();
            accentBatch.AddChamferedBox(new Vector3(0f, 1.32f, -0.35f), Quaternion.identity, new Vector3(1.98f, 0.24f, 3.70f), 0.03f);

            var jeepTop = new MeshBatchBuilder();
            jeepTop.AddChamferedBox(new Vector3(0f, 2.18f, -0.35f), Quaternion.identity, new Vector3(1.96f, 0.14f, 3.68f), 0.04f);

            CreateMeshObject("JeepneyBody", jeep.transform, env.Track(bodyBatch.ToMesh("BridgeJeepneyBodyMesh")), creamMat, Vector3.zero, receiveShadows: true, castShadows: true);
            CreateMeshObject("JeepneyBand", jeep.transform, env.Track(accentBatch.ToMesh("BridgeJeepneyBandMesh")), goldMat, Vector3.zero, receiveShadows: true);
            CreateMeshObject("JeepneyClimbRoof", jeep.transform, env.Track(jeepTop.ToMesh("BridgeJeepneyRoofMesh")), ledgeTrimMat, Vector3.zero, receiveShadows: true);
            env.AddOccluder(jeep);

            return go;
        }

        static void BuildRiverSandbar(Transform parent, BarangayEnvironment env, SVector2 ground, Material sandbarMat)
        {
            var bar = new MeshBatchBuilder();
            bar.AddChamferedBox(KalyeahResources.World(ground, -0.14f), Quaternion.Euler(0f, 14f, 0f), new Vector3(5.6f, 0.18f, 3.9f), 0.07f);
            bar.AddChamferedBox(KalyeahResources.World(ground + new SVector2(-1.4f, 0.4f), -0.16f), Quaternion.Euler(0f, -18f, 0f), new Vector3(3.4f, 0.14f, 2.6f), 0.06f);
            CreateMeshObject("DragonRiverSandbar", parent, env.Track(bar.ToMesh("DragonSandbarMesh")), sandbarMat, Vector3.zero, receiveShadows: true);
        }

        static GameObject BuildCrocodileVisual(
            Transform parent,
            BarangayEnvironment env,
            Material litTemplate,
            SVector2 ground,
            Material bellyMat,
            Material eyeGoldMat)
        {
            var go = new GameObject("CrocodileDragonVisual");
            go.transform.SetParent(parent, false);
            go.transform.position = KalyeahResources.World(ground, -0.04f);

            Material crocBodyMat = MakeLitMaterial(env, litTemplate, "CrocodileBodyMat", new Color(0.32f, 0.56f, 0.34f), 0.24f, 0f);
            env.CrocBodyMat = crocBodyMat;

            var body = new MeshBatchBuilder();
            // Chunky stylized body, stubby cute snout, tapered tail, 4 short legs (zero gore!)
            body.AddChamferedBox(new Vector3(0f, 0.44f, 0f), Quaternion.identity, new Vector3(1.25f, 0.62f, 2.15f), 0.14f);
            body.AddChamferedBox(new Vector3(0f, 0.48f, 1.48f), Quaternion.identity, new Vector3(0.84f, 0.44f, 1.05f), 0.11f);
            body.AddChamferedBox(new Vector3(0f, 0.38f, -1.65f), Quaternion.identity, new Vector3(0.68f, 0.42f, 1.45f), 0.11f);
            body.AddChamferedBox(new Vector3(-0.72f, 0.22f, 0.62f), Quaternion.identity, new Vector3(0.36f, 0.34f, 0.46f), 0.06f);
            body.AddChamferedBox(new Vector3(0.72f, 0.22f, 0.62f), Quaternion.identity, new Vector3(0.36f, 0.34f, 0.46f), 0.06f);
            body.AddChamferedBox(new Vector3(-0.72f, 0.22f, -0.68f), Quaternion.identity, new Vector3(0.36f, 0.34f, 0.46f), 0.06f);
            body.AddChamferedBox(new Vector3(0.72f, 0.22f, -0.68f), Quaternion.identity, new Vector3(0.36f, 0.34f, 0.46f), 0.06f);

            var belly = new MeshBatchBuilder();
            belly.AddChamferedBox(new Vector3(0f, 0.22f, 0.1f), Quaternion.identity, new Vector3(1.06f, 0.22f, 2.05f), 0.05f);

            var eyes = new MeshBatchBuilder();
            eyes.AddChamferedBox(new Vector3(-0.34f, 0.76f, 1.15f), Quaternion.identity, new Vector3(0.20f, 0.18f, 0.22f), 0.04f);
            eyes.AddChamferedBox(new Vector3(0.34f, 0.76f, 1.15f), Quaternion.identity, new Vector3(0.20f, 0.18f, 0.22f), 0.04f);

            CreateMeshObject("CrocBody", go.transform, env.Track(body.ToMesh("CrocodileBodyMesh")), crocBodyMat, Vector3.zero, receiveShadows: true, castShadows: true);
            CreateMeshObject("CrocBelly", go.transform, env.Track(belly.ToMesh("CrocodileBellyMesh")), bellyMat, Vector3.zero, receiveShadows: false);
            CreateMeshObject("CrocEyes", go.transform, env.Track(eyes.ToMesh("CrocodileEyesMesh")), eyeGoldMat, Vector3.zero, receiveShadows: false);
            return go;
        }

        static GameObject BuildEagleVisual(
            Transform parent,
            BarangayEnvironment env,
            Material litTemplate,
            SVector2 ground,
            float perchY,
            Material chestCreamMat,
            Material crestGoldMat)
        {
            var go = new GameObject("PhilippineEagleBaronVisual");
            go.transform.SetParent(parent, false);
            go.transform.position = KalyeahResources.World(ground, perchY);

            Material eagleBodyMat = MakeLitMaterial(env, litTemplate, "EagleBodyMat", new Color(0.46f, 0.34f, 0.24f), 0.22f, 0f);
            env.EagleBodyMat = eagleBodyMat;

            var wings = new MeshBatchBuilder();
            wings.AddChamferedBox(new Vector3(0f, 0.85f, 0f), Quaternion.identity, new Vector3(1.10f, 1.25f, 1.05f), 0.18f);
            wings.AddChamferedBox(new Vector3(-1.35f, 1.05f, -0.10f), Quaternion.Euler(0f, 0f, 16f), new Vector3(1.85f, 0.34f, 0.92f), 0.08f);
            wings.AddChamferedBox(new Vector3(1.35f, 1.05f, -0.10f), Quaternion.Euler(0f, 0f, -16f), new Vector3(1.85f, 0.34f, 0.92f), 0.08f);
            wings.AddChamferedBox(new Vector3(0f, 0.55f, -0.95f), Quaternion.Euler(-20f, 0f, 0f), new Vector3(0.85f, 0.22f, 0.95f), 0.05f);

            var chest = new MeshBatchBuilder();
            chest.AddChamferedBox(new Vector3(0f, 0.88f, 0.45f), Quaternion.identity, new Vector3(0.88f, 1.02f, 0.38f), 0.12f);
            chest.AddChamferedBox(new Vector3(0f, 1.62f, 0.22f), Quaternion.identity, new Vector3(0.68f, 0.62f, 0.66f), 0.14f);

            var crestAndBeak = new MeshBatchBuilder();
            // Iconic Philippine Eagle golden crest plumes + curved beak
            crestAndBeak.AddChamferedBox(new Vector3(0f, 2.02f, 0.05f), Quaternion.Euler(-18f, 0f, 0f), new Vector3(0.84f, 0.32f, 0.56f), 0.06f);
            crestAndBeak.AddChamferedBox(new Vector3(0f, 1.54f, 0.72f), Quaternion.Euler(14f, 0f, 0f), new Vector3(0.24f, 0.28f, 0.46f), 0.05f);

            CreateMeshObject("EagleWings", go.transform, env.Track(wings.ToMesh("EagleWingsMesh")), eagleBodyMat, Vector3.zero, receiveShadows: true, castShadows: true);
            CreateMeshObject("EagleChest", go.transform, env.Track(chest.ToMesh("EagleChestMesh")), chestCreamMat, Vector3.zero, receiveShadows: true);
            CreateMeshObject("EagleCrestAndBeak", go.transform, env.Track(crestAndBeak.ToMesh("EagleCrestMesh")), crestGoldMat, Vector3.zero, receiveShadows: false);
            return go;
        }

        static GameObject BuildMangTahoVendor(
            Transform parent,
            BarangayEnvironment env,
            SVector2 ground,
            Material woodMat,
            Material aluminumMat,
            Material umbrellaMat,
            Material cupCreamMat)
        {
            var go = new GameObject("Vendor_MangTaho");
            go.transform.SetParent(parent, false);
            go.transform.position = KalyeahResources.World(ground, 0f);

            var frame = new MeshBatchBuilder();
            // Bamboo yoke resting on wooden stand + Band-3 umbrella pole
            frame.AddChamferedBox(new Vector3(0f, 0.52f, 0f), Quaternion.identity, new Vector3(1.65f, 0.16f, 0.42f), 0.03f);
            frame.AddChamferedBox(new Vector3(-0.68f, 0.26f, 0f), Quaternion.identity, new Vector3(0.14f, 0.52f, 0.38f), 0.02f);
            frame.AddChamferedBox(new Vector3(0.68f, 0.26f, 0f), Quaternion.identity, new Vector3(0.14f, 0.52f, 0.38f), 0.02f);
            frame.AddChamferedBox(new Vector3(0f, 1.45f, -0.35f), Quaternion.identity, new Vector3(0.10f, 2.85f, 0.10f), 0.02f);

            var buckets = new MeshBatchBuilder();
            buckets.AddChamferedBox(new Vector3(-0.62f, 0.45f, 0f), Quaternion.identity, new Vector3(0.56f, 0.68f, 0.56f), 0.10f);
            buckets.AddChamferedBox(new Vector3(0.62f, 0.45f, 0f), Quaternion.identity, new Vector3(0.56f, 0.68f, 0.56f), 0.10f);

            var canopy = new MeshBatchBuilder();
            canopy.AddChamferedBox(new Vector3(0f, 2.88f, -0.15f), Quaternion.identity, new Vector3(2.35f, 0.24f, 2.15f), 0.08f);

            CreateMeshObject("TahoStand", go.transform, env.Track(frame.ToMesh("TahoStandMesh")), woodMat, Vector3.zero, receiveShadows: true, castShadows: true);
            CreateMeshObject("TahoBuckets", go.transform, env.Track(buckets.ToMesh("TahoBucketsMesh")), aluminumMat, Vector3.zero, receiveShadows: true, castShadows: true);
            CreateMeshObject("TahoUmbrella", go.transform, env.Track(canopy.ToMesh("TahoUmbrellaMesh")), umbrellaMat, Vector3.zero, receiveShadows: true, castShadows: true);

            // Stock cups/lids hidden while restocking (30 s empty indicator)
            var stock = new MeshBatchBuilder();
            stock.AddChamferedBox(new Vector3(-0.62f, 0.85f, 0f), Quaternion.identity, new Vector3(0.42f, 0.16f, 0.42f), 0.04f);
            stock.AddChamferedBox(new Vector3(0.62f, 0.85f, 0f), Quaternion.identity, new Vector3(0.42f, 0.16f, 0.42f), 0.04f);
            env.TahoStockProp = CreateMeshObject("TahoStockReady", go.transform, env.Track(stock.ToMesh("TahoStockMesh")), cupCreamMat, Vector3.zero, receiveShadows: false);
            return go;
        }

        static GameObject BuildFishballCartVendor(
            Transform parent,
            BarangayEnvironment env,
            SVector2 ground,
            Material woodMat,
            Material steelMat,
            Material canopyMat,
            Material stockGoldMat)
        {
            var go = new GameObject("Vendor_FishballCart");
            go.transform.SetParent(parent, false);
            go.transform.position = KalyeahResources.World(ground, 0f);

            var cart = new MeshBatchBuilder();
            cart.AddChamferedBox(new Vector3(0f, 0.62f, 0f), Quaternion.identity, new Vector3(1.85f, 0.78f, 1.05f), 0.06f);
            cart.AddChamferedBox(new Vector3(-0.82f, 1.65f, -0.42f), Quaternion.identity, new Vector3(0.10f, 2.25f, 0.10f), 0.02f);
            cart.AddChamferedBox(new Vector3(0.82f, 1.65f, -0.42f), Quaternion.identity, new Vector3(0.10f, 2.25f, 0.10f), 0.02f);

            var wok = new MeshBatchBuilder();
            wok.AddChamferedBox(new Vector3(-0.35f, 1.06f, 0f), Quaternion.identity, new Vector3(0.76f, 0.16f, 0.72f), 0.05f);
            wok.AddChamferedBox(new Vector3(-0.72f, 0.28f, 0.56f), Quaternion.identity, new Vector3(0.52f, 0.52f, 0.12f), 0.08f);
            wok.AddChamferedBox(new Vector3(0.72f, 0.28f, 0.56f), Quaternion.identity, new Vector3(0.52f, 0.52f, 0.12f), 0.08f);

            var roof = new MeshBatchBuilder();
            roof.AddChamferedBox(new Vector3(0f, 2.75f, -0.05f), Quaternion.Euler(8f, 0f, 0f), new Vector3(2.25f, 0.20f, 1.55f), 0.06f);

            CreateMeshObject("FishballCartBody", go.transform, env.Track(cart.ToMesh("FishballCartMesh")), woodMat, Vector3.zero, receiveShadows: true, castShadows: true);
            CreateMeshObject("FishballWokAndWheels", go.transform, env.Track(wok.ToMesh("FishballWokMesh")), steelMat, Vector3.zero, receiveShadows: true);
            CreateMeshObject("FishballCanopy", go.transform, env.Track(roof.ToMesh("FishballCanopyMesh")), canopyMat, Vector3.zero, receiveShadows: true, castShadows: true);

            // Golden fishball tray + sauce jars hidden while restocking
            var stock = new MeshBatchBuilder();
            stock.AddChamferedBox(new Vector3(0.45f, 1.15f, 0f), Quaternion.identity, new Vector3(0.58f, 0.24f, 0.52f), 0.05f);
            env.FishballStockProp = CreateMeshObject("FishballStockReady", go.transform, env.Track(stock.ToMesh("FishballStockMesh")), stockGoldMat, Vector3.zero, receiveShadows: false);
            return go;
        }

        static GameObject BuildSelectaBikeVisual(
            Transform parent,
            BarangayEnvironment env,
            SVector2 startGround,
            Material coolerMat,
            Material goldTrimMat,
            Material steelMat)
        {
            var go = new GameObject("SelectaIceCreamBike");
            go.transform.SetParent(parent, false);
            go.transform.position = KalyeahResources.World(startGround, 0f);

            var cooler = new MeshBatchBuilder();
            cooler.AddChamferedBox(new Vector3(0f, 0.72f, 0.35f), Quaternion.identity, new Vector3(1.05f, 0.78f, 1.25f), 0.08f);

            var trim = new MeshBatchBuilder();
            trim.AddChamferedBox(new Vector3(0f, 1.15f, 0.35f), Quaternion.identity, new Vector3(1.12f, 0.14f, 1.32f), 0.04f);
            trim.AddChamferedBox(new Vector3(0f, 2.35f, 0.20f), Quaternion.identity, new Vector3(1.65f, 0.18f, 1.65f), 0.06f);

            var bike = new MeshBatchBuilder();
            bike.AddChamferedBox(new Vector3(0f, 0.52f, -0.78f), Quaternion.identity, new Vector3(0.24f, 0.65f, 1.15f), 0.05f);
            bike.AddChamferedBox(new Vector3(0f, 1.45f, 0.20f), Quaternion.identity, new Vector3(0.08f, 1.85f, 0.08f), 0.02f);

            CreateMeshObject("SelectaCooler", go.transform, env.Track(cooler.ToMesh("SelectaCoolerMesh")), coolerMat, Vector3.zero, receiveShadows: true, castShadows: true);
            CreateMeshObject("SelectaGoldTrim", go.transform, env.Track(trim.ToMesh("SelectaTrimMesh")), goldTrimMat, Vector3.zero, receiveShadows: true, castShadows: true);
            CreateMeshObject("SelectaFrame", go.transform, env.Track(bike.ToMesh("SelectaFrameMesh")), steelMat, Vector3.zero, receiveShadows: true);

            var bellPivot = new GameObject("SelectaBell");
            bellPivot.transform.SetParent(go.transform, false);
            bellPivot.transform.localPosition = new Vector3(0.32f, 1.24f, 0.92f);
            var bell = new MeshBatchBuilder();
            bell.AddChamferedBox(Vector3.zero, Quaternion.identity, new Vector3(0.16f, 0.18f, 0.16f), 0.04f);
            CreateMeshObject("BellCup", bellPivot.transform, env.Track(bell.ToMesh("SelectaBellMesh")), goldTrimMat, Vector3.zero, receiveShadows: false);
            env.SelectaBellTransform = bellPivot.transform;

            go.SetActive(false); // Starts dormant until 0:30 spawn
            return go;
        }

        static void BuildTallCoverClumps(
            Transform parent,
            BarangayEnvironment env,
            Material woodMat,
            Material laundryMat,
            Material foliageMat)
        {
            IReadOnlyList<Rect2> zones = MapLayout.DefaultBarangayTallCoverZones();
            var poles = new MeshBatchBuilder();
            var cloths = new MeshBatchBuilder();
            var leaves = new MeshBatchBuilder();

            for (int i = 0; i < zones.Count; i++)
            {
                Rect2 r = zones[i];
                Vec2 c = r.GetCenter();
                bool isLaundry = c.x < -20.0;
                if (isLaundry)
                {
                    // Left-alley sampayan soft cover: bamboo poles + spaced hanging cream sheets with visible gaps
                    float halfW = (float)r.Size.x * 0.42f;
                    poles.AddChamferedBox(KalyeahResources.World(new SVector2((float)c.x - halfW, (float)c.y), 1.05f), Quaternion.identity, new Vector3(0.12f, 2.1f, 0.12f), 0.02f);
                    poles.AddChamferedBox(KalyeahResources.World(new SVector2((float)c.x + halfW, (float)c.y), 1.05f), Quaternion.identity, new Vector3(0.12f, 2.1f, 0.12f), 0.02f);
                    for (int s = -1; s <= 1; s++)
                    {
                        float sx = (float)c.x + s * (halfW * 0.62f);
                        cloths.AddChamferedBox(
                            KalyeahResources.World(new SVector2(sx, (float)c.y + (s % 2) * 0.45f), 1.12f),
                            Quaternion.Euler(0f, s * 12f, 0f),
                            new Vector3(1.15f, 1.48f, 0.14f),
                            0.03f);
                    }
                }
                else
                {
                    // Creek-bank & mid chunky painted cane / foliage clumps (docs/GRAPHICS.md L210)
                    for (int k = 0; k < 4; k++)
                    {
                        float ox = ((k % 2 == 0) ? -0.28f : 0.28f) * (float)r.Size.x;
                        float oz = ((k < 2) ? -0.26f : 0.26f) * (float)r.Size.y;
                        float h = 1.65f + (k % 2) * 0.28f;
                        leaves.AddChamferedBox(
                            KalyeahResources.World(new SVector2((float)c.x + ox, (float)c.y + oz), h * 0.5f),
                            Quaternion.Euler(0f, k * 29f + i * 17f, 0f),
                            new Vector3(1.35f, h, 1.25f),
                            0.24f);
                    }
                }
            }

            if (!poles.IsEmpty)
                CreateMeshObject("TallCoverPoles", parent, env.Track(poles.ToMesh("TallCoverPolesMesh")), woodMat, Vector3.zero, receiveShadows: true);
            if (!cloths.IsEmpty)
                CreateMeshObject("TallCoverSampayan", parent, env.Track(cloths.ToMesh("TallCoverLaundryMesh")), laundryMat, Vector3.zero, receiveShadows: true);
            if (!leaves.IsEmpty)
                CreateMeshObject("TallCoverFoliage", parent, env.Track(leaves.ToMesh("TallCoverFoliageMesh")), foliageMat, Vector3.zero, receiveShadows: true);
        }

        static void BuildPooledBuffAuras(Transform parent, BarangayEnvironment env)
        {
            const int poolSize = 10;
            Mesh ringMesh = env.Track(CreateFlatRingMesh("BuffAuraRingMesh", 0.72f, 1.02f, 28));

            Material template = Resources.Load<Material>("KalyeahMaterials/Unlit");
            Shader fallbackShader = template == null
                ? (Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"))
                : null;
            Material auraMat = template != null
                ? new Material(template) { name = "BuffFootAuraMat", enableInstancing = true }
                : (fallbackShader != null ? new Material(fallbackShader) { name = "BuffFootAuraMat", enableInstancing = true } : null);
            if (auraMat != null)
            {
                if (auraMat.HasProperty("_Surface")) auraMat.SetFloat("_Surface", 1f);
                if (auraMat.HasProperty("_SrcBlend")) auraMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                if (auraMat.HasProperty("_DstBlend")) auraMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (auraMat.HasProperty("_ZWrite")) auraMat.SetFloat("_ZWrite", 0f);
                auraMat.renderQueue = (int)RenderQueue.Transparent;
                env.Track(auraMat);
            }

            var auraRoot = new GameObject("BuffFootAuras").transform;
            auraRoot.SetParent(parent, false);
            env.BuffAuraTransforms = new Transform[poolSize];
            env.BuffAuraRenderers = new MeshRenderer[poolSize];
            env.BuffAuraMpb = new MaterialPropertyBlock();

            for (int i = 0; i < poolSize; i++)
            {
                GameObject ringGo = CreateMeshObject($"BuffAura_{i}", auraRoot, ringMesh, auraMat, Vector3.zero, receiveShadows: false, castShadows: false);
                ringGo.SetActive(false);
                env.BuffAuraTransforms[i] = ringGo.transform;
                env.BuffAuraRenderers[i] = ringGo.GetComponent<MeshRenderer>();
            }
        }

        static Mesh CreateFlatRingMesh(string name, float innerR, float outerR, int segments)
        {
            int vertCount = (segments + 1) * 2;
            var verts = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var tris = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                float ang = u * MathF.PI * 2f;
                float ca = MathF.Cos(ang);
                float sa = MathF.Sin(ang);
                int vi = i * 2;
                verts[vi] = new Vector3(ca * innerR, 0f, sa * innerR);
                verts[vi + 1] = new Vector3(ca * outerR, 0f, sa * outerR);
                normals[vi] = Vector3.up;
                normals[vi + 1] = Vector3.up;
                uvs[vi] = new Vector2(u, 0f);
                uvs[vi + 1] = new Vector2(u, 1f);
            }

            int ti = 0;
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                tris[ti++] = a;
                tris[ti++] = c;
                tris[ti++] = b;
                tris[ti++] = b;
                tris[ti++] = c;
                tris[ti++] = d;
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---- Procedural Landmark Stand-ins (when .glb assets are absent in unit tests) ------------------------------------------

        static GameObject BuildProceduralCourtFallback(Transform parent, BarangayEnvironment env, SVector2 ground, float godotYaw, Material litTemplate)
        {
            var go = new GameObject("CoveredBasketballCourt");
            go.transform.SetParent(parent, false);
            go.transform.position = KalyeahResources.World(ground, 0f);
            go.transform.rotation = Quaternion.Euler(0f, GodotSpace.ModelYawDegrees(godotYaw), 0f);

            var pillars = new MeshBatchBuilder();
            var roof = new MeshBatchBuilder();
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    pillars.AddChamferedBox(new Vector3(sx * 7.2f, 3.1f, sz * 6.4f), Quaternion.identity, new Vector3(0.55f, 6.2f, 0.55f), 0.05f);

            roof.AddChamferedBox(new Vector3(0f, 6.45f, 0f), Quaternion.identity, new Vector3(16.2f, 1.35f, 14.6f), 0.12f);

            Material pillarMat = MakeLitMaterial(env, litTemplate, "CourtPillarFallback", new Color(0.56f, 0.43f, 0.28f), 0.16f, 0f);
            Material roofMat = MakeLitMaterial(env, litTemplate, "CourtRoofFallback", new Color(0.58f, 0.40f, 0.28f), 0.36f, 0.20f);
            CreateMeshObject("Pillars", go.transform, env.Track(pillars.ToMesh("CourtPillarsFallback")), pillarMat, Vector3.zero, receiveShadows: true, castShadows: true);
            CreateMeshObject("ArchRoof", go.transform, env.Track(roof.ToMesh("CourtRoofFallback")), roofMat, Vector3.zero, receiveShadows: true, castShadows: true);
            return go;
        }

        static GameObject BuildProceduralHallFallback(Transform parent, BarangayEnvironment env, SVector2 ground, float godotYaw, Material litTemplate)
        {
            var go = new GameObject("BarangayHall");
            go.transform.SetParent(parent, false);
            go.transform.position = KalyeahResources.World(ground, 0f);
            go.transform.rotation = Quaternion.Euler(0f, GodotSpace.ModelYawDegrees(godotYaw), 0f);

            var facade = new MeshBatchBuilder();
            var trim = new MeshBatchBuilder();
            facade.AddChamferedBox(new Vector3(0f, 3.4f, 0f), Quaternion.identity, new Vector3(14.8f, 6.8f, 8.6f), 0.08f);
            trim.AddChamferedBox(new Vector3(0f, 7.1f, 0f), Quaternion.identity, new Vector3(15.6f, 0.95f, 9.4f), 0.08f);
            trim.AddChamferedBox(new Vector3(0f, 3.6f, 4.6f), Quaternion.identity, new Vector3(4.8f, 0.45f, 1.8f), 0.05f);

            Material facadeMat = MakeLitMaterial(env, litTemplate, "HallFacadeFallback", new Color(0.52f, 0.58f, 0.56f), 0.15f, 0f);
            Material trimMat = MakeLitMaterial(env, litTemplate, "HallTealFallback", new Color(0.32f, 0.46f, 0.44f), 0.28f, 0.10f);
            CreateMeshObject("Facade", go.transform, env.Track(facade.ToMesh("HallFacadeFallback")), facadeMat, Vector3.zero, receiveShadows: true, castShadows: true);
            CreateMeshObject("TealTrim", go.transform, env.Track(trim.ToMesh("HallTrimFallback")), trimMat, Vector3.zero, receiveShadows: true, castShadows: true);
            return go;
        }

        // ---- Road Splines & Kit Helpers -----------------------------------------------------------------------------------------

        static float SmoothBand(float edge0, float edge1, float val)
        {
            float t = Math.Clamp((val - edge0) / MathF.Max(edge1 - edge0, 0.0001f), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        static float LaneCenterX(float z)
        {
            float taper = 1f - SmoothBand(50f, 62f, MathF.Abs(z));
            return 4.2f * MathF.Sin(z * 0.048f) * taper;
        }

        static float LaneEastX(float z)
        {
            float taper = 1f - SmoothBand(56f, 74f, MathF.Abs(z));
            return 34.5f + (3.8f * MathF.Sin(z * 0.052f + 0.55f) - 0.055f * MathF.Abs(z)) * taper;
        }

        static float LaneWestX(float z) => -LaneEastX(-z);

        static float LaneX(int laneIndex, float z)
        {
            if (laneIndex == 0) return LaneWestX(z);
            if (laneIndex == 2) return LaneEastX(z);
            return LaneCenterX(z);
        }

        static GameObject FirstModel(string folder, string primaryId, string fallbackId)
        {
            GameObject primary = KalyeahResources.Model(folder, primaryId);
            return primary != null ? primary : KalyeahResources.Model(folder, fallbackId);
        }

        static GameObject PlaceKit(
            Transform parent,
            GameObject prefab,
            string name,
            SVector2 ground,
            float y,
            float godotYawRad,
            Vector3 scale)
        {
            GameObject inst = Object.Instantiate(prefab, parent, false);
            inst.name = name;
            KalyeahResources.SanitizeModelMaterials(inst);
            Transform t = inst.transform;
            t.localScale = scale;
            t.rotation = Quaternion.Euler(0f, GodotSpace.ModelYawDegrees(godotYawRad), 0f);
            t.position = KalyeahResources.World(ground, y);
            return inst;
        }

        static Vector3 MeasurePrefabSize(string id, GameObject prefab, Dictionary<string, Vector3> cache)
        {
            if (cache.TryGetValue(id, out Vector3 cached)) return cached;

            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            Matrix4x4 rootWorldToLocal = prefab.transform.worldToLocalMatrix;
            bool hasBounds = false;
            Bounds total = default;

            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null) continue;
                Matrix4x4 m = rootWorldToLocal * filters[i].transform.localToWorldMatrix;
                Bounds b = TransformBounds(m, mesh.bounds);
                if (!hasBounds)
                {
                    total = b;
                    hasBounds = true;
                }
                else
                {
                    total.Encapsulate(b);
                }
            }

            Vector3 size = hasBounds ? total.size : Vector3.one;
            cache[id] = size;
            return size;
        }

        static Bounds TransformBounds(Matrix4x4 m, Bounds local)
        {
            Vector3 c = m.MultiplyPoint3x4(local.center);
            Vector3 e = local.extents;
            Vector3 ax = m.MultiplyVector(new Vector3(e.x, 0f, 0f));
            Vector3 ay = m.MultiplyVector(new Vector3(0f, e.y, 0f));
            Vector3 az = m.MultiplyVector(new Vector3(0f, 0f, e.z));
            Vector3 worldExt = new Vector3(
                MathF.Abs(ax.x) + MathF.Abs(ay.x) + MathF.Abs(az.x),
                MathF.Abs(ax.y) + MathF.Abs(ay.y) + MathF.Abs(az.y),
                MathF.Abs(ax.z) + MathF.Abs(ay.z) + MathF.Abs(az.z));
            return new Bounds(c, worldExt * 2f);
        }

        static Material CreateShaderMaterial(BarangayEnvironment env, string resourceName, string shaderName, string instanceName)
        {
            Material shared = Resources.Load<Material>("KalyeahMaterials/" + resourceName);
            if (shared != null)
            {
                var clone = new Material(shared) { name = instanceName };
                return env != null ? env.Track(clone) : clone;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader != null)
            {
                var fresh = new Material(shader) { name = instanceName };
                return env != null ? env.Track(fresh) : fresh;
            }
            return null;
        }

        static Material ResolveLitTemplate(BarangayEnvironment env)
        {
            Material shared = Resources.Load<Material>("KalyeahMaterials/Ground");
            if (shared != null) return shared;
            shared = Resources.Load<Material>("KalyeahMaterials/Unlit");
            if (shared != null) return shared;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;

            var fallback = new Material(shader) { name = "BarangayLitTemplate_Fallback" };
            return env != null ? env.Track(fallback) : fallback;
        }

        static Material MakeLitMaterial(BarangayEnvironment env, Material template, string name, Color color, float smoothness, float metallic)
        {
            if (template == null) return null;
            var mat = new Material(template) { name = name, enableInstancing = true };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            return env != null ? env.Track(mat) : mat;
        }

        static GameObject CreateMeshObject(
            string name,
            Transform parent,
            Mesh mesh,
            Material material,
            Vector3 localPos,
            bool receiveShadows,
            bool castShadows = false)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = receiveShadows;
            if (material != null) mr.sharedMaterial = material;
            return go;
        }

        static Mesh CreateGridMesh(string name, float widthX, float depthZ, int segX, int segZ)
        {
            int vxCount = segX + 1;
            int vzCount = segZ + 1;
            int vertCount = vxCount * vzCount;
            var verts = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var tris = new int[segX * segZ * 6];

            float halfX = widthX * 0.5f;
            float halfZ = depthZ * 0.5f;

            int vIdx = 0;
            for (int iz = 0; iz <= segZ; iz++)
            {
                float tz = iz / (float)segZ;
                float z = Mathf.Lerp(-halfZ, halfZ, tz);
                for (int ix = 0; ix <= segX; ix++)
                {
                    float tx = ix / (float)segX;
                    float x = Mathf.Lerp(-halfX, halfX, tx);
                    verts[vIdx] = new Vector3(x, 0f, z);
                    normals[vIdx] = Vector3.up;
                    uvs[vIdx] = new Vector2(tx, tz);
                    vIdx++;
                }
            }

            int tIdx = 0;
            for (int iz = 0; iz < segZ; iz++)
            {
                int row0 = iz * vxCount;
                int row1 = (iz + 1) * vxCount;
                for (int ix = 0; ix < segX; ix++)
                {
                    int i0 = row0 + ix;
                    int i1 = row1 + ix;
                    int i2 = row1 + ix + 1;
                    int i3 = row0 + ix + 1;

                    // Clockwise when viewed from +Y in Unity left-handed space -> +Y upward normal.
                    tris[tIdx++] = i0;
                    tris[tIdx++] = i1;
                    tris[tIdx++] = i2;

                    tris[tIdx++] = i0;
                    tris[tIdx++] = i2;
                    tris[tIdx++] = i3;
                }
            }

            var mesh = new Mesh { name = name };
            if (vertCount > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            // Expand bounds slightly on Y so vertex-displaced Estero channel (-0.30 m) never frustum-culls early.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(widthX + 2f, 2.5f, depthZ + 12f));
            return mesh;
        }

        // ---- Chunky Beveled Static Mesh Batcher (3-5 cm bevels, single-mesh draw call merging) ----------------------------------

        sealed class MeshBatchBuilder
        {
            readonly List<Vector3> _verts = new List<Vector3>(1024);
            readonly List<Vector3> _normals = new List<Vector3>(1024);
            readonly List<Vector2> _uvs = new List<Vector2>(1024);
            readonly List<int> _tris = new List<int>(1536);

            public bool IsEmpty => _verts.Count == 0;

            /// <summary>
            /// Appends a chunky beveled box whose top perimeter has a 45-degree chamfer rim (`bevel` metres)
            /// so concrete curbs, bridge piers, coping stones, and pipe rails catch clean specular sun highlights.
            /// </summary>
            public void AddChamferedBox(Vector3 center, Quaternion rot, Vector3 size, float bevel)
            {
                Vector3 hx = new Vector3(size.x * 0.5f, 0f, 0f);
                Vector3 hy = new Vector3(0f, size.y * 0.5f, 0f);
                Vector3 hz = new Vector3(0f, 0f, size.z * 0.5f);

                float b = Math.Min(bevel, Math.Min(Math.Min(hx.x, hy.y), hz.z) * 0.45f);
                float yBot = -hy.y;
                float yMid = hy.y - b;
                float yTop = hy.y;
                float ox = hx.x;
                float oz = hz.z;
                float ix = MathF.Max(ox - b, 0.005f);
                float iz = MathF.Max(oz - b, 0.005f);

                // Bottom 4, Wall-top 4, Inset-top 4 (local space, transformed by center + rot)
                Vector3 b00 = Transform(center, rot, new Vector3(-ox, yBot, -oz));
                Vector3 b01 = Transform(center, rot, new Vector3(-ox, yBot, oz));
                Vector3 b11 = Transform(center, rot, new Vector3(ox, yBot, oz));
                Vector3 b10 = Transform(center, rot, new Vector3(ox, yBot, -oz));

                Vector3 m00 = Transform(center, rot, new Vector3(-ox, yMid, -oz));
                Vector3 m01 = Transform(center, rot, new Vector3(-ox, yMid, oz));
                Vector3 m11 = Transform(center, rot, new Vector3(ox, yMid, oz));
                Vector3 m10 = Transform(center, rot, new Vector3(ox, yMid, -oz));

                Vector3 t00 = Transform(center, rot, new Vector3(-ix, yTop, -iz));
                Vector3 t01 = Transform(center, rot, new Vector3(-ix, yTop, iz));
                Vector3 t11 = Transform(center, rot, new Vector3(ix, yTop, iz));
                Vector3 t10 = Transform(center, rot, new Vector3(ix, yTop, -iz));

                Vector3 nUp = rot * Vector3.up;
                Vector3 nDown = rot * Vector3.down;
                Vector3 nNorth = rot * Vector3.forward;
                Vector3 nSouth = rot * Vector3.back;
                Vector3 nEast = rot * Vector3.right;
                Vector3 nWest = rot * Vector3.left;

                const float invSqrt2 = 0.70710678f;
                Vector3 cNorth = (nUp + nNorth) * invSqrt2;
                Vector3 cSouth = (nUp + nSouth) * invSqrt2;
                Vector3 cEast = (nUp + nEast) * invSqrt2;
                Vector3 cWest = (nUp + nWest) * invSqrt2;

                // Top cap (+Y)
                AddQuad(t00, t01, t11, t10, nUp);
                // Bottom cap (-Y)
                AddQuad(b01, b00, b10, b11, nDown);

                // Vertical walls (South -Z, North +Z, East +X, West -X)
                AddQuad(b00, m00, m10, b10, nSouth);
                AddQuad(b11, m11, m01, b01, nNorth);
                AddQuad(b10, m10, m11, b11, nEast);
                AddQuad(b01, m01, m00, b00, nWest);

                // 45-degree specular bevel ring
                AddQuad(m00, t00, t10, m10, cSouth);
                AddQuad(m11, t11, t01, m01, cNorth);
                AddQuad(m10, t10, t11, m11, cEast);
                AddQuad(m01, t01, t00, m00, cWest);
            }

            static Vector3 Transform(Vector3 center, Quaternion rot, Vector3 local) => center + rot * local;

            /// <summary>
            /// Extrudes a continuous, stitched, 45-degree-chamfered stone embankment or mossy coping ribbon smoothly along
            /// <see cref="MapLayoutData.CanalZ"/> from <paramref name="xStart"/> to <paramref name="xEnd"/>, eliminating segment
            /// staircase steps and coplanar overlap Z-fighting on curved canal stretches.
            /// </summary>
            public void AddCanalBankRibbon(float xStart, float xEnd, float bankSign, int segCount, bool isCoping)
            {
                const int ringSize = 6;
                var rings = new Vector3[(segCount + 1) * ringSize];
                float spanLen = xEnd - xStart;

                for (int i = 0; i <= segCount; i++)
                {
                    float x = xStart + (i / (float)segCount) * spanLen;
                    float cz = MapLayoutData.CanalZ(x);
                    float taper = Math.Min(
                        Math.Clamp((x - xStart) / 2.4f, 0.25f, 1f),
                        Math.Clamp((xEnd - x) / 2.4f, 0.25f, 1f));

                    int baseRing = i * ringSize;
                    if (isCoping)
                    {
                        float topY = 0.045f + 0.055f * taper;
                        rings[baseRing + 0] = BankPoint(x, cz, bankSign, 4.12f, -0.10f);
                        rings[baseRing + 1] = BankPoint(x, cz, bankSign, 4.12f, topY - 0.035f);
                        rings[baseRing + 2] = BankPoint(x, cz, bankSign, 4.16f, topY);
                        rings[baseRing + 3] = BankPoint(x, cz, bankSign, 4.68f, topY);
                        rings[baseRing + 4] = BankPoint(x, cz, bankSign, 4.73f, topY - 0.035f);
                        rings[baseRing + 5] = BankPoint(x, cz, bankSign, 4.74f, -0.05f);
                    }
                    else
                    {
                        float crestJoinY = 0.010f + 0.025f * taper;
                        rings[baseRing + 0] = BankPoint(x, cz, bankSign, 3.08f, -0.28f);
                        rings[baseRing + 1] = BankPoint(x, cz, bankSign, 3.14f, -0.15f);
                        rings[baseRing + 2] = BankPoint(x, cz, bankSign, 4.04f, crestJoinY);
                        rings[baseRing + 3] = BankPoint(x, cz, bankSign, 4.10f, crestJoinY + 0.005f);
                        rings[baseRing + 4] = BankPoint(x, cz, bankSign, 4.16f, -0.04f);
                        rings[baseRing + 5] = BankPoint(x, cz, bankSign, 4.16f, -0.28f);
                    }
                }

                for (int i = 0; i < segCount; i++)
                {
                    int r0 = i * ringSize;
                    int r1 = (i + 1) * ringSize;
                    for (int k = 0; k < ringSize; k++)
                    {
                        int kNext = (k + 1) % ringSize;
                        Vector3 a0 = rings[r0 + k];
                        Vector3 a1 = rings[r0 + kNext];
                        Vector3 b0 = rings[r1 + k];
                        Vector3 b1 = rings[r1 + kNext];

                        if (bankSign > 0f)
                        {
                            Vector3 n = Vector3.Cross(b0 - a0, a1 - a0).normalized;
                            AddQuad(a0, b0, b1, a1, n);
                        }
                        else
                        {
                            Vector3 n = Vector3.Cross(a1 - a0, b0 - a0).normalized;
                            AddQuad(a0, a1, b1, b0, n);
                        }
                    }
                }

                // Capped stone ends at start (-X) and end (+X) of the sub-span
                int endRing = segCount * ringSize;
                for (int k = 1; k < ringSize - 1; k++)
                {
                    if (bankSign > 0f)
                    {
                        AddTri(rings[0], rings[k], rings[k + 1], Vector3.left);
                        AddTri(rings[endRing], rings[endRing + k + 1], rings[endRing + k], Vector3.right);
                    }
                    else
                    {
                        AddTri(rings[0], rings[k + 1], rings[k], Vector3.left);
                        AddTri(rings[endRing], rings[endRing + k], rings[endRing + k + 1], Vector3.right);
                    }
                }
            }

            static Vector3 BankPoint(float x, float cz, float bankSign, float d, float y) =>
                KalyeahResources.World(new SVector2(x, cz + bankSign * d), y);

            void AddTri(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 normal)
            {
                int baseIdx = _verts.Count;
                _verts.Add(p0);
                _verts.Add(p1);
                _verts.Add(p2);

                _normals.Add(normal);
                _normals.Add(normal);
                _normals.Add(normal);

                _uvs.Add(new Vector2(0f, 0f));
                _uvs.Add(new Vector2(0f, 1f));
                _uvs.Add(new Vector2(1f, 1f));

                _tris.Add(baseIdx);
                _tris.Add(baseIdx + 1);
                _tris.Add(baseIdx + 2);
            }

            void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal)
            {
                int baseIdx = _verts.Count;
                _verts.Add(p0);
                _verts.Add(p1);
                _verts.Add(p2);
                _verts.Add(p3);

                _normals.Add(normal);
                _normals.Add(normal);
                _normals.Add(normal);
                _normals.Add(normal);

                _uvs.Add(new Vector2(0f, 0f));
                _uvs.Add(new Vector2(0f, 1f));
                _uvs.Add(new Vector2(1f, 1f));
                _uvs.Add(new Vector2(1f, 0f));

                _tris.Add(baseIdx);
                _tris.Add(baseIdx + 1);
                _tris.Add(baseIdx + 2);

                _tris.Add(baseIdx);
                _tris.Add(baseIdx + 2);
                _tris.Add(baseIdx + 3);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (_verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(_verts);
                mesh.SetNormals(_normals);
                mesh.SetUVs(0, _uvs);
                mesh.SetTriangles(_tris, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
