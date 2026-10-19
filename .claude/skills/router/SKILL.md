---
name: router
description: Route Kalyeah tasks to one responsible specialist and the smallest useful set of installed game-development, Matt Pocock, Ponytail and gstack references.
---

# Kalyeah game-development router

Project-scoped adaptation of awesome-gamedev-agent-skills' router; provenance is in `../sources.lock.json`.

Read `CLAUDE.md`, `docs/PROJECT.md`, `docs/GDD.md` and `docs/ROADMAP.md` first.
This is Agawan Base in Godot 4.6.2: Android portrait, code-free Hotspot discovery,
Online rooms with eight-character codes, and Practice. Existing manifests win over
upstream new-project engine baselines.

## Route once per concern

| Task | Responsible specialist | Optional technical reference |
|---|---|---|
| Rules, weapons, input semantics | gameplay-simulation | godot-gdscript for engine-facing scripts only |
| Rooms, authority, snapshots, reconnect | authoritative-multiplayer | gstack-investigate for a reproduced failure |
| Menu/HUD layout, typography, navigation | gameplay-presentation | game-ui-ux + godot-ui-control |
| Animation, camera, VFX | gameplay-presentation | godot-animation or godot-shaders |
| CPU/GPU/memory, touch, lifecycle | android-performance | performance-optimization |
| Build/export compatibility | android-performance | godot-export |
| Test coverage and release readiness | game-qa-release | gstack-review |

For gameplay behavior changes, use `gameplay-engineering` and `tdd` under the
responsible specialist. Select the existing public test boundary and one behavior;
red/green proof comes from real GUT behavior, not private helper call counts.
Use `domain-modeling` only to sharpen domain language or record a consequential
trade-off; read `GLOSSARY.md` for already-settled terms. Do not create an ADR for
each routine implementation choice. These supporting workflows do not own files.

Use `ponytail` for smallest-complete implementation choices. Use `ponytail-audit`
for an explicitly scoped codebase audit; it produces findings, not edits.
Use `gstack-plan-eng-review` before complex changes, `gstack-review` before a PR,
and `gstack-investigate` for reproduction/hypothesis/root-cause work.

Choose one owner and only the references needed for the task. Read selected
SKILL.md files; load references on demand. These are workflows, not additional
agents or permission to spawn them. Never load the whole catalog per turn.
Do not install related skills automatically. A networking task keeps the existing
WebSocket/PlayerInput architecture; do not substitute generic ENet tutorials.
