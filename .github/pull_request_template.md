## What changed
<!-- The chunk of roadmap work this covers, in plain words -->

## Decisions for you
<!-- Any defaults chosen where the docs were silent (also listed in docs/GDD.md "Open decisions") -->

## Install the APK
Actions → **Android APK** → the run on this branch (or on `main` after you merge; wait for it to finish) → artifact `kalyeah-debug-apk`. If Android refuses to update, uninstall the old app first (builds signed with different debug keys do not install over each other).
1.
Device checks (only a phone shows these):
-

## Done
- [ ] Project imports and the full GUT suite passes locally
- [ ] A 720x1280 screenshot was checked for anything visual
- [ ] No hardcoded balance numbers (values in `data/`)
- [ ] Docs updated (`docs/GDD.md` for rules, `docs/ART.md` for look, `docs/ROADMAP.md` for status)
