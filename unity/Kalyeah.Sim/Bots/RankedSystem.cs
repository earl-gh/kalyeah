using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>
    /// Tagalog competitive ranked progression, "Kahoy" to "Batang Kalyeah" (the rules of scripts/sim/ranked_system.gd).
    /// Online and Local Hotspot: a win is +30 LP plus +2 per point of lead (lead capped at 5), a loss -18 LP. Practice: a win is +12 LP plus
    /// +1 per point of lead (capped at 3), a loss -6 LP. LP never goes below 0.
    /// The GDScript keeps the numbers in a ConfigFile (`user://ranked.cfg`); here they live in a <see cref="Record"/> that the game saves
    /// wherever it likes. The emblem drawing (`draw_emblem`) is presentation and is not part of the sim.
    /// </summary>
    public static class RankedSystem
    {
        public const int OnlineWinLp = 30;
        public const int OnlineLossLp = -18;
        public const int OnlineMarginStep = 2;
        public const int OnlineMarginCap = 5;
        public const int PracticeWinLp = 12;
        public const int PracticeLossLp = -6;
        public const int PracticeMarginCap = 3;

        /// <summary>One tier. `MaxLp` -1 on Batang Kalyeah means uncapped (1500+ LP). Colours are Godot `Color("rrggbb")` hex strings.</summary>
        public sealed class Tier
        {
            public string Id;
            public string Name;
            public int MinLp;
            public int MaxLp;
            public string Color;
            public string Accent;
            public string Dark;
            public string Ink;
            public string Flavor;

            public Tier(string id, string name, int minLp, int maxLp, string color, string accent, string dark, string ink, string flavor)
            {
                Id = id; Name = name; MinLp = minLp; MaxLp = maxLp; Color = color; Accent = accent; Dark = dark; Ink = ink; Flavor = flavor;
            }
        }

        /// <summary>A player's saved ranked numbers (GDScript: the `ranked` section of the ConfigFile).</summary>
        public sealed class Record
        {
            public int Lp;
            public int PeakLp;
            public int Matches;
            public int Wins;
            /// <summary>The latest RecordMatch outcome, until ConsumeLastResult takes it.</summary>
            public Outcome LastResult;
        }

        /// <summary>The LP outcome of one match (GDScript: {old_lp, new_lp, delta, tier, promoted}).</summary>
        public sealed class Outcome : IEquatable<Outcome>
        {
            public int OldLp;
            public int NewLp;
            public int Delta;
            public string Tier;
            public bool Promoted;

            public bool Equals(Outcome o) => o != null && OldLp == o.OldLp && NewLp == o.NewLp && Delta == o.Delta && Tier == o.Tier && Promoted == o.Promoted;
            public override bool Equals(object obj) => Equals(obj as Outcome);
            public override int GetHashCode() => (OldLp * 397 ^ NewLp) * 397 ^ Delta;
        }

        /// <summary>The 7 tiers, lowest first.</summary>
        public static readonly IReadOnlyList<Tier> Tiers = new[]
        {
            new Tier("kahoy", "Kahoy", 0, 199, "c8874b", "f2be86", "5c3416", "241207", "Warm Carved Wood"),
            new Tier("bakal", "Bakal", 200, 399, "8a99b0", "d0dced", "364052", "141923", "Forged Dark Iron"),
            new Tier("tanso", "Tanso", 400, 599, "dc7d3e", "ffba80", "6b3110", "281004", "Burnished Bronze"),
            new Tier("pilak", "Pilak", 600, 849, "d6e5f7", "ffffff", "536987", "182233", "Gleaming Silver"),
            new Tier("ginto", "Ginto", 850, 1149, "ffc938", "fff29c", "8c5400", "2e1900", "Radiant Barrio Gold"),
            new Tier("brilyante", "Brilyante", 1150, 1499, "4ce4ff", "d9fcff", "0d5e8a", "062338", "Prism Cyan Diamond"),
            new Tier("batang_kalyeah", "Batang Kalyeah", 1500, -1, "ff6b2b", "ffe14d", "7d1818", "24070c", "Mythic Komiks Street Crown"),
        };

        /// <summary>The index (0..6) in <see cref="Tiers"/> for `lp`.</summary>
        public static int TierIndex(int lp)
        {
            int clamped = Math.Max(0, lp);
            for (int i = Tiers.Count - 1; i >= 0; i--)
                if (clamped >= Tiers[i].MinLp) return i;
            return 0;
        }

        public static Tier TierForLp(int lp) => Tiers[TierIndex(lp)];

        public static string TierName(int lp) => Tiers[TierIndex(lp)].Name;

        /// <summary>The `MinLp` of the next tier above `lp`, or -1 at Batang Kalyeah.</summary>
        public static int NextTierLp(int lp)
        {
            int idx = TierIndex(lp);
            return idx + 1 >= Tiers.Count ? -1 : Tiers[idx + 1].MinLp;
        }

        /// <summary>0..1 progress across the current tier (1 once in Batang Kalyeah).</summary>
        public static double TierProgress(int lp)
        {
            int clamped = Math.Max(0, lp);
            int idx = TierIndex(clamped);
            if (idx + 1 >= Tiers.Count) return 1.0;
            int floorLp = Tiers[idx].MinLp;
            int ceilingLp = Tiers[idx + 1].MinLp;
            int span = Math.Max(1, ceilingLp - floorLp);
            return GMath.Clampf((double)(clamped - floorLp) / span, 0.0, 1.0);
        }

        /// <summary>The LP outcome of a match from `baseLp`, without changing anything (`mode` "practice", else online/hotspot).</summary>
        public static Outcome PreviewMatch(string mode, bool won, int myScore, int theirScore, int baseLp)
        {
            int oldLp = Math.Max(0, baseLp);
            string cleanMode = (mode ?? "").Trim().ToLowerInvariant();
            int margin = Math.Max(0, myScore - theirScore);
            int rawDelta;
            if (cleanMode == "practice") rawDelta = won ? PracticeWinLp + Math.Min(margin, PracticeMarginCap) : PracticeLossLp;
            else rawDelta = won ? OnlineWinLp + Math.Min(margin, OnlineMarginCap) * OnlineMarginStep : OnlineLossLp;
            int newLp = Math.Max(0, oldLp + rawDelta);
            int oldIdx = TierIndex(oldLp);
            int newIdx = TierIndex(newLp);
            return new Outcome { OldLp = oldLp, NewLp = newLp, Delta = newLp - oldLp, Tier = Tiers[newIdx].Name, Promoted = newIdx > oldIdx };
        }

        /// <summary>The outcome from the record's LP (GDScript `preview_match` with no base: the saved LP).</summary>
        public static Outcome PreviewMatch(Record record, string mode, bool won, int myScore, int theirScore) =>
            PreviewMatch(mode, won, myScore, theirScore, record == null ? 0 : record.Lp);

        /// <summary>Applies a match to `record` (LP, peak, matches, wins) and returns its outcome; the caller saves the record.</summary>
        public static Outcome RecordMatch(Record record, string mode, bool won, int myScore, int theirScore)
        {
            int oldLp = Math.Max(0, record.Lp);
            int prevPeak = Math.Max(oldLp, record.PeakLp);
            Outcome outcome = PreviewMatch(mode, won, myScore, theirScore, oldLp);
            record.Lp = outcome.NewLp;
            record.PeakLp = Math.Max(prevPeak, outcome.NewLp);
            record.Matches = Math.Max(0, record.Matches) + 1;
            record.Wins = Math.Max(0, record.Wins) + (won ? 1 : 0);
            record.LastResult = outcome;
            return outcome;
        }

        /// <summary>The latest RecordMatch outcome of `record`, once (null when there is none).</summary>
        public static Outcome ConsumeLastResult(Record record)
        {
            Outcome last = record.LastResult;
            record.LastResult = null;
            return last;
        }

        /// <summary>A tier colour as a GColor (Godot's Color("rrggbb")).</summary>
        public static GColor ParseColor(string hex)
        {
            int rgb = Convert.ToInt32(hex, 16);
            return new GColor(((rgb >> 16) & 0xff) / 255.0, ((rgb >> 8) & 0xff) / 255.0, (rgb & 0xff) / 255.0, 1.0);
        }
    }
}
