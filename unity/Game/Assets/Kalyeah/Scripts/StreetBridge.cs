// =====================================================================================================================================
// StreetBridge: Engine-free C# bridge for Kalyeah's 3 Filipino Larong Kalye mini-game modes:
//   - Patintero      (3v3 grid line runners vs guards, dive burst, stall clock, sidewalk jail)
//   - Tumbang Preso  (6-kid FFA slipper throws vs tin can + taya, hold-to-charge aim, kick, scared timer)
//   - Langit Lupa    (6-kid FFA heaven platforms + earth mounds vs taya, wear-out platforms, periodic brownout)
// =====================================================================================================================================
using System;
using System.Collections.Generic;
using System.Numerics;
#if KALYEAH_SIM_PORTED
using Kalyeah.Net;
using Kalyeah.Sim;
#endif

namespace Kalyeah.Game
{
    public enum StreetGameKind
    {
        Patintero = 0,
        TumbangPreso = 1,
        LangitLupa = 2,
    }

    public enum StreetRoleKind
    {
        Runner = 0,
        Guard = 1,
        Thrower = 2,
        Taya = 3,
    }

    public struct StreetKidSnapshot
    {
        public int Id;
        public int Team;
        public string CharacterId;
        public Vector2 Position;
        public Vector2 Facing;
        public float Speed01;
        public StreetRoleKind Role;
        public int Score;
        public bool Visible;
        public bool Taggable;

        // Stamina (Langit Lupa / running)
        public float Stamina;
        public float MaxStamina;

        // Patintero state
        public bool Jailed;
        public bool Stumbling;
        public bool Diving;
        public bool CrossedFarLine;

        // Tumbang Preso state
        public bool HasSlipper;
        public bool FetchedSlipper;
        public bool CarryingCan;
        public float ScaredLeft;
        public bool AimingThrow;
        public float ThrowCharge01;
        public float AimRange;
        public Vector2 AimDirection;

        // Langit Lupa state
        public bool OnLangit;
        public bool SafeOnLangit;
        public float SafeShare01;
        public float SurviveLeft;
    }

    public struct PatinteroLineSnapshot
    {
        public int GuardId;
        public bool IsVertical;
        public float Coordinate;
        public float SpanHalfExtent;
    }

    public struct PatinteroSnapshot
    {
        public int AttackTeam;
        public float HomeZ;
        public float GoalZ;
        public float LineSpan;
        public float SidewalkWidth;
        public float StallLeft;
        public float StallTotal;
        public float GraceLeft;
        public float ResetLeft;
        public bool FirstGuardPassed;
        public IReadOnlyList<PatinteroLineSnapshot> GuardLines;
    }

    public struct SlipperSnapshot
    {
        public int OwnerId;
        public Vector2 Position;
        public Vector2 Direction;
        public bool InFlight;
        public float BusyLeft;
    }

    public struct TumbangPresoSnapshot
    {
        public int TayaId;
        public int CanCarrierId;
        public int WinnerId;
        public Vector2 CanSpot;
        public Vector2 CanPosition;
        public bool CanUp;
        public float CanBusyLeft;
        public float CanHitRadius;
        public float TagCircleRadius;
        public float HomeZ;
        public float GoalZ;
        public float ThrowMinRange;
        public float ThrowMaxRange;
        public float GraceLeft;
        public float ResetLeft;
        public IReadOnlyList<SlipperSnapshot> Slippers;
    }

    public struct LangitPlatformSnapshot
    {
        public int Index;
        public GroundRect Bounds;
        public bool Standing;
        public float Wear01;
        public float WearSecondsLeft;
    }

    public struct LangitMoundSnapshot
    {
        public int OwnerId;
        public Vector2 Position;
        public float Radius;
        public float TimeLeft;
    }

    public struct LangitLupaSnapshot
    {
        public int TayaId;
        public int WinnerId;
        public bool IsBrownout;
        public float BrownoutLeft;
        public float BrownoutSight;
        public float MoundRadius;
        public float GraceLeft;
        public float ResetLeft;
        public IReadOnlyList<LangitPlatformSnapshot> Platforms;
        public IReadOnlyList<LangitMoundSnapshot> Mounds;
    }

    /// <summary>Context-sensitive action button state for the active street mini-game HUD.</summary>
    public struct StreetActionInfo
    {
        public string PrimaryLabel;
        public bool PrimaryVisible;
        public bool PrimaryEnabled;
        public bool PrimaryHoldToCharge;
        public float PrimaryReady01;

        public string SecondaryLabel;
        public bool SecondaryVisible;
        public bool SecondaryEnabled;
        public float SecondaryReady01;
    }

    /// <summary>Result summary for post-match screen and LP progression.</summary>
    public struct StreetMatchResult
    {
        public bool Won;
        public int MyPoints;
        public int BestOpponentPoints;
    }

    /// <summary>Bridge driving Patintero, Tumbang Preso, and Langit Lupa offline (with StreetModeCoach bots) or online.</summary>
    public sealed class StreetBridge
    {
        public const int LocalId = 1;
        public const int LocalTeam = 0;
        public static readonly int[] StreetAllyIds = { 2, 3 };
        public static readonly int[] StreetEnemyIds = { 4, 5, 6 };

        readonly List<StreetKidSnapshot> _kids = new List<StreetKidSnapshot>(8);
        readonly List<PatinteroLineSnapshot> _guardLines = new List<PatinteroLineSnapshot>(4);
        readonly List<SlipperSnapshot> _slippers = new List<SlipperSnapshot>(8);
        readonly List<LangitPlatformSnapshot> _platforms = new List<LangitPlatformSnapshot>(12);
        readonly List<LangitMoundSnapshot> _mounds = new List<LangitMoundSnapshot>(8);
        readonly Dictionary<int, Vector2> _lastPositions = new Dictionary<int, Vector2>(8);

        readonly float _runPerTick;
        Vector2 _move;
        Vector2 _aim;
        int _buttons;
        Vector2 _moveTarget;
        bool _hasMoveTarget;

        public StreetGameKind Kind { get; }
        public string ModeId { get; }
        public string DisplayTitle { get; }
        public bool IsFreeForAll { get; }
        public float TickDt { get; }
        public float ArenaWidth { get; }
        public float ArenaLength { get; }
        public bool IsOnline { get; }

        public IReadOnlyList<StreetKidSnapshot> Kids => _kids;
        public PatinteroSnapshot PatinteroState { get; private set; }
        public TumbangPresoSnapshot TumbangPresoState { get; private set; }
        public LangitLupaSnapshot LangitLupaState { get; private set; }
        public StreetActionInfo LocalActions { get; private set; }

        public event Action<int, int> Tagged;
        public event Action<int, int> Scored;
        public event Action<int> RolesSwapped;
        public event Action<int> MatchWon;
        public event Action<int> SlipperThrown;
        public event Action<int> CanKicked;
        public event Action<int> CanPlaced;
        public event Action<Vector2> CanFlung;
        public event Action<int, Vector2> Dived;
        public event Action<int> MoundRaised;
        public event Action<int> MoundFell;
        public event Action<int> PlatformBroke;

#if KALYEAH_SIM_PORTED
        readonly MatchSim _offlineSim;
        readonly StreetModeCoach _coach;
        readonly GameClient _client;
        MatchSim _boundSim;
        IStreetMode _boundMode;
        int _onlineInputTick;

        MatchSim ActiveSim => (_client != null && _client.Mirror != null) ? _client.Mirror : _offlineSim;

        public StreetBridge(
            DataSource data,
            StreetGameKind kind,
            long seed,
            string localCharacter = "junjun",
            GameClient onlineClient = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            Kind = kind;
            ModeId = ModeIdOf(kind);
            _client = onlineClient;
            IsOnline = onlineClient != null;

            string ruleFile = "rules__" + ModeId;
            SimData simData = SimBridge.LoadSimDataGraph(
                data,
                "rules__game_rules",
                "rules__map_layout",
                "rules__street_games",
                ruleFile);

            GameRules rules = simData.GameRules.Duplicate();
            MapLayout layout = simData.MapLayout.Duplicate();
            StreetModeRules modeRules = simData.StreetModeRules(ModeId);
            StreetMode createdMode = StreetMode.Create(modeRules);

            DisplayTitle = !string.IsNullOrEmpty(modeRules.Title) ? modeRules.Title : DefaultTitleOf(kind);
            IsFreeForAll = createdMode.IsFreeForAll();
            TickDt = 1f / rules.TickRate;
            _runPerTick = (float)rules.MoveSpeed * TickDt;
            ArenaWidth = (float)layout.LaneWidth;
            ArenaLength = (float)layout.LaneLength;

            _offlineSim = new MatchSim(rules, layout, seed);

            if (_client != null)
            {
                _coach = null;
                EnsureBound(ActiveSim);
                Refresh();
                return;
            }

            _offlineSim.AddPlayer(LocalId, LocalTeam);
            _coach = new StreetModeCoach();
            foreach (int id in StreetAllyIds)
            {
                _offlineSim.AddPlayer(id, LocalTeam);
                _coach.Add(id);
            }
            foreach (int id in StreetEnemyIds)
            {
                _offlineSim.AddPlayer(id, IsFreeForAll ? LocalTeam : (1 - LocalTeam));
                _coach.Add(id);
            }

            _offlineSim.AssignCharacters();
            if (!string.IsNullOrWhiteSpace(localCharacter))
                _offlineSim.SetCharacter(LocalId, localCharacter.Trim());

            _offlineSim.StartMode(createdMode);
            _offlineSim.BeginIntro();
            EnsureBound(_offlineSim);
            Refresh();
        }

        void EnsureBound(MatchSim sim)
        {
            if (sim == null) return;
            if (!ReferenceEquals(_boundSim, sim))
            {
                _boundSim = sim;
                sim.MatchWon += winner => MatchWon?.Invoke(winner);
            }
            if (sim.Mode != null && !ReferenceEquals(_boundMode, sim.Mode))
            {
                _boundMode = sim.Mode;
                if (sim.Mode is StreetMode sm)
                {
                    sm.Tagged += (tagger, target) => Tagged?.Invoke(tagger, target);
                    sm.Scored += (team, pid) => Scored?.Invoke(team, pid);
                    sm.RolesSwapped += who => RolesSwapped?.Invoke(who);
                }
                if (sim.Mode is Patintero pat)
                {
                    pat.Dived += (id, way, _, __) => Dived?.Invoke(id, new Vector2(way.X, way.Y));
                }
                else if (sim.Mode is TumbangPreso tp)
                {
                    tp.SlipperThrown += id => SlipperThrown?.Invoke(id);
                    tp.CanKicked += id => CanKicked?.Invoke(id);
                    tp.CanPlaced += id => CanPlaced?.Invoke(id);
                    tp.CanFlung += (from, _) => CanFlung?.Invoke(new Vector2(from.X, from.Y));
                }
                else if (sim.Mode is LangitLupa ll)
                {
                    ll.MoundRaised += id => MoundRaised?.Invoke(id);
                    ll.MoundFell += id => MoundFell?.Invoke(id);
                    ll.SpotBroke += idx => PlatformBroke?.Invoke(idx);
                }
            }
        }

        public int LocalPlayerId => (_client != null && _client.PlayerId > 0) ? _client.PlayerId : LocalId;
        public int Tick => ActiveSim.Tick;
        public float TimeLeft => ActiveSim.Mode != null ? (float)ActiveSim.Mode.TimeLeft : 0f;
        public bool IsIntro => ActiveSim.Phase == MatchSim.MatchPhase.Intro;
        public bool IsResetting => ActiveSim.Mode != null && ActiveSim.Mode.IsResetting();
        public bool IsMatchOver => ActiveSim.Phase == MatchSim.MatchPhase.MatchOver;
        public int Winner => ActiveSim.Score.Winner;

        public int TeamScore(int team)
        {
            int[] pts = ActiveSim.Score.Points;
            return (team >= 0 && team < pts.Length) ? pts[team] : 0;
        }

        public int PlayerScore(int playerId)
        {
            if (ActiveSim.Mode is TayaGame taya)
                return taya.Points.GetOrDefault(playerId, 0);
            if (ActiveSim.Players.TryGetValue(playerId, out PlayerState p))
                return TeamScore(p.Team);
            return 0;
        }

        public StreetMatchResult GetLocalResult()
        {
            MatchSim sim = ActiveSim;
            if (sim.Mode is StreetMode sm)
            {
                StreetMode.Result r = sm.LocalResult(sim, LocalPlayerId);
                return new StreetMatchResult
                {
                    Won = r.Won,
                    MyPoints = r.Mine,
                    BestOpponentPoints = r.Theirs,
                };
            }
            return default;
        }

        public bool SkipIntro()
        {
            if (_client != null || _offlineSim.Phase != MatchSim.MatchPhase.Intro) return false;
            if (_offlineSim.Mode is TumbangPreso tp && tp.Rules.RollSeconds > 0.0) return false;
            _offlineSim.IntroLeft = 0.0;
            _offlineSim.Phase = MatchSim.MatchPhase.Playing;
            Refresh();
            return true;
        }

        public void Step()
        {
            if (_client != null)
            {
                EnsureBound(ActiveSim);
                if (_client.IsOnline())
                {
                    _onlineInputTick += 1;
                    _client.SendInput(BuildInput(_onlineInputTick), TickDt);
                }
                _client.Interpolate();
                Refresh();
                return;
            }

            _coach?.Drive(_offlineSim);
            _offlineSim.SetInput(LocalId, BuildInput(_offlineSim.Tick));
            _offlineSim.Step(TickDt);
            Refresh();
        }

        public void SyncInterpolated()
        {
            if (_client == null || _client.Mirror == null) return;
            EnsureBound(_client.Mirror);
            _client.Interpolate();
            Refresh();
        }

        PlayerInput BuildInput(int tick)
        {
            int bits = _buttons;
            if (_hasMoveTarget) bits |= PlayerInput.BtnMoveTarget;
            PlayerInput input = PlayerInput.Create(
                new Vec2(_move.X, _move.Y),
                new Vec2(_aim.X, _aim.Y),
                bits,
                tick);
            if (_hasMoveTarget)
                input.MoveTarget = new Vec2(_moveTarget.X, _moveTarget.Y);
            return input;
        }

        void Refresh()
        {
            MatchSim sim = ActiveSim;
            EnsureBound(sim);
            _kids.Clear();

            int localId = LocalPlayerId;
            StreetMode smMode = sim.Mode as StreetMode;
            Patintero pat = sim.Mode as Patintero;
            TumbangPreso tp = sim.Mode as TumbangPreso;
            LangitLupa ll = sim.Mode as LangitLupa;

            foreach (var pair in sim.Players)
            {
                PlayerState p = pair.Value;
                var pos = new Vector2(p.Position.X, p.Position.Y);
                var facing = new Vector2(p.Facing.X, p.Facing.Y);
                float speed = MeasureSpeed01(p.Id, pos);

                bool isAttacker = smMode != null && smMode.IsAttacker(p);
                StreetRoleKind role;
                if (pat != null)
                    role = isAttacker ? StreetRoleKind.Runner : StreetRoleKind.Guard;
                else if (tp != null)
                    role = isAttacker ? StreetRoleKind.Thrower : StreetRoleKind.Taya;
                else
                    role = isAttacker ? StreetRoleKind.Runner : StreetRoleKind.Taya;

                bool visible = ll == null || ll.VisibleTo(sim, localId, p.Id);
                bool taggable = smMode != null && smMode.IsTaggable(p);

                Vec2 aimDir = tp != null && tp.AimDir.TryGetValue(p.Id, out Vec2 ad) ? ad : p.Facing;

                _kids.Add(new StreetKidSnapshot
                {
                    Id = p.Id,
                    Team = p.Team,
                    CharacterId = p.CharacterId ?? "",
                    Position = pos,
                    Facing = facing,
                    Speed01 = speed,
                    Role = role,
                    Score = PlayerScore(p.Id),
                    Visible = visible,
                    Taggable = taggable,

                    Stamina = (float)p.Stamina,
                    MaxStamina = (float)sim.Rules.StaminaMax,

                    Jailed = pat != null && pat.IsJailed(p.Id),
                    Stumbling = pat != null && pat.Caught.ContainsKey(p.Id),
                    Diving = pat != null && pat.IsBusy(p.Id),
                    CrossedFarLine = pat != null && pat.Crossed.ContainsKey(p.Id),

                    HasSlipper = tp != null && tp.HasSlipper(p.Id),
                    FetchedSlipper = tp != null && tp.Fetched.ContainsKey(p.Id),
                    CarryingCan = tp != null && tp.CanCarrier == p.Id,
                    ScaredLeft = tp != null ? (float)tp.ScaredLeft.GetOrDefault(p.Id, 0.0) : 0f,
                    AimingThrow = tp != null && tp.IsAiming(p.Id),
                    ThrowCharge01 = tp != null ? (float)tp.Charge(p.Id) : 0f,
                    AimRange = tp != null ? (float)tp.AimRange(p.Id) : 0f,
                    AimDirection = new Vector2(aimDir.X, aimDir.Y),

                    OnLangit = ll != null && ll.OnLangit(p),
                    SafeOnLangit = ll != null && ll.IsSafe(p),
                    SafeShare01 = ll != null ? (float)ll.SafeShare(p.Id) : 0f,
                    SurviveLeft = ll != null ? (float)ll.SurviveLeft.GetOrDefault(p.Id, 0.0) : 0f,
                });
            }

            if (pat != null) RefreshPatintero(sim, pat, localId);
            else if (tp != null) RefreshTumbangPreso(sim, tp, localId);
            else if (ll != null) RefreshLangitLupa(sim, ll, localId);
        }

        void RefreshPatintero(MatchSim sim, Patintero pat, int localId)
        {
            _guardLines.Clear();
            float halfWidth = (float)(sim.Layout.LaneWidth * 0.5);
            float lineSpan = (float)pat.Rules.LineSpan;
            foreach (var pair in pat.GuardAxis)
            {
                int gid = pair.Key;
                bool vertical = pair.Value == Patintero.Axis.Vertical;
                float coord = (float)pat.GuardLine.GetOrDefault(gid, 0.0);
                _guardLines.Add(new PatinteroLineSnapshot
                {
                    GuardId = gid,
                    IsVertical = vertical,
                    Coordinate = coord,
                    SpanHalfExtent = vertical ? lineSpan : halfWidth,
                });
            }

            PatinteroState = new PatinteroSnapshot
            {
                AttackTeam = pat.AttackTeam,
                HomeZ = (float)pat.Rules.HomeZ,
                GoalZ = (float)pat.Rules.GoalZ,
                LineSpan = lineSpan,
                SidewalkWidth = (float)pat.Rules.SidewalkWidth,
                StallLeft = (float)pat.StallLeft,
                StallTotal = (float)pat.Rules.StallSeconds,
                GraceLeft = (float)pat.GraceLeft,
                ResetLeft = (float)pat.ResetLeft,
                FirstGuardPassed = pat.FirstGuardPassed,
                GuardLines = _guardLines,
            };

            bool canDive = sim.Players.TryGetValue(localId, out PlayerState lp) && pat.CanDive(lp);
            LocalActions = new StreetActionInfo
            {
                PrimaryLabel = "",
                PrimaryVisible = false,
                SecondaryLabel = "DIVE",
                SecondaryVisible = true,
                SecondaryEnabled = canDive,
                SecondaryReady01 = (float)pat.DiveReadyShare(localId),
            };
        }

        void RefreshTumbangPreso(MatchSim sim, TumbangPreso tp, int localId)
        {
            _slippers.Clear();
            foreach (var pair in tp.Slippers)
            {
                _slippers.Add(new SlipperSnapshot
                {
                    OwnerId = pair.Key,
                    Position = new Vector2(pair.Value.X, pair.Value.Y),
                    Direction = Vector2.UnitY,
                    InFlight = false,
                    BusyLeft = (float)tp.SlipperBusy.GetOrDefault(pair.Key, 0.0),
                });
            }
            foreach (var pair in tp.Flights)
            {
                WeaponSystem.Projectile proj = pair.Value;
                if (proj == null) continue;
                _slippers.Add(new SlipperSnapshot
                {
                    OwnerId = pair.Key,
                    Position = new Vector2(proj.Position.X, proj.Position.Y),
                    Direction = new Vector2(proj.Direction.X, proj.Direction.Y),
                    InFlight = true,
                    BusyLeft = 0f,
                });
            }

            TumbangPresoState = new TumbangPresoSnapshot
            {
                TayaId = tp.TayaId,
                CanCarrierId = tp.CanCarrier,
                WinnerId = tp.WinnerId,
                CanSpot = new Vector2(tp.Rules.CanSpot.X, tp.Rules.CanSpot.Y),
                CanPosition = new Vector2(tp.CanPosition.X, tp.CanPosition.Y),
                CanUp = tp.CanUp,
                CanBusyLeft = (float)tp.CanBusyLeft,
                CanHitRadius = (float)tp.Rules.CanHitRadius,
                TagCircleRadius = (float)tp.Rules.TagCircleRadius,
                HomeZ = (float)tp.Rules.HomeZ,
                GoalZ = (float)tp.Rules.GoalZ,
                ThrowMinRange = (float)tp.Rules.ThrowMinRange,
                ThrowMaxRange = (float)tp.Rules.ThrowMaxRange,
                GraceLeft = (float)tp.GraceLeft,
                ResetLeft = (float)tp.ResetLeft,
                Slippers = _slippers,
            };

            bool isTaya = tp.TayaId == localId;
            bool canThrow = false;
            bool canKick = false;
            if (sim.Players.TryGetValue(localId, out PlayerState lp))
            {
                canThrow = tp.CanThrow(lp);
                canKick = tp.CanKick(lp);
            }
            float kickCd = (float)tp.KickLeft.GetOrDefault(localId, 0.0);
            float kickTotal = (float)Math.Max(tp.Rules.KickCooldown, 0.001);

            LocalActions = new StreetActionInfo
            {
                PrimaryLabel = "TSINELAS",
                PrimaryVisible = !isTaya,
                PrimaryEnabled = canThrow || tp.IsAiming(localId),
                PrimaryHoldToCharge = true,
                PrimaryReady01 = canThrow ? 1f : (float)tp.Charge(localId),

                SecondaryLabel = "SIPA",
                SecondaryVisible = !isTaya,
                SecondaryEnabled = canKick,
                SecondaryReady01 = Math.Clamp(1f - kickCd / kickTotal, 0f, 1f),
            };
        }

        void RefreshLangitLupa(MatchSim sim, LangitLupa ll, int localId)
        {
            _platforms.Clear();
            for (int i = 0; i < ll.Rules.LangitSpots.Count; i++)
            {
                Rect2 r = ll.Rules.LangitSpots[i];
                _platforms.Add(new LangitPlatformSnapshot
                {
                    Index = i,
                    Bounds = new GroundRect(r.Position.X, r.Position.Y, r.Size.X, r.Size.Y),
                    Standing = ll.SpotStanding(i),
                    Wear01 = (float)ll.WearShare(i),
                    WearSecondsLeft = (float)ll.WearLeft.GetOrDefault(i, 0.0),
                });
            }

            _mounds.Clear();
            foreach (var pair in ll.Mounds)
            {
                if (!ll.VisibleTo(sim, localId, pair.Key)) continue;
                _mounds.Add(new LangitMoundSnapshot
                {
                    OwnerId = pair.Key,
                    Position = new Vector2(pair.Value.X, pair.Value.Y),
                    Radius = (float)ll.Rules.MoundRadius,
                    TimeLeft = (float)ll.MoundLeft.GetOrDefault(pair.Key, 0.0),
                });
            }

            LangitLupaState = new LangitLupaSnapshot
            {
                TayaId = ll.TayaId,
                WinnerId = ll.WinnerId,
                IsBrownout = ll.IsBrownout(),
                BrownoutLeft = (float)ll.BrownoutLeft(),
                BrownoutSight = (float)ll.Rules.BrownoutSight,
                MoundRadius = (float)ll.Rules.MoundRadius,
                GraceLeft = (float)ll.GraceLeft,
                ResetLeft = (float)ll.ResetLeft,
                Platforms = _platforms,
                Mounds = _mounds,
            };

            bool isTaya = ll.TayaId == localId;
            bool canRaise = sim.Players.TryGetValue(localId, out PlayerState lp) && ll.CanRaise(lp);
            LocalActions = new StreetActionInfo
            {
                PrimaryLabel = "",
                PrimaryVisible = false,
                SecondaryLabel = "LUPA",
                SecondaryVisible = !isTaya,
                SecondaryEnabled = canRaise,
                SecondaryReady01 = (float)ll.MoundReadyShare(localId),
            };
        }
#else
        int _tick;
        float _timeLeft = 120f;

        public StreetBridge(
            DataSource data,
            StreetGameKind kind,
            long seed,
            string localCharacter = "junjun",
            object onlineClient = null)
        {
            Kind = kind;
            ModeId = ModeIdOf(kind);
            DisplayTitle = DefaultTitleOf(kind);
            IsFreeForAll = kind != StreetGameKind.Patintero;
            TickDt = 1f / 30f;
            _runPerTick = 6.4f * TickDt;
            ArenaWidth = 24f;
            ArenaLength = 48f;
            IsOnline = false;
            _kids.Add(new StreetKidSnapshot
            {
                Id = LocalId,
                Team = LocalTeam,
                CharacterId = localCharacter ?? "junjun",
                Facing = new Vector2(0f, -1f),
                Visible = true,
            });
            PatinteroState = new PatinteroSnapshot { GuardLines = _guardLines };
            TumbangPresoState = new TumbangPresoSnapshot { Slippers = _slippers };
            LangitLupaState = new LangitLupaSnapshot { Platforms = _platforms, Mounds = _mounds };
            TouchUnusedEvents();
        }

        public int LocalPlayerId => LocalId;
        public int Tick => _tick;
        public float TimeLeft => _timeLeft;
        public bool IsIntro => false;
        public bool IsResetting => false;
        public bool IsMatchOver => _timeLeft <= 0f;
        public int Winner => 0;
        public int TeamScore(int team) => 0;
        public int PlayerScore(int playerId) => 0;
        public StreetMatchResult GetLocalResult() => default;
        public bool SkipIntro() => false;

        public void Step()
        {
            _tick += 1;
            _timeLeft = MathF.Max(0f, _timeLeft - TickDt);
        }

        public void SyncInterpolated() { }

        void TouchUnusedEvents()
        {
            _ = Tagged; _ = Scored; _ = RolesSwapped; _ = MatchWon; _ = SlipperThrown;
            _ = CanKicked; _ = CanPlaced; _ = CanFlung; _ = Dived; _ = MoundRaised;
            _ = MoundFell; _ = PlatformBroke;
        }
#endif

        /// <summary>High-level street mode input: movement, aim direction, primary action (Throw hold), secondary action (Dive / Kick / Lupa).</summary>
        public void SetLocalInput(
            Vector2 move,
            Vector2 aim,
            bool primaryHeld,
            bool secondaryPressed,
            bool cancelAim = false,
            Vector2? moveTarget = null)
        {
            int bits = 0;
            if (primaryHeld) bits |= InputButtons.Card1;
            if (secondaryPressed) bits |= InputButtons.Card2;
            if (cancelAim) bits |= InputButtons.AimCancel;
            SetRawInput(move, aim, bits, moveTarget);
        }

        public void SetRawInput(Vector2 move, Vector2 aim, int buttons, Vector2? moveTarget = null)
        {
            _move = move;
            _aim = aim;
            _buttons = buttons;
            _hasMoveTarget = moveTarget.HasValue;
            _moveTarget = moveTarget ?? Vector2.Zero;
        }

        public StreetKidSnapshot? LocalKid => Find(LocalPlayerId);

        public int LocalPlayerTeam => LocalKid.HasValue ? LocalKid.Value.Team : LocalTeam;

        public StreetKidSnapshot? Find(int id)
        {
            for (int i = 0; i < _kids.Count; i++)
                if (_kids[i].Id == id) return _kids[i];
            return null;
        }

        public static string ModeIdOf(StreetGameKind kind)
        {
            switch (kind)
            {
                case StreetGameKind.TumbangPreso: return "tumbang_preso";
                case StreetGameKind.LangitLupa: return "langit_lupa";
                default: return "patintero";
            }
        }

        public static StreetGameKind? KindFromModeId(string modeId)
        {
            switch (modeId)
            {
                case "patintero": return StreetGameKind.Patintero;
                case "tumbang_preso": return StreetGameKind.TumbangPreso;
                case "langit_lupa": return StreetGameKind.LangitLupa;
                default: return null;
            }
        }

        public static string DefaultTitleOf(StreetGameKind kind)
        {
            switch (kind)
            {
                case StreetGameKind.TumbangPreso: return "Tumbang Preso";
                case StreetGameKind.LangitLupa: return "Langit Lupa";
                default: return "Patintero";
            }
        }

        float MeasureSpeed01(int id, Vector2 position)
        {
            float speed = 0f;
            if (_lastPositions.TryGetValue(id, out Vector2 last) && _runPerTick > 0f)
                speed = Math.Clamp(Vector2.Distance(position, last) / _runPerTick, 0f, 1f);
            _lastPositions[id] = position;
            return speed;
        }
    }
}
