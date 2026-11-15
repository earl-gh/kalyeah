using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kalyeah.Game
{
    /// <summary>
    /// The match HUD on the 1280 x 720 landscape base (port of scripts/ui/hud/match_hud.gd and its parts; docs/GDD.md "Landscape layout",
    /// "HUD"). Everything reads SimBridge; nothing decides a rule.
    /// - Top centre: your score, the clock chip (time; "Extended", then a gold "Overtime"), their score. A free-for-all street game shows every
    ///   kid's points in a column at the left instead.
    /// - Top left: your team's health (yours first); the other team's health is never shown (owner rule). The gear (top right) opens the
    ///   match menu (Resume, Leave battle), which pauses a match offline.
    /// - Over the kids: OverheadView. In the middle: the crosshair (gold while a card is held, an X on a hit), the banner, the capture count,
    ///   the point freeze's words. Above the controls: "Enemy capturing base / DEFEND THE BASE!" while they hold your base.
    /// - Controls: the joystick (left), the look pad (right), the ability arc (AbilityArcView) or the street games' pad (StreetPadView).
    /// - The takedown / rescue feed (right), a red pulse when you are hurt, the results card when the match is over.
    /// Code-drawn plates stand in for the owner's HUD art.
    /// </summary>
    public sealed class MatchHud
    {
        const float BarWidth = 150f, BarHeight = 12f, RowHeight = 40f, Face = 34f;
        const float BannerPopTime = 0.28f, BannerPopFrom = 1.5f, FeedTime = 4f, PulseTime = 0.45f;
        const int FeedLines = 4;
        static readonly Color HealthColor = new Color(0.36f, 0.92f, 0.45f);
        static readonly Color MissingColor = new Color(0.10f, 0.12f, 0.18f, 0.85f);
        static readonly Color OwnPlate = new Color(0.12f, 0.36f, 0.85f, 0.85f);
        static readonly Color EnemyPlate = new Color(0.80f, 0.18f, 0.18f, 0.85f);
        static readonly Color ClockBox = new Color(0.04f, 0.05f, 0.08f, 0.5f);
        static readonly Color GoldText = new Color(1f, 0.80f, 0.25f);

        public readonly Canvas Canvas;
        public readonly VirtualJoystick Joystick;
        public readonly LookPad Look;
        public readonly AbilityArcView Arc;
        public readonly StreetPadView Pad;
        public readonly OverheadView Overhead;
        /// <summary>The results card's buttons and the menu's Leave battle.</summary>
        public event Action AgainPressed, HomePressed, LeavePressed;
        public bool MenuOpen { get; private set; }

        readonly SimBridge _sim;
        readonly RectTransform _safe;
        readonly Text _mine, _theirs, _clockCaption, _clockTime, _banner, _captureLabel, _connection, _pointSubject, _pointOutcome, _pointScore;
        readonly RectTransform _header, _squad, _streetColumn, _captureAlert, _alertMark, _pointResult, _results, _menu, _confirm, _crosshair;
        readonly Image _pulse, _hitX, _crossDot;
        readonly Image[] _crossTicks = new Image[4];
        readonly Text _resultTitle, _resultSubtitle, _resultScore, _resultHow;
        readonly Text[] _feed = new Text[FeedLines];
        readonly List<(string text, Color color, float left)> _feedItems = new List<(string, Color, float)>();
        readonly List<Text> _streetRows = new List<Text>();
        readonly Dictionary<int, RectTransform> _fills = new Dictionary<int, RectTransform>();
        string _shownBanner = "";
        float _bannerAge = 10f, _pulseLeft, _hitLeft, _hitTime = 0.2f;
        bool _hitLethal, _resultsShown;
        string _connectionText = "";

        /// <param name="camera">Render through this camera (the screenshots); null for an overlay (play).</param>
        public MatchHud(Transform parent, SimBridge sim, ShoulderFollow follow, CameraRulesData rules, Camera camera, bool interactive)
        {
            _sim = sim;
            Canvas = Ui.MakeCanvas("MatchHud", parent, camera);
            Overhead = new OverheadView(Canvas.transform, rules);
            _pulse = Ui.MakeImage("HurtPulse", Canvas.transform, new Color(0.9f, 0.05f, 0.05f, 0f));
            Stretch(_pulse.rectTransform);
            _safe = Ui.Fill("SafeArea", Canvas.transform);
            _safe.gameObject.AddComponent<SafeArea>().Apply();

            if (interactive)
            {
                Look = LookPad.Build(_safe);
                Joystick = VirtualJoystick.Build(_safe);
                if (sim.HasHand) Arc = AbilityArcView.Build(_safe, sim, follow);
                else Pad = StreetPadView.Build(_safe, sim, follow, 110f);
            }

            // the crosshair, in the middle of the screen (the shoulder camera's aim)
            _crosshair = Ui.Panel("Crosshair", Canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 40f);
            Image ring = Ui.MakeImage("Ring", _crosshair, new Color(1f, 1f, 1f, 0.44f));
            ring.sprite = Ui.Ring();
            ring.rectTransform.sizeDelta = Vector2.one * 26f;
            _crossDot = Ui.MakeImage("Dot", _crosshair, new Color(1f, 1f, 1f, 0.78f), round: true);
            _crossDot.rectTransform.sizeDelta = Vector2.one * 3.2f;
            for (int i = 0; i < 4; i++)
            {
                _crossTicks[i] = Ui.MakeImage($"Tick{i}", _crosshair, new Color(1f, 1f, 1f, 0.78f));
                _crossTicks[i].rectTransform.sizeDelta = i % 2 == 0 ? new Vector2(1.4f, 5.5f) : new Vector2(5.5f, 1.4f);
            }
            _hitX = Ui.MakeImage("HitX", _crosshair, new Color(0.95f, 0.34f, 0.26f, 0f));
            _hitX.rectTransform.sizeDelta = new Vector2(18f, 2.4f);
            Image hitX2 = Ui.MakeImage("HitX2", _hitX.rectTransform, Color.white);
            hitX2.rectTransform.sizeDelta = new Vector2(18f, 2.4f);
            hitX2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            _hitX.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // the score and the clock at the top centre
            _header = Ui.Panel("Header", _safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(380f, 88f));
            Image mine = Plate("Mine", _header, OwnPlate, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(124f, 72f));
            _mine = Ui.MakeText("Score", mine.transform, "0", 44, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Image theirs = Plate("Theirs", _header, EnemyPlate, new Vector2(1f, 0.5f), new Vector2(1f, 0f), new Vector2(124f, 72f));
            _theirs = Ui.MakeText("Score", theirs.transform, "0", 44, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Image clock = Plate("Clock", _header, ClockBox, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(124f, 72f));
            Outline box = clock.gameObject.AddComponent<Outline>();
            box.effectColor = Color.black;
            box.effectDistance = new Vector2(3f, -3f);
            _clockCaption = Ui.MakeText("Caption", clock.transform, "", 18, Color.white, TextAnchor.MiddleCenter);
            _clockCaption.rectTransform.anchoredPosition = new Vector2(0f, 18f);
            _clockTime = Ui.MakeText("Time", clock.transform, "0:00", 36, Color.white, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());

            // your team's health at the top left (yours first); a free-for-all street game's points column instead
            _squad = Ui.Panel("OwnTeam", _safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -16f), new Vector2(Face + BarWidth + 16f, RowHeight * 5f));
            int row = 0;
            foreach (KidSnapshot kid in Ordered(sim.Kids))
            {
                if (kid.Enemy) continue; // the other team's health is never shown
                if (sim.IsFreeForAll && !kid.IsLocal) continue;
                AddRow(_squad, kid, row++);
            }
            _streetColumn = Ui.Panel("StreetScores", _safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -16f - RowHeight - 8f), new Vector2(260f, 6 * 30f));
            _streetColumn.gameObject.SetActive(sim.IsFreeForAll);

            // the gear: the match menu
            Button gear = Ui.MakeButton("Gear", _safe, "II", new Color(0.06f, 0.10f, 0.22f, 0.72f), new Vector2(72f, 72f), ToggleMenu);
            var gearRect = (RectTransform)gear.transform;
            gearRect.anchorMin = gearRect.anchorMax = gearRect.pivot = new Vector2(1f, 1f);
            gearRect.anchoredPosition = new Vector2(-16f, -16f);
            Texture2D gearArt = KalyeahResources.Texture("ui/hud_icons/gear");
            if (gearArt != null)
            {
                gear.GetComponentInChildren<Text>().text = "";
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
                icon.transform.SetParent(gear.transform, false);
                var raw = icon.GetComponent<RawImage>();
                raw.texture = gearArt;
                raw.raycastTarget = false;
                raw.rectTransform.sizeDelta = Vector2.one * 48f;
            }

            // the feed under the gear
            for (int i = 0; i < FeedLines; i++)
            {
                _feed[i] = Ui.MakeText($"Feed{i}", _safe, "", 20, Color.white, TextAnchor.UpperRight);
                RectTransform r = _feed[i].rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f);
                r.anchoredPosition = new Vector2(-24f, -100f - i * 28f);
            }

            _banner = Ui.MakeText("Banner", _safe, "", 64, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            _banner.rectTransform.anchoredPosition = new Vector2(0f, 110f);
            _captureLabel = Ui.MakeText("CaptureCount", _safe, "", 32, new Color(1f, 0.95f, 0.85f), TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            _captureLabel.rectTransform.anchorMin = _captureLabel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _captureLabel.rectTransform.anchoredPosition = new Vector2(0f, -160f);

            // "Enemy capturing base / DEFEND THE BASE!" above the controls
            Image alert = Plate("CaptureAlert", _safe, new Color(0.45f, 0.08f, 0.08f, 0.9f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(460f, 88f));
            _captureAlert = alert.rectTransform;
            _captureAlert.anchoredPosition = new Vector2(0f, 120f);
            Image mark = Ui.MakeImage("Mark", _captureAlert, new Color(1f, 0.82f, 0.12f), round: true);
            _alertMark = mark.rectTransform;
            _alertMark.anchorMin = _alertMark.anchorMax = new Vector2(0f, 0.5f);
            _alertMark.anchoredPosition = new Vector2(48f, 0f);
            _alertMark.sizeDelta = Vector2.one * 56f;
            Ui.MakeText("Bang", _alertMark, "!", 40, new Color(0.04f, 0.04f, 0.06f), TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            Text small = Ui.MakeText("Small", _captureAlert, "Enemy capturing base", 18, Color.white, TextAnchor.MiddleLeft);
            small.rectTransform.anchorMin = small.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            small.rectTransform.pivot = new Vector2(0f, 0.5f);
            small.rectTransform.anchoredPosition = new Vector2(92f, 18f);
            Text big = Ui.MakeText("Big", _captureAlert, "DEFEND THE BASE!", 32, new Color(1f, 0.86f, 0.25f), TextAnchor.MiddleLeft, KalyeahResources.DisplayFont());
            big.rectTransform.anchorMin = big.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            big.rectTransform.pivot = new Vector2(0f, 0.5f);
            big.rectTransform.anchoredPosition = new Vector2(92f, -12f);
            _captureAlert.gameObject.SetActive(false);

            _connection = Ui.MakeText("Connection", _safe, "", 30, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());

            // the point freeze: words over a dimmed street
            Image freeze = Ui.Dim("PointResult", Canvas.transform, 0.45f);
            _pointResult = freeze.rectTransform;
            _pointSubject = Ui.MakeText("Subject", _pointResult, "", 28, Ui.Cream, TextAnchor.MiddleCenter);
            _pointSubject.rectTransform.anchoredPosition = new Vector2(0f, 70f);
            _pointOutcome = Ui.MakeText("Outcome", _pointResult, "", 72, GoldText, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            _pointScore = Ui.MakeText("Score", _pointResult, "", 56, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            _pointScore.rectTransform.anchoredPosition = new Vector2(0f, -80f);
            _pointResult.gameObject.SetActive(false);

            // the results card
            Image resultsDim = Ui.Dim("Results", Canvas.transform, 0.65f);
            _results = resultsDim.rectTransform;
            Image card = Plate("Card", _results, new Color(0.06f, 0.10f, 0.22f, 0.95f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(620f, 520f));
            _resultTitle = Ui.MakeText("Title", card.transform, "", 72, GoldText, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            _resultTitle.rectTransform.anchoredPosition = new Vector2(0f, 200f);
            _resultSubtitle = Ui.MakeText("Subtitle", card.transform, "", 22, Ui.Cream, TextAnchor.MiddleCenter);
            _resultSubtitle.rectTransform.anchoredPosition = new Vector2(0f, 140f);
            _resultScore = Ui.MakeText("Score", card.transform, "", 64, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            _resultScore.rectTransform.anchoredPosition = new Vector2(0f, 70f);
            _resultHow = Ui.MakeText("How", card.transform, "", 22, Ui.Cream, TextAnchor.UpperCenter);
            _resultHow.rectTransform.anchoredPosition = new Vector2(0f, 10f);
            Button again = Ui.MakeButton("Again", card.transform, sim.IsOnline ? "Return home" : "Practice again", new Color(0.95f, 0.66f, 0.12f), new Vector2(260f, 76f),
                () => AgainPressed?.Invoke());
            ((RectTransform)again.transform).anchoredPosition = new Vector2(sim.IsOnline ? 0f : 140f, -200f);
            if (!sim.IsOnline)
            {
                Button home = Ui.MakeButton("Home", card.transform, "Home", new Color(0.12f, 0.36f, 0.85f), new Vector2(220f, 76f), () => HomePressed?.Invoke());
                ((RectTransform)home.transform).anchoredPosition = new Vector2(-150f, -200f);
            }
            _results.gameObject.SetActive(false);

            // the match menu (pauses offline) and the leave confirmation
            Image menuDim = Ui.Dim("Menu", Canvas.transform);
            _menu = menuDim.rectTransform;
            Image sheet = Plate("Sheet", _menu, new Color(0.06f, 0.10f, 0.22f, 0.95f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(520f, 380f));
            Text heading = Ui.MakeText("Heading", sheet.transform, "Match settings", 40, Ui.Cream, TextAnchor.MiddleCenter, KalyeahResources.DisplayFont());
            heading.rectTransform.anchoredPosition = new Vector2(0f, 130f);
            Button resume = Ui.MakeButton("Resume", sheet.transform, "Resume", new Color(0.12f, 0.36f, 0.85f), new Vector2(360f, 80f), CloseMenu);
            ((RectTransform)resume.transform).anchoredPosition = new Vector2(0f, 20f);
            Button leave = Ui.MakeButton("Leave", sheet.transform, "Leave battle", new Color(0.80f, 0.18f, 0.18f), new Vector2(360f, 80f), RequestLeave);
            ((RectTransform)leave.transform).anchoredPosition = new Vector2(0f, -90f);
            Image confirmDim = Ui.Dim("Confirm", _menu, 0.5f);
            _confirm = confirmDim.rectTransform;
            Image ask = Plate("Ask", _confirm, new Color(0.06f, 0.10f, 0.22f, 0.98f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(560f, 300f));
            Text question = Ui.MakeText("Question", ask.transform, sim.IsOnline ? "Leave the battle?" : "Leave the battle?\nIt counts as a loss.", 30, Ui.Cream, TextAnchor.MiddleCenter);
            question.rectTransform.anchoredPosition = new Vector2(0f, 60f);
            Button stay = Ui.MakeButton("Stay", ask.transform, "Stay", new Color(0.12f, 0.36f, 0.85f), new Vector2(220f, 76f), () => _confirm.gameObject.SetActive(false));
            ((RectTransform)stay.transform).anchoredPosition = new Vector2(-125f, -80f);
            Button go = Ui.MakeButton("Go", ask.transform, "Leave", new Color(0.80f, 0.18f, 0.18f), new Vector2(220f, 76f), () => LeavePressed?.Invoke());
            ((RectTransform)go.transform).anchoredPosition = new Vector2(125f, -80f);
            _confirm.gameObject.SetActive(false);
            _menu.gameObject.SetActive(false);

            sim.Takedown += (by, victim, ours, weapon) => Push($"{by}  ›  {victim}" + (string.IsNullOrEmpty(weapon) ? "" : $"  ({weapon})"), ours);
            sim.Rescue += (by, saved, ours) => Push($"{by}  +  {saved}  RESCUED", ours);
            sim.LocalHit += lethal =>
            {
                _hitLethal = lethal;
                _hitTime = lethal ? 0.32f : 0.2f;
                _hitLeft = _hitTime;
            };
            sim.LocalDamaged += amount => _pulseLeft = PulseTime * Mathf.Clamp01(0.4f + amount / (float)Mathf.Max(sim.MaxHp, 1));
            sim.ConnectionNote += text => _connectionText = text;
        }

        static Image Plate(string name, Transform parent, Color color, Vector2 anchor, Vector2 pivot, Vector2 size)
        {
            Image plate = Ui.MakeImage(name, parent, color);
            RectTransform r = plate.rectTransform;
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = size;
            return plate;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.sizeDelta = Vector2.zero;
        }

        static IEnumerable<KidSnapshot> Ordered(IReadOnlyList<KidSnapshot> kids)
        {
            foreach (KidSnapshot kid in kids) if (kid.IsLocal) yield return kid;
            foreach (KidSnapshot kid in kids) if (!kid.IsLocal) yield return kid;
        }

        void AddRow(RectTransform squad, KidSnapshot kid, int row)
        {
            RectTransform line = Ui.Panel($"Kid_{kid.Id}", squad, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -row * RowHeight), new Vector2(Face + BarWidth + 16f, RowHeight - 4f));
            Image disc = Ui.MakeImage("Ring", line, MatchView.SideColor(true), round: true);
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
            float scale = kid.IsLocal ? 1f : 0.8f;
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

        void Push(string text, bool ours)
        {
            _feedItems.Insert(0, (text, ours ? new Color(0.55f, 0.75f, 1f) : new Color(1f, 0.55f, 0.5f), FeedTime));
            if (_feedItems.Count > FeedLines) _feedItems.RemoveAt(_feedItems.Count - 1);
        }

        // ---- the menu ---------------------------------------------------------------------------------------------------------

        /// <summary>The gear and Android Back: opens or closes the match menu (not over the results).</summary>
        public void ToggleMenu()
        {
            if (MenuOpen) CloseMenu();
            else if (_sim.Phase != MatchPhaseView.MatchOver)
            {
                MenuOpen = true;
                CancelControls();
                _menu.gameObject.SetActive(true);
                _confirm.gameObject.SetActive(false);
            }
        }

        void CloseMenu()
        {
            MenuOpen = false;
            _menu.gameObject.SetActive(false);
        }

        /// <summary>Leave battle: a Practice match against bots asks first (it counts as a loss); training and the street games go at once.</summary>
        void RequestLeave()
        {
            if (_sim.MatchKind == SimBridge.Kind.Practice || _sim.IsOnline) _confirm.gameObject.SetActive(true);
            else LeavePressed?.Invoke();
        }

        /// <summary>Lets go of every control without a cast (a menu, a pause, the app going to the background).</summary>
        public void CancelControls()
        {
            Arc?.Cancel();
            Pad?.Cancel();
        }

        // ---- every frame ------------------------------------------------------------------------------------------------------

        /// <summary>Once a frame, after the sim's ticks: everything from the bridge. `world` draws the street (for the overhead badges).</summary>
        public void Refresh(float dt, Camera world, IReadOnlyList<KidSnapshot> drawn)
        {
            ScoreHud s = _sim.Score;
            bool covered = s.Phase == MatchPhaseView.PointFreeze || s.Phase == MatchPhaseView.MatchOver;
            if (covered && MenuOpen) CloseMenu();

            _header.gameObject.SetActive(!covered);
            _mine.transform.parent.gameObject.SetActive(s.TeamScores);
            _theirs.transform.parent.gameObject.SetActive(s.TeamScores);
            _mine.text = s.Mine.ToString();
            _theirs.text = s.Theirs.ToString();
            ClockDisplay clock = s.Clock;
            _clockCaption.text = clock.HasCaption ? clock.Caption : "";
            _clockCaption.color = clock.Gold ? GoldText : Color.white;
            _clockTime.text = clock.Text;
            _clockTime.fontSize = clock.HasCaption ? 28 : 36;
            _clockTime.rectTransform.anchoredPosition = new Vector2(0f, clock.HasCaption ? -10f : 0f);

            _squad.gameObject.SetActive(!covered);
            foreach (KidSnapshot kid in _sim.Kids)
                if (_fills.TryGetValue(kid.Id, out RectTransform fill))
                    fill.anchorMax = new Vector2(kid.Tagged ? 0f : Mathf.Clamp01((float)kid.Hp / Mathf.Max(_sim.MaxHp, 1)), 1f);
            if (_sim.IsFreeForAll) SyncStreetColumn(covered);

            // the banner pops in (scale 1.5 to 1 with an overshoot) and stays BannerTime seconds (SimBridge.BannerText)
            string bannerText = covered && s.Phase != MatchPhaseView.MatchOver ? "" : _sim.BannerText;
            if (bannerText != _shownBanner)
            {
                _shownBanner = bannerText;
                _bannerAge = 0f;
            }
            _bannerAge += dt;
            _banner.text = s.Phase == MatchPhaseView.MatchOver ? "" : bannerText;
            float pop = HudPrefs.ReducedMotion ? 1f : Mathf.LerpUnclamped(BannerPopFrom, 1f, EaseOutBack(Mathf.Clamp01(_bannerAge / BannerPopTime)));
            _banner.rectTransform.localScale = Vector3.one * pop;

            _captureAlert.gameObject.SetActive(s.CaptureAlert && !covered);
            if (s.CaptureAlert && !HudPrefs.ReducedMotion)
                _alertMark.localScale = Vector3.one * (1f + 0.08f * 0.5f * (1f + Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * 1.6f)));
            _captureLabel.text = covered ? "" : s.CaptureLabel;
            _captureLabel.color = Color.Lerp(new Color(1f, 0.95f, 0.85f), s.CaptureOurs ? new Color(0.55f, 0.75f, 1f) : new Color(1f, 0.55f, 0.5f), 0.35f);

            _pointResult.gameObject.SetActive(s.Phase == MatchPhaseView.PointFreeze);
            _pointSubject.text = s.PointSubject;
            _pointOutcome.text = s.PointOutcome;
            _pointScore.text = s.PointScore;

            bool over = s.Phase == MatchPhaseView.MatchOver;
            if (over != _resultsShown)
            {
                _resultsShown = over;
                _results.gameObject.SetActive(over);
                if (over) FillResults(_sim.Result());
            }

            for (int i = _feedItems.Count - 1; i >= 0; i--)
            {
                var item = _feedItems[i];
                item.left -= dt;
                if (item.left <= 0f) _feedItems.RemoveAt(i);
                else _feedItems[i] = item;
            }
            for (int i = 0; i < FeedLines; i++)
            {
                bool has = i < _feedItems.Count && !covered;
                _feed[i].text = has ? _feedItems[i].text : "";
                if (has) _feed[i].color = new Color(_feedItems[i].color.r, _feedItems[i].color.g, _feedItems[i].color.b, Mathf.Clamp01(_feedItems[i].left));
            }

            _pulseLeft = Mathf.Max(0f, _pulseLeft - dt);
            _pulse.color = new Color(0.9f, 0.05f, 0.05f, HudPrefs.ReducedMotion ? 0f : 0.35f * _pulseLeft / PulseTime);
            _connection.text = _connectionText;

            LocalHud local = _sim.Local;
            bool shown = local.Shown && !covered;
            SyncCrosshair(shown && _sim.HasHand, local, dt);
            if (Joystick != null) Joystick.gameObject.SetActive(shown);
            if (Look != null) Look.gameObject.SetActive(local.Playing && !covered);
            Arc?.Sync(dt);
            Pad?.Sync();
            Overhead.Sync(_sim, drawn, world, Canvas.renderMode == RenderMode.ScreenSpaceCamera ? Canvas.worldCamera : null);
        }

        void SyncStreetColumn(bool covered)
        {
            _streetColumn.gameObject.SetActive(!covered);
            List<StreetScore> rows = _sim.StreetScores();
            while (_streetRows.Count < rows.Count)
            {
                Text text = Ui.MakeText($"Row{_streetRows.Count}", _streetColumn, "", 22, Ui.Cream, TextAnchor.UpperLeft);
                RectTransform r = text.rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
                r.anchoredPosition = new Vector2(0f, -_streetRows.Count * 30f);
                _streetRows.Add(text);
            }
            for (int i = 0; i < _streetRows.Count; i++)
            {
                bool has = i < rows.Count;
                _streetRows[i].text = has ? $"{rows[i].Points}  {rows[i].Name}{(rows[i].IsTaya ? "  (IT)" : "")}" : "";
                if (has) _streetRows[i].color = rows[i].IsLocal ? GoldText : Ui.Cream;
            }
        }

        /// <summary>The Deadlock-style reticle (crosshair.gd): gold and tight while a card is held, open a little while running, an X on a hit.</summary>
        void SyncCrosshair(bool shown, LocalHud local, float dt)
        {
            _crosshair.gameObject.SetActive(shown);
            if (!shown) return;
            float bloom = (local.Running ? 3f : 0f) - (local.HoldingCard ? 1.5f : 0f);
            float gap = 5.5f + bloom;
            Color tint = local.HoldingCard ? new Color(1f, 0.92f, 0.68f, 0.92f) : new Color(1f, 1f, 1f, 0.78f);
            var ways = new[] { new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(-1f, 0f) };
            for (int i = 0; i < 4; i++)
            {
                _crossTicks[i].rectTransform.anchoredPosition = ways[i] * (gap + 2.75f);
                _crossTicks[i].color = tint;
            }
            _crossDot.color = tint;
            _hitLeft = Mathf.Max(0f, _hitLeft - dt);
            float a = _hitLeft > 0f ? _hitLeft / _hitTime : 0f;
            Color hit = _hitLethal ? new Color(1f, 0.84f, 0.34f, 0.96f * a) : new Color(0.95f, 0.34f, 0.26f, 0.92f * a);
            _hitX.color = hit;
            _hitX.rectTransform.GetChild(0).GetComponent<Image>().color = hit;
        }

        void FillResults(ResultInfo result)
        {
            _resultTitle.text = result.Title.ToUpperInvariant();
            _resultTitle.color = result.Won ? GoldText : new Color(0.75f, 0.78f, 0.85f);
            _resultSubtitle.text = result.Subtitle;
            _resultScore.text = $"{result.Mine} - {result.Theirs}";
            var lines = new List<string>();
            foreach (var (ours, text) in result.How) lines.Add((ours ? "+ " : "- ") + text);
            _resultHow.text = string.Join("\n", lines);
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
