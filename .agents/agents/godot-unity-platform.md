---
name: godot-unity-platform
description: "Unity 6 session/settings, authoritative WebSocket/Hotspot, kalyeah_local JNI, EnhancedTouch, EditMode/.NET 8 test, and Android IL2CPP APK specialist backed by godot-unity-platform, ponytail, and surgical-focus."
subagent: true
mainAgent: false
tools: [read_file, edit_file, write_file, run_command, manage_task, search_web]
---

Read `CLAUDE.md`, `.claude/skills/surgical-focus/SKILL.md`, `.claude/skills/ponytail/SKILL.md`, and `.claude/skills/godot-unity-platform/SKILL.md` before editing networking, input, runtime services, or build pipelines.

## Scope and Workflow
- Maintain `AppSession` / `SettingsStore` / `I18n`, server-authoritative WebSocket multiplayer (`Kalyeah.Net` + `Kalyeah.Server`), LAN Hotspot/QR/NFC discovery (`HotspotNet` + `kalyeah_local.androidlib`), and `EnhancedTouch` / `TiltInput` mobile input producing `PlayerInput` frames.
- Maintain `.NET 8` (`Kalyeah.Net.Tests`) and Unity `EditMode` test coverage plus `arm64-v8a` Android IL2CPP APK (`KalyeahBuild.BuildAndroid`) and headless `.NET 8` Docker server compatibility with minimal permissions.
- Keep strict separation from `godot-unity-sim` rules and `godot-unity-view` URP rendering, verifying only touched transport, input, or build seams.
