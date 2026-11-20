using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Kalyeah.Net;
using Kalyeah.Sim;

namespace Kalyeah.Game
{
    /// <summary>High-level mode selected by the title screen, lobby, or practice launcher.</summary>
    public enum GameMode
    {
        Practice = 0,
        Training = 1,
        Online = 2,
        HotspotHost = 3,
        HotspotClient = 4,
        Patintero = 5,
        TumbangPreso = 6,
        LangitLupa = 7,
    }

    public enum LobbyIntent
    {
        Create = 0,
        Join = 1,
    }

    public struct RankTierInfo
    {
        public string Id;
        public string Name;
        public int MinLp;
        public int MaxLp;
        public string ColorHex;
        public string AccentHex;
        public string DarkHex;
        public string InkHex;
        public string Flavor;
    }

    public struct RankOutcomeInfo
    {
        public int OldLp;
        public int NewLp;
        public int Delta;
        public string TierId;
        public string TierName;
        public bool Promoted;
    }

    public struct MatchHistoryEntry
    {
        public long TimestampUnixSeconds;
        public string Mode;
        public string CharacterId;
        public bool Won;
        public int MyScore;
        public int TheirScore;
        public int LpDelta;
    }

    /// <summary>
    /// Engine-free session, ranked progression, match history, and live network/hotspot lifecycle state
    /// (port of scripts/autoload/session.gd + scripts/autoload/net.gd + RankedSystem integration).
    /// </summary>
    public sealed class AppSession
    {
        public const string PlayerPrefsKey = "kalyeah.session.v1";
        public const string DefaultPlayerName = "GABO";
        /// <summary>Karding is the home kid until the player picks another (owner decision).</summary>
        public const string DefaultCharacter = "karding";
        public const string CustomIdPhotoFileName = "id_photo.png";
        public const int MaxNameLength = 12;
        public const int MaxHistoryEntries = 50;
        const double CloseGraceSeconds = 0.5;

        /// <summary>The 12 kids in role order (docs/SPEC.md §7): Strikers, Defenders, Seekers, Controllers.</summary>
        public static readonly string[] DefaultRoster =
        {
            "narda", "jumong", "inggo",
            "jopay", "oriang", "paeng",
            "kikay", "karding", "manoy",
            "tere", "pamela", "domeng",
        };

        /// <summary>The 6 nametag avatar photo tiles synced under <c>ui/avatars/*</c> (in <c>Session.AVATARS</c> order).</summary>
        public static readonly string[] AvatarPhotoIds =
        {
            "narda", "jumong", "inggo", "jopay", "oriang", "paeng",
        };

        public static readonly string[] DefaultSuperpowers =
        {
            "bato_light", "tsinelas_light", "papel_shield",
        };

        public static readonly RankTierInfo[] RankTiers =
        {
            new RankTierInfo { Id = "kahoy", Name = "Kahoy", MinLp = 0, MaxLp = 199, ColorHex = "c8874b", AccentHex = "f2be86", DarkHex = "5c3416", InkHex = "241207", Flavor = "Warm Carved Wood" },
            new RankTierInfo { Id = "bakal", Name = "Bakal", MinLp = 200, MaxLp = 399, ColorHex = "8a99b0", AccentHex = "d0dced", DarkHex = "364052", InkHex = "141923", Flavor = "Forged Dark Iron" },
            new RankTierInfo { Id = "tanso", Name = "Tanso", MinLp = 400, MaxLp = 599, ColorHex = "dc7d3e", AccentHex = "ffba80", DarkHex = "6b3110", InkHex = "281004", Flavor = "Burnished Bronze" },
            new RankTierInfo { Id = "pilak", Name = "Pilak", MinLp = 600, MaxLp = 849, ColorHex = "d6e5f7", AccentHex = "ffffff", DarkHex = "536987", InkHex = "182233", Flavor = "Gleaming Silver" },
            new RankTierInfo { Id = "ginto", Name = "Ginto", MinLp = 850, MaxLp = 1149, ColorHex = "ffc938", AccentHex = "fff29c", DarkHex = "8c5400", InkHex = "2e1900", Flavor = "Radiant Barrio Gold" },
            new RankTierInfo { Id = "brilyante", Name = "Brilyante", MinLp = 1150, MaxLp = 1499, ColorHex = "4ce4ff", AccentHex = "d9fcff", DarkHex = "0d5e8a", InkHex = "062338", Flavor = "Prism Cyan Diamond" },
            new RankTierInfo { Id = "batang_kalyeah", Name = "Batang Kalyeah", MinLp = 1500, MaxLp = -1, ColorHex = "ff6b2b", AccentHex = "ffe14d", DarkHex = "7d1818", InkHex = "24070c", Flavor = "Mythic Komiks Street Crown" },
        };

        static readonly Regex DisallowedNameChars = new Regex(@"[^\p{L}\p{N} '._-]", RegexOptions.Compiled);
        static readonly Regex MultiSpaces = new Regex(@" {2,}", RegexOptions.Compiled);

        static readonly List<WeaponDef> DefaultWeaponDefs = BuildDefaultWeaponDefs();
        static readonly Dictionary<string, WeaponDef> DefaultWeaponDefMap = BuildWeaponDefMap(DefaultWeaponDefs);

        public static AppSession Current { get; } = new AppSession();
        public static AppSession Instance => Current;

        public event Action Changed;
        public event Action<bool> ConnectionChanged;

        string _playerName = DefaultPlayerName;
        string _selectedCharacter = DefaultCharacter;
        int _avatarIndex;
        bool _hasCustomIdPhoto;
        string _defaultServerUrl = "";
        readonly List<string> _selectedSuperpowers = new List<string>(DefaultSuperpowers);
        readonly List<MatchHistoryEntry> _history = new List<MatchHistoryEntry>();

        readonly RankedSystem.Record _rankRecord = new RankedSystem.Record();
        SimData _sharedSimData;
        Dictionary<string, WeaponDef> _sharedWeaponDefMap;
        GameClient _client;
        GameServer _hostServer;
        GameServer _closingServer;
        double _closingLeft;
        HotspotBeacon _beacon;
        HotspotFinder _finder;

        public GameMode Mode { get; set; } = GameMode.Practice;
        public LobbyIntent Intent { get; set; } = LobbyIntent.Create;
        public string BotDifficulty { get; set; } = "medium";
        public int FirstTo { get; set; } = 3;
        public bool BriefingPending { get; set; }

        public string RoomCode { get; set; } = "";
        public int LocalPeerId { get; set; } = SimBridge.LocalId;
        public string ReconnectToken { get; set; } = "";
        public string ReconnectUrl { get; set; } = "";
        public string ServerUrl { get; set; } = "";
        public string HostEndpoint { get; set; } = "";

        /// <summary>CI-stamped online WebSocket URL loaded from <c>Data/build_stamp</c>.</summary>
        public string DefaultServerUrl => _defaultServerUrl;

        public string EffectiveServerUrl =>
            SettingsStore.NormalizeServerUrl(
                !string.IsNullOrWhiteSpace(ServerUrl)
                    ? ServerUrl
                    : (!string.IsNullOrWhiteSpace(_defaultServerUrl)
                        ? _defaultServerUrl
                        : SettingsStore.Current.EffectiveServerUrl));

        public string PlayerName
        {
            get => _playerName;
            set
            {
                string cleaned = CleanName(value);
                _playerName = string.IsNullOrEmpty(cleaned) ? DefaultPlayerName : cleaned;
                Changed?.Invoke();
            }
        }

        public string SelectedCharacter
        {
            get => _selectedCharacter;
            set
            {
                string id = string.IsNullOrWhiteSpace(value) ? DefaultCharacter : value.Trim();
                if (!IsKnownAvatar(id)) id = DefaultCharacter;
                _selectedCharacter = id;
                Changed?.Invoke();
            }
        }

        /// <summary>Selected nametag avatar photo index (0..5 across <see cref="AvatarPhotoIds"/>).</summary>
        public int AvatarIndex
        {
            get => _avatarIndex;
            set
            {
                int next = Math.Clamp(value, 0, AvatarPhotoIds.Length - 1);
                if (_avatarIndex == next) return;
                _avatarIndex = next;
                Changed?.Invoke();
            }
        }

        public string AvatarPhotoId => AvatarPhotoIds[Math.Clamp(_avatarIndex, 0, AvatarPhotoIds.Length - 1)];

        /// <summary>True when a local-only selfie (<see cref="CustomIdPhotoFileName"/>) is kept on this device.</summary>
        public bool HasCustomIdPhoto
        {
            get => _hasCustomIdPhoto;
            set
            {
                if (_hasCustomIdPhoto == value) return;
                _hasCustomIdPhoto = value;
                Changed?.Invoke();
            }
        }

        public IReadOnlyList<string> SelectedSuperpowers => _selectedSuperpowers;
        public IReadOnlyList<MatchHistoryEntry> History => _history;

        public bool IsStreetMiniGame =>
            Mode == GameMode.Patintero || Mode == GameMode.TumbangPreso || Mode == GameMode.LangitLupa;

        public bool IsNetworkedMode =>
            Mode == GameMode.Online || Mode == GameMode.HotspotHost || Mode == GameMode.HotspotClient;

        public StreetGameKind SelectedStreetGameKind
        {
            get
            {
                switch (Mode)
                {
                    case GameMode.TumbangPreso: return StreetGameKind.TumbangPreso;
                    case GameMode.LangitLupa: return StreetGameKind.LangitLupa;
                    default: return StreetGameKind.Patintero;
                }
            }
        }

        /// <summary>
        /// True when the active match is one of the 3 Larong Kalye modes — either offline (<see cref="IsStreetMiniGame"/>)
        /// or in an Online / Hotspot room whose selected map is "patintero", "tumbang_preso", or "langit_lupa".
        /// </summary>
        public bool TryGetActiveStreetKind(out StreetGameKind kind)
        {
            if (IsStreetMiniGame)
            {
                kind = SelectedStreetGameKind;
                return true;
            }

            if (IsNetworkedMode && _client != null)
            {
                string roomMap = _client.Room != null ? _client.Room.GetStr("map") : "";
                StreetGameKind? fromRoom = StreetBridge.KindFromModeId(roomMap);
                if (fromRoom.HasValue)
                {
                    kind = fromRoom.Value;
                    return true;
                }

                string matchMap = "";
                if (_client.MatchInfo != null)
                {
                    matchMap = _client.MatchInfo.GetStr("game");
                    if (string.IsNullOrEmpty(matchMap))
                        matchMap = _client.MatchInfo.GetStr("map");
                }
                StreetGameKind? fromMatch = StreetBridge.KindFromModeId(matchMap);
                if (fromMatch.HasValue)
                {
                    kind = fromMatch.Value;
                    return true;
                }

                IStreetMode liveMode = _client.Mirror?.Mode;
                if (liveMode is Patintero) { kind = StreetGameKind.Patintero; return true; }
                if (liveMode is TumbangPreso) { kind = StreetGameKind.TumbangPreso; return true; }
                if (liveMode is LangitLupa) { kind = StreetGameKind.LangitLupa; return true; }
            }

            kind = StreetGameKind.Patintero;
            return false;
        }

        public GameClient Client => _client;
        public GameServer HostServer => _hostServer;
        public HotspotBeacon Beacon => _beacon;
        public HotspotFinder Finder => _finder;

        public bool IsOnlineConnected => _client != null && _client.IsOnline();
        public bool IsConnecting => _client != null && _client.IsConnecting();
        public bool IsHosting => _hostServer != null && _hostServer.IsListening;

        public int Lp => _rankRecord.Lp;
        public int PeakLp => _rankRecord.PeakLp;
        public int TotalMatches => _rankRecord.Matches;
        public int TotalWins => _rankRecord.Wins;
        public float WinRate01 => TotalMatches > 0 ? (float)TotalWins / TotalMatches : 0f;

        public int TierIndex => RankedSystem.TierIndex(_rankRecord.Lp);
        public RankTierInfo CurrentTier => RankTiers[Math.Clamp(TierIndex, 0, RankTiers.Length - 1)];
        public int NextTierLp => RankedSystem.NextTierLp(_rankRecord.Lp);
        public float TierProgress01 => (float)RankedSystem.TierProgress(_rankRecord.Lp);

        public RankOutcomeInfo? LastOutcome
        {
            get
            {
                RankedSystem.Outcome r = _rankRecord.LastResult;
                if (r == null) return null;
                RankedSystem.Tier t = RankedSystem.TierForLp(r.NewLp);
                return new RankOutcomeInfo
                {
                    OldLp = r.OldLp,
                    NewLp = r.NewLp,
                    Delta = r.Delta,
                    TierId = t != null ? t.Id : CurrentTier.Id,
                    TierName = !string.IsNullOrEmpty(r.Tier) ? r.Tier : CurrentTier.Name,
                    Promoted = r.Promoted,
                };
            }
        }

        /// <summary>
        /// Applies CI build-stamp text (<c>KALYEAH_SERVER_URL</c> / <c>Data/build_stamp</c>) as <see cref="DefaultServerUrl"/>
        /// and updates <see cref="ServerUrl"/> unless a different custom endpoint is already set.
        /// </summary>
        public string ApplyBuildStampServerUrl(string rawStamp)
        {
            string normalized = SettingsStore.ExtractServerUrlFromBuildStamp(rawStamp);
            string previousDefault = _defaultServerUrl;
            _defaultServerUrl = normalized;
            if (ReferenceEquals(this, Current))
                SettingsStore.Current.ApplyBuildStampServerUrl(rawStamp);

            if (!string.IsNullOrEmpty(normalized) &&
                (string.IsNullOrWhiteSpace(ServerUrl) || string.Equals(ServerUrl, previousDefault, StringComparison.Ordinal)))
            {
                ServerUrl = normalized;
                Changed?.Invoke();
            }
            return normalized;
        }

        public bool SetSuperpowers(IList<string> weaponIds)
        {
            if (weaponIds == null || weaponIds.Count == 0 || weaponIds.Count > 3) return false;
            var unique = new List<string>(3);
            for (int i = 0; i < weaponIds.Count; i++)
            {
                string id = weaponIds[i];
                if (string.IsNullOrWhiteSpace(id)) return false;
                string trimmed = id.Trim();
                if (unique.Contains(trimmed)) return false;
                unique.Add(trimmed);
            }
            _selectedSuperpowers.Clear();
            _selectedSuperpowers.AddRange(unique);
            Changed?.Invoke();
            return true;
        }

        public string[] GetSuperpowerArray() => _selectedSuperpowers.ToArray();

        /// <summary>True when every currently selected superpower in <see cref="SelectedSuperpowers"/> deals 0 direct damage (<see cref="WeaponPick.DealsNoDamage"/>).</summary>
        public bool IsZeroDamageLoadout() => IsZeroDamageLoadout(_selectedSuperpowers, null);

        /// <summary>True when <paramref name="weaponIds"/> is non-empty and none of its weapons deal direct damage (<see cref="WeaponPick.DealsNoDamage"/>).</summary>
        public bool IsZeroDamageLoadout(IList<string> weaponIds, DataSource data = null)
        {
            if (weaponIds == null || weaponIds.Count == 0) return false;
            ResolveWeaponDefs(data, out _, out IReadOnlyDictionary<string, WeaponDef> defMap);
            var cleaned = new List<string>(weaponIds.Count);
            for (int i = 0; i < weaponIds.Count; i++)
            {
                string id = weaponIds[i];
                if (string.IsNullOrWhiteSpace(id)) return false;
                cleaned.Add(id.Trim());
            }
            return WeaponPick.DealsNoDamage(cleaned, defMap);
        }

        public static bool DealsNoDamage(IList<string> weaponIds, DataSource data = null) =>
            Current.IsZeroDamageLoadout(weaponIds, data);

        /// <summary>Returns the canonical 3-slot superpower suggestion (Targeted Attack + Block + Crowd Control) via <see cref="WeaponPick.SuggestionFor"/>.</summary>
        public string[] SuggestSuperpowers() => SuggestSuperpowers(null, 3, null);

        /// <summary>Returns <paramref name="count"/> suggested superpowers via <see cref="WeaponPick.Suggestion"/> / <see cref="WeaponPick.SuggestionFor"/>.</summary>
        public string[] SuggestSuperpowers(int count) => SuggestSuperpowers(null, count, null);

        /// <summary>Returns <paramref name="count"/> suggested superpowers from <paramref name="data"/> (excluding any taken IDs in <paramref name="excludedWeaponIds"/>).</summary>
        public string[] SuggestSuperpowers(DataSource data, int count = 3, ICollection<string> excludedWeaponIds = null)
        {
            int targetCount = Math.Max(count, 1);
            ResolveWeaponDefs(data, out IList<WeaponDef> defs, out _);
            IList<WeaponDef> pool = defs;
            if (excludedWeaponIds != null && excludedWeaponIds.Count > 0)
            {
                var filtered = new List<WeaponDef>(defs.Count);
                for (int i = 0; i < defs.Count; i++)
                {
                    WeaponDef def = defs[i];
                    if (def != null && !string.IsNullOrEmpty(def.Id) && !excludedWeaponIds.Contains(def.Id))
                        filtered.Add(def);
                }
                pool = filtered;
            }

            List<string> suggested = targetCount <= WeaponPick.DefaultSlots
                ? WeaponPick.Suggestion(pool)
                : WeaponPick.SuggestionFor(pool, targetCount);

            if (targetCount == 1 && suggested.Count > 1)
                return new[] { suggested[0] };
            return suggested.ToArray();
        }

        public string[] ValidLoadoutFor(IList<string> availableWeaponIds, int count = 3)
        {
            int limit = Math.Max(count, 1);
            var valid = new List<string>(limit);
            if (availableWeaponIds != null)
            {
                for (int i = 0; i < _selectedSuperpowers.Count && valid.Count < limit; i++)
                {
                    string candidate = _selectedSuperpowers[i];
                    if (ContainsOrdinal(availableWeaponIds, candidate) && !ContainsOrdinal(valid, candidate))
                        valid.Add(candidate);
                }
                for (int i = 0; i < availableWeaponIds.Count && valid.Count < limit; i++)
                {
                    string fallback = availableWeaponIds[i];
                    if (!string.IsNullOrEmpty(fallback) && !ContainsOrdinal(valid, fallback))
                        valid.Add(fallback);
                }
            }
            return valid.ToArray();
        }

        public int PreviewMatchLp(string mode, bool won, int myScore = 0, int theirScore = 0)
        {
            RankedSystem.Outcome o = RankedSystem.PreviewMatch(mode ?? "practice", won, myScore, theirScore, _rankRecord.Lp);
            return o.Delta;
        }

        public RankOutcomeInfo RecordMatchResult(
            string mode,
            string characterId,
            bool won,
            int myScore,
            int theirScore,
            long timestampUnixSeconds = 0)
        {
            string normMode = string.IsNullOrWhiteSpace(mode) ? ModeKey(Mode) : mode.Trim().ToLowerInvariant();
            string kid = string.IsNullOrWhiteSpace(characterId) ? _selectedCharacter : characterId.Trim();
            long ts = timestampUnixSeconds > 0 ? timestampUnixSeconds : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            RankedSystem.Outcome outcome = RankedSystem.RecordMatch(_rankRecord, normMode, won, myScore, theirScore);
            RankedSystem.Tier tierObj = RankedSystem.TierForLp(outcome.NewLp);
            var info = new RankOutcomeInfo
            {
                OldLp = outcome.OldLp,
                NewLp = outcome.NewLp,
                Delta = outcome.Delta,
                TierId = tierObj != null ? tierObj.Id : CurrentTier.Id,
                TierName = !string.IsNullOrEmpty(outcome.Tier) ? outcome.Tier : CurrentTier.Name,
                Promoted = outcome.Promoted,
            };

            _history.Insert(0, new MatchHistoryEntry
            {
                TimestampUnixSeconds = ts,
                Mode = normMode,
                CharacterId = kid,
                Won = won,
                MyScore = myScore,
                TheirScore = theirScore,
                LpDelta = info.Delta,
            });
            while (_history.Count > MaxHistoryEntries)
                _history.RemoveAt(_history.Count - 1);

            Changed?.Invoke();
            return info;
        }

        public RankOutcomeInfo? ConsumeLastOutcome()
        {
            RankedSystem.Outcome r = RankedSystem.ConsumeLastResult(_rankRecord);
            if (r == null) return null;
            RankedSystem.Tier t = RankedSystem.TierForLp(r.NewLp);
            return new RankOutcomeInfo
            {
                OldLp = r.OldLp,
                NewLp = r.NewLp,
                Delta = r.Delta,
                TierId = t != null ? t.Id : CurrentTier.Id,
                TierName = !string.IsNullOrEmpty(r.Tier) ? r.Tier : CurrentTier.Name,
                Promoted = r.Promoted,
            };
        }

        /// <summary>Builds a <see cref="MatchSetup"/> ready to pass to <c>new SimBridge(data, setup)</c>.</summary>
        public MatchSetup BuildMatchSetup(long seed = 0)
        {
            long actualSeed = seed != 0 ? seed : DateTime.UtcNow.Ticks;
            var setup = new MatchSetup
            {
                Seed = actualSeed,
                LocalCharacter = _selectedCharacter,
                SuperpowerLoadout = GetSuperpowerArray(),
                BotDifficulty = SettingsStore.NormalizeDifficulty(BotDifficulty),
                TrainingMode = Mode == GameMode.Training,
                CardCycleOverride = SettingsStore.Current.CardsTrayMode ? true : (bool?)null,
                FirstTo = FirstTo,
            };
            if (IsNetworkedMode && _client != null)
                setup.OnlineClient = _client;
            return setup;
        }

        public SimData EnsureSimData(DataSource data)
        {
            if (_sharedSimData != null) return _sharedSimData;
            _sharedSimData = SimBridge.LoadSimDataGraph(
                data,
                "rules__game_rules",
                "rules__map_layout",
                "rules__net_rules",
                "rules__street_games",
                "rules__patintero",
                "rules__tumbang_preso",
                "rules__langit_lupa",
                "rules__larong_kalye",
                "rules__donate_info");
            _sharedWeaponDefMap = BuildWeaponDefMap(_sharedSimData.GameRules.Weapons);
            return _sharedSimData;
        }

        public GameClient EnsureClient(DataSource data)
        {
            SimData simData = EnsureSimData(data);
            if (_client == null)
            {
                NetRules netRules = NetRules.FromData(simData);
                var catalog = new StreetCatalog(simData);
                _client = new GameClient(simData.GameRules, simData.MapLayout, netRules, catalog)
                {
                    PlayerName = _playerName,
                    Token = ReconnectToken ?? "",
                };
                _client.ConnectionChanged += OnClientConnectionChanged;
                _client.Welcomed += _ =>
                {
                    if (_client.PlayerId > 0) LocalPeerId = _client.PlayerId;
                    ReconnectToken = _client.Token ?? "";
                    Changed?.Invoke();
                };
                _client.RoomChanged += room =>
                {
                    RoomCode = room != null ? room.GetStr("code") : "";
                    if (_client.PlayerId > 0) LocalPeerId = _client.PlayerId;
                    ReconnectToken = _client.Token ?? "";
                    Changed?.Invoke();
                };
            }
            else
            {
                _client.PlayerName = _playerName;
            }
            return _client;
        }

        public bool StartOnlineClient(DataSource data, string wsUrl, bool patient = true)
        {
            string candidate = !string.IsNullOrWhiteSpace(wsUrl) ? wsUrl : EffectiveServerUrl;
            string normalized = SettingsStore.NormalizeServerUrl(candidate);
            if (string.IsNullOrEmpty(normalized)) return false;
            GameClient client = EnsureClient(data);
            client.Token = ReconnectToken ?? "";
            if (!client.ConnectTo(normalized, patient)) return false;
            ServerUrl = normalized;
            ReconnectUrl = normalized;
            Mode = GameMode.Online;
            return true;
        }

        public bool StartHotspotHost(DataSource data, int port = 0, long seed = 0)
        {
            StopHost();
            FinishClosingServer();

            SimData simData = EnsureSimData(data);
            NetRules netRules = NetRules.FromData(simData);
            var catalog = new StreetCatalog(simData);
            long serverSeed = seed != 0 ? seed : DateTime.UtcNow.Ticks;
            int listenPort = port > 0 ? port : netRules.DefaultPort;

            var server = new GameServer(simData.GameRules, simData.MapLayout, netRules, serverSeed, catalog)
            {
                AutoJoin = true,
            };
            try
            {
                server.Listen(listenPort);
            }
            catch
            {
                return false;
            }
            if (!server.IsListening)
                return false;

            _hostServer = server;
            _beacon ??= new HotspotBeacon();
            _beacon.Listen(netRules.DiscoveryPort, netRules.DiscoveryBurst);

            HostEndpoint = "ws://127.0.0.1:" + listenPort.ToString(CultureInfo.InvariantCulture);
            GameClient client = EnsureClient(data);
            client.Token = "";
            if (!client.ConnectTo(HostEndpoint, false))
            {
                StopHost();
                return false;
            }

            Mode = GameMode.HotspotHost;
            Changed?.Invoke();
            return true;
        }

        public HotspotFinder StartHotspotDiscovery(DataSource data, IReadOnlyList<NetInterface> interfaces = null)
        {
            StopDiscovery();
            SimData simData = EnsureSimData(data);
            NetRules netRules = NetRules.FromData(simData);
            IEnumerable<NetInterface> activeInterfaces = interfaces;
            if (activeInterfaces == null)
            {
                try
                {
                    activeInterfaces = NetInterface.FromSystem();
                }
                catch
                {
                    activeInterfaces = Array.Empty<NetInterface>();
                }
            }
            HotspotNet.Plan plan = HotspotNet.SearchPlan(activeInterfaces, netRules);
            _finder = new HotspotFinder();
            _finder.Start(plan);
            return _finder;
        }

        public bool ConnectHotspotGuest(DataSource data, string wsUrl)
        {
            string normalized = SettingsStore.NormalizeServerUrl(wsUrl);
            if (string.IsNullOrEmpty(normalized)) return false;
            StopDiscovery();
            GameClient client = EnsureClient(data);
            client.Token = "";
            if (!client.ConnectTo(normalized, false))
                return false;
            HostEndpoint = normalized;
            ReconnectUrl = normalized;
            Mode = GameMode.HotspotClient;
            Changed?.Invoke();
            return true;
        }

        public void PollNetwork(double dt)
        {
            _client?.Poll(dt);
            if (_hostServer != null)
            {
                _hostServer.Poll(dt);
                _beacon?.Poll();
            }
            if (_closingServer != null)
            {
                _closingServer.Poll(dt);
                _closingLeft -= dt;
                if (_closingLeft <= 0.0)
                    FinishClosingServer();
            }
            _finder?.Poll(dt);

            if (_client != null && _client.PlayerId > 0)
                LocalPeerId = _client.PlayerId;
        }

        public void StopDiscovery()
        {
            if (_finder != null)
            {
                _finder.Stop();
                _finder = null;
            }
        }

        public void StopHost()
        {
            _beacon?.Stop();
            if (_hostServer != null)
            {
                FinishClosingServer();
                _hostServer.CloseRooms();
                _closingServer = _hostServer;
                _closingLeft = CloseGraceSeconds;
                _hostServer = null;
            }
        }

        public void DisconnectClient()
        {
            _client?.Close();
        }

        public void StopAllNetwork()
        {
            StopDiscovery();
            DisconnectClient();
            StopHost();
            FinishClosingServer();
        }

        void FinishClosingServer()
        {
            if (_closingServer != null)
            {
                _closingServer.Stop();
                _closingServer = null;
            }
        }

        void OnClientConnectionChanged(bool online)
        {
            ConnectionChanged?.Invoke(online);
            Changed?.Invoke();
        }

        public string ToJson()
        {
            var sb = new StringBuilder(576);
            sb.Append('{');
            sb.Append("\"player_name\":\"");
            SettingsStore.EscapeJsonString(sb, _playerName);
            sb.Append("\",\"selected_character\":\"");
            SettingsStore.EscapeJsonString(sb, _selectedCharacter);
            sb.Append("\",\"avatar_index\":").Append(_avatarIndex.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"has_custom_id_photo\":").Append(_hasCustomIdPhoto ? "true" : "false");
            sb.Append(",\"bot_difficulty\":\"");
            SettingsStore.EscapeJsonString(sb, BotDifficulty ?? "medium");
            sb.Append("\",\"first_to\":").Append(FirstTo.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"room_code\":\"");
            SettingsStore.EscapeJsonString(sb, RoomCode ?? "");
            sb.Append("\",\"reconnect_token\":\"");
            SettingsStore.EscapeJsonString(sb, ReconnectToken ?? "");
            sb.Append("\",\"reconnect_url\":\"");
            SettingsStore.EscapeJsonString(sb, ReconnectUrl ?? "");
            sb.Append("\",\"server_url\":\"");
            SettingsStore.EscapeJsonString(sb, ServerUrl ?? "");
            sb.Append("\",\"superpowers\":[");
            for (int i = 0; i < _selectedSuperpowers.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"');
                SettingsStore.EscapeJsonString(sb, _selectedSuperpowers[i]);
                sb.Append('"');
            }
            sb.Append("],\"lp\":").Append(Lp.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"peak_lp\":").Append(PeakLp.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"matches\":").Append(TotalMatches.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"wins\":").Append(TotalWins.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"history\":[");
            for (int i = 0; i < _history.Count; i++)
            {
                if (i > 0) sb.Append(',');
                MatchHistoryEntry h = _history[i];
                sb.Append("{\"ts\":").Append(h.TimestampUnixSeconds.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"mode\":\"");
                SettingsStore.EscapeJsonString(sb, h.Mode ?? "");
                sb.Append("\",\"kid\":\"");
                SettingsStore.EscapeJsonString(sb, h.CharacterId ?? "");
                sb.Append("\",\"won\":").Append(h.Won ? "true" : "false");
                sb.Append(",\"my\":").Append(h.MyScore.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"their\":").Append(h.TheirScore.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"delta\":").Append(h.LpDelta.ToString(CultureInfo.InvariantCulture)).Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        public bool LoadJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            Dictionary<string, object> d;
            try
            {
                d = MiniJson.Parse(json) as Dictionary<string, object>;
            }
            catch
            {
                return false;
            }
            if (d == null) return false;

            if (SettingsStore.TryString(d, "player_name", out string pn))
            {
                string cleaned = CleanName(pn);
                _playerName = string.IsNullOrEmpty(cleaned) ? DefaultPlayerName : cleaned;
            }
            if (SettingsStore.TryString(d, "selected_character", out string sc) && IsKnownAvatar(sc))
                _selectedCharacter = sc;
            if (SettingsStore.TryInt(d, "avatar_index", out int avIdx))
                _avatarIndex = Math.Clamp(avIdx, 0, AvatarPhotoIds.Length - 1);
            if (SettingsStore.TryBool(d, "has_custom_id_photo", out bool customPhoto))
                _hasCustomIdPhoto = customPhoto;
            if (SettingsStore.TryString(d, "bot_difficulty", out string bd))
                BotDifficulty = SettingsStore.NormalizeDifficulty(bd);
            if (SettingsStore.TryInt(d, "first_to", out int ft) && ft > 0)
                FirstTo = ft;
            if (SettingsStore.TryString(d, "room_code", out string rc))
                RoomCode = rc;
            if (SettingsStore.TryString(d, "reconnect_token", out string rt))
                ReconnectToken = rt;
            if (SettingsStore.TryString(d, "reconnect_url", out string ru))
                ReconnectUrl = ru;
            if (SettingsStore.TryString(d, "server_url", out string su))
                ServerUrl = !string.IsNullOrWhiteSpace(su) ? SettingsStore.NormalizeServerUrl(su) : _defaultServerUrl;

            if (d.TryGetValue("superpowers", out object spObj) && spObj is List<object> spList)
            {
                var picks = new List<string>(3);
                for (int i = 0; i < spList.Count && picks.Count < 3; i++)
                    if (spList[i] is string s && !string.IsNullOrWhiteSpace(s) && !picks.Contains(s))
                        picks.Add(s);
                if (picks.Count > 0)
                {
                    _selectedSuperpowers.Clear();
                    _selectedSuperpowers.AddRange(picks);
                }
            }

            SettingsStore.TryInt(d, "lp", out int lp);
            SettingsStore.TryInt(d, "peak_lp", out int peak);
            SettingsStore.TryInt(d, "matches", out int matches);
            SettingsStore.TryInt(d, "wins", out int wins);
            _rankRecord.Lp = Math.Max(0, lp);
            _rankRecord.PeakLp = Math.Max(_rankRecord.Lp, peak);
            _rankRecord.Matches = Math.Max(0, matches);
            _rankRecord.Wins = Math.Clamp(wins, 0, _rankRecord.Matches);

            _history.Clear();
            if (d.TryGetValue("history", out object histObj) && histObj is List<object> histList)
            {
                for (int i = 0; i < histList.Count && _history.Count < MaxHistoryEntries; i++)
                {
                    if (!(histList[i] is Dictionary<string, object> hd)) continue;
                    SettingsStore.TryLong(hd, "ts", out long ts);
                    SettingsStore.TryString(hd, "mode", out string hm);
                    SettingsStore.TryString(hd, "kid", out string hk);
                    SettingsStore.TryBool(hd, "won", out bool hw);
                    SettingsStore.TryInt(hd, "my", out int hmy);
                    SettingsStore.TryInt(hd, "their", out int hth);
                    SettingsStore.TryInt(hd, "delta", out int hdl);
                    _history.Add(new MatchHistoryEntry
                    {
                        TimestampUnixSeconds = ts,
                        Mode = hm ?? "practice",
                        CharacterId = hk ?? DefaultCharacter,
                        Won = hw,
                        MyScore = hmy,
                        TheirScore = hth,
                        LpDelta = hdl,
                    });
                }
            }

            Changed?.Invoke();
            return true;
        }

        public static AppSession FromJson(string json)
        {
            var session = new AppSession();
            session.LoadJson(json);
            return session;
        }

        public static string FilterName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            return MultiSpaces.Replace(DisallowedNameChars.Replace(raw, ""), " ");
        }

        public static string CleanName(string raw)
        {
            string filtered = FilterName(raw).Trim();
            if (filtered.Length > MaxNameLength)
                filtered = filtered.Substring(0, MaxNameLength).Trim();
            return filtered;
        }

        public static bool IsKnownAvatar(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < DefaultRoster.Length; i++)
                if (string.Equals(DefaultRoster[i], id, StringComparison.Ordinal))
                    return true;
            return false;
        }

        public static string ModeKey(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Online: return "online";
                case GameMode.HotspotHost:
                case GameMode.HotspotClient: return "local";
                case GameMode.Training: return "training";
                case GameMode.Patintero: return "patintero";
                case GameMode.TumbangPreso: return "tumbang_preso";
                case GameMode.LangitLupa: return "langit_lupa";
                default: return "practice";
            }
        }

        void ResolveWeaponDefs(
            DataSource data,
            out IList<WeaponDef> list,
            out IReadOnlyDictionary<string, WeaponDef> map)
        {
            if (data != null)
            {
                try
                {
                    SimData simData = EnsureSimData(data);
                    if (simData?.GameRules?.Weapons != null && simData.GameRules.Weapons.Count > 0)
                    {
                        list = simData.GameRules.Weapons;
                        _sharedWeaponDefMap ??= BuildWeaponDefMap(simData.GameRules.Weapons);
                        map = _sharedWeaponDefMap;
                        return;
                    }
                }
                catch
                {
                }
            }
            else if (_sharedSimData?.GameRules?.Weapons != null && _sharedSimData.GameRules.Weapons.Count > 0)
            {
                list = _sharedSimData.GameRules.Weapons;
                _sharedWeaponDefMap ??= BuildWeaponDefMap(_sharedSimData.GameRules.Weapons);
                map = _sharedWeaponDefMap;
                return;
            }

            list = DefaultWeaponDefs;
            map = DefaultWeaponDefMap;
        }

        static List<WeaponDef> BuildDefaultWeaponDefs()
        {
            return new List<WeaponDef>(14)
            {
                MakeStubWeapon("bato_light", WeaponDef.WeaponKind.Attack, WeaponDef.WeaponShape.Targeted, 18),
                MakeStubWeapon("bato_heavy", WeaponDef.WeaponKind.Attack, WeaponDef.WeaponShape.GroundAoe, 36),
                MakeStubWeapon("gunting_light", WeaponDef.WeaponKind.Attack, WeaponDef.WeaponShape.Cone, 9),
                MakeStubWeapon("gunting_heavy", WeaponDef.WeaponKind.Attack, WeaponDef.WeaponShape.Cone, 38),
                MakeStubWeapon("papel_trap", WeaponDef.WeaponKind.CrowdControl, WeaponDef.WeaponShape.Trap, 0),
                MakeStubWeapon("papel_shield", WeaponDef.WeaponKind.Block, WeaponDef.WeaponShape.Shield, 0),
                MakeStubWeapon("tsinelas_light", WeaponDef.WeaponKind.Attack, WeaponDef.WeaponShape.Boomerang, 9),
                MakeStubWeapon("tsinelas_heavy", WeaponDef.WeaponKind.Attack, WeaponDef.WeaponShape.Boomerang, 25),
                MakeStubWeapon("lata", WeaponDef.WeaponKind.CrowdControl, WeaponDef.WeaponShape.GroundAoe, 16),
                MakeStubWeapon("jacks", WeaponDef.WeaponKind.CrowdControl, WeaponDef.WeaponShape.Field, 6),
                MakeStubWeapon("bola", WeaponDef.WeaponKind.CrowdControl, WeaponDef.WeaponShape.Bouncer, 14),
                MakeStubWeapon("trumpo", WeaponDef.WeaponKind.CrowdControl, WeaponDef.WeaponShape.Spinner, 22),
                MakeStubWeapon("heal", WeaponDef.WeaponKind.Heal, WeaponDef.WeaponShape.Heal, 0),
                MakeStubWeapon("langit_lupa", WeaponDef.WeaponKind.Block, WeaponDef.WeaponShape.Field, 0),
            };
        }

        static WeaponDef MakeStubWeapon(
            string id,
            WeaponDef.WeaponKind kind,
            WeaponDef.WeaponShape shape,
            int damage)
        {
            return new WeaponDef
            {
                Id = id,
                Kind = kind,
                Shape = shape,
                Damage = damage,
            };
        }

        static Dictionary<string, WeaponDef> BuildWeaponDefMap(IList<WeaponDef> defs)
        {
            var map = new Dictionary<string, WeaponDef>(defs.Count, StringComparer.Ordinal);
            for (int i = 0; i < defs.Count; i++)
            {
                WeaponDef d = defs[i];
                if (d != null && !string.IsNullOrEmpty(d.Id))
                    map[d.Id] = d;
            }
            return map;
        }

        static bool ContainsOrdinal(IList<string> list, string item)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], item, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }
}
