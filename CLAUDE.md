# CLAUDE.md — rules for Claude Code in this repo

Read first: `docs/ROADMAP.md` (what is done, what is next, how to build), `docs/GDD.md` (rules + numbers), `docs/PROJECT.md` (scope).
**Android is the primary target** (competition entry); the browser build is optional for players but is the owner's testing channel. The owner works from an **iPhone** in cloud sessions: they can't run the editor. Every change must be checkable on the **GitHub Pages web build (one live link, rebuilt on every merge to `main`)**.

## Stack (do not change without asking)
- Godot **4.6.x** stable (exact version in `.godot-version`), **Compatibility renderer**, GDScript with static types everywhere.
- Web export: **single-threaded** (`variant/thread_support=false`) so it runs on GitHub Pages without COOP/COEP headers.
- Networking: `WebSocketMultiplayerPeer`, **server-authoritative** headless Godot. Clients send inputs only.
- Tests: GUT, run headless. Claude runs them locally before every PR (Godot 4.6.2 headless binary from the official GitHub release); the Pages deploy runs them again on `main` and refuses to deploy if any fail.

## Architecture rules (these prevent most bugs)
1. **Sim/view split.** All gameplay rules live in `scripts/sim/` as plain classes (`RefCounted`) that never touch nodes, input, or rendering. `scripts/view/` only reads sim state and plays visuals/audio. The same sim runs on the server, in offline practice, and in tests.
2. **Data-driven.** Every number from `docs/GDD.md` lives in `data/*.tres` (`WeaponDef`, `GameRules`). No magic numbers in code.
3. **Fixed timestep.** Sim advances only in `step(dt)` with dt = 1/30. Use a seeded RNG owned by the match. No `randf()` in sim.
4. **One input format.** `PlayerInput` (move vec, aim vec, buttons bitmask, tick). Touch, keyboard and network all produce it.
5. **Autoloads only:** `Net` (connection + RPC), `Session` (local player/room state), `Settings`. Nothing else global.
6. Signals over polling for view updates; no `get_node("../../..")` — use exported NodePaths or `%UniqueName`.

## Folder layout
```
scenes/{ui,match,map,props,fx}   scripts/{sim,view,net,ui,input}   data/{weapons,characters,rules}
assets/{models,textures,audio,icons,fonts}   server/   tests/{unit,integration}
```

## Workflow — versioning and PRs (mandatory)
- **Never commit to `main`.** One branch per task: `feat/<scope>-<short>`, `fix/...`, `chore/...`, `docs/...`, `test/...`.
- **Conventional Commits** (`feat(weapons): add trumpo airborne`). Breaking: `feat!:`. This drives release-please.
- **Continuous development, meaningful PRs:** one PR per solid chunk of progress (several roadmap tasks or a whole phase), not one PR per tiny task. Use `.github/pull_request_template.md`. In the PR body say what changed and how to check it on a phone on the single live link (`https://earl-gh.github.io/kalyeah/`, valid after the owner merges).
- **No CI on PRs.** The owner reads the PR and merges. Before opening it, Claude runs the full GUT suite locally and it must pass.
- Releases: release-please opens a release PR on `main`; merging it tags `vX.Y.Z` and updates `CHANGELOG.md`. Don't edit versions or the changelog by hand. Pre-1.0: `feat` = minor, `fix` = patch.
- **Ship loop (owner merges, Claude never does):**
  1. Develop a meaningful chunk on a feature branch with tests.
  2. Run the full GUT suite locally (and import the project) until everything passes; re-read the diff and fix anything found.
  3. Open the PR with a clear summary, any design choices that need the owner's call, and exactly what to check on the live link. Then hand over and keep going on the next chunk on a new branch.
  4. The owner reads, merges, and checks `https://earl-gh.github.io/kalyeah/` once the `Pages` run on `main` finishes. Never merge the release-please PR.

## Platforms and how the owner tests
- **Dev channel = web build on the owner's iPhone browser** (the single Pages link, rebuilt on every merge to `main`). Always hand over with that link and what to check.
- **Android stays releasable:** never add anything that breaks the `Android` export preset or needs desktop-only/thread APIs. An installable debug APK can be built any time from Actions → **Android APK** → Run workflow (artifact `kalyeah-debug-apk`). Signed release APK/AAB is M6.
- Android-only problems (touch, safe areas, performance) can't be seen on the iPhone web build; call them out in the PR instead of assuming they work.

## Art and orientation (binding)
- **Portrait (vertical) only**: Android locked to portrait; the web build shows a "rotate your phone" prompt if held in landscape. Kalyeah is a vertical MOBA by design (see `docs/PROJECT.md` section 3.8 and `docs/GDD.md` "Vertical layout").
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
- Works with touch **and** keyboard/mouse; UI built portrait-first at 720×1280 base and usable at 390×844 with safe areas (notch, home bar); thumb-reachable controls.
- Web build loads on Pages; nothing requires threads, local files or desktop-only APIs.
- `docs/GDD.md` updated if a rule or number changed.

## Don'ts
- No C#/GDExtension (breaks single-threaded web export). No addons except GUT unless asked.
- No ENet/UDP for gameplay (doesn't work in browsers).
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
