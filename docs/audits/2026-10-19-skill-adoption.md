# Requested skill adoption — 19 October 2026

Reviewed against main `dbef716` after the accessibility PR (#175) merged.
This is a skill-installation and focused accessibility review, not a new whole-game
AAA/readiness certification. The expected gameplay load is up to six players (3v3).

## Installed and used

awesome-gamedev-agent-skills supplies seven selected Godot/UI/performance skills
with their references, plus a project-scoped router adaptation. Ponytail supplies
its unmodified standalone implementation and audit skills. gstack supplies three
compact Kalyeah ports: engineering plan review, change review and investigation.
Exact upstream commits, file hashes, adaptation modes and source paths are in
`.claude/skills/sources.lock.json`; original licenses/NOTICE are retained.
Claude and Codex share the canonical skills through the relative `.agents/skills`
link. There are 13 additions alongside the five existing specialist skills.

Ponytail's reuse/minimal-change ladder was applied to the installation: select
Markdown and references, reuse existing specialists/GUT, and share one skill tree.
No installer package, browser runtime, lifecycle hook, telemetry, external model
integration, automatic update, new engine, addon or game dependency was introduced.
The gstack ports are methodology adaptations, not full upstream CLI installs.

The game-dev upstream's Godot 4.7 examples were identified as a compatibility risk.
A marked local notice on each selected skill preserves the pinned 4.6.2 engine and
requires checking APIs before use. The router preserves fixed 30 Hz simulation,
server authority, canonical input, portrait layout, owner-supplied art and the
single-threaded Web build. Generic ENet, C#, art-generation and other-engine skills
are excluded. Existing implementation ownership remains unchanged.

## First review using the skills

Used `ponytail`, `game-ui-ux`, `godot-ui-control` and `gstack-review`, with the
existing presentation/QA specialists, to trace the recently merged accessibility
path and the installation diff. The review inspected connected callers and tests,
not only changed lines. It found no new verified defect in this bounded scope.

| Review question | Evidence inspected | Result |
|---|---|---|
| Does text size compound or reset choices? | `UiAccessibility.valid_scale/apply_node`, loadout consumers, `test_accessibility_preferences.gd` | Original font-size metadata and malformed-value checks exist; regression coverage preserves selections. |
| Can menu navigation move/cast? | `MatchHud.toggle_menu/set_controls_active`, `PracticeMatch._step_once/_local_input`, `MatchInput.clear` | Both local and online branches use neutral/cancel input while blocked; regression coverage checks held aim cancellation and recovery. |
| Do modal focus and enlarged layouts have checks? | `MenuDialog._input/open/close`, `UiFocus`, existing focus and largest-room/dialog/loadout tests | Enabled/visible Tab cycle and opener restoration are covered; prior visual evidence remains in the accessibility audit. |
| Are safe-area examples copied blindly? | `LobbyWidgets.apply_safe_area`, layout/dialog callers | Existing physical-pixel to logical-viewport conversion must be reused, rather than replacing it with upstream's simplified example. |
| Does the skill diff weaken a gate or alter gameplay? | Full installation diff, manifests, original five specialists | Documentation/skills only; no test, runtime source, balance, export preset or version change. |

This review makes no new device performance claim. The existing 140% screenshots
were produced for PR #175; this documentation-only change does not change pixels.

## Validation and follow-up

Installation checks verify 13 unique names/frontmatter, 20 installed-file SHA-256
hashes, pinned source commits, local reference files, preserved licenses/NOTICE
and the shared Claude/Codex path. Godot 4.6.2 import and single-threaded Web
pack export succeeded. The full GUT run had 456 tests: 455 passed, zero failures,
and the existing model-surface test pending with three torn-mesh findings.
There were 6,868 assertions. Existing four null-material cleanup errors, deprecated
helpers/orphan reports and ObjectDB shutdown warning remain visible; they are not
new fixes or clean-run claims. The exported pack was checked for both skill paths;
neither skill tree is packaged. Logs: `/tmp/kalyeah-skills-{import,gut,export}.log`.

The next product work remains native Android validation: TalkBack, cutouts,
OS scaling, keyboard occlusion, multitouch, resume/reconnect, and populated 3v3
CPU/GPU/memory/thermal profiling. Full gamepad/switch access and increased contrast
remain separate accessibility work. Existing Toni/Popoy/Inday torn meshes and
headless cleanup warnings are not fixed by skill installation.

Before future PRs, route to one owner, load only needed references, apply Ponytail's
smallest-complete change rule, then use the gstack review port and the existing
QA/release checks. Upstream updates are deliberate reviewed changes, not background
network activity. An agent may need a project/session reload to refresh discovery;
explicitly reading the SKILL.md works immediately.
