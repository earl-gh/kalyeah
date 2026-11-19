# Mistake log (auto-loaded; keep tiny, one line per rule, no repeats)
Don't:
- design or build from memory, from other games or from an older doc: `docs/SPEC.md`, `KITS.md`, `MAPS.md`, `LORE.md` and `GRAPHICS.md` are the only source of truth (`CLAUDE.md`); if they are silent, use `SPEC.md` §11 or ask the owner
- add a design doc outside those five, or recreate a deleted one (audits, "AAA quality" or scorecard docs); record owner answers in the matching file
- put a logo, wordmark or tagline on any game screen (`SPEC.md` §8: the splash only)
- commit generated `unity/Game/Assets/Imported/`, `Assets/Kalyeah/Generated/`, or `Library/` noise; stage explicit paths
- run Unity batchmode before running `python tools/unity_export/sync_assets.py`
- hand-edit generated `.unity` scenes or URP assets owned by `KalyeahBuild.Setup`
- call a 3D model fine before checking its `.glb` scale, `+Z`/`GodotSpace` facing, and sanitized URP materials
- claim a visual fix you haven't inspected at both `1280×720` (`16:9`) and `844×390` (`~19.5:9` notched); say what was unchecked
- use an art asset without a logged licence in `assets/CREDITS.md` (CC0, or CC-BY with credit; never non-commercial or another game's)
- guess phone hotspot interfaces (gateway `.1`, "any private IPv4 = hotspot", interface names): check AOSP / `HotspotNet` rules and vendor interface tests
- write an APK or `aapt2` parser from memory: test against captured fixtures in `unity/Golden/Android/`
- wire Android Back directly to `Application.Quit()`: route through `MenuStack.HandleBack()` and `MatchOverlaysView.HandleBack()`
Do:
- name the spec sections a change follows in the PR ("Spec check"), and list every deviation under "Decisions for you"
- run targeted `dotnet test` / `EditMode` seam checks while iterating, and full suites before opening a PR
- remove temporary files you created; ask before deleting lock or user files
Repeat instructions:
- "make a PR": read recent PRs + `.github/pull_request_template.md`, fill sections, open via `gh`, give link
- "fix quality": inspect `.glb` / texture sync + URP material properties in code, fix root cause, pin with a test
- "replace model": keep foot at origin, height in `unity/Data/`, run `sync_assets.py`, update tests + `docs/ART.md`
