# Kalyeah project skills

Engine-neutral agent skills shared across the repository as Kalyeah migrates to Unity. Each skill defers to `CLAUDE.md` and `AGENTS.md` for project rules and workflow constraints.

## Installed skills (10)

| Source | Installed skills | Purpose |
|---|---|---|
| Ponytail | `ponytail`, `ponytail-review`, `ponytail-audit`, `ponytail-debt`, `ponytail-gain`, `ponytail-help` | Smallest complete implementation, connected-code diff review, whole-repo quality audits and shortcut debt ledger |
| awesome-gamedev-agent-skills | `game-ui-ux`, `performance-optimization` | Engine-neutral responsive HUD/menu patterns, safe areas, frame-budget profiling and pooling (includes Unity 6.3 LTS references) |
| Matt Pocock — Skills for Real Engineers | `tdd`, `domain-modeling` | Behavior-first red-green-refactor testing, domain glossary (`GLOSSARY.md`) and proportionate ADRs |

Load only the `SKILL.md` and `references/` files relevant to the current task:
- Use `ponytail` on coding tasks, `ponytail-review` before PRs, `ponytail-audit` for scoped codebase audits, and `ponytail-debt` to list `shortcut:` comments.
- Use `tdd` for behavior-first tests against public boundaries; do not reconfirm already-authorized test seams.
- Use `domain-modeling` when sharpening terminology in [GLOSSARY.md](../../GLOSSARY.md) or recording consequential architecture decisions.
- Use `game-ui-ux` and `performance-optimization` for responsive mobile UI and profiler-driven performance work.

## Discovery and provenance

- **Claude Code** reads `.claude/skills/`.
- **Codex** reads `.agents/skills/` (a relative symlink to `../.claude/skills`).
- **Antigravity** reads `.agents/skills.json` (which resolves `.claude/skills` directly, including on Windows checkouts where Git symlinks are plain files).

[sources.lock.json](sources.lock.json) pins upstream commit SHAs and SHA-256 hashes for all 10 vendored skills; licenses and the Apache NOTICE live in `licenses/`. These are standalone Markdown skills — no upstream CLI binaries, lifecycle hooks, telemetry or auto-updaters are installed.

## Studio subagents

Eight Claude Code subagents remain in `.claude/agents/` (shared conventions in [docs/STUDIO.md](../../docs/STUDIO.md)): `orchestrator`, `game-designer`, `gameplay-engineer`, `ux-ui-artist`, `art-director`, `qa-engineer`, `hotspot-engineer`, and `android-engineer`.
