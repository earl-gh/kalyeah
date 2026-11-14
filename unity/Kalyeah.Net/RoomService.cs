using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Kalyeah.Sim;

namespace Kalyeah.Net
{
    /// <summary>
    /// Online and hotspot rooms (port of room_service.gd; docs/GDD.md "Lobby rules"): open (listed, joined with a tap) or private
    /// (passcode only), ten places, five a team; each game says how many it needs to start (`StartBlocker`). Pure logic, no sockets:
    /// the GameServer feeds it peer ids and sends out whatever changed. A member keeps one `PlayerId` for the whole match (its id in
    /// the MatchSim); `PeerId` is its current connection and changes on reconnect.
    /// </summary>
    public sealed class RoomService
    {
        /// <summary>The id of the first bot of a match; ids count up from here (a player never gets one).</summary>
        public const int BotIdBase = 1000;
        /// <summary>Every room is 5v5: five places a team, ten in all.</summary>
        public const int MaxTeamSize = 5;
        /// <summary>The name of the base game (the street games say theirs in their rules).</summary>
        public const string BaseTitle = "Agawang Base";
        public static readonly string[] MapIds = { "street", "tumbang_preso", "patintero", "langit_lupa" };
        public static readonly string[] PlayableMapIds = { "street", "patintero", "tumbang_preso", "langit_lupa" };

        public enum Result { Ok, NotFound, Full, InProgress, NotAllowed }
        public enum State { Lobby, Loading, Playing, Ended }

        public sealed class Member
        {
            public int PlayerId;
            public int PeerId;
            public string Name = "";
            /// <summary>-1 = no team slot yet.</summary>
            public int Team = -1;
            public bool Ready;
            public bool Connected = true;
            /// <summary>Seconds left to reconnect after dropping out of a running match.</summary>
            public double ReconnectLeft;
            /// <summary>Seconds since the member dropped out of a running match (0 while connected).</summary>
            public double GoneFor;
            /// <summary>Gone for `AfkAfter` seconds or more: away until they come back.</summary>
            public bool Afk;
            /// <summary>Secret handed to the client so it can take its slot back after a drop.</summary>
            public string Token = "";
            /// <summary>The kid picked in the room (cosmetic); "" = none.</summary>
            public string Character = "";
            public long RankLp;
            public string RankTier = "Kahoy";
        }

        public sealed class Room
        {
            public string Code = "";
            public int HostPlayerId;
            public int FirstTo;
            public string MapId = "street";
            public bool Open = true;
            /// <summary>Hotspot rooms only: the places nobody took are played by bots.</summary>
            public bool FillBots;
            public State State = State.Lobby;
            public List<Member> Members = new List<Member>();

            public Member Member(int playerId) => Members.FirstOrDefault(m => m.PlayerId == playerId);
            public int TeamCount(int team) => Members.Count(m => m.Team == team);
        }

        public readonly OrderedDict<string, Room> Rooms = new OrderedDict<string, Room>();
        /// <summary>What a host may pick and what a new room starts with (GameRules.first_to*).</summary>
        public List<int> FirstToChoices = new List<int> { 3 };
        public int DefaultFirstTo = 3;
        /// <summary>True on a hotspot host's own server only (set by the owner, never a client message): rooms may fill with bots.</summary>
        public bool BotsAllowed;

        readonly NetRules _rules;
        readonly StreetCatalog _games;
        readonly GodotRng _rng;
        int _nextPlayerId = 1;
        /// <summary>peer id -> room code, for connected members.</summary>
        readonly Dictionary<int, string> _peerRooms = new Dictionary<int, string>();

        public RoomService(NetRules rules, StreetCatalog games, long seed)
        {
            _rules = rules;
            _games = games;
            _rng = new GodotRng(seed);
        }

        public StreetCatalog Games => _games;

        /// <summary>Trimmed (Godot `strip_edges`) and cut to `max_name_length` characters; never empty.</summary>
        public string CleanName(string raw)
        {
            string text = Left(StripEdges(raw ?? ""), _rules.MaxNameLength);
            return text.Length > 0 ? text : "Player";
        }

        /// <summary>Godot's `strip_edges()`: drops every character with a code of 32 or less from both ends.</summary>
        public static string StripEdges(string s)
        {
            int start = 0, end = s.Length;
            while (start < end && s[start] <= 32) start++;
            while (end > start && s[end - 1] <= 32) end--;
            return s.Substring(start, end - start);
        }

        /// <summary>Godot's `left(n)`: the first `n` characters, counting code points as Godot's UTF-32 strings do.</summary>
        public static string Left(string s, int count)
        {
            if (count < 0) count = Math.Max(0, CodePoints(s) + count);
            var builder = new StringBuilder();
            int taken = 0;
            for (int i = 0; i < s.Length && taken < count; i++, taken++)
            {
                builder.Append(s[i]);
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) builder.Append(s[++i]);
            }
            return builder.ToString();
        }

        static int CodePoints(string s)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++, n++)
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) i++;
            return n;
        }

        public Room RoomOfPeer(int peerId) =>
            _peerRooms.TryGetValue(peerId, out string code) && Rooms.TryGetValue(code, out Room room) ? room : null;

        public Member MemberOfPeer(int peerId)
        {
            Room room = RoomOfPeer(peerId);
            return room?.Members.FirstOrDefault(m => m.PeerId == peerId && m.Connected);
        }

        /// <summary>Creates a room; the creator joins as host. Null when the peer is already in one.</summary>
        public Room CreateRoom(int peerId, string playerName, long rankLp = 0)
        {
            if (RoomOfPeer(peerId) != null) return null;
            var room = new Room { Code = NewCode(), FirstTo = DefaultFirstTo };
            Rooms[room.Code] = room;
            Member host = AddMember(room, peerId, playerName, rankLp);
            room.HostPlayerId = host.PlayerId;
            return room;
        }

        public Result Join(string code, int peerId, string playerName, long rankLp = 0)
        {
            Rooms.TryGetValue(StripEdges(code ?? "").ToUpperInvariant(), out Room room);
            if (room == null) return Result.NotFound;
            if (room.State != State.Lobby) return Result.InProgress;
            if (room.Members.Count >= MaxTeamSize * 2) return Result.Full;
            if (RoomOfPeer(peerId) != null) return Result.NotAllowed;
            AddMember(room, peerId, playerName, rankLp);
            return Result.Ok;
        }

        /// <summary>Updates a lobby member's Tagalog Ranked LP and tier name.</summary>
        public bool SetRank(int peerId, long lp)
        {
            Room room = RoomOfPeer(peerId);
            Member m = MemberOfPeer(peerId);
            if (m == null || room == null || room.State != State.Lobby) return false;
            m.RankLp = Math.Max(0, lp);
            m.RankTier = Lobby.TierName(m.RankLp);
            return true;
        }

        /// <summary>Moves a member to team 0 or 1 if it has room and no teammate has their kid. Un-readies them.</summary>
        public bool PickTeam(int peerId, long team)
        {
            Room room = RoomOfPeer(peerId);
            Member m = MemberOfPeer(peerId);
            if (m == null || room.State != State.Lobby || team < 0 || team > 1 || m.Team == team) return false;
            if (room.TeamCount((int)team) >= MaxTeamSize) return false;
            foreach (Member other in room.Members)
                if (other.Team == team && m.Character != "" && other.Character == m.Character) return false;
            m.Team = (int)team;
            m.Ready = false;
            return true;
        }

        public bool SetReady(int peerId, bool ready)
        {
            Room room = RoomOfPeer(peerId);
            Member m = MemberOfPeer(peerId);
            if (m == null || room.State != State.Lobby || (ready && m.Team < 0)) return false;
            m.Ready = ready;
            return true;
        }

        /// <summary>
        /// Why the host cannot start this room yet, in words for the host ("" = nothing in the way). Agawang Base needs players on both
        /// teams, Tumbang Preso `room_min_players` kids, Patintero `room_min_team` on each team; then everyone has a team and is Ready.
        /// `hostReady`: count the host as ready (the room screen's one button readies and starts at once).
        /// </summary>
        public string StartBlocker(Room room, bool hostReady = false)
        {
            if (room.State != State.Lobby) return "The game has already started.";
            StreetModeRules game = _games.RulesFor(room.MapId);
            bool ffa = FreeForAll(room.MapId);
            if (room.FillBots)
            {
                // the bots fill the places, so no game is short of players
            }
            else if (game == null)
            {
                if (ShortOfTeam(room, _rules.BaseRoomMinTeam)) return TeamWords(BaseTitle, _rules.BaseRoomMinTeam);
            }
            else if (ffa)
            {
                if (room.Members.Count < game.RoomMinPlayers)
                    return string.Format(CultureInfo.InvariantCulture, "{0} needs at least {1} players.", game.Title, game.RoomMinPlayers);
            }
            else if (ShortOfTeam(room, game.RoomMinTeam))
            {
                return TeamWords(game.Title, game.RoomMinTeam);
            }
            int waiting = 0;
            foreach (Member m in room.Members)
            {
                if (m.Team < 0 && !ffa) return "Everyone needs a team first.";
                if (!(m.Ready || (hostReady && m.PlayerId == room.HostPlayerId))) waiting++;
            }
            if (waiting > 0)
                return string.Format(CultureInfo.InvariantCulture, "Waiting for {0} player{1} to ready up.", waiting, waiting == 1 ? "" : "s");
            return "";
        }

        public bool CanStart(Room room, bool hostReady = false) => StartBlocker(room, hostReady).Length == 0;

        static bool ShortOfTeam(Room room, int least) => room.TeamCount(0) < least || room.TeamCount(1) < least;

        static string TeamWords(string title, int least) =>
            string.Format(CultureInfo.InvariantCulture, "{0} needs at least {1} player{2} on each team.", title, least, least == 1 ? "" : "s");

        bool IsHostInLobby(int peerId, out Room room, out Member m)
        {
            room = RoomOfPeer(peerId);
            m = MemberOfPeer(peerId);
            return m != null && m.PlayerId == room.HostPlayerId && room.State == State.Lobby;
        }

        /// <summary>Host only, in the lobby: first to 3, 5 or 7 (one of `FirstToChoices`).</summary>
        public bool SetFirstTo(int peerId, long count)
        {
            if (!IsHostInLobby(peerId, out Room room, out _)) return false;
            if (!FirstToChoices.Contains((int)count) || count != (int)count) return false;
            room.FirstTo = (int)count;
            return true;
        }

        /// <summary>Host only, in the lobby: the map the match is played on (one of `PlayableMapIds`).</summary>
        public bool SetMap(int peerId, string mapId)
        {
            if (!IsHostInLobby(peerId, out Room room, out _)) return false;
            if (Array.IndexOf(PlayableMapIds, mapId) < 0) return false;
            room.MapId = mapId;
            if (FreeForAll(mapId))
                // everyone gets a place (the two columns are only places, not teams)
                foreach (Member other in room.Members)
                    if (other.Team < 0) other.Team = room.TeamCount(0) <= room.TeamCount(1) ? 0 : 1;
            return true;
        }

        /// <summary>True for a street game where every kid plays for itself (Tumbang Preso, Langit Lupa).</summary>
        public bool FreeForAll(string mapId) => _games.IsFreeForAll(mapId);

        /// <summary>Host only, in the lobby, and only where `BotsAllowed`: the empty places are played by bots.</summary>
        public bool SetFillBots(int peerId, bool on)
        {
            if (!BotsAllowed || !IsHostInLobby(peerId, out Room room, out _)) return false;
            room.FillBots = on;
            return true;
        }

        /// <summary>
        /// The team of each bot that fills the places left in `room` (none unless `FillBots`): every team up to `MaxTeamSize`, and in a
        /// free-for-all game the room up to ten kids (all on team 0, as the sim plays them).
        /// </summary>
        public List<int> BotTeams(Room room)
        {
            var teams = new List<int>();
            if (!room.FillBots) return teams;
            if (FreeForAll(room.MapId))
            {
                for (int i = 0; i < Math.Max(MaxTeamSize * 2 - room.Members.Count, 0); i++) teams.Add(0);
                return teams;
            }
            for (int team = 0; team < 2; team++)
                for (int i = 0; i < Math.Max(MaxTeamSize - room.TeamCount(team), 0); i++) teams.Add(team);
            return teams;
        }

        /// <summary>Host only, in the lobby: open (listed) or private (code only).</summary>
        public bool SetOpen(int peerId, bool open)
        {
            if (!IsHostInLobby(peerId, out Room room, out _)) return false;
            room.Open = open;
            return true;
        }

        /// <summary>The rooms anyone can join with a tap: open, in the lobby, not full; the fullest first, at most `limit` (Array[Dictionary]).</summary>
        public GArray OpenRooms(int limit)
        {
            var found = Rooms.Values.Where(r => r.Open && r.State == State.Lobby && r.Members.Count < MaxTeamSize * 2).ToList();
            GodotSort.SortCustom(found, (a, b) => a.Members.Count > b.Members.Count);
            var listed = new GArray(VariantType.Dictionary);
            foreach (Room room in found.Take(Math.Max(limit, 0)))
            {
                Member host = room.Member(room.HostPlayerId);
                listed.Add(GDict.Of("code", room.Code, "host_name", host != null ? host.Name : "", "team_size", (long)MaxTeamSize,
                    "players", (long)room.Members.Count, "first_to", (long)room.FirstTo, "map", new StringName(room.MapId)));
            }
            return listed;
        }

        /// <summary>Any member, in the lobby: the kid they will play (one of `Lobby.Avatars`), not one a teammate has.</summary>
        public bool SetCharacter(int peerId, string character)
        {
            Room room = RoomOfPeer(peerId);
            Member m = MemberOfPeer(peerId);
            if (m == null || room.State != State.Lobby || Array.IndexOf(Lobby.Avatars, character) < 0) return false;
            foreach (Member other in room.Members)
                if (other != m && other.Team == m.Team && other.Character == character) return false;
            m.Character = character;
            m.Ready = false;
            return true;
        }

        /// <summary>Host only. The room moves to Loading.</summary>
        public bool Start(int peerId)
        {
            Room room = RoomOfPeer(peerId);
            Member m = MemberOfPeer(peerId);
            if (m == null || m.PlayerId != room.HostPlayerId || !CanStart(room)) return false;
            room.State = State.Loading;
            return true;
        }

        /// <summary>Leaving the lobby (or a lost connection there). The next member becomes host; an empty room closes (null).</summary>
        public Room Leave(int peerId)
        {
            Room room = RoomOfPeer(peerId);
            Member m = MemberOfPeer(peerId);
            _peerRooms.Remove(peerId);
            if (room == null || m == null) return null;
            room.Members.Remove(m);
            if (room.Members.Count == 0)
            {
                Rooms.Remove(room.Code);
                return null;
            }
            if (room.HostPlayerId == m.PlayerId) room.HostPlayerId = room.Members[0].PlayerId;
            return room;
        }

        /// <summary>A connection dropped. In the lobby that is a leave; in a running match the slot is kept for `reconnect_window` s.</summary>
        public Room Disconnected(int peerId)
        {
            Room room = RoomOfPeer(peerId);
            if (room == null) return null;
            if (room.State == State.Lobby) return Leave(peerId);
            Member m = MemberOfPeer(peerId);
            _peerRooms.Remove(peerId);
            if (m != null)
            {
                m.Connected = false;
                m.ReconnectLeft = _rules.ReconnectWindow;
                m.GoneFor = 0.0;
            }
            return room;
        }

        /// <summary>Takes a dropped slot back with its token. Returns the member, or null.</summary>
        public Member Reconnect(string token, int peerId)
        {
            if (string.IsNullOrEmpty(token) || RoomOfPeer(peerId) != null) return null;
            foreach (var pair in Rooms)
                foreach (Member m in pair.Value.Members)
                    if (m.Token == token && !m.Connected && m.ReconnectLeft > 0.0)
                    {
                        m.Connected = true;
                        m.PeerId = peerId;
                        m.ReconnectLeft = 0.0;
                        m.GoneFor = 0.0;
                        m.Afk = false;
                        _peerRooms[peerId] = pair.Key;
                        return m;
                    }
            return null;
        }

        /// <summary>
        /// Counts reconnect windows down. A slot whose window ran out stays in the match (the player stands still); its token stops
        /// working. A room nobody can come back to closes. Returns the rooms where someone just turned away (AFK).
        /// </summary>
        public List<Room> Step(double dt)
        {
            var changed = new List<Room>();
            foreach (string code in Rooms.KeyList())
            {
                Room room = Rooms[code];
                bool anyone = false;
                foreach (Member m in room.Members)
                {
                    if (!m.Connected && m.ReconnectLeft > 0.0) m.ReconnectLeft = Math.Max(0.0, m.ReconnectLeft - dt);
                    if (!m.Connected && room.State != State.Lobby)
                    {
                        m.GoneFor += dt;
                        if (!m.Afk && m.GoneFor >= _rules.AfkAfter)
                        {
                            m.Afk = true;
                            if (!changed.Contains(room)) changed.Add(room);
                        }
                    }
                    if (m.Connected || m.ReconnectLeft > 0.0) anyone = true;
                }
                if (!anyone) Rooms.Remove(code);
            }
            return changed;
        }

        /// <summary>Plain data for the ROOM message (room_service.gd `describe`).</summary>
        public GDict Describe(Room room)
        {
            var members = new GArray(VariantType.Dictionary);
            foreach (Member m in room.Members)
                members.Add(GDict.Of("player_id", (long)m.PlayerId, "name", m.Name, "team", (long)m.Team, "ready", m.Ready,
                    "connected", m.Connected, "afk", m.Afk, "char", new StringName(m.Character), "rank_lp", m.RankLp, "rank_tier", m.RankTier));
            return GDict.Of(
                "code", room.Code, "team_size", (long)MaxTeamSize, "host", (long)room.HostPlayerId, "first_to", (long)room.FirstTo,
                "state", (long)room.State, "members", members, "can_start", CanStart(room),
                "can_start_host", CanStart(room, true), "start_note", StartBlocker(room, true), "map", new StringName(room.MapId),
                "open", room.Open, "fill_bots", room.FillBots, "bots_allowed", BotsAllowed);
        }

        Member AddMember(Room room, int peerId, string playerName, long rankLp)
        {
            var m = new Member { PlayerId = _nextPlayerId++, PeerId = peerId, Name = CleanName(playerName) };
            m.RankLp = Math.Max(0, rankLp);
            m.RankTier = Lobby.TierName(m.RankLp);
            m.Token = _rng.Randi().ToString("x8", CultureInfo.InvariantCulture) + _rng.Randi().ToString("x8", CultureInfo.InvariantCulture);
            // drop into the smaller team (ties go to team 0)
            int team = room.TeamCount(0) <= room.TeamCount(1) ? 0 : 1;
            if (room.TeamCount(team) < MaxTeamSize) m.Team = team;
            foreach (string avatar in Lobby.Avatars)
                if (!room.Members.Any(other => other.Team == m.Team && other.Character == avatar))
                {
                    m.Character = avatar;
                    break;
                }
            room.Members.Add(m);
            _peerRooms[peerId] = room.Code;
            return m;
        }

        string NewCode()
        {
            while (true)
            {
                var code = new StringBuilder();
                for (int i = 0; i < _rules.PasscodeLength; i++)
                    code.Append(_rules.PasscodeAlphabet[_rng.RandiRange(0, _rules.PasscodeAlphabet.Length - 1)]);
                if (!Rooms.ContainsKey(code.ToString())) return code.ToString();
            }
        }
    }
}
