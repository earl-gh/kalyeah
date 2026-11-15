using System;
using System.Collections.Generic;
using Kalyeah.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.UI
{
    /// <summary>The shared frame of a page: a dark glass panel with a heading, and a scrolling body.</summary>
    public static class PageFrame
    {
        public static RectTransform Build(RectTransform root, string heading, out RectTransform body, float spacing = 18f)
        {
            Image glass = MenuKit.Box("Glass", root, MenuKit.Glass, raycast: true);
            MenuKit.Stretch(glass.rectTransform);
            Text title = MenuKit.Title("Heading", root, heading, MenuKit.Headline + 6, MenuKit.Gold, TextAnchor.MiddleLeft);
            MenuKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -12f), new Vector2(800f, 64f));
            RectTransform area = MenuKit.Stretch(MenuKit.Rect("Area", root), 24f, 20f, 24f, 84f);
            body = MenuKit.Scroll("Scroll", area, spacing, 8);
            return area;
        }

        /// <summary>A setting row: the name and a hint at the left, the control at the right (UiKit.setting_row).</summary>
        public static RectTransform SettingRow(Transform parent, string name, string hint, out RectTransform control)
        {
            RectTransform row = MenuKit.Rect("Row_" + name, parent);
            MenuKit.Size(row, -1f, 88f);
            Text label = MenuKit.Label("Name", row, name, MenuKit.Body, MenuKit.Cream, TextAnchor.UpperLeft, display: true);
            MenuKit.Stretch(label.rectTransform, 8f, 40f, 360f, 6f);
            Text small = MenuKit.Label("Hint", row, hint, MenuKit.Caption, MenuKit.Muted, TextAnchor.LowerLeft);
            MenuKit.Stretch(small.rectTransform, 8f, 6f, 360f, 46f);
            control = MenuKit.Place(MenuKit.Rect("Control", row), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(340f, 72f));
            return row;
        }

        public static Text Section(Transform parent, string name)
        {
            Text label = MenuKit.Title("Section_" + name, parent, name.ToUpperInvariant(), MenuKit.Detail + 2, MenuKit.Amber, TextAnchor.LowerLeft);
            MenuKit.Size(label, -1f, 44f);
            return label;
        }
    }

    /// <summary>
    /// The Settings page (settings_page.gd): Accessibility (Colour-blind teams, Reduce motion), Controls (Vibration, Left-handed, the
    /// card tray, control size, aim guide, tilt to run), Audio (Sounds, Music), Language (only when a second one is ready) and Graphics
    /// (Frame rate, Performance, Screen shake), then Quit game. No server address (the online server comes with the game). Every switch
    /// saves at once. Also used inside the room's Settings dialog (`Fill` without Quit).
    /// </summary>
    public static class SettingsPage
    {
        public static RectTransform Build(RectTransform root, Action quit)
        {
            PageFrame.Build(root, "Settings", out RectTransform body);
            Fill(body, quit);
            return root;
        }

        public static void Fill(RectTransform body, Action quit)
        {
            SettingsModel s = MenuServices.Settings;
            PageFrame.Section(body, "Accessibility");
            Toggle(body, "Colour-blind teams", "Blue and orange teams", s.Colorblind, s.SetColorblind);
            Toggle(body, "Reduce motion", "Fewer pops, no shake", s.ReducedMotion, s.SetReducedMotion);

            PageFrame.Section(body, "Controls");
            if (Application.platform == RuntimePlatform.Android)
                Toggle(body, "Vibration", "Buzz on buttons and when you are hit", s.Vibration, s.SetVibration);
            Toggle(body, "Left-handed", "The stick on the right, the cards on the left", s.LeftHanded, s.SetLeftHanded);
            Choice(body, "Card tray", "Where the cards sit in a match", new[] { ("bottom", "Bottom"), ("sides", "Sides") }, s.CardsTray, s.SetCardsTray);
            Choice(body, "Control size", "The stick and the cards", new[] { ("small", "Small"), ("normal", "Normal"), ("large", "Large") },
                SizeName(s.ControlScale), id => s.SetControlScale(SettingsModel.ControlSizes[id]));
            Toggle(body, "Aim guide", "Show where a card will land", s.AimGuide, s.SetAimGuide);
            Toggle(body, "Tilt to run", "Lean the phone the way to run", s.TiltToRun, s.SetTiltToRun);

            PageFrame.Section(body, "Audio");
            Toggle(body, "Sounds", "Hits, cards and buttons", s.Sounds, s.SetSounds);
            Toggle(body, "Music", "The match's music", s.Music, s.SetMusic);

            if (s.AvailableLanguages.Count > 1)
            {
                PageFrame.Section(body, "Language");
                var options = new List<(string, string)>();
                foreach (string code in s.AvailableLanguages) options.Add((code, SettingsModel.Languages[code]));
                Choice(body, "Language", "The words in the game", options, s.Language, s.SetLanguage);
            }

            PageFrame.Section(body, "Graphics");
            Choice(body, "Frame rate", "30 saves battery, 60 is smoother", new[] { ("30", "30"), ("60", "60") }, s.FrameRate.ToString(),
                id => s.SetFrameRate(int.Parse(id)));
            Choice(body, "Performance", "Turbo helps a slower phone keep up", new[] { ("standard", "Standard"), ("turbo", "Turbo") }, s.Performance,
                s.SetPerformance);
            Toggle(body, "Screen shake", "The street shakes when you are hit", s.ScreenShake, s.SetScreenShake);

            if (quit == null) return;
            Button leave = MenuKit.Button("QuitButton", body, "Quit game", "red", () => quit());
            MenuKit.Size(leave, -1f, 88f);
        }

        static string SizeName(double scale)
        {
            foreach (var pair in SettingsModel.ControlSizes)
                if (Math.Abs(pair.Value - scale) < 1e-6) return pair.Key;
            return "normal";
        }

        static void Toggle(Transform body, string name, string hint, bool on, Action<bool> set)
        {
            PageFrame.SettingRow(body, name, hint, out RectTransform control);
            var toggle = new MenuKit.Switch(control, on, set);
            MenuKit.Place((RectTransform)toggle.Button.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(112f, 64f));
        }

        static void Choice(Transform body, string name, string hint, IList<(string, string)> options, string selected, Action<string> set)
        {
            PageFrame.SettingRow(body, name, hint, out RectTransform control);
            var choice = new MenuKit.Segmented(control, options, selected, set);
            MenuKit.Stretch(choice.Root);
        }
    }

    /// <summary>
    /// The kids (the main screen's MGA BATA, and the kid pick of the Practice row): one kid at a time, big, with the arrows to go
    /// through the ten, the kid's name, street epithet, archetype and look, and Choose. Kids are cosmetic only: choosing one never
    /// changes how you play.
    /// </summary>
    public sealed class KidsPage
    {
        readonly RectTransform _root;
        readonly Action<string> _chosen;
        readonly RawImage _portrait;
        readonly Text _name;
        readonly Text _epithet;
        readonly Text _about;
        readonly Button _choose;
        int _index;

        public KidsPage(RectTransform root, string heading, Action<string> chosen)
        {
            _root = root;
            _chosen = chosen;
            Image glass = MenuKit.Box("Glass", root, MenuKit.Glass, raycast: true);
            MenuKit.Stretch(glass.rectTransform);
            Text title = MenuKit.Title("Heading", root, heading, MenuKit.Headline + 6, MenuKit.Gold, TextAnchor.MiddleLeft);
            MenuKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -12f), new Vector2(800f, 64f));

            RectTransform frame = MenuKit.Place(MenuKit.Rect("PortraitFrame", root), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, -20f),
                new Vector2(330f, 440f));
            Image ring = MenuKit.Box("Ring", frame, MenuKit.Cream, trim: true);
            MenuKit.Stretch(ring.rectTransform);
            _portrait = MenuKit.Art("Portrait", frame, null);
            MenuKit.Stretch(_portrait.rectTransform, 10f, 10f, 10f, 10f);

            Button left = MenuKit.Button("Previous", root, "<", "blue", () => Step(-1), MenuKit.Display);
            MenuKit.Place((RectTransform)left.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, -20f), new Vector2(80f, 120f));
            Button right = MenuKit.Button("Next", root, ">", "blue", () => Step(1), MenuKit.Display);
            MenuKit.Place((RectTransform)right.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(450f, -20f), new Vector2(80f, 120f));

            RectTransform side = MenuKit.Stretch(MenuKit.Rect("Side", root), 560f, 130f, 32f, 90f);
            MenuKit.Column(side, 10f, 0, TextAnchor.UpperLeft);
            _name = MenuKit.Title("Name", side, "", MenuKit.Display, MenuKit.Cream, TextAnchor.MiddleLeft);
            MenuKit.Size(_name, -1f, 70f);
            _epithet = MenuKit.Label("Epithet", side, "", MenuKit.Body, MenuKit.Amber, TextAnchor.MiddleLeft, display: true);
            MenuKit.Size(_epithet, -1f, 44f);
            _about = MenuKit.Label("About", side, "", MenuKit.Detail, MenuKit.Muted, TextAnchor.UpperLeft, wrap: true);

            _choose = MenuKit.Button("Choose", root, "Choose", "orange", Choose, MenuKit.Headline);
            MenuKit.Place((RectTransform)_choose.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 28f), new Vector2(320f, 92f));
            _index = Math.Max(0, Array.IndexOf(SessionModel.Avatars, MenuServices.Session.AvatarId));
            Refresh();
        }

        public RectTransform Root => _root;

        void Step(int way)
        {
            int count = SessionModel.Avatars.Length;
            _index = ((_index + way) % count + count) % count;
            Refresh();
        }

        void Refresh()
        {
            string id = SessionModel.Avatars[_index];
            Texture2D art = MenuKit.Picture("portraits/" + id);
            _portrait.texture = art;
            _portrait.enabled = art != null;
            if (art != null) _portrait.GetComponent<AspectRatioFitter>().aspectRatio = (float)art.width / Mathf.Max(1, art.height);
            _name.text = MenuServices.Data.KidName(id).ToUpperInvariant();
            MenuCatalog.KidMeta meta = MenuCatalog.Kids[id];
            _epithet.text = meta.Epithet + " · " + meta.Role;
            _epithet.color = MenuKit.Hex(meta.Accent);
            _about.text = Description(id);
            bool current = id == MenuServices.Session.AvatarId;
            MenuKit.Restyle(_choose, current ? "grey" : "orange", current ? "Your kid" : "Choose");
        }

        static string Description(string id)
        {
            string source = "res://data/characters/" + id + ".tres";
            foreach (string known in MenuServices.Data.Data.Sources)
                if (known == source) return MenuServices.Data.Data.Get<CharacterDef>(source).Description;
            return "";
        }

        void Choose()
        {
            string id = SessionModel.Avatars[_index];
            MenuServices.Session.SetAvatar(id);
            Refresh();
            _chosen?.Invoke(id);
        }
    }

    /// <summary>
    /// The Book (cards_hub.gd / codex_page.gd): every weapon card in the deck, each with its name, kind, elixir cost, the street game
    /// it comes from and what it does. Everyone plays every card (the deck cycles); the Book only explains them.
    /// </summary>
    public static class CardsPage
    {
        public static RectTransform Build(RectTransform root)
        {
            PageFrame.Build(root, "The Book", out RectTransform body, 14f);
            Text intro = MenuKit.Label("Intro", body, "Every card is in your deck. Four are in your hand; the next one comes in as you play.",
                MenuKit.Detail, MenuKit.Muted, TextAnchor.MiddleLeft, wrap: true);
            MenuKit.Size(intro, -1f, 56f);
            foreach (WeaponDef card in MenuServices.Data.Cards()) Card(body, card);
            return root;
        }

        static void Card(Transform body, WeaponDef card)
        {
            Image plate = MenuKit.Box("Card_" + card.Id, body, new Color(1f, 1f, 1f, 0.07f));
            MenuKit.Size(plate, -1f, 132f);
            RectTransform rect = plate.rectTransform;
            Image cost = MenuKit.Disc("Elixir", rect, MenuKit.Hex("b65cf0"));
            MenuKit.Place(cost.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(72f, 72f));
            Text number = MenuKit.Title("Cost", cost.rectTransform, card.ElixirCost.ToString(), MenuKit.Headline, MenuKit.Cream, TextAnchor.MiddleCenter);
            MenuKit.Stretch(number.rectTransform);
            string name = card.ShortName.Length > 0 ? card.ShortName : card.DisplayName;
            Text title = MenuKit.Title("Name", rect, name, MenuKit.Body + 2, MenuKit.Cream, TextAnchor.UpperLeft);
            MenuKit.Stretch(title.rectTransform, 110f, 80f, 260f, 12f);
            Text kind = MenuKit.Label("Kind", rect, card.KindLabel().ToUpperInvariant() + (card.StreetGame.Length > 0 ? " · " + card.StreetGame : ""),
                MenuKit.Caption, KindColor(card), TextAnchor.UpperRight, display: true);
            MenuKit.Stretch(kind.rectTransform, 700f, 80f, 18f, 14f);
            Text about = MenuKit.Label("About", rect, card.Description, MenuKit.Caption + 1, MenuKit.Muted, TextAnchor.UpperLeft, wrap: true);
            MenuKit.Stretch(about.rectTransform, 110f, 8f, 18f, 52f);
        }

        static Color KindColor(WeaponDef card)
        {
            switch (card.KindLabel())
            {
                case "Attack": return MenuKit.Coral;
                case "Control": return MenuKit.Blue;
                case "Heal": return MenuKit.Jade;
                default: return MenuKit.Amber;
            }
        }
    }

    /// <summary>
    /// History (history_page.gd, the rail's RANGGO): the battles played on this phone, newest first, with how many were won, the win
    /// rate and the streak, filtered by mode. Nothing leaves the phone.
    /// </summary>
    public sealed class HistoryPage
    {
        readonly RectTransform _list;
        readonly Text _summary;
        string _mode = "";

        public HistoryPage(RectTransform root)
        {
            PageFrame.Build(root, "History", out RectTransform body, 10f);
            _summary = MenuKit.Label("Summary", body, "", MenuKit.Body, MenuKit.Cream, TextAnchor.MiddleLeft, display: true);
            MenuKit.Size(_summary, -1f, 52f);
            var filter = new MenuKit.Segmented(body, new[] { ("", "All"), ("practice", "Practice"), ("hotspot", "Local"), ("online", "Online") }, "",
                id =>
                {
                    _mode = id;
                    Refresh();
                });
            MenuKit.Size(filter.Root, -1f, 64f);
            _list = MenuKit.Rect("Battles", body);
            MenuKit.Column(_list, 8f);
            Refresh();
        }

        void Refresh()
        {
            MenuKit.Clear(_list);
            List<HistoryEntry> all = MenuServices.History.Entries();
            List<HistoryEntry> shown = MatchHistoryStore.OfMode(all, _mode);
            MatchHistoryStore.Summary summary = MatchHistoryStore.Summarise(shown);
            _summary.text = summary.Played == 0 ? "No battles yet. Go play!"
                : summary.Played + " played · " + summary.Wins + " won · " + summary.Rate + "% · streak " + summary.Streak;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (HistoryEntry entry in shown)
            {
                Image row = MenuKit.Box("Battle", _list, new Color(1f, 1f, 1f, 0.07f));
                MenuKit.Size(row, -1f, 76f);
                Text result = MenuKit.Title("Result", row.rectTransform, entry.Won ? "VICTORY" : "DEFEAT", MenuKit.Body, entry.Won ? MenuKit.Jade : MenuKit.Coral);
                MenuKit.Stretch(result.rectTransform, 20f, 0f, 700f, 0f);
                Text score = MenuKit.Title("Score", row.rectTransform, entry.Mine + " - " + entry.Theirs, MenuKit.Body, MenuKit.Cream, TextAnchor.MiddleCenter);
                MenuKit.Stretch(score.rectTransform, 260f, 0f, 420f, 0f);
                Text mode = MenuKit.Label("Mode", row.rectTransform, MatchHistoryStore.ModeName(entry.Mode) + " · " + MatchHistoryStore.Ago(entry.At, now),
                    MenuKit.Detail, MenuKit.Muted, TextAnchor.MiddleRight);
                MenuKit.Stretch(mode.rectTransform, 500f, 0f, 20f, 0f);
            }
        }
    }
}
