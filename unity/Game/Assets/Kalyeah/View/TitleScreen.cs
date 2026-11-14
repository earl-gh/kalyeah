using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// A first-pass title in landscape: the studio's boot icon, the game's name, the ten kids' portraits and one Play button (a 5v5
    /// practice match). The Godot game's menus (cards, games, history, settings, lobby) are not ported yet. Back asks nothing yet: it
    /// leaves the app (the Godot title asks "Quit game?" first; to port).
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        public const string SceneName = "Title";
        static readonly Color Sky = new Color(0.98f, 0.70f, 0.36f);
        static readonly Color Dusk = new Color(0.36f, 0.20f, 0.42f);
        static readonly Color PlayColor = new Color(0.98f, 0.78f, 0.18f);

        void Awake()
        {
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Ui.EnsureEventSystem();
            var camera = new GameObject("TitleCamera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(transform, false);
            Build(transform, camera, () => SceneManager.LoadScene(MatchBoot.SceneName));
        }

        void Update()
        {
            if (MatchBoot.BackPressed()) Application.Quit();
        }

        /// <summary>
        /// Builds the title under `parent`, drawn by `camera` (a plain colour behind it). `play` runs when Play is tapped (null: not
        /// clickable, the screenshot).
        /// </summary>
        public static Canvas Build(Transform parent, Camera camera, UnityEngine.Events.UnityAction play)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Dusk;
            camera.orthographic = true;
            Canvas canvas = Ui.MakeCanvas("Title", parent, camera);
            RectTransform full = Ui.Fill("Backdrop", canvas.transform);

            // a warm evening sky: two stacked colours (the owner's key art replaces it)
            Image top = Ui.MakeImage("Sky", full, Sky);
            top.rectTransform.anchorMin = new Vector2(0f, 0.35f);
            top.rectTransform.anchorMax = Vector2.one;
            top.rectTransform.sizeDelta = Vector2.zero;
            Image bottom = Ui.MakeImage("Street", full, Dusk);
            bottom.rectTransform.anchorMin = Vector2.zero;
            bottom.rectTransform.anchorMax = new Vector2(1f, 0.35f);
            bottom.rectTransform.sizeDelta = Vector2.zero;

            RectTransform safe = Ui.Fill("SafeArea", canvas.transform);
            safe.gameObject.AddComponent<SafeArea>().Apply();

            Texture2D icon = KalyeahResources.Texture("ui/boot/boot_icon");
            if (icon != null)
            {
                var go = new GameObject("BootIcon", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(safe, false);
                var image = go.GetComponent<RawImage>();
                image.texture = icon;
                image.raycastTarget = false;
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                image.rectTransform.pivot = new Vector2(0.5f, 1f);
                image.rectTransform.anchoredPosition = new Vector2(0f, -36f);
                float tall = 120f;
                image.rectTransform.sizeDelta = new Vector2(tall * icon.width / Mathf.Max(icon.height, 1), tall);
            }

            Text title = Ui.MakeText("Name", safe, "KALYEAH", 120, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.62f);

            string[] kids = KalyeahResources.Data().CharacterIds();
            const float size = 96f, gap = 10f;
            float x0 = -(kids.Length * size + (kids.Length - 1) * gap) * 0.5f + size * 0.5f;
            for (int i = 0; i < kids.Length; i++)
            {
                Texture2D art = KalyeahResources.Texture("portraits/" + kids[i]);
                Image ring = Ui.MakeImage($"Kid_{kids[i]}", safe, Ui.Cream, round: true);
                RectTransform r = ring.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.36f);
                r.anchoredPosition = new Vector2(x0 + i * (size + gap), 0f);
                r.sizeDelta = Vector2.one * size;
                if (art == null) continue;
                var go = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(r, false);
                var portrait = go.GetComponent<RawImage>();
                portrait.texture = art;
                portrait.raycastTarget = false;
                portrait.rectTransform.anchorMin = Vector2.zero;
                portrait.rectTransform.anchorMax = Vector2.one;
                portrait.rectTransform.sizeDelta = Vector2.one * -8f;
            }

            Image plate = Ui.MakeImage("Play", safe, PlayColor, raycast: play != null);
            RectTransform p = plate.rectTransform;
            p.anchorMin = p.anchorMax = new Vector2(0.5f, 0f);
            p.pivot = new Vector2(0.5f, 0f);
            p.anchoredPosition = new Vector2(0f, 40f);
            p.sizeDelta = new Vector2(320f, 96f);
            Text label = Ui.MakeText("Label", p, "LARO!", 52, new Color(0.18f, 0.10f, 0.04f), TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            label.GetComponent<Outline>().enabled = false;
            if (play != null)
            {
                Button button = plate.gameObject.AddComponent<Button>();
                button.targetGraphic = plate;
                button.onClick.AddListener(play);
            }
            return canvas;
        }
    }
}
