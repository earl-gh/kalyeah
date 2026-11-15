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
        /// <summary>The kids a player may pick (session.gd AVATARS), in order: a new member gets the first one free on its team.</summary>
        public static readonly string[] Avatars = { "junjun", "ligaya", "migo", "toni", "popoy", "inday", "kiko", "nene", "buknoy", "tala" };

        /// <summary>ranked_system.gd `tier_name(lp)`.</summary>
        public static string TierName(long lp) => RankedSystem.TierName((int)Math.Max(int.MinValue, Math.Min(int.MaxValue, lp)));
    }
}
