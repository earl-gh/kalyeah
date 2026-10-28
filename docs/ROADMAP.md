# Kalyeah roadmap

What is built, what is next, and the rules for building it. Rules and numbers live in `GDD.md`; look and feel in `ART.md`; scope in `PROJECT.md`.

## Done

| Milestone | What shipped |
|---|---|
| M0 Scaffold | Godot 4.6 project, Android + server export presets (a web preset and a Pages deploy were here until the Android-only change), GUT, title screen |
| M1 Sandbox | Sim core (fixed 30 Hz step, seeded RNG), movement and collision, follow camera, touch input, death delay, Dash and Bookmark, practice dummies, six random characters |
| M2 Weapons | 12 weapons as data over 9 generic shapes, status effects, weapon pick (10 s) and respawn swap, aim and cancel, cooldown rules (no precasting, auto-aim locked at the press) |
| M3 Objectives | Breakable cardboard walls, base scoring, sets and side switch, the guava neutral (heals half HP on pickup, can be thrown), tricycle |
| M4 Multiplayer | Rooms with passcodes, headless authoritative server, snapshots, prediction and reconnect, lobby screens, Dockerfile and deploy guide |
| M5 Look and feel (first pass) | Procedural low-poly street, props and kids, themed UI, vector icons, synthesised audio, MOBA HUD, camera, rigged kid animation |
| Tag street (owner redesign) | The street-game redesign from the reference video: a 24 × 48 m barangay street with your post bottom left and theirs top right, a matching tall minimap, a few cover pieces (crates, benches, a sawhorse, a NO ENTRY board) that block both teams, Tag (always, at 0 HP) with taped boxes and rescues, captures (whole team in the enemy base, or every enemy tagged) held 5 s, first to 3; the guava, the tricycle, the breakable walls and the death delay are gone; a 40° wide-lens camera |
| Controls (owner) | The movement stick at the bottom left with weapon 1 and 2 at the bottom right (tilt was tried and removed), no dash and no pin; one-tier scoring, first to 3 / 5 / 7 picked by the host in the lobby, every point a new round; score at the top right |
| Hotspot rooms (owner) | Create starts the room server inside the Android app; Join verifies the host's active room on the hotspot automatically, no address or room code |
| Hotspot working (owner) | Create checks that the hotspot is on and Join checks the Wi-Fi, each in a modal with Try again and Cancel; Join finds the host by a UDP broadcast the host answers (the host is not the ".1" on stock Android), verified over the WebSocket; the failure shows the phone's network. Audit, assumptions and the phone test script: [Hotspot audit](audits/2026-10-26-hotspot.md). Needs the phones: nothing here proves the access point, the Wi-Fi's broadcast filtering or the settings button |
| Map 1:2 (owner) | The playing area is 24 × 48 m (1:2, was 30 × 40 m 3:4); the minimap is 90 × 180 px and draws the street at exactly 1:2; posts, boxes, cover and dummies re-placed |
| Character select (owner) | A fighting-game style select at the start of every match: six 3:4 profile cards, then a tab per street game over weapon cards (art, name, description below); the kid you pick is the kid you play (also your main-screen face); a team never has the same kid twice (the other team may have it once); online, the server settles swaps and greys out kids a teammate locked in |
| Map 1:2 (owner) | The playing area is 24 × 48 m (1:2, was 3:4 24 × 32 m); the minimap is 100 × 200 px at exactly that shape; the cover wall keeps its distance from the base |
| Home UI refinement (owner) | Bungee typography, a compact profile bar, the titled Agawan Base model, one primary Online action, concise dialogs and a responsive phone layout |
| Shared lobby and UI hierarchy | One Online/Hotspot room view, Online code copying, guest format summaries, shared typography/spacing/color roles, readable guide/history/HUD, bounded dialogs, and explicit setup/exit navigation. Accessibility and device follow-up in [UI audit](audits/2026-10-19-ui-hig.md). |
| UI preferences and navigation | Reduced decorative motion, modal focus/navigation and keyboard-safe match settings. Owner-requested simplification removes text-size modes and uses one readable scale; [current GUI follow-up](audits/2026-10-19-ui-density.md). |
| Loadout and control feedback | Untimed Practice setup, saved validated weapon pairs, explicit replacement slots, larger readable cards, contextual control hints, persistent reconnect state, and truthful result actions |
| Lobby and match UI refinement | Narrow-phone lobby and loadout layouts, concise room states, unified match announcements, and a score card with clock and first-to context |
| UI rebuild, phase 1 | A code-drawn game skin (`UiSkin`: glossy buttons that sink into a lip, lit panels, soft shadows), a component kit, and a rebuilt Home (hero carousel, tab bar), pages, dialogs and room cards. Round 2 added the Loadout, the shared choice buttons, a Play-only top bar and a gold-only-for-the-main-action rule. The Weapons page groups by street game or by type into headed sections of tiles, with a detail sheet. The match HUD waits for a device-checked pass. Critique and decisions: [UI rebuild](audits/2026-10-20-ui-rebuild-critique.md). |

| Lead review, P0 and P1 | The stranded UI, cast-feedback and sparring-bot work on `main`; fair round rules (no team-0 tie-break, contested bases, optional overtime and timed box release) and a team-round bot harness. [Review](audits/2026-10-21-lead-review.md), [round rules](audits/2026-10-21-round-rules.md). Then P2 weapons ([weapon pass](audits/2026-10-21-weapon-pass.md)) and P3 a 3v3 Practice against bots ([practice vs bots](audits/2026-10-21-practice-bots.md)). Next: P4 UI polish, P5 device checks. |
| Card tray audit 4 | Cancel mark out of every full aim, tray wells under the cards, *Nobody in range* / NO TARGET feedback, a touch the system cancels keeps the card, one RESCUE marker under the box and no per-kid *Tagged*, drawn text at the 22 px floor, kid names that fit, results layering; protocol v2 checked at the handshake, no mid-match PICK, the shuffler queue in snapshots, reconnect hands with an empty slot; effects in the thrower's team colour from frame one, colour-blind self near white |
| Visual pass (code-drawn) | Lighter street shadows and calmer cement, readable bases and rescue areas, chunky rounded bars, bigger impacts and landing rims, big outlined banners, a bevelled tray and D-pad, deeper buttons with unchanged sizes, teammate faces and the release ring while tagged, a grouped results card, a stage and name on the kid screen; art briefs in `docs/art-briefs/` |
| UI kit pass | One corner scale (`UiSkin.RADIUS_S/M/L/XL` 8/16/24/32 and `round_radius`), one focus ring that follows its control's curve, one display scale for big words (`UiMetrics.HEADLINE/DISPLAY/HERO/SCORE` 40/56/80/120 lettered by `UiKit.letter`), a shared press feel on every styled button (off with Reduce motion) and one breath on Home's main action; lint tests keep bare radii and font sizes out of `scripts/ui` |
| Tagged on the sidewalk (owner) | Tagged kids wait outside the street on the sidewalk, facing in, level with their team's rescue area; the rescue area is one 3 × 6 m rectangle against the street's side edge (no inner box); base and rescue outlines half as thick (0.15 m). Bots, practice dummies, pips and the tagged texts follow; `team_rounds.gd` now counts rescues. [Balance and thresholds](audits/2026-10-25-sidewalk-tagged.md): no number moved. |
| Corner bases (owner) | The street's four corners are rounded with the base's radius (`base_corner`), the sim keeps kids on the curve (`MapLayout.keep_in_street`), the posts stand at the centre of their corner's curve, over each kid your own bar says *You* and every bar has a numbered team badge (the room's numbers, stacked where kids stand together), and a tagged kid on the sidewalk has no ring. [Balance check](audits/2026-10-25-corner-bases.md) |
| Android only (owner) | The game is played and tested as an APK: the Web export preset, the Pages deploy, the web-only code (orientation gate, touch-cancel shim, hotspot layout preview, `?server=` and `?dev=1`) and every desktop path (keyboard, mouse, right-click, WASD, wheel, Esc) are gone, with the developer tools (UI gallery, seven-tap switch, practice damage and status buttons). `Android APK` is the one workflow and the quality gate: import, full suite, debug APK, APK inspection, and a manual emulator smoke test. The Back button no longer quits the app: it closes a dialog, then a page, then asks to quit; in a match it opens the match settings. The preset lost a stale option and gained the Vibrate permission, the only one the code needed beyond Internet. Device checks remain in [Android validation](audits/2026-10-19-android-validation.md). |

## Short-phone GUI follow-up

Loadout uses compact touch-sized portraits, full-width weapon effects/stats and a
fixed Ready action. Timed setup groups the auto-pick explanation with its heading.
Lobby identity lives in the status tag so an ellipsized name cannot hide You.
Validation and remaining GUI work: [GUI follow-up](audits/2026-10-19-gui-loadout.md).

## Android validation follow-up

Application interruptions now cancel local gestures and immediately send stop/cancel
input to a connected server, even if no further client frame runs. Regression
coverage includes pause/focus ordering, quick resume, real touch state, stopped
client polling and disconnects. The [Android validation record](audits/2026-10-19-android-validation.md)
separates automated evidence, APK build status and outstanding physical-phone tests.

**Local (hotspot or Wi-Fi) validation checklist** (full script in the [Hotspot audit](audits/2026-10-26-hotspot.md)): with a stock phone, a phone with a maker
skin and a third one joining, check: Create with neither a hotspot nor a Wi-Fi (also on mobile data only) says so and never makes a room;
Network settings opens; Create works with the hotspot on and with only a Wi-Fi; Join says to connect when there is no Wi-Fi, says no room before Create and
joins within seconds after; full and started messages; Create again after leaving; the host in the background for 10 s; mobile data on
and off; and report the exact message of any failure with the phone model.

## Development workflow

Selected awesome-gamedev-agent-skills, standalone Ponytail and compact gstack
workflow ports are installed for Claude and Codex. Selected Matt Pocock TDD/domain
skills and a project-authored gameplay-engineering companion add behavior-first
methods without a new engine or test framework. The five project specialists
retain ownership. [Catalog](../.claude/skills/README.md), provenance lock and
[first-use review](audits/2026-10-19-skill-adoption.md) record scope and validation.

## Next: the Kalyeah visual overhaul

Goal: the look of the owner's reference image (League of Legends mid view, Clash of Clans design language, Filipino street): soft, chunky, painted-looking 3D with readable effects. Procedural boxes are not enough; real Blender-authored assets are.

| Phase | Work | Done when |
|---|---|---|
| P0 ✅ | Rename to Kalyeah; docs cleaned up (this file replaces `HANDOFF.md`) | no stale names or docs; tests green |
| P1 ✅ | The match screen is split into `MatchInput`, `MatchHud` (all HUD built in code) and wiring; `practice.tscn` is 79 lines | behaviour unchanged, tests green |
| P2 ✅ | HUD spec below | screenshot matches the spec |
| P3 ✅ | League of Legends camera | numbers below, camera tests updated |
| P4 | 3D kids (rigged GLB models): done, all six kids use their own rigged and animated model and portrait | all six kids use their own model |
| P5 | Environment kit in Blender (tin roofs, cardboard sheets, tyres, plants, hoop, drains, tricycle with driver, base post with posters) | street matches the reference |
| P6 | Effects: impact debris, trails, dust, ground rings, per-weapon projectiles (ground rings and code-drawn impacts done in the visual pass; sprite sheets briefed in `docs/art-briefs/`) | every weapon has a clear effect |
| P7 | UI pass: painted skill icons, team portraits and timer across the top, framed minimap | HUD matches the reference |
| P8 | Performance and Android: frame rate, download size, APK | budgets below |

Budgets (M6): 60 fps on a recommended Android phone, 30+ on the minimum; APK under 200 MB (enforced by the `Android APK` workflow; it was 150 until the owner's Junjun, Ligaya and Migo models were added at full quality, owner's call; the run measured 192 MB with them, so the limit is 200). The prop, weapon and kid textures are imported **VRAM compressed** (`compress/mode=2`: ETC2 on the phone, a quarter of the memory of a lossless texture on the GPU; the normal maps flagged `compress/normal_map=1`), pinned by `tests/unit/test_texture_budget.gd`. Lossless, the three first authored kids and the 11 houses and carts had put the APK at 205 MB; compressed it measured about 145 MB (the textures are 165 MB of imported files lossless, 105 MB compressed), 148 MB before the last three kids. **Estimate for Junjun, Ligaya and Migo (the Android APK run measured 192 MB in the end):** their imported files are 40.0 MiB of ETC2 textures (Junjun 18.7, Ligaya 10.7, Migo 10.7; Junjun's roughness/metal map is 4096 px, 10.7 MiB of it) and 6.7 MiB of meshes (0.8, 2.4, 3.4), minus the 4.3 MiB of the old baked models they replace: about +42 MiB, so about 190 MiB if the APK stores them as they are. Capping Junjun's roughness/metal map at 2048 px (`process/size_limit=2048` in `junjun_0.png.import`) would save 8 MiB. A new 3D model's textures must be imported the same way.

### HUD spec (owner)
- No player name above the health bar.
- A status such as *Stunned* is **italic** and shown **above** the health bar.
- (Superseded: no cooldown bars under the health bar since dash and the pin were removed.)
- Skill buttons are art only, plus the weapon type (Attack, Control, Block or Heal, in capitals) as a small tag. No health panel in the top-left corner.

### Camera spec (owner): superseded: now orthographic at 45° (see `docs/GDD.md`); the original League of Legends numbers follow for history
- Pitch **56°** from horizontal, field of view **30°** (vertical), far camera with a flat perspective.
- The camera distance is derived from the screen shape so about 13 m of the lane width is visible on any phone; the angle and field of view are not changed for that. It looks 2.5 m ahead of the hero.
- It follows the hero and eases after them; it does not tilt or roll.

## Rules for building (to avoid retries)
1. **No regex on `.tscn` files.** Complex screens are built in code and tested; scene files stay small and stable.
2. **Every text replacement is guarded:** it must match the expected number of times or the edit stops.
3. **One concern per commit.** Each step ends with: project import, the full GUT suite, and a 720×1280 screenshot checked by eye.
4. Files over about 400 lines are split before they are edited again.
5. Pull requests are meaningful chunks, opened by Claude and merged by the owner. See `CLAUDE.md`.

## Decisions log
- No basic attack (see `GDD.md`).
- Tag street (owner): the map, the rules and the camera follow the reference video of the street game; portrait stays binding. The guava, the tricycle, breakable walls and the death delay were removed.
- Game name: **Kalyeah**. Tagline: *Mga larong Pinoy sa kalye*. Android package `com.kalyeah.game`.
- Environment assets are made in Blender (the `bpy` Python module works in the cloud session). The kids are rigged, animated GLB models from the owner; `tools/blender/build_kid_game_model.py` makes them light for phones (`build_junjun_model.py` was the first, Mixamo-based Junjun; the owner's rebuilt-rig boy now goes through the same script).
- Multiplayer is built and tested; deploying the server and setting `KALYEAH_SERVER_URL` is the owner's step (`server/README.md`).

## UI room-flow polish

Code-free hotspot discovery now checks Kalyeah protocol and active-room availability before connecting. Lobby choices persist into matches, with team-unique reservations. The hotspot room uses a compact map header, larger team rows and touch targets, best-of controls, scrollable kid cards and a readiness explanation. Character-book portraits reuse owner-provided face art. Online layout and room header/map/player presentation are separated from controller logic. Gateway discovery was replaced by a broadcast the host answers ([Hotspot audit](audits/2026-10-26-hotspot.md)). Signed Android delivery and the physical phone-to-phone checks of that audit remain release work.
