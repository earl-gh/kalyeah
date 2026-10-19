# Behavior-first engineering skills — 19 October 2026

Base: main `f9c8c33` after the application-interruption fix merged.

## Selection and provenance

Added Matt Pocock's `tdd` and `domain-modeling` from `mattpocock/skills`, pinned at
`49dd158d1076134a641b33efb035946536778336`, with supporting Markdown, agent metadata
and the original MIT license. Marked local integration notices retain Godot 4.6.2,
GUT and existing specialist ownership. The TDD notice explicitly overrides the
upstream rule requiring repeated confirmation of already-authorized test seams;
new public interfaces or unresolved game rules still require clarification.
The test-first and behavioral-proof requirements remain intact.

Added `gameplay-engineering`, a project-authored companion adapted from Matt's
methods. This is not an official Matt Pocock game-development skill. It connects
player intent, rule outcomes, multiplayer authority, input interruption/recovery,
animation readability and measured device/build evidence. It routes implementation
to the existing five specialists; it does not add an owner, toolchain or agent.
Its source attribution and hashes are in `.claude/skills/sources.lock.json`.

The project now has 21 skills. The shared `.agents/skills` link keeps Codex and
Claude on one canonical tree. No plugin installer, hooks, new test runner,
JavaScript runtime dependency, engine upgrade or runtime game changes are included.
Overlapping diagnosis/review bundles were not added. Generic headless-runner
recipes assume a project without a testing addon; Kalyeah already uses GUT.

## First use

Used `domain-modeling` to record the established player vocabulary in `GLOSSARY.md`:
Agawan Base, Tagged, tagged box, rescue, the two scoring conditions, round/match,
loadout and the two multiplayer paths. The definitions preserve the owner's
Agawan Base correction and distinguish code-free Hotspot from coded Online.
Rules/numbers remain in GDD; the glossary contains no implementation details.
No new architecture decision was made, so no ADR was created.

Applied the companion/TDD criteria to the existing interruption regression as a
bounded example rather than inventing a feature just to demonstrate a skill:

| Question | Observed contract/evidence |
|---|---|
| Player outcome | Backgrounding while moving/aiming must stop movement and cancel the held aim; fresh input must work on return. |
| Responsible layers | The match view interprets lifecycle input; the existing GameClient transports canonical input; the server decides outcomes. |
| Meaningful proof | `test_backgrounding_stops_server_input_without_another_view_frame` uses real loopback WebSockets, stops client rendering/polling, checks authoritative movement/aim, then checks fresh input after resume. |
| Independent expectation | Cancellation comes from the documented interruption behavior; the test does not derive its expected state by reimplementing the input code. |
| Remaining test boundary | Existing lifecycle tests invoke the notification callback directly. New lifecycle tests should prefer public Node notifications when verifying engine notification propagation. The existing checks prove handler/network behavior, not native OS event delivery. |
| Player experience limit | Headless tests cannot certify animation readability, TalkBack, touch latency or populated 3v3 performance. Use the existing physical-phone matrix. |

No new red/green implementation cycle is claimed for a skills/documentation-only
change. Existing gameplay tests are rerun; no assertions, thresholds or CI gates
are weakened to satisfy the new workflow.

## Validation

Install checks verify all 21 discoverable skills, 29 pinned installed-file hashes,
four upstream provider commits, local Markdown references, licenses and the shared
Claude/Codex directory. Godot 4.6.2 import and single-threaded Web export passed.
The full GUT run had 461 passing tests, zero failures, 462 tests total and 6,918
assertions; the existing model-surface test reports three pending mesh findings.
The 154,607,520-byte Web pack is SHA-256-identical to the pre-install pack and
contains neither skill tree. Existing headless material-cleanup
errors, orphan reports, deprecated helper notices and shutdown warning remain
visible; they are not claimed as fixed by this installation. Game rules, assets, export presets and release versions are
unchanged; a new UI screenshot or APK build would not validate this installation.

The existing physical Android checks, package-size overage and torn character
meshes remain in the [Android validation record](2026-10-19-android-validation.md).
These skills improve the working method; they do not establish release readiness.
