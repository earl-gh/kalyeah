---
name: gameplay-engineering
description: Apply behavior-first engineering to Kalyeah gameplay changes by connecting player intent, fixed-tick rules, authority, presentation and device evidence through the existing specialists and GUT tests.
---

# Gameplay engineering — Kalyeah companion

A project-authored adaptation of Matt Pocock's TDD and domain-modeling methods
for this game. It is not an official Matt Pocock game-development skill.
Provenance and source hashes: `../sources.lock.json`; license: `../licenses/mattpocock-LICENSE`.

Act as a lead gameplay engineer responsible for both player experience and
competitive correctness. This is a workflow across existing roles, not a sixth
implementation owner. Read `CLAUDE.md`, `GLOSSARY.md`, the relevant GDD section,
`../tdd/SKILL.md` and the responsible specialist. Keep the current engine and stack.

## One player behavior at a time

1. State the contract in player language: when this input/event occurs in this
   state, what should the player and other players observe? Name the normal,
   interrupted and recovery cases. Resolve against the GDD; never invent balance.
2. Select one existing public testing boundary and a responsible specialist.
   Use the current task's authorized interfaces; raise only unresolved product
   choices. Trace callers before changing shared behavior. Use `domain-modeling`
   when a term actually needs clarifying, not to create speculative documentation.
3. Reproduce a real failure, or write one meaningful failing GUT test for the
   requested behavior. Run it and establish why it fails. Expected results come
   from the rule/contract, not a copy of the implementation. Do not mock the rule
   being tested, write every imagined test up front, or test private call counts.
4. Implement only enough to satisfy that behavior at its owning layer. Run the
   same test; then take the next necessary vertical slice. Keep unrelated
   refactoring for a separate justified change. Ponytail's smallest-complete rule
   applies throughout.

## Pick the right proof

| Concern | Owner and public boundary | What that proof does not establish |
|---|---|---|
| Tag, rescue, capture, weapon rule | gameplay-simulation; MatchSim inputs, fixed steps and observable state/signals | Touch feel, networking delivery or animation clarity |
| Room, authority, reconnect | authoritative-multiplayer; real GameClient/GameServer flows and observed match state | Radio behavior or performance on physical phones |
| Controls, menu and interruptions | android-performance with presentation; real input events, public Node notifications and visible control behavior | Native OS event delivery, TalkBack or physical multitouch |
| Animation, camera and feedback | gameplay-presentation; sim events into the actual view, controlled playback and rendered observation | Competitive correctness or target-device frame pacing |
| Performance and build size | android-performance; representative device profiles or measured exported artifacts | Speed gains from code appearance alone |

## Protect the game

- Keep the deterministic 30 Hz sim free of nodes, device polling and rendering.
  Use match-owned seeded randomness and typed data resources. Same inputs/seed
  must produce the same rule outcomes at different rendering frame rates.
- Preserve PlayerInput and server authority. Check the changed contract in
  Practice and the shared Hotspot/Online match path as relevant. Do not replace
  loopback integration with mocks or label it real phone/radio coverage.
- For input changes, check press, drag, release, cancel and fresh input after
  interruption. Menu opening, focus loss, pause, disconnect and reconnect are
  distinct cases; cover only those affected, through the actual boundary.
- Define readable anticipation, action, impact and recovery for changed feedback.
  Reuse event hooks; preserve telegraph/hit/cooldown timing and cosmetic characters.
  Honor Reduce motion. Never use Engine.time_scale or pausing the sim as visual
  hit-stop in multiplayer, and never generate owner-controlled art.
- Keep effects and allocations bounded. Measure before pooling, batching or
  compressing. Asset exclusions must account for dynamic runtime loads.

## Finish with evidence

Run the relevant focused checks, required full GUT/import and affected exports.
For visible changes, inspect 720x1280 and narrow-phone layouts and record observed
results. Report red/green evidence, changed behavior, remaining limits and exact
build/device/scene for measurements. Distinguish headless, browser, APK compilation
and physical-phone proof. Use the existing release process; never merge or edit
versions by hand. Do not spawn agents merely because several roles are named.
