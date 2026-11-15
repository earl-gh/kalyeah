# CLAUDE.md — rules for coding agents in this repo

@docs/DOS_AND_DONTS.md

Read first (with surgical focus): `docs/ROADMAP.md` (what is done, what is next, how to build), `docs/GDD.md` (rules + numbers), `docs/PROJECT.md` (scope) — slice-reading only the sections relevant to your task.
**Android is the only client**: the game is played and tested as an installed **APK**. There is no web or desktop build. The owner works from a phone in cloud sessions and can't run the editor, so every change must be checkable in the **APK that the `Unity 6 CI` workflow builds** (Actions → Unity 6 CI → the run on `main`, or the run you start on a branch → artifacts `kalyeah-unity-apk` and `kalyeah-unity-screenshots`).

## Stack (do not change without asking)
- **Unity 6 LTS** (`6000.3.28f1` in `unity/Game/ProjectSettings/ProjectVersion.txt`), **URP Mobile**, strict static-typed C# (`.NET 8` shared assemblies + Unity `netstandard 2.1`).
- **Android export:** one IL2CPP APK, `arm64-v8a` only, `1280×720` landscape auto-rotation (`sensorLandscape`), minimal permissions (`KalyeahAndroidManifest.cs` + `unity/Game/Assets/Plugins/Android/kalyeah_local.androidlib/`). The standalone **`.NET 8` Dedicated Server** (`unity/Kalyeah.Server`, containerized via root `Dockerfile`) is the only other build target.
- **Networking:** RFC-6455 WebSocket (`WsClientTransport` / `WsServerTransport` in `Kalyeah.Net`), **server-authoritative** (`Kalyeah.Server` online and in-app `HotspotNet` on LAN Hotspot). Clients send `PlayerInput` frames only.
- **Tests & Visual Gates:**
  - `.NET 8` unit & integration suites: `dotnet test unity/Kalyeah.sln --configuration Release` (`Kalyeah.Sim.Tests` + `Kalyeah.Net.Tests`).
  - Unity `EditMode` NUnit suite (after `python tools/unity_export/sync_assets.py`): `Unity -batchmode -projectPath unity/Game -runTests -testPlatform EditMode -quit`.
  - Dual-aspect visual captures (`1280×720` `16:9` + `844×390` `~19.5:9` notched): `Unity -batchmode -projectPath unity/Game -executeMethod KalyeahBuild.Screenshots -quit`.

## Architecture rules (these prevent most bugs)
1. **Sim/view split.** All gameplay rules live in `unity/Kalyeah.Sim/` (`Kalyeah.Sim.asmdef`, `noEngineReferences: true`) as pure C# classes/structs that never touch `UnityEngine`, `MonoBehaviour`, input, or rendering. `unity/Game/Assets/Kalyeah/View/` only reads sim/bridge state and plays visuals/audio. The exact same `Kalyeah.Sim` assembly runs on `Kalyeah.Server`, in-app LAN Hotspot, offline Practice vs Bots, client prediction, and NUnit tests.
2. **Data-driven.** Every number from `docs/GDD.md` lives in `unity/Data/*.json` (`SimData` / `GameData`: `WeaponDef`, `GameRules`, `CharacterDef`, `MapLayout`, `CameraRules`, `NetRules`, `BotLevel`, `StreetModeRules`), with localization in `unity/Data/i18n__strings.csv`. No magic numbers in code.
3. **Fixed timestep.** Sim advances only in `Step(dt)` with `dt = 1f / 30f` using the match-owned deterministic RNG (`GodotRng`, plus isolated `TRIP_SEED_MIX` stream). Never call `UnityEngine.Random`, `System.Random.Shared`, or wall-clock APIs inside sim.
4. **One input format.** `PlayerInput` (move vec, move target, aim vec/point, buttons bitmask, tick). Touch (`EnhancedTouch` / `TiltInput`) and network both produce it.
5. **Bounded session services:** `AppSession` (local player/room/client/server state), `SettingsStore` (persisted preferences), and `GameData` (loaded JSON rules) inside `Kalyeah.Game.Core` (`noEngineReferences: true`). No scattered `static Instance` singletons.
6. **Event-driven views & explicit wiring:** typed C# events and dirty checks over per-frame UI polling; `[SerializeField]` / code-constructed references instead of `GameObject.Find` or `SendMessage`.

## Folder layout
```
assets/{models,textures,audio,icons,fonts,ui,portraits,app_icon}   docs/   tools/{art,blender,balance,data,i18n,unity_export}
unity/Data/*.{json,csv}   unity/Kalyeah.{Sim,Net,Server,Sim.Tests,Net.Tests}/
unity/Game/Assets/{Kalyeah/{Scripts,View,Shaders,Editor,Tests},Plugins/Android/kalyeah_local.androidlib}
```

## Workflow — versioning and PRs (mandatory)
- **Never commit to `main`.** One branch per task: `feat/<scope>-<short>`, `fix/...`, `chore/...`, `docs/...`, `test/...`.
- **Conventional Commits** (`feat(weapons): add trumpo airborne`). Breaking: `feat!:`.
- **Continuous development, meaningful PRs:** one PR per solid chunk of progress, using `.github/pull_request_template.md`. In the PR body say what changed and how to check it on a phone: which `Unity 6 CI` run to install (`kalyeah-unity-apk`) and what to tap.
- **Pre-PR verification:** run `python tools/unity_export/sync_assets.py`, `dotnet test unity/Kalyeah.sln`, Unity `EditMode` tests, and dual-aspect screenshots (`1280×720` + `844×390`) locally when visuals/UI change; `.github/workflows/unity.yml` runs on pushes/PRs and on demand.
- Releases: no automation (release-please was removed). Don't edit `version.txt` or `CHANGELOG.md` unless the owner asks.
- **Ship loop (owner merges, agents never do):**
  1. Develop a meaningful chunk on a feature branch with tests.
  2. Run `dotnet test unity/Kalyeah.sln` (and `sync_assets.py` + `EditMode` + dual-aspect captures when touching Unity view/UI/build) until everything passes; re-read the diff and fix anything found.
  3. Open the PR with a clear summary, any design choices that need the owner's call, and what to check in the APK.
  4. The owner reads, merges, and installs `kalyeah-unity-apk` from the `Unity 6 CI` run.

## Platforms and how the owner tests
- **Test channel = the debug APK** from Actions → **Unity 6 CI** (artifact `kalyeah-unity-apk`, paired with `kalyeah-unity-screenshots`). Always hand over with which run to install and what to tap.
- **Android stays releasable:** never add desktop-only APIs, `System.Net.HttpListener` (broken on IL2CPP), or unapproved permissions. Signed release APK/AAB is M6; no keystore or password is ever committed.
- What only a phone shows (touch feel, safe areas, thermals, LAN Hotspot / Wi-Fi QR / NFC on real devices) is called out in the PR as a device check instead of assumed.

## Art and orientation (binding)
- **Landscape `1280×720` across menus and matches (owner):** matches use a third-person over-the-shoulder perspective camera (`ShoulderCamera` + `AimSolver`; `docs/GDD.md` "Shoulder view") with a left-thumb `VirtualJoystick`, right-thumb look drag + radial `AbilityArc` (`Tirador` + 3 Superpowers + `Luksong-Tinik` + `Bagsak`), circular `BarangayMinimap`, 5v5 Squad Wings, and overhead health/team bars (zero ground rings under kids). Top-down follow/orthographic cameras remain data toggles in `CameraRules`.
- **Art style:** **Valorant × Arcane painted 3D clarity** fused with **90s Filipino Komiks** bold ink framing on top of chunky stylized Filipino street silhouettes (`docs/ART.md`, `docs/PROJECT.md` §3.8). Inspired by, never copied: no Supercell, Riot, or Valve assets, names, or logos. Authored 3D assets come from Blender (`bpy` in `tools/blender/`).

## Assets: advise, don't generate (owner rule)
When an asset is needed (a prop, a character, an effect, an icon, a texture, a screen background), **do not generate it**. Say which kind it should be and why, and leave the making to the owner:
- **Blender (3D model)** — things the camera sees from changing angles or that must sit in the 3D street with real depth and shadows (props, cover, buildings, the post).
- **Sprite (sprite sheet)** — animated things that should keep a painted 2D look in the 3D world (poses, puffs, hit effects).
- **2D image** — flat, static art on screen or on a surface (HUD icons, button art, posters, signs, ground decals, backgrounds, loading art).
Keep existing procedural/code-drawn stand-ins working until the owner's asset is added.

## Definition of done (per task)
- Zero compiler warnings (`warnaserror`), strict LF (`\n`) line endings.
- Unit tests for every sim/net rule touched (`unity/Kalyeah.Sim.Tests`, `unity/Kalyeah.Net.Tests`, or `unity/Game/Assets/Kalyeah/Tests/EditMode`).
- Mobile touch-first (`EnhancedTouch` + `TiltInput`; Editor-only `PcCombatInput` stays `#if UNITY_EDITOR || DEVELOPMENT_BUILD` with zero PC HUD prompts). Verified at `1280×720` (`16:9`) and `844×390` (`~19.5:9` notched) with asymmetric safe areas respected and `>= 48 dp` thumb-reachable controls.
- Android IL2CPP APK (`KalyeahBuild.BuildAndroid`) and `.NET 8` `Kalyeah.Server` build cleanly.
- `docs/GDD.md` updated if a rule or number changed.

## Don'ts
- No `UnityEngine` references inside `Kalyeah.Sim`, `Kalyeah.Net`, or `Kalyeah.Game.Core`.
- No ENet/UDP for gameplay state: gameplay runs over WebSocket (`WsClientTransport` / `WsServerTransport`); UDP is used strictly for LAN Hotspot `Beacon` discovery.
- No third-party account systems, pay-to-win items, hero stat classes, or damage-type matrices — characters are **100% cosmetic** (`100` HP, `0.45 m` hitbox across all 10 kids; `docs/GDD.md`).
- Never let a character choice change gameplay (hitbox, stats, timing).
- Don't guess on items listed under "Open decisions" in `docs/GDD.md`: implement the documented default and keep it a data toggle in `unity/Data/*.json`.

## Project skills

Read `.claude/skills/README.md` and select only the skills relevant to the task.
Use `surgical-focus` (slice-read only touched seams and run targeted tests for fast turns)
and `ponytail` for smallest-complete changes, `ponytail-review` before PRs,
`ponytail-audit` for codebase audits, `game-ui-ux` / `performance-optimization`
for UI and profiling, and Matt Pocock's `tdd` and `domain-modeling` for behavior
tests and terminology (`GLOSSARY.md`). Use `godot-unity-*` (`godot-unity-sim`,
`godot-unity-view`, `godot-unity-platform`) for Unity 6 / `.NET 8` simulation, URP Mobile
view, and platform/Hotspot/APK architecture, and `apex-*` (`apex-combat-cam`,
`apex-street-world`, `apex-mobile-hud`, `apex-screen-fit`, `apex-net-frame`,
`apex-roast-gate`, `apex-rig-forge`, and `apex-anim-craft`) for AAA mobile craft, multi-screen
responsiveness, brutal 3D/HUD review, acceptance-pass gating, animation-ready character rigs, and
character animation. Architecture, owner art policy and this PR workflow
remain binding. Do not execute upstream plugin installers, hooks, telemetry or
auto-updaters.
