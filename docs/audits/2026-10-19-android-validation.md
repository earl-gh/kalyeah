# Android interruption fix and validation — 19 October 2026

Base: main `1d30ff6` after the skill installation merged. Used Ponytail to keep the
implementation in the existing match notification/input path; no new manager,
framework, protocol message, runtime dependency or performance overlay is needed.
The Android/performance and QA workflows plus the local gstack investigation/review
ports guide the checks. No game balance, art or export settings change.

## Verified defect and fix

The previous focus/pause handler cleared local gestures but relied on another game
frame to send cancellation. The server intentionally repeats the last accepted
input until another arrives. With client rendering/polling stopped, a reproduced
WebSocket match therefore continued moving and holding the weapon aim. Input was
also not gated by application focus/pause state. Clearing the input before asking
the touch widgets to cancel could queue a synthetic held tick for the old release.

`PracticeMatch` now tracks focus and pause independently, clears gesture callbacks
before retaining a cancel input, blocks local input while either state is active,
and sends one neutral/cancel input immediately on each interruption notification
when connected. It uses the existing input counter, connection/WELCOME guards and
`GameClient.send_input` with zero prediction delta; it does not step the sim early.
Notification ordering and duplicate pause/focus events do not restore controls
until both conditions clear. Menu, pick and connection gates still apply.

Before the fix, lifecycle unit checks failed and the real WebSocket reproduction
showed a growing aim hold and continued authoritative movement. After the fix,
that test stops both the match view and its client's network polling after the
notification; the server receives cancellation, stands still and accepts fresh
input on resume. Four focused lifecycle tests cover keyboard aim, independent
focus/pause ordering, actual joystick/aim touches and pause/resume between ticks.
A separate socket-drop regression verifies existing server cancellation behavior.
The latter already passed before the fix; no server change was necessary.

## Validation gate repair

The existing knockout-pose test passed alone but failed in full runs. A focused
probe measured the same clip time (1.5 s) with hip height 0.5757 m before evaluating
the settled blend and 0.1053 m after a zero-time advance. The old test mixed its
explicit advance with an automatic animation clock and sampled before the blend
was evaluated. It now uses a manual playback clock, evaluates the settled blend
at the same time, and awaits process frames for the skeleton. The lying-height
threshold and recovery checks are unchanged; no production animation changed.
Temporary probe output was removed. This repairs the test rather than retrying
until a passing timing happens or increasing sleeps.

## Available validation

This workspace has Godot 4.6.2 and Java, but no Android SDK/adb or attached Android
phone. Local headless tests can exercise state transitions and real loopback
WebSockets; they cannot establish TalkBack, Android event delivery, touch latency,
GPU timing, thermals, radio behavior or physical cutout correctness.

Local Godot 4.6.2 import and single-threaded Web pack export succeeded. The full GUT suite passed with 461 passing
checks, zero failures, 462 tests total and 6,918 assertions. The existing
model-surface test still reports three pending Toni/Popoy/Inday mesh findings.
Four headless material-cleanup errors, existing orphan reports, 19 deprecated
helper notices and the ObjectDB shutdown warning remain visible in the log.

Use the existing manual **Android APK** workflow on the feature branch to build a
debug APK. The run/artifact link and exact automated results are recorded in the
PR. A successful APK build proves export/build compatibility, not device readiness.
The feature-branch [Android APK run](https://github.com/earl-gh/kalyeah/actions/runs/39209731329)
succeeded at runtime commit `fc33cf2` and uploaded
[artifact 12167545706](https://github.com/earl-gh/kalyeah/actions/runs/39209731329/artifacts/12167545706)
(`kalyeah-debug-apk`). The artifact archive is 181,667,160 bytes. Local artifact
download was denied with HTTP 403 by the download endpoint, so its contained APK
was not independently inspected here; use the authenticated GitHub artifact link.

The local Web pack is 154,607,520 bytes versus the pre-fix pack's 154,600,964 bytes
(a 6,556-byte increase). The existing package-size budgets are not met by these
raw artifacts; final APK file size and compressed Web delivery still need direct
inspection. Inventory exported assets before choosing exclusions or compression;
do not remove dynamically loaded character/weapon resources blindly.

No deployment or release signing is part of this change. UI pixels are unchanged;
no new visual screenshot is claimed for this input-only fix.

## Physical-phone acceptance record

Record build SHA, device/model, Android version, RAM, display/font scaling, network,
player count, duration and observer. Start every result as **not run**. Include a
minimum phone and a recommended phone from `docs/PROJECT.md`, plus a tall/cutout
screen. Keep one row per device/build when recording failures.

| Check | Procedure and required result | Current result |
|---|---|---|
| Interrupt movement and aim | Auto-run and hold a weapon; open the notification shade, app switcher and lock screen separately. Teammate observes stopped movement and cancelled aim once the stop reaches the server. No cast on return; fresh touches work. | Not run on hardware |
| Event ordering/repetition | Repeat lock/unlock and foreground/background cycles; open a menu before backgrounding. No stuck controls, duplicate cast, auto-run restart or menu bypass. | Not run on hardware |
| Multitouch | Move with one finger and aim with another. Cancel/lift each independently, interrupt the gesture and resume. Ordinary deliberate releases still cast; cancelled gestures do not. | Not run on hardware |
| Reconnect | Disable Wi-Fi/switch network while moving/aiming, restore inside and outside the existing 60 s reconnect window. Server confirms the original slot or offers Return Home; no ghost input. | Not run on hardware |
| Hotspot | Use actual host and guest phones. Join discovers an active host room without a code; absent/closed room fails clearly. Test host background/return and host leaving. | Not run on hardware |
| Online | Create and join by the generated eight-character code; populate 1v1, 2v2 and 3v3. Check readiness, character reservation and reconnect state on both sides. | Not run on hardware |
| Large text and cutouts | At 140% in-game text and enlarged OS display/font settings, open guides, settings, full 3v3 room and loadout. Critical actions remain reachable; controls avoid cutouts/home area. | Not run on hardware |
| Keyboard and Back | Rename with the virtual keyboard, submit/dismiss it and use Android Back: on Home it asks *Quit game?*, on a page it closes the page, on a dialog it closes the dialog, in the lobby it leaves the room, in a match it opens the match settings (Resume, Leave) and never quits by itself. Fields/actions remain reachable; backing out does not cast or leave accidentally. The emulator smoke test only proves Back on Home does not close the app. | Not run on hardware |
| Vibration and install | Settings shows the Vibration switch; a button press and a hit buzz (`VIBRATE` is the only permission besides `INTERNET`). Install a second APK over the first (same debug key) and check the settings and name survived. | Not run on hardware |
| Assistive access | Enable TalkBack and check names, roles, reading order, values, actions and modal focus. Check switch access and physical keyboard separately. Visible text/tooltips are not proof of support. | Not run on hardware |
| Populated 3v3 performance | Profile representative repeated matches with six players and overlapping weapons, reconnect and scene churn. Record CPU/GPU frame time where supported, memory, draw calls, load time, network latency and thermal behavior. | Not measured on hardware |

Use the existing budgets: 60 fps on recommended hardware (16.67 ms/frame), 30+
fps on minimum hardware (33.33 ms/frame), APK below 150 MB (the `Android APK` workflow enforces the size).
Use debug builds for diagnostics; use an appropriate release build on the target
phone for release performance conclusions. Do not invent subsystem budgets or
pooling changes before locating a measured bottleneck. Keep the same scene, build
settings and device conditions for before/after measurements.

## Remaining release gates

TalkBack/switch/gamepad coverage, physical touch/lifecycle/network validation,
increased contrast and populated 3v3 performance remain unverified on hardware.
Toni/Popoy/Inday torn meshes and existing headless material/orphan/shutdown warnings
remain separate work. This focused interruption fix is not an AAA/readiness
certification and does not claim those blockers are resolved.
