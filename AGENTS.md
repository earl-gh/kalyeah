# Kalyeah agent entry point

Apply **surgical focus**: read `CLAUDE.md` first, plus `.claude/skills/surgical-focus/SKILL.md` and `.claude/skills/ponytail/SKILL.md`. Read `docs/GDD.md`, `docs/PROJECT.md`, `docs/ROADMAP.md`, `docs/ART.md`, or `GLOSSARY.md` **selectively** (targeted sections/lines only when gameplay rules, scope, art, or player terms apply to the task). The same project constraints apply to every coding agent.

Consult `.claude/skills/README.md` and load only the `SKILL.md` and references needed for the task. Canonical skills live in `.claude/skills/`; `.agents/skills/` points to that tree for Codex and `.agents/skills.json` exposes it to Antigravity.

The catalog pairs 10 vendored skills — Ponytail, awesome-gamedev-agent-skills (`game-ui-ux`, `performance-optimization`), and Matt Pocock (`tdd`, `domain-modeling`), with exact upstream commits, hashes and licenses in `.claude/skills/sources.lock.json` — with 10 custom Kalyeah skills (`surgical-focus`, `godot-unity-*`, `apex-*`). Use `surgical-focus` and `ponytail` to keep turns fast and changes small, `ponytail-review` or `apex-roast-gate` before PRs, `tdd` for behavior changes, and `domain-modeling` when terminology in `GLOSSARY.md` needs work. Do not reconfirm already-authorized test boundaries; clarify genuinely new interfaces or unresolved game rules.
Spawn subagents or /swarm workers only when parallel work or explicit user direction calls for them.

## Subagents and `/swarm` workers

Antigravity subagents in `.agents/agents/` share names with `/swarm` workers and load these canonical skills:

| Name | Subagent | Backing `.claude/skills/` files |
|---|---|---|
| `ponytail` | `.agents/agents/ponytail.md` | `.claude/skills/ponytail/SKILL.md`, `.claude/skills/ponytail-help/SKILL.md`, `.claude/skills/ponytail-gain/SKILL.md` |
| `reviewer` | `.agents/agents/reviewer.md` | `.claude/skills/ponytail-review/SKILL.md` |
| `auditor` | `.agents/agents/auditor.md` | `.claude/skills/ponytail-audit/SKILL.md`, `.claude/skills/ponytail-debt/SKILL.md` |
| `tdd` | `.agents/agents/tdd.md` | `.claude/skills/tdd/SKILL.md`, `.claude/skills/tdd/tests.md`, `.claude/skills/tdd/mocking.md` |
| `domain` | `.agents/agents/domain.md` | `.claude/skills/domain-modeling/SKILL.md`, `.claude/skills/domain-modeling/GLOSSARY-FORMAT.md`, `.claude/skills/domain-modeling/ADR-FORMAT.md` |
| `ui-ux` | `.agents/agents/ui-ux.md` | `.claude/skills/game-ui-ux/SKILL.md`, `.claude/skills/game-ui-ux/references/layout-and-flow.md` |
| `perf` | `.agents/agents/perf.md` | `.claude/skills/performance-optimization/SKILL.md`, `.claude/skills/performance-optimization/references/profiling-and-budgets.md` |
| `surgical-focus` | `.agents/agents/surgical-focus.md` | `.claude/skills/surgical-focus/SKILL.md` |
| `godot-unity-sim` | `.agents/agents/godot-unity-sim.md` | `.claude/skills/godot-unity-sim/SKILL.md` |
| `godot-unity-view` | `.agents/agents/godot-unity-view.md` | `.claude/skills/godot-unity-view/SKILL.md` |
| `godot-unity-platform` | `.agents/agents/godot-unity-platform.md` | `.claude/skills/godot-unity-platform/SKILL.md` |
| `apex-combat-cam` | `.agents/agents/apex-combat-cam.md` | `.claude/skills/apex-combat-cam/SKILL.md` |
| `apex-street-world` | `.agents/agents/apex-street-world.md` | `.claude/skills/apex-street-world/SKILL.md` |
| `apex-mobile-hud` | `.agents/agents/apex-mobile-hud.md` | `.claude/skills/apex-mobile-hud/SKILL.md` |
| `apex-screen-fit` | `.agents/agents/apex-screen-fit.md` | `.claude/skills/apex-screen-fit/SKILL.md` |
| `apex-net-frame` | `.agents/agents/apex-net-frame.md` | `.claude/skills/apex-net-frame/SKILL.md` |
| `apex-roast-gate` | `.agents/agents/apex-roast-gate.md` | `.claude/skills/apex-roast-gate/SKILL.md` |
| `subworker-1` .. `subworker-10` | `.agents/agents/subworker-1.md` .. `.agents/agents/subworker-10.md` | `.claude/skills/surgical-focus/SKILL.md`, `.claude/skills/ponytail/SKILL.md`, plus task-relevant `.claude/skills/*` |

Work on a task branch, validate locally and open a reviewable PR; the owner merges.
Versions (`version.txt`) and CHANGELOG.md change only when the owner asks (release-please was removed). Do not run the upstream automation
installers or add unrelated dependencies as part of skill activation.
