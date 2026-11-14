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
Do not spawn agents merely because a skill describes a role.

Work on a task branch, validate locally and open a reviewable PR; the owner merges.
Versions (`version.txt`) and CHANGELOG.md change only when the owner asks (release-please was removed). Do not run the upstream automation
installers or add unrelated dependencies as part of skill activation.
