using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Kalyeah.Sim
{
    /// <summary>
    /// A small self-contained JSON reader (no extra DLL in Unity): objects become <see cref="OrderedDict{TKey,TValue}"/> of string to
    /// object (key order kept), arrays <c>List&lt;object&gt;</c>, numbers <c>long</c> (no fraction or exponent) or <c>double</c>,
    /// plus string, bool and null.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            var parser = new Parser(text);
            parser.SkipWs();
            object value = parser.Value();
            parser.SkipWs();
            if (!parser.AtEnd) throw new FormatException($"JSON: trailing characters at {parser.Pos}");
            return value;
        }

        /// <summary>A number as a double (long or double).</summary>
        public static double Num(object o) => o is long l ? l : o is double d ? d : o is bool b ? (b ? 1 : 0) : Convert.ToDouble(o, CultureInfo.InvariantCulture);

        sealed class Parser
        {
            readonly string _s;
            int _i;
            public Parser(string s) { _s = s; }
            public bool AtEnd => _i >= _s.Length;
            public int Pos => _i;

            public void SkipWs()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            public object Value()
            {
                if (AtEnd) throw new FormatException("JSON: unexpected end");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return Obj();
                    case '[': return Arr();
                    case '"': return Str();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return Number();
                }
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw new FormatException($"JSON: expected {word} at {_i}");
                _i += word.Length;
            }

            OrderedDict<string, object> Obj()
            {
                var result = new OrderedDict<string, object>();
                _i++;
                SkipWs();
                if (_s[_i] == '}') { _i++; return result; }
                while (true)
                {
                    SkipWs();
                    string key = Str();
                    SkipWs();
                    if (_s[_i] != ':') throw new FormatException($"JSON: expected ':' at {_i}");
                    _i++;
                    SkipWs();
                    result[key] = Value();
                    SkipWs();
                    char c = _s[_i++];
                    if (c == '}') return result;
                    if (c != ',') throw new FormatException($"JSON: expected ',' at {_i - 1}");
                }
            }

            List<object> Arr()
            {
                var result = new List<object>();
                _i++;
                SkipWs();
                if (_s[_i] == ']') { _i++; return result; }
                while (true)
                {
                    SkipWs();
                    result.Add(Value());
                    SkipWs();
                    char c = _s[_i++];
                    if (c == ']') return result;
                    if (c != ',') throw new FormatException($"JSON: expected ',' at {_i - 1}");
                }
            }

            string Str()
            {
                if (_s[_i] != '"') throw new FormatException($"JSON: expected string at {_i}");
                _i++;
                var sb = new StringBuilder();
                while (true)
                {
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
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
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw new FormatException($"JSON: bad escape at {_i - 1}");
                    }
                }
            }

            object Number()
            {
                int start = _i;
                bool real = false;
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c == '.' || c == 'e' || c == 'E') real = true;
                    else if (!(char.IsDigit(c) || c == '-' || c == '+')) break;
                    _i++;
                }
                string token = _s.Substring(start, _i - start);
                if (token.Length == 0) throw new FormatException($"JSON: unexpected character at {start}");
                if (!real && long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) return l;
                return double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }
}
