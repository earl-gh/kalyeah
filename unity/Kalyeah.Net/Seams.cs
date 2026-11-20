using System;
using Kalyeah.Sim;

namespace Kalyeah.Net
{
    /// <summary>The street games a room can pick (street_mode.gd `rules_for` / `create`), from the shipped data.</summary>
    public sealed class StreetCatalog
    {
        readonly SimData _data;

        public StreetCatalog(SimData data) { _data = data; }

        /// <summary>The game's rules, or null for Agawang Base (any other map id).</summary>
        public StreetModeRules RulesFor(string mapId) => StreetMode.RulesFor(_data, mapId);

        /// <summary>True for a game where every kid plays for itself (room_service.gd `free_for_all`: Tumbang Preso, Langit Lupa).</summary>
        public bool IsFreeForAll(string mapId)
        {
            StreetModeRules rules = RulesFor(mapId);
            return rules != null && StreetMode.Create(rules).IsFreeForAll();
        }

        public StreetMode Create(StreetModeRules rules) => StreetMode.Create(rules);
    }

    /// <summary>Lobby constants that live outside scripts/net in the Godot game.</summary>
    public static class Lobby
    {
        /// <summary>The 12 kids (docs/SPEC.md §7), in order: a new member gets the first one free on its team.</summary>
        public static readonly string[] Roster = { "narda", "jumong", "inggo", "jopay", "oriang", "paeng", "kikay", "karding", "manoy", "tere", "pamela", "domeng" };

        static string[] _avatars = Roster;

        /// <summary>The kids a player may pick (session.gd AVATARS): the roster, unless a replay swapped it.</summary>
        public static string[] Avatars => _avatars;

        /// <summary>Replays recordings made with an older roster (the Godot net goldens); dispose to restore the roster.</summary>
        public static IDisposable UseAvatars(string[] avatars)
        {
            string[] previous = _avatars;
            _avatars = avatars ?? throw new ArgumentNullException(nameof(avatars));
            return new Restore(() => _avatars = previous);
        }

        sealed class Restore : IDisposable
        {
            Action _undo;
            public Restore(Action undo) => _undo = undo;
            public void Dispose()
            {
                _undo?.Invoke();
                _undo = null;
            }
        }

        static Func<long, string> _tierName = lp => RankedSystem.TierName((int)Math.Max(int.MinValue, Math.Min(int.MaxValue, lp)));

        /// <summary>ranked_system.gd `tier_name(lp)`.</summary>
        public static string TierName(long lp) => _tierName(lp);

        /// <summary>Replays recordings made before Karton existed (the Godot net goldens); dispose to restore the tier names.</summary>
        public static IDisposable UseTierNames(Func<long, string> tierName)
        {
            Func<long, string> previous = _tierName;
            _tierName = tierName ?? throw new ArgumentNullException(nameof(tierName));
            return new Restore(() => _tierName = previous);
        }
    }
}
