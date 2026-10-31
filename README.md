# Kalyeah

*Mga larong Pinoy sa kalye.* A stylised 3D multiplayer MOBA (1v1, 2v2, 3v3) built on Filipino street games: bato-bato-pik, tumbang preso, jackstones and trumpo. Portrait, one thumb, quick matches. Built with Godot 4.6 as an Android app.

- **Play:** install the debug APK from the latest successful **Android APK** run on `main` (repository → Actions → Android APK → the run → artifact `kalyeah-debug-apk`).

## Docs
- [Roadmap](docs/ROADMAP.md): what is done, what is next, and how we build
- [Game design](docs/GDD.md): rules and balance numbers (the source of truth)
- [Art, UI and audio](docs/ART.md): look, assets, HUD, sound
- [Project documentation](docs/PROJECT.md): abstract, requirements, features
- [Server guide](server/README.md): run and deploy the game server
- [CLAUDE.md](CLAUDE.md): rules for Claude Code (stack, architecture, workflow)

## Online play
Create and Join Room need the game server running somewhere. See [server/README.md](server/README.md): free deploy on Render or Fly.io, then set the repo variable `KALYEAH_SERVER_URL` so the next APK uses it. Hotspot rooms need no server: the host's phone is the server.

## One-time GitHub setup
1. **Settings → Branches →** protect `main`: require a PR (no required status checks; there is no CI on PRs).
2. Optional: **Settings → Secrets and variables → Actions →** secret `KALYEAH_DEBUG_KEYSTORE_B64` (a base64 debug keystore, alias `androiddebugkey`, password `android`) so every APK is signed alike and installs over the last one.

## Workflows
- **Android APK** (every push to `main`, and Actions tab → Run workflow on any branch): imports the project, runs the full test suite (a failure stops the build), exports the debug APK, inspects it (package, SDK, permissions, arm64 only, 16 KB alignment, size) and uploads it as `kalyeah-debug-apk`. A manual run also installs it on an emulator for 30 s (`kalyeah-smoke`: screenshots and log).

## Versioning
Conventional Commits. There is no release automation (release-please was removed): the version is `version.txt`, changed by hand when the owner decides, and `CHANGELOG.md` is kept as history.

## Run the tests
```sh
godot --headless --import
godot --headless -s addons/gut/gut_cmdln.gd -gexit
```

## Starting a Claude Code cloud session
Paste this as the first prompt:

> Read CLAUDE.md and docs/ROADMAP.md. Continue with the next phase on a new branch, follow the workflow rules, open a PR, and stop for my review.
