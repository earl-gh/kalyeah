using System;
using System.Collections.Generic;
using Kalyeah.Net;

namespace Kalyeah.UI
{
    /// <summary>
    /// What the room screen shows for one ROOM message (port of hotspot_room_view.gd show_room, room_player_cards.gd and
    /// room_character_cards.gd), with no widget in it: the header, the host's settings or a guest's summary, the two team columns
    /// (a card for each player, an open slot for each free place), the kid strip and the one big button. The server owns the room;
    /// this only reads it.
    /// </summary>
    public sealed class RoomViewModel
    {
        public sealed class Slot
        {
            public int Team;
            public int Row;
            /// <summary>The number in the team block: team * size + row + 1.</summary>
            public int Number;
            /// <summary>-1 for an open slot.</summary>
            public int PlayerId = -1;
            public string Name = "";
            public string Kid = "";
            /// <summary>"You · Host", "Ready", "Host"... ("" for nobody special).</summary>
            public string Tag = "";
            public bool Ready;
            public bool Connected = true;
            /// <summary>An open slot on the other team: tapping it moves you there.</summary>
            public bool CanJoin;
            public bool IsOpen => PlayerId < 0;
        }

        public sealed class KidChoice
        {
            public string Id;
            public bool Picked;
            /// <summary>A teammate plays this kid already (the other team may).</summary>
            public bool Taken;
        }

        public string Code = "";
        public bool Online;
        public bool IsHost;
        public int MyId = -1;
        public int MyTeam = -1;
        public string MapId = "street";
        public string MapName = "";
        public int TeamSize = RoomService.MaxTeamSize;
        public int FirstTo = 3;
        /// <summary>Agawang Base has a Race to; a street game is timed and has none.</summary>
        public bool ShowRace;
        /// <summary>Every kid for itself (Tumbang Preso, Langit Lupa): no team headers.</summary>
        public bool FreeForAll;
        public bool Open = true;
        /// <summary>The host's Open / Private switch (Online rooms only).</summary>
        public bool ShowOpenSwitch;
        /// <summary>A Local room: the Fill with bots row.</summary>
        public bool ShowBots;
        public bool FillBots;
        /// <summary>A guest's line under the rows: "Open" or "Private" (Online only).</summary>
        public string Summary = "";
        public string Readiness = MenuCatalog.ReadyToPlay;
        /// <summary>The server's reason a start is blocked ("" when the host can start).</summary>
        public string StartNote = "";
        public string ActionText = "Ready";
        /// <summary>Drawn in the hot colour (orange) rather than grey.</summary>
        public bool ActionHot = true;
        public bool MyReady;
        public string MyKid = "";
        /// <summary>[team][row], `TeamSize` rows a team.</summary>
        public readonly List<Slot>[] Teams = { new List<Slot>(), new List<Slot>() };
        /// <summary>Players with no team yet (none in a 5v5 room that is not full).</summary>
        public readonly List<Slot> Unplaced = new List<Slot>();
        public readonly List<KidChoice> Kids = new List<KidChoice>();

        public static int SlotNumber(int team, int row, int size) => team * size + row + 1;

        /// <summary>Reads `room` (GameClient.Room) for player `myId`. `data` tells street games from Agawang Base (null: Agawang Base).</summary>
        public static RoomViewModel From(GDict room, int myId, bool online, MenuData data, string fallbackKid = "")
        {
            var view = new RoomViewModel { MyId = myId, Online = online };
            view.Code = online ? room.GetStr("code") : "";
            int host = room.GetInt("host", -1);
            view.IsHost = host == myId && myId >= 0;
            view.MapId = room.GetStr("map", "street");
            view.MapName = MenuCatalog.MapName(view.MapId);
            view.TeamSize = Math.Max(1, Math.Min(RoomService.MaxTeamSize, room.GetInt("team_size", RoomService.MaxTeamSize)));
            view.FirstTo = room.GetInt("first_to", 3);
            view.ShowRace = data == null || data.StreetRules(view.MapId) == null;
            view.FreeForAll = data != null && data.FreeForAll(view.MapId);
            view.Open = room.GetBool("open", true);
            view.ShowOpenSwitch = view.IsHost && online;
            view.ShowBots = room.GetBool("bots_allowed");
            view.FillBots = room.GetBool("fill_bots");
            view.Summary = online && !view.IsHost ? (view.Open ? "Open" : "Private") : "";
            view.StartNote = room.GetStr("start_note");
            view.Readiness = view.StartNote.Length > 0 ? view.StartNote : MenuCatalog.ReadyToPlay;

            var members = new List<GDict>();
            foreach (object raw in room.GetArr("members"))
                if (raw is GDict member) members.Add(member);
            GDict me = null;
            foreach (GDict member in members)
                if (member.GetInt("player_id", -2) == myId) me = member;
            view.MyTeam = me != null ? me.GetInt("team", -1) : -1;
            view.MyReady = me != null && me.GetBool("ready");
            view.MyKid = me != null ? me.GetStr("char") : "";
            if (view.MyKid.Length == 0) view.MyKid = fallbackKid ?? "";

            var byTeam = new[] { new List<GDict>(), new List<GDict>() };
            foreach (GDict member in members)
            {
                int team = member.GetInt("team", -1);
                if (team == 0 || team == 1) byTeam[team].Add(member);
                else view.Unplaced.Add(PlayerSlot(member, -1, 0, view.TeamSize, host, myId));
            }
            for (int team = 0; team < 2; team++)
                for (int row = 0; row < view.TeamSize; row++)
                {
                    if (row < byTeam[team].Count) view.Teams[team].Add(PlayerSlot(byTeam[team][row], team, row, view.TeamSize, host, myId));
                    else view.Teams[team].Add(new Slot { Team = team, Row = row, Number = SlotNumber(team, row, view.TeamSize), CanJoin = view.MyTeam != team });
                }

            foreach (string id in SessionModel.Avatars)
            {
                bool taken = false;
                foreach (GDict member in members)
                    if (member.GetInt("player_id", -1) != myId && member.GetInt("team", -1) == view.MyTeam && member.GetStr("char") == id) taken = true;
                view.Kids.Add(new KidChoice { Id = id, Picked = id == view.MyKid, Taken = taken });
            }

            if (view.IsHost)
            {
                // always pressable: when the game cannot start yet, pressing it says why (start_note)
                view.ActionText = "Start battle";
                view.ActionHot = room.GetBool("can_start_host");
            }
            else
            {
                view.ActionText = view.MyReady ? "Cancel ready" : "Ready";
                view.ActionHot = !view.MyReady;
            }
            return view;
        }

        static Slot PlayerSlot(GDict member, int team, int row, int size, int host, int myId)
        {
            int id = member.GetInt("player_id", -1);
            bool ready = member.GetBool("ready");
            string tag = id == host ? "Host" : ready ? "Ready" : "";
            if (id == myId) tag = "You" + (tag.Length > 0 ? " · " + tag : "");
            if (!member.GetBool("connected", true)) tag = tag.Length > 0 ? tag + " · Away" : "Away";
            return new Slot
            {
                Team = team, Row = row, Number = team >= 0 ? SlotNumber(team, row, size) : 0, PlayerId = id, Name = member.GetStr("name"),
                Kid = member.GetStr("char"), Tag = tag, Ready = ready, Connected = member.GetBool("connected", true),
            };
        }

        /// <summary>Who hosts `room` (their name, "" when unknown).</summary>
        public static string HostName(GDict room)
        {
            int host = room.GetInt("host", -1);
            foreach (object raw in room.GetArr("members"))
                if (raw is GDict member && member.GetInt("player_id", -2) == host) return member.GetStr("name");
            return "";
        }
    }
}
