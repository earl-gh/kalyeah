# Kalyeah project skills

Agent skills shared across the repository as Kalyeah builds on Godot 4.6 and migrates toward Unity 6. Each skill defers to `CLAUDE.md` and `AGENTS.md` for project rules and workflow constraints.

## Installed skills (19)

### Vendored upstream skills (10)

Pinned in [sources.lock.json](sources.lock.json) with licenses and Apache NOTICE in `licenses/`:

| Source | Installed skills | Purpose |
|---|---|---|
| Ponytail | `ponytail`, `ponytail-review`, `ponytail-audit`, `ponytail-debt`, `ponytail-gain`, `ponytail-help` | Smallest complete implementation, connected-code diff review, whole-repo quality audits and shortcut debt ledger |
| awesome-gamedev-agent-skills | `game-ui-ux`, `performance-optimization` | Engine-neutral responsive HUD/menu patterns, safe areas, frame-budget profiling and pooling (includes Unity 6.3 LTS references) |
| Matt Pocock — Skills for Real Engineers | `tdd`, `domain-modeling` | Behavior-first red-green-refactor testing, domain glossary (`GLOSSARY.md`) and proportionate ADRs |

### Custom Kalyeah skills (9)

Authored in `.claude/skills/` for fast execution, Godot-to-Unity migration, and AAA mobile craft:

| Group | Installed skills | Purpose |
|---|---|---|
| Fast execution (1) | `surgical-focus` | Fast responses by checking only touched files/lines/1-hop callers, skipping unsolicited whole-repo audits, and running targeted seam tests |
| Godot -> Unity migration (3) | `godot-unity-sim`, `godot-unity-view`, `godot-unity-platform` | `godot-unity-sim`: pure C# `30 Hz` deterministic sim & `.tres` -> `ScriptableObject` parity; `godot-unity-view`: `.tscn`/signals/`.glb`/shaders -> Unity 6 URP Mobile Prefabs, C# events, Animator & pooled views; `godot-unity-platform`: Autoloads, WebSocket/Hotspot, EnhancedTouch, GUT->UTF & Android IL2CPP `arm64-v8a` APK |
| Apex Mobile Studio (5, Riot Wild Rift & Valorant × Valve Deadlock × CoD Mobile × Fortnite) | `apex-combat-cam`, `apex-street-world`, `apex-mobile-hud`, `apex-net-frame`, `apex-roast-gate` | `apex-combat-cam`: 3P shoulder cam, kinetic locomotion & `<150 ms` combat telegraphs; `apex-street-world`: 3D barangay sightlines, 3-band chest-height value clarity & stylized forms; `apex-mobile-hud`: thumb-zone radial arc, drag/tap/cancel UX, clear center screen & overhead team bars; `apex-net-frame`: `30 Hz` authoritative prediction/reconciliation & sustained 60 FPS Android GPU/GC budgets; `apex-roast-gate`: acceptance criteria gate, over/under-engineering razor, brutal 3D world + GUI/HUD roaster & `ANOTHER PASS REQUIRED` loop |

Load only the `SKILL.md` and `references/` files relevant to the current task:
- Pair `surgical-focus` and `ponytail` on coding tasks; use `ponytail-review` or `apex-roast-gate` before PRs, `ponytail-audit` for requested codebase audits, and `ponytail-debt` to list `shortcut:` comments.
- Use `tdd` for behavior-first tests against public boundaries; do not reconfirm already-authorized test seams.
- Use `domain-modeling` when sharpening terminology in [GLOSSARY.md](../../GLOSSARY.md) or recording consequential architecture decisions.
- Use `godot-unity-*` when porting sim, view, or platform seams to Unity 6, and `apex-*` (plus `game-ui-ux` / `performance-optimization`) for 3P camera, 3D barangay world, thumb-zone HUD, net/frame budgets, and acceptance-pass gating.

## Discovery and provenance

- **Claude Code** reads `.claude/skills/`.
- **Codex** reads `.agents/skills/` (a relative symlink to `../.claude/skills`).
- **Antigravity** reads `.agents/skills.json` (which resolves `.claude/skills` directly, including on Windows checkouts where Git symlinks are plain files).

[sources.lock.json](sources.lock.json) pins upstream commit SHAs and SHA-256 hashes for all 10 vendored skills; the 9 custom Kalyeah skills live directly in `.claude/skills/`. These are standalone Markdown skills — no upstream CLI binaries, lifecycle hooks, telemetry or auto-updaters are installed.

## Studio subagents

- **Antigravity & `/swarm` (`.agents/agents/`):** 26 subagents mapped in [AGENTS.md](../../AGENTS.md) (`ponytail`, `reviewer`, `auditor`, `tdd`, `domain`, `ui-ux`, `perf`, the 9 custom-skill specialists, and `subworker-1` .. `subworker-10`).
- **Claude Code (`.claude/agents/`):** 8 studio subagents (shared conventions in [docs/STUDIO.md](../../docs/STUDIO.md)): `orchestrator`, `game-designer`, `gameplay-engineer`, `ux-ui-artist`, `art-director`, `qa-engineer`, `hotspot-engineer`, and `android-engineer`.
