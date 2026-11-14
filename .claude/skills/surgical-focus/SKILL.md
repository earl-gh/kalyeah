---
name: surgical-focus
description: >
  Keep turns fast and low-token without cutting correctness: locate symbols with
  grep first, slice-read only touched functions and 1-hop seams, batch parallel
  reads, skip unsolicited repo-wide audits, run targeted seam tests during
  iteration, and reply in one concise screen. Pairs with ponytail on every task.
---

# Surgical Focus

`ponytail` cuts code bloat; `surgical-focus` cuts context, turn, and test bloat. Solve the exact task with the fewest reads, tool rounds, and test runs required to prove the touched seam works.

## Scope boundary

- **Owns:** context gathering, search/read granularity, tool-call batching, iteration test selection, and reply brevity.
- **Defers to:** `ponytail` for minimal code shape, `ponytail-audit` when the user explicitly requests a whole-repo audit, and `tdd` for red-green-refactor test design.

## 1. Locate first, then slice-read

- Run `rg -n` or `git grep -n` for exact class, method, signal, or resource field names before opening files.
- Read only `StartLine..EndLine` around the target function plus 1-hop direct callers, callees, and data contracts.
- Never read entire directories, untouched sibling systems, or 400+ line docs (`docs/GDD.md`, `docs/ROADMAP.md`, `docs/PROJECT.md`, `docs/ART.md`, `GLOSSARY.md`) end-to-end when one grepped section answers the question.
- Do not open generated imports, `.uid` files, binary assets, or lockfiles unless debugging an asset/import failure.

## 2. Single-pass batching & bounded thinking

- Batch all independent searches and file slices into a single parallel tool turn instead of sequential one-file-at-a-time loops.
- Stop reading the instant you have enough evidence from code and data contracts to edit or answer.
- Never re-read a file slice already in context unless an external process mutated it.
- Keep pre-edit reasoning bounded to the concrete diff and its 1-hop callers; never write multi-paragraph pre-edit essays or restate the user prompt.

## 3. Touch only the active seam (no unsolicited audits)

- Trace breakage strictly along the connected call/signal/data path of your change.
- Never run unrequested whole-repo audits, formatting sweeps, file splits, dead-code hunts, or tangential refactors in adjacent files.
- If you spot an unrelated bug outside your seam, note `file:line` in one sentence at the end of your reply and leave the file untouched.

## 4. Targeted seam testing during iteration

Run the narrowest test that exercises the changed seam while iterating:

| Stack / Layer | Iteration command (fast seam check) | When to escalate to full suite |
|---|---|---|
| Godot GUT (`tests/unit/`, `tests/integration/`) | `godot --headless -s addons/gut/gut_cmdln.gd -gselect=test_<seam> -gexit` | Shared core contracts (`PlayerInput`, `GameRules`, snapshot wire format) or final PR gate |
| Unity Test Framework (`EditMode` / `PlayMode`) | `unity -batchmode -runTests -testPlatform EditMode -testFilter <NamespaceOrClass>` | Assembly-wide refactors, `SimConfig` schema changes, or pre-PR gate |
| Shell / CI scripts (`.github/scripts/*.sh`) | `bash -n <script.sh>` + targeted fixture test (`test_inspect_apk.gd`) | Workflow YAML or release-gate changes |

- Never run the entire test suite after every tiny edit when a single `-gselect` or `-testFilter` proves the behavior in seconds.
- Do not re-test unchanged systems.

## 5. One-screen output discipline

- Lead with the result or fix, not a chronological diary of what you searched.
- Link every referenced file and line range (`[file.gd](file:///.../file.gd#L10-L24)`).
- Skip prompt recaps, filler transitions, and boilerplate summaries; close with Ponytail's 1–2 line skipped/risk note when shipping code.

## Anti-patterns to reject

- Reading all of `scripts/sim/` or `scripts/ui/` "to understand the architecture" before a localized fix.
- Running full GUT or PlayMode suites three times in one turn for a single-file rule tweak.
- Turning a 10-line bug fix into a 6-file cleanup pass.
- Writing 300 words explaining a 5-line diff that speaks for itself.
