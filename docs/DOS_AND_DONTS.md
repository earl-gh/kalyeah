# Do's and Don'ts (Claude's mistake log)

Short on purpose. Repo-wide rules stay in `CLAUDE.md`; this file only holds things that went wrong in practice. One rule per line; no rule appears twice.

## Don't
- Don't commit the user's Godot 4.7.2 import noise (`project.godot`, rewritten `.import` files). Stage explicit paths; read `git status` before every commit.
- Don't run tests on anything but Godot 4.6.2.
- Don't rely on background processes; they die when the call ends. Run import + tests in the foreground (under 180 s per call).
- Don't reuse a staged file name in `device_commit_files`; it serves the cached old content.
- Don't say a model is "fine" without comparing it to its source file (size, textures, import params).
- Don't add a material in code (like `ModelLook`) without copying every setting the old one had; anything not copied is lost.
- Don't claim a visual result you have not seen. Say what was and was not looked at.
- Don't generate assets (`CLAUDE.md` owner rule): advise, the owner makes them.

## Do
- Verify copied files with `md5sum` against the source.
- Request delete permission before removing git lock files or temp files in the user's folder.
- Remove temp files you made in the user's folder (e.g. `pr.bundle`).
- Keep the branch pushed with upstream set (`git push -u`).

## When the instruction repeats, do this
| Instruction | Do |
|---|---|
| "make a PR" | Read the last PRs and `.github/pull_request_template.md`, copy their sections, open via `gh api` (REST), give the link. |
| "fix quality" | Diff against the source asset, check import params (`.import`: compression, mipmaps, LODs) and the material built in code, fix the cause, add a test that pins it. |
| "N passes of testing" | Import, then run the full GUT suite N times and start the practice scene once; report passing/failing/pending counts. |
| "replace model X" | Bake into one mesh per material, keep the foot at the origin, set the height in `data/`, update tests and `docs/ART.md`. |
