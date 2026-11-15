using System;
using System.Collections.Generic;
using Kalyeah.Net;
using Kalyeah.Sim;

namespace Kalyeah.UI
{
    /// <summary>
    /// Everything the menus read from the shipped data (unity/Data, the same files the sim and the server load), wrapped once: the
    /// rules, the network numbers, the street games, the kids, the weapon cards and the Practice difficulties.
    /// </summary>
    public sealed class MenuData
    {
        public readonly SimData Data;
        public readonly GameRules Rules;
        public readonly MapLayout Layout;
        public readonly NetRules Net;
        public readonly StreetCatalog Games;

        public MenuData(SimData data)
        {
            Data = data;
            Rules = data.GameRules;
            Layout = data.MapLayout;
            Net = NetRules.FromData(data);
            Games = new StreetCatalog(data);
        }

        /// <summary>A client for Online and Local rooms (the lobby, then the match's mirror).</summary>
        public GameClient NewClient() => new GameClient(Rules, Layout, Net, Games);

        /// <summary>The kid `id`'s name as the game writes it ("Junjun"), or the id capitalised.</summary>
        public string KidName(string id)
        {
            string source = "res://data/characters/" + id + ".tres";
            foreach (string known in Data.Sources)
                if (known == source) return Data.Get<CharacterDef>(source).DisplayName;
            return id.Length > 0 ? char.ToUpperInvariant(id[0]) + id.Substring(1) : id;
        }

        /// <summary>Every weapon card in the deck, in the rules' order (the Book).</summary>
        public List<WeaponDef> Cards()
        {
            var cards = new List<WeaponDef>();
            foreach (WeaponDef weapon in Rules.Weapons)
                if (weapon != null && !cards.Contains(weapon)) cards.Add(weapon);
            return cards;
        }

        /// <summary>The Practice difficulty `id` (the rules' default when it is unknown or ""), or null.</summary>
        public BotLevel BotLevel(string id) => Rules.GetBotLevel(id ?? "");

        /// <summary>The street game's rules, or null for Agawang Base ("street").</summary>
        public StreetModeRules StreetRules(string mapId) => Games.RulesFor(mapId);

        public bool FreeForAll(string mapId) => Games.IsFreeForAll(mapId);
    }

    /// <summary>The fixed words and choices of the menus (title_screen.gd, hotspot_room_view.gd, open_rooms_list.gd...).</summary>
    public static class MenuCatalog
    {
        public sealed class Mode
        {
            public string Id;
            public string Title;
            public string Text;
            public string Status;
            public string Cta;
            public string Sub;
        }

        /// <summary>The modes in the selector: Online, Local in the middle (the default, `SessionModel.MenuMode` 1), Practice.</summary>
        public static readonly Mode[] Modes =
        {
            new Mode { Id = "online", Title = "Online", Text = "Invite friends with a room code!", Status = "ONLINE", Cta = "CREATE ONLINE ROOM", Sub = "JOIN" },
            new Mode { Id = "hotspot", Title = "Local", Text = "Battle your friends nearby!", Status = "LOCAL · WIFI", Cta = "HOST LOCAL ROOM", Sub = "SUMALI" },
            new Mode { Id = "practice", Title = "Practice", Text = "Take on the Barangay Bots!", Status = "BOTS", Cta = "PLAY", Sub = "TRAINING" },
        };

        public sealed class RailItem
        {
            public string Id;
            public string Title;
            public string Sub;
        }

        /// <summary>The left navigation rail (1280 x 720): play, kids, cards (arsenal), history; Settings sits in the header.</summary>
        public static readonly RailItem[] Rail =
        {
            new RailItem { Id = "play", Title = "LARO", Sub = "PLAY" },
            new RailItem { Id = "kids", Title = "MGA BATA", Sub = "KIDS" },
            new RailItem { Id = "cards", Title = "ARMAS", Sub = "CARDS" },
            new RailItem { Id = "history", Title = "RANGGO", Sub = "HISTORY" },
        };

        public sealed class KidMeta
        {
            public string Epithet;
            public string Role;
            /// <summary>The kid's aura tint, #rrggbb.</summary>
            public string Accent;
        }

        /// <summary>Each kid's street epithet, archetype and aura tint (title_screen.gd KID_SHOWCASE_META). Words only: never gameplay.</summary>
        public static readonly IReadOnlyDictionary<string, KidMeta> Kids = new Dictionary<string, KidMeta>
        {
            { "junjun", new KidMeta { Epithet = "KAPITAN KUMOT", Role = "FRONTLINER", Accent = "f04e3e" } },
            { "ligaya", new KidMeta { Epithet = "KUMANDER TEKS", Role = "TACTICIAN", Accent = "ff8c42" } },
            { "migo", new KidMeta { Epithet = "PLASTIK-BALLOON", Role = "ZONER", Accent = "36c9b8" } },
            { "toni", new KidMeta { Epithet = "BARANGAY LIGA MVP", Role = "DUELIST", Accent = "f5a623" } },
            { "popoy", new KidMeta { Epithet = "TANGKE NG TANGHALI", Role = "GUARDIAN", Accent = "4aa0ff" } },
            { "inday", new KidMeta { Epithet = "REYNA NG PATINTERO", Role = "RUNNER", Accent = "ffd15c" } },
            { "kiko", new KidMeta { Epithet = "TSINELAS SPRINTER", Role = "SKIRMISHER", Accent = "28c7a8" } },
            { "nene", new KidMeta { Epithet = "TIRADOR SCOUT", Role = "MARKSMAN", Accent = "f78ca2" } },
            { "buknoy", new KidMeta { Epithet = "MEKANIKO NG KOLONG", Role = "BRAWLER", Accent = "e07a38" } },
            { "tala", new KidMeta { Epithet = "KOMIKS PINTOR", Role = "ARTILLERY", Accent = "b37cf5" } },
        };

        public static readonly IReadOnlyDictionary<string, string> MapNames = new Dictionary<string, string>
        {
            { "street", "Agawang Base" }, { "tumbang_preso", "Tumbang Preso" }, { "patintero", "Patintero" }, { "langit_lupa", "Langit Lupa" },
        };

        /// <summary>How each game plays (the Games page's blurbs, street_page.gd), for the Practice game picker.</summary>
        public static readonly IReadOnlyDictionary<string, string> MapBlurbs = new Dictionary<string, string>
        {
            { "street", "Two teams, two bases! Tag the other team, free your own, and hold their base." },
            { "tumbang_preso", "5 minutes: Knock the can over, fetch your slipper, and run to the other end to earn a score. The 'it' fetches the can and places it back to position. It can't tag players when the can is knocked over and not in position." },
            { "patintero", "5 minutes: Get past the enemy guards, run to the other end and back to earn a team point. Enemy players that act as guards tag you or your ally to reduce the chances of your team making it to the other end and back." },
            { "langit_lupa", "5 minutes: Run from the taya! Up on a langit you're safe for a few seconds, but the langit wears out, you can't rest up there, and the lights go out now and then." },
        };

        /// <summary>The room game picker's line under a game (room_map_picker.gd info_for).</summary>
        public static string MapInfo(MenuData data, string mapId, int teamSize, int firstTo)
        {
            StreetModeRules game = data.StreetRules(mapId);
            if (game == null) return teamSize + "v" + teamSize + " · Teams · Race to " + firstTo;
            int minutes = (int)Math.Round(game.MatchSeconds / 60.0, MidpointRounding.AwayFromZero);
            if (data.FreeForAll(mapId)) return game.RoomMinPlayers + " to " + RoomService.MaxTeamSize * 2 + " kids · Every kid for itself · " + minutes + " min";
            if (game.RoomMinTeam > 1) return teamSize + "v" + teamSize + " · Teams · " + game.RoomMinTeam + " or more a team · " + minutes + " min";
            return teamSize + "v" + teamSize + " · Teams · " + minutes + " min";
        }

        public static string MapName(string mapId) =>
            MapNames.TryGetValue(mapId ?? "", out string name) ? name : (mapId ?? "").ToUpperInvariant();

        public const string ComingSoon = "Online is coming soon!";
        public const string WakingText = "Waking up the server. This can take a minute…";
        public const string CantReach = "Can't reach the server. Check your connection and try again.";
        public const string ConnectionLost = "Connection lost. Try again!";
        public const string EmptyRooms = "No open rooms yet. Create one!";
        public const string LookingRooms = "Looking for rooms…";
        public const string ReadyToPlay = "Ready to play";

        /// <summary>The Practice confirm's line (title_screen.gd open_practice_confirm).</summary>
        public static string PracticeWords(MenuData data, string game)
        {
            StreetModeRules rules = data.StreetRules(game);
            if (rules == null) return "5 v 5 against Barangay bots! Ready to rumble?";
            string who = data.FreeForAll(game) ? "You against 9 bots." : "5 v 5 against bots.";
            return who + " " + (int)Math.Round(rules.MatchSeconds / 60.0, MidpointRounding.AwayFromZero) + " minutes, most points win!";
        }

        /// <summary>The Practice confirm's title: the game's name.</summary>
        public static string PracticeTitle(MenuData data, string game)
        {
            StreetModeRules rules = data.StreetRules(game);
            return rules != null && rules.Title.Length > 0 ? rules.Title : MapName(game);
        }

        /// <summary>"5v5" for a team game, "FFA" where every kid plays for itself.</summary>
        public static string FormatTag(MenuData data, string game) => data.FreeForAll(game) ? "FFA" : "5v5";

        /// <summary>An open room's card: "Ana's room", then "5v5 · 3/10 · Race to 3" (open_rooms_list.gd card_text).</summary>
        public static string OpenRoomText(GDict room, MenuData data)
        {
            int size = room.GetInt("team_size", 1);
            string host = room.GetStr("host_name");
            string title = host.Length > 0 ? host + "'s room" : "Open room";
            string mapId = room.GetStr("map", "street");
            string goal = mapId == "street" ? "Race to " + room.GetInt("first_to", 3) : MapName(mapId);
            int players = room.GetInt("players");
            if (data != null && data.FreeForAll(mapId)) return title + "\n" + players + "/" + size * 2 + " · " + goal;
            return title + "\n" + size + "v" + size + " · " + players + "/" + size * 2 + " · " + goal;
        }

        /// <summary>A Local room in the search list: "Gabo's room  1/6" (no address: the player never sees network details).</summary>
        public static string LocalRoomText(HotspotFinder.FoundRoom room)
        {
            string title = room.Host.Length == 0 ? "A room" : room.Host + "'s room";
            return room.Size <= 0 ? title : title + "  " + room.Players + "/" + room.Size;
        }

        /// <summary>The loading screen's tips, one per load.</summary>
        public static readonly string[] Tips =
        {
            "Lure the other team away from their post, then rush in and hold it!",
            "Tagged kids wait on the sidewalk. Stand in the rescue area to free them!",
            "Drag a card onto the street, then let go to throw it!",
            "Block a throw with a paper shield, or duck behind cover!",
            "Practice is the best place to try every weapon!",
            "Local rooms need no server. Share a hotspot or a Wi-Fi and battle!",
            "A quick tap on a card aims at the nearest enemy for you!",
        };
    }
}
