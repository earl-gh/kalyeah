using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Kalyeah.Game
{
    /// <summary>
    /// A small strict JSON reader for the files tools/unity_export/export.gd writes (unity/Data/*.json). Objects come back as
    /// Dictionary&lt;string, object&gt;, arrays as List&lt;object&gt;, numbers as double, plus string, bool and null. No engine
    /// and no package needed, so the view's data code compiles and runs in plain .NET too.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var reader = new Reader(text);
            object value = reader.ReadValue();
            reader.SkipWhitespace();
            if (!reader.AtEnd) throw reader.Error("text after the JSON value");
            return value;
        }

        sealed class Reader
        {
            readonly string _s;
            int _i;

            public Reader(string s) { _s = s; }

            public bool AtEnd => _i >= _s.Length;

            public FormatException Error(string what) => new FormatException($"JSON: {what} at character {_i}");

            public void SkipWhitespace()
            {
                while (_i < _s.Length && (_s[_i] == ' ' || _s[_i] == '\t' || _s[_i] == '\n' || _s[_i] == '\r' || _s[_i] == '﻿')) _i++;
            }

            public object ReadValue()
            {
                SkipWhitespace();
                if (AtEnd) throw Error("unexpected end");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error($"unexpected '{c}'");
                }
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw Error($"expected {word}");
                _i += word.Length;
            }

            Dictionary<string, object> ReadObject()
            {
                var result = new Dictionary<string, object>();
                _i++; // {
                SkipWhitespace();
                if (!AtEnd && _s[_i] == '}') { _i++; return result; }
                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || _s[_i] != '"') throw Error("expected a key");
                    string key = ReadString();
                    SkipWhitespace();
                    if (AtEnd || _s[_i] != ':') throw Error("expected ':'");
                    _i++;
                    result[key] = ReadValue();
                    SkipWhitespace();
                    if (AtEnd) throw Error("unexpected end in an object");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; return result; }
                    throw Error("expected ',' or '}'");
                }
            }

            List<object> ReadArray()
            {
                var result = new List<object>();
                _i++; // [
                SkipWhitespace();
                if (!AtEnd && _s[_i] == ']') { _i++; return result; }
                while (true)
                {
                    result.Add(ReadValue());
                    SkipWhitespace();
                    if (AtEnd) throw Error("unexpected end in an array");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; return result; }
                    throw Error("expected ',' or ']'");
                }
            }

            string ReadString()
            {
                _i++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("unterminated escape");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Error("short \\u escape");
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw Error($"bad escape \\{e}");
                    }
                }
            }

            double ReadNumber()
            {
                int start = _i;
                if (_s[_i] == '-') _i++;
                while (!AtEnd && "0123456789.eE+-".IndexOf(_s[_i]) >= 0) _i++;
                string token = _s.Substring(start, _i - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) throw Error($"bad number '{token}'");
                return value;
            }
        }
    }
}
