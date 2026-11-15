using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using SVector2 = System.Numerics.Vector2;

namespace Kalyeah.Game
{
    /// <summary>
    /// Editor &amp; Development build keyboard + mouse 3C playtest input translator (port of scripts/input/pc_combat_input.gd).
    /// Coexists cleanly with mobile touch controls (VirtualJoystick, LookPad, AbilityArcView, CardHandView):
    /// ignores touch-emulated mouse events, never adds PC keybind labels to the mobile HUD, and exposes an opt-in
    /// F1 runtime stats readout created only when toggled on.
    /// </summary>
    public sealed class PcCombatInput : IDisposable
    {
        public const float WalkScale = 0.45f;
        public const float RunSlideThreshold = 0.62f;
        public const float WasdCurveSmoothRate = 15.5f;
        public const float WasdCounterstrafeRate = 28.0f;
        const float DebugRefreshInterval = 0.25f;

        readonly List<RaycastResult> _uiHits = new List<RaycastResult>(8);
        PointerEventData _cachedPointerEvent;

        Vector2 _smoothedStick;
        bool _wasdActive;
        int _heldSlot = -1;
        Key _heldKey = Key.None;
        bool _slotPressObservedBySim;
        int _pendingReleaseSlot = -1;
        float _pendingReleaseMaxRange = 16f;
        bool _pendingReleaseCancel;
        int _latchedPulseButtons;
        bool _latchedAimCancel;
        int _lastSampledSimTick = int.MinValue;

        RectTransform _debugPanel;
        Text _debugText;
        float _debugRefreshTimer;
        float _smoothedFrameDt = 1f / 60f;
        bool _disposed;

        public bool RmbHeld { get; private set; }
        public bool LmbHeld { get; private set; }
        public bool MouseCaptured { get; private set; }
        public bool IsDebugStatsVisible { get; private set; }
        public Vector2 LastStick { get; private set; }
        public int HeldSlot => _heldSlot;

        /// <summary>
        /// Polls keyboard &amp; mouse playtest controls and merges them into <paramref name="touchStick"/> and <paramref name="ability"/>
        /// without overwriting active mobile touch inputs.
        /// </summary>
        public void Sample(
            SimBridge sim,
            MatchHud hud,
            ShoulderCamera shoulder,
            Camera worldCamera,
            MapLayoutData layout,
            float screenHeight,
            float dt,
            ref Vector2 touchStick,
            ref AbilityInputState ability)
        {
            if (_disposed) return;

            float frameDt = Mathf.Clamp(dt > 0f ? dt : Time.unscaledDeltaTime, 0.0005f, 0.1f);
            _smoothedFrameDt = Mathf.Lerp(_smoothedFrameDt, frameDt, 0.12f);

            Keyboard kb = null;
            Mouse mouse = null;
            try
            {
                kb = Keyboard.current;
                mouse = Mouse.current;
            }
            catch (Exception)
            {
                return;
            }

            if (kb != null && kb.f1Key.wasPressedThisFrame)
                SetDebugStatsVisible(!IsDebugStatsVisible, hud != null ? hud.SafeRoot : null);

            if (IsDebugStatsVisible)
                TickDebugStats(sim, hud != null ? hud.SafeRoot : null, frameDt);

            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (_heldSlot >= 0)
                {
                    _heldSlot = -1;
                    _heldKey = Key.None;
                    _pendingReleaseSlot = -1;
                    _latchedAimCancel = true;
                }
                SetMouseCaptured(false);
                RmbHeld = false;
                SyncCursorState();
            }

            bool blocked = hud == null || (hud.Overlays != null && hud.Overlays.IsBlockingGameplayInput);
            if (blocked)
            {
                Reset(releaseCursor: true);
                return;
            }

            if (kb != null && kb.tabKey.wasPressedThisFrame)
                hud.Minimap?.Toggle();

            if (kb != null && kb.backquoteKey.wasPressedThisFrame)
            {
                SetMouseCaptured(!MouseCaptured);
                SyncCursorState();
            }

            bool touchActive = HasActiveTouches();

            // 1. Mouse look (RMB drag or backquote cursor-capture toggle; ignores touch-emulated pointers)
            if (mouse != null && !touchActive)
            {
                bool rmbNow = mouse.rightButton.isPressed;
                if (rmbNow != RmbHeld)
                {
                    RmbHeld = rmbNow;
                    SyncCursorState();
                }

                if (RmbHeld || MouseCaptured)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    if (delta.sqrMagnitude > 0.0001f)
                        shoulder?.Look(delta, Mathf.Max(1f, screenHeight));
                }
            }
            else if (RmbHeld)
            {
                RmbHeld = false;
                SyncCursorState();
            }

            // 2. WASD movement (+ Alt walk 0.45x), yielding whenever mobile touch VirtualJoystick is active
            Vector2 rawWasd = ReadWasd(kb, out bool altWalk);
            if (rawWasd.sqrMagnitude > 0.0001f)
            {
                bool startingHold = !_wasdActive;
                _wasdActive = true;
                _smoothedStick = SmoothWasd(_smoothedStick, rawWasd, frameDt, startingHold, SettingsStore.Current.ReduceMotion);
                LastStick = _smoothedStick;
                if (touchStick.sqrMagnitude < 0.0025f)
                    touchStick = _smoothedStick;
            }
            else if (_wasdActive)
            {
                _wasdActive = false;
                _smoothedStick = Vector2.zero;
                LastStick = Vector2.zero;
            }

            // 3. Sim tick advance check so sub-tick key taps are never dropped at 60..240 FPS
            int simTick = sim != null ? sim.Tick : (_lastSampledSimTick + 1);
            bool tickAdvanced = simTick != _lastSampledSimTick;
            if (tickAdvanced)
                _lastSampledSimTick = simTick;

            KidSnapshot? localOpt = sim?.LocalKid;
            bool canAct = sim != null && sim.Phase == MatchPhaseKind.Playing && localOpt.HasValue && !localOpt.Value.Tagged;
            bool cardsMode = localOpt.HasValue && localOpt.Value.CardsMode;

            // 4. Locomotion & ability buttons
            bool crouchHeld = false;
            if (kb != null && canAct)
            {
                if (kb.spaceKey.wasPressedThisFrame)
                    _latchedPulseButtons |= InputButtons.Jump;

                if (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame)
                    _latchedPulseButtons |= InputButtons.Dash;

                bool crouchPressedNow = kb.cKey.wasPressedThisFrame || kb.leftCtrlKey.wasPressedThisFrame;
                crouchHeld = kb.cKey.isPressed || kb.leftCtrlKey.isPressed;
                if (crouchPressedNow && !altWalk && touchStick.magnitude >= RunSlideThreshold)
                    _latchedPulseButtons |= InputButtons.Slide;

                bool ultPressed = kb.fKey.wasPressedThisFrame ||
                                  (!cardsMode && (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame));
                if (ultPressed)
                    _latchedPulseButtons |= InputButtons.Ultimate;

                PollSuperpowerKeys(kb, sim, localOpt, cardsMode);
            }
            else
            {
                CancelHeldSlot();
            }

            // 5. LMB Primary Tirador (blocked when clicking unlocked UGUI buttons)
            if (mouse != null && !touchActive && canAct)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    bool overUi = !MouseCaptured && !RmbHeld && IsPointerOverInteractiveUi(mouse.position.ReadValue());
                    LmbHeld = !overUi;
                }
                else if (!mouse.leftButton.isPressed)
                {
                    LmbHeld = false;
                }
            }
            else
            {
                LmbHeld = false;
            }

            // 6. Merge PC combat state into AbilityInputState without clobbering active touch casts
            SVector2 kidFeet = localOpt.HasValue ? localOpt.Value.Position : SVector2.Zero;
            SVector2 kidFacing = localOpt.HasValue ? localOpt.Value.Facing : new SVector2(0f, -1f);
            float originH = (localOpt.HasValue && (localOpt.Value.Crouching || localOpt.Value.Sliding))
                ? AimSolver.CrouchedOriginHeight
                : AimSolver.DefaultOriginHeight;

            if (crouchHeld)
                ability.Buttons |= InputButtons.Crouch;

            if (_latchedPulseButtons != 0)
            {
                ability.Buttons |= _latchedPulseButtons;
                if ((_latchedPulseButtons & InputButtons.Ultimate) != 0 && sim != null && sim.UltimateWeaponInfo.HasValue && !ability.HasAimPoint)
                {
                    AimSolution uSol = AimSolver.Solve(worldCamera, kidFeet, kidFacing, sim.UltimateWeaponInfo.Value.MaxRange, layout, originH);
                    ability.Aim = uSol.SimAimDirection;
                    ability.AimPoint = uSol.SimAimPoint;
                    ability.HasAimPoint = true;
                }
                if (tickAdvanced)
                    _latchedPulseButtons = 0;
            }

            bool touchOwnsSlot = ability.HeldSlot >= 0 || ability.IsDraggingAim;
            if (!touchOwnsSlot)
            {
                if (_heldSlot >= 0)
                {
                    ability.Buttons |= InputButtons.CardSlotBit(_heldSlot);
                    if (tickAdvanced)
                        _slotPressObservedBySim = true;

                    string wid = ResolveSlotWeaponId(localOpt, _heldSlot);
                    float maxRange = ResolveWeaponRange(sim, wid, 16f);
                    AimSolution sol = AimSolver.Solve(worldCamera, kidFeet, kidFacing, maxRange, layout, originH);
                    ability.Aim = sol.SimAimDirection;
                    ability.AimPoint = sol.SimAimPoint;
                    ability.HasAimPoint = true;
                    ability.PreviewWeaponId = wid;
                    ability.HeldSlot = _heldSlot;
                    ability.IsDraggingAim = true;
                }
                else if (_pendingReleaseSlot >= 0)
                {
                    if (!_slotPressObservedBySim)
                    {
                        ability.Buttons |= InputButtons.CardSlotBit(_pendingReleaseSlot);
                        if (tickAdvanced)
                            _slotPressObservedBySim = true;
                    }
                    else
                    {
                        if (_pendingReleaseCancel)
                        {
                            ability.Buttons |= InputButtons.AimCancel;
                        }
                        else
                        {
                            AimSolution sol = AimSolver.Solve(worldCamera, kidFeet, kidFacing, _pendingReleaseMaxRange, layout, originH);
                            ability.Aim = sol.SimAimDirection;
                            ability.AimPoint = sol.SimAimPoint;
                            ability.HasAimPoint = true;
                        }

                        if (tickAdvanced)
                        {
                            _pendingReleaseSlot = -1;
                            _slotPressObservedBySim = false;
                            _pendingReleaseCancel = false;
                        }
                    }
                }
                else if (LmbHeld && (ability.Buttons & InputButtons.Primary) == 0)
                {
                    ability.Buttons |= InputButtons.Primary;
                    float pRange = sim != null && sim.PrimaryWeaponInfo.HasValue ? sim.PrimaryWeaponInfo.Value.MaxRange : 18f;
                    AimSolution pSol = AimSolver.Solve(worldCamera, kidFeet, kidFacing, pRange, layout, originH);
                    ability.Aim = pSol.SimAimDirection;
                    ability.AimPoint = pSol.SimAimPoint;
                    ability.HasAimPoint = true;
                }
            }

            if (_latchedAimCancel)
            {
                ability.Buttons |= InputButtons.AimCancel;
                if (tickAdvanced)
                    _latchedAimCancel = false;
            }
        }

        public void SetMouseCaptured(bool captured)
        {
            MouseCaptured = captured;
            SyncCursorState();
        }

        public void SetDebugStatsVisible(bool visible, Transform parent = null)
        {
            IsDebugStatsVisible = visible;
            if (!visible)
            {
                if (_debugPanel != null)
                    _debugPanel.gameObject.SetActive(false);
                return;
            }

            EnsureDebugPanel(parent);
            if (_debugPanel != null)
            {
                _debugPanel.gameObject.SetActive(true);
                _debugRefreshTimer = 0f;
            }
        }

        public void Reset(bool releaseCursor = true)
        {
            LmbHeld = false;
            RmbHeld = false;
            _wasdActive = false;
            _smoothedStick = Vector2.zero;
            LastStick = Vector2.zero;
            _latchedPulseButtons = 0;
            _latchedAimCancel = false;
            CancelHeldSlot();

            if (releaseCursor)
            {
                MouseCaptured = false;
                SyncCursorState();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Reset(releaseCursor: true);
            if (_debugPanel != null)
            {
                KalyeahResources.Kill(_debugPanel.gameObject);
                _debugPanel = null;
                _debugText = null;
            }
        }

        /// <summary>Smooth octant-curve filter &amp; fast counter-strafe response matching pc_combat_input.gd.</summary>
        public static Vector2 SmoothWasd(Vector2 current, Vector2 target, float dt, bool snapNow, bool reduceMotion)
        {
            if (snapNow || reduceMotion || current.sqrMagnitude <= 0.0001f || dt <= 0f)
                return target;

            float targetLen = target.magnitude;
            if (targetLen <= 0.0001f)
                return Vector2.zero;

            Vector2 targetDir = target / targetLen;
            float curLen = current.magnitude;
            Vector2 curDir = current / Mathf.Max(curLen, 0.0001f);

            if (Vector2.Dot(curDir, targetDir) < -0.5f)
            {
                float fastW = 1f - Mathf.Exp(-dt * WasdCounterstrafeRate);
                return Vector2.Lerp(current, target, fastW);
            }

            float curveW = 1f - Mathf.Exp(-dt * WasdCurveSmoothRate);
            float curAngle = Mathf.Atan2(curDir.y, curDir.x) * Mathf.Rad2Deg;
            float tgtAngle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;
            float nextRad = Mathf.LerpAngle(curAngle, tgtAngle, curveW) * Mathf.Deg2Rad;
            float nextLen = Mathf.Lerp(curLen, targetLen, curveW);
            return new Vector2(Mathf.Cos(nextRad), Mathf.Sin(nextRad)) * nextLen;
        }

        public static int SlotForKey(Key key, bool cardsMode = false)
        {
            switch (key)
            {
                case Key.Digit1:
                case Key.Numpad1:
                case Key.Q:
                    return 0;
                case Key.Digit2:
                case Key.Numpad2:
                case Key.E:
                    return 1;
                case Key.Digit3:
                case Key.Numpad3:
                case Key.R:
                    return 2;
                case Key.Digit4:
                case Key.Numpad4:
                    return cardsMode ? 3 : -1;
                default:
                    return -1;
            }
        }

        void PollSuperpowerKeys(Keyboard kb, SimBridge sim, KidSnapshot? localOpt, bool cardsMode)
        {
            ReadOnlySpan<Key> keys = stackalloc Key[]
            {
                Key.Digit1, Key.Numpad1, Key.Q,
                Key.Digit2, Key.Numpad2, Key.E,
                Key.Digit3, Key.Numpad3, Key.R,
                Key.Digit4, Key.Numpad4,
            };

            if (_heldSlot >= 0 && _heldKey != Key.None)
            {
                var heldControl = kb[_heldKey];
                if (heldControl == null || !heldControl.isPressed)
                {
                    string wid = ResolveSlotWeaponId(localOpt, _heldSlot);
                    _pendingReleaseSlot = _heldSlot;
                    _pendingReleaseMaxRange = ResolveWeaponRange(sim, wid, 16f);
                    _pendingReleaseCancel = false;
                    _heldSlot = -1;
                    _heldKey = Key.None;
                }
                return;
            }

            if (_pendingReleaseSlot >= 0)
                return;

            for (int i = 0; i < keys.Length; i++)
            {
                Key k = keys[i];
                int slot = SlotForKey(k, cardsMode);
                if (slot < 0) continue;

                var ctrl = kb[k];
                if (ctrl == null || !ctrl.wasPressedThisFrame) continue;

                if (!IsSlotReady(localOpt, slot))
                    continue;

                _heldSlot = slot;
                _heldKey = k;
                _slotPressObservedBySim = false;
                _pendingReleaseSlot = -1;
                _pendingReleaseCancel = false;
                break;
            }
        }

        static bool IsSlotReady(KidSnapshot? localOpt, int slot)
        {
            if (!localOpt.HasValue) return false;
            KidSnapshot kid = localOpt.Value;
            if (kid.CastingBlocked || kid.HandWeaponIds == null || slot < 0 || slot >= kid.HandCount || slot >= kid.HandWeaponIds.Length)
                return false;
            if (string.IsNullOrEmpty(kid.HandWeaponIds[slot]))
                return false;
            if (kid.SlotCooldowns != null && slot < kid.SlotCooldowns.Length && kid.SlotCooldowns[slot] > 0.01f)
                return false;
            if (kid.CardsMode && kid.SlotElixirCosts != null && slot < kid.SlotElixirCosts.Length && kid.Elixir + 0.001f < kid.SlotElixirCosts[slot])
                return false;
            return true;
        }

        static string ResolveSlotWeaponId(KidSnapshot? localOpt, int slot)
        {
            if (!localOpt.HasValue) return "";
            KidSnapshot kid = localOpt.Value;
            if (kid.HandWeaponIds == null || slot < 0 || slot >= kid.HandWeaponIds.Length)
                return "";
            return kid.HandWeaponIds[slot] ?? "";
        }

        static float ResolveWeaponRange(SimBridge sim, string weaponId, float fallback)
        {
            if (sim == null || string.IsNullOrEmpty(weaponId)) return fallback;
            WeaponInfo? info = sim.GetWeaponInfo(weaponId);
            return (info.HasValue && info.Value.MaxRange > 0.1f) ? info.Value.MaxRange : fallback;
        }

        void CancelHeldSlot()
        {
            _heldSlot = -1;
            _heldKey = Key.None;
            _pendingReleaseSlot = -1;
            _slotPressObservedBySim = false;
            _pendingReleaseCancel = false;
        }

        static Vector2 ReadWasd(Keyboard kb, out bool altWalk)
        {
            altWalk = false;
            if (kb == null) return Vector2.zero;

            altWalk = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
            float x = 0f;
            float y = 0f;
            if (kb.aKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed) x += 1f;
            if (kb.wKey.isPressed) y += 1f;
            if (kb.sKey.isPressed) y -= 1f;

            Vector2 way = new Vector2(x, y);
            if (way.sqrMagnitude <= 0.0001f)
                return Vector2.zero;

            if (way.sqrMagnitude > 1f)
                way.Normalize();
            return altWalk ? way * WalkScale : way;
        }

        static bool HasActiveTouches()
        {
            try
            {
                if (EnhancedTouchSupport.enabled && ETouch.activeTouches.Count > 0)
                    return true;
                Touchscreen ts = Touchscreen.current;
                return ts != null && ts.primaryTouch.press.isPressed;
            }
            catch (Exception)
            {
                return false;
            }
        }

        bool IsPointerOverInteractiveUi(Vector2 screenPos)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;

            if (_cachedPointerEvent == null)
                _cachedPointerEvent = new PointerEventData(es);
            else
                _cachedPointerEvent.Reset();
            _cachedPointerEvent.position = screenPos;

            _uiHits.Clear();
            es.RaycastAll(_cachedPointerEvent, _uiHits);
            for (int i = 0; i < _uiHits.Count; i++)
            {
                GameObject hitGo = _uiHits[i].gameObject;
                if (hitGo == null) continue;
                if (hitGo.GetComponentInParent<Selectable>() != null)
                    return true;
                // AbilityArcView & CardHandView slots implement IPointerDownHandler directly
                if (hitGo.GetComponentInParent<AbilityArcView>() != null || hitGo.GetComponentInParent<CardHandView>() != null)
                    return true;
            }
            return false;
        }

        void SyncCursorState()
        {
            if (Application.isMobilePlatform || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return;

            bool lockNow = RmbHeld || MouseCaptured;
            CursorLockMode targetMode = lockNow ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != targetMode)
                Cursor.lockState = targetMode;
            Cursor.visible = !lockNow;
        }

        void EnsureDebugPanel(Transform parent)
        {
            if (_debugPanel != null)
            {
                if (parent != null && _debugPanel.parent != parent)
                    _debugPanel.SetParent(parent, false);
                return;
            }
            if (parent == null) return;

            _debugPanel = Ui.Panel(
                "PcDebugStats",
                parent,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(12f, -162f),
                new Vector2(228f, 54f));
            var group = _debugPanel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            Image bg = Ui.MakeImage("Bg", _debugPanel, new Color(0.03f, 0.05f, 0.10f, 0.80f), raycast: false);
            bg.rectTransform.anchorMin = Vector2.zero;
            bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.sizeDelta = Vector2.zero;

            _debugText = Ui.MakeText("Stats", _debugPanel, "", 12, new Color(0.72f, 0.96f, 0.84f, 0.98f), TextAnchor.MiddleLeft);
            _debugText.raycastTarget = false;
            _debugText.rectTransform.anchorMin = Vector2.zero;
            _debugText.rectTransform.anchorMax = Vector2.one;
            _debugText.rectTransform.offsetMin = new Vector2(8f, 4f);
            _debugText.rectTransform.offsetMax = new Vector2(-8f, -4f);
        }

        void TickDebugStats(SimBridge sim, Transform parent, float dt)
        {
            EnsureDebugPanel(parent);
            if (_debugPanel == null || _debugText == null) return;

            _debugRefreshTimer -= dt;
            if (_debugRefreshTimer > 0f) return;
            _debugRefreshTimer = DebugRefreshInterval;

            int fps = Mathf.Clamp(Mathf.RoundToInt(1f / Mathf.Max(_smoothedFrameDt, 0.0005f)), 1, 999);
            int tick = sim != null ? sim.Tick : 0;
            string mode = AppSession.ModeKey(AppSession.Current.Mode).ToUpperInvariant();
            var urp = (QualitySettings.renderPipeline as UniversalRenderPipelineAsset)
                      ?? (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset);
            float rs = urp != null ? urp.renderScale : (SettingsStore.Current.IsTurboGraphics ? SettingsStore.TurboRenderScale : 1f);

            _debugText.text = string.Concat(
                "FPS ", fps.ToString(CultureInfo.InvariantCulture),
                " · TICK ", tick.ToString(CultureInfo.InvariantCulture),
                "\n", mode,
                " · SCALE ", rs.ToString("0.00", CultureInfo.InvariantCulture), "x");
        }
    }
}
