using System;
using System.Collections.Generic;

namespace Kalyeah.UI
{
    /// <summary>
    /// What the phone's Back button does in the menus (docs/GDD.md "Settings"): it closes the top dialog, then the open page, and on
    /// the home screen it asks to quit; while something is loading, or while a screen is busy (a match is starting), it does nothing.
    /// The Unity side pushes a layer when it opens a dialog or a page and calls `Back` on the button; each layer closes itself.
    /// </summary>
    public sealed class MenuNavigation
    {
        public enum Kind { Page, Dialog }

        public enum BackResult { Nothing, Closed, AskQuit }

        sealed class Layer
        {
            public Kind Kind;
            public string Id;
            public Action Close;
            /// <summary>A layer that ignores Back (the kid pick keeps your kid; a dialog that must be answered).</summary>
            public bool Sticky;
        }

        readonly List<Layer> _layers = new List<Layer>();

        /// <summary>A loading screen is up: Back waits.</summary>
        public bool Busy;

        public int Count => _layers.Count;
        public bool AnyDialog => _layers.Exists(l => l.Kind == Kind.Dialog);
        public bool AnyPage => _layers.Exists(l => l.Kind == Kind.Page);
        public string TopId => _layers.Count > 0 ? _layers[_layers.Count - 1].Id : "";

        /// <summary>The page on screen (the topmost page, "" on the home screen).</summary>
        public string CurrentPage
        {
            get
            {
                for (int i = _layers.Count - 1; i >= 0; i--)
                    if (_layers[i].Kind == Kind.Page) return _layers[i].Id;
                return "";
            }
        }

        /// <summary>Puts a layer on top. A page replaces the page under it (one page at a time); `close` runs when Back closes it.</summary>
        public void Push(Kind kind, string id, Action close, bool sticky = false)
        {
            if (kind == Kind.Page)
                for (int i = _layers.Count - 1; i >= 0; i--)
                    if (_layers[i].Kind == Kind.Page)
                    {
                        Action old = _layers[i].Close;
                        _layers.RemoveAt(i);
                        old?.Invoke();
                    }
            _layers.Add(new Layer { Kind = kind, Id = id, Close = close, Sticky = sticky });
        }

        /// <summary>A layer closed by its own button: forget it (its close action is not run again).</summary>
        public void Remove(string id)
        {
            for (int i = _layers.Count - 1; i >= 0; i--)
                if (_layers[i].Id == id)
                {
                    _layers.RemoveAt(i);
                    return;
                }
        }

        public bool Has(string id) => _layers.Exists(l => l.Id == id);

        /// <summary>The Back button: closes the top layer, or says to ask "Quit game?".</summary>
        public BackResult Back()
        {
            if (Busy) return BackResult.Nothing;
            if (_layers.Count == 0) return BackResult.AskQuit;
            Layer top = _layers[_layers.Count - 1];
            if (top.Sticky) return BackResult.Nothing;
            _layers.RemoveAt(_layers.Count - 1);
            top.Close?.Invoke();
            return BackResult.Closed;
        }

        /// <summary>Closes every layer (going to a match, or back home).</summary>
        public void Clear()
        {
            var all = new List<Layer>(_layers);
            _layers.Clear();
            for (int i = all.Count - 1; i >= 0; i--) all[i].Close?.Invoke();
        }
    }

    /// <summary>
    /// The loading screen's bar (boot_screen.gd): it fills with what has loaded, never faster than `MaxFillPerSecond`, so the screen is
    /// seen even when loading is instant; it is done once it is full.
    /// </summary>
    public sealed class LoadingProgress
    {
        public const double MaxFillPerSecond = 0.7;

        public double Shown { get; private set; }
        /// <summary>How much has really loaded (0..1); the caller sets it.</summary>
        public double Loaded;
        public bool Done => Shown >= 1.0;

        /// <summary>Moves the bar towards `Loaded`; true on the frame it becomes full.</summary>
        public bool Advance(double delta)
        {
            if (Done) return false;
            double target = Math.Max(0.0, Math.Min(1.0, Loaded));
            Shown = Math.Min(target, Shown + MaxFillPerSecond * Math.Max(0.0, delta));
            return Done;
        }

        /// <summary>The whole percent drawn over the bar.</summary>
        public int Percent => (int)Math.Floor(Shown * 100.0 + 1e-9);

        /// <summary>The tip for this load (`pick(n)` is a random 0..n-1 from the caller).</summary>
        public static string Tip(Func<int, int> pick) => MenuCatalog.Tips[Math.Max(0, Math.Min(MenuCatalog.Tips.Length - 1, pick(MenuCatalog.Tips.Length)))];
    }

    /// <summary>
    /// Quick chat's stickers (docs/GDD.md "Quick chat"): six, in a two-column grid; picking one sends its number (GameClient.Say) and
    /// closes the panel. The server takes at most one a `chat_min_gap`; the panel keeps to the same gap so a tap is never dropped
    /// silently.
    /// </summary>
    public sealed class QuickChat
    {
        public sealed class Sticker
        {
            public int Number;
            public string Label;
            /// <summary>The owner's picture under assets/ui/stickers (a .webp: drawn as its label until the art imports in Unity).</summary>
            public string Picture;
        }

        public static readonly Sticker[] Stickers =
        {
            new Sticker { Number = 0, Label = "Haha!", Picture = "haha" },
            new Sticker { Number = 1, Label = "Easy!", Picture = "easy" },
            new Sticker { Number = 2, Label = "Grrr!", Picture = "grrr" },
            new Sticker { Number = 3, Label = "Love it!", Picture = "love_it" },
            new Sticker { Number = 4, Label = "Huhu!", Picture = "huhu" },
            new Sticker { Number = 5, Label = "Yum!", Picture = "yum" },
        };

        /// <summary>How long a sticker floats over its kid.</summary>
        public const double ShowSeconds = 2.0;

        readonly double _minGap;
        readonly int _count;
        double _clock;
        double _lastSent = double.NegativeInfinity;

        public QuickChat(double minGap, int count)
        {
            _minGap = minGap;
            _count = Math.Min(count, Stickers.Length);
        }

        public void Tick(double delta) => _clock += delta;

        /// <summary>True when sticker `number` may go now (inside the set, and the gap has passed); marks it sent.</summary>
        public bool TrySend(int number)
        {
            if (number < 0 || number >= _count) return false;
            if (_clock - _lastSent < _minGap) return false;
            _lastSent = _clock;
            return true;
        }
    }
}
