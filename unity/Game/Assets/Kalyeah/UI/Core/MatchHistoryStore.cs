using System;
using System.Collections.Generic;
using Kalyeah.Sim;

namespace Kalyeah.UI
{
    /// <summary>One finished battle on this phone.</summary>
    public sealed class HistoryEntry
    {
        /// <summary>"practice", "hotspot" or "online".</summary>
        public string Mode = "";
        public bool Won;
        public int Mine;
        public int Theirs;
        /// <summary>Unix seconds.</summary>
        public long At;
        public int LpDelta;
        public string Tier = "";
    }

    /// <summary>
    /// The last battles this phone played (port of match_history.gd), in a small local file so the History page can list them.
    /// Nothing leaves the phone. The match screen calls `Record` when a battle ends.
    /// </summary>
    public sealed class MatchHistoryStore
    {
        public const int MaxEntries = 20;
        public const string FileName = "history.json";
        public static readonly string[] Modes = { "practice", "hotspot", "online" };

        readonly ConfigStore _store;

        public MatchHistoryStore(ConfigStore store) { _store = store ?? new ConfigStore(null); }

        public void Record(string mode, bool won, int mine, int theirs, long now, int lpDelta = 0, string tier = "")
        {
            var list = new List<object>();
            var entry = new OrderedDict<string, object>();
            entry["mode"] = mode;
            entry["won"] = won;
            entry["mine"] = (long)mine;
            entry["theirs"] = (long)theirs;
            entry["at"] = now;
            if (lpDelta != 0) entry["lp_delta"] = (long)lpDelta;
            if (!string.IsNullOrEmpty(tier)) entry["tier"] = tier;
            list.Add(entry);
            foreach (object old in _store.GetList("history", "entries"))
                if (old is OrderedDict<string, object> && list.Count < MaxEntries) list.Add(old);
            _store.Put("history", "entries", list);
        }

        /// <summary>Newest first.</summary>
        public List<HistoryEntry> Entries()
        {
            var found = new List<HistoryEntry>();
            foreach (object raw in _store.GetList("history", "entries"))
            {
                if (!(raw is OrderedDict<string, object> item)) continue;
                found.Add(new HistoryEntry
                {
                    Mode = item.GetOrDefault("mode") as string ?? "",
                    Won = item.GetOrDefault("won") is bool b && b,
                    Mine = (int)Number(item.GetOrDefault("mine")),
                    Theirs = (int)Number(item.GetOrDefault("theirs")),
                    At = Number(item.GetOrDefault("at")),
                    LpDelta = (int)Number(item.GetOrDefault("lp_delta")),
                    Tier = item.GetOrDefault("tier") as string ?? "",
                });
                if (found.Count == MaxEntries) break;
            }
            return found;
        }

        public void Clear() => _store.Put("history", "entries", new List<object>());

        public struct Summary
        {
            public int Played;
            public int Wins;
            /// <summary>Win rate, a whole percent.</summary>
            public int Rate;
            /// <summary>Wins in a row since the newest (0 when the newest was lost).</summary>
            public int Streak;
        }

        public static Summary Summarise(IList<HistoryEntry> list)
        {
            int wins = 0;
            foreach (HistoryEntry entry in list)
                if (entry.Won) wins++;
            int streak = 0;
            foreach (HistoryEntry entry in list)
            {
                if (!entry.Won) break;
                streak++;
            }
            int rate = list.Count > 0 ? (int)Math.Round(100.0 * wins / list.Count, MidpointRounding.AwayFromZero) : 0;
            return new Summary { Played = list.Count, Wins = wins, Rate = rate, Streak = streak };
        }

        /// <summary>Only the battles of `mode`; "" keeps them all.</summary>
        public static List<HistoryEntry> OfMode(IList<HistoryEntry> list, string mode)
        {
            var kept = new List<HistoryEntry>();
            foreach (HistoryEntry entry in list)
                if (string.IsNullOrEmpty(mode) || entry.Mode == mode) kept.Add(entry);
            return kept;
        }

        /// <summary>"just now", "5 min ago", "3 h ago", "2 days ago".</summary>
        public static string Ago(long at, long now)
        {
            long seconds = Math.Max(0, now - at);
            if (seconds < 60) return "just now";
            if (seconds < 3600) return (seconds / 60) + " min ago";
            if (seconds < 86400) return (seconds / 3600) + " h ago";
            return (seconds / 86400) + " days ago";
        }

        /// <summary>What the History page and the results call each mode (Local is the hotspot mode's name on screen).</summary>
        public static string ModeName(string mode) => mode == "hotspot" ? "Local" : mode == "online" ? "Online" : "Practice";

        static long Number(object value) => value is long l ? l : value is double d ? (long)d : 0;
    }
}
