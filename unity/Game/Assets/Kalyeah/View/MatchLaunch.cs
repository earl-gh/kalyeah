using System;
using Kalyeah.Net;
using Kalyeah.Sim;
using UnityEngine.SceneManagement;

namespace Kalyeah.Game
{
    /// <summary>
    /// How the menus start a match and hear how it ended. Set up a match with one of the Start calls (each loads the Match scene; MatchBoot
    /// reads what was set here), and optionally fill `OnFinished` / `OnLeft` to write the history. Without a Start call (the Match scene
    /// opened on its own) it is a Practice match of Agawang Base against bots.
    /// </summary>
    public static class MatchLaunch
    {
        /// <summary>What the next match is (null: Practice, Agawang Base, defaults).</summary>
        public static MatchOptions Options;
        /// <summary>Online: the lobby's client, already in the match (client.InMatch()), and the data it was made from.</summary>
        public static GameClient Client;
        public static SimData ClientData;
        /// <summary>Where Leave, Home and Return home go (the menus' scene).</summary>
        public static string ReturnScene = TitleScreen.SceneName;
        /// <summary>The match is over: the results (once per match). For the history and the rank.</summary>
        public static Action<ResultInfo> OnFinished;
        /// <summary>You left before the end; true when it counts as a loss (a Practice match left after 15 s).</summary>
        public static Action<bool> OnLeft;

        /// <summary>Practice: Agawang Base, you and four bots against five bots (options.StreetGame is ignored).</summary>
        public static void StartPractice(MatchOptions options)
        {
            options ??= new MatchOptions();
            options.StreetGame = null;
            Begin(options, null, null);
        }

        /// <summary>Practice: a street game against bots (tumbang_preso, patintero, langit_lupa).</summary>
        public static void StartStreetGame(string gameId, MatchOptions options)
        {
            options ??= new MatchOptions();
            options.StreetGame = gameId;
            Begin(options, null, null);
        }

        /// <summary>
        /// Online or a hotspot room: call once the server started the match (client.InMatch(), e.g. on client.MatchLoading). Keep polling the
        /// client until then; from here MatchBoot polls it. `data` is the SimData the client was made from (SimBridge.LoadSimData).
        /// </summary>
        public static void StartOnline(GameClient client, SimData data, MatchOptions options)
        {
            options ??= new MatchOptions();
            if (string.IsNullOrEmpty(options.ModeText) || options.ModeText == "Practice") options.ModeText = "Online";
            Begin(options, client ?? throw new ArgumentNullException(nameof(client)), data ?? throw new ArgumentNullException(nameof(data)));
        }

        static void Begin(MatchOptions options, GameClient client, SimData data)
        {
            Options = options;
            Client = client;
            ClientData = data;
            SceneManager.LoadScene(MatchBoot.SceneName);
        }

        /// <summary>The bridge for the match set up here (a fresh seed offline).</summary>
        internal static SimBridge CreateBridge(DataSource data)
        {
            MatchOptions options = Options ?? new MatchOptions();
            if (Client != null && ClientData != null) return SimBridge.Online(ClientData, Client, options);
            options.Seed = DateTime.UtcNow.Ticks;
            return SimBridge.Practice(data, options);
        }
    }
}
