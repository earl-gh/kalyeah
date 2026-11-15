using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Kalyeah.UI
{
    /// <summary>
    /// The local player's state that outlives scenes (port of scripts/autoload/session.gd): the name and avatar, the Practice game,
    /// the saved loadout and the reconnect token and address (saved, so a restarted app can take its slot back within the reconnect
    /// window), plus what the main screen hands the room screen (not saved).
    /// </summary>
    public sealed class SessionModel
    {
        public enum LobbyIntent { Create = 0, Join = 1 }

        public static readonly string[] Avatars = { "junjun", "ligaya", "migo", "toni", "popoy", "inday", "kiko", "nene", "buknoy", "tala" };
        /// <summary>Names share the server's twelve-character limit.</summary>
        public const int MaxNameLength = 12;
        public const string DefaultName = "GABO";
        public const string FileName = "session.json";
        /// <summary>The Practice games (RoomService.MAP_IDS), Agawang Base first.</summary>
        public static readonly string[] PracticeGames = { "street", "tumbang_preso", "patintero", "langit_lupa" };
        /// <summary>What a name may hold: letters (any language), digits, a space, apostrophe, dot, underscore or hyphen.</summary>
        static readonly Regex NotNameCharacter = new Regex(@"[^\p{L}\p{N} '._-]");
        static readonly Regex Spaces = new Regex(" {2,}");

        readonly ConfigStore _store;

        public string PlayerName = "";
        /// <summary>The kid whose face is your avatar and whom you play (picked once, saved).</summary>
        public string AvatarId = "";
        public readonly List<string> SavedLoadout = new List<string>();
        public string RoomCode = "";
        public LobbyIntent Intent = LobbyIntent.Create;
        /// <summary>The room screen was opened from Local (hotspot) rather than Online.</summary>
        public bool LobbyHotspot;
        /// <summary>The room a Local Join found and verified (a ws:// address), for the room screen to join.</summary>
        public string LobbyUrl = "";
        /// <summary>The mode on the main screen: 0 Online, 1 Local (the default), 2 Practice.</summary>
        public int MenuMode = 1;
        /// <summary>The next Practice match against bots opens with the briefing (not saved).</summary>
        public bool BriefingPending;
        /// <summary>"training" (a sandbox with dummies) or "match" (a full match against bots); not saved.</summary>
        public string PracticeKind = "training";
        /// <summary>The street game Practice plays (saved).</summary>
        public string PracticeGame = "street";
        public string ReconnectToken = "";
        public string ReconnectUrl = "";

        public SessionModel(ConfigStore store)
        {
            _store = store ?? new ConfigStore(null);
            Load();
        }

        public void Load()
        {
            PlayerName = CleanName(_store.GetString("player", "name", ""));
            AvatarId = _store.GetString("player", "avatar", "");
            string game = _store.GetString("player", "practice_game", "street");
            PracticeGame = Array.IndexOf(PracticeGames, game) >= 0 ? game : "street";
            SavedLoadout.Clear();
            foreach (object id in _store.GetList("player", "loadout"))
                if (id is string s && s.Length > 0) SavedLoadout.Add(s);
            ReconnectToken = _store.GetString("match", "token", "");
            ReconnectUrl = _store.GetString("match", "url", "");
        }

        public void Save()
        {
            _store.Set("player", "name", PlayerName);
            _store.Set("player", "avatar", AvatarId);
            _store.Set("player", "practice_game", PracticeGame);
            _store.Set("player", "loadout", new List<object>(SavedLoadout));
            _store.Set("match", "token", ReconnectToken);
            _store.Set("match", "url", ReconnectUrl);
            _store.Save();
        }

        /// <summary>A new player gets the name GABO and a random kid's face; both can be changed. `random(n)` picks 0..n-1.</summary>
        public void EnsureProfile(Func<int, int> random)
        {
            bool changed = false;
            if (PlayerName.Trim().Length == 0)
            {
                PlayerName = DefaultName;
                changed = true;
            }
            if (Array.IndexOf(Avatars, AvatarId) < 0)
            {
                AvatarId = Avatars[Math.Max(0, Math.Min(Avatars.Length - 1, random(Avatars.Length)))];
                changed = true;
            }
            if (changed) Save();
        }

        /// <summary>The name as the game uses it: allowed characters only, single spaces, trimmed and at most `MaxNameLength` long.</summary>
        public static string CleanName(string raw)
        {
            string kept = FilterName(raw).Trim();
            if (kept.Length > MaxNameLength) kept = kept.Substring(0, MaxNameLength);
            return kept.Trim();
        }

        /// <summary>`raw` without the characters a name cannot have and with one space where there were several (for typing).</summary>
        public static string FilterName(string raw) => Spaces.Replace(NotNameCharacter.Replace(raw ?? "", ""), " ");

        /// <summary>Sets the name (cleaned; "" keeps the old one) and saves.</summary>
        public bool Rename(string raw)
        {
            string name = CleanName(raw);
            if (name.Length == 0 || name == PlayerName) return false;
            PlayerName = name;
            Save();
            return true;
        }

        public void SetAvatar(string id)
        {
            if (Array.IndexOf(Avatars, id) < 0 || id == AvatarId) return;
            AvatarId = id;
            Save();
        }

        public void SetPracticeGame(string id)
        {
            if (Array.IndexOf(PracticeGames, id) < 0 || id == PracticeGame) return;
            PracticeGame = id;
            Save();
        }

        /// <summary>The saved loadout's weapons that are still `available`, without repeats, at most `count`.</summary>
        public List<string> LoadoutFor(IList<string> available, int count = 3)
        {
            int limit = Math.Max(count, 1);
            var valid = new List<string>();
            foreach (string id in SavedLoadout)
                if (available.Contains(id) && !valid.Contains(id) && valid.Count < limit) valid.Add(id);
            return valid;
        }

        /// <summary>Keeps a pick of 2 or 3 different weapons (WeaponPick.SLOTS is 2).</summary>
        public void RememberLoadout(IList<string> picks)
        {
            if (picks.Count < 2 || picks.Count > 3) return;
            var unique = new List<string>();
            foreach (string id in picks)
            {
                if (string.IsNullOrEmpty(id) || unique.Contains(id)) return;
                unique.Add(id);
            }
            SavedLoadout.Clear();
            SavedLoadout.AddRange(unique);
            Save();
        }

        /// <summary>A room was joined: keep its code and the slot's token and address, to rejoin after a drop.</summary>
        public void RememberRoom(string code, string token, string url)
        {
            RoomCode = code ?? "";
            ReconnectToken = token ?? "";
            ReconnectUrl = url ?? "";
            Save();
        }

        public void ForgetMatch()
        {
            ReconnectToken = "";
            Save();
        }
    }
}
