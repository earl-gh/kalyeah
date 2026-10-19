---
name: gstack-plan-eng-review
description: Review a Kalyeah implementation plan through architecture, code quality, tests and performance before complex implementation.
---

# gstack engineering plan review — Kalyeah port

Compact adaptation of upstream `plan-eng-review/SKILL.md` and
`plan-eng-review/sections/review-sections.md`. See `../sources.lock.json` and
`../licenses/gstack-LICENSE`. This installs the methodology, not the gstack CLI.

Read `CLAUDE.md`, the relevant project specialist, and the proposed change first.
Use existing session decisions; clarify only genuinely missing product constraints.

1. Scope: identify player-visible outcome, what exists already, the smallest
   complete solution, and what is outside this change. Reuse before inventing.
2. Architecture: trace data/input authority, ownership, dependencies and failure
   recovery. Keep sim/view, fixed ticks, canonical input and WebSocket contracts.
   Name one realistic failure per new path, including disconnect or interruption.
3. Code quality: inspect actual interfaces and callers; require explicit error
   handling and edge cases. Similar-looking code alone does not justify extraction.
4. Tests: map each changed behavior/failure to the existing GUT coverage or a
   necessary new regression. A test should fail for a real defect, not mirror setup.
   Include touch/device lifecycle or multiplayer checks only where relevant.
5. Performance: identify expected player count, CPU/GPU/allocation/packet costs,
   bounds and device evidence needed. Unknown measurements stay unknown.
6. Produce ordered tasks with file ownership, dependencies, acceptance checks and
   verification commands. Flag shared files so simultaneous work cannot collide.
   Do not spawn agents without the user's or governing instructions' authorization.

Use the current task's authorized scope. Planning does not authorize implementation,
network deployment or merging; do not manufacture approval gates for routine choices.
The output is a reviewable plan with risks and evidence requirements, not a shipping claim.
No gstack hooks, telemetry, external model calls, auto-updates or release tooling are required.
