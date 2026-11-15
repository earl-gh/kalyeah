// =====================================================================================================================================
// SimBridge: the ONE place the Unity game touches a match (unity/Kalyeah.Sim's MatchSim, its bots and street games, and a GameClient's
// mirror online). It is scripts/view/practice_match.gd without the nodes: it sets a match up (a Practice match against bots, a Practice
// street game, or an online match), steps it at the fixed tick, turns the HUD's gestures into PlayerInput (MatchInput), and copies out
// what the view draws (KidSnapshot, LocalHud, ScoreHud, StreetPadState) plus the presentation's events (banners, the feed, hits).
// Engine-free: it compiles in plain .NET (unity/Kalyeah.Game.Tests runs it) and in Unity (Kalyeah.Game.Core).
//
// SIM API USED (namespace Kalyeah.Sim / Kalyeah.Net; Vec2 is Godot's float32 Vector2, X = x and Y = z on the ground):
//   SimData (AddFile per unity/Data json, following $refs) -> GameRules (WithBotLevel), MapLayout, StreetModeRules
//   MatchSim: AddPlayer, Equip, SetLoadoutList, AssignCharacters, SetCharacter, StartMode, SetInput, Step, BeginIntro, Rng, Players, Tick,
//       Phase, IntroLeft, MatchTime, RoundTime, Score, PointLog, CaptureTeam/Kind/Elapsed/Needed, RescueRescuer/Elapsed, ClockLeft,
//       GetClockStage, CountsDown, CaptureTimeLeft, WeaponReady, LacksTarget, CastingBlocked, PaysElixir, WeaponDefs, Layout, Mode,
//       events PlayerTagged, PlayerRescued, PointScored, MatchWon, IntroFinished, PlayerDamaged, PlayerDashed, UltimateCast, WeaponHit
//   TeamCoach (Agawang Base bots), StreetModeCoach (street game bots), StreetMode / TumbangPreso / Patintero / LangitLupa
//   GameClient (online): Mirror, PlayerId, Poll, Interpolate, SendInput, Loaded, PickWeapons, Stage, StageChanged, RoomClosed, LeaveRoom
// =====================================================================================================================================
using System;
using System.Collections.Generic;
using System.Numerics;
using Kalyeah.Net;
using Kalyeah.Sim;

namespace Kalyeah.Game
{
    /// <summary>What the view draws of one kid, copied out of the sim after each tick.</summary>
    public struct KidSnapshot
    {
        public int Id;
        public int Team;
        public string CharacterId;
        /// <summary>Feet on the ground, sim XZ (GodotSpace.World makes it a Unity point).</summary>
        public Vector2 Position;
        public Vector2 Facing;
        public int Hp;
        public bool Tagged;
        /// <summary>How fast it moved this tick, as a share of a full run (0..1): idle or run.</summary>
        public float Speed01;
        /// <summary>Metres off the ground (a jump), crouching, sliding.</summary>
        public float AirY;
        public bool Crouching, Sliding;
        /// <summary>Overhead (overhead_hud.gd): the number badge, the kid's name, the status words ("Stunned · Slowed", "AFK"), and whether
        /// this kid plays against you (the other team, or the taya: its health is NEVER shown, owner rule).</summary>
        public int Number;
        public string Name;
        public string Status;
        public bool Enemy;
        /// <summary>The overhead health bar is drawn (not tagged, not hidden, and not an enemy).</summary>
        public bool HealthShown;
        /// <summary>Hidden from you (Langit Lupa's brownout): nothing over it.</summary>
        public bool Hidden;
        /// <summary>A progress ring over the kid, 0..1, or -1: capturing the enemy base (gold) or rescuing (green).</summary>
        public float CaptureProgress, RescueProgress;
        public bool IsLocal, IsTaya;
    }

    /// <summary>One card or superpower of your hand, as the arc draws it.</summary>
    public struct CardInfo
    {
        public string Id, Name;
        public int Cost;
        public bool Pickable, IsHeal;
        /// <summary>How close to ready (0..1): the bar against its cost, or its cooldown.</summary>
        public float Reached;
        public float CooldownLeft;
        /// <summary>A passing note on the card ("NO TARGET"), or "".</summary>
        public string Notice;
        public bool Present => !string.IsNullOrEmpty(Id);
    }

    /// <summary>The local player's controls state (card_hand.gd / ability_arc.gd show_state, match_hud.gd sync_player).</summary>
    public sealed class LocalHud
    {
        /// <summary>Gestures may start (free, playing, controls on). `Shown`: the controls are drawn (also during the opening show).</summary>
        public bool Playing, Shown, Tagged;
        /// <summary>What stops casting now ("STUNNED", "CAPTURING", ...), or "".</summary>
        public string Block = "";
        public bool UsesElixir;
        public float Elixir, ElixirMax;
        public readonly CardInfo[] Cards = new CardInfo[ArcLayout.Slots];
        public string NextId = "", NextName = "";
        /// <summary>NEXT's fill while a shuffle runs (1: ready).</summary>
        public float NextFill = 1f;
        public string PrimaryId = "", UltId = "";
        public float PrimaryCdLeft, PrimaryCdShare = 1f, UltCharge;
        public bool UltReady, VaultReady, VaultActive, JumpReady, InAir, CanMove, CanSlide, Sliding, Crouching, Running;
        public float VaultCdLeft, VaultCdShare = 1f, JumpCdShare = 1f, SlideCdLeft, SlideCdShare = 1f;
        /// <summary>The stamina bar under the ice water bar: shown, its share, and its tone (0 normal, 1 low, 2 exhausted).</summary>
        public bool StaminaShown;
        public float StaminaShare = 1f;
        public int StaminaTone;
        public int Hp, MaxHp;
        public bool HoldingCard;
    }

    /// <summary>The top of the screen and the overlays: score, clock, capture, point freeze and results.</summary>
    public sealed class ScoreHud
    {
        public int Mine, Theirs;
        public ClockDisplay Clock;
        public MatchPhaseView Phase;
        /// <summary>"Enemy capturing base / DEFEND THE BASE!" stays up while they hold your base.</summary>
        public bool CaptureAlert;
        /// <summary>"HOLD THE BASE!\n7", "ALL TAGGED!\n3", or "" (match_hud.gd _sync_capture).</summary>
        public string CaptureLabel = "";
        public bool CaptureOurs;
        /// <summary>The point freeze's words (point_result.gd): "Enemy base" / "CAPTURED" / "2 - 1"; empty outside a freeze.</summary>
        public string PointSubject = "", PointOutcome = "", PointScore = "";
        /// <summary>The scoreboard shows team scores (not in a free-for-all street game, which has its own column).</summary>
        public bool TeamScores = true;
    }

    public enum MatchPhaseView { Intro, Playing, PointFreeze, MatchOver }

    public enum StreetAction { None, Throw, Kick, Dive, Lupa }

    /// <summary>The street games' pad (street_game_pad.gd show_state, match_hud.gd _sync_street_pad).</summary>
    public sealed class StreetPadState
    {
        public bool Shown;
        public string Title = "", Hint = "";
        public StreetAction Action;
        public float Fill;
        public bool CanAct, OnSidewalk, Aiming;
    }

    /// <summary>One row of a free-for-all street game's score column.</summary>
    public struct StreetScore
    {
        public int Id, Points;
        public string Name;
        public bool IsLocal, IsTaya;
    }

    /// <summary>The results card (match_results.gd _fill).</summary>
    public sealed class ResultInfo
    {
        public bool Won;
        public int Mine, Theirs;
        public string Title = "", Subtitle = "";
        /// <summary>How the points went: (ours, words) per group, in order.</summary>
        public readonly List<(bool ours, string text)> How = new List<(bool, string)>();
    }

    /// <summary>What a match is: who plays and how (the menus pass one to MatchLaunch).</summary>
    public sealed class MatchOptions
    {
        /// <summary>Your kid (a CharacterDef id).</summary>
        public string Character = "junjun";
        /// <summary>Your name over your kid (empty: your kid's name).</summary>
        public string PlayerName = "";
        /// <summary>A street game id (tumbang_preso, patintero, langit_lupa) for a Practice street game; null for Agawang Base.</summary>
        public string StreetGame;
        /// <summary>The bots' level (easy, medium, hard; null: the rules' default).</summary>
        public string BotLevel;
        /// <summary>Your superpowers (weapon ids, in slot order); null: the sim's own pick.</summary>
        public IList<string> Loadout;
        /// <summary>First to N points (one of GameRules.first_to_choices; 0: the rules' default).</summary>
        public int FirstTo;
        public long Seed = 1;
        /// <summary>"Practice", "Online" or "Local" for the results card.</summary>
        public string ModeText = "Practice";
    }

    public sealed class SimBridge
    {
        /// <summary>Same ids as scripts/view/practice_match.gd (LOCAL_ID, BOT_ALLY_IDS, BOT_ENEMY_IDS, STREET_*).</summary>
        public const int PracticeLocalId = 1;
        public const int LocalId = PracticeLocalId;
        public static readonly int[] AllyIds = { 2, 3, 4, 5 };
        public static readonly int[] EnemyIds = { 6, 7, 8, 9, 10 };
        public static readonly int[] StreetAllyIds = { 2, 3 };
        public static readonly int[] StreetEnemyIds = { 4, 5, 6 };
        public const int LocalTeam = 0;

        /// <summary>match_hud.gd constants.</summary>
        public const string CapturingTag = "CAPTURING", RescuingTag = "RESCUING", NoTargetTag = "NO TARGET";
        public const long NoTargetMs = 1200;
        public const float CaptureLinger = 0.4f;
        public const float BannerTime = 2f;
        /// <summary>overhead_hud.gd: the stamina bar turns amber below this share; its exhausted blink period.</summary>
        public const float StaminaLow = 0.25f, BlinkPeriod = 0.25f;
        static readonly (StatusEffects.Type type, string tag)[] CastBlockTags =
        {
            (StatusEffects.Type.Stun, "STUNNED"), (StatusEffects.Type.Airborne, "AIRBORNE"), (StatusEffects.Type.Bounce, "BOUNCED"),
            (StatusEffects.Type.Knockout, "OUT"), (StatusEffects.Type.Tripped, "TRIPPED"), (StatusEffects.Type.Polymorph, "CAN"),
        };

        public enum Kind { Practice, Street, Online }

        readonly MatchSim _sim;
        readonly GameRules _rules;
        readonly GameClient _client;
        readonly List<TeamCoach> _coaches = new List<TeamCoach>();
        readonly StreetModeCoach _modeCoach;
        readonly List<KidSnapshot> _kids = new List<KidSnapshot>();
        readonly Dictionary<int, Vector2> _lastPositions = new Dictionary<int, Vector2>();
        readonly Dictionary<int, string> _names = new Dictionary<int, string>();
        readonly float _runPerTick;
        readonly bool[] _noTarget = new bool[ArcLayout.Slots];
        readonly long[] _noTargetUntil = new long[ArcLayout.Slots];
        readonly GroundRect[] _obstacles;
        readonly string _modeText;
        int _inputTick;
        long _nowMs;
        float _captureLinger;
        bool _captureAlerting, _suppressIdleBehind, _holdTicked, _roomClosed;
        float _throwAngle;
        float _bannerLeft;
        Vector2 _crosshair;

        public Kind MatchKind { get; }
        public int Me { get; }
        public int MyTeam { get; }
        /// <summary>Seconds per tick (1 / tick_rate: 1/30).</summary>
        public float TickDt { get; }
        public int MaxHp { get; }
        public IReadOnlyList<KidSnapshot> Kids => _kids;
        /// <summary>The gestures of this tick and the next (the HUD feeds it through the methods below).</summary>
        public readonly MatchInput Input = new MatchInput();
        public readonly LocalHud Local = new LocalHud();
        public readonly ScoreHud Score = new ScoreHud();
        public readonly StreetPadState Pad = new StreetPadState();
        /// <summary>False under a menu, the briefing or a lost connection: every gesture is dropped and a held card cancels.</summary>
        public bool ControlsActive = true;
        /// <summary>Offline only: the match stands still (the menu is open).</summary>
        public bool Paused;
        public float ArenaRadius { get; }
        public IReadOnlyList<GroundRect> Obstacles => _obstacles;
        /// <summary>The banner across the middle ("GO!", "YOU'RE IT!"), or "" when none is up.</summary>
        public string BannerText { get; private set; } = "";

        /// <summary>A new banner (the view pops it in).</summary>
        public event Action<string> Banner;
        /// <summary>The takedown feed: (tagger name, tagged name, true when your side made it, weapon words).</summary>
        public event Action<string, string, bool, string> Takedown;
        /// <summary>The rescue feed: (rescuer name, rescued name, true when the rescued kid is on your side).</summary>
        public event Action<string, string, bool> Rescue;
        /// <summary>Your cast hit an enemy (false) or tagged one (true): the crosshair's hitmarker.</summary>
        public event Action<bool> LocalHit;
        /// <summary>The camera shakes this much (a hit on you, your dash, a catch).</summary>
        public event Action<float> Shake;
        /// <summary>You were hurt (amount): the hurt pulse.</summary>
        public event Action<int> LocalDamaged;
        /// <summary>The match is over: the results card (and the menus' history) take it from here.</summary>
        public event Action<ResultInfo> MatchOver;
        /// <summary>Online: the room closed or the connection is gone for good ("The host left.\nThe battle is over.").</summary>
        public event Action<string> ConnectionNote;
        /// <summary>(id, byId) when a kid is tagged; (team, how) when a point is scored. Raised during Step.</summary>
        public event Action<int, int> Tagged;
        public event Action<int, int> PointScored;

        // ---- setting a match up ----------------------------------------------------------------------------------------------

        /// <summary>A 5v5 Practice match against bots (practice_match.gd _setup_bot_match), the way the screenshots and the old title start it.</summary>
        public SimBridge(DataSource data, long seed, string localCharacter)
            : this(LoadSimData(data), Kind.Practice, new MatchOptions { Seed = seed, Character = localCharacter }, null) { }

        /// <summary>Practice: Agawang Base against bots, or (options.StreetGame) a street game against bots.</summary>
        public static SimBridge Practice(DataSource data, MatchOptions options) =>
            new SimBridge(LoadSimData(data), string.IsNullOrEmpty(options.StreetGame) ? Kind.Practice : Kind.Street, options, null);

        /// <summary>Online (or a hotspot room): draws `client.Mirror` and sends inputs. The lobby made the client and the server started the
        /// match (client.InMatch()); `data` must be the same SimData the client was made from (LoadSimData).</summary>
        public static SimBridge Online(SimData data, GameClient client, MatchOptions options) => new SimBridge(data, Kind.Online, options, client);

        SimBridge(SimData simData, Kind kind, MatchOptions options, GameClient client)
        {
            options ??= new MatchOptions();
            MatchKind = kind;
            _client = client;
            _modeText = options.ModeText;
            GameRules rules = simData.GameRules;
            if (kind != Kind.Online && !string.IsNullOrEmpty(options.BotLevel)) rules = rules.WithBotLevel(options.BotLevel);
            _rules = rules;
            MapLayout layout = simData.MapLayout;
            TickDt = 1f / rules.TickRate;
            MaxHp = rules.PlayerMaxHp;
            _runPerTick = (float)rules.MoveSpeed * TickDt;
            Input.TapTime = rules.CardTapTime;
            Input.FollowHold = rules.MoveFollowHold;
            ArenaRadius = (float)layout.ArenaRadius;
            List<Rect2> rects = layout.ObstacleRects();
            _obstacles = new GroundRect[rects.Count];
            for (int i = 0; i < rects.Count; i++) _obstacles[i] = new GroundRect(rects[i].Position.X, rects[i].Position.Y, rects[i].Size.X, rects[i].Size.Y);

            if (kind == Kind.Online)
            {
                if (client == null || client.Mirror == null) throw new InvalidOperationException("SimBridge.Online: the client is not in a match");
                _sim = client.Mirror;
                Me = client.PlayerId;
                MyTeam = _sim.Players.TryGetValue(Me, out PlayerState me) ? me.Team : 0;
                client.RoomClosed += OnRoomClosed;
                client.RoomChanged += OnRoomChanged;
                OnRoomChanged(client.Room);
                _onStage = stage => { if (stage == "pick") PickOnline(options); };
                client.StageChanged += _onStage;
                client.Loaded();
                if (client.Stage == "pick") PickOnline(options);
            }
            else
            {
                _sim = new MatchSim(rules, layout, options.Seed);
                Me = PracticeLocalId;
                MyTeam = LocalTeam;
                if (options.FirstTo > 0) _sim.SetFirstTo(options.FirstTo);
                StreetModeRules game = kind == Kind.Street ? StreetMode.RulesFor(simData, options.StreetGame) : null;
                if (kind == Kind.Street && game == null) throw new ArgumentException($"no street game '{options.StreetGame}'");
                if (game != null) SetupStreetGame(game, options.Character, out _modeCoach);
                else SetupBotMatch(options);
                _sim.BeginIntro();
            }
            if (!string.IsNullOrEmpty(options.PlayerName)) _names[Me] = options.PlayerName;
            HookEvents();
            Refresh();
        }

        /// <summary>
        /// Everything a match reads from unity/Data: the game rules, the map, the net rules, the street games, and every file they name through
        /// a `$ref`. The menus make their GameClient from this too (`new GameClient(d.GameRules, d.MapLayout, NetRules.FromData(d), new
        /// StreetCatalog(d))`).
        /// </summary>
        public static SimData LoadSimData(DataSource data)
        {
            var simData = new SimData();
            var pending = new Queue<string>(new[]
            {
                "rules__game_rules", "rules__map_layout", "rules__net_rules", "rules__street_games",
                "rules__tumbang_preso", "rules__patintero", "rules__langit_lupa",
            });
            var seen = new HashSet<string>();
            while (pending.Count > 0)
            {
                string name = pending.Dequeue();
                if (!seen.Add(name)) continue;
                string text = data.Text(name);
                simData.AddFile(text);
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "\"\\$ref\"\\s*:\\s*\"(res://data/[^\"]+)\""))
                    pending.Enqueue(DataSource.NameOfResPath(m.Groups[1].Value));
            }
            return simData;
        }

        /// <summary>practice_match.gd _setup_bot_match: you, four allies backing you up, five enemies, ten different kids.</summary>
        void SetupBotMatch(MatchOptions options)
        {
            _sim.AddPlayer(Me, MyTeam);
            var allies = new TeamCoach(MyTeam, Me);
            var foes = new TeamCoach(1 - MyTeam);
            foreach (int id in AllyIds) { _sim.AddPlayer(id, MyTeam); allies.Add(_sim, id, _rules.BotSkill); }
            foreach (int id in EnemyIds) { _sim.AddPlayer(id, 1 - MyTeam); foes.Add(_sim, id, _rules.BotSkill); }
            _coaches.Add(allies);
            _coaches.Add(foes);
            AssignUniqueRoster(options.Character);
            _sim.Equip(Me);
            foreach (int id in AllyIds) _sim.Equip(id, true);
            foreach (int id in EnemyIds) _sim.Equip(id, true);
            if (options.Loadout != null && options.Loadout.Count >= 2 && !_rules.CardCycle) _sim.SetLoadoutList(Me, options.Loadout);
        }

        /// <summary>
        /// practice_match.gd _setup_street_game: Patintero is you and two bot allies against three bots; Tumbang Preso and Langit Lupa are
        /// every kid for itself (all on one team): you and five bots. No skills in these games: nobody is dealt a hand.
        /// </summary>
        void SetupStreetGame(StreetModeRules game, string character, out StreetModeCoach coach)
        {
            bool forAll = StreetMode.Create(game).IsFreeForAll();
            _sim.AddPlayer(Me, MyTeam);
            coach = new StreetModeCoach();
            foreach (int id in StreetAllyIds) { _sim.AddPlayer(id, MyTeam); coach.Add(id); }
            foreach (int id in StreetEnemyIds) { _sim.AddPlayer(id, forAll ? MyTeam : 1 - MyTeam); coach.Add(id); }
            _sim.AssignCharacters();
            if (!string.IsNullOrEmpty(character)) _sim.SetCharacter(Me, character);
            _sim.StartMode(StreetMode.Create(game));
        }

        /// <summary>practice_match.gd _assign_unique_roster: ten different kids drawn from the match's rng, yours first.</summary>
        void AssignUniqueRoster(string avatar)
        {
            if (_rules.Characters.Count < _sim.Players.Count || string.IsNullOrEmpty(avatar))
            {
                _sim.AssignCharacters();
                if (!string.IsNullOrEmpty(avatar)) _sim.SetCharacter(Me, avatar);
                return;
            }
            var pool = new List<string>();
            foreach (CharacterDef c in _rules.Characters) pool.Add(c.Id);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = _sim.Rng.RandiRange(0, i);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            int wanted = pool.IndexOf(avatar);
            if (wanted > 0) { pool[wanted] = pool[0]; pool[0] = avatar; }
            int cursor = 0;
            foreach (var pair in _sim.Players) pair.Value.CharacterId = pool[cursor++];
        }

        /// <summary>Online, while the server waits for picks: your kid and superpowers (the loadout given, else the rules' first ones).</summary>
        void PickOnline(MatchOptions options)
        {
            if (_picked) return;
            _picked = true;
            var picks = new List<string>();
            if (options.Loadout != null) picks.AddRange(options.Loadout);
            for (int i = 0; picks.Count < Math.Clamp(_rules.LoadoutSize, 2, 3) && i < _rules.Weapons.Count; i++)
                if (!picks.Contains(_rules.Weapons[i].Id)) picks.Add(_rules.Weapons[i].Id);
            _client.PickWeapons(picks.Count > 0 ? picks[0] : "", picks.Count > 1 ? picks[1] : "", options.Character ?? "", picks.Count > 2 ? picks[2] : "");
        }

        void HookEvents()
        {
            _sim.PlayerTagged += OnTagged;
            _sim.PlayerRescued += OnRescued;
            _sim.PointScored += (team, how) => PointScored?.Invoke(team, how);
            _sim.MatchWon += _ => MatchOver?.Invoke(Result());
            _sim.IntroFinished += () => Say("GO!");
            _sim.PlayerDamaged += (id, amount) =>
            {
                if (id != Me) return;
                Shake?.Invoke(Math.Min(0.1f + amount * 0.01f, 0.4f));
                LocalDamaged?.Invoke(amount);
            };
            _sim.PlayerDashed += id => { if (id == Me) Shake?.Invoke(0.07f); };
            _sim.UltimateCast += id =>
            {
                if (id != Me) return;
                Shake?.Invoke(0.30f);
                Say("BAGSAK NG KALYE!");
            };
            _sim.WeaponHit += (target, weapon) =>
            {
                if (_sim.Players.TryGetValue(target, out PlayerState hit) && !IsAlly(hit) && _lastCaster.TryGetValue(weapon, out int caster) && caster == Me)
                    LocalHit?.Invoke(false);
                _lastHitWeapon[target] = weapon;
            };
            _sim.WeaponCast += (id, weapon) => _lastCaster[weapon] = id;
            if (_sim.Mode is StreetMode mode) HookStreetGame(mode);
        }

        readonly Dictionary<string, int> _lastCaster = new Dictionary<string, int>();
        readonly Dictionary<int, string> _lastHitWeapon = new Dictionary<int, string>();

        void OnTagged(int id, int byId)
        {
            Tagged?.Invoke(id, byId);
            _sim.Players.TryGetValue(byId, out PlayerState by);
            if (_sim.Players.TryGetValue(id, out PlayerState victim))
            {
                string weapon = _lastHitWeapon.TryGetValue(id, out string w) ? WeaponWords(w) : "";
                Takedown?.Invoke(by != null ? NameOf(by) : "KALYE", NameOf(victim), by != null ? IsAlly(by) : !IsAlly(victim), weapon);
            }
            if (byId == Me) LocalHit?.Invoke(true);
            if (id == Me) Shake?.Invoke(0.25f);
        }

        void OnRescued(int id, int byId)
        {
            if (byId == MatchSim.ReleasedAlone) return;
            if (_sim.Players.TryGetValue(id, out PlayerState saved) && _sim.Players.TryGetValue(byId, out PlayerState rescuer))
                Rescue?.Invoke(NameOf(rescuer), NameOf(saved), IsAlly(saved));
        }

        /// <summary>practice_match_presentation.gd _connect_street_game: the street games say what happens in banners.</summary>
        void HookStreetGame(StreetMode mode)
        {
            mode.Tagged += (tagger, target) =>
            {
                if (tagger == Me || target == Me) Shake?.Invoke(0.3f);
            };
            mode.Scored += (team, id) =>
            {
                if (mode is Patintero || mode is LangitLupa) return;
                if (id == Me) Say("+1 YOU SCORED!");
                else if (!mode.IsFreeForAll()) Say(team == MyTeam ? "+1 POINT!" : "+1 THEM");
            };
            if (mode is TumbangPreso game)
            {
                game.TayaChanged += (_, newTaya, why) =>
                {
                    if (why == "tagged") return;
                    string scared = why == "scared" ? "TOO SCARED! " : "";
                    Say(scared + (newTaya == Me ? "YOU'RE IT!" : NameOf(newTaya).ToUpperInvariant() + " IS IT!"));
                };
                game.SlipperLanded += (id, _, __) => { if (id == Me) Say("MISSED! FETCH YOUR SLIPPER!"); };
                game.SlipperFlung += (id, _, path) =>
                {
                    if (id == Me) Say(path.Count == 1 ? "CAN DOWN! FETCH YOUR SLIPPER!" : "BOUNCED! FETCH YOUR SLIPPER!");
                    else if (game.TayaId == Me) Say("CAN DOWN! PUT IT BACK!");
                };
                game.CanKicked += id =>
                {
                    if (id == Me) Say("KICKED!");
                    else if (game.TayaId == Me) Say("THEY KICKED THE CAN!");
                };
                game.SlipperFetched += id => { if (id == Me) Say("RUN TO THE OTHER END!"); };
                game.CanPlaced += _ => Say("THE CAN IS UP!");
            }
            if (mode is Patintero lines)
            {
                lines.RolesSwapped += team => Say(team == MyTeam ? "YOU RUN!" : "YOU GUARD!");
                lines.RunnerCrossed += id => { if (id == Me) Say("PAST THE LAST GUARD! RUN BACK!"); };
                lines.Scored += (team, _) =>
                {
                    int how = _sim.PointLog.Count > 0 ? _sim.PointLog[_sim.PointLog.Count - 1].Y : -1;
                    bool ours = team == MyTeam;
                    if (how == (int)MatchSim.CaptureKind.TooSlow) Say(ours ? "TOO SLOW! +1 FOR YOU" : "TOO SLOW! +1 FOR THE GUARDS");
                    else if (how == (int)MatchSim.CaptureKind.Touched) Say(ours ? "ALL TAGGED! +1 FOR YOU" : "ALL TAGGED! +1 FOR THEM");
                    else Say(ours ? "+1 FOR YOU!" : "+1 FOR THEM");
                };
            }
        }

        void Say(string text)
        {
            BannerText = text;
            _bannerLeft = BannerTime;
            Banner?.Invoke(text);
        }

        /// <summary>Shows a banner from outside the sim ("Back online!", the menus' notes).</summary>
        public void ShowBanner(string text) => Say(text);

        // ---- the match's clock ------------------------------------------------------------------------------------------------

        public int Tick => _sim.Tick;
        public float ClockSeconds => (float)_sim.MatchTime;
        public int ScoreOf(int team) => _sim.Score.Points[team];
        public bool IsStreetGame => _sim.Mode != null;
        public string StreetGameId => _sim.Mode?.Rules.Id ?? "";
        public bool IsFreeForAll => _sim.Mode is StreetMode m && m.IsFreeForAll();
        public bool IsOnline => MatchKind == Kind.Online;
        public bool HasHand => _sim.Mode == null;

        public MatchPhaseView Phase => _sim.Phase switch
        {
            MatchSim.MatchPhase.Intro => MatchPhaseView.Intro,
            MatchSim.MatchPhase.PointFreeze => MatchPhaseView.PointFreeze,
            MatchSim.MatchPhase.MatchOver => MatchPhaseView.MatchOver,
            _ => MatchPhaseView.Playing,
        };

        /// <summary>
        /// Once a frame, before the ticks: online it polls the connection and places the other kids between snapshots; the banner and the
        /// hold clocks run. `dt` is the frame's seconds.
        /// </summary>
        public void Frame(float dt)
        {
            _nowMs += (long)Math.Round(dt * 1000.0);
            if (_bannerLeft > 0f && Phase != MatchPhaseView.MatchOver)
            {
                _bannerLeft -= dt;
                if (_bannerLeft <= 0f) BannerText = "";
            }
            if (_client != null)
            {
                _client.Poll(dt);
                _client.Interpolate();
                if (_client.Mirror == _sim) Refresh();
            }
        }

        /// <summary>
        /// One fixed tick: the bots decide, your input goes in, the sim steps (online: the input goes to the server and your kid is
        /// predicted). While Paused (offline, the menu open) one neutral tick drops what was held, then nothing moves.
        /// </summary>
        public void Step()
        {
            if (Paused && !IsOnline)
            {
                if (_holdTicked) return;
                _holdTicked = true;
            }
            else _holdTicked = false;
            if (IsOnline)
            {
                if (_roomClosed || !_client.IsOnline()) return;
                _inputTick++;
                _client.SendInput(LocalInput(_inputTick), TickDt);
                Refresh();
                return;
            }
            foreach (TeamCoach coach in _coaches) coach.Drive(_sim);
            _modeCoach?.Drive(_sim);
            _sim.SetInput(Me, LocalInput(_sim.Tick));
            _sim.Step(TickDt);
            Refresh();
        }

        /// <summary>A menu, a pause or a lost connection drops what is held: a held aim must cancel rather than cast.</summary>
        PlayerInput LocalInput(int tick)
        {
            if (!ControlsActive || Paused)
            {
                Input.Clear();
                return PlayerInput.Create(Vec2.Zero, Vec2.Zero, PlayerInput.BtnAimCancel, tick);
            }
            // the shoulder view: a held card follows the crosshair every tick (the kid walks, the view turns), not only when the finger moves
            if (Input.CardHeld() && Input.OverStreet) Input.CardDrag(false, V(_crosshair), true);
            return Input.Build(tick);
        }

        /// <summary>Tests: jump past the opening show (offline). A tap does it too, except Tumbang Preso's roll.</summary>
        public void SkipIntro()
        {
            if (IsOnline || _sim.Phase != MatchSim.MatchPhase.Intro) return;
            if (_sim.Mode is TumbangPreso tp && tp.Rules.RollSeconds > 0.0) return;
            _sim.IntroLeft = 0.0;
            _sim.Phase = MatchSim.MatchPhase.Playing;
            Say("GO!");
        }

        /// <summary>Leaving: online the room slot goes back to the server. Returns true when an offline Practice match counted as a loss
        /// (left after 15 s and before the end; practice_match.gd record_if_abandoned) so the menus can write it to the history.</summary>
        public bool Leave()
        {
            if (IsOnline)
            {
                if (_client.InMatch()) _client.LeaveRoom();
                _client.RoomClosed -= OnRoomClosed;
                _client.RoomChanged -= OnRoomChanged;
                _client.StageChanged -= _onStage;
                return false;
            }
            return _sim.Phase != MatchSim.MatchPhase.MatchOver && _sim.MatchTime >= 15.0;
        }

        void OnRoomClosed()
        {
            _roomClosed = true;
            Input.Clear();
            ConnectionNote?.Invoke("The host left.\nThe battle is over.");
        }

        /// <summary>Online: the names in the room go over their kids; someone away shows "AFK".</summary>
        void OnRoomChanged(Kalyeah.Net.GDict room)
        {
            _away.Clear();
            if (room == null || !room.Has("members")) return;
            foreach (object item in room.GetArr("members"))
            {
                if (!(item is Kalyeah.Net.GDict entry)) continue;
                int id = entry.GetInt("player_id", -1);
                string name = entry.GetStr("name");
                if (!string.IsNullOrEmpty(name)) _names[id] = name;
                if (entry.GetBool("afk")) _away.Add(id);
            }
        }

        readonly HashSet<int> _away = new HashSet<int>();
        Action<string> _onStage;
        bool _picked;

        // ---- the hand and the hero buttons (practice_match.gd card_pressed ... card_cancelled) -------------------------------

        /// <summary>
        /// The street point under the crosshair this frame: `streetHit` is where the ray through the middle of the screen meets the ground (null:
        /// over the horizon), `forward` the view's way on the ground. Kept inside the arena; past it, `aim_reach` metres along the view
        /// (practice_match.gd crosshair_point). Call once a frame before the gestures.
        /// </summary>
        public Vector2 SetCrosshair(Vector2? streetHit, Vector2 forward, float boundsEdge, float aimReach)
        {
            Vector2 from = _sim.Players.TryGetValue(Me, out PlayerState me) ? P(me.Position) : Vector2.Zero;
            Vector2 point = streetHit.HasValue && streetHit.Value.Length() <= ArenaRadius + boundsEdge ? streetHit.Value : from + forward * aimReach;
            _crosshair = P(_sim.Layout.ClampToArena(V(point), _rules.PlayerRadius));
            return _crosshair;
        }

        public Vector2 Crosshair => _crosshair;

        /// <summary>A card was picked up from the hand.</summary>
        public void CardPressed(int slot) => Input.CardPress(slot, _nowMs);

        /// <summary>
        /// The held card's finger moved (the shoulder view: the drag turns the view and the card goes where the crosshair is). `onCard`: still on
        /// its own button; `overCancel`: over the X. A heal tapped without turning heals yourself (docs/GDD.md "Heal: a quick tap heals
        /// yourself").
        /// </summary>
        public void CardMoved(int slot, bool onCard, bool overCancel)
        {
            bool cancel = overCancel && !onCard;
            if (onCard && !cancel && IsHeal(slot))
            {
                Input.CardDrag(true, null);
                return;
            }
            Input.CardDrag(false, cancel ? (Vec2?)null : V(_crosshair), true);
        }

        /// <summary>The held card was let go: a cast at the crosshair, a tap, or (over the X) a cancel. Returns what it meant.</summary>
        public MatchInput.Release CardReleased(int slot, bool onCard, bool overCancel)
        {
            CardMoved(slot, onCard, overCancel);
            MatchInput.Release kind = Input.CardRelease(_nowMs);
            bool cancelled = kind == MatchInput.Release.Cancel;
            if (slot >= 0 && slot < _noTarget.Length && !cancelled && _noTarget[slot]) _noTargetUntil[slot] = _nowMs + NoTargetMs;
            return kind;
        }

        public void CardCancelled() => Input.CardCancel();

        /// <summary>The hero primary (Tirador) held or let go, aimed at the crosshair.</summary>
        public void SetPrimary(bool held) => Input.SetPrimary(held, V(_crosshair));
        public void PressDash() => Input.PressDash();
        public void PressUltimate() => Input.PressUltimate(V(_crosshair));
        public void PressJump() => Input.PressJump();
        public void SetCrouch(bool held) => Input.SetCrouch(held);
        public void PressSlide() => Input.PressSlide();

        /// <summary>The joystick or the street games' knob, as a way on the ground (ShoulderFollow.GroundWay of the screen way).</summary>
        public void SetStick(Vector2 groundWay) => Input.Stick = V(groundWay);

        /// <summary>True when the card in `slot` of your hand heals.</summary>
        public bool IsHeal(int slot)
        {
            if (!_sim.Players.TryGetValue(Me, out PlayerState state) || slot < 0 || slot >= state.Weapons.Count) return false;
            return _sim.WeaponDefs.TryGetValue(state.Weapons[slot], out WeaponDef def) && def.Kind == WeaponDef.WeaponKind.Heal;
        }

        // ---- the street games' pad (practice_match.gd throw_pressed ... action_pressed) ---------------------------------------

        /// <summary>Tumbang Preso's Throw button went down: aim at the can, the hold starts the line.</summary>
        public void ThrowPressed()
        {
            if (!(_sim.Mode is TumbangPreso game) || !_sim.Players.TryGetValue(Me, out PlayerState me)) return;
            _throwAngle = AngleOf(P(game.AimAtCan(me.Position)));
            Input.ThrowPress(V(WayOf(_throwAngle)));
        }

        /// <summary>
        /// The finger dragged `drag` UI pixels (y down): the part across the throw line on screen (`screenRight`: the ground's +X as a screen
        /// way, ShoulderFollow.ScreenWay) turns the aim by that much times the line's sensitivity now, within the rules' arc.
        /// </summary>
        public void ThrowDragged(Vector2 drag, Vector2 screenRight)
        {
            if (!(_sim.Mode is TumbangPreso game) || !Input.ThrowHeld) return;
            float turn = Vector2.Dot(drag, screenRight) * (float)game.SensitivityAt(game.Charge(Me));
            float arc = (float)(game.Rules.AimArcDegrees * Math.PI / 180.0);
            _throwAngle = Math.Clamp(_throwAngle + turn, -arc, arc);
            Input.ThrowTurn(V(WayOf(_throwAngle)));
        }

        public void ThrowReleased(bool cancel) => Input.ThrowRelease(cancel);

        /// <summary>The Kick, Dive or Lupa button: one tap.</summary>
        public void ActionPressed() => Input.KickPress();

        /// <summary>The angle (radians; 0 = straight up the street toward the far end, positive toward +X) of a way in sim XZ, and back.</summary>
        public static float AngleOf(Vector2 way) => MathF.Atan2(way.X, -way.Y);
        public static Vector2 WayOf(float angle) => new Vector2(MathF.Sin(angle), -MathF.Cos(angle));

        /// <summary>The base side you defend this set (+1: the +Z end), as practice_match.gd own_side: the camera looks from it.</summary>
        public int OwnSide()
        {
            _sim.Players.TryGetValue(Me, out PlayerState me);
            if (_sim.Mode is Patintero lines) return me == null || lines.IsAttacker(me) ? MapLayout.SideOwn : MapLayout.SideEnemy;
            if (_sim.Mode is TumbangPreso tp) return tp.TayaId == Me ? MapLayout.SideEnemy : MapLayout.SideOwn;
            return _sim.SideForTeam(MyTeam);
        }

        /// <summary>The camera stands at the -Z end (your side is the enemy end of the map now).</summary>
        public bool Flip => OwnSide() == MapLayout.SideEnemy;

        // ---- the camera's locomotion (practice_match.gd _sync_camera_locomotion) ---------------------------------------------

        /// <summary>
        /// Once a frame: the posture (crouch, slide, jump) and the auto-follow behind your running kid. `screenMove` is the joystick's push on
        /// screen (x right, y down). While aiming, holding the primary or backpedalling the view stays where the finger put it.
        /// </summary>
        public void DriveCamera(ShoulderFollow camera, Vector2 screenMove, float dt)
        {
            if (!_sim.Players.TryGetValue(Me, out PlayerState player)) return;
            bool low = !player.Tagged && (player.Crouching || player.SlideLeft > 0.0);
            camera.SyncPosture(low, player.Tagged ? 0f : (float)player.AirY, dt);
            camera.SetSpeedBoost(player.Sprinting || player.DashLeft > 0.0 || player.SlideLeft > 0.0);
            if (_sim.Mode != null || _sim.Phase != MatchSim.MatchPhase.Playing || player.Tagged)
            {
                _suppressIdleBehind = false;
                return;
            }
            bool backpedaling = screenMove.Y > 0.35f;
            bool forward = (screenMove != Vector2.Zero && !backpedaling) || player.HasMoveTarget || player.DashLeft > 0.0 || player.SlideLeft > 0.0;
            if (backpedaling) _suppressIdleBehind = true;
            else if (forward) _suppressIdleBehind = false;
            if (Input.PrimaryHeld || Input.CardHeld() || backpedaling || (_suppressIdleBehind && !forward))
            {
                camera.TickManualLook(dt);
                return;
            }
            camera.SyncBehindCharacter(P(player.Facing), forward || Input.Stick != Vec2.Zero, dt);
        }

        // ---- what the view draws ----------------------------------------------------------------------------------------------

        public KidSnapshot? Find(int id)
        {
            foreach (KidSnapshot kid in _kids)
                if (kid.Id == id) return kid;
            return null;
        }

        /// <summary>Kept for the screenshots: `move` is a way on the ground, `buttons` a PlayerInput mask sent with it.</summary>
        public void SetLocalInput(Vector2 move, int buttons)
        {
            Input.Stick = V(move);
            Input.PendingButtons |= buttons;
        }

        void Refresh()
        {
            _kids.Clear();
            List<int> hidden = _sim.Mode is StreetMode mode ? mode.HiddenFrom(_sim, Me) : null;
            int taya = _sim.Mode is TayaGame tg ? tg.TayaId : -1;
            foreach (var pair in _sim.Players)
            {
                PlayerState p = pair.Value;
                var position = P(p.Position);
                float speed = 0f;
                if (_lastPositions.TryGetValue(p.Id, out Vector2 last) && _runPerTick > 0f)
                    speed = Math.Clamp(Vector2.Distance(position, last) / _runPerTick, 0f, 1f);
                _lastPositions[p.Id] = position;
                bool enemy = p.Id != Me && (p.Id == taya || p.Team != MyTeam);
                bool isHidden = hidden != null && hidden.Contains(p.Id);
                _kids.Add(new KidSnapshot
                {
                    Id = p.Id, Team = p.Team, CharacterId = p.CharacterId ?? "", Position = position, Facing = P(p.Facing), Hp = p.Hp, Tagged = p.Tagged,
                    Speed01 = speed, AirY = (float)p.AirY, Crouching = p.Crouching, Sliding = p.SlideLeft > 0.0,
                    Number = PlayerNumber(p), Name = NameOf(p), Status = StatusText(p), Enemy = enemy, Hidden = isHidden,
                    HealthShown = !p.Tagged && !isHidden && !enemy,
                    CaptureProgress = SpinnerVisible(p) ? (float)(_sim.CaptureElapsed / Math.Max(_sim.CaptureNeeded, 0.001)) : -1f,
                    RescueProgress = RescueVisible(p) ? (float)(_sim.RescueElapsed[p.Team] / Math.Max(_rules.RescueHoldTime, 0.001)) : -1f,
                    IsLocal = p.Id == Me, IsTaya = p.Id == taya,
                });
            }
            RefreshLocal();
            RefreshScore();
            RefreshPad();
        }

        /// <summary>
        /// overhead_hud.gd player_number: the kid's slot in the room (team 0 is 1..N, team 1 is N+1..2N, by the order they joined), the number
        /// on its badge.
        /// </summary>
        int PlayerNumber(PlayerState state)
        {
            int row = 0, teamZero = 0, teamOne = 0;
            bool before = true;
            foreach (var pair in _sim.Players)
            {
                PlayerState other = pair.Value;
                if (other.Id == state.Id) before = false;
                else if (before && other.Team == state.Team) row++;
                if (other.Team == 0) teamZero++;
                else teamOne++;
            }
            return HudText.SlotNumber(state.Team, row, Math.Max(teamZero, teamOne));
        }

        /// <summary>The name over a kid: the one the room gave it (yours in Practice), else its kid's name (overhead_hud.gd name_of).</summary>
        public string NameOf(int id) => _sim.Players.TryGetValue(id, out PlayerState state) ? NameOf(state) : "";

        string NameOf(PlayerState state)
        {
            if (_names.TryGetValue(state.Id, out string given) && !string.IsNullOrEmpty(given)) return given;
            foreach (CharacterDef character in _rules.Characters)
                if (character.Id == state.CharacterId) return character.DisplayName;
            return "";
        }

        /// <summary>overhead_hud.gd _status_text: "AFK", the status effects, or "Exhausted".</summary>
        string StatusText(PlayerState state)
        {
            var words = new List<string>();
            if (_away.Contains(state.Id)) words.Add("AFK");
            List<string> effects = state.Effects.DisplayNameList();
            if (effects.Count > 0) words.AddRange(effects);
            else if (state.Overclocked) words.Add("Exhausted");
            return string.Join(" · ", words);
        }

        bool SpinnerVisible(PlayerState state) =>
            !state.Tagged && _sim.CaptureTeam == state.Team && _sim.CurrentCaptureKind == MatchSim.CaptureKind.Base
            && _sim.Layout.InBase(-_sim.SideForTeam(state.Team), state.Position);

        bool RescueVisible(PlayerState state) =>
            !state.Tagged && state.Team >= 0 && state.Team < _sim.RescueRescuer.Length && _sim.RescueRescuer[state.Team] == state.Id;

        /// <summary>Yours, or on your team (in a free-for-all street game only you).</summary>
        bool IsAlly(PlayerState state) => state.Id == Me || (state.Team == MyTeam && !IsFreeForAll);

        string WeaponWords(string id)
        {
            WeaponDef def = null;
            if (!string.IsNullOrEmpty(id)) _sim.WeaponDefs.TryGetValue(id, out def);
            if (def == null && _rules.PrimaryWeapon != null && _rules.PrimaryWeapon.Id == id) def = _rules.PrimaryWeapon;
            if (def == null && _rules.UltimateWeapon != null && _rules.UltimateWeapon.Id == id) def = _rules.UltimateWeapon;
            if (def == null) return "";
            return !string.IsNullOrEmpty(def.ShortName) ? def.ShortName : def.DisplayName;
        }

        /// <summary>match_hud.gd sync_player + card_hand.gd / ability_arc.gd show_state.</summary>
        void RefreshLocal()
        {
            LocalHud h = Local;
            if (!_sim.Players.TryGetValue(Me, out PlayerState player))
            {
                h.Playing = h.Shown = false;
                return;
            }
            bool playing = !player.Tagged && _sim.Phase == MatchSim.MatchPhase.Playing && ControlsActive && !_roomClosed;
            h.Playing = playing;
            h.Shown = playing || (_sim.Phase == MatchSim.MatchPhase.Intro && !player.Tagged && ControlsActive && !_roomClosed);
            h.Tagged = player.Tagged;
            h.Hp = player.Hp;
            h.MaxHp = _rules.PlayerMaxHp;
            h.HoldingCard = Input.CardHeld();
            h.Block = playing ? BlockTag(player) : "";
            h.UsesElixir = _sim.PaysElixir(player);
            h.Elixir = (float)player.Elixir;
            h.ElixirMax = (float)_rules.ElixirMax;
            for (int slot = 0; slot < ArcLayout.Slots; slot++)
            {
                _noTarget[slot] = playing && _sim.Mode == null && _sim.LacksTarget(player, slot);
                WeaponDef def = null;
                if (slot < player.Weapons.Count && !string.IsNullOrEmpty(player.Weapons[slot])) _sim.WeaponDefs.TryGetValue(player.Weapons[slot], out def);
                float cooldown = slot < player.WeaponCooldowns.Count ? (float)player.WeaponCooldowns[slot] : 0f;
                h.Cards[slot] = def == null ? default : new CardInfo
                {
                    Id = def.Id, Name = !string.IsNullOrEmpty(def.ShortName) ? def.ShortName : def.DisplayName, Cost = def.ElixirCost,
                    Pickable = playing && _sim.WeaponReady(player, slot), IsHeal = def.Kind == WeaponDef.WeaponKind.Heal,
                    Reached = Reached(player, slot, def), CooldownLeft = cooldown,
                    Notice = playing && _nowMs < _noTargetUntil[slot] ? NoTargetTag : "",
                };
            }
            WeaponDef next = null, shuffling = null;
            if (!string.IsNullOrEmpty(player.NextCard)) _sim.WeaponDefs.TryGetValue(player.NextCard, out next);
            if (!string.IsNullOrEmpty(player.ShuffleCard)) _sim.WeaponDefs.TryGetValue(player.ShuffleCard, out shuffling);
            h.NextFill = 1f;
            if (shuffling != null)
            {
                next = shuffling;
                h.NextFill = 1f - (float)Math.Clamp(player.ShuffleLeft / Math.Max(player.ShuffleTotal, 0.001), 0.0, 1.0);
            }
            h.NextId = next?.Id ?? "";
            h.NextName = next == null ? "" : !string.IsNullOrEmpty(next.ShortName) ? next.ShortName : next.DisplayName;

            WeaponDef primary = _rules.PrimaryWeapon, ult = _rules.UltimateWeapon;
            h.PrimaryId = primary == null || primary.Id == "tirador_bato" ? "tirador" : primary.Id;
            h.UltId = ult?.Id ?? "ult_bagsak";
            h.PrimaryCdLeft = (float)Math.Max(player.PrimaryCd, 0.0);
            float primaryMax = primary != null && primary.Cooldown > 0.0 ? (float)primary.Cooldown : 0.48f;
            h.PrimaryCdShare = 1f - Math.Clamp(h.PrimaryCdLeft / primaryMax, 0f, 1f);
            h.VaultCdLeft = (float)Math.Max(player.DashCd, 0.0);
            h.VaultCdShare = 1f - Math.Clamp(h.VaultCdLeft / (float)Math.Max(_rules.DashCooldown, 0.001), 0f, 1f);
            h.VaultActive = player.DashLeft > 0.0;
            h.VaultReady = playing && !player.Tagged && player.Effects.CanCast() && h.VaultCdLeft <= 0.001f && !h.VaultActive;
            h.UltCharge = (float)Math.Clamp(player.UltCharge, 0.0, 100.0);
            h.UltReady = playing && h.Block.Length == 0 && h.UltCharge >= 99.99f && ult != null;
            h.Crouching = player.Crouching;
            float jumpCd = (float)Math.Max(player.JumpCd, 0.0);
            h.JumpCdShare = 1f - Math.Clamp(jumpCd / (float)Math.Max(_rules.JumpCooldown, 0.05), 0f, 1f);
            h.SlideCdLeft = (float)Math.Max(player.SlideCd, 0.0);
            h.SlideCdShare = 1f - Math.Clamp(h.SlideCdLeft / (float)Math.Max(_rules.SlideCooldown, 0.1), 0f, 1f);
            h.CanMove = playing && !player.Tagged && player.Effects.CanMove();
            h.InAir = player.AirY > 0.02;
            h.JumpReady = h.CanMove && !h.InAir && jumpCd <= 0.01f;
            h.Sliding = player.SlideLeft > 0.01;
            h.Running = h.CanMove && !player.Walking && !player.Crouching && !player.Overclocked && (player.RunTime > 0.01 || player.Sprinting);
            h.CanSlide = h.Running && !h.InAir && h.SlideCdLeft <= 0.01f && !h.Sliding;

            bool staminaOn = _rules.StaminaEnabled && player.HasStamina && !player.Tagged;
            h.StaminaShown = staminaOn;
            h.StaminaShare = 1f;
            h.StaminaTone = 0;
            if (staminaOn)
            {
                if (player.Overclocked && player.ExhaustLeft > 0.0)
                {
                    h.StaminaShare = (int)(player.ExhaustLeft / BlinkPeriod) % 2 == 0 ? 1f : 0f;
                    h.StaminaTone = 2;
                }
                else
                {
                    h.StaminaShare = (float)Math.Clamp(player.Stamina / Math.Max(_rules.StaminaMax, 0.001), 0.0, 1.0);
                    h.StaminaTone = player.Overclocked || h.StaminaShare < StaminaLow ? 1 : 0;
                }
            }
            // a stun or a tag drops the card (the sim spent nothing)
            if (Input.CardHeld() && (!playing || h.Block.Length > 0)) Input.CardCancel();
            if (Input.PrimaryHeld && (!playing || h.Block.Length > 0)) Input.SetPrimary(false, V(_crosshair));
            if (Input.CrouchHeld && (!playing || !player.Effects.CanMove())) Input.SetCrouch(false);
        }

        float Reached(PlayerState player, int slot, WeaponDef def)
        {
            if (_sim.PaysElixir(player)) return (float)Math.Clamp(player.Elixir / Math.Max(def.ElixirCost, 0.001), 0.0, 1.0);
            if (slot < player.WeaponCooldowns.Count && def.Cooldown > 0.0) return 1f - (float)Math.Clamp(player.WeaponCooldowns[slot] / def.Cooldown, 0.0, 1.0);
            return 1f;
        }

        /// <summary>match_hud.gd _block_tag: what stops your casting now.</summary>
        string BlockTag(PlayerState player)
        {
            if (_sim.CastingBlocked(player))
                return _sim.Layout.InBase(-_sim.SideForTeam(player.Team), player.Position) ? CapturingTag : RescuingTag;
            if (player.Effects.CanCast()) return "";
            foreach (var (type, tag) in CastBlockTags)
                if (player.Effects.Has(type)) return tag;
            return "";
        }

        /// <summary>match_hud.gd sync_score: the score, the clock, the capture warning, the point freeze's words.</summary>
        void RefreshScore()
        {
            ScoreHud s = Score;
            int mine = MyTeam, theirs = 1 - MyTeam;
            s.Mine = _sim.Score.Points[mine];
            s.Theirs = _sim.Score.Points[theirs];
            s.Phase = Phase;
            s.TeamScores = !IsFreeForAll;
            bool counting = _sim.CountsDown();
            MatchSim.ClockStage stage = _sim.GetClockStage();
            s.Clock = ClockDisplay.Of(counting ? _sim.ClockLeft() : _sim.MatchTime, (int)stage, counting);

            bool running = _sim.CaptureTeam >= 0 && _sim.Phase == MatchSim.MatchPhase.Playing;
            bool ours = _sim.CaptureTeam == MyTeam;
            bool alert = running && !ours && _sim.CurrentCaptureKind == MatchSim.CaptureKind.Base;
            if (running)
            {
                _captureLinger = CaptureLinger;
                _captureAlerting = alert;
            }
            else if (_sim.Phase == MatchSim.MatchPhase.Playing && _captureLinger > 0f)
            {
                _captureLinger -= TickDt;
                running = _captureLinger > 0f;
                alert = running && _captureAlerting;
            }
            else _captureLinger = 0f;
            s.CaptureAlert = alert;
            s.CaptureOurs = ours;
            s.CaptureLabel = "";
            if (running && !alert && _sim.CaptureTeam >= 0)
            {
                int seconds = (int)Math.Ceiling(_sim.CaptureTimeLeft());
                string how = _sim.CurrentCaptureKind == MatchSim.CaptureKind.AllTagged ? (ours ? "ALL TAGGED!" : "ALL OF YOU TAGGED!") : "HOLD THE BASE!";
                s.CaptureLabel = how + "\n" + seconds;
            }
            if (_sim.Phase == MatchSim.MatchPhase.PointFreeze)
            {
                Vec2I point = _sim.PointLog.Count > 0 ? _sim.PointLog[_sim.PointLog.Count - 1] : new Vec2I(MyTeam, (int)MatchSim.CaptureKind.Base);
                bool won = point.X == MyTeam;
                if (point.Y == (int)MatchSim.CaptureKind.AllTagged)
                {
                    s.PointSubject = won ? "All enemies" : "All allies";
                    s.PointOutcome = "TAGGED";
                }
                else
                {
                    s.PointSubject = won ? "Enemy base" : "Ally base";
                    s.PointOutcome = "CAPTURED";
                }
                s.PointScore = $"{s.Mine} - {s.Theirs}";
            }
            else s.PointSubject = s.PointOutcome = s.PointScore = "";
        }

        /// <summary>match_hud.gd _sync_street_pad and practice_match_presentation.gd street_status.</summary>
        void RefreshPad()
        {
            StreetPadState pad = Pad;
            if (!(_sim.Mode is StreetMode mode) || !_sim.Players.TryGetValue(Me, out PlayerState player))
            {
                pad.Shown = false;
                return;
            }
            bool playing = Local.Playing;
            pad.Shown = Local.Shown && !player.Tagged;
            pad.Action = StreetAction.None;
            pad.Fill = 0f;
            pad.CanAct = false;
            pad.Aiming = Input.ThrowHeld;
            if (playing && mode is TumbangPreso game && game.IsAttacker(player))
            {
                pad.Fill = (float)game.Charge(player.Id);
                if (game.CanThrow(player)) pad.Action = StreetAction.Throw;
                else if (!game.HasSlipper(player.Id))
                {
                    pad.Action = StreetAction.Kick;
                    pad.CanAct = game.CanKick(player);
                }
            }
            else if (playing && mode is Patintero lines && !lines.IsJailed(player.Id))
            {
                pad.Action = StreetAction.Dive;
                pad.Fill = (float)lines.DiveReadyShare(player.Id);
                pad.CanAct = lines.CanDive(player);
            }
            else if (playing && mode is LangitLupa langit && langit.IsAttacker(player))
            {
                pad.Action = StreetAction.Lupa;
                pad.Fill = (float)langit.MoundReadyShare(player.Id);
                pad.CanAct = langit.CanRaise(player);
            }
            // a Throw button that went away (the slipper is gone) lets go of the aim
            if (pad.Action != StreetAction.Throw && Input.ThrowHeld) Input.ThrowRelease(true);
            pad.OnSidewalk = mode is Patintero p && p.IsJailed(player.Id);
            (pad.Title, pad.Hint) = StreetStatus(mode, player);
        }

        /// <summary>What your kid is to do now (practice_match_presentation.gd street_status): [what, how].</summary>
        (string, string) StreetStatus(StreetMode mode, PlayerState player)
        {
            if (mode is TumbangPreso game)
            {
                int id = player.Id;
                if (id == game.TayaId) return ("YOU'RE IT!", game.CanUp ? "Move into a thrower's circle to tag it!" : "Fetch the can and put it back!");
                if (game.Flights.ContainsKey(id)) return ("", "");
                if (game.Fetched.ContainsKey(id)) return ("RUN TO THE OTHER END!", "");
                if (game.HasSlipper(id)) return ("YOUR THROW!", "Hold THROW to aim, let go to throw.");
                if (game.ScaredLeft.TryGetValue(id, out double scared) && player.Position.y >= game.Rules.HomeZ)
                    return ("FETCH IT OR YOU'RE IT!", $"Step into the fetch area: {(int)Math.Ceiling(scared)}");
                return ("FETCH YOUR SLIPPER!", "Kick the can to keep the taya busy.");
            }
            if (mode is LangitLupa langit)
            {
                const string dark = "It's dark! You only see the kids near you.";
                bool brownout = langit.IsBrownout();
                if (player.Id == langit.TayaId) return ("YOU'RE IT!", brownout ? dark : "Tag a kid on the street. Kids on a langit are safe for a while.");
                if (langit.IsSafe(player))
                    return ("SAFE!", $"Safe for {(int)Math.Ceiling(langit.SafeLeft.GetOrDefault(player.Id, 0.0))} s. You don't rest up here.");
                if (langit.OnLangit(player)) return ("GET DOWN!", "Your safe time is up: the street fills it again.");
                return ("RUN!", brownout ? dark : "Get on a langit, or press LUPA to raise your own.");
            }
            var lines = (Patintero)mode;
            if (lines.IsJailed(player.Id))
                return ("TAGGED!", "Walk the sidewalk and cheer your team on in quick chat. A teammate who scores frees you!");
            if (lines.IsAttacker(player))
            {
                if (!lines.FirstGuardPassed) return ("YOU RUN!", $"Get past the first guard in {(int)Math.Ceiling(Math.Max(lines.StallLeft, 0.0))} s!");
                if (lines.Crossed.ContainsKey(player.Id)) return ("YOU RUN!", "Back to the base!");
                if (!lines.MiddleCrossed.ContainsKey(player.Id)) return ("YOU RUN!", "Cross the middle line, then get past the last guard!");
                return ("YOU RUN!", "Past the last guard, then back to the base!");
            }
            return ("YOU GUARD!", "Tag all the runners. Move into a runner's circle!");
        }

        /// <summary>A free-for-all street game's score column (score_column.gd): every kid's points, best first.</summary>
        public List<StreetScore> StreetScores()
        {
            var rows = new List<StreetScore>();
            if (!(_sim.Mode is TayaGame game)) return rows;
            foreach (int id in game.Ranking())
                rows.Add(new StreetScore { Id = id, Points = game.Points[id], Name = NameOf(id), IsLocal = id == Me, IsTaya = id == game.TayaId });
            return rows;
        }

        /// <summary>The results card's words (match_results.gd _fill).</summary>
        public ResultInfo Result()
        {
            var info = new ResultInfo();
            int team = MyTeam;
            info.Won = _sim.Score.Winner == team;
            info.Mine = _sim.Score.Points[team];
            info.Theirs = _sim.Score.Points[1 - team];
            if (_sim.Mode is StreetMode mode)
            {
                StreetMode.Result r = mode.LocalResult(_sim, Me);
                info.Won = r.Won;
                info.Mine = r.Mine;
                info.Theirs = r.Theirs;
            }
            info.Title = info.Won ? "Victory" : "Defeat";
            int allies = 0, enemies = 0;
            foreach (var pair in _sim.Players)
                if (pair.Value.Team == team) allies++;
                else enemies++;
            if (_sim.Mode is StreetMode m)
            {
                int minutes = (int)Math.Round(m.Rules.MatchSeconds / 60.0);
                info.Subtitle = m.IsFreeForAll()
                    ? $"{_modeText} ({m.Rules.Title} · {minutes} min) ({allies} kids)"
                    : $"{_modeText} ({m.Rules.Title} · {minutes} min) ({allies}v{enemies})";
            }
            else info.Subtitle = $"{_modeText} (First to {_sim.Score.FirstTo}) ({allies}v{enemies})";
            var log = new List<(int team, int how)>();
            foreach (Vec2I entry in _sim.PointLog) log.Add((entry.X, entry.Y));
            foreach (var (ours, how, times) in HudText.HowGroups(log, team)) info.How.Add((ours, HudText.GroupText(ours, how, times)));
            return info;
        }

        static Vec2 V(Vector2 v) => new Vec2(v.X, v.Y);
        static Vector2 P(Vec2 v) => new Vector2(v.X, v.Y);
    }
}
