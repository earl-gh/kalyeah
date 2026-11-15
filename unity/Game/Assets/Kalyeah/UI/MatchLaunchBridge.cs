// =====================================================================================================================================
// MatchLaunchBridge: the ONE file where the menus start a match, wired to the match side's real launcher
// (Assets/Kalyeah/View/MatchLaunch.cs, assembly Kalyeah.Game):
//   MatchLaunch.StartPractice(MatchOptions)                 Agawang Base, you and four bots against five
//   MatchLaunch.StartStreetGame(gameId, MatchOptions)       tumbang_preso, patintero, langit_lupa against bots
//   MatchLaunch.StartOnline(GameClient, SimData, MatchOptions)  once the server started the match (client.InMatch())
//   MatchLaunch.OnFinished(ResultInfo) / OnLeft(bool countsAsLoss)  -> the history (MenuServices.History)
// Training (the sandbox with dummies) has no match-side port yet: it opens a Practice match of Agawang Base for now.
// =====================================================================================================================================

using System;
using Kalyeah.Game;
using Kalyeah.Net;

namespace Kalyeah.UI
{
    public static class MatchLaunchBridge
    {
        /// <summary>The Practice game id that means the training sandbox rather than a match.</summary>
        public const string Training = "training";
        /// <summary>The Practice game id of Agawang Base (the street itself); the others are street games.</summary>
        public const string AgawangBase = "street";

        public static void StartPractice(string game, string botLevel, string avatar)
        {
            MatchOptions options = Options(avatar, "Practice");
            options.BotLevel = string.IsNullOrEmpty(botLevel) ? null : botLevel;
            Listen("Practice");
            if (string.IsNullOrEmpty(game) || game == AgawangBase || game == Training)
                MatchLaunch.StartPractice(options);
            else
                MatchLaunch.StartStreetGame(game, options);
        }

        public static void StartOnline(GameClient client)
        {
            string mode = MenuServices.Net != null && MenuServices.Net.IsHosting ? "Local" : "Online";
            MatchOptions options = Options(MenuServices.Session?.AvatarId, mode);
            Listen(mode);
            MatchLaunch.StartOnline(client, MenuServices.Data.Data, options);
        }

        static MatchOptions Options(string avatar, string mode)
        {
            var options = new MatchOptions { ModeText = mode };
            if (!string.IsNullOrEmpty(avatar)) options.Character = avatar;
            string name = MenuServices.Session?.PlayerName;
            if (!string.IsNullOrEmpty(name)) options.PlayerName = name;
            return options;
        }

        /// <summary>The match's end goes into the History page (a Practice match left after 15 s counts as a loss).</summary>
        static void Listen(string mode)
        {
            MatchLaunch.OnFinished = r => MenuServices.History?.Record(mode, r.Won, r.Mine, r.Theirs, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            MatchLaunch.OnLeft = countsAsLoss =>
            {
                if (countsAsLoss) MenuServices.History?.Record(mode, false, 0, 0, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            };
        }
    }
}
