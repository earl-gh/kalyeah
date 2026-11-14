using System;
using System.Collections;
using System.Collections.Generic;
using Kalyeah.Sim;

namespace Kalyeah.Net
{
    /// <summary>Godot 4's Variant type ids (the low byte of every encoded value's header).</summary>
    public enum VariantType
    {
        Nil = 0, Bool = 1, Int = 2, Float = 3, String = 4, Vector2 = 5, Vector2I = 6, Rect2 = 7, Rect2I = 8, Vector3 = 9, Vector3I = 10,
        Transform2D = 11, Vector4 = 12, Vector4I = 13, Plane = 14, Quaternion = 15, Aabb = 16, Basis = 17, Transform3D = 18, Projection = 19,
        Color = 20, StringName = 21, NodePath = 22, Rid = 23, Object = 24, Callable = 25, Signal = 26, Dictionary = 27, Array = 28,
        PackedByteArray = 29, PackedInt32Array = 30, PackedInt64Array = 31, PackedFloat32Array = 32, PackedFloat64Array = 33,
        PackedStringArray = 34, PackedVector2Array = 35, PackedVector3Array = 36, PackedColorArray = 37, PackedVector4Array = 38,
    }

    /// <summary>
    /// A Godot StringName on the wire (type 21). It is a different type from a String in the bytes, so the port keeps the two apart
    /// wherever GDScript does (`&amp;"street"` vs `"street"`); as a dictionary key it equals the same String, as in Godot.
    /// </summary>
    public sealed class StringName : IEquatable<StringName>
    {
        public static readonly StringName Empty = new StringName("");
        public readonly string Value;

        public StringName(string value) { Value = value ?? ""; }

        public bool IsEmpty => Value.Length == 0;
        public bool Equals(StringName other) => other is object && other.Value == Value;
        public override bool Equals(object obj) => obj is StringName s && s.Value == Value;
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value;
        public static bool operator ==(StringName a, StringName b) => a is null ? b is null : a.Equals(b);
        public static bool operator !=(StringName a, StringName b) => !(a == b);
    }

    /// <summary>
    /// A value of a fixed-size Godot type the protocol never builds itself (Rect2, Vector3, Color...): kept as its encoded bytes so a
    /// message that carries one decodes and re-encodes unchanged.
    /// </summary>
    public sealed class GOpaque
    {
        public readonly VariantType Type;
        public readonly int Flags;
        public readonly byte[] Payload;

        public GOpaque(VariantType type, int flags, byte[] payload) { Type = type; Flags = flags; Payload = payload; }
        public override bool Equals(object obj) => obj is GOpaque o && o.Type == Type && o.Flags == Flags && ((IStructuralEquatable)o.Payload).Equals(Payload, StructuralComparisons.StructuralEqualityComparer);
        public override int GetHashCode() => (int)Type * 31 + Payload.Length;
    }

    /// <summary>
    /// A Godot Array. `ElementType` is the type of a typed array (`Array[int]`, Nil = untyped), which is part of the bytes; `Packed` is
    /// set for a Packed*Array (then the elements are long, double, string or Vec2). Elements use the wire's C# types: null, bool,
    /// long, double, string, StringName, Vec2, Vec2I, GArray, GDict, GOpaque (an int or float given when building is fine too).
    /// </summary>
    public sealed class GArray : List<object>
    {
        public VariantType ElementType;
        public VariantType Packed;

        public GArray() { }
        public GArray(VariantType elementType) { ElementType = elementType; }
        public GArray(IEnumerable<object> items, VariantType elementType = VariantType.Nil) : base(items) { ElementType = elementType; }

        public static GArray Of(params object[] items) => new GArray(items);
        public static GArray Typed(VariantType type, params object[] items) => new GArray(items, type);

        /// <summary>Array[StringName] of `ids`.</summary>
        public static GArray StringNames(IEnumerable<string> ids)
        {
            var array = new GArray(VariantType.StringName);
            foreach (string id in ids) array.Add(new StringName(id));
            return array;
        }

        /// <summary>Array[int] of `values`.</summary>
        public static GArray Ints(IEnumerable<int> values)
        {
            var array = new GArray(VariantType.Int);
            foreach (int v in values) array.Add((long)v);
            return array;
        }

        /// <summary>Array[float] of `values`.</summary>
        public static GArray Floats(IEnumerable<double> values)
        {
            var array = new GArray(VariantType.Float);
            foreach (double v in values) array.Add(v);
            return array;
        }

        /// <summary>A shallow copy that keeps the type (GDScript `duplicate()`).</summary>
        public GArray Duplicate() => new GArray(this, ElementType) { Packed = Packed };
    }

    /// <summary>
    /// A Godot Dictionary: insertion-ordered, optionally typed (`Dictionary[int, int]`, part of the bytes). Keys compare like Godot's:
    /// int and long are one type, a String equals the same StringName.
    /// </summary>
    public sealed class GDict : IEnumerable<KeyValuePair<object, object>>
    {
        public VariantType KeyType;
        public VariantType ValueType;

        readonly List<object> _keys = new List<object>();
        readonly List<object> _values = new List<object>();
        readonly Dictionary<object, int> _index = new Dictionary<object, int>(KeyComparer.Instance);

        public GDict() { }
        public GDict(VariantType keyType, VariantType valueType) { KeyType = keyType; ValueType = valueType; }

        public int Count => _keys.Count;
        public bool IsEmpty => _keys.Count == 0;
        public IReadOnlyList<object> Keys => _keys;
        public IReadOnlyList<object> Values => _values;

        /// <summary>Reads null for a missing key; writing adds the key at the end (or replaces its value in place).</summary>
        public object this[object key]
        {
            get => _index.TryGetValue(key, out int i) ? _values[i] : null;
            set
            {
                if (_index.TryGetValue(key, out int i))
                {
                    _values[i] = value;
                    return;
                }
                _index[key] = _keys.Count;
                _keys.Add(Wire.Normalize(key));
                _values.Add(value);
            }
        }

        public bool Has(object key) => _index.ContainsKey(key);

        /// <summary>GDScript `d.get(key, fallback)`.</summary>
        public object Get(object key, object fallback = null) => _index.TryGetValue(key, out int i) ? _values[i] : fallback;

        public bool TryGet(object key, out object value)
        {
            if (_index.TryGetValue(key, out int i))
            {
                value = _values[i];
                return true;
            }
            value = null;
            return false;
        }

        public bool Remove(object key)
        {
            if (!_index.TryGetValue(key, out int i)) return false;
            _keys.RemoveAt(i);
            _values.RemoveAt(i);
            _index.Clear();
            for (int k = 0; k < _keys.Count; k++) _index[_keys[k]] = k;
            return true;
        }

        public void Clear()
        {
            _keys.Clear();
            _values.Clear();
            _index.Clear();
        }

        /// <summary>A shallow copy that keeps the order and the type (GDScript `duplicate()`).</summary>
        public GDict Duplicate()
        {
            var copy = new GDict(KeyType, ValueType);
            for (int i = 0; i < _keys.Count; i++) copy[_keys[i]] = _values[i];
            return copy;
        }

        /// <summary>GDScript `merge(other)`: adds the keys this one does not have (overwrite = false).</summary>
        public void Merge(GDict other, bool overwrite = false)
        {
            foreach (var pair in other)
                if (overwrite || !Has(pair.Key)) this[pair.Key] = pair.Value;
        }

        /// <summary>Builds a dictionary from key, value, key, value...</summary>
        public static GDict Of(params object[] keysAndValues)
        {
            var d = new GDict();
            for (int i = 0; i + 1 < keysAndValues.Length; i += 2) d[keysAndValues[i]] = keysAndValues[i + 1];
            return d;
        }

        // ---- GDScript-style reads (the `_int_in`, `_float_in`... helpers of snapshot.gd / game_client.gd) ----

        /// <summary>An int (a float is truncated, as `int(v)`), else `fallback`.</summary>
        public long GetLong(object key, long fallback = 0)
        {
            object v = Get(key);
            if (Wire.IsInt(v)) return Wire.AsLong(v);
            if (v is double d) return Wire.TruncateToLong(d);
            if (v is float f) return Wire.TruncateToLong(f);
            return fallback;
        }

        public int GetInt(object key, int fallback = 0) => (int)GetLong(key, fallback);

        /// <summary>A float, or an int as a float, else `fallback`.</summary>
        public double GetFloat(object key, double fallback = 0.0)
        {
            object v = Get(key);
            if (v is double d) return d;
            if (v is float f) return f;
            if (Wire.IsInt(v)) return Wire.AsLong(v);
            return fallback;
        }

        public bool GetBool(object key, bool fallback = false) => Get(key) is bool b ? b : fallback;

        /// <summary>A String, or a StringName as its text, else `fallback`.</summary>
        public string GetStr(object key, string fallback = "")
        {
            object v = Get(key);
            if (v is string s) return s;
            if (v is StringName n) return n.Value;
            return fallback;
        }

        public Vec2 GetVec2(object key, Vec2 fallback) => Get(key) is Vec2 v ? v : fallback;
        public GDict GetDict(object key) => Get(key) as GDict ?? new GDict();
        public GArray GetArr(object key) => Get(key) as GArray ?? new GArray();

        public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
        {
            for (int i = 0; i < _keys.Count; i++) yield return new KeyValuePair<object, object>(_keys[i], _values[i]);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Godot's key comparison: ints of any C# width are one, a String equals the StringName of the same text.</summary>
        sealed class KeyComparer : IEqualityComparer<object>
        {
            public static readonly KeyComparer Instance = new KeyComparer();

            public new bool Equals(object a, object b)
            {
                if (Wire.IsInt(a) && Wire.IsInt(b)) return Wire.AsLong(a) == Wire.AsLong(b);
                if (Wire.IsStringLike(a) && Wire.IsStringLike(b)) return Wire.Text(a) == Wire.Text(b);
                if (a is GArray || a is GDict) return ReferenceEquals(a, b);
                return object.Equals(a, b);
            }

            public int GetHashCode(object o)
            {
                if (o == null) return 0;
                if (Wire.IsInt(o)) return Wire.AsLong(o).GetHashCode();
                if (Wire.IsStringLike(o)) return Wire.Text(o).GetHashCode();
                if (o is GArray || o is GDict) return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
                return o.GetHashCode();
            }
        }
    }

    /// <summary>Small helpers for wire values (GDScript's `is int`, `is String`...).</summary>
    public static class Wire
    {
        /// <summary>GDScript `v is int` (any C# integer the port uses).</summary>
        public static bool IsInt(object v) => v is long || v is int;
        public static long AsLong(object v) => v is long l ? l : (int)v;
        public static bool IsFloat(object v) => v is double || v is float;
        public static bool IsNumber(object v) => IsInt(v) || IsFloat(v);

        /// <summary>`float(v)` for an int or a float, else 0 (snapshot.gd `_number`).</summary>
        public static double Number(object v) => v is double d ? d : v is float f ? f : IsInt(v) ? AsLong(v) : 0.0;

        public static bool IsStringLike(object v) => v is string || v is StringName;
        public static string Text(object v) => v is string s ? s : v is StringName n ? n.Value : null;

        /// <summary>A StringName for a StringName or a String, else `fallback` (game_client.gd `_sname_val`).</summary>
        public static StringName SName(object v, StringName fallback = null) =>
            v is StringName n ? n : v is string s ? new StringName(s) : (fallback ?? StringName.Empty);

        /// <summary>GDScript `int(float)`: truncation toward zero, saturating like Godot's double-to-int64.</summary>
        public static long TruncateToLong(double d)
        {
            if (double.IsNaN(d)) return long.MinValue;
            if (d >= 9.2233720368547758e18) return long.MaxValue;
            if (d <= -9.2233720368547758e18) return long.MinValue;
            return (long)d;
        }

        internal static object Normalize(object key) => key is int i ? (long)i : key;

        /// <summary>Godot's `snappedf(value, step)`.</summary>
        public static double Snapped(double value, double step) => step != 0.0 ? Math.Floor(value / step + 0.5) * step : value;

        public static Vec2 SnapVec(Vec2 v, double step = 0.01) => new Vec2(Snapped(v.x, step), Snapped(v.y, step));

        /// <summary>Lower-case hex, like PackedByteArray.hex_encode().</summary>
        public static string Hex(byte[] bytes)
        {
            var chars = new char[bytes.Length * 2];
            const string digits = "0123456789abcdef";
            for (int i = 0; i < bytes.Length; i++)
            {
                chars[i * 2] = digits[bytes[i] >> 4];
                chars[i * 2 + 1] = digits[bytes[i] & 15];
            }
            return new string(chars);
        }

        public static byte[] FromHex(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
    }
}
