# Kalyeah

*Mga larong Pinoy sa kalye.* A stylized 3D 5v5 hero-brawler & multiplayer street-game arena built on Filipino street games (*Agawang Base*, *Patintero*, *Tumbang Preso*, *Langit Lupa*, *bato-bato-pik*, *jackstones*, and *trumpo*). Native `1280×720` landscape dual-thumb action built with **Unity 6.3 LTS (`6000.3.28f1`, URP Mobile)** for Android (`arm64-v8a` IL2CPP) and **`.NET 8`** (`Kalyeah.Server`).

- **Website:** [kalyeah-promo.vercel.app](https://kalyeah-promo.vercel.app/), the game's page, with the latest APK to download.
- **Play:** get the APK from [the website](https://kalyeah-promo.vercel.app/), or install the debug APK from the latest **Unity 6 CI** run (Actions → Unity 6 CI → artifact `kalyeah-unity-apk`).

## Docs
- [Unity 6 & .NET 8 architecture](unity/README.md): assemblies, asset/data sync, tests, screenshots, and CI
- [Spec](docs/SPEC.md): what we are building (vision, modes, accounts, store, ranked); wins over older docs
- [Roadmap](docs/ROADMAP.md): what is done, what is next, and how we build
- [Game design](docs/GDD.md): rules and balance numbers (the source of truth, backed by `unity/Data/*.json`)
- [Art, UI and audio](docs/ART.md): look, assets, HUD, sound
- [Server guide](server/README.md): run and deploy `unity/Kalyeah.Server` (`Dockerfile`)
- [CLAUDE.md](CLAUDE.md): rules for coding agents (stack, architecture, workflow)

## Online & Local play
Create and Join Online rooms connect to `unity/Kalyeah.Server` over WebSocket (`wss://`). Deploy the root `.NET 8` `Dockerfile` on Render or Fly.io ([server/README.md](server/README.md)), then set the repo variable `KALYEAH_SERVER_URL` so the next APK build stamps it in. **Local (Hotspot)** rooms need no internet server: the host's Android phone runs the authoritative `GameServer` inside the app with UDP `Beacon`, Wi-Fi QR, and NFC Tap-to-Join discovery.

## Workflows (`.github/workflows/unity.yml`)
- **Unity 6 CI** (pushes to `main`, pull requests, and `workflow_dispatch`):
  1. Runs `.NET 8` simulation & networking tests (`dotnet test unity/Kalyeah.sln`) and verifies `dotnet publish unity/Kalyeah.Server`.
  2. Syncs `assets/` and `unity/Data/` via `tools/unity_export/sync_assets.sh` and runs Unity `EditMode` NUnit tests.
  3. Exports and inspects the `arm64-v8a` IL2CPP Android APK (`kalyeah-unity-apk`).
  4. Renders dual-aspect (`1280×720` + `844×390`) visual captures (`kalyeah-unity-screenshots`).

## Versioning
Conventional Commits. There is no release automation: the version lives in `version.txt`, changed only when the owner decides, and `CHANGELOG.md` is kept as history.

## Run the tests locally
```sh
dotnet test unity/Kalyeah.sln --configuration Release
python tools/unity_export/sync_assets.py
Unity -batchmode -projectPath unity/Game -runTests -testPlatform EditMode -quit
```
