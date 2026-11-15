## What changed
<!-- The chunk of roadmap work this covers, in plain words -->

## Decisions for you
<!-- Any defaults chosen where the docs were silent (also listed in docs/GDD.md "Open decisions") -->

## Install the APK
Actions → **Unity 6 CI** → the run on this branch (or on `main` after you merge; wait for it to finish) → artifact `kalyeah-unity-apk` (and `kalyeah-unity-screenshots` for rendered captures). If Android refuses to update, uninstall the old app first (builds signed with different debug keys do not install over each other).
1.
Device checks (only a phone shows these):
-

## Done
- [ ] `dotnet test unity/Kalyeah.sln` and Unity `EditMode` tests pass locally
- [ ] Dual-aspect `1280x720` (`16:9`) and `844x390` (`~19.5:9` notched) screenshots were checked for anything visual
- [ ] No hardcoded balance numbers (values in `unity/Data/*.json`)
- [ ] Docs updated (`docs/GDD.md` for rules, `docs/ART.md` for look, `docs/ROADMAP.md` for status)
