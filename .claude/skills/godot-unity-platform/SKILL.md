---
name: godot-unity-platform
description: >
  Own Kalyeah's Unity 6 session/settings bridge, IL2CPP-safe WebSocket and LAN
  Hotspot transport, kalyeah_local.androidlib JNI plugin, EnhancedTouch/Tilt
  input, EditMode/.NET 8 test gates, Android arm64 APK, and Kalyeah.Server builds.
---

# Unity 6 & .NET 8 Architecture — Part 3: Platform, Net, Input & APK (`godot-unity-platform`)

Owns `unity/Kalyeah.Net/`, `unity/Kalyeah.Server/`, `unity/Game/Assets/Kalyeah/Scripts/` session/input/net bridges, `Plugins/Android/kalyeah_local.androidlib/`, `Kalyeah.Editor`, and CI/APK build gates.

## Boundary (zero overlap)

- **Owns:** `AppSession` / `SettingsStore` / `I18n` lifecycle, IL2CPP-safe RFC-6455 WebSocket client/server (`WsClientTransport` / `WsServerTransport`) + UDP `HotspotBeacon` + `kalyeah_local.androidlib` JNI (`AndroidLocalLink`), `.NET 8` `Kalyeah.Server` pacing, `EnhancedTouch` + `GravitySensor` -> `PlayerInput` sampling, NUnit/`.NET 8` + `EditMode` test gates, and Android `arm64-v8a` IL2CPP APK + Docker server builds.
- **Defers to:** `godot-unity-sim` (`Kalyeah.Sim` & `unity/Data/*.json`); `godot-unity-view` (URP Mobile world, shaders, rigs, pools); `apex-net-frame` (prediction/reconciliation, `<= 300 B` delta snapshot budget, send-queue backpressure, and frame/GC budgets); `apex-mobile-hud` & `apex-screen-fit` (thumb-zone layout, radial arc UX, and multi-aspect safe areas).

## 1. Bounded session root, Back stack & Android OS lifecycle

- Keep session state in plain C# `AppSession`, `SettingsStore`, `I18n`, and `GameData` (`Kalyeah.Game.Core`, `noEngineReferences: true`) — never scatter `static Instance` `MonoBehaviour` singletons or third-party DI frameworks.
- **Android suspension hook:** On `OnApplicationPause(true)` and `OnApplicationFocus(false)`, clear active touch/aim gesture state on the main thread and immediately flush a neutral stop + `BTN_AIM_CANCEL` `PlayerInput` frame before Android suspends process threads (and re-arm NFC reader mode on resume via `AndroidLocalLink`).
- **Hardware Back stack:** Route Android Back (`Escape` / back action) through `MatchOverlaysView.HandleBack()` in matches and `MenuStack.HandleBack()` in menus (closing sheets/dialogs first, then popping pages, and asking to quit on Home — never calling `Application.Quit()` abruptly).

## 2. IL2CPP-safe WebSocket server, LAN Hotspot & `kalyeah_local.androidlib`

- **Ban `System.Net.HttpListener`:** `HttpListener` is broken under Android IL2CPP. Run `WsServerTransport` directly over `System.Net.Sockets.TcpListener` / `Socket` (`NoDelay = true`) so the exact same `Kalyeah.Net` code powers both:
  1. **`.NET 8` Dedicated Server** (`unity/Kalyeah.Server`, monotonic `30 Hz` loop + HTTP `200 kalyeah` health check), and
  2. **In-app Android Hotspot Host** (`HotspotNet`, authoritative `30 Hz` room server inside the host phone's IL2CPP APK).
- Pump `WsClientTransport` and `WsServerTransport` completions onto the main loop before touching `AppSession` or `MatchSim` (zero ENet/UDP gameplay sockets; `link.xml` preserves `Kalyeah.Sim` + `Kalyeah.Net` under IL2CPP).
- Pair UDP broadcast `HotspotBeacon` discovery (verifying active Kalyeah room over WebSocket — never hardcoding `.1` gateway IPs, and classifying vendor interfaces `ap0`/`wlan1`/`swlan0`/`rndis0` cleanly) with pure Java `kalyeah_local.androidlib` (`AndroidJavaProxy` in `AndroidLocalLink.cs`) for `LocalOnlyHotspot`, `WifiNetworkSpecifier`, Wi-Fi QR (`WifiQr`), and NFC Tap-to-Join HCE/Reader.

## 3. Mobile touch & tilt input (`EnhancedTouch` + `GravitySensor`)

- Enable `EnhancedTouchSupport` and sample `Touch.activeTouches` by `fingerId` into `PlayerInput` (`Move`, `MoveTarget`, `AimPoint`, button bitmask, `Tick`), keeping `#if UNITY_EDITOR || DEVELOPMENT_BUILD` `PcCombatInput` strictly isolated with zero PC prompts on the mobile HUD.
- Run native `sensorLandscape` (`1280×720` base, `844×390` narrow-phone safe) and feed dynamic `Screen.safeArea` insets on every resize/rotation (`apex-screen-fit`).
- Drive opt-in `TiltInput` via `GravitySensor.current`: calibrate neutral hold on resume, apply an `8°` deadzone, emit direction-only move targets (never tilt-scaled speed), and yield immediately to active touch.

## 4. `.NET 8`, `EditMode` & Android IL2CPP APK build gates

- Run `dotnet test unity/Kalyeah.sln` + `python tools/unity_export/sync_assets.py` + Unity `EditMode` tests + `KalyeahBuild.Screenshots` (`1280×720` & `844×390`) before opening a PR.
- Keep `KalyeahBuild.BuildAndroid` + `KalyeahAndroidManifest` aligned with `.github/scripts/inspect-apk.sh`: IL2CPP `arm64-v8a` today (`libil2cpp.so` + `libunity.so`; `armeabi-v7a` is planned, `docs/SPEC.md` §2), `16 KB` page alignment, `sensorLandscape`, `allowBackup="false"`, and the exact 12 declared permissions (including `NFC`).

## Anti-patterns to reject

- Using `System.Net.HttpListener` on Android IL2CPP or leaving `Kalyeah.Server` uncapped at `100%` CPU.
- Mutating `AppSession` or `MatchSim` from background socket callbacks instead of main-thread queues.
- Sending client positions/hits over the wire, exposing PC key labels on mobile HUD, or adding unapproved Android permissions.
