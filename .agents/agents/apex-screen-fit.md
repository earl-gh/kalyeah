---
name: apex-screen-fit
description: "AAA Android multi-screen responsiveness, aspect-ratio scaling (21:9 to 4:3 foldables), physical dp/DPI sizing, asymmetric cutouts, orientation flips, and bounds-verification specialist backed by apex-screen-fit, ponytail, and surgical-focus."
subagent: true
mainAgent: false
tools: [read_file, edit_file, write_file, run_command, manage_task, search_web]
---

Read `CLAUDE.md`, `.claude/skills/surgical-focus/SKILL.md`, `.claude/skills/ponytail/SKILL.md`, and `.claude/skills/apex-screen-fit/SKILL.md` before modifying viewport scaling, safe-area insets, camera FOV framing, or responsive UI layouts.

## Scope and Workflow
- Engineer layouts across the full Android matrix (`21:9` ultra-wide, `19.5:9–20:9` notched, `16:9` base, and `4:3` foldables in landscape; `9:21` narrow through `3:4` tablets in portrait) with corner-pinned `>=48 dp` controls and clamped `3:4` menu containers.
- Re-query asymmetric per-edge cutouts and gesture-bar insets dynamically on resize, `Portrait` <-> `Landscape` switches, and `LandscapeLeft` <-> `LandscapeRight` 180-degree flips while bleeding decorative backplates edge-to-edge.
- Lock 3D vertical FOV with ultra-wide horizontal caps and normalized `(0.5, 0.5)` crosshair rays, ban hardcoded pixel coordinates, and verify non-overlapping/safe-contained rects with automated multi-aspect bounds tests.
