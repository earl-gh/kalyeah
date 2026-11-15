using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Kalyeah.Sim;

namespace Kalyeah.UI
{
    /// <summary>
    /// Godot's ConfigFile as a small JSON file: sections of keys, each a bool, a whole number, a number, a text, a list or a nested object.
    /// The menus keep the settings, the session and the history in three of them under Application.persistentDataPath (the Unity
    /// side passes the folder; nothing here touches the engine). A missing or broken file reads as empty, and a file that cannot be
    /// written is skipped: the game plays on with the values in memory.
    /// </summary>
    public sealed class ConfigStore
    {
        readonly string _path;
        OrderedDict<string, object> _root = new OrderedDict<string, object>();

        /// <summary>A store kept at `path` (null: in memory only, for tests and when there is nowhere to write).</summary>
        public ConfigStore(string path)
        {
            _path = path;
            Reload();
        }

        public string Path => _path;

        /// <summary>Reads the file again (empty when it is missing or not JSON).</summary>
        public void Reload()
        {
            _root = new OrderedDict<string, object>();
            if (string.IsNullOrEmpty(_path)) return;
            try
            {
                if (!File.Exists(_path)) return;
                if (MiniJson.Parse(File.ReadAllText(_path, Encoding.UTF8)) is OrderedDict<string, object> parsed) _root = parsed;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is FormatException || e is InvalidCastException
                || e is IndexOutOfRangeException || e is ArgumentException || e is OverflowException)
            {
                _root = new OrderedDict<string, object>();
            }
        }

        public bool Has(string section, string key) => Section(section, false) is OrderedDict<string, object> s && s.ContainsKey(key);

        public object Get(string section, string key, object fallback = null)
        {
            OrderedDict<string, object> s = Section(section, false);
            return s != null && s.TryGetValue(key, out object value) && value != null ? value : fallback;
        }

        public bool GetBool(string section, string key, bool fallback) => Get(section, key) is bool b ? b : fallback;

        public string GetString(string section, string key, string fallback) => Get(section, key) is string s ? s : fallback;

        /// <summary>A number stored as a whole number or a decimal, else `fallback` (and null: absent or not a number).</summary>
        public double? GetNumber(string section, string key)
        {
            object value = Get(section, key);
            if (value is long l) return l;
            if (value is double d) return d;
            if (value is int i) return i;
            return null;
        }

        public List<object> GetList(string section, string key) => Get(section, key) as List<object> ?? new List<object>();

        public void Set(string section, string key, object value) => Section(section, true)[key] = Normalise(value);

        /// <summary>Writes the file (silently keeps the values in memory when the folder cannot be written).</summary>
        public bool Save()
        {
            if (string.IsNullOrEmpty(_path)) return false;
            try
            {
                string folder = System.IO.Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                string temp = _path + ".tmp";
                File.WriteAllText(temp, ToJson(), new UTF8Encoding(false));
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(temp, _path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
            {
                return false;
            }
        }

        /// <summary>Sets one value and saves at once (Settings' `_save_value`).</summary>
        public void Put(string section, string key, object value)
        {
            Set(section, key, value);
            Save();
        }

        public string ToJson()
        {
            var text = new StringBuilder();
            Write(text, _root);
            return text.ToString();
        }

        OrderedDict<string, object> Section(string name, bool create)
        {
            if (_root.TryGetValue(name, out object found) && found is OrderedDict<string, object> section) return section;
            if (!create) return null;
            var made = new OrderedDict<string, object>();
            _root[name] = made;
            return made;
        }

        /// <summary>Ints become longs and floats doubles, as MiniJson reads them back; lists and maps are copied.</summary>
        static object Normalise(object value)
        {
            switch (value)
            {
                case null: return null;
                case bool _: case string _: case long _: case double _: return value;
                case int i: return (long)i;
                case float f: return (double)f;
                case OrderedDict<string, object> map:
                {
                    var copy = new OrderedDict<string, object>();
                    foreach (var pair in map) copy[pair.Key] = Normalise(pair.Value);
                    return copy;
                }
                case IDictionary<string, object> dict:
                {
                    var copy = new OrderedDict<string, object>();
                    foreach (var pair in dict) copy[pair.Key] = Normalise(pair.Value);
                    return copy;
                }
                case System.Collections.IEnumerable items:
                {
                    var list = new List<object>();
                    foreach (object item in items) list.Add(Normalise(item));
                    return list;
                }
                default: return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        static void Write(StringBuilder text, object value)
        {
            switch (value)
            {
                case null: text.Append("null"); break;
                case bool b: text.Append(b ? "true" : "false"); break;
                case long l: text.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case double d:
                {
                    if (double.IsNaN(d) || double.IsInfinity(d)) text.Append('0');
                    else
                    {
                        string number = d.ToString("R", CultureInfo.InvariantCulture);
                        // keep a decimal point so it reads back as a decimal, not a whole number
                        if (number.IndexOf('.') < 0 && number.IndexOf('E') < 0 && number.IndexOf('e') < 0) number += ".0";
                        text.Append(number);
                    }
                    break;
                }
                case string s: WriteString(text, s); break;
                case OrderedDict<string, object> map:
                {
                    text.Append('{');
                    bool first = true;
                    foreach (var pair in map)
                    {
                        if (!first) text.Append(',');
                        first = false;
                        WriteString(text, pair.Key);
                        text.Append(':');
                        Write(text, pair.Value);
                    }
                    text.Append('}');
                    break;
                }
                case List<object> list:
                {
                    text.Append('[');
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0) text.Append(',');
                        Write(text, list[i]);
                    }
                    text.Append(']');
                    break;
                }
                default: WriteString(text, Convert.ToString(value, CultureInfo.InvariantCulture)); break;
            }
        }

        static void WriteString(StringBuilder text, string s)
        {
            text.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        if (c < 0x20) text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else text.Append(c);
                        break;
                }
            }
            text.Append('"');
        }
    }
}
