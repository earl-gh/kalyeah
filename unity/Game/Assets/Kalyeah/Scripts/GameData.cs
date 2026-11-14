using System;
using System.Collections.Generic;
using System.Numerics;

namespace Kalyeah.Game
{
    /// <summary>
    /// Reads unity/Data/*.json (every Godot resource under data/, exported by tools/unity_export/export.gd). A file is named after its
    /// res:// path: res://data/rules/camera_rules.tres is "rules__camera_rules". The view reads the numbers it needs to draw (camera,
    /// map geometry for the scenery); the rules themselves are the sim's (SimBridge hands the same reader to it).
    /// </summary>
    public sealed class DataSource
    {
        readonly Func<string, string> _read;
        readonly Dictionary<string, Dictionary<string, object>> _cache = new Dictionary<string, Dictionary<string, object>>();

        /// <param name="read">Returns the JSON text of a data file by its name without ".json" (for example "rules__game_rules"), or null.</param>
        public DataSource(Func<string, string> read) { _read = read ?? throw new ArgumentNullException(nameof(read)); }

        /// <summary>The raw text of a data file (what the sim's own loader is given).</summary>
        public string Text(string name) => _read(name) ?? throw new KeyNotFoundException($"no data file {name}.json (run tools/unity_export/sync_assets.sh)");

        /// <summary>The "data" object of a data file (its properties).</summary>
        public Dictionary<string, object> Props(string name)
        {
            if (!_cache.TryGetValue(name, out var props))
            {
                var root = (Dictionary<string, object>)MiniJson.Parse(Text(name));
                props = (Dictionary<string, object>)root["data"];
                _cache[name] = props;
            }
            return props;
        }

        /// <summary>The ids of GameRules.characters, in the rules' order (the ten kids).</summary>
        public string[] CharacterIds()
        {
            var refs = (List<object>)Props("rules__game_rules")["characters"];
            var ids = new string[refs.Count];
            for (int i = 0; i < refs.Count; i++) ids[i] = S(Props(NameOfRef(refs[i])), "id");
            return ids;
        }

        /// <summary>"res://data/weapons/bola.tres" as a data file name: "weapons__bola".</summary>
        public static string NameOfResPath(string resPath)
        {
            const string prefix = "res://data/";
            string path = resPath.StartsWith(prefix, StringComparison.Ordinal) ? resPath.Substring(prefix.Length) : resPath;
            if (path.EndsWith(".tres", StringComparison.Ordinal)) path = path.Substring(0, path.Length - ".tres".Length);
            return path.Replace("/", "__");
        }

        /// <summary>The data file a {"$ref": "res://..."} entry points at.</summary>
        public static string NameOfRef(object entry) => NameOfResPath((string)((Dictionary<string, object>)entry)["$ref"]);

        public static float F(Dictionary<string, object> props, string key) => Convert.ToSingle(props[key], System.Globalization.CultureInfo.InvariantCulture);

        public static int I(Dictionary<string, object> props, string key) => Convert.ToInt32(props[key], System.Globalization.CultureInfo.InvariantCulture);

        public static bool B(Dictionary<string, object> props, string key) => (bool)props[key];

        public static string S(Dictionary<string, object> props, string key) => (string)props[key];

        public static Vector2 V2(object value)
        {
            var d = (Dictionary<string, object>)value;
            return new Vector2(Convert.ToSingle(d["x"]), Convert.ToSingle(d["y"]));
        }

        public static GroundRect R(object value)
        {
            var d = (Dictionary<string, object>)value;
            return new GroundRect(Convert.ToSingle(d["x"]), Convert.ToSingle(d["y"]), Convert.ToSingle(d["w"]), Convert.ToSingle(d["h"]));
        }

        public static float[] Floats(object value)
        {
            var list = (List<object>)value;
            var result = new float[list.Count];
            for (int i = 0; i < list.Count; i++) result[i] = Convert.ToSingle(list[i]);
            return result;
        }

        public static string[] Strings(object value)
        {
            var list = (List<object>)value;
            var result = new string[list.Count];
            for (int i = 0; i < list.Count; i++) result[i] = (string)list[i];
            return result;
        }
    }

    /// <summary>Godot's Rect2 on the ground plane (x, z): position and size, in metres.</summary>
    public readonly struct GroundRect
    {
        public readonly float X, Y, W, H;

        public GroundRect(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }

        public Vector2 Center => new Vector2(X + W * 0.5f, Y + H * 0.5f);
        public Vector2 End => new Vector2(X + W, Y + H);

        /// <summary>The point-symmetric mirror (Godot: Rect2(-rect.end, rect.size)), how the enemy half repeats the own half.</summary>
        public GroundRect Mirrored => new GroundRect(-(X + W), -(Y + H), W, H);

        public bool Contains(Vector2 p) => p.X >= X && p.X <= X + W && p.Y >= Y && p.Y <= Y + H;
    }

    /// <summary>data/rules/camera_rules.tres: the shoulder camera's numbers (docs/GDD.md "Shoulder view").</summary>
    public sealed class CameraRulesData
    {
        public float ShoulderHeight, ShoulderDistance, ShoulderOffset, ShoulderFov, ShoulderNear;
        public float LookPitchDegrees, LookPitchMin, LookPitchMax, LookSensitivity, KidViewScale;

        public static CameraRulesData Load(DataSource data)
        {
            var p = data.Props("rules__camera_rules");
            return new CameraRulesData
            {
                ShoulderHeight = DataSource.F(p, "shoulder_height"),
                ShoulderDistance = DataSource.F(p, "shoulder_distance"),
                ShoulderOffset = DataSource.F(p, "shoulder_offset"),
                ShoulderFov = DataSource.F(p, "shoulder_fov"),
                ShoulderNear = DataSource.F(p, "shoulder_near"),
                LookPitchDegrees = DataSource.F(p, "look_pitch_degrees"),
                LookPitchMin = DataSource.F(p, "look_pitch_min"),
                LookPitchMax = DataSource.F(p, "look_pitch_max"),
                LookSensitivity = DataSource.F(p, "look_sensitivity"),
                KidViewScale = DataSource.F(p, "kid_view_scale"),
            };
        }

        public float ClampPitch(float pitch) => Math.Clamp(pitch, LookPitchMin, LookPitchMax);
    }

    /// <summary>
    /// data/rules/map_layout.tres, the geometry the scenery is drawn from: the same formulas as scripts/sim/map_layout.gd for the circular
    /// barangay (bases, posts, rescue sheds, building parcels mirrored point-symmetrically to the enemy half). View-side copy: the sim
    /// keeps its own.
    /// </summary>
    public sealed class MapLayoutData
    {
        public const int SideOwn = 1;
        public const int SideEnemy = -1;

        public float ArenaRadius, BaseInset, BaseRadius, BaseCorner, SpawnOffset, BoundaryThickness;
        public float CoverHeight, PostHeight, PostWidthShare, SceneryScale;
        public Vector2 RescueSize;
        public GroundRect[] Obstacles;
        public float[] ObstacleYawsDeg;
        public float[] ObstacleScales;
        public string[] ObstacleProps;
        public Vector2[] PowerupSpots;

        public static MapLayoutData Load(DataSource data)
        {
            var p = data.Props("rules__map_layout");
            var obstacles = (List<object>)p["obstacles"];
            var spots = (List<object>)p["powerup_spots"];
            var layout = new MapLayoutData
            {
                ArenaRadius = DataSource.F(p, "arena_radius"),
                BaseInset = DataSource.F(p, "base_inset"),
                BaseRadius = DataSource.F(p, "base_radius"),
                BaseCorner = DataSource.F(p, "base_corner"),
                SpawnOffset = DataSource.F(p, "spawn_offset"),
                BoundaryThickness = DataSource.F(p, "boundary_thickness"),
                CoverHeight = DataSource.F(p, "cover_height"),
                PostHeight = DataSource.F(p, "post_height"),
                PostWidthShare = DataSource.F(p, "post_width_share"),
                SceneryScale = DataSource.F(p, "scenery_scale"),
                RescueSize = DataSource.V2(p["rescue_size"]),
                Obstacles = new GroundRect[obstacles.Count],
                ObstacleYawsDeg = DataSource.Floats(p["obstacle_yaws_deg"]),
                ObstacleScales = DataSource.Floats(p["obstacle_scales"]),
                ObstacleProps = DataSource.Strings(p["obstacle_props"]),
                PowerupSpots = new Vector2[spots.Count],
            };
            for (int i = 0; i < obstacles.Count; i++) layout.Obstacles[i] = DataSource.R(obstacles[i]);
            for (int i = 0; i < spots.Count; i++) layout.PowerupSpots[i] = DataSource.V2(spots[i]);
            return layout;
        }

        /// <summary>Own-half parcels plus their point-symmetric mirrors on the enemy half.</summary>
        public int ParcelCount => Obstacles.Length * 2;

        public GroundRect ParcelRect(int index) => index < Obstacles.Length ? Obstacles[index] : Obstacles[index - Obstacles.Length].Mirrored;

        public bool HasOrientedParcels => ObstacleYawsDeg.Length > 0 && ObstacleYawsDeg.Length == Obstacles.Length;

        /// <summary>Godot yaw (radians) of a parcel; the mirrored half is turned half way round (map_layout.gd parcel_yaw_rad).</summary>
        public float ParcelYawRad(int index)
        {
            int half = Obstacles.Length;
            if (half == 0 || ObstacleYawsDeg.Length == 0) return 0f;
            int baseIdx = index % half;
            float rad = (baseIdx < ObstacleYawsDeg.Length ? ObstacleYawsDeg[baseIdx] : 0f) * MathF.PI / 180f;
            if (index >= half) rad = WrapPi(rad + MathF.PI);
            return rad;
        }

        public float ParcelScale(int index)
        {
            int half = Obstacles.Length;
            if (half == 0 || ObstacleScales.Length == 0) return 1f;
            int baseIdx = index % half;
            return baseIdx < ObstacleScales.Length ? ObstacleScales[baseIdx] : 1f;
        }

        public string ParcelProp(int index)
        {
            if (ObstacleProps.Length == 0 || Obstacles.Length == 0) return "bench";
            return ObstacleProps[(index % Obstacles.Length) % ObstacleProps.Length];
        }

        public Vector2 BaseCenter(int side) => new Vector2(0f, side * MathF.Max(ArenaRadius - BaseInset, 0f));

        public Vector2 PostPosition(int side)
        {
            float zCenter = MathF.Max(ArenaRadius - BaseInset, 0f);
            float postShift = MathF.Min(MathF.Max(BaseCorner, 3.5f), MathF.Max(BaseRadius - 2f, 0f));
            return new Vector2(0f, side * (zCenter + postShift));
        }

        public Vector2 SpawnCenter(int side) => BaseCenter(side) - new Vector2(0f, side * SpawnOffset);

        public GroundRect RescueRect(int side)
        {
            float zMid = -side * (ArenaRadius * 0.375f);
            float zFar = MathF.Abs(zMid) + RescueSize.Y * 0.5f;
            float innerR = MathF.Max(ArenaRadius - 2.2f, RescueSize.X);
            float xEdge = MathF.Sqrt(MathF.Max(innerR * innerR - zFar * zFar, 0f));
            float left = side == SideOwn ? -xEdge : xEdge - RescueSize.X;
            return new GroundRect(left, zMid - RescueSize.Y * 0.5f, RescueSize.X, RescueSize.Y);
        }

        /// <summary>The creek's centre line z(x) (map_layout.gd canal_z).</summary>
        public static float CanalZ(float x) => 2.4f * MathF.Sin(x * 0.046f) + 0.8f * MathF.Sin(x * 0.105f);

        static float WrapPi(float a)
        {
            float twoPi = 2f * MathF.PI;
            a = (a + MathF.PI) % twoPi;
            if (a < 0f) a += twoPi;
            return a - MathF.PI;
        }
    }
}
