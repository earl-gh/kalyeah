using System;
using System.Collections.Generic;
using System.Linq;
using Kalyeah.Sim;

namespace Kalyeah.Net
{
    /// <summary>Where a ServerMatch sends its messages (the GameServer).</summary>
    public interface IMatchOutput
    {
        void Send(int peerId, GDict message);
    }

    /// <summary>
    /// One running match on the server (port of server_match.gd): loading sync, the weapon pick, then the authoritative MatchSim stepped
    /// at 30 Hz from queued client inputs, with snapshots at `snapshot_rate` and the sim's signals relayed as EVENTS. A room on a street
    /// game plays that game instead (no weapon pick, a random team runs first, every signal of the game relayed under "mode").
    /// </summary>
    public sealed class ServerMatch
    {
        public enum Stage { Loading, Picking, Playing }

        /// <summary>
        /// One-shot pulse buttons cleared when the last input repeats on an empty queue tick; held buttons (primary, crouch, the cards,
        /// the aim point) stay held.
        /// </summary>
        public const int PulseBits = PlayerInput.BtnMoveTarget | PlayerInput.BtnDash | PlayerInput.BtnUltimate | PlayerInput.BtnJump | PlayerInput.BtnSlide;

        public readonly RoomService.Room Room;
        public readonly MatchSim Sim;
        public Stage CurrentStage = Stage.Loading;
        public double StageLeft;
        public readonly long SeedValue;
        /// <summary>The bots of a room whose host chose "fill with bots" (id -> name): sim players driven here, never room members.</summary>
        public readonly OrderedDict<int, string> Bots = new OrderedDict<int, string>();

        IMatchOutput _server;
        readonly NetRules _net;
        readonly OrderedDict<int, bool> _loaded = new OrderedDict<int, bool>();
        readonly OrderedDict<int, bool> _picked = new OrderedDict<int, bool>();
        readonly List<TeamCoach> _coaches = new List<TeamCoach>();
        StreetModeCoach _modeCoach;
        readonly Dictionary<int, List<PlayerInput>> _queues = new Dictionary<int, List<PlayerInput>>();
        readonly Dictionary<int, PlayerInput> _lastInput = new Dictionary<int, PlayerInput>();
        readonly OrderedDict<int, int> _acks = new OrderedDict<int, int>();
        readonly GArray _events = new GArray(VariantType.Array);
        readonly List<Action> _unsubscribe = new List<Action>();
        double _accumulator;
        double _snapshotAccumulator;

        public ServerMatch(IMatchOutput server, RoomService rooms, RoomService.Room room, GameRules rules, MapLayout layout, NetRules net, long seed)
        {
            _server = server;
            Room = room;
            _net = net;
            SeedValue = seed;
            Sim = new MatchSim(rules, layout, seed);
            Sim.SetFirstTo(room.FirstTo);
            StreetModeRules game = rooms.Games.RulesFor(room.MapId);
            StreetMode mode = game != null ? rooms.Games.Create(game) : null;
            foreach (RoomService.Member m in room.Members)
            {
                // Tumbang Preso / Langit Lupa: every kid for itself, all on one team in the sim (as in Practice)
                Sim.AddPlayer(m.PlayerId, mode != null && mode.IsFreeForAll() ? 0 : m.Team);
                _queues[m.PlayerId] = new List<PlayerInput>();
                _acks[m.PlayerId] = -1;
            }
            AddBots(rooms, mode);
            Sim.AssignCharacters();
            // explicit lobby choices after the seeded defaults (SetCharacter swaps a teammate's random default away)
            foreach (RoomService.Member m in room.Members)
                if (m.Character != "") Sim.SetCharacter(m.PlayerId, m.Character);
            if (mode != null)
            {
                mode.AttackTeam = Sim.Rng.RandiRange(0, 1); // owner: in a room, a random team runs first (Patintero)
                Sim.StartMode(mode); // no skills in these games: nobody is dealt a hand
            }
            else
            {
                foreach (int id in Sim.Players.KeyList()) Sim.Equip(id, Bots.ContainsKey(id));
            }
            StageLeft = net.LoadTimeout;
            RelaySignals();
        }

        StreetMode Mode => Sim.Mode as StreetMode;

        /// <summary>Fills the places the host left empty with bots, after the people (each team's order is theirs first).</summary>
        void AddBots(RoomService rooms, StreetMode mode)
        {
            var coaches = new OrderedDict<int, TeamCoach>();
            if (mode != null) _modeCoach = new StreetModeCoach();
            foreach (int team in rooms.BotTeams(Room))
            {
                int id = RoomService.BotIdBase + Bots.Count;
                Sim.AddPlayer(id, team);
                Bots[id] = "Bot " + (Bots.Count + 1);
                if (mode != null)
                {
                    _modeCoach.Add(id);
                    continue;
                }
                if (!coaches.ContainsKey(team)) coaches[team] = new TeamCoach(team);
                coaches[team].Add(Sim, id, Sim.Rules.BotSkill);
            }
            _coaches.AddRange(coaches.Values);
        }

        /// <summary>Unhooks the sim's events and the server so the match can be dropped.</summary>
        public void Dispose()
        {
            foreach (Action off in _unsubscribe) off();
            _unsubscribe.Clear();
            _server = null;
        }

        /// <summary>MATCH_LOADING: everything a client needs to build its mirror sim.</summary>
        public GDict Info()
        {
            var players = new GArray(VariantType.Dictionary);
            foreach (RoomService.Member m in Room.Members)
                players.Add(GDict.Of("id", (long)m.PlayerId, "team", (long)Sim.Players[m.PlayerId].Team, "name", m.Name,
                    "char", new StringName(Sim.Players[m.PlayerId].CharacterId)));
            foreach (var bot in Bots)
                players.Add(GDict.Of("id", (long)bot.Key, "team", (long)Sim.Players[bot.Key].Team, "name", bot.Value,
                    "char", new StringName(Sim.Players[bot.Key].CharacterId)));
            return NetProtocol.Message(NetProtocol.Msg.MatchLoading, "seed", SeedValue, "team_size", (long)RoomService.MaxTeamSize,
                "players", players, "game", Room.MapId, "runs", (long)(Mode != null ? Mode.AttackTeam : 0));
        }

        /// <summary>MATCH_PLAY: the current stage (pick or play) for a client joining or catching up.</summary>
        public GDict StageMessage()
        {
            var loadouts = new GDict();
            var chars = new GDict();
            var locked = new GArray(VariantType.Int);
            foreach (var pair in _picked) locked.Add((long)pair.Key);
            foreach (var pair in Sim.Players)
            {
                loadouts[(long)pair.Key] = GArray.StringNames(pair.Value.Weapons);
                chars[(long)pair.Key] = new StringName(pair.Value.CharacterId);
            }
            return NetProtocol.Message(NetProtocol.Msg.MatchPlay, "stage", CurrentStage == Stage.Playing ? "play" : "pick", "time", StageLeft,
                "loadouts", loadouts, "chars", chars, "locked", locked);
        }

        public void Loaded(int playerId) => _loaded[playerId] = true;

        /// <summary>First input number a reconnected client can safely send (inputs queued before its socket dropped count too).</summary>
        public int NextInputSequence(int playerId)
        {
            int last = _acks.TryGetValue(playerId, out int ack) ? ack : -1;
            if (_queues.TryGetValue(playerId, out List<PlayerInput> queue))
                foreach (PlayerInput item in queue) last = Math.Max(last, item.Tick);
            return last + 1;
        }

        /// <summary>
        /// A weapon pick (taken during loading too). `character` is the kid the player chose ("" keeps the random one); a kid a
        /// confirmed teammate plays is not taken from them. Ignored once the match plays (a late or forged pick changes nothing).
        /// </summary>
        public void Pick(int playerId, string first, string second, string character = "", string third = "")
        {
            if (CurrentStage == Stage.Playing) return;
            // cards mode: nobody picks weapons, the hand was dealt at the start; only the kid comes with the confirmation
            bool accepted = Sim.Rules.CardCycle || Sim.SetLoadout(playerId, first, second, third);
            if (!accepted) return;
            _picked[playerId] = true;
            if (character != "" && !TakenKids(playerId).Contains(character)) Sim.SetCharacter(playerId, character);
            // everyone learns who is locked in, so their select screens grey those kids out
            Broadcast(StageMessage());
        }

        /// <summary>The kids `playerId` cannot take: the ones its confirmed teammates play.</summary>
        public List<string> TakenKids(int playerId) => Sim.TeammatesKids(playerId, _picked.Keys);

        public void Swap(int playerId, string first, string second, string third = "")
        {
            if (CurrentStage == Stage.Playing) Sim.SwapLoadout(playerId, first, second, third);
        }

        public void QueueInput(int playerId, PlayerInput input)
        {
            if (!_queues.TryGetValue(playerId, out List<PlayerInput> queue) || input.Tick <= _acks[playerId]) return;
            queue.Add(input);
            while (queue.Count > _net.MaxQueuedInputs) queue.RemoveAt(0);
        }

        public void Poll(double delta)
        {
            switch (CurrentStage)
            {
                case Stage.Loading:
                    StageLeft -= delta;
                    if (StageLeft <= 0.0 || EveryoneIn(_loaded))
                    {
                        if (Sim.Mode != null)
                        {
                            StartPlay(); // a street game has no weapon pick
                            return;
                        }
                        CurrentStage = Stage.Picking;
                        StageLeft = Sim.Rules.WeaponPickTime + _net.PickGrace;
                        Broadcast(StageMessage());
                    }
                    break;
                case Stage.Picking:
                    StageLeft -= delta;
                    if (StageLeft <= 0.0 || EveryoneIn(_picked)) StartPlay();
                    break;
                case Stage.Playing:
                    Play(delta);
                    break;
            }
        }

        void StartPlay()
        {
            CurrentStage = Stage.Playing;
            Room.State = RoomService.State.Playing;
            Sim.BeginIntro();
            Broadcast(StageMessage());
        }

        void Play(double delta)
        {
            int away = AwayTeam();
            if (away >= 0 && Sim.Phase != MatchSim.MatchPhase.MatchOver) Sim.Forfeit(away);
            double dt = Sim.Rules.TickDt();
            _accumulator += delta;
            while (_accumulator >= dt)
            {
                _accumulator -= dt;
                foreach (TeamCoach coach in _coaches) coach.Drive(Sim);
                _modeCoach?.Drive(Sim);
                foreach (RoomService.Member m in Room.Members) Sim.SetInput(m.PlayerId, NextInput(m));
                Sim.Step(dt);
            }
            _snapshotAccumulator += delta;
            if (_events.Count > 0)
            {
                Broadcast(NetProtocol.Message(NetProtocol.Msg.Events, "list", _events.Duplicate()));
                _events.Clear();
            }
            if (_snapshotAccumulator >= 1.0 / _net.SnapshotRate)
            {
                _snapshotAccumulator = 0.0;
                SendSnapshots(Snapshot.Capture(Sim, _acks));
            }
        }

        /// <summary>The team whose every player is away (AFK), or -1.</summary>
        int AwayTeam()
        {
            for (int team = 0; team < 2; team++)
            {
                int players = Room.Members.Count(m => m.Team == team);
                int away = Room.Members.Count(m => m.Team == team && m.Afk);
                if (players > 0 && away == players) return team;
            }
            return -1;
        }

        /// <summary>The same public snapshot to everyone, each with only its own private part and without what it may not see.</summary>
        void SendSnapshots(GDict publicPart)
        {
            if (_server == null) return;
            foreach (RoomService.Member m in Room.Members)
            {
                if (!m.Connected) continue;
                GDict message = publicPart.Duplicate();
                message.Merge(Snapshot.PrivateFor(Sim, m.PlayerId));
                _server.Send(m.PeerId, Snapshot.HideFor(Sim, m.PlayerId, message));
            }
        }

        /// <summary>
        /// One input per tick per player, oldest first, so taps are never lost. With none queued the last one repeats (held buttons stay
        /// held, one-shot orders are not given twice); a dropped player stands still.
        /// </summary>
        PlayerInput NextInput(RoomService.Member m)
        {
            if (!m.Connected)
            {
                if (Sim.Players.TryGetValue(m.PlayerId, out PlayerState state)) Sim.EndMove(state);
                return PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, Sim.Tick);
            }
            List<PlayerInput> queue = _queues[m.PlayerId];
            if (queue.Count > 0)
            {
                PlayerInput front = queue[0];
                queue.RemoveAt(0);
                _lastInput[m.PlayerId] = front;
                _acks[m.PlayerId] = front.Tick;
                return front;
            }
            if (_lastInput.TryGetValue(m.PlayerId, out PlayerInput repeated))
            {
                repeated.Buttons &= ~PulseBits;
                return repeated;
            }
            return PlayerInput.Create(Vec2.Zero, Vec2.Zero, 0, Sim.Tick);
        }

        bool EveryoneIn(OrderedDict<int, bool> flags) => Room.Members.All(m => !m.Connected || flags.ContainsKey(m.PlayerId));

        /// <summary>A quick chat sticker goes to every connected player of the room, the sender too.</summary>
        public void Say(int playerId, long sticker) => Broadcast(NetProtocol.Message(NetProtocol.Msg.Said, "p", (long)playerId, "s", sticker));

        void Broadcast(GDict message)
        {
            if (_server == null) return;
            foreach (RoomService.Member m in Room.Members)
                if (m.Connected) _server.Send(m.PeerId, message);
        }

        void Push(string target, string signal, params object[] args) => _events.Add(GArray.Of(target, signal, new GArray(args)));

        /// <summary>The sim's signals relayed to clients (server_match.gd RELAYED, with Godot's argument types).</summary>
        void RelaySignals()
        {
            MatchSim sim = Sim;
            Action<int, int> tagged = (a, b) => Push("sim", "player_tagged", (long)a, (long)b);
            Action<int, int> rescued = (a, b) => Push("sim", "player_rescued", (long)a, (long)b);
            Action<int, int> scored = (a, b) => Push("sim", "point_scored", (long)a, (long)b);
            Action<int> won = a => Push("sim", "match_won", (long)a);
            Action reset = () => Push("sim", "point_reset");
            Action<int, string> cast = (a, w) => Push("sim", "weapon_cast", (long)a, new StringName(w));
            Action<int, string> hit = (a, w) => Push("sim", "weapon_hit", (long)a, new StringName(w));
            Action<int, int> damaged = (a, b) => Push("sim", "player_damaged", (long)a, (long)b);
            Action<int, int> healed = (a, b) => Push("sim", "player_healed", (long)a, (long)b);
            Action<int, StatusEffects.Type> effect = (a, t) => Push("sim", "effect_applied", (long)a, (long)t);
            Action<int> exhausted = a => Push("sim", "exhausted", (long)a);
            Action<int> dashed = a => Push("sim", "player_dashed", (long)a);
            Action<int> ultimate = a => Push("sim", "ultimate_cast", (long)a);
            Action<int, int, int> powerup = (a, b, c) => Push("sim", "powerup_taken", (long)a, (long)b, (long)c);
            Action<int, WeaponDef, Vec2, Vec2> struck = (a, d, o, dir) => Push("weapons", "cone_struck", (long)a, new StringName(d.Id), o, dir);
            Action<int, WeaponDef, Vec2, Vec2> winding = (a, d, o, dir) => Push("weapons", "cone_winding", (long)a, new StringName(d.Id), o, dir);
            sim.PlayerTagged += tagged;
            sim.PlayerRescued += rescued;
            sim.PointScored += scored;
            sim.MatchWon += won;
            sim.PointReset += reset;
            sim.WeaponCast += cast;
            sim.WeaponHit += hit;
            sim.PlayerDamaged += damaged;
            sim.PlayerHealed += healed;
            sim.EffectApplied += effect;
            sim.Exhausted += exhausted;
            sim.PlayerDashed += dashed;
            sim.UltimateCast += ultimate;
            sim.PowerupTaken += powerup;
            sim.Weapons.ConeStruck += struck;
            sim.Weapons.ConeWinding += winding;
            _unsubscribe.Add(() =>
            {
                sim.PlayerTagged -= tagged;
                sim.PlayerRescued -= rescued;
                sim.PointScored -= scored;
                sim.MatchWon -= won;
                sim.PointReset -= reset;
                sim.WeaponCast -= cast;
                sim.WeaponHit -= hit;
                sim.PlayerDamaged -= damaged;
                sim.PlayerHealed -= healed;
                sim.EffectApplied -= effect;
                sim.Exhausted -= exhausted;
                sim.PlayerDashed -= dashed;
                sim.UltimateCast -= ultimate;
                sim.PowerupTaken -= powerup;
                sim.Weapons.ConeStruck -= struck;
                sim.Weapons.ConeWinding -= winding;
            });
            if (Mode != null)
            {
                // every signal the street game declares (tags, points, role swaps, dives...): each phone's views play them
                _unsubscribe.Add(StreetWire.Relay(Mode, (name, args) => _events.Add(GArray.Of("mode", name, args))));
            }
        }
    }
}
