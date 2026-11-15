using System;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.UI
{
    /// <summary>
    /// Quick chat for the match HUD (docs/GDD.md "Quick chat"; quick_chat.gd): a speech-bubble button fixed at the top right, and on a
    /// tap a white rounded panel under it with the six stickers in two columns and three rows. Picking one sends it (Online:
    /// GameClient.Say; the sticker comes back as Said for everyone, you too) and closes the panel; a tap outside closes it. The
    /// stickers' pictures are the owner's .webp files, which Unity does not import, so each shows its label until they are added as
    /// .png. Made for the match side: `QuickChatPanel.Attach(hudRoot, number => client.Say(number))`.
    /// </summary>
    public sealed class QuickChatPanel : MonoBehaviour
    {
        QuickChat _chat;
        RectTransform _panel;
        GameObject _shield;
        Action<int> _send;

        public static QuickChatPanel Attach(RectTransform hud, Action<int> send)
        {
            RectTransform root = MenuKit.Fill("QuickChat", hud);
            var panel = root.gameObject.AddComponent<QuickChatPanel>();
            panel._send = send;
            MenuData data = MenuServices.Data;
            panel._chat = new QuickChat(data != null ? data.Net.ChatMinGap : 1.0, data != null ? data.Net.ChatStickers : QuickChat.Stickers.Length);
            panel.Build(root);
            return panel;
        }

        void Build(RectTransform root)
        {
            Button bubble = MenuKit.Button("Bubble", root, "...", "glass", Toggle, MenuKit.Headline);
            MenuKit.Place((RectTransform)bubble.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -12f), new Vector2(88f, 76f));
            Image shield = MenuKit.Box("Outside", root, new Color(0f, 0f, 0f, 0f), raycast: true, rounded: false);
            MenuKit.Stretch(shield.rectTransform);
            Button outside = shield.gameObject.AddComponent<Button>();
            outside.transition = Selectable.Transition.None;
            outside.onClick.AddListener(Hide);
            _shield = shield.gameObject;
            Image panel = MenuKit.Box("Panel", root, Color.white, raycast: true, trim: true);
            _panel = MenuKit.Place(panel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -96f), new Vector2(380f, 300f));
            var grid = _panel.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(14, 14, 14, 14);
            grid.cellSize = new Vector2(169f, 82f);
            grid.spacing = new Vector2(14f, 9f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            foreach (QuickChat.Sticker sticker in QuickChat.Stickers)
            {
                int number = sticker.Number;
                MenuKit.Button("Sticker_" + sticker.Picture, _panel, sticker.Label, "orange", () => Pick(number), MenuKit.Body);
            }
            Hide();
        }

        void Toggle()
        {
            bool show = !_panel.gameObject.activeSelf;
            _panel.gameObject.SetActive(show);
            _shield.SetActive(show);
        }

        void Hide()
        {
            _panel.gameObject.SetActive(false);
            _shield.SetActive(false);
        }

        void Pick(int number)
        {
            Hide();
            if (_chat.TrySend(number)) _send?.Invoke(number);
        }

        void Update() => _chat?.Tick(Time.unscaledDeltaTime);
    }
}
