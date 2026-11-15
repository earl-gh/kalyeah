# Kalyeah on Unity (migration in progress)

The Unity port of the Godot game. The Godot project at the repository root stays the shipping game until the port reaches parity.

- `Kalyeah.Sim/`: the game's rules (the sim), plain C# with no engine dependency. The same files compile in Unity (`Kalyeah.Sim.asmdef`),
  in the dedicated server and in the tests.
- `Kalyeah.Sim.Tests/`: NUnit tests, run with `dotnet test unity/Kalyeah.sln`. They check the port against the Godot game itself:
  `Golden/` holds recordings made by the Godot game (`tools/unity_export/export.gd`): its random numbers and scripted 5v5 matches stepped
  tick by tick.
- `Data/`: every resource under `data/` exported to JSON by the same tool (rules, weapons, characters, bot levels).
- `Game/`: the Unity project (Unity 6 LTS, URP, Android).

Regenerate the data and the recordings after a rule changes in the Godot game:
`godot --headless --path . -s tools/unity_export/export.gd`

## Unity project

`Game/` is a Unity **6000.0.86f1** (Unity 6 LTS) project: URP, glTFast (imports the Godot game's `.glb` files at import time), the
Input System, UGUI and the Test Framework. Only text is kept in git: the scripts, the asmdefs, `Packages/manifest.json` and
`ProjectSettings/ProjectVersion.txt`. Everything else is made by code, so no fragile hand-written asset YAML:

- **`tools/unity_export/sync_assets.sh`** copies the Godot project's art (`assets/models/{characters,barangay,props,weapons}`,
  `textures`, `audio`, `fonts`, `ui`, `icons`, `portraits`) to `Game/Assets/Imported/Resources/`, `Data/*.json` to
  `Game/Assets/Imported/Resources/Data/`, and the sim's sources to the embedded package `Game/Packages/com.kalyeah.sim/` (with its
  `package.json`). All of it is git-ignored: the art stays in one place. Run it before opening the project and after the art or the sim
  changes. The package's version is `1.0.0` once `Kalyeah.Sim/` has a `MatchSim` class and `0.1.0` before; `Kalyeah.Game.Core.asmdef`
  turns `1.0.0` into the `KALYEAH_SIM_PORTED` define (versionDefines).
- **`KalyeahBuild`** (`Game/Assets/Kalyeah/Editor/KalyeahBuild.cs`, menu *Kalyeah > Setup Project*, or `-executeMethod`):
  - `KalyeahBuild.Setup`: player settings (`com.kalyeah.game`, Kalyeah, landscape left/right only, arm64-v8a with IL2CPP, min SDK 24,
    target SDK 35 like the Godot APK, linear colour, the Input System), the URP asset, the three materials the code uses, glTFast's
    models imported again under URP with legacy animation clips, the `Title` and `Match` scenes (each one GameObject; everything else
    is built in code) and the build's scene list. Everything it writes goes to `Assets/Kalyeah/Generated/` (git-ignored).
  - `KalyeahBuild.BuildAndroid`: Setup, then the APK (to GameCI's `-customBuildPath`, else `Build/Android/kalyeah-unity.apk`).
    `KalyeahAndroidManifest` adds exactly the Godot preset's permissions: INTERNET, VIBRATE, CHANGE_WIFI_MULTICAST_STATE, CAMERA.
  - `KalyeahBuild.Screenshots`: Setup, then the title and the match (spawn, after two seconds of running, five fixed shoulder spots)
    as 1280x720 PNGs in `Game/Build/screenshots/`. It renders in the editor's edit mode, so it needs a graphics device: run it
    **without `-nographics`** (`Unity -batchmode -projectPath unity/Game -executeMethod KalyeahBuild.Screenshots -quit`; on a
    Linux server under `xvfb-run`, which GameCI's editor images do for you).

**Open it locally:** `tools/unity_export/sync_assets.sh`, then open `unity/Game` in Unity Hub with 6000.0.86f1 (with Android Build
Support), wait for the first import, then *Kalyeah > Setup Project* once, and play `Assets/Kalyeah/Generated/Scenes/Title.unity`.

**Code layout** (`Game/Assets/Kalyeah/`):
- `Scripts/` (`Kalyeah.Game.Core`, engine-free, compiles in plain .NET too and is tested by `Kalyeah.Game.Tests/`): **`SimBridge.cs`, the one
  file that touches a match** (practice_match.gd without the nodes: Practice vs bots, Practice street games, online through a `GameClient`
  mirror; gestures in, `KidSnapshot` / `LocalHud` / `ScoreHud` / `StreetPadState` and banner/feed events out), `MatchInput` (gestures to
  `PlayerInput`), `ShoulderFollow` (the shoulder camera's look, auto-follow, pivot spring, posture, spring arm, prop-hiding maths),
  `ArcLayout` (the ability arc's geometry), `HudText` (clock, results words), the data reader, `GodotSpace` and `BarangayPlan`.
- `View/` (`Kalyeah.Game`): `MatchLaunch` (how the menus start a match: `StartPractice`, `StartStreetGame`, `StartOnline`, and
  `OnFinished` / `OnLeft`), `MatchBoot` (fixed 30 Hz ticks, Back opens the match menu), `MatchView` (street, kids, hides buildings between
  the camera and your kid), `KidView`, `ShoulderCamera`, `MatchHud` (score, clock chip with Extended/Overtime, own-team health only,
  banner, capture alert, crosshair, feed, point freeze, results, match menu), `AbilityArcView` (cards + NEXT + ice water/stamina bars, or
  the hero kit: primary, superpowers, ultimate, vault, jump, crouch/slide), `StreetPadView` (Throw/Kick/Dive/Lupa), `OverheadView`
  (badges, names, status words, own-team bars), `VirtualJoystick`, `LookPad`, `TitleScreen`.
- `Editor/` (`Kalyeah.Editor`): `KalyeahBuild`, `KalyeahAndroidManifest`.

**CI** (`.github/workflows/unity.yml`, on pushes touching `unity/**` and on demand): `dotnet test unity/Kalyeah.sln`; then, when the
licence secrets exist, GameCI (`game-ci/unity-builder@v4`, Android image, version read from `ProjectVersion.txt`) builds the APK
(artifact `kalyeah-unity-apk`) and renders the screenshots (artifact `kalyeah-unity-screenshots`). Without the secrets those two jobs
are skipped with a notice and the run stays green.

**Secrets the owner adds** (repository *Settings > Secrets and variables > Actions*):
- `UNITY_EMAIL` and `UNITY_PASSWORD`: the Unity account (no two-factor login, or GameCI cannot sign in).
- `UNITY_LICENSE`: the whole contents of a `Unity_lic.ulf` file. Unity no longer hands out Personal licence files on the web: sign in
  to Unity Hub once on any computer (*Preferences > Licenses > Add > Get a free personal license*), then copy the file from
  `C:\ProgramData\Unity\Unity_lic.ulf` (Windows), `/Library/Application Support/Unity/Unity_lic.ulf` (macOS) or
  `~/.local/share/unity3d/Unity/Unity_lic.ulf` (Linux). Steps: <https://game.ci/docs/github/activation>.
  With a Pro/Plus licence set `UNITY_SERIAL` instead of `UNITY_LICENSE`.

**Ported so far:** the project and its build, data loading, the real sim with bots (5v5 Practice), the three street games against
bots, online play through a `GameClient` mirror, the barangay's parcels, posts, stands, wall, trees and houses, the ten kids' GLBs with
idle/run, the shoulder camera (look drag, movement auto-follow, pivot spring, crouch/jump posture, FOV kick, shake, spring arm, buildings
hidden between camera and kid), the joystick, the ability arc (cards/NEXT/ice water/stamina, or the hero kit with dash/jump/crouch/slide/
ultimate), cast at the crosshair with cancel, overhead badges (own-team health only), score/clock stages, capture alert, banners, feed,
point freeze, results, match menu (pauses offline), the street games' pads, the title, safe areas, landscape.
**Not yet:** weapon FX and sounds, kid animations beyond idle/run (casts, dives, tags), the weapon pick screen, quick chat, the aim guide
on the street, the creek and bridges, landmarks, wiring and curbs, chalk zones, the outline, music, the menus (another port).
Unverified until the first CI run with a licence: the package versions resolving (glTFast 6.8.0, Input System 1.11.2, URP 17.0.4,
Test Framework 1.4.5), every Unity-only API call (`View/` was compiled here only against the 2021.3 engine modules and hand UGUI stubs, `Editor/` not at all),
the look of anything on screen, and the target SDK 35 platform being in GameCI's Android image.

## The sim port (Kalyeah.Sim)

- Names: GDScript snake_case becomes PascalCase (`rules.move_speed` is `Rules.MoveSpeed`, `sim.add_player()` is `sim.AddPlayer()`),
  signals become C# events of the same name (`player_tagged` is `PlayerTagged`). Exceptions where C# needs a different name:
  `MatchSim.Phase` is a `MatchSim.MatchPhase`, `capture_kind` is `CurrentCaptureKind`, `clock_stage()` is `GetClockStage()`,
  `GameRules.bot_level()` is `GetBotLevel()`, `WeaponDef.Kind/Shape` enums are `WeaponKind/WeaponShape`, `sim._brains` is `Brains`.
- Maths: `Vec2`/`Rect2` are Godot's float32 Vector2/Rect2 with Godot's formulas; `.x`/`.y` read a component as a double, like GDScript.
  Scalars are doubles. `GodotRng` is Godot's RandomNumberGenerator. The golden replays match Godot bit for bit.
- Seams for the ports still to come: `IStreetMode` (what MatchSim calls on a street game; `MatchSim.StartMode`), `DummyBrain`
  (virtual `Think`, `IsFighter`; FightBrain/TeamBrain extend it), `Geometry2D.SegmentHitsRect` (FightBrain's static helper).
- No extra DLL: data loads with the built-in `MiniJson` via `SimData.Load(dir)` or `SimData.AddFile(json)` (e.g. from TextAssets).
  SimData fills fields by reflection, so keep this assembly from being stripped in IL2CPP builds (a `link.xml` preserving `Kalyeah.Sim`).

## The network layer (Kalyeah.Net) and the dedicated server

- `unity/Kalyeah.Net/`: rooms, protocol, snapshots, the server match, client, WebSocket transports, hotspot beacon/finder (no engine
  dependency; synced into the Unity package `com.kalyeah.sim/Net`). Byte-compatible with the Godot game: the wire format is Godot's
  `var_to_bytes` (`VariantCodec`), checked by goldens in `unity/Golden/Net/` (`tools/unity_export/export_net.gd`) and by live runs of a
  Godot client on the C# server and C# clients on the Godot server (`GodotInteropTests`, explicit). Not ported: the Android hotspot/NFC
  plugin glue (`local_link.gd`).
- `unity/Kalyeah.Server/`: the headless server (`PORT=8080 dotnet run --project unity/Kalyeah.Server`; data from `$KALYEAH_DATA`,
  `--data`, or the `Data` folder next to the binary). A plain HTTP GET answers `200 kalyeah` for health checks.
- Client: `new GameClient(rules, layout, NetRules.FromData(data), new StreetCatalog(data))`, `ConnectTo(url)`, `Poll(dt)` every frame,
  the lobby calls (`CreateRoom`, `JoinRoom`, `PickTeam`, `SetReady`, `StartMatch`...), then `SendInput` each tick and draw `client.Mirror`.
