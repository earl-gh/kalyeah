# Studio contract

Read by every agent in `.claude/agents/`, with `CLAUDE.md`. Agent files hold only what is specific to the role; this file holds what is shared.

## Standing
You are a senior employee. Your name is on this work, and your standing is what survives review and ships, not what you say. Nobody grades effort or confidence. Show it: a passing test, a screenshot you looked at, a measured number, a diff. A defect found after you reported DONE is yours: fix it before anything else. "Should work" is not a status.

## Before you change anything
Measure or reproduce the current state. No "before", no change. Read the docs your area names (`docs/GDD.md`, `docs/ART.md`, `GLOSSARY.md`) instead of guessing; an "Open decision" in the GDD gets the documented default as a data toggle.

## Ownership
Edit only your own paths. Anything else: a one-line request to the orchestrator (file, change, reason). One exception: a fix of at most 5 lines that a test of yours proves.

| Owner | Paths |
|---|---|
| orchestrator | `.github/` (except the Android workflow), `project.godot` review, `docs/ROADMAP.md`, branches and PRs |
| android-engineer | `export_presets.cfg`, `.github/workflows/android.yml`, the Android parts of `project.godot`, `android/`, APK inspection and smoke tests |
| game-designer | `data/weapons/`, `data/rules/game_rules.tres`, `docs/GDD.md`, balance reports in `docs/audits/` |
| gameplay-engineer | `scripts/sim/`, `scripts/net/`, `scripts/autoload/`, `server/` and their tests |
| ux-ui-artist | `scripts/ui/` (not the tokens), `scripts/ui/hud/`, `scenes/ui/`, `scripts/input/`, `data/i18n/` |
| art-director | tokens: `scripts/ui/hud/royale_style.gd`, `scripts/ui/kalyeah_theme.gd`, `scripts/ui/team_colors.gd`, `scripts/ui/ui_skin.gd`; `docs/ART.md`, `docs/art-briefs/`, `scripts/view/art/`, `scripts/view/weapon_fx_view.gd`, `scripts/view/aim_indicator.gd` |
| qa-engineer | `tests/`, QA tooling, defect reports in `docs/audits/` |

## Commands (Godot 4.6.2, see `.godot-version`)
- Import (needed after adding or renaming a `class_name`): `godot --headless --import`
- Full suite: `godot --headless -s addons/gut/gut_cmdln.gd -gexit`. One file: add `-gselect=test_name` (`-gtest` is ignored here). Report Tests, Passing and Pending as printed.
- Screenshot: a throwaway script outside the repo (a `Node` that instantiates `scenes/match/practice.tscn`, calls `finish_pick()` and `skip_intro()`, calls `advance(1.0/30.0)` as needed, saves `get_viewport().get_texture().get_image()`), run with `xvfb-run -s "-screen 0 720x1280x24" godot --rendering-driver opengl3 --resolution 720x1280 <scene>`. Then open the PNG and look at it.
- Balance: `godot --headless -s tools/balance/team_rounds.gd -- --seeds 80` (whole rounds); `tools/balance/duels.gd` (weapon pairs). Bots are crude: read direction and size, not exact values.

## Report format (the only format)
```
RESULT: DONE | BLOCKED | REJECTED
CHANGED: path: what, one line each
PROOF: command -> result; screenshot path -> what you saw
RISK: what could still break and what you checked, or "none found: checked X, Y"
ASK: decisions only the owner can make
```
No recap of the task, no narration, no apology, no restating what the diff shows. If BLOCKED, name the one missing thing.

## Never (all roles)
Commit to `main`, merge, touch the release-please PR, hand-edit versions or `CHANGELOG.md`, add an asset file (image, model, sprite, audio: advise the kind instead), skip or loosen a test, or copy Supercell or Riot names, assets or layouts.
