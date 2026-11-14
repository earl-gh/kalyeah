using System;
using System.Collections.Generic;

namespace Kalyeah.Sim
{
    /// <summary>A cosmetic character (port of character_def.gd). Never affects gameplay.</summary>
    [Serializable]
    public sealed class CharacterDef
    {
        public enum HairStyle { Short, Ponytail, Pigtails, CapBackward, Buzz, Spiky, Curly, LongWavy }
        public enum TopStyle { Sando, TeeKnotted, Bestida, Jersey, PoloStriped, PeShirt }
        public enum BottomStyle { Shorts, CargoShorts, JoggingPants, None }
        public enum ExtraStyle { None, Bimpo, HairClip, HeadbandBeltBag, IceCandy, PonyBands }

        public string Id = "";
        public string DisplayName = "";
        public string Description = "";
        public GColor Tint = GColor.White;
        public GColor Skin = new GColor(0.78, 0.58, 0.42);
        public GColor HairColor = new GColor(0.1, 0.08, 0.07);
        public HairStyle Hair = HairStyle.Short;
        public TopStyle Top = TopStyle.TeeKnotted;
        public GColor TopAccent = GColor.White;
        public BottomStyle Bottom = BottomStyle.Shorts;
        public GColor BottomColor = new GColor(0.2, 0.3, 0.6);
        public bool Shoes;
        public GColor ShoeColor = new GColor(0.2, 0.5, 0.9);
        public ExtraStyle Extra = ExtraStyle.None;
        public GColor ExtraColor = GColor.White;
        public double CastReleaseStanding;
        public double CastReleaseMoving;
        public double BodyWidth = 1.0;
    }

    /// <summary>One Practice difficulty (port of bot_level.gd).</summary>
    [Serializable]
    public sealed class BotLevel
    {
        public string Id = "";
        public string Label = "";
        public string Blurb = "";
        public double Skill;
        public double AimErrorDegrees;
        public double CastGap;
        public double AggroRange;
        public double AssaultDelay;
        public double AssaultWindow;
        public double RestTime;
    }

    /// <summary>What the Donate sheet shows (port of donate_info.gd).</summary>
    [Serializable]
    public sealed class DonateInfo
    {
        public string Thanks = "";
        public string GrownUps = "";
        public string Url = "";

        public bool HasLink() => Url.StartsWith("https://", StringComparison.Ordinal);
    }

    /// <summary>The street game behind each weapon, one sentence each (port of street_games.gd). Text only.</summary>
    [Serializable]
    public sealed class StreetGames
    {
        public OrderedDict<string, string> Blurbs = new OrderedDict<string, string>();

        /// <summary>The sentence for `game` (case-insensitive), or "" when there is none.</summary>
        public string BlurbOf(string game)
        {
            if (Blurbs.TryGetValue(game, out string exact)) return exact;
            string lower = game.ToLowerInvariant();
            foreach (var pair in Blurbs)
                if (pair.Key.ToLowerInvariant() == lower) return pair.Value;
            return "";
        }
    }

    /// <summary>The Filipino street game of every month (port of larong_kalye.gd). Text only.</summary>
    [Serializable]
    public sealed class LarongKalye
    {
        public static readonly string[] MonthNames =
            { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };

        /// <summary>Each game: month, name, also, players_min, players_max, time, place, items, goal, setup, how, rules, safety, quick_*.</summary>
        public List<OrderedDict<string, object>> Games = new List<OrderedDict<string, object>>();

        /// <summary>The game of `month` (1 to 12), or an empty dictionary when there is none.</summary>
        public OrderedDict<string, object> ForMonth(int month)
        {
            foreach (var game in Games)
                if (game.TryGetValue("month", out object m) && (int)MiniJson.Num(m) == month) return game;
            return new OrderedDict<string, object>();
        }

        public static string MonthName(int month) => MonthNames[GMath.Clampi(month, 1, 12) - 1];
    }
}
