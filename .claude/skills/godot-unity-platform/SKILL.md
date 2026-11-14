---
name: godot-unity-platform
description: >
  Port Kalyeah's autoloads (Net, Session, Settings), authoritative WebSocket and
  LAN Hotspot networking, touch/tilt input, GUT tests, and Android arm64 APK
  pipeline into Unity 6 and Linux headless Dedicated Server.
---

# Godot-to-Unity Migration — Part 3: Platform, Net, Input & APK (`godot-unity-platform`)

Owns porting Godot autoloads (`Net`, `Session`, `Settings`), WebSocket + LAN Hotspot transport, touch/tilt input, GUT-to-UTF test suites, and Android/Linux build gates into Unity 6.

## Boundary (zero overlap)

- **Owns:** Single Bootstrap composition root, WebSocket client/server transport + `20 Hz` snapshot wire serializer, UDP LAN Hotspot discovery + `kalyeah_local` Android bridge, `EnhancedTouch` + `GravitySensor` -> `PlayerInput` sampling, NUnit `EditMode`/`PlayMode` test harness, and Android `arm64-v8a` APK + Linux headless server builds.
- **Defers to:** `godot-unity-sim` for `Kalyeah.Sim` gameplay rules and `ScriptableObject` data schemas; `godot-unity-view` for URP Mobile Prefabs, shaders, `.glb` rigs, and visual pools.

## 1. Autoloads -> single Bootstrap composition root

- Replace Godot's three allowed globals (`Net`, `Session`, `Settings`) — and nothing else global — with one `Bootstrap` startup scene owning a single `DontDestroyOnLoad` `AppServices` root that wires plain C# `NetService`, `SessionState`, and `SettingsStore` into screen/match controllers.
- Do not add scattered `static Instance` singletons, third-party DI containers, or extra global managers.
- Handle Android lifecycle interruptions (`OnApplicationPause`, `OnApplicationFocus(false)`) synchronously: cancel active touch gestures/held cards and immediately flush a stop + `BTN_AIM_CANCEL` `PlayerInput` packet to the server before suspension.
- Map Android hardware Back (`Key.Escape` on Android) to match-settings toggle in matches, dialog/page pop in menus, and quit confirmation on Home — never hard-quitting abruptly.

## 2. Authoritative WebSocket server & LAN Hotspot

- Preserve the exact network contract: **WebSocket** gameplay transport (zero ENet/UDP gameplay sockets), protocol version check at handshake, server-authoritative `30 Hz` sim, quantized `20 Hz` delta snapshots, and client-to-server `PlayerInput` frames serialized into pooled byte buffers (`Span<byte>`, zero per-packet GC).
- Run one shared C# transport/room assembly across all three hosts:
  1. **Dedicated Server** (`Linux64` Unity Dedicated Server build target, `-batchmode -nographics`),
  2. **In-app Android Hotspot Host** (local authoritative room server inside the host phone's APK), and
  3. **Android Client** (`8`-char alphanumeric Online room codes + QR scan, plus code-free Local Hotspot join).
- Keep UDP broadcast `Beacon` LAN discovery (host answers broadcast and client verifies Kalyeah protocol + active room over WebSocket — never hardcoding `.1` gateway IP on stock Android) and JNI `AndroidJavaObject` calls to `android/kalyeah_local` for Wi-Fi QR and NFC join.

## 3. Touch-only input (`EnhancedTouch` + `GravitySensor`)

- Build gameplay and menu input exclusively on Unity Input System `EnhancedTouchSupport` — **no mouse, keyboard, or desktop gameplay paths**.
- Sample active touches deterministically each tick into `PlayerInput` (`Move`, `MoveTarget`, `AimPoint`, button bitmask, `Tick`):
  - **Landscape match (`1280×720` base, usable at `844×390`):** left-thumb movement stick, right-side shoulder camera drag, right-thumb ability arc with drag-to-aim, `MapLayout` out-of-bounds/hand-drop cancel (`BTN_AIM_CANCEL`), and quick-tap auto-aim within `card_tap_time` (`0.3 s`, target locked at press).
  - **Portrait menus (`720×1280` base, usable at `390×844`):** switch `Screen.orientation` cleanly on match enter/exit until menus flip to landscape; enforce `Screen.safeArea` notch/home-bar insets on every edge.
  - **Opt-in Tilt (`GravitySensor`):** calibrate neutral hold when controls resume, apply `8°` deadzone, emit direction-only move targets (never tilt-scaled speed), and yield immediately to any active street touch.

## 4. GUT -> Unity Test Framework & Android APK gate

| Godot gate | Unity 6 gate |
|---|---|
| `tests/unit/test_*.gd` (headless GUT) | Fast NUnit `EditMode` tests (`Kalyeah.Tests.EditMode`, zero scene load) |
| `tests/integration/` & touch/UI checks | `PlayMode` tests (`InputTestFixture` simulated `Touchscreen`, no mouse shims) |
| `Android APK` workflow + `inspect-apk.sh` | IL2CPP `arm64-v8a` APK (`minSdkVersion 26`), `16 KB` page-aligned `.so`, compressed native libs, ASTC/ETC2 VRAM textures, minimal `INTERNET` + `VIBRATE` (+ `kalyeah_local` Wi-Fi/NFC permissions) |

- Run `EditMode` + `PlayMode` suites headlessly before opening any PR, verify the `Android` preset builds without desktop/editor-only APIs, and list phone-only verification steps (touch feel, safe areas, thermals, physical Hotspot/QR/NFC) in the PR body.

## Anti-patterns to reject

- Sending client position, hit claims, cooldowns, or deck state over the wire instead of `PlayerInput`.
- Using reflection/JSON string serialization per `20 Hz` snapshot instead of compact binary bitpacking.
- Adding `Standalone`/WebGL export paths, mouse/WASD fallbacks, or unapproved Android manifest permissions.
