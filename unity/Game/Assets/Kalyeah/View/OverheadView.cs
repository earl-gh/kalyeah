using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// Over every kid's head (port of scripts/ui/hud/overhead_hud.gd): its number badge in its team's colour, the status words ("Stunned ·
    /// Slowed", "AFK") or else its name (only within `shoulder_name_reach` metres of the camera), and a health bar for you (green) and your
    /// team (blue) only: an enemy's health is never shown (owner rule, 2026-11-13; the taya counts as an enemy). A gold ring over a kid
    /// capturing a base and a green one over a kid rescuing. Groups that would overlap are stacked upward. Nothing shows in a point freeze
    /// or after the match.
    /// </summary>
    public sealed class OverheadView
    {
        const float HeadHeight = KidView.KidHeight + 0.2f;
        static readonly Vector2 BarSize = new Vector2(96f, 18f);
        const float Outline = 2f, BadgeSize = 30f, BadgeOverlap = 6f, LabelLine = 16f, EdgePad = 4f, SpinnerRadius = 14f, SpinnerLift = 34f;
        static readonly Color EmptyBar = new Color(0.18f, 0.19f, 0.21f);
        static readonly Color SoftBorder = new Color(0.10f, 0.11f, 0.14f, 0.62f);
        static readonly Color SelfColor = new Color(0.35f, 0.95f, 0.35f);
        static readonly Color AllyColor = new Color(0.35f, 0.65f, 1f);
        static readonly Color CaptureColor = new Color(1f, 0.86f, 0.3f);
        static readonly Color RescueColor = new Color(0.45f, 0.9f, 0.5f);

        sealed class Head
        {
            public RectTransform Root, Bar, Fill, Badge, Spinner;
            public Image FillImage, BadgeImage, SpinnerFill;
            public Text Number, Label;
        }

        readonly RectTransform _layer;
        readonly float _barScale, _viewScale, _nameReach;
        readonly Dictionary<int, Head> _heads = new Dictionary<int, Head>();
        readonly List<Rect> _placed = new List<Rect>();

        public OverheadView(Transform canvas, CameraRulesData rules)
        {
            _layer = Ui.Fill("Overhead", canvas);
            _barScale = rules.BarScale > 0f ? rules.BarScale : 1f;
            _viewScale = rules.KidViewScale;
            _nameReach = rules.ShoulderNameReach;
        }

        public void SetVisible(bool on) => _layer.gameObject.SetActive(on);

        Head Make(KidSnapshot kid)
        {
            var h = new Head { Root = Ui.Panel($"Head_{kid.Id}", _layer, Vector2.zero, Vector2.zero, new Vector2(0f, 0f), Vector2.zero, Vector2.zero) };
            Vector2 bar = BarSize * _barScale;
            Image back = Ui.MakeImage("Bar", h.Root, SoftBorder);
            h.Bar = back.rectTransform;
            h.Bar.anchorMin = h.Bar.anchorMax = Vector2.zero;
            h.Bar.pivot = new Vector2(0f, 0.5f);
            h.Bar.sizeDelta = bar + Vector2.one * Outline * 2f;
            h.Bar.anchoredPosition = new Vector2(-Outline, 0f);
            Image empty = Ui.MakeImage("Empty", h.Bar, EmptyBar);
            empty.rectTransform.anchorMin = Vector2.zero;
            empty.rectTransform.anchorMax = Vector2.one;
            empty.rectTransform.sizeDelta = Vector2.one * -Outline * 2f;
            h.FillImage = Ui.MakeImage("Fill", empty.rectTransform, AllyColor);
            h.Fill = h.FillImage.rectTransform;
            h.Fill.anchorMin = Vector2.zero;
            h.Fill.anchorMax = Vector2.one;
            h.Fill.pivot = new Vector2(0f, 0.5f);
            h.Fill.sizeDelta = Vector2.zero;

            h.BadgeImage = Ui.MakeImage("Badge", h.Root, MatchView.SideColor(!kid.Enemy));
            h.Badge = h.BadgeImage.rectTransform;
            h.Badge.anchorMin = h.Badge.anchorMax = Vector2.zero;
            h.Badge.sizeDelta = Vector2.one * BadgeSize;
            h.Badge.anchoredPosition = new Vector2(BadgeOverlap - BadgeSize * 0.5f - Outline, 0f);
            h.Number = Ui.MakeText("Number", h.Badge, kid.Number.ToString(), 20, Color.white, TextAnchor.MiddleCenter);

            h.Label = Ui.MakeText("Label", h.Root, "", 18, new Color(1f, 1f, 1f, 0.94f), TextAnchor.LowerLeft);
            RectTransform label = h.Label.rectTransform;
            label.anchorMin = label.anchorMax = Vector2.zero;
            label.pivot = new Vector2(0f, 0f);
            label.anchoredPosition = new Vector2(BadgeOverlap + Outline, bar.y * 0.5f + Outline);

            Image spinner = Ui.MakeImage("Spinner", h.Root, new Color(0.08f, 0.09f, 0.12f, 0.48f), round: true);
            h.Spinner = spinner.rectTransform;
            h.Spinner.anchorMin = h.Spinner.anchorMax = Vector2.zero;
            h.Spinner.sizeDelta = Vector2.one * (SpinnerRadius + 3f) * 2f;
            h.Spinner.anchoredPosition = new Vector2(bar.x * 0.5f, SpinnerLift + LabelLine);
            h.SpinnerFill = Ui.Sweep("Progress", h.Spinner, SpinnerRadius, CaptureColor);
            _heads[kid.Id] = h;
            return h;
        }

        /// <summary>Places every kid's head group on the screen (`camera` draws the street, `canvasCamera` the HUD; null for an overlay).</summary>
        public void Sync(SimBridge sim, IReadOnlyList<KidSnapshot> drawn, Camera camera, Camera canvasCamera)
        {
            bool show = sim.Phase != MatchPhaseView.PointFreeze && sim.Phase != MatchPhaseView.MatchOver;
            SetVisible(show);
            if (!show || camera == null) return;
            _placed.Clear();
            // yours first, so the others stack round it
            foreach (KidSnapshot kid in drawn) if (kid.IsLocal) Place(kid, camera, canvasCamera, sim.MaxHp);
            foreach (KidSnapshot kid in drawn) if (!kid.IsLocal) Place(kid, camera, canvasCamera, sim.MaxHp);
        }

        void Place(KidSnapshot kid, Camera camera, Camera canvasCamera, int maxHp)
        {
            if (!_heads.TryGetValue(kid.Id, out Head h)) h = Make(kid);
            float tall = HeadHeight * _viewScale * (kid.Crouching ? 0.78f : 1f) + kid.AirY;
            Vector3 world = KalyeahResources.World(kid.Position, tall);
            Vector3 screen = camera.WorldToScreenPoint(world);
            bool visible = !kid.Tagged && !kid.Hidden && screen.z > 0f;
            h.Root.gameObject.SetActive(visible);
            if (!visible) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen, canvasCamera, out Vector2 local))
            {
                h.Root.gameObject.SetActive(false);
                return;
            }
            Rect layer = _layer.rect;
            Vector2 bar = BarSize * _barScale;
            float minX = layer.xMin + BadgeSize - BadgeOverlap + EdgePad;
            float x = Mathf.Clamp(local.x - bar.x * 0.5f, minX, Mathf.Max(layer.xMax - bar.x - EdgePad, minX));
            float y = local.y;
            // keep clear of the groups already placed: move up over them (overhead_hud.gd _clear_of_others; y up here)
            var group = new Rect(x + BadgeOverlap - BadgeSize, y - bar.y * 0.5f - Outline, bar.x + BadgeSize - BadgeOverlap, bar.y + Outline * 2f + LabelLine);
            bool moved = true;
            for (int guard = 0; moved && guard < 12; guard++)
            {
                moved = false;
                foreach (Rect taken in _placed)
                    if (taken.Overlaps(group))
                    {
                        group.y = taken.yMax;
                        moved = true;
                    }
            }
            _placed.Add(group);
            y = group.y + bar.y * 0.5f + Outline;
            h.Root.anchoredPosition = new Vector2(x - layer.xMin, y - layer.yMin);

            h.Bar.gameObject.SetActive(kid.HealthShown);
            if (kid.HealthShown)
            {
                h.Fill.anchorMax = new Vector2(Mathf.Clamp01((float)kid.Hp / Mathf.Max(maxHp, 1)), 1f);
                h.FillImage.color = kid.IsLocal ? SelfColor : AllyColor;
            }
            h.BadgeImage.color = MatchView.SideColor(!kid.Enemy);
            h.Number.text = kid.Number.ToString();
            string label = kid.Status;
            if (string.IsNullOrEmpty(label))
                label = _nameReach > 0f && Vector3.Distance(camera.transform.position, KalyeahResources.World(kid.Position)) > _nameReach ? "" : kid.Name;
            h.Label.text = label;
            bool capturing = kid.CaptureProgress >= 0f, rescuing = kid.RescueProgress >= 0f;
            h.Spinner.gameObject.SetActive(capturing || rescuing);
            if (capturing || rescuing)
            {
                h.SpinnerFill.color = capturing ? CaptureColor : RescueColor;
                h.SpinnerFill.fillAmount = Mathf.Clamp01(capturing ? kid.CaptureProgress : kid.RescueProgress);
            }
        }
    }
}
