# CLAUDE.md — rules for coding agents in this repo

@docs/DOS_AND_DONTS.md

## The five sources of truth (owner rule, 2026-11-19): follow them, never your memory
Every design decision comes from one of these five files. Before you design, change or build anything, find the section that
covers it and read that section (reading only the touched sections is fine; skipping them is not):

| Task touches | Read and follow |
|---|---|
| Modes, rules, accounts, progression, store, controls, screens, platform, the HUD list | `docs/SPEC.md` |
| A kid's skills, kit, role, aim view, reveals | `docs/KITS.md` |
| A map's layout, routes, climb spots, vendors, neutrals | `docs/MAPS.md` |
| Names, puroks, banter, personality, poses | `docs/LORE.md` |
| How a match looks: models, map detail, effects, camera look, HUD look, materials | `docs/GRAPHICS.md` |

Rules: (1) these five win over every other doc, skill, comment, test name and your own idea of what a game like this does.
(2) `docs/GDD.md`, `docs/ART.md` and `docs/ROADMAP.md` record what is built and the order of work; they never override the five.
(3) If the five are silent, use the default in `docs/SPEC.md` §11 or ask the owner; do not invent a rule. If two of them
disagree, stop and ask. (4) Do not write a new design doc outside the five; record an owner answer in the matching file.
(5) Every PR names the sections it follows (the PR template has a "Spec check"), and anything that deviates goes under "Decisions
for you". (6) Never reintroduce what the five dropped (for example a logo on a game screen, a left or swappable shoulder camera).
A CI test (`SourceOfTruthTests`) fails if one of the five goes missing, an entry file stops naming them, or an unlisted doc appears.

Order of work: `docs/ROADMAP.md`. Rules and numbers as built: `docs/GDD.md`. Look as built: `docs/ART.md`.
The owner works from a phone and can't run the editor: every change must be checkable in the **APK built by the
`Unity 6 CI` workflow** (artifacts `kalyeah-unity-apk` and `kalyeah-unity-screenshots`).

## Stack
- **Unity 6.3 LTS** (`6000.3.28f1`), **URP Mobile**, C# (`.NET 8` shared assemblies + Unity `netstandard 2.1`).
- **Builds:** the Android app (`KalyeahBuild.BuildAndroid`; iOS follows after launch, so game code stays platform-neutral)
  and the **`.NET 8` dedicated server** (`unity/Kalyeah.Server`, root `Dockerfile`).
- **Networking:** WebSocket (`WsClientTransport` / `WsServerTransport` in `Kalyeah.Net`); UDP only for Hotspot discovery.

## Architecture (the game's backbone)
1. **Server-authoritative.** `Kalyeah.Server` online and in-app `HotspotNet` on Hotspot decide every hit and score;
   phones send `PlayerInput` only.
2. **Sim/view split.** Gameplay rules live in `unity/Kalyeah.Sim/` as pure C# with no `UnityEngine`; the same assembly
   runs on the server, Hotspot, offline play, client prediction and tests. `unity/Game/Assets/Kalyeah/View/` only reads
   state and plays visuals and audio. `Kalyeah.Net` and `Kalyeah.Game.Core` stay engine-free too.
3. **Data-driven.** Every gameplay number lives in `unity/Data/*.json` (strings in `unity/Data/i18n__strings.csv`).
4. **Fixed timestep and determinism.** The sim steps at `dt = 1/30` with the match-owned RNG; no `UnityEngine.Random`,
   `System.Random.Shared` or wall clock inside the sim.
5. **One input format** (`PlayerInput`) from touch, tilt and network.
6. **Session services** `AppSession`, `SettingsStore`, `GameData` in `Kalyeah.Game.Core`; no scattered singletons.
7. **Event-driven views**, explicit references; no `GameObject.Find` or `SendMessage`.

## Folder layout
```
assets/{models,textures,audio,icons,fonts,ui,portraits,app_icon}   docs/   tools/{art,blender,balance,data,i18n,unity_export}
unity/Data/*.{json,csv}   unity/Kalyeah.{Sim,Net,Server,Sim.Tests,Net.Tests}/
unity/Game/Assets/{Kalyeah/{Scripts,View,Shaders,Editor,Tests},Plugins/Android/kalyeah_local.androidlib}
```

## Workflow (owner merges, agents never do)
- **Never commit to `main`.** One branch per task (`feat/...`, `fix/...`, `chore/...`, `docs/...`, `test/...`),
  Conventional Commits.
- One PR per solid chunk, using `.github/pull_request_template.md`: what changed, which `Unity 6 CI` run to install and
  what to tap. Call out what only a real phone can show (touch feel, thermals, Hotspot, NFC) as a device check.
- Don't edit `version.txt` or `CHANGELOG.md` unless the owner asks. Never commit a keystore or password.
- **Roadmap work follows the `ponytail` skill (owner rule):** the smallest complete, YAGNI-driven change; reuse what the
  codebase has; no abstractions, options, toggles or "for later" code the spec step doesn't need.
  **Minimal never means weak:** every change is finished and production-quality: the whole spec step works end to end
  (sim, net, bots, HUD), edge cases are handled, every rule has a real test, names and comments are precise, and nothing
  is left half-done, stubbed, placeholder or padded with filler. Small and solid, never small and sloppy.

## Tests and visual gate (before every PR)
- `dotnet test unity/Kalyeah.sln --configuration Release`.
- `python tools/unity_export/sync_assets.py`, then Unity `EditMode` tests
  (`Unity -batchmode -projectPath unity/Game -runTests -testPlatform EditMode -quit`).
- UI or visual changes: screenshots at `1280×720` and `844×390`
  (`Unity -batchmode -projectPath unity/Game -executeMethod KalyeahBuild.Screenshots -quit`); safe areas respected,
  touch targets `>= 48 dp`. With no local Unity, the `Unity 6 CI` run is the gate.
- Zero compiler warnings, LF line endings, a test for every rule touched, `docs/GDD.md` updated when a rule or number
  changes.

## Assets: licence-checked free assets, otherwise advise (owner rule, 2026-11-19)
Agents may use **licence-checked free 3D assets and textures**: CC0 freely (Poly Haven, ambientCG, KayKit, Quaternius),
CC-BY only with a credit line (Sketchfab), never another game's assets or non-commercial licences. Log every asset's
source, author and licence in `assets/CREDITS.md`, make it game-ready by script, and show the owner screenshots to
approve. The owner's own models come from Meshy. Agents don't generate audio or 2D art; for anything else, say which
kind is needed and why, and keep the code-drawn stand-in working until it arrives:
- **Blender (3D model):** seen from changing angles or standing in the 3D world (props, cover, buildings, kids).
- **Sprite sheet:** animated things that keep a painted 2D look (puffs, hit effects).
- **2D image:** flat art on screen or on a surface (icons, button art, posters, decals, backgrounds).

## Project skills
Read `.claude/skills/README.md` and load only what the task needs: `surgical-focus` and `ponytail` always,
`ponytail-review` or `apex-roast-gate` before PRs, `tdd` for behaviour, `domain-modeling` for `GLOSSARY.md` terms,
`godot-unity-*` for sim / view / platform, `apex-*` for camera, world, HUD, screen fit, netcode, rigs and animation.
Don't run third-party plugin installers, hooks, telemetry or auto-updaters.
