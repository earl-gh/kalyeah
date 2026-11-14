---
name: godot-unity-platform
description: >
  Port Kalyeah's autoloads (Net, Session, Settings), IL2CPP-safe WebSocket and
  LAN Hotspot hosts, EnhancedTouch/GravitySensor input, GUT tests, and Android
  arm64 APK + Linux Dedicated Server builds into Unity 6.
---

# Godot-to-Unity Migration — Part 3: Platform, Net, Input & APK (`godot-unity-platform`)

Owns translating Godot autoloads (`Net`, `Session`, `Settings`), WebSocket + LAN Hotspot transport, touch/tilt input, GUT-to-UTF test runners, and Android/Linux build pipelines into Unity 6.

## Boundary (zero overlap)

- **Owns:** Single `Bootstrap` composition root, IL2CPP-safe RFC-6455 WebSocket server/client + UDP `Beacon` + `kalyeah_local` JNI bridge, `Linux64` `-batchmode` tick pacing, `EnhancedTouch` + `GravitySensor` -> `PlayerInput` sampling, NUnit `EditMode`/`PlayMode` test harness, and Android `arm64-v8a` APK + `Linux64` Dedicated Server builds.
- **Defers to:** `godot-unity-sim` (`Kalyeah.Sim` & `SimConfig`); `godot-unity-view` (URP Mobile Prefabs, shaders, rigs, pools); `apex-net-frame` (client prediction/reconciliation, `<= 300 B` delta snapshot budget, send-queue backpressure, and frame/GC budgets); `apex-mobile-hud` (thumb-zone layout, radial ability arc UX, and safe-area visual ergonomics).

## 1. Autoloads -> single `Bootstrap` root & Android OS lifecycle

- Replace Godot's three allowed globals (`Net`, `Session`, `Settings`) — and nothing else global — with one `Bootstrap` startup scene owning a single `DontDestroyOnLoad` `AppServices` root that injects plain C# `NetService`, `SessionState`, and `SettingsStore`.
- Never introduce scattered `static Instance` singletons, third-party DI frameworks, or extra global managers.
- **Android suspension hook:** Handle `OnApplicationPause(true)` and `OnApplicationFocus(false)` synchronously on the Unity main thread — clear active `EnhancedTouch` gesture state and immediately write/flush a stop + `BTN_AIM_CANCEL` `PlayerInput` frame to the socket before Android suspends process threads.
- Map Android hardware Back (`Keyboard.current.escapeKey` / back action) to match-settings toggle in matches, dialog/page pop in menus, and quit confirmation on Home — never calling `Application.Quit()` abruptly mid-screen.

## 2. IL2CPP-safe WebSocket server, LAN Hotspot & headless CPU cap

- **Ban `System.Net.HttpListener`:** `HttpListener` is unsupported/broken under Android IL2CPP. Implement one shared managed RFC-6455 WebSocket handshake + binary framing server directly over `System.Net.Sockets.TcpListener` / `Socket` (`NoDelay = true`) so the exact same C# transport assembly powers:
  1. **`Linux64` Dedicated Server** (`-batchmode -nographics`), and
  2. **In-app Android Hotspot Host** (authoritative `30 Hz` room server running inside the host phone's IL2CPP APK).
- Pair `ClientWebSocket` (Online rooms + Hotspot clients) and server sockets with a preallocated **main-thread ring-buffer pump** (`Span<byte>` framing, protocol-version handshake check, zero ENet/UDP gameplay sockets).
- Guard Android IL2CPP `ClientWebSocket` keepalive/TLS quirks and marshal all async socket completions onto the Unity main loop before touching `SessionState` or `MatchSim` (`apex-net-frame` owns snapshot quantization, reconciliation, and send-queue backpressure).
- Keep UDP broadcast `Beacon` LAN discovery (client verifies Kalyeah protocol + active room over WebSocket — never hardcoding `.1` gateway IP on stock Android) and JNI `AndroidJavaObject` bindings to `android/kalyeah_local` for Wi-Fi QR and NFC join.
- **Cap `Linux64` headless CPU spin:** Unity `-batchmode -nographics` ignores vSync and spins an uncapped loop at `1000+ FPS` (`100%` vCPU) unless throttled; explicitly set `QualitySettings.vSyncCount = 0` and `Application.targetFrameRate = 30` (or drive `MatchSim.Step` via a monotonic `Stopwatch` sleep loop) so dedicated rooms pace cleanly at `30 Hz`.

## 3. Touch-only input sampling (`EnhancedTouch` + `GravitySensor`)

- Enable `EnhancedTouchSupport` at bootstrap and bind zero mouse, keyboard, or gamepad gameplay actions.
- Sample `Touch.activeTouches` by `fingerId` once per sim tick into `PlayerInput` (`Move`, `MoveTarget`, `AimPoint`, button bitmask, `Tick`), including `MapLayout` out-of-bounds/hand-drop `BTN_AIM_CANCEL` and `card_tap_time` (`0.3 s`, target locked at press) — deferring HUD layout and radial arc ergonomics to `apex-mobile-hud`.
- Switch `Screen.orientation` cleanly between Portrait menus (`720×1280` / `390×844`) and Landscape match (`1280×720` / `844×390`), feeding `Screen.safeArea` insets to UI roots on orientation/resolution change.
- Drive opt-in Tilt via `InputSystem.EnableDevice(GravitySensor.current)`: calibrate neutral hold when controls resume, apply an `8°` deadzone, emit direction-only move targets (never tilt-scaled speed), and yield immediately to any active street touch.

## 4. GUT -> Unity Test Framework & Android/Linux build gates

| Godot 4.6 gate | Unity 6 gate |
|---|---|
| `tests/unit/test_*.gd` (headless GUT) | Fast NUnit `EditMode` tests (`Kalyeah.Tests.EditMode`, zero scene load) |
| `tests/integration/` & touch/UI checks | `PlayMode` tests (`InputTestFixture` simulated `Touchscreen`, zero mouse shims) |
| `Android APK` workflow + `inspect-apk.sh` | IL2CPP `arm64-v8a` APK (`minSdkVersion 26`), `16 KB` page-aligned `.so`, compressed native libs, minimal `INTERNET` + `VIBRATE` (+ `kalyeah_local` Wi-Fi/NFC permissions) |
| Headless server check | `Linux64` Dedicated Server build (`-batchmode -nographics`, capped `30 Hz` loop) |

- Run `EditMode` + `PlayMode` suites headlessly before opening a PR, verify both `Android` and `Linux64` Dedicated Server targets compile without editor/desktop-only APIs, and list phone-only checks (touch feel, safe areas, thermals, Hotspot/QR/NFC) in the PR body.

## Anti-patterns to reject

- Using `System.Net.HttpListener` for the Android Hotspot WebSocket host or leaving `Linux64` `-batchmode` uncapped at `100%` CPU.
- Mutating `SessionState` or `MatchSim` directly from background `Socket`/`ClientWebSocket` thread-pool callbacks instead of pumping a main-thread ring buffer.
- Sending client positions/hits over the wire, or adding `Standalone`/WebGL targets, mouse/WASD fallbacks, or unapproved Android permissions.
