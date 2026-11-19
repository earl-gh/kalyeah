using System.Collections.Generic;

namespace Kalyeah.Sim.Tests
{
    /// <summary>
    /// The 10-kid roster the Godot goldens were recorded with. AssignCharacters shuffles the roster with the match RNG, so a replay
    /// must deal from the same roster to stay tick-identical; the game itself plays the 12 kids of docs/SPEC.md §7.
    /// </summary>
    public static class RecordedRoster
    {
        public static readonly string[] Ids =
        {
            "junjun", "ligaya", "migo", "toni", "popoy",
            "inday", "kiko", "nene", "buknoy", "tala",
        };

        /// <summary>`rules` with its characters swapped for the recorded roster (mutates and returns the given copy).</summary>
        public static GameRules Apply(GameRules rules)
        {
            var characters = new List<CharacterDef>();
            foreach (string id in Ids) characters.Add(new CharacterDef { Id = id, DisplayName = id, Description = id });
            rules.Characters = characters;
            return rules;
        }
    }
}
