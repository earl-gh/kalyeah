using System;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.UI
{
    /// <summary>Where the menus put their dialogs and toasts, and the Back stack they share (set up by MenuRoot).</summary>
    public sealed class MenuHost
    {
        public MenuNavigation Nav = new MenuNavigation();
        /// <summary>The overlay that holds dialogs (above the pages).</summary>
        public RectTransform DialogLayer;
        /// <summary>The overlay that holds toasts (above everything).</summary>
        public RectTransform ToastLayer;

        public void Toast(string text, string kind = "ok") => MenuKit.Toast(ToastLayer, text, kind);
    }

    /// <summary>
    /// A modal card over a dark tint (menu_dialog.gd): a title, a body of lines and controls, a row of buttons and an X. Back closes it
    /// (unless it is sticky); a tap on the tint closes it too. One dialog at a time: opening one closes the one before.
    /// </summary>
    public sealed class MenuDialog
    {
        public const float WidthSmall = 640f;
        public const float WidthSheet = 900f;

        static MenuDialog _current;
        static int _serial;

        public readonly RectTransform Root;
        public readonly RectTransform Card;
        public readonly RectTransform Body;
        public event Action Closed;
        public bool IsOpen { get; private set; } = true;

        readonly MenuHost _host;
        readonly string _id;
        RectTransform _buttons;

        MenuDialog(MenuHost host, string title, float width, bool sticky)
        {
            _host = host;
            _id = "dialog" + (++_serial);
            Root = MenuKit.Fill("Dialog_" + title, host.DialogLayer);
            Image tint = MenuKit.Box("Tint", Root, MenuKit.Tint, raycast: true, rounded: false);
            MenuKit.Stretch(tint.rectTransform);
            if (!sticky)
            {
                Button outside = tint.gameObject.AddComponent<Button>();
                outside.transition = Selectable.Transition.None;
                outside.onClick.AddListener(Close);
            }

            Image card = MenuKit.Box("Card", Root, MenuKit.ModalBody, raycast: true, trim: true);
            Card = MenuKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 200f));
            VerticalLayoutGroup column = MenuKit.Column(Card, 18f, 28);
            column.padding.top = 22;
            var fitter = Card.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform head = MenuKit.Rect("Head", Card);
            MenuKit.Size(head, -1f, 64f);
            Text heading = MenuKit.Title("Title", head, title, MenuKit.Headline, MenuKit.Gold, TextAnchor.MiddleCenter);
            MenuKit.Stretch(heading.rectTransform, 72f, 0f, 72f, 0f);
            if (!sticky)
            {
                Button x = MenuKit.Button("Close", head, "X", "red", Close, MenuKit.Body);
                MenuKit.Place((RectTransform)x.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            }

            Body = MenuKit.Rect("Body", Card);
            MenuKit.Column(Body, 14f);
            host.Nav.Push(MenuNavigation.Kind.Dialog, _id, () => Close(), sticky);
        }

        /// <summary>Opens a dialog titled `title` (closing the one before). `sticky`: Back and the tint do not close it.</summary>
        public static MenuDialog Open(MenuHost host, string title, float width = WidthSmall, bool sticky = false)
        {
            if (_current != null && _current.IsOpen) _current.Close();
            _current = new MenuDialog(host, title, width, sticky);
            return _current;
        }

        public Text AddText(string text, int size = MenuKit.Body, TextAnchor align = TextAnchor.MiddleCenter, Color? color = null)
        {
            Text label = MenuKit.Label("Text", Body, text, size, color ?? MenuKit.ModalText, align, wrap: true);
            return label;
        }

        public RectTransform AddRow(float height, float spacing = 12f)
        {
            RectTransform row = MenuKit.Rect("Row", Body);
            MenuKit.Row(row, spacing);
            MenuKit.Size(row, -1f, height);
            return row;
        }

        /// <summary>An empty block `height` high, its children placed by hand (a scroll view, a list).</summary>
        public RectTransform AddBlock(float height)
        {
            RectTransform block = MenuKit.Rect("Block", Body);
            MenuKit.Size(block, -1f, height);
            return block;
        }

        /// <summary>A button in the dialog's button row (made under the body on first use).</summary>
        public Button AddButton(string text, string tone, Action onClick, int fontSize = MenuKit.Body)
        {
            if (_buttons == null)
            {
                _buttons = MenuKit.Rect("Buttons", Card);
                MenuKit.Row(_buttons, 16f);
                MenuKit.Size(_buttons, -1f, 88f);
            }
            return MenuKit.Button(text, _buttons, text, tone, () => onClick?.Invoke(), fontSize);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _host.Nav.Remove(_id);
            if (Root != null) UnityEngine.Object.Destroy(Root.gameObject);
            if (_current == this) _current = null;
            Closed?.Invoke();
        }

        public static void CloseCurrent()
        {
            if (_current != null) _current.Close();
        }
    }
}
