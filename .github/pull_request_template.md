## What changed
<!-- The chunk of roadmap work this covers, in plain words -->

## Spec check
<!-- Which sections of docs/SPEC.md, KITS.md, MAPS.md, LORE.md and GRAPHICS.md this follows, e.g. "SPEC §7 movement, GRAPHICS camera". Anything that deviates or was not covered goes under "Decisions for you". -->

## Decisions for you
<!-- Any defaults chosen where the docs were silent (also listed in docs/SPEC.md §11) -->

## Install the APK
Actions → **Unity 6 CI** → the run on this branch (or on `main` after you merge; wait for it to finish) → artifact `kalyeah-unity-apk` (and `kalyeah-unity-screenshots` for rendered captures). If Android refuses to update, uninstall the old app first (builds signed with different debug keys do not install over each other).
1.
Device checks (only a phone shows these):
-

## Done
- [ ] `dotnet test unity/Kalyeah.sln` and Unity `EditMode` tests pass locally
- [ ] Dual-aspect `1280x720` (`16:9`) and `844x390` (`~19.5:9` notched) screenshots were checked for anything visual
- [ ] Follows `docs/SPEC.md`, `KITS.md`, `MAPS.md`, `LORE.md` and `GRAPHICS.md` (no invented rules; deviations listed above)
- [ ] No hardcoded balance numbers (values in `unity/Data/*.json`)
- [ ] Docs updated (the matching one of the five when the owner decided something, `docs/ROADMAP.md` for status)
