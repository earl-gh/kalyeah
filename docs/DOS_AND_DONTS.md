# Mistake log (auto-loaded; keep tiny, one line per rule, no repeats)
Don't:
- commit Godot 4.7.2 noise (`project.godot`, rewritten `.import`); stage explicit paths
- test on anything but Godot 4.6.2; run import+tests in the foreground (<180 s/call)
- reuse a staged file name in `device_commit_files` (cached)
- call a model fine before diffing it against its source (size, textures, `.import`)
- rebuild a material in code without copying all its settings
- claim a visual you haven't seen; say what was unchecked
- generate assets (owner makes them)
- use `ui_cancel` for Android Back (it is Esc): handle `NOTIFICATION_WM_GO_BACK_REQUEST`, deferred
Do:
- `md5sum` copied files; remove temp files you made; `git push -u`
- ask delete permission before removing lock/temp files
Repeat instructions:
- "make a PR": read last PRs + template, copy sections, `gh api` REST, give link
- "fix quality": diff vs source, check `.import` + code materials, fix cause, pin with a test
- "N passes": import, full GUT N times, start practice scene once, report counts
- "replace model": bake one mesh per material, foot at origin, height in `data/`, update tests + `docs/ART.md`
