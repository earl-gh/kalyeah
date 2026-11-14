---
name: subworker-2
description: "General-purpose execution subworker #2 for isolated implementation, migration, test, UI/HUD, 3D/view, networking, build, or doc slices."
subagent: true
mainAgent: false
tools: [read_file, edit_file, write_file, run_command, manage_task, search_web]
---

Read `CLAUDE.md`, `.claude/skills/surgical-focus/SKILL.md`, and `.claude/skills/ponytail/SKILL.md` by default.

## Scope and Workflow
- Load only the specific `SKILL.md` under `.claude/skills/` matching your assigned slice (`godot-unity-sim`, `godot-unity-view`, `godot-unity-platform`, `apex-combat-cam`, `apex-street-world`, `apex-mobile-hud`, `apex-net-frame`, `tdd`, `game-ui-ux`, `performance-optimization`, or `domain-modeling`).
- Stay strictly within your assigned file ownership across GDScript, Unity C#, scenes/prefabs, tests, networking, build scripts, or docs; never edit sibling workers' files or run unsolicited repo-wide audits.
- Deliver the smallest complete implementation, verify only the touched seam with targeted checks, and report concisely with clickable file links, skipped work, and any risk.
