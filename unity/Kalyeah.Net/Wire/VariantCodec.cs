using System;
using System.IO;
using System.Text;
using Kalyeah.Sim;

namespace Kalyeah.Net
{
    /// <summary>
    /// Godot 4's binary Variant serialisation (`var_to_bytes` / `bytes_to_var` without objects, core/io/marshalls.cpp), which is what
    /// NetProtocol puts on the wire. Byte for byte the same as Godot 4.6 for every type the protocol uses, typed arrays and typed
    /// dictionaries included (checked against unity/Golden/Net). Little-endian; every value starts with a 4-byte header (type id in
    /// the low byte, flags above), strings are UTF-8 padded to 4 bytes.
    /// </summary>
    public static class VariantCodec
    {
        const int Flag64 = 1 << 16;
        const int TypedShift = 16;
        const int TypedBuiltin = 1;
        /// <summary>Godot's Variant::MAX_RECURSION_DEPTH.</summary>
        const int MaxDepth = 1024;

        public static byte[] Encode(object value)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) Write(writer, value, 0);
            return stream.ToArray();
        }

        /// <summary>`bytes_to_var` refusing objects: false (and null) for anything malformed. Trailing bytes are ignored, as in Godot.</summary>
        public static bool TryDecode(byte[] bytes, out object value)
        {
            value = null;
            if (bytes == null) return false;
            int pos = 0;
            try
            {
                return Read(bytes, ref pos, 0, out value);
            }
            catch (Exception e) when (e is ArgumentException || e is IndexOutOfRangeException || e is OverflowException)
            {
                value = null;
                return false;
            }
        }

        // ---- encoding --------------------------------------------------------------------------------------------------------

        static void Write(BinaryWriter w, object value, int depth)
        {
            if (depth > MaxDepth) throw new InvalidOperationException("too deep");
            switch (value)
            {
                case null:
                    w.Write((uint)VariantType.Nil);
                    break;
                case bool b:
                    w.Write((uint)VariantType.Bool);
                    w.Write(b ? 1u : 0u);
                    break;
                case int i:
                    WriteInt(w, i);
                    break;
                case long l:
                    WriteInt(w, l);
                    break;
                case float f:
                    WriteFloat(w, f);
                    break;
                case double d:
                    WriteFloat(w, d);
                    break;
                case string s:
                    w.Write((uint)VariantType.String);
                    WriteString(w, s);
                    break;
                case StringName n:
                    w.Write((uint)VariantType.StringName);
                    WriteString(w, n.Value);
                    break;
                case Vec2 v:
                    w.Write((uint)VariantType.Vector2);
                    w.Write(v.X);
                    w.Write(v.Y);
                    break;
                case Vec2I vi:
                    w.Write((uint)VariantType.Vector2I);
                    w.Write(vi.X);
                    w.Write(vi.Y);
                    break;
                case GOpaque o:
                    w.Write((uint)o.Type | (uint)o.Flags);
                    w.Write(o.Payload);
                    break;
                case GDict d:
                    WriteDict(w, d, depth);
                    break;
                case GArray a:
                    WriteArray(w, a, depth);
                    break;
                case Enum e:
                    WriteInt(w, Convert.ToInt64(e));
                    break;
                default:
                    throw new ArgumentException("not a wire value: " + value.GetType().Name);
            }
        }

        static void WriteInt(BinaryWriter w, long l)
        {
            if (l > int.MaxValue || l < int.MinValue)
            {
                w.Write((uint)VariantType.Int | Flag64);
                w.Write(l);
            }
            else
            {
                w.Write((uint)VariantType.Int);
                w.Write((int)l);
            }
        }

        static void WriteFloat(BinaryWriter w, double d)
        {
            float f = (float)d;
            if ((double)f != d)
            {
                w.Write((uint)VariantType.Float | Flag64);
                w.Write(d);
            }
            else
            {
                w.Write((uint)VariantType.Float);
                w.Write(f);
            }
        }

        static void WriteString(BinaryWriter w, string s)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(s);
            w.Write((uint)utf8.Length);
            w.Write(utf8);
            for (int pad = utf8.Length; pad % 4 != 0; pad++) w.Write((byte)0);
        }

        static void WriteDict(BinaryWriter w, GDict d, int depth)
        {
            uint header = (uint)VariantType.Dictionary;
            if (d.KeyType != VariantType.Nil) header |= TypedBuiltin << TypedShift;
            if (d.ValueType != VariantType.Nil) header |= TypedBuiltin << (TypedShift + 2);
            w.Write(header);
            if (d.KeyType != VariantType.Nil) w.Write((uint)d.KeyType);
            if (d.ValueType != VariantType.Nil) w.Write((uint)d.ValueType);
            w.Write((uint)d.Count);
            foreach (var pair in d)
            {
                Write(w, pair.Key, depth + 1);
                Write(w, pair.Value, depth + 1);
            }
        }

        static void WriteArray(BinaryWriter w, GArray a, int depth)
        {
            if (a.Packed != VariantType.Nil)
            {
                WritePacked(w, a);
                return;
            }
            uint header = (uint)VariantType.Array;
            if (a.ElementType != VariantType.Nil) header |= TypedBuiltin << TypedShift;
            w.Write(header);
            if (a.ElementType != VariantType.Nil) w.Write((uint)a.ElementType);
            w.Write((uint)a.Count);
            foreach (object item in a) Write(w, item, depth + 1);
        }

        static void WritePacked(BinaryWriter w, GArray a)
        {
            w.Write((uint)a.Packed);
            w.Write((uint)a.Count);
            switch (a.Packed)
            {
                case VariantType.PackedByteArray:
                    foreach (object item in a) w.Write((byte)Wire.AsLong(item));
                    for (int pad = a.Count; pad % 4 != 0; pad++) w.Write((byte)0);
                    break;
                case VariantType.PackedInt32Array:
                    foreach (object item in a) w.Write((int)Wire.AsLong(item));
                    break;
                case VariantType.PackedInt64Array:
                    foreach (object item in a) w.Write(Wire.AsLong(item));
                    break;
                case VariantType.PackedFloat32Array:
                    foreach (object item in a) w.Write((float)Wire.Number(item));
                    break;
                case VariantType.PackedFloat64Array:
                    foreach (object item in a) w.Write(Wire.Number(item));
                    break;
                case VariantType.PackedStringArray:
                    foreach (object item in a) WriteString(w, Wire.Text(item) ?? "");
                    break;
                case VariantType.PackedVector2Array:
                    foreach (object item in a)
                    {
                        var v = (Vec2)item;
                        w.Write(v.X);
                        w.Write(v.Y);
                    }
                    break;
                default:
                    throw new ArgumentException("packed type " + a.Packed);
            }
        }

        // ---- decoding --------------------------------------------------------------------------------------------------------

        static uint U32(byte[] b, ref int pos)
        {
            if (pos + 4 > b.Length) throw new ArgumentException("short");
            uint v = BitConverter.ToUInt32(b, pos);
            pos += 4;
            return v;
        }

        static void Need(byte[] b, int pos, long count)
        {
            if (count < 0 || pos + count > b.Length) throw new ArgumentException("short");
        }

        /// <summary>Fixed payload sizes of the types the protocol only passes through (real_t = float; Flag64 doubles them).</summary>
        static int FixedSize(VariantType type, bool wide)
        {
            int r = wide ? 8 : 4;
            switch (type)
            {
                case VariantType.Rect2: return 4 * r;
                case VariantType.Rect2I: return 16;
                case VariantType.Vector3: return 3 * r;
                case VariantType.Vector3I: return 12;
                case VariantType.Transform2D: return 6 * r;
                case VariantType.Vector4: return 4 * r;
                case VariantType.Vector4I: return 16;
                case VariantType.Plane: return 4 * r;
                case VariantType.Quaternion: return 4 * r;
                case VariantType.Aabb: return 6 * r;
                case VariantType.Basis: return 9 * r;
                case VariantType.Transform3D: return 12 * r;
                case VariantType.Projection: return 16 * r;
                case VariantType.Color: return 16;
                case VariantType.Rid: return 8;
                default: return -1;
            }
        }

        static bool Read(byte[] b, ref int pos, int depth, out object value)
        {
            value = null;
            if (depth > MaxDepth) return false;
            uint header = U32(b, ref pos);
            var type = (VariantType)(header & 0xFF);
            bool wide = (header & Flag64) != 0;
            switch (type)
            {
                case VariantType.Nil:
                    return true;
                case VariantType.Bool:
                    value = U32(b, ref pos) != 0;
                    return true;
                case VariantType.Int:
                    if (wide)
                    {
                        Need(b, pos, 8);
                        value = BitConverter.ToInt64(b, pos);
                        pos += 8;
                    }
                    else
                    {
                        value = (long)(int)U32(b, ref pos);
                    }
                    return true;
                case VariantType.Float:
                    if (wide)
                    {
                        Need(b, pos, 8);
                        value = BitConverter.ToDouble(b, pos);
                        pos += 8;
                    }
                    else
                    {
                        Need(b, pos, 4);
                        value = (double)BitConverter.ToSingle(b, pos);
                        pos += 4;
                    }
                    return true;
                case VariantType.String:
                    value = ReadString(b, ref pos);
                    return true;
                case VariantType.StringName:
                    value = new StringName(ReadString(b, ref pos));
                    return true;
                case VariantType.Vector2:
                    if (wide)
                    {
                        Need(b, pos, 16);
                        value = new Vec2(BitConverter.ToDouble(b, pos), BitConverter.ToDouble(b, pos + 8));
                        pos += 16;
                    }
                    else
                    {
                        Need(b, pos, 8);
                        value = new Vec2(BitConverter.ToSingle(b, pos), BitConverter.ToSingle(b, pos + 4));
                        pos += 8;
                    }
                    return true;
                case VariantType.Vector2I:
                    Need(b, pos, 8);
                    value = new Vec2I(BitConverter.ToInt32(b, pos), BitConverter.ToInt32(b, pos + 4));
                    pos += 8;
                    return true;
                case VariantType.Dictionary:
                    return ReadDict(b, ref pos, header, depth, out value);
                case VariantType.Array:
                    return ReadArray(b, ref pos, header, depth, out value);
                case VariantType.PackedByteArray:
                case VariantType.PackedInt32Array:
                case VariantType.PackedInt64Array:
                case VariantType.PackedFloat32Array:
                case VariantType.PackedFloat64Array:
                case VariantType.PackedStringArray:
                case VariantType.PackedVector2Array:
                    value = ReadPacked(b, ref pos, type);
                    return true;
                case VariantType.PackedVector3Array:
                case VariantType.PackedColorArray:
                case VariantType.PackedVector4Array:
                {
                    int start = pos;
                    uint count = U32(b, ref pos);
                    int each = type == VariantType.PackedVector3Array ? (wide ? 24 : 12) : 16;
                    Need(b, pos, (long)count * each);
                    pos += (int)count * each;
                    value = new GOpaque(type, (int)(header & ~0xFFu), Slice(b, start, pos - start));
                    return true;
                }
                default:
                {
                    int size = FixedSize(type, wide);
                    if (size < 0) return false; // objects, callables, signals, node paths: refused like bytes_to_var
                    Need(b, pos, size);
                    value = new GOpaque(type, (int)(header & ~0xFFu), Slice(b, pos, size));
                    pos += size;
                    return true;
                }
            }
        }

        static byte[] Slice(byte[] b, int start, int length)
        {
            var copy = new byte[length];
            Array.Copy(b, start, copy, 0, length);
            return copy;
        }

        static string ReadString(byte[] b, ref int pos)
        {
            uint length = U32(b, ref pos);
            Need(b, pos, length);
            string s = Encoding.UTF8.GetString(b, pos, (int)length);
            pos += (int)length;
            int padded = (int)((length + 3) & ~3u);
            pos += padded - (int)length;
            return s;
        }

        /// <summary>The builtin type of a typed container (bits at `shift`): Nil when untyped; false for a class or script type.</summary>
        static bool ReadContainerType(byte[] b, ref int pos, uint header, int shift, out VariantType type)
        {
            type = VariantType.Nil;
            uint kind = (header >> shift) & 3;
            if (kind == 0) return true;
            if (kind != TypedBuiltin) return false;
            uint raw = U32(b, ref pos);
            if (raw >= 39) return false;
            type = (VariantType)raw;
            return true;
        }

        static bool ReadDict(byte[] b, ref int pos, uint header, int depth, out object value)
        {
            value = null;
            if (!ReadContainerType(b, ref pos, header, TypedShift, out VariantType keyType)) return false;
            if (!ReadContainerType(b, ref pos, header, TypedShift + 2, out VariantType valueType)) return false;
            uint count = U32(b, ref pos) & 0x7FFFFFFF;
            var d = new GDict(keyType, valueType);
            for (uint i = 0; i < count; i++)
            {
                if (!Read(b, ref pos, depth + 1, out object key)) return false;
                if (!Read(b, ref pos, depth + 1, out object item)) return false;
                d[key] = item;
            }
            value = d;
            return true;
        }

        static bool ReadArray(byte[] b, ref int pos, uint header, int depth, out object value)
        {
            value = null;
            if (!ReadContainerType(b, ref pos, header, TypedShift, out VariantType elementType)) return false;
            uint count = U32(b, ref pos) & 0x7FFFFFFF;
            Need(b, pos, count * 4L); // every element takes at least its header
            var a = new GArray(elementType);
            for (uint i = 0; i < count; i++)
            {
                if (!Read(b, ref pos, depth + 1, out object item)) return false;
                a.Add(item);
            }
            value = a;
            return true;
        }

        static GArray ReadPacked(byte[] b, ref int pos, VariantType type)
        {
            uint count = U32(b, ref pos);
            var a = new GArray { Packed = type };
            switch (type)
            {
                case VariantType.PackedByteArray:
                    Need(b, pos, count);
                    for (int i = 0; i < count; i++) a.Add((long)b[pos + i]);
                    pos += (int)((count + 3) & ~3u);
                    break;
                case VariantType.PackedInt32Array:
                    Need(b, pos, count * 4L);
                    for (int i = 0; i < count; i++) a.Add((long)BitConverter.ToInt32(b, pos + i * 4));
                    pos += (int)count * 4;
                    break;
                case VariantType.PackedInt64Array:
                    Need(b, pos, count * 8L);
                    for (int i = 0; i < count; i++) a.Add(BitConverter.ToInt64(b, pos + i * 8));
                    pos += (int)count * 8;
                    break;
                case VariantType.PackedFloat32Array:
                    Need(b, pos, count * 4L);
                    for (int i = 0; i < count; i++) a.Add((double)BitConverter.ToSingle(b, pos + i * 4));
                    pos += (int)count * 4;
                    break;
                case VariantType.PackedFloat64Array:
                    Need(b, pos, count * 8L);
                    for (int i = 0; i < count; i++) a.Add(BitConverter.ToDouble(b, pos + i * 8));
                    pos += (int)count * 8;
                    break;
                case VariantType.PackedStringArray:
                    for (int i = 0; i < count; i++) a.Add(ReadString(b, ref pos));
                    break;
                case VariantType.PackedVector2Array:
                    Need(b, pos, count * 8L);
                    for (int i = 0; i < count; i++) a.Add(new Vec2(BitConverter.ToSingle(b, pos + i * 8), BitConverter.ToSingle(b, pos + i * 8 + 4)));
                    pos += (int)count * 8;
                    break;
            }
            return a;
        }
    }
}
