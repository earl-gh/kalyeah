# Kalyeah — Unity 6 & .NET 8 Architecture

Canonical **Unity 6 LTS (`6000.0.86f1`, URP Mobile, Android `arm64-v8a` IL2CPP)** client and **headless `.NET 8`** authoritative server (`Kalyeah.Server`) architecture for Kalyeah. Authored 3D/2D/audio source assets live in `assets/`, canonical balance and localization data live in `unity/Data/*.{json,csv}`, and scenes/URP assets are generated deterministically by code (`KalyeahBuild.Setup`) rather than hand-edited YAML.

## Asset, data & package sync (`sync_assets.py`)

Run before opening `unity/Game` or running Unity batchmode builds/tests:

```bash
tools/unity_export/sync_assets.sh        # delegates to sync_assets.py when Python >= 3.8 is available
python tools/unity_export/sync_assets.py # cross-platform (Windows / macOS / Linux)
```

`tools/unity_export/sync_assets.py` populates git-ignored directories under `unity/Game/` while preserving Unity `.meta` files and pruning stale files:

- **Art & `.webp` -> `.png` conversion:** mirrors `assets/models/{characters,barangay,props,weapons}`, `textures`, `audio`, `fonts`, `ui`, `icons`, and `portraits` into `Game/Assets/Imported/Resources/`, converting all `.webp` UI/card/icon/portrait textures into RGBA `.png` via Pillow (`pip install Pillow`, with `ffmpeg`/`dwebp` fallback) so `Resources.Load<Texture2D>` and `Sprite` load them directly, plus `assets/app_icon/` into `Game/Assets/Imported/AppIcon/`.
- **Data & localization:** copies `unity/Data/*.json` (39 rule, weapon, character, bot, camera, net, and street-game catalogs) plus `unity/Data/i18n__strings.csv` into `Game/Assets/Imported/Resources/Data/`.
- **Embedded shared C# package:** mirrors `unity/Kalyeah.Sim` (`Runtime/`) and `unity/Kalyeah.Net` (`Net/`) into `Game/Packages/com.kalyeah.sim/` (`v1.0.0`).

Edit gameplay or balance numbers directly in `unity/Data/*.json` (or regenerate `rules__larong_kalye.json` via `python tools/data/make_larong_kalye.py` and refresh `unity/Data/i18n__strings.csv` via `python tools/i18n/extract.py`), then re-run `sync_assets.py` and `dotnet test unity/Kalyeah.sln`.

## Assembly layout

### Engine-free `.NET 8` & embedded package (`unity/Kalyeah.sln`)
- **`Kalyeah.Sim`** (`unity/Kalyeah.Sim/`, `Kalyeah.Sim.asmdef`, `noEngineReferences: true`): deterministic 30 Hz 5v5 *Agawang Base* simulation (`MatchSim`, 16 weapons, objectives, status effects), `BarangayNav` A*, bots (`DummyBrain`, `FightBrain`, `TeamBrain`, `TeamCoach`), `RankedSystem`, `BalanceDuel`, `GodotRng`, `Vec2`/`Rect2` math, `SimData` JSON loader, and the 3 *Larong Kalye* street modes (`Patintero`, `TumbangPreso`, `LangitLupa`, `StreetModeCoach`).
- **`Kalyeah.Net`** (`unity/Kalyeah.Net/`, `Kalyeah.Net.asmdef`, `noEngineReferences: true`): binary `VariantCodec` wire framing, 10-slot `RoomService`, authoritative `ServerMatch` & `GameServer`, `GameClient` mirror/snapshots, `StreetWire`, IL2CPP-safe WebSocket client/server (`WsClientTransport`, `WsServerTransport`), and LAN Hotspot discovery (`HotspotNet`, `HotspotBeacon`, `WifiQr`).
- **`Kalyeah.Server`** (`unity/Kalyeah.Server/`, containerized by root `Dockerfile`): standalone headless `.NET 8` WebSocket + HTTP health-check (`200 kalyeah`) dedicated server (`PORT=8080 dotnet run --project unity/Kalyeah.Server`).
- **`Kalyeah.Sim.Tests` & `Kalyeah.Net.Tests`**: NUnit test suites verifying sim rules, `BarangayNav` A*, bots, street modes, rooms, snapshots, brownout anti-cheat, vendor hotspot interface classification, and live loopback WebSocket sessions (`unity/Golden/`).

### Unity 6 project (`unity/Game/Assets/`)
- **`Kalyeah.Game.Core`** (`Kalyeah/Scripts/Kalyeah.Game.Core.asmdef`, `noEngineReferences: true`, refs `Kalyeah.Sim`, `Kalyeah.Net`): engine-free game bridge — `SimBridge` (local 5v5 practice/training + `GameClient` online/hotspot mirror), `StreetBridge` (*Patintero*, *Tumbang Preso*, *Langit Lupa* offline bots + online wire), `AppSession`, `SettingsStore`, `I18n`, `GameData`, `GodotSpace` (`sim (x, z)` -> Unity `(x, 0, -z)`, yaw `180° - θ`), and `BarangayPlan`.
- **`Kalyeah.Game`** (`Kalyeah/View/Kalyeah.Game.asmdef`, refs `Kalyeah.Game.Core`, `Kalyeah.Sim`, `Kalyeah.Net`, `Unity.InputSystem`, `UnityEngine.UI`, URP): 3D barangay world & URP shaders (`MatchView`, `BarangayEnvironmentBuilder`, `SpaghettiWiresBuilder`, `BarangayCurbsAndProps`, `ObjectiveZonesView`, `KidView` with team ink outlines and zero foot rings), `ShoulderCamera` + `AimSolver`, zero-GC pooled `FxPool` / `AudioHub` / `Haptics`, responsive touch & tilt HUD (`MatchHud`, ability arc/cards, `MatchOverlaysView`, `VirtualJoystick`, `LookPad`, `SafeArea`, `ScreenFitMetrics`, `TiltInput`), menus/profile/QR/lobby/street views, `AndroidLocalLink` JNI bridge, and `link.xml` preserving `Kalyeah.Sim` + `Kalyeah.Net` under IL2CPP.
- **`Plugins/Android/kalyeah_local.androidlib`**: pure Android Java plugin (`KalyeahLocal.java`, `ShareService.java`) powering in-app `LocalOnlyHotspot`, `WifiNetworkSpecifier` auto-join, and NFC Tap-to-Join HCE/Reader.
- **`Kalyeah.Editor`** (`Kalyeah/Editor/Kalyeah.Editor.asmdef`, Editor-only): `KalyeahBuild` (`Setup`, `BuildAndroid`, `Screenshots`, launcher icons, `build_stamp.txt`, CI version/keystore stamping) and `KalyeahAndroidManifest` (`sensorLandscape`, `arm64-v8a` IL2CPP, target SDK 35, 12 minimal Android permissions incl. `NFC`, `allowBackup="false"`).
- **`Kalyeah.Tests.EditMode`** (`Kalyeah/Tests/EditMode/Kalyeah.Tests.EditMode.asmdef`, Editor NUnit): Unity EditMode suite checking synced `Resources`/`.png` textures, `KalyeahAndroidManifest` XML & Back-stack priority, `I18n` & accessibility contrast, `SimBridge`/`StreetBridge` contracts, and multi-aspect HUD/safe-area bounds.

## Game modes: Online, LAN Hotspot & Street (*Larong Kalye*)

- **Practice & Training (offline 5v5 *Agawang Base*):** `SimBridge` steps local `MatchSim` at 30 Hz with `easy`/`medium`/`hard` `TeamBrain`/`TeamCoach` bots (or training dummies) and the player's cosmetic kid + superpower loadout.
- **Online rooms:** `AppSession` connects `GameClient` over WebSocket (`WsClientTransport`) to `Kalyeah.Server`, manages 10-slot room codes (`CreateRoom`, `JoinRoom`, QR share/scan, team swap, ready, start), sends `PlayerInput` each tick, and renders `client.Mirror`.
- **LAN Hotspot:** `HotspotNet` + `AndroidLocalLink` (`kalyeah_local.androidlib`) run an authoritative in-app `GameServer` (`WsServerTransport` over managed `TcpListener` — no `HttpListener`) advertised via UDP `HotspotBeacon`, `WifiQr` Wi-Fi QR, and NFC Tap-to-Join.
- **Street modes (*Larong Kalye*):** `StreetBridge` drives *Patintero* (grid-line crossings vs line taggers), *Tumbang Preso* (slipper throws, can knockdown/reset, opening roulette, and *taya* chase), and *Langit Lupa* (elevated safe platforms, raising mounds, spotlight brownout phase, and *taya* tag) offline with `StreetModeCoach` bots or online via `StreetWire`.

## Testing & dual-aspect screenshots

- **`.NET 8` sim + net suites (no Unity licence required):**
  `dotnet test unity/Kalyeah.sln --configuration Release`
- **Unity EditMode tests (after `sync_assets.py`):**
  `Unity -batchmode -projectPath unity/Game -runTests -testPlatform EditMode -testResults build/editmode-results.xml -quit`
- **Dual-aspect visual captures (`KalyeahBuild.Screenshots`):**
  `Unity -batchmode -projectPath unity/Game -executeMethod KalyeahBuild.Screenshots -quit` (run **without `-nographics`**; wrapped in `xvfb-run` on Linux CI). Renders deterministic PNGs into `unity/Game/Build/screenshots/` across both mobile landscape gates: **`1280×720`** (`16:9` base) and **`844×390`** (`~19.5:9` notched phone with simulated safe-area cutout).
- **Open locally in Unity Hub:** run `python tools/unity_export/sync_assets.py`, open `unity/Game` in Unity `6000.0.86f1` (with Android Build Support), click *Kalyeah > Setup Project*, and play `Assets/Kalyeah/Generated/Scenes/Title.unity`.

## CI (`.github/workflows/unity.yml`) & licence secrets

Runs on pushes to `main`, pull requests, and `workflow_dispatch`:

1. **`sim-tests`:** runs `dotnet test unity/Kalyeah.sln` and verifies `dotnet publish unity/Kalyeah.Server` (always runs; needs no Unity secrets).
2. **`unity-check`:** reads `ProjectVersion.txt` and checks `UNITY_EMAIL`, `UNITY_PASSWORD`, and `UNITY_LICENSE` (or `UNITY_SERIAL`); skips the Unity jobs cleanly with a notice when unset.
3. **`editmode-tests`:** ensures `Pillow`, runs `tools/unity_export/sync_assets.sh`, and executes `game-ci/unity-test-runner@v4` (`EditMode`).
4. **`android`:** ensures `Pillow`, runs `sync_assets.sh`, builds `KalyeahBuild.BuildAndroid` (`arm64-v8a` APK), runs `.github/scripts/inspect-apk.sh` (verifying `libil2cpp.so`/`libunity.so` `arm64-v8a` ABI, `16 KB` page alignment, permissions, landscape orientation, and informational size in MB), and uploads `kalyeah-unity-apk`.
5. **`screenshots`:** ensures `Pillow`, runs `sync_assets.sh`, renders `KalyeahBuild.Screenshots` under `xvfb`, and uploads `kalyeah-unity-screenshots`.

**Repository secrets** (*Settings > Secrets and variables > Actions*, see <https://game.ci/docs/github/activation>):
- `UNITY_EMAIL` & `UNITY_PASSWORD`: Unity account credentials (without 2FA challenge blocking headless sign-in).
- `UNITY_LICENSE`: full contents of `Unity_lic.ulf` from Unity Hub (`C:\ProgramData\Unity\Unity_lic.ulf` on Windows, `/Library/Application Support/Unity/Unity_lic.ulf` on macOS, `~/.local/share/unity3d/Unity/Unity_lic.ulf` on Linux), or `UNITY_SERIAL` for Pro/Plus.
