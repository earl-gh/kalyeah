using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// The round ability arc under the right thumb (port of scripts/ui/hud/ability_arc.gd; docs/GDD.md "Landscape layout", "Shoulder view").
    /// ArcLayout says where everything stands; this draws it with UGUI and turns touches into SimBridge calls:
    /// - <b>Cards</b> (elixir on): four round cards + NEXT, the ice water bar and the stamina bar. Press a ready card (a card that is not ready
    ///   shakes), drag to turn the view (the card goes where the crosshair is), let go to cast there; let go on the X to cancel; a quick tap
    ///   on a heal heals yourself.
    /// - <b>Hero</b> (today's data): the primary (held: fires at the crosshair, a drag on it turns the view), three superpowers (as the cards),
    ///   the ultimate (when charged), vault (dash), jump and crouch / slide (slide while running, else a held crouch).
    /// Code-drawn white discs and rings stand in for the owner's HUD art (owner rule: no generated art); a weapon's icon is
    /// assets/ui/hud_icons/&lt;id&gt;.png when it is there.
    /// </summary>
    public sealed class AbilityArcView : MonoBehaviour
    {
        const float DenyTime = 0.32f, DenyShake = 9f, DenyWaves = 3f, WhiteOff = 0.45f;
        static readonly Color Navy = new Color(0.06f, 0.10f, 0.22f, 0.78f);
        static readonly Color Rim = new Color(0.93f, 0.95f, 1f, 0.75f);
        static readonly Color Gold = new Color(1f, 0.80f, 0.25f, 1f);
        static readonly Color Cool = new Color(0.06f, 0.05f, 0.08f, 0.72f);
        static readonly Color CancelRim = new Color(1f, 0.35f, 0.3f, 0.9f);
        static readonly Color CancelHot = new Color(0.85f, 0.15f, 0.12f, 0.85f);
        static readonly Color BarTrack = new Color(0.035f, 0.1f, 0.27f, 0.6f);
        static readonly Color IceWater = new Color(0.36f, 0.8f, 1f);
        static readonly Color StaminaBlue = new Color(0.22f, 0.52f, 1f);
        static readonly Color StaminaAmber = new Color(1f, 0.7f, 0.2f);
        static readonly Color StaminaRed = new Color(0.95f, 0.25f, 0.2f);
        static readonly Color WhiteBody = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color WhiteBodyHeld = new Color(1f, 1f, 1f, 0.32f);

        sealed class Button
        {
            public RectTransform Root;
            public Image Body, Rim, Sweep, Flash;
            public RawImage Icon;
            public Text Label, Small, Tag;
            public string IconId;
            public Vector2 Center;
            public float Radius;
            public float Deny, Pop;
        }

        SimBridge _bridge;
        ShoulderFollow _camera;
        RectTransform _rect;
        readonly ArcLayout _layout = new ArcLayout();
        readonly Button[] _slots = new Button[ArcLayout.Slots];
        Button _next, _cancel, _primary, _ult, _vault, _jump, _crouch;
        RectTransform _barTrack, _barFill, _staminaTrack, _staminaFill;
        Text _barNumber;
        Image _staminaFillImage;
        Vector2 _laidOutFor = new Vector2(-1f, -1f);
        bool _laidOutElixir;
        bool _built;

        int _cardPointer = int.MinValue, _heldSlot = -1;
        Vector2 _pressAt;
        bool _onCard, _overCancel;
        int _primaryPointer = int.MinValue, _crouchPointer = int.MinValue;

        public bool Holding => _heldSlot >= 0;

        public static AbilityArcView Build(Transform parent, SimBridge bridge, ShoulderFollow camera)
        {
            RectTransform rect = Ui.Fill("AbilityArc", parent);
            rect.pivot = new Vector2(0f, 1f);
            var view = rect.gameObject.AddComponent<AbilityArcView>();
            view._rect = rect;
            view._bridge = bridge;
            view._camera = camera;
            return view;
        }

        // ---- layout ---------------------------------------------------------------------------------------------------------

        void Relayout(bool elixir)
        {
            Vector2 size = _rect.rect.size;
            if (_built && size == _laidOutFor && elixir == _laidOutElixir) return;
            Cancel();
            for (int i = _rect.childCount - 1; i >= 0; i--) KalyeahResources.Kill(_rect.GetChild(i).gameObject);
            _laidOutFor = size;
            _laidOutElixir = elixir;
            _built = true;
            _layout.Layout(new SVector2(size.x, size.y), SVector2.Zero, elixir);

            BuildBars();
            for (int slot = 0; slot < ArcLayout.Slots; slot++)
                _slots[slot] = MakeButton($"Slot{slot}", G(_layout.Centers[slot]), _layout.SlotRadius, elixir, slot, ArcLayout.Button.None);
            _primary = _ult = _vault = _jump = _crouch = _next = null;
            if (elixir)
            {
                _next = MakeButton("Next", G(_layout.NextCenter), ArcLayout.NextRadius, true, -1, ArcLayout.Button.None);
                Text word = Ui.MakeText("NextWord", _next.Root, "NEXT", 16, Ui.Cream, TextAnchor.LowerCenter);
                word.rectTransform.anchoredPosition = new Vector2(0f, ArcLayout.NextRadius + 12f);
            }
            else
            {
                _primary = MakeButton("Primary", G(_layout.PrimaryCenter), ArcLayout.PrimaryRadius, false, -1, ArcLayout.Button.Primary);
                _ult = MakeButton("Ultimate", G(_layout.UltCenter), ArcLayout.UltRadius, false, -1, ArcLayout.Button.Ultimate);
                _vault = MakeButton("Vault", G(_layout.VaultCenter), ArcLayout.VaultRadius, false, -1, ArcLayout.Button.Vault);
                _jump = MakeButton("Jump", G(_layout.JumpCenter), ArcLayout.JumpRadius, false, -1, ArcLayout.Button.Jump);
                _crouch = MakeButton("Crouch", G(_layout.CrouchCenter), ArcLayout.CrouchRadius, false, -1, ArcLayout.Button.Crouch);
            }
            _cancel = MakeButton("Cancel", G(_layout.CancelCenter), ArcLayout.CancelRadius, elixir, -1, ArcLayout.Button.None);
            _cancel.Body.color = elixir ? new Color(0f, 0f, 0f, 0.35f) : WhiteBody;
            _cancel.Rim.color = elixir ? CancelRim : Color.white;
            foreach (float angle in new[] { 45f, -45f })
            {
                Image arm = Ui.MakeImage("Arm", _cancel.Root, Color.white);
                arm.rectTransform.sizeDelta = new Vector2(ArcLayout.CancelRadius * 0.9f, 5f);
                arm.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            _cancel.Root.gameObject.SetActive(false);
        }

        void BuildBars()
        {
            Vector4 bar = V4(_layout.BarRect), stamina = V4(_layout.StaminaRect);
            _barTrack = Strip("IceWaterBar", bar, BarTrack);
            _barFill = Ui.MakeImage("Fill", _barTrack, IceWater).rectTransform;
            LeftFill(_barFill);
            _barNumber = Ui.MakeText("Whole", _barTrack, "0", 20, Color.white, TextAnchor.MiddleRight);
            _barNumber.rectTransform.anchorMin = _barNumber.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            _barNumber.rectTransform.anchoredPosition = new Vector2(-14f, 0f);
            _staminaTrack = Strip("StaminaBar", stamina, BarTrack);
            _staminaFillImage = Ui.MakeImage("Fill", _staminaTrack, StaminaBlue);
            _staminaFill = _staminaFillImage.rectTransform;
            LeftFill(_staminaFill);
        }

        RectTransform Strip(string name, Vector4 r, Color color)
        {
            Image image = Ui.MakeImage(name, _rect, color);
            RectTransform t = image.rectTransform;
            t.anchorMin = t.anchorMax = new Vector2(0f, 1f);
            t.pivot = new Vector2(0f, 1f);
            t.anchoredPosition = new Vector2(r.x, -r.y);
            t.sizeDelta = new Vector2(r.z, r.w);
            return t;
        }

        static void LeftFill(RectTransform fill)
        {
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(1f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.sizeDelta = Vector2.zero;
        }

        Button MakeButton(string name, Vector2 center, float radius, bool card, int slot, ArcLayout.Button hero)
        {
            var b = new Button { Center = center, Radius = radius };
            b.Body = Ui.Disc(name, _rect, center, radius, card ? Navy : WhiteBody);
            b.Root = b.Body.rectTransform;
            b.Body.raycastTarget = slot >= 0 || hero != ArcLayout.Button.None;
            b.Sweep = Ui.Sweep("Sweep", b.Root, radius, card ? Cool : new Color(1f, 1f, 1f, 0.22f));
            b.Sweep.fillAmount = 0f;
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
            iconGo.transform.SetParent(b.Root, false);
            b.Icon = iconGo.GetComponent<RawImage>();
            b.Icon.raycastTarget = false;
            b.Icon.rectTransform.sizeDelta = Vector2.one * radius * 1.36f;
            b.Icon.enabled = false;
            b.Label = Ui.MakeText("Label", b.Root, "", Mathf.RoundToInt(Mathf.Clamp(radius * 0.36f, 12f, 20f)), Color.white, TextAnchor.MiddleCenter);
            b.Small = Ui.MakeText("Small", b.Root, "", 16, Color.white, TextAnchor.MiddleCenter);
            b.Small.rectTransform.anchoredPosition = new Vector2(0f, -radius * 0.56f);
            b.Tag = Ui.MakeText("Tag", b.Root, "", 14, new Color(1f, 0.78f, 0.3f), TextAnchor.MiddleCenter);
            b.Flash = Ui.MakeImage("Flash", b.Root, new Color(1f, 1f, 1f, 0f), round: true);
            b.Flash.rectTransform.sizeDelta = Vector2.one * radius * 2f;
            b.Rim = Ui.MakeImage("Rim", b.Root, card ? Rim : Color.white);
            b.Rim.sprite = Ui.Ring();
            b.Rim.rectTransform.sizeDelta = Vector2.one * radius * 2f;
            if (b.Body.raycastTarget)
            {
                var hit = b.Root.gameObject.AddComponent<ArcTouch>();
                hit.Arc = this;
                hit.Slot = slot;
                hit.Hero = hero;
                float pad = slot >= 0 ? ArcLayout.CardGap : ArcLayout.HeroTouchPad;
                hit.Radius = pad + radius;
                b.Body.raycastPadding = new Vector4(-pad, -pad, -pad, -pad); // the rect test before the round one reaches the pad too
            }
            return b;
        }

        // ---- every frame ----------------------------------------------------------------------------------------------------

        /// <summary>Reads your hand and buttons from the bridge (SimBridge.Local) and redraws what changed.</summary>
        public void Sync(float dt)
        {
            LocalHud h = _bridge.Local;
            Relayout(h.UsesElixir);
            _rect.gameObject.SetActive(h.Shown);
            if (!h.Shown) return;
            bool blocked = h.Block.Length > 0;
            if (_heldSlot >= 0 && (!h.Playing || blocked || !h.Cards[_heldSlot].Present)) Cancel();

            for (int slot = 0; slot < ArcLayout.Slots; slot++)
            {
                Button b = _slots[slot];
                CardInfo card = h.Cards[slot];
                bool shown = card.Present || h.UsesElixir;
                b.Root.gameObject.SetActive(shown);
                if (!shown) continue;
                Tick(b, dt);
                SetIcon(b, card.Present ? card.Id : "", card.Name);
                bool held = slot == _heldSlot;
                if (h.UsesElixir)
                {
                    float light = blocked ? 0.45f : 1f;
                    b.Body.color = card.Present ? new Color(Navy.r, Navy.g, Navy.b, Navy.a) * new Color(light, light, light, 1f) : new Color(0f, 0f, 0f, 0.3f);
                    b.Rim.color = held ? Gold : Rim;
                    b.Sweep.fillAmount = !blocked && card.Present && !card.Pickable && card.Reached < 1f ? 1f - card.Reached : 0f;
                    b.Small.text = card.Present ? card.Cost.ToString() : "";
                }
                else
                {
                    bool ready = !blocked && card.CooldownLeft <= 0.02f && card.Pickable;
                    b.Body.color = held ? WhiteBodyHeld : WhiteBody;
                    b.Sweep.fillAmount = !blocked && card.CooldownLeft > 0.02f ? card.Reached : 0f;
                    SetStrength(b, ready || held ? 0.96f : WhiteOff);
                    b.Small.text = !blocked && card.CooldownLeft > 0.02f ? HudText.CooldownText(card.CooldownLeft) : "";
                }
                b.Tag.text = blocked ? h.Block : card.Notice;
            }

            if (_next != null)
            {
                Tick(_next, dt);
                SetIcon(_next, h.NextId, h.NextName);
                _next.Sweep.fillAmount = h.NextFill < 1f ? 1f - h.NextFill : 0f;
            }
            if (_primary != null) SyncHero(h, blocked, dt);

            _barTrack.gameObject.SetActive(h.UsesElixir);
            if (h.UsesElixir)
            {
                _barFill.anchorMax = new Vector2(Mathf.Clamp01(h.Elixir / Mathf.Max(h.ElixirMax, 0.001f)), 1f);
                _barNumber.text = Mathf.FloorToInt(h.Elixir + 0.001f).ToString();
            }
            _staminaTrack.gameObject.SetActive(h.StaminaShown);
            if (h.StaminaShown)
            {
                _staminaFill.anchorMax = new Vector2(Mathf.Clamp01(h.StaminaShare), 1f);
                _staminaFillImage.color = h.StaminaTone == 2 ? StaminaRed : h.StaminaTone == 1 ? StaminaAmber : StaminaBlue;
            }
            _cancel.Root.gameObject.SetActive(_heldSlot >= 0);
            if (_heldSlot >= 0 && h.UsesElixir) _cancel.Body.color = _overCancel ? CancelHot : new Color(0f, 0f, 0f, 0.35f);
            else if (_heldSlot >= 0) _cancel.Body.color = _overCancel ? WhiteBodyHeld : WhiteBody;
        }

        void SyncHero(LocalHud h, bool blocked, float dt)
        {
            foreach (Button b in new[] { _primary, _ult, _vault, _jump, _crouch }) Tick(b, dt);
            SetIcon(_primary, h.PrimaryId, "TIRADOR");
            _primary.Body.color = _primaryPointer != int.MinValue ? WhiteBodyHeld : WhiteBody;
            _primary.Sweep.fillAmount = h.PrimaryCdLeft > 0.02f ? h.PrimaryCdShare : 0f;
            SetStrength(_primary, blocked ? WhiteOff : 0.96f);

            bool ultFull = h.UltCharge >= 99.99f;
            SetIcon(_ult, h.UltId, "BAGSAK");
            _ult.Sweep.fillAmount = !ultFull && h.UltCharge > 0.5f ? h.UltCharge / 100f : 0f;
            _ult.Small.text = ultFull ? "" : Mathf.FloorToInt(h.UltCharge) + "%";
            float halo = HudPrefs.ReducedMotion ? 0.48f : 0.36f + 0.24f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.2f));
            _ult.Rim.color = ultFull ? new Color(1f, 1f, 1f, 0.5f + halo) : Color.white;
            SetStrength(_ult, ultFull && !blocked ? 0.96f : WhiteOff);

            SetIcon(_vault, "vault", "VAULT");
            _vault.Body.color = h.VaultActive ? WhiteBodyHeld : WhiteBody;
            _vault.Sweep.fillAmount = h.VaultCdLeft > 0.02f ? h.VaultCdShare : 0f;
            _vault.Small.text = h.VaultCdLeft > 0.02f ? HudText.CooldownText(h.VaultCdLeft) : "";
            SetStrength(_vault, h.VaultReady || h.VaultActive ? 0.95f : WhiteOff);

            SetIcon(_jump, "jump", "JUMP");
            _jump.Body.color = h.InAir ? WhiteBodyHeld : WhiteBody;
            _jump.Sweep.fillAmount = h.JumpCdShare < 1f ? h.JumpCdShare : 0f;
            SetStrength(_jump, h.JumpReady || h.InAir ? 0.95f : WhiteOff);

            bool slide = h.CanSlide || h.Sliding;
            SetIcon(_crouch, slide ? "slide" : "crouch", slide ? "SLIDE" : "CROUCH");
            _crouch.Body.color = _crouchPointer != int.MinValue || h.Crouching || h.Sliding ? WhiteBodyHeld : WhiteBody;
            _crouch.Sweep.fillAmount = h.SlideCdLeft > 0.02f && !h.Crouching ? h.SlideCdShare : 0f;
            _crouch.Small.text = h.Running && h.SlideCdLeft > 0.15f && !h.Crouching ? HudText.CooldownText(h.SlideCdLeft) : "";
            SetStrength(_crouch, !blocked && h.CanMove ? 0.95f : WhiteOff);
            if (_crouchPointer != int.MinValue && (!h.Playing || !h.CanMove)) { _crouchPointer = int.MinValue; _bridge.SetCrouch(false); }
            if (_primaryPointer != int.MinValue && (!h.Playing || blocked)) { _primaryPointer = int.MinValue; _bridge.SetPrimary(false); }
        }

        /// <summary>The shake of a button that said no, and the flash of one that fired.</summary>
        void Tick(Button b, float dt)
        {
            float shake = 0f;
            if (b.Deny > 0f && !HudPrefs.ReducedMotion)
            {
                float t = 1f - b.Deny / DenyTime;
                shake = Mathf.Sin(t * Mathf.PI * 2f * DenyWaves) * DenyShake * (1f - t);
            }
            b.Root.anchoredPosition = new Vector2(b.Center.x + shake, -b.Center.y);
            float flash = b.Deny > 0f ? 0.2f * b.Deny / DenyTime : b.Pop > 0f ? 0.26f * b.Pop / 0.2f : 0f;
            b.Flash.color = b.Deny > 0f ? new Color(1f, 0.25f, 0.2f, flash * 2f) : new Color(1f, 1f, 1f, flash);
            b.Deny = Mathf.Max(0f, b.Deny - dt);
            b.Pop = Mathf.Max(0f, b.Pop - dt);
        }

        void SetIcon(Button b, string id, string fallback)
        {
            if (b.IconId == id) return;
            b.IconId = id;
            Texture2D icon = string.IsNullOrEmpty(id) ? null : KalyeahResources.Texture("ui/hud_icons/" + id);
            b.Icon.texture = icon;
            b.Icon.enabled = icon != null;
            b.Label.text = icon == null && !string.IsNullOrEmpty(id) ? Short(fallback) : "";
        }

        static string Short(string name) => string.IsNullOrEmpty(name) ? "" : name.Length > 8 ? name.Substring(0, 8).ToUpperInvariant() : name.ToUpperInvariant();

        static void SetStrength(Button b, float alpha)
        {
            b.Icon.color = new Color(1f, 1f, 1f, alpha);
            b.Label.color = new Color(1f, 1f, 1f, alpha);
        }

        // ---- touches --------------------------------------------------------------------------------------------------------

        /// <summary>Drops everything held without a cast (a menu opened, the controls went off).</summary>
        public void Cancel()
        {
            if (_heldSlot >= 0)
            {
                _heldSlot = -1;
                _cardPointer = int.MinValue;
                _bridge.CardCancelled();
            }
            if (_primaryPointer != int.MinValue)
            {
                _primaryPointer = int.MinValue;
                _bridge.SetPrimary(false);
            }
            if (_crouchPointer != int.MinValue)
            {
                _crouchPointer = int.MinValue;
                _bridge.SetCrouch(false);
            }
            _overCancel = false;
        }

        void OnDisable() => Cancel();

        /// <summary>A finger's screen point in the arc's own pixels, Godot style (top left, y down).</summary>
        SVector2 Local(PointerEventData data)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, data.position, data.pressEventCamera, out Vector2 local);
            return new SVector2(local.x, -local.y);
        }

        /// <summary>A drag's move in base UI pixels, Godot style (y down).</summary>
        SVector2 Drag(PointerEventData data)
        {
            Canvas canvas = _rect.GetComponentInParent<Canvas>();
            float scale = canvas != null ? Mathf.Max(canvas.scaleFactor, 0.0001f) : 1f;
            return new SVector2(data.delta.x / scale, -data.delta.y / scale);
        }

        internal void Down(ArcTouch touch, PointerEventData data)
        {
            LocalHud h = _bridge.Local;
            if (!h.Playing) return;
            if (touch.Slot >= 0)
            {
                if (_heldSlot >= 0 || !h.Cards[touch.Slot].Present) return;
                if (!h.Cards[touch.Slot].Pickable)
                {
                    _slots[touch.Slot].Deny = DenyTime;
                    return;
                }
                _cardPointer = data.pointerId;
                _heldSlot = touch.Slot;
                _pressAt = Local(data);
                _onCard = true;
                _overCancel = false;
                _bridge.CardPressed(touch.Slot);
                return;
            }
            switch (touch.Hero)
            {
                case ArcLayout.Button.Primary:
                    if (_primaryPointer != int.MinValue) return;
                    if (h.Block.Length > 0) { _primary.Deny = DenyTime; return; }
                    _primaryPointer = data.pointerId;
                    _bridge.SetPrimary(true);
                    break;
                case ArcLayout.Button.Vault:
                    if (h.VaultReady) { _vault.Pop = 0.18f; _bridge.PressDash(); }
                    else _vault.Deny = DenyTime;
                    break;
                case ArcLayout.Button.Ultimate:
                    if (h.UltReady) { _ult.Pop = 0.24f; _bridge.PressUltimate(); }
                    else _ult.Deny = DenyTime;
                    break;
                case ArcLayout.Button.Jump:
                    if (h.JumpReady) { _jump.Pop = 0.16f; _bridge.PressJump(); }
                    else _jump.Deny = DenyTime;
                    break;
                case ArcLayout.Button.Crouch:
                    if (_crouchPointer != int.MinValue) return;
                    if (!h.CanMove) { _crouch.Deny = DenyTime; return; }
                    if (h.CanSlide) { _crouch.Pop = 0.2f; _bridge.PressSlide(); }
                    else
                    {
                        _crouchPointer = data.pointerId;
                        _bridge.SetCrouch(true);
                    }
                    break;
            }
        }

        internal void Move(ArcTouch touch, PointerEventData data)
        {
            if (data.pointerId == _cardPointer && _heldSlot >= 0)
            {
                _camera.Look(Drag(data));
                SVector2 finger = Local(data);
                _overCancel = _layout.OverCancel(finger);
                _onCard = !_overCancel && SVector2.Distance(finger, _pressAt) <= ArcLayout.AimDeadzone;
                _bridge.CardMoved(_heldSlot, _onCard, _overCancel);
            }
            else if (data.pointerId == _primaryPointer)
            {
                _camera.Look(Drag(data));
            }
        }

        internal void Up(ArcTouch touch, PointerEventData data)
        {
            if (data.pointerId == _cardPointer && _heldSlot >= 0)
            {
                SVector2 finger = Local(data);
                _overCancel = _layout.OverCancel(finger);
                _onCard = !_overCancel && SVector2.Distance(finger, _pressAt) <= ArcLayout.AimDeadzone;
                int slot = _heldSlot;
                _heldSlot = -1;
                _cardPointer = int.MinValue;
                _bridge.CardReleased(slot, _onCard, _overCancel);
                _overCancel = false;
            }
            else if (data.pointerId == _primaryPointer)
            {
                _primaryPointer = int.MinValue;
                _bridge.SetPrimary(false);
            }
            else if (data.pointerId == _crouchPointer)
            {
                _crouchPointer = int.MinValue;
                _bridge.SetCrouch(false);
            }
        }

        static Vector2 G(SVector2 v) => new Vector2(v.X, v.Y);
        static Vector4 V4(System.Numerics.Vector4 v) => new Vector4(v.X, v.Y, v.Z, v.W);
    }

    /// <summary>One round button of the arc: round hit test, and the finger's down / drag / up handed to the arc (the drag goes on off it).</summary>
    public sealed class ArcTouch : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler, ICanvasRaycastFilter
    {
        internal AbilityArcView Arc;
        internal int Slot = -1;
        internal ArcLayout.Button Hero;
        internal float Radius;

        public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = false;
        public void OnPointerDown(PointerEventData data) => Arc.Down(this, data);
        public void OnDrag(PointerEventData data) => Arc.Move(this, data);
        public void OnPointerUp(PointerEventData data) => Arc.Up(this, data);

        public bool IsRaycastLocationValid(Vector2 screen, Camera eventCamera)
        {
            var rect = (RectTransform)transform;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, eventCamera, out Vector2 local) && local.magnitude <= Radius;
        }
    }

    /// <summary>The player's comfort settings the match reads (the menus' Settings set them): Reduce motion and screen shake.</summary>
    public static class HudPrefs
    {
        public static bool ReducedMotion;
        public static bool ScreenShake = true;
    }
}
