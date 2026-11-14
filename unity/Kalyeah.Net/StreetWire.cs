using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Kalyeah.Sim;

namespace Kalyeah.Net
{
    /// <summary>
    /// A street game (Kalyeah.Sim's StreetMode) on the wire: its `Snapshot()` as Godot sends it (field names as StringNames, tables as
    /// typed `Dictionary[int, T]`, "flights" an untyped {int: int}), the way back into the exact C# types `Restore` wants, and its
    /// signals as EVENTS entries ("mode", snake_case name, args in Godot's types).
    /// </summary>
    public static class StreetWire
    {
        /// <summary>street_mode.gd `snapshot()` (and Tumbang Preso's "flights") in wire types.</summary>
        public static GDict Snapshot(StreetMode mode)
        {
            OrderedDict<string, object> data = mode.Snapshot();
            var output = new GDict();
            foreach (var pair in data)
            {
                // synced fields are StringNames in GDScript; "flights" is a String key holding an untyped dictionary
                object key = pair.Key == "flights" ? (object)"flights" : new StringName(pair.Key);
                output[key] = ToWire(pair.Value, pair.Key != "flights");
            }
            return output;
        }

        static object ToWire(object value, bool typedTable)
        {
            switch (value)
            {
                case int i: return (long)i;
                case Enum e: return Convert.ToInt64(e);
                case double _:
                case bool _:
                case Vec2 _:
                case string _:
                    return value;
            }
            Type type = value.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(OrderedDict<,>))
            {
                Type valueType = type.GetGenericArguments()[1];
                var table = typedTable ? new GDict(VariantType.Int, WireType(valueType)) : new GDict();
                foreach (object entry in (IEnumerable)value)
                {
                    Type entryType = entry.GetType();
                    object k = entryType.GetProperty("Key").GetValue(entry);
                    object v = entryType.GetProperty("Value").GetValue(entry);
                    table[ToWire(k, false)] = ToWire(v, false);
                }
                return table;
            }
            if (value is IEnumerable<int> ids)
            {
                var array = new GArray();
                foreach (int id in ids) array.Add((long)id);
                return array;
            }
            throw new ArgumentException("no wire form for " + type.Name);
        }

        static VariantType WireType(Type t)
        {
            if (t == typeof(bool)) return VariantType.Bool;
            if (t == typeof(int) || t.IsEnum) return VariantType.Int;
            if (t == typeof(double)) return VariantType.Float;
            if (t == typeof(Vec2)) return VariantType.Vector2;
            if (t == typeof(string)) return VariantType.StringName;
            return VariantType.Nil;
        }

        /// <summary>
        /// Wire data back into what `mode.Restore` accepts: every field of the game's own snapshot converted to its C# type (a value of
        /// another type is left out, as street_mode.gd skips it), "flights" as OrderedDict&lt;int, int&gt;, "unseen" as a List&lt;int&gt;.
        /// </summary>
        public static OrderedDict<string, object> FromWire(StreetMode mode, GDict data)
        {
            var output = new OrderedDict<string, object>();
            OrderedDict<string, object> shape = mode.Snapshot();
            foreach (var pair in shape)
            {
                if (!data.TryGet(pair.Key, out object value)) continue;
                if (FromWire(value, pair.Value.GetType(), out object converted)) output[pair.Key] = converted;
            }
            if (data.Get("unseen") is GArray unseen)
            {
                var ids = new List<int>();
                foreach (object id in unseen)
                    if (Wire.IsInt(id)) ids.Add((int)Wire.AsLong(id));
                output["unseen"] = ids;
            }
            return output;
        }

        static bool FromWire(object value, Type type, out object result)
        {
            result = null;
            if (type == typeof(int)) { if (Wire.IsInt(value)) result = (int)Wire.AsLong(value); }
            else if (type.IsEnum) { if (Wire.IsInt(value)) result = Enum.ToObject(type, Wire.AsLong(value)); }
            else if (type == typeof(double)) { if (Wire.IsFloat(value)) result = Wire.Number(value); }
            else if (type == typeof(bool)) { if (value is bool) result = value; }
            else if (type == typeof(Vec2)) { if (value is Vec2) result = value; }
            else if (type == typeof(string)) { if (Wire.IsStringLike(value)) result = Wire.Text(value); }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(OrderedDict<,>) && value is GDict table)
            {
                // the game's own table type (already instantiated by the sim), filled through its indexer
                object fresh = Activator.CreateInstance(type);
                PropertyInfo item = type.GetProperty("Item");
                Type valueType = type.GetGenericArguments()[1];
                foreach (var pair in table)
                {
                    if (!Wire.IsInt(pair.Key)) continue;
                    if (FromWire(pair.Value, valueType, out object v)) item.SetValue(fresh, v, new object[] { (int)Wire.AsLong(pair.Key) });
                }
                result = fresh;
            }
            return result != null;
        }

        /// <summary>Langit Lupa's `filter_for` on wire data: the hidden kids' mounds are left out.</summary>
        public static GDict FilterFor(StreetMode mode, GDict data, List<int> hidden)
        {
            if (!(mode is LangitLupa)) return data;
            GDict output = data.Duplicate();
            foreach (string field in new[] { "mounds", "mound_left" })
            {
                GDict table = output.Get(field) is GDict t ? t.Duplicate() : new GDict();
                foreach (int id in hidden) table.Remove((long)id);
                output[field] = table;
            }
            return output;
        }

        /// <summary>Subscribes `push(name, args)` to every signal of `mode` (street_mode.gd's and its game's); returns the unsubscribe.</summary>
        public static Action Relay(StreetMode mode, Action<string, GArray> push)
        {
            var off = new List<Action>();
            Action<int, int> tagged = (a, b) => push("tagged", GArray.Of((long)a, (long)b));
            Action<int, int> scored = (a, b) => push("scored", GArray.Of((long)a, (long)b));
            Action<int> swapped = a => push("roles_swapped", GArray.Of((long)a));
            mode.Tagged += tagged;
            mode.Scored += scored;
            mode.RolesSwapped += swapped;
            off.Add(() =>
            {
                mode.Tagged -= tagged;
                mode.Scored -= scored;
                mode.RolesSwapped -= swapped;
            });
            switch (mode)
            {
                case TumbangPreso tp:
                {
                    Action<int> thrown = a => push("slipper_thrown", GArray.Of((long)a));
                    Action<int, Vec2, bool> landed = (a, at, hit) => push("slipper_landed", GArray.Of((long)a, at, hit));
                    Action<Vec2, List<Vec2>> canFlung = (from, path) => push("can_flung", GArray.Of(from, Path(path)));
                    Action<int, Vec2, List<Vec2>> slipperFlung = (a, from, path) => push("slipper_flung", GArray.Of((long)a, from, Path(path)));
                    Action<int> kicked = a => push("can_kicked", GArray.Of((long)a));
                    Action<int> fetched = a => push("slipper_fetched", GArray.Of((long)a));
                    Action<int> placed = a => push("can_placed", GArray.Of((long)a));
                    Action<int, int, string> taya = (a, b, why) => push("taya_changed", GArray.Of((long)a, (long)b, new StringName(why)));
                    tp.SlipperThrown += thrown;
                    tp.SlipperLanded += landed;
                    tp.CanFlung += canFlung;
                    tp.SlipperFlung += slipperFlung;
                    tp.CanKicked += kicked;
                    tp.SlipperFetched += fetched;
                    tp.CanPlaced += placed;
                    tp.TayaChanged += taya;
                    off.Add(() =>
                    {
                        tp.SlipperThrown -= thrown;
                        tp.SlipperLanded -= landed;
                        tp.CanFlung -= canFlung;
                        tp.SlipperFlung -= slipperFlung;
                        tp.CanKicked -= kicked;
                        tp.SlipperFetched -= fetched;
                        tp.CanPlaced -= placed;
                        tp.TayaChanged -= taya;
                    });
                    break;
                }
                case Patintero pt:
                {
                    Action<int> crossed = a => push("runner_crossed", GArray.Of((long)a));
                    Action<int, Vec2, double, double> dived = (a, way, time, recover) => push("dived", GArray.Of((long)a, way, time, recover));
                    Action<int, int> jailed = (a, side) => push("jailed_runner", GArray.Of((long)a, (long)side));
                    pt.RunnerCrossed += crossed;
                    pt.Dived += dived;
                    pt.JailedRunner += jailed;
                    off.Add(() =>
                    {
                        pt.RunnerCrossed -= crossed;
                        pt.Dived -= dived;
                        pt.JailedRunner -= jailed;
                    });
                    break;
                }
                case LangitLupa ll:
                {
                    Action<int> raised = a => push("mound_raised", GArray.Of((long)a));
                    Action<int> fell = a => push("mound_fell", GArray.Of((long)a));
                    Action<int> broke = a => push("spot_broke", GArray.Of((long)a));
                    Action<int, int> taya = (a, b) => push("taya_changed", GArray.Of((long)a, (long)b));
                    ll.MoundRaised += raised;
                    ll.MoundFell += fell;
                    ll.SpotBroke += broke;
                    ll.TayaChanged += taya;
                    off.Add(() =>
                    {
                        ll.MoundRaised -= raised;
                        ll.MoundFell -= fell;
                        ll.SpotBroke -= broke;
                        ll.TayaChanged -= taya;
                    });
                    break;
                }
            }
            return () =>
            {
                foreach (Action a in off) a();
            };
        }

        static GArray Path(List<Vec2> path)
        {
            var array = new GArray(VariantType.Vector2);
            foreach (Vec2 point in path) array.Add(point);
            return array;
        }
    }
}
