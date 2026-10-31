---
name: gstack-review
description: Review a Kalyeah diff and connected callers for correctness, trust boundaries, test-gate integrity, compatibility and measured performance before a PR.
---

# gstack change review — Kalyeah port

Compact adaptation of upstream `review/SKILL.md` and `review/checklist.md`.
See `../sources.lock.json` and `../licenses/gstack-LICENSE`.
This is a local review workflow, not the gstack executable/tooling installation.

Read `CLAUDE.md`, `.claude/skills/game-qa-release/SKILL.md` and the specialist owning the change.
Resolve the target (named diff/PR, otherwise working changes) and its base before review.
Read changed code, every relevant caller and existing regression coverage.

## Critical pass

- Trace trust boundaries: malformed/forged inputs, room ownership, packet sizes,
  stale/reordered snapshots and reconnect identity. Clients send input, not outcomes.
- Check conditional side effects, lifecycle races, duplicate signals/tweens,
  held-input cancellation and offline/online parity.
- Trace new fields/statuses/constants through unchanged consumers and allowlists.
- Inspect test/config changes for deleted checks, relaxed thresholds, swallowed
  errors or mocks that cannot prove the claim. Do not silently weaken a gate.
- Preserve Godot 4.6.2, Compatibility, GUT and Android support.

## Quality pass

- Check error paths, resource cleanup, bounds on effects/caches and per-tick work.
- Extract shared behavior only when callers' contracts agree and it reduces risk
  or net complexity. Keep intentional differences explicit.
- Verify documentation and claimed checks against actual results. Never call a
  headless run proof of physical Android accessibility or performance.
- Retain existing owner-supplied art policy and owner-only version changes.

For each finding, quote a real file:line, concrete trigger, impact and smallest
fix. Verify the motivating lines and search sibling paths before reporting.
Separate verified defects from hypotheses and unavailable checks; do not invent
confidence or performance figures. Order findings by player/data/authority impact.
For an authorized implementation task, fix in-scope defects and rerun affected
checks. A review-only request changes no implementation. Never post review comments
or merge on the user's behalf without explicit authorization.
Finish with the checked scope, findings/disposition, tests and residual limits.
No background specialists, telemetry, global state, CLI helper or browser stack required.
