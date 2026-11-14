using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SVector2 = System.Numerics.Vector2;
using SVector3 = System.Numerics.Vector3;

namespace Kalyeah.Game
{
    /// <summary>
    /// The street and the kids, drawn from the sim's state: the barangay kits where scripts/view/street_map.gd puts them (BarangayPlan),
    /// the ground, plain discs for the bases and rescue areas, the sun and sky of scripts/view/arena_environment.gd, and a KidView per
    /// player. Reads only; never changes the sim.
    /// </summary>
    public sealed class MatchView
    {
        // scripts/view/arena_environment.gd: warm afternoon sun from the south-east, a cool fill, a flat ambient
        static readonly Vector2 SunAngles = new Vector2(48f, 38f);      // (pitch down, yaw), degrees, Godot
        static readonly Color SunColor = new Color(1f, 0.91f, 0.78f);
        const float SunIntensity = 1.25f;                               // Godot energy 0.78 on its own exposure; tuned by eye, unverified
        static readonly Vector2 FillAngles = new Vector2(34f, -142f);
        static readonly Color FillColor = new Color(0.62f, 0.72f, 0.95f);
        const float FillIntensity = 0.25f;
        static readonly Color Ambient = new Color(0.86f, 0.86f, 0.89f) * 0.55f;
        static readonly Color GroundColor = new Color(0.62f, 0.56f, 0.46f);  // packed earth around the barangay
        static readonly Color StreetColor = new Color(0.70f, 0.67f, 0.60f);  // the cement inside the arena
        static readonly Color FogColor = new Color(0.80f, 0.86f, 0.92f);
        const float OuterGround = 1200f;                                // street_map.gd OUTER_GROUND_MARGIN
        static readonly Color OwnColor = new Color(0.20f, 0.50f, 1.00f);  // palette.gd TEAM_OWN
        static readonly Color EnemyColor = new Color(1.00f, 0.28f, 0.28f); // palette.gd TEAM_ENEMY

        public readonly Transform Root;
        public readonly MapLayoutData Layout;
        readonly CameraRulesData _camera;
        readonly Material _unlit;
        readonly Dictionary<int, KidView> _kids = new Dictionary<int, KidView>();
        readonly Dictionary<string, Vector3> _naturalSizes = new Dictionary<string, Vector3>();
        int _missingKits;

        public MatchView(Transform parent, MapLayoutData layout, CameraRulesData camera)
        {
            Root = new GameObject("MatchView").transform;
            Root.SetParent(parent, false);
            Layout = layout;
            _camera = camera;
            _unlit = KalyeahResources.Material("Unlit", "Universal Render Pipeline/Unlit");
            BuildLights();
            BuildGround();
            BuildZones();
            BuildKits();
            if (_missingKits > 0) Debug.LogWarning($"MatchView: {_missingKits} barangay kits are missing (run tools/unity_export/sync_assets.sh)");
        }

        public static Color TeamColor(int team) => team == SimBridge.LocalTeam ? OwnColor : EnemyColor;

        void BuildLights()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(Root, false);
            sun.type = LightType.Directional;
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.LookRotation(KalyeahResources.U(GodotSpace.Forward(SunAngles.x, SunAngles.y)));

            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(Root, false);
            fill.type = LightType.Directional;
            fill.color = FillColor;
            fill.intensity = FillIntensity;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.LookRotation(KalyeahResources.U(GodotSpace.Forward(FillAngles.x, FillAngles.y)));

            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Ambient;
            Material sky = KalyeahResources.Material("Sky", "Skybox/Procedural");
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = 76f;   // street_map.gd fog_distance()
            RenderSettings.fogEndDistance = 260f;
        }

        void BuildGround()
        {
            Material lit = KalyeahResources.Material("Ground", "Universal Render Pipeline/Lit");
            float outer = Layout.ArenaRadius * 2f + OuterGround;
            Flat("Ground", PrimitiveType.Plane, SVector2.Zero, new Vector3(outer / 10f, 1f, outer / 10f), 0f, lit, GroundColor);
            float arena = Layout.ArenaRadius * 2f + 6f;
            Flat("Street", PrimitiveType.Cylinder, SVector2.Zero, new Vector3(arena, 0.01f, arena), 0.005f, lit, StreetColor);
        }

        /// <summary>Plain stand-ins for street_map.gd's chalk zones: a tinted disc per base, a tinted slab per rescue area.</summary>
        void BuildZones()
        {
            foreach (int side in new[] { MapLayoutData.SideOwn, MapLayoutData.SideEnemy })
            {
                Color color = side == MapLayoutData.SideOwn ? OwnColor : EnemyColor;
                float d = Layout.BaseRadius * 2f;
                Flat($"Base_{side}", PrimitiveType.Cylinder, Layout.BaseCenter(side), new Vector3(d, 0.01f, d), 0.02f, _unlit, Color.Lerp(color, StreetColor, 0.45f));
                GroundRect rescue = Layout.RescueRect(side);
                Flat($"Rescue_{side}", PrimitiveType.Cube, rescue.Center, new Vector3(rescue.W, 0.01f, rescue.H), 0.02f, _unlit, Color.Lerp(color, Color.white, 0.5f));
            }
        }

        GameObject Flat(string name, PrimitiveType shape, SVector2 at, Vector3 scale, float y, Material material, Color color)
        {
            GameObject flat = GameObject.CreatePrimitive(shape);
            flat.name = name;
            KalyeahResources.Kill(flat.GetComponent<Collider>());
            flat.transform.SetParent(Root, false);
            flat.transform.position = KalyeahResources.World(at, y);
            flat.transform.localScale = scale;
            var renderer = flat.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (material != null) renderer.sharedMaterial = material;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
            return flat;
        }

        void BuildKits()
        {
            var parent = new GameObject("Barangay").transform;
            parent.SetParent(Root, false);
            foreach (KitPlacement kit in BarangayPlan.Build(Layout)) PlaceKit(parent, kit);
        }

        /// <summary>
        /// One kit: scaled in its own frame, turned as Godot turns it (GodotSpace.ModelYawDegrees), then either its origin on the spot or
        /// its footprint centred on the spot with its lowest point `Lift` above the ground (street_map.gd _add_obstacle).
        /// </summary>
        void PlaceKit(Transform parent, KitPlacement kit)
        {
            GameObject prefab = KalyeahResources.Model("barangay", kit.Kit);
            if (prefab == null)
            {
                _missingKits++;
                return;
            }
            GameObject model = Object.Instantiate(prefab, parent, false);
            model.name = kit.Kit;
            Transform t = model.transform;
            Vector3 natural = NaturalSize(kit.Kit, model);
            t.localScale = KalyeahResources.U(kit.ScaleFor(new SVector3(natural.x, natural.y, natural.z)));
            t.rotation = Quaternion.Euler(0f, GodotSpace.ModelYawDegrees(kit.GodotYawRad), 0f);
            Vector3 spot = KalyeahResources.World(kit.Ground);
            t.position = spot;
            if (kit.Fit == KitFit.Footprint)
            {
                Bounds placed = KalyeahResources.RenderBounds(model);
                t.position += new Vector3(spot.x - placed.center.x, kit.Lift - placed.min.y, spot.z - placed.center.z);
            }
        }

        /// <summary>A kit's size at scale 1 (street_map.gd _kit_bounds), measured once per kit.</summary>
        Vector3 NaturalSize(string id, GameObject model)
        {
            if (_naturalSizes.TryGetValue(id, out Vector3 size)) return size;
            Transform t = model.transform;
            t.localScale = Vector3.one;
            t.rotation = Quaternion.identity;
            t.position = Vector3.zero;
            size = KalyeahResources.RenderBounds(model).size;
            _naturalSizes[id] = size;
            return size;
        }

        /// <summary>
        /// Draws the kids where the sim has them: `previous` and `current` are two ticks, `alpha` how far between them this frame is (0..1),
        /// so the 30 Hz sim moves smoothly at any frame rate (the Godot game's TickBlend).
        /// </summary>
        public void Sync(IReadOnlyList<KidSnapshot> previous, IReadOnlyList<KidSnapshot> current, float alpha, float dt)
        {
            foreach (KidSnapshot kid in current)
            {
                if (!_kids.TryGetValue(kid.Id, out KidView view))
                {
                    view = new KidView(Root, kid, _camera.KidViewScale, TeamColor(kid.Team), _unlit);
                    _kids[kid.Id] = view;
                }
                SVector2 from = kid.Position;
                if (previous != null)
                    foreach (KidSnapshot before in previous)
                        if (before.Id == kid.Id) { from = before.Position; break; }
                // a tagged kid stays drawn: the sim has already put it on the sidewalk by its rescue area
                view.Place(SVector2.Lerp(from, kid.Position, Mathf.Clamp01(alpha)), kid.Facing);
                view.Animate(kid.Tagged ? 0f : kid.Speed01, dt);
            }
        }
    }
}
