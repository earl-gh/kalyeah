using System;
using System.Collections.Generic;

namespace Kalyeah.Game
{
    /// <summary>
    /// The match clock as the HUD shows it (scripts/ui/hud/clock_chip.gd): the time (m:ss; counting down it rounds up, so 0:00 shows only
    /// once the time is really out, counting up it rounds down) and the stage's caption: none in the Regular stage, "Extended", then a gold
    /// "Overtime" (MatchSim.ClockStage 0, 1, 2).
    /// </summary>
    public readonly struct ClockDisplay
    {
        public const int Regular = 0, Extended = 1, Overtime = 2;

        public readonly int Seconds;
        public readonly int Stage;
        public readonly bool CountingDown;

        public ClockDisplay(int seconds, int stage, bool countingDown)
        {
            Seconds = seconds;
            Stage = stage;
            CountingDown = countingDown;
        }

        public static ClockDisplay Of(double value, int stage, bool down) =>
            new ClockDisplay(down ? (int)Math.Ceiling(value - 0.0001) : (int)Math.Floor(value), stage, down);

        public string Text => $"{Seconds / 60}:{Seconds % 60:00}";

        /// <summary>The stage word stands over the time (Extended, Overtime); in the Regular stage the box holds the time alone.</summary>
        public bool HasCaption => Stage != Regular;

        public string Caption => Stage switch
        {
            Extended => "Extended",
            Overtime => "Overtime",
            _ => "Time left:",
        };

        /// <summary>Gold in Overtime.</summary>
        public bool Gold => Stage == Overtime;
    }

    /// <summary>The HUD's words that need no sim (match_results.gd, room_player_cards.gd).</summary>
    public static class HudText
    {
        /// <summary>MatchSim.CaptureKind values as the point log stores them.</summary>
        public const int HowNone = -1, HowBase = 0, HowAllTagged = 1, HowTouched = 2, HowTooSlow = 3;

        /// <summary>room_player_cards.gd slot_number: team 0 is 1..size, team 1 is size+1..2*size.</summary>
        public static int SlotNumber(int team, int row, int size) => team * size + row + 1;

        /// <summary>The words for one point (match_results.gd how_text).</summary>
        public static string HowText(bool ours, int how) => how switch
        {
            HowAllTagged => ours ? "Tagged them all" : "Tagged you all",
            HowTooSlow => ours ? "Runners too slow" : "You were too slow",
            HowTouched => ours ? "Tagged a runner" : "Tagged by a guard",
            HowNone => ours ? "Ran it home" : "They ran it home",
            _ => ours ? "Held their base" : "Held your base",
        };

        /// <summary>The point log (team, how) grouped by who and how, in the order each first happened, with a count.</summary>
        public static List<(bool ours, int how, int times)> HowGroups(IEnumerable<(int team, int how)> log, int localTeam)
        {
            var groups = new List<(bool ours, int how, int times)>();
            foreach (var (team, how) in log)
            {
                bool ours = team == localTeam;
                int found = groups.FindIndex(g => g.ours == ours && g.how == how);
                if (found >= 0) groups[found] = (ours, how, groups[found].times + 1);
                else groups.Add((ours, how, 1));
            }
            return groups;
        }

        public static string GroupText(bool ours, int how, int times) => times < 2 ? HowText(ours, how) : $"{HowText(ours, how)} × {times}";

        /// <summary>A hero button's cooldown seconds: "0.4" under a second, else whole seconds rounded up (ability_arc.gd _draw_cd_seconds).</summary>
        public static string CooldownText(float secondsLeft) =>
            secondsLeft < 1f ? secondsLeft.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : ((int)Math.Ceiling(secondsLeft)).ToString();
    }
}
