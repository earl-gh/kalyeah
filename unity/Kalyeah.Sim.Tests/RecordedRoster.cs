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

        /// <summary>`rules` with its characters and Agawang Base rules swapped for the recorded ones (mutates and returns the given copy).</summary>
        public static GameRules Apply(GameRules rules)
        {
            var characters = new List<CharacterDef>();
            foreach (string id in Ids) characters.Add(new CharacterDef { Id = id, DisplayName = id, Description = id });
            rules.Characters = characters;
            return Rules(rules);
        }

        /// <summary>`rules` with the pre-spec Agawang Base rules the Godot goldens were recorded with (docs/SPEC.md §5 replaced them).</summary>
        public static GameRules Rules(GameRules rules)
        {
            rules.SpecRules = false;
            // pre-spec movement (docs/SPEC.md §7 replaced it): one run speed in proportion to the stick, no running, old crouch and slide
            rules.MoveSpeed = 6.4;
            rules.SprintEnabled = false;
            rules.SprintDelay = 1.2;
            rules.SprintMultiplier = 1.32;
            rules.CrouchSpeedShare = 0.55;
            rules.SlideSpeed = 10.2;
            rules.SlideDuration = 0.46;
            rules.SlideCooldown = 3.2;
            rules.RescueHoldTime = 4.0;
            rules.RescueCooldown = 8.0;
            rules.NoCastInRescueArea = true;
            rules.RescueCheckpoint = 0.0;
            rules.RevengeReleaseSeconds = 0.0;
            rules.RevengeShieldSeconds = 0.0;
            rules.CaptureDrainShare = 0.0;
            rules.BarrierSeconds = 0.0;
            rules.CaptureSoloTime = 12.0;
            rules.CaptureAllyBonus = 0.65;
            rules.OvertimeAfter = 240.0;
            return rules;
        }
    }
}
