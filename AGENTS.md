# Kalyeah agent entry point

Read `CLAUDE.md` first, then `docs/PROJECT.md`, `docs/GDD.md` and `docs/ROADMAP.md`.
The same project constraints apply to every coding agent.

Read `.claude/skills/README.md` and load only the `SKILL.md` and references
needed for the task. Canonical skills live in `.claude/skills/`; `.agents/skills/`
points to that tree for Codex and `.agents/skills.json` exposes it to Antigravity.

The catalog vendors engine-neutral skills from Ponytail, awesome-gamedev-agent-skills
(`game-ui-ux`, `performance-optimization`), and Matt Pocock (`tdd`, `domain-modeling`),
with exact upstream commits, hashes and licenses in `.claude/skills/sources.lock.json`.
Use `ponytail` to keep changes complete and small, and `ponytail-review` before PRs.
Use `tdd` for behavior changes and `domain-modeling` when terminology needs work.
Read `GLOSSARY.md` for player terms. Do not reconfirm already-authorized test
boundaries; clarify genuinely new interfaces or unresolved game rules.
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

Work on a task branch, validate locally and open a reviewable PR; the owner merges.
Versions (`version.txt`) and CHANGELOG.md change only when the owner asks (release-please was removed). Do not run the upstream automation
installers or add unrelated dependencies as part of skill activation.
