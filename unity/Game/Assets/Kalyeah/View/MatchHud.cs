using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// The first-pass match HUD on the 1280 x 720 landscape base: the score and the clock at the top centre, your team's health bars
    /// (yours first) at the top left, the joystick bottom-left and the look pad on the right. The other team's health is never shown
    /// (owner rule). Not ported yet: the ability arc, the crosshair, the minimap-free header extras, the overhead badges.
    /// </summary>
    public sealed class MatchHud
    {
        const float BarWidth = 150f, BarHeight = 12f, RowHeight = 40f, Face = 34f;
        static readonly Color HealthColor = new Color(0.36f, 0.92f, 0.45f);
        static readonly Color MissingColor = new Color(0.10f, 0.12f, 0.18f, 0.85f);

        public readonly Canvas Canvas;
        public readonly VirtualJoystick Joystick;
        public readonly LookPad Look;
        readonly Text _score, _clock;
        readonly Dictionary<int, RectTransform> _fills = new Dictionary<int, RectTransform>();
        readonly int _maxHp;

        /// <param name="camera">Render through this camera (the screenshots); null for an overlay (play).</param>
        public MatchHud(Transform parent, SimBridge sim, Camera camera, bool interactive)
        {
            _maxHp = Mathf.Max(sim.MaxHp, 1);
            Canvas = Ui.MakeCanvas("MatchHud", parent, camera);
            RectTransform safe = Ui.Fill("SafeArea", Canvas.transform);
            safe.gameObject.AddComponent<SafeArea>().Apply();

            if (interactive)
            {
                Look = LookPad.Build(safe);
                Joystick = VirtualJoystick.Build(safe);
            }

            RectTransform header = Ui.Panel("Header", safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(300f, 88f));
            Image plate = Ui.MakeImage("Plate", header, Ui.Navy);
            plate.rectTransform.anchorMin = Vector2.zero;
            plate.rectTransform.anchorMax = Vector2.one;
            plate.rectTransform.sizeDelta = Vector2.zero;
            _score = Ui.MakeText("Score", header, "0 - 0", 40, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            _score.rectTransform.anchoredPosition = new Vector2(0f, 14f);
            _clock = Ui.MakeText("Clock", header, "0:00", 24, Ui.Cream, TextAnchor.MiddleCenter);
            _clock.rectTransform.anchoredPosition = new Vector2(0f, -24f);

            if (sim.UsesStandIn)
            {
                Text note = Ui.MakeText("StandInNote", safe, "Unity port preview: the rules are a stand-in until the C# sim lands", 18,
                    new Color(1f, 1f, 1f, 0.8f), TextAnchor.LowerCenter);
                RectTransform r = note.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
                r.anchoredPosition = new Vector2(0f, 16f);
            }

            RectTransform squad = Ui.Panel("OwnTeam", safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -16f), new Vector2(Face + BarWidth + 16f, RowHeight * 5f));
            int row = 0;
            foreach (KidSnapshot kid in Ordered(sim.Kids))
            {
                if (kid.Team != SimBridge.LocalTeam) continue; // the other team's health is never shown
                AddRow(squad, kid, row++);
            }
        }

        static IEnumerable<KidSnapshot> Ordered(IReadOnlyList<KidSnapshot> kids)
        {
            foreach (KidSnapshot kid in kids) if (kid.Id == SimBridge.LocalId) yield return kid;
            foreach (KidSnapshot kid in kids) if (kid.Id != SimBridge.LocalId) yield return kid;
        }

        void AddRow(RectTransform squad, KidSnapshot kid, int row)
        {
            RectTransform line = Ui.Panel($"Kid_{kid.Id}", squad, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -row * RowHeight), new Vector2(Face + BarWidth + 16f, RowHeight - 4f));
            Image disc = Ui.MakeImage("Ring", line, MatchView.TeamColor(kid.Team), round: true);
            disc.rectTransform.anchorMin = disc.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            disc.rectTransform.pivot = new Vector2(0f, 0.5f);
            disc.rectTransform.sizeDelta = Vector2.one * Face;
            Texture2D faceArt = KalyeahResources.Texture("portraits/faces/" + kid.CharacterId);
            if (faceArt != null)
            {
                var go = new GameObject("Face", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(disc.transform, false);
                var face = go.GetComponent<RawImage>();
                face.texture = faceArt;
                face.raycastTarget = false;
                face.rectTransform.anchorMin = Vector2.zero;
                face.rectTransform.anchorMax = Vector2.one;
                face.rectTransform.sizeDelta = Vector2.one * -4f;
            }
            float scale = kid.Id == SimBridge.LocalId ? 1f : 0.8f;
            Image back = Ui.MakeImage("Bar", line, MissingColor);
            RectTransform bar = back.rectTransform;
            bar.anchorMin = bar.anchorMax = new Vector2(0f, 0.5f);
            bar.pivot = new Vector2(0f, 0.5f);
            bar.anchoredPosition = new Vector2(Face + 8f, 0f);
            bar.sizeDelta = new Vector2(BarWidth * scale, BarHeight);
            Image fill = Ui.MakeImage("Health", bar, HealthColor);
            RectTransform f = fill.rectTransform;
            f.anchorMin = Vector2.zero;
            f.anchorMax = new Vector2(1f, 1f);
            f.pivot = new Vector2(0f, 0.5f);
            f.sizeDelta = Vector2.zero;
            _fills[kid.Id] = f;
        }

        /// <summary>Once a frame: the score, the clock and your team's health.</summary>
        public void Refresh(SimBridge sim)
        {
            _score.text = $"{sim.ScoreOf(SimBridge.LocalTeam)} - {sim.ScoreOf(1 - SimBridge.LocalTeam)}";
            int seconds = Mathf.FloorToInt(sim.ClockSeconds);
            _clock.text = $"{seconds / 60}:{seconds % 60:00}";
            foreach (KidSnapshot kid in sim.Kids)
                if (_fills.TryGetValue(kid.Id, out RectTransform fill))
                    fill.anchorMax = new Vector2(Mathf.Clamp01((float)kid.Hp / _maxHp), 1f);
        }
    }
}
