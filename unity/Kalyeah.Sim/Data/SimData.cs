using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Kalyeah.Sim
{
    /// <summary>
    /// Loads the game's data (unity/Data/*.json, exported from the Godot resources under data/ by tools/unity_export/export.gd) into the
    /// sim's data classes. Each JSON property (snake_case) fills the C# field of the same name in PascalCase; `$ref` links to another
    /// resource resolve to the same shared instance (a weapon is one object, like a loaded Godot resource).
    /// Classes the sim does not model (CameraRules, NetRules) stay available as raw JSON through <see cref="Raw"/>.
    /// </summary>
    public sealed class SimData
    {
        static readonly Dictionary<string, Type> Classes = new Dictionary<string, Type>
        {
            { "GameRules", typeof(GameRules) },
            { "WeaponDef", typeof(WeaponDef) },
            { "CharacterDef", typeof(CharacterDef) },
            { "BotLevel", typeof(BotLevel) },
            { "MapLayout", typeof(MapLayout) },
            { "StreetModeRules", typeof(StreetModeRules) },
            { "StreetGames", typeof(StreetGames) },
            { "LarongKalye", typeof(LarongKalye) },
            { "DonateInfo", typeof(DonateInfo) },
        };

        readonly Dictionary<string, OrderedDict<string, object>> _files = new Dictionary<string, OrderedDict<string, object>>();
        readonly Dictionary<string, object> _loaded = new Dictionary<string, object>();

        /// <summary>JSON keys that matched no C# member while loading (a data/port mismatch; the tests keep it empty).</summary>
        public readonly List<string> UnmappedKeys = new List<string>();

        /// <summary>Reads every *.json in `directory` (the files from unity/Data).</summary>
        public static SimData Load(string directory)
        {
            var data = new SimData();
            foreach (string path in Directory.GetFiles(directory, "*.json"))
                data.AddFile(File.ReadAllText(path, Encoding.UTF8));
            return data;
        }

        /// <summary>Adds one exported file (`{"source", "class", "data"}`), e.g. from a Unity TextAsset.</summary>
        public void AddFile(string json)
        {
            var root = (OrderedDict<string, object>)MiniJson.Parse(json);
            _files[(string)root["source"]] = root;
        }

        /// <summary>All the resource paths loaded (res://data/...).</summary>
        public IEnumerable<string> Sources => _files.Keys;

        /// <summary>The raw `data` object of resource `source` (res://data/...), for classes the sim does not model.</summary>
        public OrderedDict<string, object> Raw(string source) => (OrderedDict<string, object>)_files[source]["data"];

        /// <summary>The Godot class of resource `source`.</summary>
        public string ClassOf(string source) => (string)_files[source]["class"];

        /// <summary>Resource `source` (res://data/...) as its C# class (shared instance).</summary>
        public T Get<T>(string source) where T : class => (T)Resolve(source);

        public GameRules GameRules => Get<GameRules>("res://data/rules/game_rules.tres");
        public MapLayout MapLayout => Get<MapLayout>("res://data/rules/map_layout.tres");
        public StreetGames StreetGames => Get<StreetGames>("res://data/rules/street_games.tres");
        public LarongKalye LarongKalye => Get<LarongKalye>("res://data/rules/larong_kalye.tres");
        public DonateInfo DonateInfo => Get<DonateInfo>("res://data/rules/donate_info.tres");

        /// <summary>A street game's rules by id (tumbang_preso, patintero, langit_lupa), or null (StreetMode.rules_for).</summary>
        public StreetModeRules StreetModeRules(string id)
        {
            string source = "res://data/rules/" + id + ".tres";
            return _files.ContainsKey(source) && ClassOf(source) == "StreetModeRules" ? Get<StreetModeRules>(source) : null;
        }

        /// <summary>A weapon by file name (data/weapons/{name}.tres).</summary>
        public WeaponDef Weapon(string name) => Get<WeaponDef>("res://data/weapons/" + name + ".tres");

        object Resolve(string source)
        {
            if (_loaded.TryGetValue(source, out object done)) return done;
            if (!_files.TryGetValue(source, out var file)) throw new KeyNotFoundException("SimData: no resource " + source);
            string cls = (string)file["class"];
            if (!Classes.TryGetValue(cls, out Type type)) throw new InvalidOperationException($"SimData: {source} is a {cls}, not a sim class");
            object instance = Activator.CreateInstance(type);
            _loaded[source] = instance;
            Fill(instance, (OrderedDict<string, object>)file["data"], source);
            return instance;
        }

        void Fill(object target, OrderedDict<string, object> obj, string where)
        {
            Type type = target.GetType();
            foreach (var pair in obj)
            {
                if (pair.Key == "$class") continue;
                FieldInfo field = type.GetField(Pascal(pair.Key), BindingFlags.Public | BindingFlags.Instance);
                if (field == null)
                {
                    UnmappedKeys.Add(where + ":" + pair.Key);
                    continue;
                }
                field.SetValue(target, Convert(pair.Value, field.FieldType, where + ":" + pair.Key));
            }
        }

        object Convert(object value, Type to, string where)
        {
            if (to == typeof(double)) return MiniJson.Num(value);
            if (to == typeof(float)) return (float)MiniJson.Num(value);
            if (to == typeof(int)) return (int)MiniJson.Num(value);
            if (to == typeof(long)) return (long)MiniJson.Num(value);
            if (to == typeof(bool)) return value is bool b ? b : MiniJson.Num(value) != 0.0;
            if (to == typeof(string)) return value == null ? "" : value is string s ? s : System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            if (to.IsEnum) return Enum.ToObject(to, (int)MiniJson.Num(value));
            if (to == typeof(Vec2))
            {
                var o = (OrderedDict<string, object>)value;
                return new Vec2(MiniJson.Num(o["x"]), MiniJson.Num(o["y"]));
            }
            if (to == typeof(Vec2I))
            {
                var o = (OrderedDict<string, object>)value;
                return new Vec2I((int)MiniJson.Num(o["x"]), (int)MiniJson.Num(o["y"]));
            }
            if (to == typeof(Rect2))
            {
                var o = (OrderedDict<string, object>)value;
                return new Rect2(MiniJson.Num(o["x"]), MiniJson.Num(o["y"]), MiniJson.Num(o["w"]), MiniJson.Num(o["h"]));
            }
            if (to == typeof(GColor))
            {
                var o = (OrderedDict<string, object>)value;
                return new GColor(MiniJson.Num(o["r"]), MiniJson.Num(o["g"]), MiniJson.Num(o["b"]), MiniJson.Num(o["a"]));
            }
            if (to == typeof(OrderedDict<string, object>)) return value;
            if (to == typeof(OrderedDict<string, string>))
            {
                var result = new OrderedDict<string, string>();
                foreach (var pair in (OrderedDict<string, object>)value) result[pair.Key] = (string)Convert(pair.Value, typeof(string), where);
                return result;
            }
            if (to.IsGenericType && to.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type item = to.GetGenericArguments()[0];
                var list = (IList)Activator.CreateInstance(to);
                if (value != null)
                    foreach (object element in (List<object>)value) list.Add(Convert(element, item, where));
                return list;
            }
            if (Classes.ContainsValue(to))
            {
                if (value == null) return null;
                var o = (OrderedDict<string, object>)value;
                if (o.TryGetValue("$ref", out object reference)) return Resolve((string)reference);
                object inline = Activator.CreateInstance(to);
                Fill(inline, o, where);
                return inline;
            }
            throw new InvalidOperationException($"SimData: cannot read {where} as {to.Name}");
        }

        /// <summary>snake_case to PascalCase (the port's naming rule).</summary>
        public static string Pascal(string snake)
        {
            var sb = new StringBuilder(snake.Length);
            bool upper = true;
            foreach (char c in snake)
            {
                if (c == '_')
                {
                    upper = true;
                    continue;
                }
                sb.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            return sb.ToString();
        }
    }
}
