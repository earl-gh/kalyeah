# KalyeAh project skills

These project-local agent skills encode the specialist workflows used to change KalyeAh without broadening its scope or weakening its architecture. Each skill reads `CLAUDE.md` first; that file remains the source of truth.

| Skill | Owns | Does not own |
| --- | --- | --- |
| `gameplay-simulation` | Agawan Base rules, deterministic simulation, weapon behavior, input semantics | Network transport, presentation, export settings |
| `authoritative-multiplayer` | WebSocket protocol, rooms, server authority, snapshots, reconnects | Game balance, animation, Android rendering |
| `gameplay-presentation` | Animation, camera, VFX/audio hooks, HUD feedback, sim-to-view mapping | Simulation outcomes, protocol, asset creation |
| `android-performance` | Profiling, mobile budgets, touch/safe areas, Android and web compatibility | Game rules, networking semantics, art direction |
| `game-qa-release` | Risk-based tests, regression coverage, validation, PR and release readiness | Product scope, feature implementation, manual version edits |

The boundaries are deliberate. Multiple agents may use these skills at the same time when their file ownership does not overlap. If a task crosses boundaries, split it into commits and let the owning specialist review each part.

## Requested upstream skills

The owner's requested collections are installed as project-local Markdown skills.
The existing five specialists above retain implementation ownership; upstream
skills provide references or review methods, not competing owners.
Start with [router](router/SKILL.md) when the task crosses concerns.

| Source | Installed skills | Purpose |
|---|---|---|
| awesome-gamedev-agent-skills | `godot-gdscript`, `godot-ui-control`, `godot-animation`, `godot-shaders`, `godot-export`, `game-ui-ux`, `performance-optimization`; project-scoped `router` port | Concrete Godot APIs, responsive UI and measured optimization |
| Ponytail | `ponytail`, `ponytail-audit` (standalone upstream skills) | Smallest complete implementation; evidence-based codebase audits |
| gstack | `gstack-plan-eng-review`, `gstack-review`, `gstack-investigate` (compact Kalyeah workflow ports) | Architecture/code/tests/performance review, trust-boundary and test-gate review, root-cause debugging |
| Matt Pocock — Skills for Real Engineers | `tdd`, `domain-modeling` (vendored with project overrides) | Behavior-first tests, stable public test boundaries, precise game terminology and proportionate architecture records |
| Kalyeah companion adapted from Matt's methods | `gameplay-engineering` | Connect player intent, fixed-tick simulation, authority, readable feedback and device evidence through the existing specialists |

There are **21 installed skills**: the five original specialists plus 16 supporting
skills/workflow ports. The game-specific companion is project-authored; it is not
an official Matt Pocock game-development package.

Use one responsible specialist and the smallest relevant reference set per task.
Use Ponytail when choosing an implementation, gstack engineering review for complex
plans, gstack review before PRs, and investigation for reproduced bugs. Use `gameplay-engineering` with `tdd` for
gameplay behavior changes; use `domain-modeling` when terminology actually needs
clarification. [GLOSSARY.md](../../GLOSSARY.md) records settled player terms.
An audit or
review-only request produces findings without changing implementation.

### Compatibility and installation scope

`CLAUDE.md`, project manifests and user instructions take precedence. The game-dev
upstream uses a Godot 4.7 new-project baseline; Kalyeah stays on **4.6.2**. Each
vendored skill has an explicit compatibility override; examples must be checked
against the pinned engine. Generic movement, safe-area and pause examples do not
replace Kalyeah's deterministic simulation, coordinate conversion or running
multiplayer. Related skills named upstream are optional, not auto-install requests.

These are **skills, not plugin installations**. gstack's CLI, browser binaries,
Bun/Node packages, hooks, telemetry, external-model integrations and auto-updater
are not installed. Its three ports preserve selected review/debugging methods;
they do not claim full upstream gstack command compatibility. Ponytail's UI band,
board and lifecycle hooks are not needed for its standalone Markdown skills.
Other engines, ENet samples, C#, asset generation and alternate test frameworks
are omitted. No agent-tool package is needed at game runtime.

Claude reads `.claude/skills/`. Codex reads `.agents/skills/`, a relative symlink
to the same canonical tree, so references cannot drift between duplicate copies.
If a client does not follow project skill links, read this catalog directly;
restart or reopen the project if its skill picker caches discovery. Skills can
also be used immediately by reading the named `SKILL.md`.

### Matt Pocock integration

The copied `tdd` and `domain-modeling` folders retain their references and agent
metadata, plus the original MIT license. Marked local additions require Godot/GUT
and existing ownership; their TypeScript examples do not add a JavaScript stack.
The local TDD notice explicitly replaces upstream's per-seam confirmation rule:
already-authorized behavior at an existing public boundary does not need another
confirmation. New interfaces or unresolved product rules still need clarification.
No testing requirement is relaxed. Optional related skills are not auto-installed.

The `gameplay-engineering` companion applies the methods to simulation, multiplayer,
input interruptions, animation feedback and measured Android/build validation.
It is a workflow reference, not another implementation owner. Existing game-dev
references remain the concrete API guides. No new test runner or engine bundle
is installed. First use: [behavior-first skill review](../../docs/audits/2026-10-19-behavior-first-skills.md).

### Provenance and maintenance

[sources.lock.json](sources.lock.json) pins all four upstream commit SHAs and
SHA-256 hashes for installed upstream-derived files. It identifies unchanged
copies, added compatibility notices and workflow ports, including the source
files used for each adaptation. Licenses and the Apache NOTICE are in `licenses/`.
This is a provenance manifest, not a package-manager executable lockfile.

To update: fetch the named upstream at an explicit commit in a temporary checkout,
review only the selected skill directories and references, retain local overrides,
then refresh the hashes and run the install checks and required Godot validation.
Never auto-update or overwrite the existing specialist skills. New runtime tools,
frameworks and unrelated bundles require a separate justified task.

First use and remaining checks: [skill adoption review](../../docs/audits/2026-10-19-skill-adoption.md).

## Studio subagents

Six role agents live in `.claude/agents/` (Claude Code subagents, started by name from a task or with `/agents`). They are a Supercell-style studio team; each reads `CLAUDE.md` first and obeys the owner rules (no asset generation, never merge, GDD stays the source of rules).

| Agent | Role | Edits files? |
| --- | --- | --- |
| `game-designer` | Core loop, combat, card cycle, balance by bot-run evidence | No: proposes exact numbers |
| `ux-ui-artist` | Portrait HUD, menus, flow, touch ergonomics | Yes (UI code and layout tests) |
| `concept-artist` | Characters, street, animation and VFX as written briefs | No: briefs for the owner to make |
| `art-director` | Brand and visual consistency, originality line | No: approve, change or reject |
| `gameplay-engineer` | Fixed-tick sim, server-authoritative multiplayer, client architecture | Yes (with GUT tests) |
| `qa-engineer` | Regression tests, Playwright interaction audits, bot-run evidence, release readiness | Yes (tests) |

They sit next to the specialist skills above, not instead of them: `gameplay-engineer` uses `gameplay-simulation`, `authoritative-multiplayer` and `tdd`; `qa-engineer` uses `game-qa-release`; `ux-ui-artist` uses `game-ui-ux` and `godot-ui-control`.
