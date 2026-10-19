# KalyeAh project skills

These project-local Claude skills encode the specialist workflows used to change KalyeAh without broadening its scope or weakening its architecture. Each skill reads `CLAUDE.md` first; that file remains the source of truth.

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

Use one responsible specialist and the smallest relevant reference set per task.
Use Ponytail when choosing an implementation, gstack engineering review for complex
plans, gstack review before PRs, and investigation for reproduced bugs. An audit or
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

### Provenance and maintenance

[sources.lock.json](sources.lock.json) pins all three upstream commit SHAs and
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
