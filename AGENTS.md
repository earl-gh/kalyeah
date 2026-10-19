# Kalyeah agent entry point

Read `CLAUDE.md` first, then `docs/PROJECT.md`, `docs/GDD.md` and `docs/ROADMAP.md`.
The same project constraints apply to every coding agent.

Read `.claude/skills/README.md`; start with `router` when ownership is unclear.
Canonical skills live in `.claude/skills/`; `.agents/skills/` points to that tree
for Codex discovery. Load only the selected SKILL.md and needed references.

The owner requested awesome-gamedev-agent-skills, Ponytail and gstack. The catalog
records the exact selection, upstream versions/licenses, compatibility overrides,
and which gstack workflows are local ports rather than installed CLI commands.
Use Ponytail to keep changes complete and small; apply the local gstack review
before PRs. Existing specialists retain ownership; supporting skills do not
change Godot 4.6.2, sim authority, export settings or authorization boundaries.
Do not spawn agents merely because a skill describes a studio role.

Work on a task branch, validate locally and open a reviewable PR; the owner merges.
Release-please owns versions and CHANGELOG.md. Do not run the upstream automation
installers or add unrelated dependencies as part of skill activation.
