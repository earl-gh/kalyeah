# CLAUDE.md — rules for Claude Code in this repo

@docs/DOS_AND_DONTS.md

Read first: `docs/ROADMAP.md` (what is done, what is next, how to build), `docs/GDD.md` (rules + numbers), `docs/PROJECT.md` (scope).
**Android is the only client** (competition entry): the game is played and tested as an installed **APK**. There is no web or desktop build. The owner works from a phone in cloud sessions and can't run the editor, so every change must be checkable in the **debug APK that the `Android APK` workflow builds** (Actions → Android APK → the run on `main`, or the run you start on a branch → artifact `kalyeah-debug-apk`).

## Stack (do not change without asking)
- Godot **4.6.x** stable (exact version in `.godot-version`), **Compatibility renderer**, GDScript with static types everywhere.
- Android export: one APK, `arm64-v8a`, portrait, only the permissions the game uses (see `tests/unit/test_android_build.gd`). The dedicated **Server** export (Linux, headless) is the only other preset.
- Networking: `WebSocketMultiplayerPeer`, **server-authoritative** headless Godot. Clients send inputs only.
- Tests: GUT, run headless. Claude runs them locally before every PR (Godot 4.6.2 headless binary from the official GitHub release); the `Android APK` workflow runs them again on `main` and fails the build if any fail.

## Architecture rules (these prevent most bugs)
1. **Sim/view split.** All gameplay rules live in `scripts/sim/` as plain classes (`RefCounted`) that never touch nodes, input, or rendering. `scripts/view/` only reads sim state and plays visuals/audio. The same sim runs on the server, in offline practice, and in tests.
2. **Data-driven.** Every number from `docs/GDD.md` lives in `data/*.tres` (`WeaponDef`, `GameRules`). No magic numbers in code.
3. **Fixed timestep.** Sim advances only in `step(dt)` with dt = 1/30. Use a seeded RNG owned by the match. No `randf()` in sim.
4. **One input format.** `PlayerInput` (move vec, aim vec, buttons bitmask, tick). Touch and network both produce it.
5. **Autoloads only:** `Net` (connection + RPC), `Session` (local player/room state), `Settings`. Nothing else global.
6. Signals over polling for view updates; no `get_node("../../..")` — use exported NodePaths or `%UniqueName`.

## Folder layout
```
scenes/{ui,match,map,props,fx}   scripts/{sim,view,net,ui,input}   data/{weapons,characters,rules}
assets/{models,textures,audio,icons,fonts}   server/   tests/{unit,integration}
```

## Workflow — versioning and PRs (mandatory)
- **Never commit to `main`.** One branch per task: `feat/<scope>-<short>`, `fix/...`, `chore/...`, `docs/...`, `test/...`.
- **Conventional Commits** (`feat(weapons): add trumpo airborne`). Breaking: `feat!:`.
- **Continuous development, meaningful PRs:** one PR per solid chunk of progress (several roadmap tasks or a whole phase), not one PR per tiny task. Use `.github/pull_request_template.md`. In the PR body say what changed and how to check it on a phone: which `Android APK` run to install (the owner's own run on the branch, or the run on `main` after the merge) and what to tap.
- **No CI on PRs.** The owner reads the PR and merges. Before opening it, Claude runs the full GUT suite locally and it must pass. The only workflow that tests is `Android APK` (on `main` and on demand); it is the gate and builds the APK.
- Releases: no automation (release-please was removed). Don't edit `version.txt` or `CHANGELOG.md` unless the owner asks.
- **Ship loop (owner merges, Claude never does):**
  1. Develop a meaningful chunk on a feature branch with tests.
  2. Run the full GUT suite locally (and import the project) until everything passes; re-read the diff and fix anything found.
  3. Open the PR with a clear summary, any design choices that need the owner's call, and exactly what to check in the APK. Then hand over and keep going on the next chunk on a new branch.
  4. The owner reads, merges, and installs the APK from the `Android APK` run on `main` once it finishes.

## Platforms and how the owner tests
- **Test channel = the debug APK** from Actions → **Android APK** (every push to `main`, or Run workflow on a branch; artifact `kalyeah-debug-apk`). The run imports the project, runs the full suite, exports, inspects the APK (package, SDK, permissions, arm64 only, 16 KB alignment, size) and, on a manual run, plays it for 30 s on an emulator. Always hand over with which run to install and what to tap.
- **Android stays releasable:** never add anything that breaks the `Android` export preset or needs desktop-only/thread APIs. Signed release APK/AAB is M6; no keystore or password is ever committed.
- What only a phone shows (touch feel, safe areas, performance, the hotspot on a real network) is called out in the PR as a device check instead of assumed.

## Art and orientation (binding)
- **Portrait (vertical) only**: Android locked to portrait. Kalyeah is a vertical MOBA by design (see `docs/PROJECT.md` section 3.8 and `docs/GDD.md` "Vertical layout").
- Art style: **Clash of Clans design language** on a fixed **orthographic camera tilted 45°** (nothing shrinks with distance; the whole street is always on screen; `docs/GDD.md` "Vertical layout"): soft, chunky, painted-looking stylised 3D with a Filipino street theme, readable at phone size. Inspired by, never copied: no Supercell or Riot assets, names or logos. Assets are authored in Blender (`bpy` works in the cloud session). Specs: `docs/PROJECT.md` section 3.8, `docs/ART.md`, `docs/ROADMAP.md`.

## Assets: advise, don't generate (owner rule)
When an asset is needed (a prop, a character, an effect, an icon, a texture, a screen background), **do not generate it**. Say which kind it should be and why, and leave the making to the owner:
- **Blender (3D model)** — things the camera sees from changing angles or that must sit in the 3D street with real depth and shadows (props, cover, buildings, the post).
- **Sprite (sprite sheet)** — animated things that should keep a painted 2D look in the 3D world (the kids, poses, puffs, hit effects).
- **2D image** — flat, static art on screen or on a surface (HUD icons, button art, posters, signs, ground decals, backgrounds, loading art).
Keep the existing procedural/code-drawn stand-ins working until the owner's asset is added.

## Definition of done (per task)
- Static-typed, no warnings (treat warnings as errors in project settings).
- Unit tests for every sim rule touched (`tests/unit/test_<thing>.gd`).
- Touch only; no keyboard, mouse or desktop path. UI built portrait-first at 720×1280 base and usable at 390×844 with safe areas (notch, home bar); thumb-reachable controls.
- The APK builds and starts (the `Android APK` workflow); nothing requires threads, local files or desktop-only APIs.
- `docs/GDD.md` updated if a rule or number changed.

## Don'ts
- No C#/GDExtension (one plain APK). No addons except GUT unless asked.
- No ENet/UDP for gameplay: the protocol is `WebSocketMultiplayerPeer`.
- No accounts, ranked, leveling, items, hero classes/abilities, damage types — out of scope by design. Characters are cosmetic only and chosen by the player on the select screen (docs/GDD.md).
- **No basic attack.** Players only use the four weapon cards in their hand (plus Next, the card that comes in) (every weapon is in a shuffled deck that cycles like Clash Royale; nobody picks weapons, owner), plus the default skills (Dash, Bookmark).
- Never let a character choice change gameplay (hitbox, stats, timing).
- Don't guess on items listed under "Open decisions" in GDD: implement the default and keep it a data toggle.

## Project skills

Read `.claude/skills/README.md` and select only the skills relevant to the task.
Use `router` for ownership, `ponytail` for smallest-complete changes and the local
gstack planning/review/investigation ports when appropriate. The existing five
specialists own implementation; upstream skills are supporting methods/API references.
Pinned Godot 4.6.2, architecture, owner art policy and this PR workflow remain binding.
Do not execute upstream plugin installers, hooks, telemetry or auto-updaters.

For gameplay behavior changes, use the local `gameplay-engineering` companion and
Matt Pocock's `tdd` under the existing specialist. Read `GLOSSARY.md`; use
`domain-modeling` to resolve terminology without adding speculative abstractions.
