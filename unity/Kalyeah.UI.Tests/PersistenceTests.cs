using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Kalyeah.UI.Tests
{
    public class ConfigStoreTests
    {
        [Test]
        public void ValuesSurviveAWriteAndARead()
        {
            string path = Path.Combine(UiKit.Scratch(), "a.json");
            var store = new ConfigStore(path);
            store.Set("s", "flag", true);
            store.Set("s", "count", 7);
            store.Set("s", "share", 0.75);
            store.Set("s", "whole", 60.0);
            store.Set("t", "name", "Ñino \"quoted\"\n");
            store.Set("t", "list", new List<string> { "a", "b" });
            Assert.IsTrue(store.Save());

            var back = new ConfigStore(path);
            Assert.AreEqual(true, back.GetBool("s", "flag", false));
            Assert.AreEqual(7.0, back.GetNumber("s", "count"));
            Assert.AreEqual(0.75, back.GetNumber("s", "share"));
            Assert.AreEqual(60.0, back.GetNumber("s", "whole"));
            Assert.IsInstanceOf<double>(back.Get("s", "whole"), "a decimal stays a decimal");
            Assert.AreEqual("Ñino \"quoted\"\n", back.GetString("t", "name", ""));
            CollectionAssert.AreEqual(new object[] { "a", "b" }, back.GetList("t", "list"));
        }

        [Test]
        public void ABrokenOrMissingFileReadsAsEmpty()
        {
            string dir = UiKit.Scratch();
            string broken = Path.Combine(dir, "broken.json");
            File.WriteAllText(broken, "{\"s\": {\"x\": tru");
            Assert.IsFalse(new ConfigStore(broken).Has("s", "x"));
            Assert.IsFalse(new ConfigStore(Path.Combine(dir, "none.json")).Has("s", "x"));
            Assert.AreEqual("d", new ConfigStore(null).GetString("s", "x", "d"));
        }
    }

    public class SettingsModelTests
    {
        [Test]
        public void DefaultsMatchSettingsGd()
        {
            var settings = new SettingsModel(new ConfigStore(null));
            Assert.IsTrue(settings.Vibration);
            Assert.AreEqual(60, settings.FrameRate);
            Assert.AreEqual("standard", settings.Performance);
            Assert.IsTrue(settings.ScreenShake);
            Assert.IsFalse(settings.ReducedMotion);
            Assert.IsFalse(settings.Colorblind);
            Assert.IsFalse(settings.LeftHanded);
            Assert.AreEqual("bottom", settings.CardsTray);
            Assert.AreEqual("en", settings.Language);
            Assert.IsTrue(settings.AimGuide);
            Assert.IsFalse(settings.TiltToRun);
            Assert.IsTrue(settings.Sounds);
            Assert.IsTrue(settings.Music);
            Assert.AreEqual(1.0, settings.ControlScale);
            Assert.AreEqual("", settings.BotLevel);
        }

        [Test]
        public void EverySettingIsSavedUnderItsGodotSectionAndKey()
        {
            string path = Path.Combine(UiKit.Scratch(), SettingsModel.FileName);
            var settings = new SettingsModel(new ConfigStore(path));
            var changes = new List<SettingsModel.Change>();
            settings.Changed += changes.Add;
            settings.SetColorblind(true);
            settings.SetReducedMotion(true);
            settings.SetSounds(false);
            settings.SetMusic(false);
            settings.SetFrameRate(30);
            settings.SetPerformance("turbo");
            settings.SetScreenShake(false);
            settings.SetLeftHanded(true);
            settings.SetCardsTray("sides");
            settings.SetControlScale(1.3);
            settings.SetAimGuide(false);
            settings.SetTiltToRun(true);
            settings.SetVibration(false);
            settings.SetBotLevel("hard");
            CollectionAssert.Contains(changes, SettingsModel.Change.Accessibility);
            CollectionAssert.Contains(changes, SettingsModel.Change.Audio);
            CollectionAssert.Contains(changes, SettingsModel.Change.Graphics);

            var raw = new ConfigStore(path);
            Assert.AreEqual(true, raw.GetBool("accessibility", "colorblind", false));
            Assert.AreEqual(true, raw.GetBool("accessibility", "reduced_motion", false));
            Assert.AreEqual(false, raw.GetBool("audio", "sounds", true));
            Assert.AreEqual(false, raw.GetBool("audio", "music", true));
            Assert.AreEqual(30.0, raw.GetNumber("graphics", "frame_rate"));
            Assert.AreEqual("turbo", raw.GetString("graphics", "performance", ""));
            Assert.AreEqual(false, raw.GetBool("graphics", "screen_shake", true));
            Assert.AreEqual(true, raw.GetBool("input", "left_handed", false));
            Assert.AreEqual("sides", raw.GetString("input", "cards_tray", ""));
            Assert.AreEqual(1.2, raw.GetNumber("input", "control_scale"));
            Assert.AreEqual(false, raw.GetBool("input", "aim_guide", true));
            Assert.AreEqual(true, raw.GetBool("input", "tilt_to_run", false));
            Assert.AreEqual(false, raw.GetBool("input", "vibration", true));
            Assert.AreEqual("hard", raw.GetString("practice", "bot_level", ""));

            var again = new SettingsModel(new ConfigStore(path));
            Assert.IsTrue(again.Colorblind);
            Assert.AreEqual(30, again.FrameRate);
            Assert.AreEqual("turbo", again.Performance);
            Assert.AreEqual(0.75, again.RenderScale);
            Assert.IsFalse(again.ShakeAllowed);
            Assert.AreEqual(1.2, again.ControlScale);
            Assert.AreEqual("hard", again.BotLevel);
        }

        [Test]
        public void AHandEditedFileIsClamped()
        {
            var store = new ConfigStore(null);
            store.Set("graphics", "frame_rate", 47);
            store.Set("graphics", "performance", "ultra");
            store.Set("input", "control_scale", 0.9);
            store.Set("input", "cards_tray", "top");
            store.Set("ui", "language", "xx");
            store.Set("audio", "sounds", "yes");
            var settings = new SettingsModel(store);
            Assert.AreEqual(60, settings.FrameRate, "47 is nearer 60");
            Assert.AreEqual("standard", settings.Performance);
            Assert.AreEqual(0.85, settings.ControlScale);
            Assert.AreEqual("bottom", settings.CardsTray);
            Assert.AreEqual("en", settings.Language);
            Assert.IsTrue(settings.Sounds, "not a bool: the default");
        }

        [Test]
        public void UnchangedValuesRaiseNothingAndUnknownChoicesAreRefused()
        {
            var settings = new SettingsModel(new ConfigStore(null));
            int changes = 0;
            settings.Changed += _ => changes++;
            settings.SetSounds(true);
            settings.SetPerformance("ultra");
            settings.SetCardsTray("top");
            settings.SetLanguage("fil");
            Assert.AreEqual(0, changes);
            Assert.AreEqual("en", settings.Language, "Filipino is offered only once its translation is there");
        }

        [TestCase("", "")]
        [TestCase("kalyeah.example.com", "wss://kalyeah.example.com")]
        [TestCase("https://kalyeah.example.com", "wss://kalyeah.example.com")]
        [TestCase("localhost:8080", "ws://localhost:8080")]
        [TestCase("192.168.43.1:8080", "ws://192.168.43.1:8080")]
        [TestCase("172.20.10.1:8080", "ws://172.20.10.1:8080")]
        [TestCase("172.40.0.1", "wss://172.40.0.1")]
        [TestCase("ws://a", "ws://a")]
        public void NormalizeUrlMatchesTheGodotGame(string raw, string expected) => Assert.AreEqual(expected, SettingsModel.NormalizeUrl(raw));
    }

    public class SessionModelTests
    {
        [Test]
        public void NamesAreCleanedLikeSessionGd()
        {
            Assert.AreEqual("Ñino Dela Cr", SessionModel.CleanName("  Ñino   Dela Cruz  "), "single spaces, then cut to 12");
            Assert.AreEqual("Ñino Dela", SessionModel.CleanName("Ñino   Dela   "));
            Assert.AreEqual("abcdefghijkl", SessionModel.CleanName("abcdefghijklmnop"));
            Assert.AreEqual("Ana_O'Neil.2", SessionModel.CleanName("Ana_O'Neil.2!@#"));
            Assert.AreEqual("", SessionModel.CleanName("💥💥"));
            Assert.AreEqual("a b", SessionModel.FilterName("a    b"));
        }

        [Test]
        public void ANewPlayerGetsGaboAndAKidAndItIsSaved()
        {
            string path = Path.Combine(UiKit.Scratch(), SessionModel.FileName);
            var session = new SessionModel(new ConfigStore(path));
            session.EnsureProfile(n => n - 1);
            Assert.AreEqual(SessionModel.DefaultName, session.PlayerName);
            Assert.AreEqual("tala", session.AvatarId);
            session.SetPracticeGame("patintero");
            session.RememberRoom("ABCDEFGH", "tok", "wss://x");
            session.RememberLoadout(new[] { "bola", "jacks" });

            var back = new SessionModel(new ConfigStore(path));
            Assert.AreEqual("GABO", back.PlayerName);
            Assert.AreEqual("tala", back.AvatarId);
            Assert.AreEqual("patintero", back.PracticeGame);
            Assert.AreEqual("tok", back.ReconnectToken);
            Assert.AreEqual("wss://x", back.ReconnectUrl);
            CollectionAssert.AreEqual(new[] { "bola", "jacks" }, back.SavedLoadout);
            Assert.AreEqual("", back.RoomCode, "the room code is not saved (as in session.gd)");
            CollectionAssert.AreEqual(new[] { "jacks" }, back.LoadoutFor(new List<string> { "jacks", "lata" }));
        }

        [Test]
        public void BadSavedValuesFallBack()
        {
            var store = new ConfigStore(null);
            store.Set("player", "practice_game", "chess");
            store.Set("player", "name", "  <b>Bo</b>  ");
            var session = new SessionModel(store);
            Assert.AreEqual("street", session.PracticeGame);
            Assert.AreEqual("bBob", session.PlayerName);
            session.SetAvatar("nobody");
            Assert.AreEqual("", session.AvatarId);
            Assert.IsFalse(session.Rename("   "));
        }

        [Test]
        public void ALoadoutNeedsTwoOrThreeDifferentWeapons()
        {
            var session = new SessionModel(new ConfigStore(null));
            session.RememberLoadout(new[] { "bola" });
            session.RememberLoadout(new[] { "bola", "bola" });
            Assert.IsEmpty(session.SavedLoadout);
            session.RememberLoadout(new[] { "bola", "jacks", "lata" });
            Assert.AreEqual(3, session.SavedLoadout.Count);
        }
    }

    public class MatchHistoryTests
    {
        [Test]
        public void KeepsTheLastTwentyNewestFirst()
        {
            var history = new MatchHistoryStore(new ConfigStore(Path.Combine(UiKit.Scratch(), MatchHistoryStore.FileName)));
            for (int i = 0; i < 25; i++) history.Record(i % 2 == 0 ? "practice" : "online", i >= 22, i, 3, 1000 + i);
            List<HistoryEntry> entries = history.Entries();
            Assert.AreEqual(MatchHistoryStore.MaxEntries, entries.Count);
            Assert.AreEqual(24, entries[0].Mine);
            Assert.AreEqual(1024, entries[0].At);
            MatchHistoryStore.Summary summary = MatchHistoryStore.Summarise(entries);
            Assert.AreEqual(20, summary.Played);
            Assert.AreEqual(3, summary.Wins);
            Assert.AreEqual(15, summary.Rate);
            Assert.AreEqual(3, summary.Streak);
            Assert.AreEqual(10, MatchHistoryStore.OfMode(entries, "online").Count);
            history.Clear();
            Assert.IsEmpty(history.Entries());
        }

        [TestCase(0, "just now")]
        [TestCase(59, "just now")]
        [TestCase(300, "5 min ago")]
        [TestCase(3 * 3600 + 5, "3 h ago")]
        [TestCase(2 * 86400 + 5, "2 days ago")]
        public void Ago(long seconds, string expected) => Assert.AreEqual(expected, MatchHistoryStore.Ago(1000, 1000 + seconds));
    }
}
