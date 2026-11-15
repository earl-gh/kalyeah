using System;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.UI
{
    /// <summary>
    /// The loading screen (boot_screen.gd), laid over everything and taking every touch: the studio's boot icon on a matte base, one
    /// tip, and a navy track along the bottom with a glossy blue fill whose percentage straddles its top edge. The bar never fills faster
    /// than LoadingProgress allows, so it is seen even when loading is instant; `done` runs once it is full. Used at launch, before a
    /// Practice match and when a room's match starts. (The Godot boot art is a .webp, which Unity does not import: the matte base and
    /// the icon stand in until the owner's art is added as a .png.)
    /// </summary>
    public sealed class LoadingOverlay : MonoBehaviour
    {
        static readonly Color BaseColor = new Color(0.085f, 0.085f, 0.09f);
        static readonly Color Track = MenuKit.Hex("070f1d");
        static readonly Color FillColor = MenuKit.Hex("4eb8ff");
        const float BandHeight = 44f;

        readonly LoadingProgress _bar = new LoadingProgress();
        RectTransform _fill;
        Text _percent;
        Action _done;
        bool _finished;

        /// <summary>Shows the loading screen; `loaded` reports how much has really loaded (null: nothing to wait for).</summary>
        public static LoadingOverlay Show(Transform parent, Action done, Func<float> loaded = null)
        {
            Canvas canvas = MenuKit.Canvas("LoadingScreen", parent, 1000);
            var overlay = canvas.gameObject.AddComponent<LoadingOverlay>();
            overlay._done = done;
            overlay._loaded = loaded;
            overlay.Build(canvas.transform);
            return overlay;
        }

        Func<float> _loaded;

        void Build(Transform root)
        {
            Image matte = MenuKit.Box("Base", root, BaseColor, raycast: true, rounded: false);
            MenuKit.Stretch(matte.rectTransform);
            RectTransform safe = MenuKit.Fill("SafeArea", root);
            safe.gameObject.AddComponent<MenuSafeArea>().Apply();

            Texture2D art = MenuKit.Picture("ui/boot/boot_icon");
            RectTransform iconSlot = MenuKit.Place(MenuKit.Rect("IconSlot", safe), new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 300f));
            if (art != null) MenuKit.Stretch(MenuKit.Art("BootIcon", iconSlot, art).rectTransform);
            else MenuKit.Stretch(MenuKit.Title("Name", iconSlot, "KALYEAH", 120, MenuKit.Cream, TextAnchor.MiddleCenter).rectTransform);

            Text tip = MenuKit.Label("Tip", safe, LoadingProgress.Tip(MenuServices.Pick), MenuKit.Detail + 2, MenuKit.Cream, TextAnchor.MiddleCenter, wrap: true);
            MenuKit.Place(tip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, BandHeight + 70f), new Vector2(980f, 80f));

            Image band = MenuKit.Box("Track", root, Track, rounded: false);
            RectTransform bandRect = band.rectTransform;
            bandRect.anchorMin = Vector2.zero;
            bandRect.anchorMax = new Vector2(1f, 0f);
            bandRect.pivot = new Vector2(0.5f, 0f);
            bandRect.anchoredPosition = Vector2.zero;
            bandRect.sizeDelta = new Vector2(0f, BandHeight + Mathf.Max(0f, Screen.safeArea.yMin * MenuKit.Base.y / Mathf.Max(1, Screen.height)));
            Image fill = MenuKit.Box("Fill", bandRect, FillColor, rounded: false);
            _fill = fill.rectTransform;
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = new Vector2(0f, 1f);
            _fill.pivot = new Vector2(0f, 0.5f);
            _fill.offsetMin = Vector2.zero;
            _fill.offsetMax = Vector2.zero;
            _percent = MenuKit.Title("Percent", bandRect, "0%", 48, MenuKit.Cream, TextAnchor.MiddleCenter);
            MenuKit.Place(_percent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 64f));
        }

        void Update()
        {
            if (_finished) return;
            _bar.Loaded = _loaded != null ? Mathf.Clamp01(_loaded()) : 1f;
            _bar.Advance(Time.unscaledDeltaTime);
            _fill.anchorMax = new Vector2((float)_bar.Shown, 1f);
            _percent.text = _bar.Percent + "%";
            if (!_bar.Done) return;
            _finished = true;
            _done?.Invoke();
        }

        public void Hide()
        {
            if (this != null) Destroy(gameObject);
        }
    }
}
