# Accessibility follow-up — 19 October 2026

Scope: follow-up to the shared UI/HIG pass, based on main `325c8ee`. Android remains the target; browser rendering is the available visual validation channel. This pass does not certify platform accessibility compliance.

## Delivered

| Area | Player-visible behavior | Boundary |
|---|---|---|
| Reading size | Saved 100%, 120% and 140% options, shared across Home, room and match settings. Existing and newly opened reading labels/fields update without restarting or compounding font sizes. | Original action-label sizes, portrait captions, drawn HUD counters, boot percentage and already prominent intro/result lettering retain their geometry. This is not OS Dynamic Type. |
| Reflow | Settings and modal contents scroll and follow keyboard focus. Loadout descriptions grow and names reserve space for selection badges, mode descriptions and HUD hints wrap, player rows accommodate enlarged reading text. Room map width now fits its parent margins. | Long lobby names can still ellipsize; full text is available as a tooltip. Native touch/screen-reader name exposure needs device validation. |
| Motion | Saved Reduce motion preference stops UI/button/banner pops, animated carousel snapping, camera shake, moving grain and decorative NPC sway; the intro uses the normal gameplay camera. Live preference changes cancel active tracked tweens. | Character actions, aim/weapon telegraphs, follow-camera movement, touch dragging and authoritative timing remain intact. This is not a comprehensive photosensitivity mode. |
| Focus | Mode carousel supports Left/Right with a visible ring. Modal Tab/Shift-Tab loops through enabled visible controls, focuses text entry when present, scrolls focused content and returns focus to the opener. Portrait focus is distinct from selection. | Keyboard navigation is not proof of Android TalkBack/switch-access or full gamepad support. |
| Input isolation | Opening match settings sends neutral movement plus aim cancellation through the existing PlayerInput path, in both offline and online branches. Local cached keyboard aim is cleared. | Multiplayer continues while settings are open. No protocol, server rules, balance or sim timing changed. |

## Verification

Godot 4.6.2 editor import and single-threaded Web pack export completed. The full GUT run contains 456 tests: 455 passed, no failed tests, and one pending model-surface test reports the existing three Toni/Popoy/Inday torn-mesh findings. Ten new tests cover saved/malformed preferences, synchronized controls, idempotent scaling, new-node inheritance, loadout selection preservation, reduced camera/film motion with unchanged intro timing, modal focus/restore, carousel keyboard selection, decorative sway, largest-size room/dialog bounds and input cancellation/recovery.

An initial full run while import/export jobs were active hit the existing knockout-animation pose assertion (`test_art_audio.gd:307`). A sequential rerun passed without modifying that test or animation. Treat its frame-dependent timing as a separate QA stability follow-up. Existing headless null-material cleanup errors (four), deprecated GUT helpers, orphan reports and the ObjectDB shutdown warning remain visible in logs; they are not newly claimed as fixed here.

Browser checks use the exported build at 720×1280, 390×844 and 360×640 with 140% reading text. Saved size and motion preferences were checked after a reload. Screenshots and logs are local review artifacts under `/workspace/kalyeah-ui-pass/` and `/tmp/kalyeah-access-*.log`, not shipped game assets.

## Next release gates

1. Test signed/native Android builds on minimum and recommended phones: cutouts, OS display/font scaling, keyboard appearance, Back, multitouch, interrupted gestures and resume/reconnect. Verify full 3v3 rooms and active matches at 140% text with vibration visible.
2. Audit TalkBack roles/names/order, switch access and physical keyboard/gamepad navigation. Do not infer screen-reader support from visible labels or tooltips.
3. Profile populated 3v3 play on hardware against the existing frame-rate/memory/build-size budgets. Include accessibility setting changes and scene churn in allocation checks.
4. Add a separately scoped contrast/camera-comfort pass after measuring world/HUD readability outdoors and in low light; preserve competitive telegraph timing.
5. Resolve the owner's torn character meshes and stabilize frame-dependent animation tests. Track existing headless cleanup/orphan warnings separately.

No new assets, dependencies, autoloads, export settings or hand-edited versions/changelog are introduced. The owner merges the feature PR; release-please owns versioning.
