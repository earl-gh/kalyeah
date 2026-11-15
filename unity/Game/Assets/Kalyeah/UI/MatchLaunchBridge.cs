// =====================================================================================================================================
// MatchLaunchBridge: the ONE file where the menus start a match. Fix the names here, and only here, when the match side lands.
//
// ASSUMED API (owned by the in-match code in Assets/Kalyeah/View, assembly Kalyeah.Game, namespace Kalyeah.Game):
//
//     public static class MatchLauncher
//     {
//         // A Practice match on this phone. `game`: "street" (Agawang Base), "tumbang_preso", "patintero", "langit_lupa", or
//         // "training" (the sandbox with dummies and one sparring partner). `botLevel`: a GameRules.BotLevels id ("easy", "medium",
//         // "hard"; "" = the rules' default). `avatar`: the kid the player plays (one of SessionModel.Avatars; cosmetic only).
//         public static void StartPractice(string game, string botLevel, string avatar);
//
//         // An Online or Local room's match. The client has just raised MatchLoading: its Mirror is built and MatchInfo is set.
//         // The match side owns everything from here: loading the Match scene, calling client.Loaded() once it is up, sending
//         // input every tick, and returning to the Title scene when the match ends (MenuRoot then leaves the room and stops a
//         // Local host by itself). The client keeps being polled by MenuServices (DontDestroyOnLoad), and so does a Local host's
//         // GameServer (MenuServices.Net.Host), so friends keep playing while the scenes change.
//         public static void StartOnline(Kalyeah.Net.GameClient client);
//     }
//
// Also read by the match side (no call from here): MenuServices.Settings (colour-blind, reduced motion, left-handed, card tray,
// control size, aim guide, tilt to run, sounds, music, screen shake), MenuServices.Session (BriefingPending: open the briefing;
// PracticeKind), MenuServices.History.Record(...) when a battle ends, and QuickChatPanel for the quick chat button.
// =====================================================================================================================================

using Kalyeah.Net;

namespace Kalyeah.UI
{
    public static class MatchLaunchBridge
    {
        /// <summary>The Practice game id that means the training sandbox rather than a match.</summary>
        public const string Training = "training";

        public static void StartPractice(string game, string botLevel, string avatar) =>
            Kalyeah.Game.MatchLauncher.StartPractice(game, botLevel, avatar);

        public static void StartOnline(GameClient client) =>
            Kalyeah.Game.MatchLauncher.StartOnline(client);
    }
}
