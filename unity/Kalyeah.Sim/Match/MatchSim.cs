using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// Authoritative match simulation (port of scripts/sim/match_sim.gd). Never touches the engine; the same class runs on the server,
    /// in offline practice and in tests. Advances only through <see cref="Step"/> and uses its own seeded RNG.
    /// GDScript names map snake_case to PascalCase (signals become C# events of the same name; the views subscribe to them).
    /// </summary>
    public sealed class MatchSim
    {
        // ---- signals ----
        /// <summary>(id, byId): `id` was tagged; `byId` dealt the blow (-1: scripted).</summary>
        public event Action<int, int> PlayerTagged;
        /// <summary>(id, byId): `byId` is the rescuing teammate, or -1 (a dummy walked out, or a round reset).</summary>
        public event Action<int, int> PlayerRescued;
        /// <summary>(team, how): a team scored (`how` is a CaptureKind). A freeze follows.</summary>
        public event Action<int, int> PointScored;
        public event Action<int> MatchWon;
        /// <summary>The freeze is over: everyone is back at their post at full HP.</summary>
        public event Action PointReset;
        /// <summary>The opening show is over: play starts now.</summary>
        public event Action IntroFinished;
        public event Action<int, StatusEffects.Type> EffectApplied;
        public event Action<int, int> PlayerHealed;
        /// <summary>(id, weaponId): a weapon was cast (for sounds and effects).</summary>
        public event Action<int, string> WeaponCast;
        /// <summary>(id, weaponId): a weapon's hit landed on player `id` (before its damage or effect).</summary>
        public event Action<int, string> WeaponHit;
        /// <summary>(id, slot): a new card took a slot of a hand (slot -1: the window).</summary>
        public event Action<int, int> CardDrawn;
        public event Action<int, int> PlayerDamaged;
        public event Action<int> Exhausted;
        public event Action<int> PlayerDashed;
        public event Action<int> UltimateCast;
        /// <summary>(playerId, powerupId, kind).</summary>
        public event Action<int, int, int> PowerupTaken;

        public const int ReleasedAlone = -1;
        /// <summary>Float slack for timers and the elixir bar (sums of 1/30 s ticks), not a gameplay number.</summary>
        public const double Epsilon = 0.00001;
        /// <summary>Mixed into the match seed for the trip rolls' own generator.</summary>
        public const long TripSeedMix = 0x7121B;
        public const int PowerupNone = 0;
        public const int PowerupPalamig = 1;
        public const int PowerupTeks = 2;
        public const int PowerupTuron = 3;
        /// <summary>Minimum `AirY` height (metres) that clears low pavement snares and jackstone fields.</summary>
        public const double JumpClearHeight = 0.45;
        public static readonly Vec2[] DefaultPowerupSpots = { new Vec2(-34.0, 0.0), new Vec2(0.0, 0.0), new Vec2(34.0, 0.0) };

        /// <summary>INTRO: the opening show; nothing moves.</summary>
        public enum MatchPhase { Playing, PointFreeze, MatchOver, Intro }
        public enum CaptureKind { None = -1, Base = 0, AllTagged = 1, Touched = 2, TooSlow = 3 }
        public enum ClockStage { Regular, Extended, Overtime }

        /// <summary>A Barangay merienda power-up stand (GDScript: a Dictionary with these keys).</summary>
        public sealed class Powerup
        {
            public int Id;
            public int Kind;
            public Vec2 Pos;
            public bool Active;
            public double RespawnLeft;
        }

        public GameRules Rules;
        public MapLayout Layout;
        public GodotRng Rng = new GodotRng();
        readonly GodotRng _tripRng = new GodotRng();
        public int Tick;
        /// <summary>id to state, in insertion order (stepping order is deterministic).</summary>
        public OrderedDict<int, PlayerState> Players = new OrderedDict<int, PlayerState>();
        /// <summary>Cover on the street (from the map layout).</summary>
        public List<Rect2> Obstacles = new List<Rect2>();
        public WeaponSystem Weapons = new WeaponSystem();
        public WeaponCasting Casting = new WeaponCasting();
        public MatchObjectives Objectives = new MatchObjectives();
        public Dictionary<string, WeaponDef> WeaponDefs = new Dictionary<string, WeaponDef>();
        public MatchScore Score;
        /// <summary>The team whose capture countdown is running (-1: none), how, and for how long.</summary>
        public int CaptureTeam = -1;
        public CaptureKind CurrentCaptureKind = CaptureKind.None;
        public double CaptureElapsed;
        public double CaptureNeeded;
        public double CaptureRate = 1.0;
        /// <summary>Per team (0, 1): the teammate rescuing now (-1: none), the seconds into it, and seconds until the area is ready.</summary>
        public int[] RescueRescuer = { -1, -1 };
        public double[] RescueElapsed = { 0.0, 0.0 };
        public double[] RescueReadyLeft = { 0.0, 0.0 };
        public int TagCounter;
        public double RoundTime;
        public MatchPhase Phase = MatchPhase.Playing;
        public double FreezeLeft;
        public double IntroLeft;
        public double MatchTime;
        /// <summary>How every point was won, in order: (team that scored, CaptureKind).</summary>
        public List<Vec2I> PointLog = new List<Vec2I>();
        public List<Powerup> Powerups = new List<Powerup>();
        /// <summary>A timed street game in place of Agawang Base's rescues and captures; null for Agawang Base.</summary>
        public IStreetMode Mode;

        readonly OrderedDict<int, PlayerInput> _inputs = new OrderedDict<int, PlayerInput>();
        readonly OrderedDict<int, DummyBrain> _brains = new OrderedDict<int, DummyBrain>();
        readonly Dictionary<int, int> _teamCounts = new Dictionary<int, int>();
        readonly List<Rect2> _blocking = new List<Rect2>();
        readonly List<Rect2> _walkBlocking = new List<Rect2>();
        readonly List<Rect2> _walkObstacles = new List<Rect2>();
        public BarangayNav Nav;
        int _castSerial;
        Rect2 _street;

        /// <summary>The bots driving players (id to brain), in the order they were added. Coaches and tests read it.</summary>
        public OrderedDict<int, DummyBrain> Brains => _brains;
        /// <summary>The latest input of every player (id to input).</summary>
        public OrderedDict<int, PlayerInput> Inputs => _inputs;

        public MatchSim(GameRules rules, MapLayout layout, long seed)
        {
            Rules = rules;
            Layout = layout;
            Rng.Seed = seed;
            _tripRng.Seed = seed ^ TripSeedMix;
            Obstacles = layout.ObstacleRects();
            _walkObstacles.AddRange(layout.WalkObstacleRects());
            _street = layout.LaneRect();
            _blocking.AddRange(layout.BoundaryRects());
            _blocking.AddRange(Obstacles);
            _walkBlocking.AddRange(layout.BoundaryRects());
            _walkBlocking.AddRange(_walkObstacles);
            Nav = new BarangayNav(layout, rules.PlayerRadius);
            foreach (WeaponDef def in rules.Weapons) WeaponDefs[def.Id] = def;
            if (rules.PrimaryWeapon != null) WeaponDefs[rules.PrimaryWeapon.Id] = rules.PrimaryWeapon;
            if (rules.UltimateWeapon != null) WeaponDefs[rules.UltimateWeapon.Id] = rules.UltimateWeapon;
            Score = new MatchScore(rules.FirstTo);
            CaptureNeeded = rules.CaptureSoloTime;
            RescueReadyLeft = new[] { rules.RescueCooldown, rules.RescueCooldown };
            ResetPowerups();
        }

        // ---- signal emitters (GDScript's `sim.<signal>.emit(...)` from the other sim classes) ----
        public void EmitPlayerTagged(int id, int byId) => PlayerTagged?.Invoke(id, byId);
        public void EmitPlayerRescued(int id, int byId) => PlayerRescued?.Invoke(id, byId);
        public void EmitPointScored(int team, int how) => PointScored?.Invoke(team, how);
        public void EmitMatchWon(int team) => MatchWon?.Invoke(team);
        public void EmitPointReset() => PointReset?.Invoke();
        public void EmitWeaponHit(int id, string weaponId) => WeaponHit?.Invoke(id, weaponId);
        public void EmitCardDrawn(int id, int slot) => CardDrawn?.Invoke(id, slot);

        /// <summary>Emits WeaponCast (the sim counts every cast first, like the GDScript's own first listener).</summary>
        public void EmitWeaponCast(int id, string weaponId)
        {
            _castSerial += 1;
            WeaponCast?.Invoke(id, weaponId);
        }

        /// <summary>Team 0 plays from the own (+Z) end, team 1 from the enemy (-Z) end.</summary>
        public int SideForTeam(int team) => team == 0 ? MapLayout.SideOwn : MapLayout.SideEnemy;

        /// <summary>First to `count` points (one of `Rules.FirstToChoices`; anything else is ignored).</summary>
        public void SetFirstTo(int count)
        {
            if (Rules.FirstToChoices.Contains(count)) Score = new MatchScore(count);
        }

        /// <summary>Plays a street game instead of Agawang Base and puts everyone in their places. Call once all players are in.</summary>
        public void StartMode(IStreetMode mode)
        {
            Mode = mode;
            Powerups.Clear();
            Obstacles.Clear();
            _walkObstacles.Clear();
            _blocking.Clear();
            _blocking.AddRange(Layout.BoundaryRects());
            _walkBlocking.Clear();
            _walkBlocking.AddRange(Layout.BoundaryRects());
            Score.FirstTo = 0;
            Mode.Setup(this);
        }

        /// <summary>Client-side prediction: runs only the movement part of a tick for one player.</summary>
        public void PredictMove(int id, PlayerInput input, double dt)
        {
            PlayerState state = Players.GetOrDefault(id);
            if (state == null || state.Tagged || Phase != MatchPhase.Playing || (Mode != null && Mode.IsResetting())) return;
            Move(state, input, dt);
        }

        public PlayerState AddPlayer(int id, int team)
        {
            int slot = _teamCounts.TryGetValue(team, out int had) ? had : 0;
            _teamCounts[team] = slot + 1;
            int side = SideForTeam(team);
            var state = new PlayerState { Id = id, Team = team };
            state.SpawnPosition = SpawnPosition(side, slot);
            state.Position = state.SpawnPosition;
            state.Facing = new Vec2(0.0, -side);
            state.Hp = Rules.PlayerMaxHp;
            state.Radius = Rules.PlayerRadius;
            state.Effects.ImmunityLength = Rules.CcImmunity;
            state.Elixir = Rules.ElixirStart;
            state.Stamina = Rules.StaminaMax;
            Players[id] = state;
            return state;
        }

        /// <summary>A random loadout with at least one weapon that hurts.</summary>
        public List<string> FightingLoadout()
        {
            List<string> kit = RandomLoadout();
            for (int attempt = 0; attempt < 20; attempt++)
            {
                bool hasDamage = false;
                foreach (string wid in kit)
                {
                    if (WeaponDefs.TryGetValue(wid, out WeaponDef def) && def.Damage > 0)
                    {
                        hasDamage = true;
                        break;
                    }
                }
                if (hasDamage) break;
                kit = RandomLoadout();
            }
            return kit;
        }

        /// <summary>True when `id` is driven by a bot that fights back.</summary>
        public bool IsFighter(int id) => _brains.TryGetValue(id, out DummyBrain brain) && brain.IsFighter;

        public bool SetLoadout(int id, string first, string second, string third = "")
        {
            var chosen = new List<string> { first, second };
            if (third != "") chosen.Add(third);
            return SetLoadoutList(id, chosen);
        }

        public bool SetLoadoutList(int id, IList<string> chosen)
        {
            if (!Players.ContainsKey(id) || chosen.Count < 2 || chosen.Count > PlayerInput.CardBits.Length) return false;
            for (int i = 0; i < chosen.Count; i++)
            {
                string wid = chosen[i];
                if (wid == "" || !Rules.Weapons.Exists(w => w.Id == wid)) return false;
                for (int j = i + 1; j < chosen.Count; j++)
                    if (chosen[j] == wid) return false;
            }
            PlayerState state = Players[id];
            state.Weapons = new List<string>(chosen);
            SizeSlots(state);
            return true;
        }

        void SizeSlots(PlayerState state)
        {
            state.AimHold.Clear();
            state.ResizeSlots();
        }

        /// <summary>Cards mode: shuffles every weapon into the player's deck with the match's seeded rng.</summary>
        public bool DealCards(int id)
        {
            int hand = GMath.Clampi(Rules.HandSize, 1, PlayerInput.CardBits.Length);
            if (!Players.ContainsKey(id) || WeaponDefs.Count < hand + 2) return false;
            PlayerState state = Players[id];
            var ids = new List<string>();
            foreach (WeaponDef def in Rules.Weapons) ids.Add(def.Id);
            for (int i = ids.Count - 1; i > 0; i--)
            {
                int j = Rng.RandiRange(0, i);
                string held = ids[i];
                ids[i] = ids[j];
                ids[j] = held;
            }
            state.Cards = true;
            state.Weapons = ids.GetRange(0, Math.Min(hand, ids.Count));
            state.NextCard = hand < ids.Count ? ids[hand] : "";
            state.Deck = hand + 1 < ids.Count ? ids.GetRange(hand + 1, ids.Count - hand - 1) : new List<string>();
            state.Incoming.Clear();
            state.ShuffleCard = "";
            state.ShuffleLeft = 0.0;
            state.ShuffleTotal = 0.0;
            SizeSlots(state);
            return true;
        }

        /// <summary>True when `state` pays for its cards with elixir; otherwise cards play their cooldown.</summary>
        public bool PaysElixir(PlayerState state) => state.Cards && Rules.UsesElixir();

        /// <summary>Equips a player: a dealt hand in cards mode, else `LoadoutSize` random weapons.</summary>
        public void Equip(int id, bool fighter = false)
        {
            if (Rules.CardCycle)
            {
                DealCards(id);
                return;
            }
            List<string> kit = fighter ? FightingLoadout() : RandomLoadout();
            SetLoadoutList(id, kit);
        }

        /// <summary>A card was used from `slot`: it cycles through the deck (elixir) or the shuffler (cooldowns).</summary>
        public void CycleUsed(PlayerState state, int slot)
        {
            if (!state.Cards) return;
            if (PaysElixir(state))
            {
                state.Deck.Add(state.Weapons[slot]);
                state.Weapons[slot] = state.NextCard;
                state.NextCard = PopFront(state.Deck);
                EmitCardDrawn(state.Id, slot);
                return;
            }
            state.Incoming.Add(state.Weapons[slot]);
            state.Weapons[slot] = state.NextCard;
            state.NextCard = "";
            state.WeaponCooldowns[slot] = 0.0;
            if (state.Weapons[slot] != "") EmitCardDrawn(state.Id, slot);
            TickShuffler(state, 0.0);
        }

        static string PopFront(List<string> list)
        {
            if (list.Count == 0) return "";
            string first = list[0];
            list.RemoveAt(0);
            return first;
        }

        void TickShuffler(PlayerState state, double dt)
        {
            if (!state.Cards) return;
            if (state.ShuffleCard == "" && state.Incoming.Count > 0)
            {
                state.ShuffleCard = PopFront(state.Incoming);
                state.ShuffleTotal = WeaponDefs[state.ShuffleCard].Cooldown;
                state.ShuffleLeft = state.ShuffleTotal;
            }
            if (state.ShuffleCard == "") return;
            state.ShuffleLeft -= dt;
            if (state.ShuffleLeft > 0.0) return;
            state.Deck.Add(state.ShuffleCard);
            state.ShuffleCard = "";
            state.ShuffleLeft = 0.0;
            state.ShuffleTotal = 0.0;
            PlaceNext(state);
        }

        void PlaceNext(PlayerState state)
        {
            if (state.Deck.Count == 0) return;
            for (int slot = 0; slot < state.Weapons.Count; slot++)
            {
                if (state.Weapons[slot] == "")
                {
                    state.Weapons[slot] = PopFront(state.Deck);
                    EmitCardDrawn(state.Id, slot);
                    return;
                }
            }
            if (state.NextCard == "")
            {
                state.NextCard = PopFront(state.Deck);
                EmitCardDrawn(state.Id, -1);
            }
        }

        /// <summary>A new round: the shuffler finishes at once and the hand and the window are full again.</summary>
        public void FinishReloads(PlayerState state)
        {
            for (int slot = 0; slot < state.WeaponCooldowns.Count; slot++) state.WeaponCooldowns[slot] = 0.0;
            if (!state.Cards) return;
            if (state.ShuffleCard != "") state.Deck.Add(state.ShuffleCard);
            state.Deck.AddRange(state.Incoming);
            state.Incoming.Clear();
            state.ShuffleCard = "";
            state.ShuffleLeft = 0.0;
            state.ShuffleTotal = 0.0;
            int attempts = state.Weapons.Count + 1;
            for (int attempt = 0; attempt < attempts; attempt++) PlaceNext(state);
        }

        /// <summary>Swap: only while tagged. A weapon you keep keeps its cooldown.</summary>
        public bool SwapLoadout(int id, string first, string second, string third = "")
        {
            var chosen = new List<string> { first, second };
            if (third != "") chosen.Add(third);
            return SwapLoadoutList(id, chosen);
        }

        public bool SwapLoadoutList(int id, IList<string> chosen)
        {
            if (Rules.CardCycle || !Players.ContainsKey(id) || !Players[id].Tagged) return false;
            PlayerState state = Players[id];
            var kept = new Dictionary<string, double>();
            for (int slot = 0; slot < state.Weapons.Count; slot++) kept[state.Weapons[slot]] = state.WeaponCooldowns[slot];
            if (!SetLoadoutList(id, chosen)) return false;
            for (int slot = 0; slot < state.Weapons.Count; slot++)
                state.WeaponCooldowns[slot] = kept.TryGetValue(state.Weapons[slot], out double cd) ? cd : 0.0;
            return true;
        }

        /// <summary>Distinct random weapons (`LoadoutSize` slots).</summary>
        public List<string> RandomLoadout()
        {
            var ids = new List<string>();
            foreach (WeaponDef def in Rules.Weapons) ids.Add(def.Id);
            int count = GMath.Clampi(Rules.LoadoutSize, 2, GMath.Mini(PlayerInput.CardBits.Length, ids.Count));
            for (int i = ids.Count - 1; i > 0; i--)
            {
                int j = Rng.RandiRange(0, i);
                string swap = ids[i];
                ids[i] = ids[j];
                ids[j] = swap;
            }
            return ids.GetRange(0, Math.Min(count, ids.Count));
        }

        /// <summary>Sets a player's character; a teammate who held it gets the previous one back. False for an unknown id or player.</summary>
        public bool SetCharacter(int id, string characterId)
        {
            if (!Players.ContainsKey(id) || !Rules.Characters.Exists(c => c.Id == characterId)) return false;
            string previous = Players[id].CharacterId;
            foreach (var pair in Players)
                if (pair.Key != id && pair.Value.Team == Players[id].Team && pair.Value.CharacterId == characterId) pair.Value.CharacterId = previous;
            Players[id].CharacterId = characterId;
            return true;
        }

        /// <summary>The kids a teammate of `id` has locked in (they confirmed), which `id` cannot take.</summary>
        public List<string> TeammatesKids(int id, IEnumerable<int> confirmed)
        {
            var kids = new List<string>();
            foreach (int other in confirmed)
                if (other != id && Players.ContainsKey(other) && Players.ContainsKey(id) && Players[other].Team == Players[id].Team)
                    kids.Add(Players[other].CharacterId);
            return kids;
        }

        /// <summary>Gives every player a random character, with no duplicates on the same team (seeded). Purely cosmetic.</summary>
        public void AssignCharacters()
        {
            var baseIds = new List<string>();
            foreach (CharacterDef character in Rules.Characters) baseIds.Add(character.Id);
            if (baseIds.Count == 0) return;
            var pools = new Dictionary<int, List<string>>();
            var cursors = new Dictionary<int, int>();
            foreach (var pair in Players)
            {
                int t = pair.Value.Team;
                if (!pools.ContainsKey(t))
                {
                    var pool = new List<string>(baseIds);
                    for (int i = pool.Count - 1; i > 0; i--)
                    {
                        int j = Rng.RandiRange(0, i);
                        string swap = pool[i];
                        pool[i] = pool[j];
                        pool[j] = swap;
                    }
                    pools[t] = pool;
                    cursors[t] = 0;
                }
                List<string> poolT = pools[t];
                int idx = cursors[t];
                pair.Value.CharacterId = poolT[idx % poolT.Count];
                cursors[t] = idx + 1;
            }
        }

        /// <summary>Players that can be hit (everyone not waiting on the sidewalk).</summary>
        public bool IsTargetable(PlayerState state) => !state.Tagged;

        public List<PlayerState> EnemiesOf(int team)
        {
            var result = new List<PlayerState>();
            foreach (var pair in Players)
                if (pair.Value.Team != team && IsTargetable(pair.Value)) result.Add(pair.Value);
            return result;
        }

        public PlayerState NearestEnemy(PlayerState caster, double maxRange)
        {
            PlayerState best = null;
            double bestDistance = maxRange;
            foreach (PlayerState target in EnemiesOf(caster.Team))
            {
                double distance = caster.Position.DistanceTo(target.Position);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = target;
                }
            }
            return best;
        }

        /// <summary>Status effect from a weapon.</summary>
        public void ApplyEffect(int id, StatusEffects.Type type, double duration, double magnitude)
        {
            if (!Players.ContainsKey(id) || !IsTargetable(Players[id])) return;
            PlayerState state = Players[id];
            bool fresh = type != StatusEffects.Type.None && !state.Effects.Has(type);
            bool applied = state.Effects.Apply(type, duration, magnitude);
            if (applied && state.Effects.IsHardCc(type)) EndMove(state);
            if (fresh && applied) EffectApplied?.Invoke(id, type);
        }

        /// <summary>A training dummy (or bot): a normal player on `team` at `position`, driven by `brain`. Dummies don't count for captures.</summary>
        public PlayerState AddDummy(int id, int team, Vec2 position, DummyBrain brain)
        {
            PlayerState state = AddPlayer(id, team);
            state.Position = position;
            state.SpawnPosition = position;
            state.HasStamina = false;
            state.Trips = false;
            _brains[id] = brain;
            return state;
        }

        public void SetInput(int id, PlayerInput input) => _inputs[id] = input;

        /// <summary>Applies damage (`by` is who dealt it, -1 for nobody). At 0 HP the player is tagged.</summary>
        public void Damage(int id, int amount, int by = -1)
        {
            if (!Players.ContainsKey(id) || amount <= 0) return;
            PlayerState state = Players[id];
            if (state.Tagged || Mode != null) return;
            int finalAmount = amount;
            if (by >= 0 && Players.ContainsKey(by))
            {
                PlayerState src = Players[by];
                if (src.BuffType == PowerupTeks && src.BuffLeft > 0.0)
                    finalAmount = GMath.Maxi(1, GMath.Roundi(finalAmount * (1.0 + Rules.TeksDamageBonus)));
            }
            if (state.BuffType == PowerupTuron && state.BuffLeft > 0.0)
                finalAmount = GMath.Maxi(1, GMath.Roundi(finalAmount * (1.0 - Rules.TuronDamageReduction)));
            state.Hp = GMath.Maxi(0, state.Hp - finalAmount);
            state.OutOfCombatTime = 0.0;
            state.Sprinting = false;
            state.UltCharge = GMath.Minf(100.0, state.UltCharge + finalAmount * Rules.UltPerDamage * 0.45);
            if (by >= 0 && by != id && Players.ContainsKey(by))
            {
                PlayerState attacker = Players[by];
                if (attacker.Team != state.Team)
                    attacker.UltCharge = GMath.Minf(100.0, attacker.UltCharge + finalAmount * Rules.UltPerDamage);
            }
            PlayerDamaged?.Invoke(id, finalAmount);
            if (state.Hp <= 0) Objectives.Tag(this, state, by);
        }

        /// <summary>Heals up to max HP; returns the HP actually gained. Nothing while tagged.</summary>
        public int Heal(int id, int amount)
        {
            PlayerState state = Players.GetOrDefault(id);
            if (state == null || amount <= 0 || state.Tagged) return 0;
            int gained = GMath.Mini(amount, Rules.PlayerMaxHp - state.Hp);
            if (gained <= 0) return 0;
            state.Hp += gained;
            PlayerHealed?.Invoke(id, gained);
            return gained;
        }

        /// <summary>Tags a player straight away (scripted or out-of-play).</summary>
        public void Kill(int id)
        {
            if (Players.ContainsKey(id) && !Players[id].Tagged)
            {
                Players[id].Hp = 0;
                Objectives.Tag(this, Players[id]);
            }
        }

        /// <summary>How long the opening show lasts (a street game with a roll adds the roll's time).</summary>
        public double IntroTotal() => Rules.IntroTime + (Mode != null ? Mode.Rules.RollSeconds : 0.0);

        /// <summary>Starts the opening show. Call once, before the first step.</summary>
        public void BeginIntro()
        {
            Phase = MatchPhase.Intro;
            IntroLeft = IntroTotal();
        }

        /// <summary>Everyone on `team` is gone: the match ends now and the other team wins.</summary>
        public void Forfeit(int team)
        {
            if (Phase == MatchPhase.MatchOver) return;
            Score.Winner = 1 - team;
            foreach (var pair in Players) EndMove(pair.Value);
            Phase = MatchPhase.MatchOver;
            MatchWon?.Invoke(Score.Winner);
        }

        public void Step(double dt)
        {
            if (Phase == MatchPhase.MatchOver)
            {
                Tick += 1;
                return;
            }
            if (Phase == MatchPhase.Intro)
            {
                IntroLeft -= dt;
                if (IntroLeft <= 0.0)
                {
                    IntroLeft = 0.0;
                    Phase = MatchPhase.Playing;
                    IntroFinished?.Invoke();
                }
                Tick += 1;
                return;
            }
            if (Phase == MatchPhase.PointFreeze)
            {
                FreezeLeft -= dt;
                if (FreezeLeft <= 0.0) Objectives.EndFreeze(this);
                Tick += 1;
                return;
            }
            foreach (var pair in _brains)
                if (!Players[pair.Key].Tagged) _inputs[pair.Key] = pair.Value.Think(Players[pair.Key], this);
            foreach (var pair in Players)
            {
                PlayerState state = pair.Value;
                PlayerInput input = _inputs.GetOrDefault(pair.Key);
                if (state.Tagged) StepTagged(state, input, dt);
                else StepFree(state, input, dt);
            }
            MatchTime += dt;
            RoundTime += dt;
            CaptureNeeded = CurrentBaseCaptureTime();
            Weapons.Step(this, dt);
            if (Mode != null)
            {
                Mode.Step(this, dt);
            }
            else
            {
                StepPowerups(dt);
                Objectives.CheckRescues(this, dt);
                Objectives.CheckCaptures(this, _brains, dt);
            }
            Tick += 1;
        }

        void TickCooldowns(PlayerState state, double dt)
        {
            for (int slot = 0; slot < state.WeaponCooldowns.Count; slot++)
                state.WeaponCooldowns[slot] = GMath.Maxf(0.0, state.WeaponCooldowns[slot] - dt);
            state.PrimaryCd = GMath.Maxf(0.0, state.PrimaryCd - dt);
            state.DashCd = GMath.Maxf(0.0, state.DashCd - dt);
            state.JumpCd = GMath.Maxf(0.0, state.JumpCd - dt);
            state.SlideCd = GMath.Maxf(0.0, state.SlideCd - dt);
            TickBuff(state, dt);
            if (Mode == null && !state.Tagged) state.UltCharge = GMath.Minf(100.0, state.UltCharge + Rules.UltPassivePerS * dt);
            TickShuffler(state, dt);
        }

        void StepFree(PlayerState state, PlayerInput input, double dt)
        {
            state.Effects.Step(dt);
            if (Mode != null && Mode.IsResetting()) return;
            TickCooldowns(state, dt);
            TickElixir(state, dt);
            if (Mode != null)
            {
                Mode.HandleInput(this, state, input, dt);
            }
            else if (input != null)
            {
                StepHeroActions(state, input);
                HandleWeaponCasting(state, input, dt);
            }
            Vec2 before = state.Position;
            Move(state, input, dt);
            if (Mode != null) Mode.Constrain(this, state);
            bool moved = state.Position.DistanceSquaredTo(before) > Epsilon * Epsilon;
            TickStamina(state, moved, dt);
            TickTrip(state, moved, dt);
        }

        int HeldSlot(PlayerState state)
        {
            for (int slot = 0; slot < state.AimHold.Count; slot++)
                if (state.AimHold[slot] >= 0.0) return slot;
            return -1;
        }

        /// <summary>Weapon slot aiming/release; a release that casts nothing keeps the slot's cooldown at 0 (cooldown mode).</summary>
        void HandleWeaponCasting(PlayerState state, PlayerInput input, double dt)
        {
            int releasedSlot = HeldSlot(state);
            bool wasReady = releasedSlot >= 0 && releasedSlot < state.WeaponCooldowns.Count && state.WeaponCooldowns[releasedSlot] <= 0.0;
            bool didRelease = wasReady && (state.PreviousButtons & WeaponCasting.CardBits[releasedSlot]) != 0
                && !input.IsPressed(WeaponCasting.CardBits[releasedSlot]);
            int beforeSerial = _castSerial;
            Casting.Handle(this, state, input, dt);
            if (didRelease && !Rules.CardCycle && _castSerial == beforeSerial) state.WeaponCooldowns[releasedSlot] = 0.0;
        }

        void StepHeroActions(PlayerState state, PlayerInput input)
        {
            // 0. Jump and slide
            bool jumpFresh = input.IsPressed(PlayerInput.BtnJump) && (state.PreviousButtons & PlayerInput.BtnJump) == 0;
            if (jumpFresh && state.AirY <= 0.01 && state.JumpCd <= 0.0 && state.DashLeft <= 0.0
                && state.SlideLeft <= 0.0 && state.CastLockLeft <= 0.0 && state.Effects.CanMove() && Rules.JumpVelocity > 0.0)
            {
                state.VelY = Rules.JumpVelocity;
                state.JumpCd = Rules.JumpCooldown;
                state.Crouching = false;
            }

            Vec2 stickMove = input.Move.LimitLength(1.0);
            double stickMag = stickMove.Length();
            bool hasTargetRun = stickMag <= 0.001 && state.HasMoveTarget && state.Position.DistanceSquaredTo(state.MoveTarget) > 0.04;
            bool runningFast = (stickMag >= Rules.WalkRunThreshold || hasTargetRun) && !state.Overclocked;
            bool slideFresh = input.IsPressed(PlayerInput.BtnSlide) && (state.PreviousButtons & PlayerInput.BtnSlide) == 0;
            bool crouchFresh = input.IsPressed(PlayerInput.BtnCrouch) && (state.PreviousButtons & PlayerInput.BtnCrouch) == 0;
            if ((slideFresh || (crouchFresh && !state.Crouching)) && runningFast && state.AirY <= 0.01
                && state.SlideCd <= 0.0 && state.SlideLeft <= 0.0 && state.DashLeft <= 0.0
                && state.CastLockLeft <= 0.0 && state.Effects.CanMove() && Rules.SlideDuration > 0.0)
            {
                Vec2 sDir;
                if (stickMag > 0.001) sDir = stickMove.Normalized();
                else if (hasTargetRun) sDir = (state.MoveTarget - state.Position).Normalized();
                else sDir = state.Facing.LengthSquared() > 0.001 ? state.Facing : new Vec2(0.0, -SideForTeam(state.Team));
                state.SlideDir = sDir;
                state.Facing = sDir;
                state.SlideLeft = Rules.SlideDuration;
                state.SlideCd = Rules.SlideCooldown;
                state.Crouching = true;
                state.Walking = false;
                state.RunTime = 0.0;
            }
            else if (crouchFresh && !runningFast && state.SlideLeft > 0.0)
            {
                state.SlideLeft = 0.0;
            }

            // 1. Dash (fresh press)
            bool dashFresh = input.IsPressed(PlayerInput.BtnDash) && (state.PreviousButtons & PlayerInput.BtnDash) == 0;
            if (dashFresh && state.DashCd <= 0.0 && state.DashLeft <= 0.0 && state.Effects.CanMove() && Rules.DashDuration > 0.0)
            {
                Vec2 dir;
                if (input.Move.LengthSquared() > 0.001) dir = input.Move.Normalized();
                else if (state.HasMoveTarget && state.Position.DistanceSquaredTo(state.MoveTarget) > 0.04) dir = (state.MoveTarget - state.Position).Normalized();
                else if (input.IsPressed(PlayerInput.BtnAimPoint) && input.AimPoint.IsFinite() && state.Position.DistanceSquaredTo(input.AimPoint) > 0.04)
                    dir = (input.AimPoint - state.Position).Normalized();
                else dir = state.Facing.LengthSquared() > 0.001 ? state.Facing : new Vec2(0.0, -SideForTeam(state.Team));
                state.DashDir = dir;
                state.Facing = dir;
                state.DashLeft = Rules.DashDuration;
                state.DashCd = Rules.DashCooldown;
                state.SlideLeft = 0.0;
                state.Crouching = false;
                state.Effects.ClearType(StatusEffects.Type.Slow);
                PlayerDashed?.Invoke(state.Id);
            }

            // 2. Ultimate (fresh press at 100% charge)
            bool ultFresh = input.IsPressed(PlayerInput.BtnUltimate) && (state.PreviousButtons & PlayerInput.BtnUltimate) == 0;
            if (ultFresh && Rules.UltimateWeapon != null && state.UltCharge >= 100.0 - Epsilon && state.CanAct() && !CastingBlocked(state))
            {
                WeaponDef ultDef = Rules.UltimateWeapon;
                Vec2 ultAim = Vec2.Zero;
                if (input.IsPressed(PlayerInput.BtnAimPoint) && input.AimPoint.IsFinite())
                {
                    ultAim = AimFromPoint(state, ultDef, input.AimPoint);
                }
                else if (input.Aim.IsFinite() && input.Aim.Length() >= Rules.AimDeadzone)
                {
                    ultAim = input.Aim.LimitLength(1.0);
                }
                else
                {
                    PlayerState near = NearestEnemy(state, ultDef.MaxRange);
                    if (near != null && ultDef.MaxRange > 0.0) ultAim = ((near.Position - state.Position) / ultDef.MaxRange).LimitLength(1.0);
                }
                if (Weapons.Fire(this, state, ultDef, ultAim, 0.0, -1))
                {
                    state.UltCharge = 0.0;
                    state.OutOfCombatTime = 0.0;
                    state.Sprinting = false;
                    if (ultDef.Heal > 0) Heal(state.Id, ultDef.Heal);
                    UltimateCast?.Invoke(state.Id);
                    EmitWeaponCast(state.Id, ultDef.Id);
                }
            }

            // 3. Primary fire (hold or tap, when no card slot is being aimed)
            if (input.IsPressed(PlayerInput.BtnPrimary) && Rules.PrimaryWeapon != null && state.PrimaryCd <= 0.0
                && state.CanAct() && !CastingBlocked(state) && HeldSlot(state) < 0)
            {
                WeaponDef pDef = Rules.PrimaryWeapon;
                Vec2 pAim;
                if (input.IsPressed(PlayerInput.BtnAimPoint) && input.AimPoint.IsFinite()) pAim = AimFromPoint(state, pDef, input.AimPoint);
                else if (input.Aim.IsFinite() && input.Aim.Length() >= Rules.AimDeadzone) pAim = input.Aim.LimitLength(1.0);
                else pAim = Weapons.AutoAim(this, state, pDef);
                if (Weapons.Fire(this, state, pDef, pAim, 0.0, -1))
                {
                    if (pAim.LengthSquared() > 0.001) state.Facing = pAim.Normalized();
                    state.PrimaryCd = pDef.Cooldown;
                    state.OutOfCombatTime = 0.0;
                    state.Sprinting = false;
                    EmitWeaponCast(state.Id, pDef.Id);
                }
            }
        }

        /// <summary>Each `TripInterval` s of running without stopping is one roll at `TripChance` (the trip rng).</summary>
        void TickTrip(PlayerState state, bool moved, double dt)
        {
            if (!state.Trips || Rules.TripChance <= 0.0 || Rules.TripInterval <= 0.0) return;
            if (!moved || state.Walking || state.Crouching || state.SlideLeft > 0.0 || state.DashLeft > 0.0
                || state.Overclocked || !state.Effects.CanMove())
            {
                state.RunTime = 0.0;
                return;
            }
            if (state.AirY > 0.0) return;
            state.RunTime += dt;
            if (state.RunTime < Rules.TripInterval - Epsilon) return;
            state.RunTime = 0.0;
            if (_tripRng.Randf() < Rules.TripChance) ApplyEffect(state.Id, StatusEffects.Type.Tripped, Rules.TripSeconds, 0.0);
        }

        /// <summary>Spawns or resets the merienda power-up stands (circular map, no street mode).</summary>
        public void ResetPowerups()
        {
            Powerups.Clear();
            if (Layout == null || !Layout.IsCircular() || Mode != null) return;
            var spots = new List<Vec2>(Layout.PowerupSpots);
            if (spots.Count == 0) spots.AddRange(DefaultPowerupSpots);
            for (int i = 0; i < spots.Count; i++)
                Powerups.Add(new Powerup { Id = i + 1, Kind = (i % 3) + 1, Pos = spots[i], Active = true, RespawnLeft = 0.0 });
        }

        void StepPowerups(double dt)
        {
            if (Powerups.Count == 0 || Mode != null) return;
            foreach (Powerup entry in Powerups)
            {
                if (!entry.Active)
                {
                    double rem = GMath.Maxf(0.0, entry.RespawnLeft - dt);
                    entry.RespawnLeft = rem;
                    if (rem <= Epsilon)
                    {
                        entry.RespawnLeft = 0.0;
                        entry.Active = true;
                    }
                    continue;
                }
                PlayerState winner = null;
                double bestDist = double.PositiveInfinity;
                foreach (var pair in Players)
                {
                    if (_brains.ContainsKey(pair.Key)) continue;
                    PlayerState candidate = pair.Value;
                    if (candidate.Tagged) continue;
                    double reach = Rules.PowerupPickupRadius + candidate.Radius;
                    double dist = candidate.Position.DistanceTo(entry.Pos);
                    if (dist <= reach && dist < bestDist)
                    {
                        bestDist = dist;
                        winner = candidate;
                    }
                }
                if (winner == null) continue;
                entry.Active = false;
                entry.RespawnLeft = Rules.PowerupRespawnSeconds;
                ApplyPowerup(winner, entry.Kind);
                PowerupTaken?.Invoke(winner.Id, entry.Id, entry.Kind);
            }
        }

        void ApplyPowerup(PlayerState state, int kind)
        {
            state.BuffRegenCarry = 0.0;
            switch (kind)
            {
                case PowerupPalamig:
                    Heal(state.Id, Rules.PalamigHeal);
                    state.BuffType = PowerupPalamig;
                    state.BuffLeft = Rules.PalamigDuration;
                    break;
                case PowerupTeks:
                    if (Mode == null) state.UltCharge = GMath.Minf(100.0, state.UltCharge + Rules.TeksUltBonus);
                    state.BuffType = PowerupTeks;
                    state.BuffLeft = Rules.TeksDuration;
                    break;
                case PowerupTuron:
                    state.BuffType = PowerupTuron;
                    state.BuffLeft = Rules.TuronDuration;
                    break;
            }
        }

        void TickBuff(PlayerState state, double dt)
        {
            if (state.BuffLeft <= 0.0 || state.BuffType == PowerupNone)
            {
                state.BuffLeft = 0.0;
                state.BuffType = PowerupNone;
                state.BuffRegenCarry = 0.0;
                return;
            }
            double stepDt = GMath.Minf(dt, state.BuffLeft);
            state.BuffLeft = GMath.Maxf(0.0, state.BuffLeft - dt);
            if (!state.Tagged && state.BuffType == PowerupTuron && Rules.TuronRegenPerS > 0.0)
            {
                if (state.Hp < Rules.PlayerMaxHp)
                {
                    state.BuffRegenCarry += Rules.TuronRegenPerS * stepDt;
                    int wholeHp = (int)Math.Floor(state.BuffRegenCarry + Epsilon);
                    if (wholeHp > 0)
                    {
                        state.BuffRegenCarry = GMath.Maxf(0.0, state.BuffRegenCarry - wholeHp);
                        Heal(state.Id, wholeHp);
                    }
                }
                else
                {
                    state.BuffRegenCarry = 0.0;
                }
            }
            if (state.BuffLeft <= Epsilon)
            {
                state.BuffLeft = 0.0;
                state.BuffType = PowerupNone;
                state.BuffRegenCarry = 0.0;
            }
        }

        void StepAir(PlayerState state, double dt)
        {
            if (state.AirY > 0.0 || state.VelY > 0.0)
            {
                state.AirY += state.VelY * dt;
                state.VelY -= Rules.JumpGravity * dt;
                if (state.AirY <= 0.0)
                {
                    state.AirY = 0.0;
                    state.VelY = 0.0;
                }
            }
        }

        /// <summary>Elixir fills while the match runs (also while tagged), up to the bar.</summary>
        void TickElixir(PlayerState state, double dt)
        {
            if (PaysElixir(state)) state.Elixir = GMath.Minf(Rules.ElixirMax, state.Elixir + Rules.ElixirRegenPerS * dt);
        }

        /// <summary>Running drains stamina; standing still refills it after `StaminaRegenDelay`. At 0 the kid is overclocked.</summary>
        void TickStamina(PlayerState state, bool moved, double dt)
        {
            if (!Rules.StaminaEnabled || !state.HasStamina) return;
            if (state.Overclocked)
            {
                state.StillTime = 0.0;
                if (state.ExhaustLeft > 0.0)
                {
                    state.ExhaustLeft = state.ExhaustLeft - dt > Epsilon ? state.ExhaustLeft - dt : 0.0;
                    return;
                }
                if (Mode != null && !Mode.CanRest(state)) return;
                state.Stamina = GMath.Minf(Rules.StaminaMax, state.Stamina + Rules.StaminaRegenPerS * dt);
                if (state.Stamina >= Rules.StaminaMax - Epsilon)
                {
                    state.Stamina = Rules.StaminaMax;
                    state.Overclocked = false;
                }
                return;
            }
            if (moved)
            {
                state.StillTime = 0.0;
                state.Stamina = GMath.Maxf(0.0, state.Stamina - Rules.StaminaDrainPerS * dt);
                if (state.Stamina <= Epsilon)
                {
                    state.Stamina = 0.0;
                    state.Overclocked = true;
                    state.ExhaustLeft = Rules.ExhaustSeconds;
                    Exhausted?.Invoke(state.Id);
                }
                return;
            }
            if (Mode != null && !Mode.CanRest(state))
            {
                state.StillTime = 0.0;
                return;
            }
            double refill = GMath.Clampf(state.StillTime + dt - Rules.StaminaRegenDelay, 0.0, dt);
            state.StillTime += dt;
            state.Stamina = GMath.Minf(Rules.StaminaMax, state.Stamina + Rules.StaminaRegenPerS * refill);
        }

        /// <summary>Full stamina, not overclocked.</summary>
        public void RefillStamina(PlayerState state)
        {
            state.Stamina = Rules.StaminaMax;
            state.Overclocked = false;
            state.ExhaustLeft = 0.0;
            state.StillTime = 0.0;
        }

        /// <summary>Waiting on the sidewalk: no moving or skills, cooldowns keep running. Training dummies walk out on their own.</summary>
        void StepTagged(PlayerState state, PlayerInput input, double dt)
        {
            TickCooldowns(state, dt);
            TickElixir(state, dt);
            TickStamina(state, false, dt);
            state.TagTime += dt;
            if (input != null) state.PreviousButtons = input.Buttons;
            if (_brains.ContainsKey(state.Id) && state.TagTime >= Rules.DummyReleaseTime) Objectives.Rescue(this, state, ReleasedAlone);
        }

        /// <summary>The street point a move target really goes to: pulled into the street and out of cover (the server's own clamp).</summary>
        public Vec2 ClampMoveTarget(Vec2 point, double radius)
        {
            Rect2 street = _street.Grow(-radius);
            Vec2 streetEnd = street.End;
            Vec2 result = Layout.ClampToArena(point, radius);
            foreach (Rect2 rect in _walkObstacles)
            {
                Rect2 grown = rect.Grow(radius);
                if (!grown.HasPoint(result)) continue;
                Vec2 end = grown.End;
                Vec2[] exits =
                {
                    new Vec2(grown.Position.x, result.y), new Vec2(end.x, result.y),
                    new Vec2(result.x, grown.Position.y), new Vec2(result.x, end.y),
                };
                Vec2 best = result;
                double bestDistance = double.PositiveInfinity;
                foreach (Vec2 exit in exits)
                {
                    bool inside = Layout.ArenaRadius > 0.0
                        ? Layout.InArena(exit, GMath.Maxf(radius - Epsilon, 0.0))
                        : exit.x >= street.Position.x - Epsilon && exit.x <= streetEnd.x + Epsilon
                          && exit.y >= street.Position.y - Epsilon && exit.y <= streetEnd.y + Epsilon;
                    if (inside && exit.DistanceTo(result) < bestDistance)
                    {
                        bestDistance = exit.DistanceTo(result);
                        best = exit;
                    }
                }
                result = best;
            }
            return result;
        }

        /// <summary>True when the card in `slot` can be paid for now (always true when cards play cooldowns instead).</summary>
        public bool CanAfford(PlayerState state, int slot)
        {
            if (!PaysElixir(state)) return true;
            if (slot < 0 || slot >= state.Weapons.Count) return false;
            WeaponDefs.TryGetValue(state.Weapons[slot], out WeaponDef def);
            return def != null && state.Elixir + Epsilon >= def.ElixirCost;
        }

        /// <summary>Ends the kid's walk to its move target.</summary>
        public void EndMove(PlayerState state)
        {
            state.HasMoveTarget = false;
            state.StuckTime = 0.0;
        }

        /// <summary>A card aimed at street `point`, as the stick-style aim WeaponSystem.Fire reads.</summary>
        public Vec2 AimFromPoint(PlayerState state, WeaponDef def, Vec2 point)
        {
            Vec2 offset = point - state.Position;
            switch (def.Shape)
            {
                case WeaponDef.WeaponShape.Heal:
                    return offset.Length() <= Rules.SelfCastRadius ? Vec2.Zero : offset.Normalized();
                case WeaponDef.WeaponShape.GroundAoe:
                case WeaponDef.WeaponShape.Trap:
                case WeaponDef.WeaponShape.Field:
                    return def.MaxRange > 0.0 ? (offset / def.MaxRange).LimitLength(1.0) : Vec2.Zero;
            }
            if (offset.Length() <= Rules.AimNoDirectionRadius) return state.Facing;
            return offset.Normalized();
        }

        /// <summary>The targetable enemy nearest street `point` among those within `def.MaxRange` of the kid (-1: none).</summary>
        public int TargetNearPoint(PlayerState state, WeaponDef def, Vec2 point)
        {
            int best = -1;
            double bestDistance = double.PositiveInfinity;
            foreach (PlayerState enemy in EnemiesOf(state.Team))
            {
                if (state.Position.DistanceTo(enemy.Position) > def.MaxRange) continue;
                double distance = enemy.Position.DistanceTo(point);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = enemy.Id;
                }
            }
            return best;
        }

        public bool WeaponReady(PlayerState state, int slot)
        {
            if (Mode != null) return false;
            return Casting.WeaponReady(state, slot) && CanAfford(state, slot) && !CastingBlocked(state);
        }

        /// <summary>Where a free player cannot cast: in the enemy base, or in its own rescue area while a teammate waits.</summary>
        public bool CastingBlocked(PlayerState state) => !state.Tagged && CastingBlockedAt(state, state.Position);

        public bool CastingBlockedAt(PlayerState state, Vec2 position)
        {
            if (Mode != null) return false;
            if (Rules.NoCastInEnemyBase && Layout.InBase(-SideForTeam(state.Team), position)) return true;
            return Rules.NoCastInRescueArea && Objectives.TeammateWaiting(this, state.Team)
                && Layout.RescueRect(SideForTeam(state.Team)).Grow(state.Radius).HasPoint(position);
        }

        public Vec2 ResolvedAim(PlayerState state, int slot, Vec2 stick) => Casting.ResolvedAim(this, state, slot, stick);

        public int ResolvedTarget(PlayerState state, int slot) => Casting.ResolvedTarget(this, state, slot);

        /// <summary>True when releasing `slot` now would throw nothing (a TARGETED weapon with nobody in range).</summary>
        public bool LacksTarget(PlayerState state, int slot)
        {
            if (slot < 0 || slot >= state.Weapons.Count) return false;
            WeaponDefs.TryGetValue(state.Weapons[slot], out WeaponDef def);
            if (def == null || def.Shape != WeaponDef.WeaponShape.Targeted) return false;
            return Weapons.TargetInRange(this, state, def, ResolvedTarget(state, slot)) == null;
        }

        void Move(PlayerState state, PlayerInput input, double dt)
        {
            if (input != null) TakeMoveOrder(state, input);
            StepAir(state, dt);
            if (state.DashLeft > 0.0)
            {
                state.DashLeft = GMath.Maxf(0.0, state.DashLeft - dt);
                state.Walking = false;
                state.Crouching = false;
                if (state.Effects.CanMove())
                {
                    MoveBy(state, state.DashDir * Rules.DashSpeed * dt);
                    return;
                }
                state.DashLeft = 0.0;
            }
            if (state.SlideLeft > 0.0)
            {
                state.SlideLeft = GMath.Maxf(0.0, state.SlideLeft - dt);
                state.Walking = false;
                state.Sprinting = false;
                if (state.Effects.CanMove() && state.AirY <= 0.01)
                {
                    state.Crouching = state.SlideLeft > 0.0 || (input != null && input.IsPressed(PlayerInput.BtnCrouch));
                    double slideV = Rules.SlideSpeed;
                    if (Mode == null && state.BuffType == PowerupPalamig && state.BuffLeft > 0.0) slideV *= 1.0 + Rules.PalamigSpeedBonus;
                    MoveBy(state, state.SlideDir * slideV * dt);
                    return;
                }
                state.SlideLeft = 0.0;
            }
            if (state.CastLockLeft > 0.0)
            {
                state.CastLockLeft = GMath.Maxf(0.0, state.CastLockLeft - dt);
                state.Sprinting = false;
                state.Walking = false;
                state.Crouching = false;
                return;
            }
            if (!state.Effects.CanMove())
            {
                state.Sprinting = false;
                state.Walking = false;
                state.Crouching = false;
                return;
            }
            state.OutOfCombatTime += dt;
            Vec2 move = input != null ? input.Move.LimitLength(1.0) : Vec2.Zero;
            double stickLen = move.Length();
            state.Crouching = Mode == null && state.AirY <= 0.01 && input != null && input.IsPressed(PlayerInput.BtnCrouch);
            state.Walking = !state.Crouching && stickLen > 0.0 && stickLen < Rules.WalkRunThreshold;
            double speed = Rules.MoveSpeed;
            if (Mode != null) speed *= Mode.SpeedScale(state);
            if (state.Overclocked) speed *= Rules.OverclockSpeedShare;
            else if (state.Crouching) speed *= Rules.CrouchSpeedShare;
            if (Mode == null && state.BuffType == PowerupPalamig && state.BuffLeft > 0.0) speed *= 1.0 + Rules.PalamigSpeedBonus;
            speed *= state.Effects.SpeedMultiplier(Rules.PolymorphSpeedScale);
            bool wantsMove = stickLen > 0.0 || state.HasMoveTarget;
            state.Sprinting = wantsMove && !state.Walking && !state.Crouching && Rules.SprintEnabled
                && Mode == null && !state.Overclocked && !state.Effects.Has(StatusEffects.Type.Slow)
                && state.OutOfCombatTime >= Rules.SprintDelay;
            if (state.Sprinting) speed *= Rules.SprintMultiplier;
            Vec2 prevPos = state.Position;
            if (stickLen > 0.0)
            {
                state.Facing = move / stickLen;
                MoveBy(state, move * speed * dt);
            }
            else if (state.HasMoveTarget)
            {
                WalkToTarget(state, speed * dt, dt);
            }
            if (state.Walking && state.Position.DistanceSquaredTo(prevPos) <= Epsilon * Epsilon) state.Walking = false;
        }

        /// <summary>A non-zero stick clears the move target; a new street point replaces it (pulled into the street and out of cover).</summary>
        void TakeMoveOrder(PlayerState state, PlayerInput input)
        {
            if (input.Move.LengthSquared() > 0.0)
            {
                EndMove(state);
                return;
            }
            if (!input.IsPressed(PlayerInput.BtnMoveTarget) || !input.MoveTarget.IsFinite()) return;
            Vec2 point = ClampMoveTarget(Mode == null ? input.MoveTarget : Mode.MovePoint(state, input.MoveTarget), state.Radius);
            if (state.HasMoveTarget && point == state.MoveTarget) return;
            state.MoveTarget = point;
            state.HasMoveTarget = true;
            state.StuckTime = 0.0;
        }

        /// <summary>One tick of walking toward the move target in a straight line (sliding along walls).</summary>
        void WalkToTarget(PlayerState state, double reach, double dt)
        {
            Vec2 offset = state.MoveTarget - state.Position;
            double distance = offset.Length();
            if (distance <= Rules.MoveArrivalRadius)
            {
                EndMove(state);
                return;
            }
            Vec2 direction = offset / distance;
            state.Facing = direction;
            double intended = GMath.Minf(reach, distance);
            Vec2 before = state.Position;
            MoveBy(state, direction * intended);
            if (state.Position.DistanceTo(state.MoveTarget) <= Rules.MoveArrivalRadius)
            {
                EndMove(state);
                return;
            }
            if ((state.Position - before).Dot(direction) < Rules.MoveStuckShare * intended)
            {
                state.StuckTime += dt;
                if (state.StuckTime + Epsilon >= Rules.MoveStuckTime) EndMove(state);
            }
            else
            {
                state.StuckTime = 0.0;
            }
        }

        /// <summary>Moves by `delta` in sub-steps no longer than the player's radius so fast moves never tunnel through an obstacle.</summary>
        public void MoveBy(PlayerState state, Vec2 delta)
        {
            double length = delta.Length();
            if (length <= 0.0) return;
            int count = GMath.Maxi(1, GMath.Ceili(length / state.Radius));
            Vec2 piece = delta / count;
            for (int i = 0; i < count; i++)
                state.Position = Layout.ClampToArena(Collision.Resolve(state.Position + piece, state.Radius, _walkBlocking), state.Radius);
        }

        /// <summary>Slot 0 next to the post, then alternating +x / -x by spawn_spacing.</summary>
        Vec2 SpawnPosition(int side, int slot)
        {
            Vec2 basePos = Layout.SpawnCenter(side);
            int offset = GMath.Ceili(slot / 2.0);
            int direction = slot % 2 == 1 ? 1 : -1;
            return basePos + new Vec2(direction * offset * Rules.SpawnSpacing, 0.0);
        }

        /// <summary>What stops `team`'s players and throws: the street edges and every obstacle.</summary>
        public IReadOnlyList<Rect2> BlockingRectsForTeam(int team) => _blocking;

        /// <summary>Index of the obstacle at `point`, or -1.</summary>
        public int ObstacleAtPoint(Vec2 point)
        {
            for (int index = 0; index < Obstacles.Count; index++)
                if (Obstacles[index].HasPoint(point)) return index;
            return -1;
        }

        public bool TeamInEnemyBase(int team) => Objectives.TeamInEnemyBase(this, team, _brains);

        public int CapturersInEnemyBase(int team) => Objectives.CapturersInEnemyBase(this, team, _brains);

        public bool AllEnemiesTagged(int team) => Objectives.AllEnemiesTagged(this, team, _brains);

        /// <summary>Solo base hold time, shortened only by configured overtime.</summary>
        public double CurrentBaseCaptureTime()
        {
            if (InOvertime()) return GMath.Minf(Rules.CaptureSoloTime, Rules.OvertimeCaptureTime);
            return Rules.CaptureSoloTime;
        }

        /// <summary>The stage of the round clock, from the round's time only (GDScript `clock_stage()`).</summary>
        public ClockStage GetClockStage()
        {
            if (Mode != null) return ClockStage.Regular;
            if (Rules.OvertimeAfter <= 0.0 || RoundTime < Rules.OvertimeAfter) return ClockStage.Regular;
            if (RoundTime < Rules.OvertimeAfter + Rules.ExtendedSeconds) return ClockStage.Extended;
            return ClockStage.Overtime;
        }

        /// <summary>The round time at which `stage` begins.</summary>
        public double ClockStageStart(ClockStage stage)
        {
            switch (stage)
            {
                case ClockStage.Extended: return Rules.OvertimeAfter;
                case ClockStage.Overtime: return Rules.OvertimeAfter + Rules.ExtendedSeconds;
            }
            return 0.0;
        }

        /// <summary>Seconds left in the current stage (the header's clock).</summary>
        public double ClockLeft()
        {
            if (Mode != null) return Mode.TimeLeft;
            double end = Rules.OvertimeAfter;
            switch (GetClockStage())
            {
                case ClockStage.Extended: end += Rules.ExtendedSeconds; break;
                case ClockStage.Overtime: end += Rules.ExtendedSeconds + Rules.OvertimeSeconds; break;
            }
            return GMath.Maxf(end - RoundTime, 0.0);
        }

        /// <summary>True when the header's clock counts down.</summary>
        public bool CountsDown() => Mode != null || Rules.OvertimeAfter > 0.0;

        public bool InOvertime() => GetClockStage() == ClockStage.Overtime;

        /// <summary>Seconds left on the running capture countdown (0 when none runs).</summary>
        public double CaptureTimeLeft() => Objectives.CaptureTimeLeft(this);
    }
}
