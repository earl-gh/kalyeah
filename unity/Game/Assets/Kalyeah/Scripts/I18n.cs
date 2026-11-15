using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Kalyeah.Game
{
    /// <summary>
    /// Engine-free runtime CSV localization table supporting English ("en") and Filipino/Tagalog ("fil" / "tl").
    /// Parses RFC-4180 CSV text (such as <c>Data/i18n__strings</c>) with quoted multiline cells, escaped quotes,
    /// and printf-style <c>%s</c>/<c>%d</c>/<c>%f</c> formatting.
    /// </summary>
    public static class I18n
    {
        public const int MinTranslatedForLocale = 30;

        static readonly object Sync = new object();
        static readonly Dictionary<string, Dictionary<string, string>> Entries =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        static readonly List<string> LoadedLanguages = new List<string> { "en" };

        public static int KeyCount
        {
            get
            {
                lock (Sync)
                {
                    return Entries.Count;
                }
            }
        }

        public static IReadOnlyCollection<string> Keys
        {
            get
            {
                lock (Sync)
                {
                    var copy = new string[Entries.Count];
                    Entries.Keys.CopyTo(copy, 0);
                    return copy;
                }
            }
        }

        public static IReadOnlyList<string> AvailableLanguages
        {
            get
            {
                lock (Sync)
                {
                    var result = new List<string> { "en" };
                    for (int i = 0; i < LoadedLanguages.Count; i++)
                    {
                        string code = LoadedLanguages[i];
                        if (code == "en" || result.Contains(code)) continue;
                        if (CountTranslatedLocked(code) >= MinTranslatedForLocale)
                            result.Add(code);
                    }
                    return result;
                }
            }
        }

        public static void Clear()
        {
            lock (Sync)
            {
                Entries.Clear();
                LoadedLanguages.Clear();
                LoadedLanguages.Add("en");
            }
        }

        public static bool LoadFromData(DataSource data, string resourceName = "i18n__strings")
        {
            if (data == null || string.IsNullOrWhiteSpace(resourceName)) return false;
            try
            {
                string csv = data.Text(resourceName);
                return LoadCsv(csv) > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Replaces the active translation table from RFC-4180 CSV text (<c>keys,en,...</c>) and returns <see cref="KeyCount"/>.
        /// </summary>
        public static int LoadCsv(string csv)
        {
            lock (Sync)
            {
                Entries.Clear();
                LoadedLanguages.Clear();
                LoadedLanguages.Add("en");

                if (string.IsNullOrWhiteSpace(csv))
                    return 0;

                List<List<string>> rows = ParseCsvRows(csv);
                if (rows.Count == 0)
                    return 0;

                int headerRowIndex = -1;
                for (int r = 0; r < rows.Count; r++)
                {
                    List<string> candidate = rows[r];
                    if (candidate.Count == 0) continue;
                    string first = StripBom(candidate[0]).Trim();
                    if (string.IsNullOrEmpty(first) || first[0] == '#') continue;
                    headerRowIndex = r;
                    break;
                }

                if (headerRowIndex < 0)
                    return 0;

                List<string> header = rows[headerRowIndex];
                var colLangs = new string[header.Count];
                for (int c = 1; c < header.Count; c++)
                {
                    string rawCode = StripBom(header[c]).Trim();
                    if (string.IsNullOrEmpty(rawCode)) continue;
                    string norm = NormalizeLanguage(rawCode);
                    colLangs[c] = norm;
                    if (!LoadedLanguages.Contains(norm))
                        LoadedLanguages.Add(norm);
                }

                for (int r = headerRowIndex + 1; r < rows.Count; r++)
                {
                    List<string> row = rows[r];
                    if (row.Count == 0) continue;
                    string key = row[0];
                    if (string.IsNullOrEmpty(key)) continue;
                    if (row.Count == 1 && key.TrimStart().StartsWith("#", StringComparison.Ordinal))
                        continue;

                    if (!Entries.TryGetValue(key, out Dictionary<string, string> translations))
                    {
                        translations = new Dictionary<string, string>(StringComparer.Ordinal);
                        Entries[key] = translations;
                    }

                    int limit = Math.Min(row.Count, colLangs.Length);
                    for (int c = 1; c < limit; c++)
                    {
                        string lang = colLangs[c];
                        if (string.IsNullOrEmpty(lang)) continue;
                        string cell = row[c];
                        if (!string.IsNullOrEmpty(cell))
                            translations[lang] = cell;
                    }
                }

                return Entries.Count;
            }
        }

        public static bool HasKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            lock (Sync)
            {
                return Entries.ContainsKey(key);
            }
        }

        public static bool HasTranslation(string key, string lang)
        {
            if (string.IsNullOrEmpty(key)) return false;
            string norm = NormalizeLanguage(lang);
            lock (Sync)
            {
                return Entries.TryGetValue(key, out Dictionary<string, string> map) &&
                       map.TryGetValue(norm, out string text) &&
                       !string.IsNullOrEmpty(text);
            }
        }

        public static int TranslatedCount(string lang)
        {
            string norm = NormalizeLanguage(lang);
            lock (Sync)
            {
                return CountTranslatedLocked(norm);
            }
        }

        /// <summary>
        /// Looks up <paramref name="key"/> in <paramref name="lang"/> (defaults to <see cref="SettingsStore.Language"/>),
        /// falling back to English ("en") or <paramref name="key"/> itself when untranslated.
        /// </summary>
        public static string Tr(string key, string lang = null)
        {
            if (string.IsNullOrEmpty(key)) return key ?? "";
            string target = string.IsNullOrWhiteSpace(lang)
                ? NormalizeLanguage(SettingsStore.Current.Language)
                : NormalizeLanguage(lang);

            lock (Sync)
            {
                if (!Entries.TryGetValue(key, out Dictionary<string, string> map))
                    return key;

                if (map.TryGetValue(target, out string localized) && !string.IsNullOrEmpty(localized))
                    return localized;

                if (target != "en" && map.TryGetValue("en", out string english) && !string.IsNullOrEmpty(english))
                    return english;

                return key;
            }
        }

        /// <summary>Translates <paramref name="key"/> using the active language and formats printf-style (<c>%s</c>, <c>%d</c>, <c>%f</c>) placeholders.</summary>
        public static string Format(string key, params object[] args) =>
            FormatLang(key, null, args);

        /// <summary>Translates <paramref name="key"/> in <paramref name="lang"/> and formats printf-style (<c>%s</c>, <c>%d</c>, <c>%f</c>) placeholders.</summary>
        public static string FormatLang(string key, string lang, params object[] args)
        {
            string pattern = Tr(key, lang);
            if (args == null || args.Length == 0 || string.IsNullOrEmpty(pattern))
                return pattern;
            return FormatPrintf(pattern, args);
        }

        /// <summary>Normalizes language codes ("tl", "fil", "tl-PH", "fil-PH" -&gt; "fil"; "en", "en-US" -&gt; "en").</summary>
        public static string NormalizeLanguage(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang)) return "en";
            string clean = lang.Trim().ToLowerInvariant();
            int dash = clean.IndexOfAny(new[] { '-', '_' });
            if (dash > 0) clean = clean.Substring(0, dash);
            if (clean == "tl" || clean == "fil" || clean == "tagalog" || clean == "filipino")
                return "fil";
            if (clean == "en" || clean == "english")
                return "en";
            return clean;
        }

        static int CountTranslatedLocked(string normLang)
        {
            int count = 0;
            foreach (KeyValuePair<string, Dictionary<string, string>> kv in Entries)
            {
                if (kv.Value.TryGetValue(normLang, out string v) && !string.IsNullOrEmpty(v))
                    count++;
            }
            return count;
        }

        static string StripBom(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value[0] == '\uFEFF' ? value.Substring(1) : value;
        }

        static string FormatPrintf(string pattern, object[] args)
        {
            if (pattern.IndexOf('%') < 0)
            {
                if (pattern.IndexOf("{0", StringComparison.Ordinal) >= 0)
                {
                    try { return string.Format(CultureInfo.InvariantCulture, pattern, args); }
                    catch { return pattern; }
                }
                return pattern;
            }

            var sb = new StringBuilder(pattern.Length + 16);
            int argIndex = 0;
            int i = 0;
            while (i < pattern.Length)
            {
                char c = pattern[i];
                if (c != '%' || i + 1 >= pattern.Length)
                {
                    sb.Append(c);
                    i++;
                    continue;
                }

                char next = pattern[i + 1];
                if (next == '%')
                {
                    sb.Append('%');
                    i += 2;
                    continue;
                }

                int j = i + 1;
                int precision = -1;
                if (j < pattern.Length && pattern[j] == '.')
                {
                    j++;
                    int pStart = j;
                    while (j < pattern.Length && char.IsDigit(pattern[j])) j++;
                    if (j > pStart)
                        int.TryParse(pattern.Substring(pStart, j - pStart), NumberStyles.Integer, CultureInfo.InvariantCulture, out precision);
                }

                if (j < pattern.Length)
                {
                    char spec = pattern[j];
                    if ((spec == 's' || spec == 'd' || spec == 'i' || spec == 'u' || spec == 'f') && argIndex < args.Length)
                    {
                        object val = args[argIndex++];
                        AppendFormattedArg(sb, spec, precision, val);
                        i = j + 1;
                        continue;
                    }
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }

        static void AppendFormattedArg(StringBuilder sb, char spec, int precision, object val)
        {
            if (val == null) return;
            switch (spec)
            {
                case 'd':
                case 'i':
                case 'u':
                    try
                    {
                        long n = Convert.ToInt64(val, CultureInfo.InvariantCulture);
                        sb.Append(n.ToString(CultureInfo.InvariantCulture));
                        return;
                    }
                    catch
                    {
                        sb.Append(Convert.ToString(val, CultureInfo.InvariantCulture));
                        return;
                    }
                case 'f':
                    try
                    {
                        double d = Convert.ToDouble(val, CultureInfo.InvariantCulture);
                        string fmt = precision >= 0
                            ? "F" + precision.ToString(CultureInfo.InvariantCulture)
                            : "0.##";
                        sb.Append(d.ToString(fmt, CultureInfo.InvariantCulture));
                        return;
                    }
                    catch
                    {
                        sb.Append(Convert.ToString(val, CultureInfo.InvariantCulture));
                        return;
                    }
                default:
                    sb.Append(Convert.ToString(val, CultureInfo.InvariantCulture));
                    return;
            }
        }

        static List<List<string>> ParseCsvRows(string csv)
        {
            var rows = new List<List<string>>();
            var currentRow = new List<string>();
            var cell = new StringBuilder(64);
            bool inQuotes = false;

            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else if (c == '\r')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '\n')
                            i++;
                        cell.Append('\n');
                    }
                    else
                    {
                        cell.Append(c);
                    }
                }
                else
                {
                    if (c == '"' && cell.Length == 0)
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        currentRow.Add(cell.ToString());
                        cell.Clear();
                    }
                    else if (c == '\r' || c == '\n')
                    {
                        if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                            i++;
                        currentRow.Add(cell.ToString());
                        cell.Clear();
                        if (HasNonEmptyCell(currentRow))
                            rows.Add(currentRow);
                        currentRow = new List<string>();
                    }
                    else
                    {
                        cell.Append(c);
                    }
                }
            }

            if (cell.Length > 0 || currentRow.Count > 0)
            {
                currentRow.Add(cell.ToString());
                if (HasNonEmptyCell(currentRow))
                    rows.Add(currentRow);
            }

            return rows;
        }

        static bool HasNonEmptyCell(List<string> row)
        {
            for (int i = 0; i < row.Count; i++)
                if (!string.IsNullOrEmpty(row[i])) return true;
            return false;
        }
    }
}
