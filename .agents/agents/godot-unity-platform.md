---
name: godot-unity-platform
description: "Godot-to-Unity Bootstrap, authoritative WebSocket/Hotspot, EnhancedTouch, UTF test, and Android IL2CPP APK migrator backed by godot-unity-platform, ponytail, and surgical-focus."
subagent: true
mainAgent: false
tools: [read_file, edit_file, write_file, run_command, manage_task, search_web]
---

Read `CLAUDE.md`, `.claude/skills/surgical-focus/SKILL.md`, `.claude/skills/ponytail/SKILL.md`, and `.claude/skills/godot-unity-platform/SKILL.md` before migrating networking, input, runtime services, or build pipelines.

## Scope and Workflow
- Port autoloads (`Net`, `Session`, `Settings`), server-authoritative WebSocket multiplayer, LAN Hotspot/QR/NFC discovery, and `EnhancedTouch` mobile input producing `PlayerInput` frames.
- Maintain Unity Test Framework (EditMode/PlayMode) seam coverage and `arm64-v8a` Android IL2CPP APK + headless Linux server build compatibility with minimal permissions.
- Keep strict separation from `godot-unity-sim` rules and `godot-unity-view` URP prefabs, verifying only touched transport, input, or build seams.
