#!/usr/bin/env bash
# Runs inside the emulator (android-emulator-runner). Usage: smoke-test.sh <apk> <output folder>
# Installs the build, starts it, lets it run 30 s, sends Back and Home like a player would, and fails on a dead process, a SCRIPT ERROR
# or a fatal signal in logcat. Leaves screenshots and the log in the output folder.
# A launch that did not start the game (the launcher was still busy right after the boot, which a run on 2026-10-27 showed: the first
# `monkey` did nothing and the one after Home started the game) is repeated, up to 3 times; the game only has to be running 30 s after it
# really started, and any attempt that failed is named in the log.
set -uo pipefail

APK="$1"
OUT="$2"
PACKAGE="com.kalyeah.game"
mkdir -p "$OUT"
failed=0
check() { # check <description> <command...>
  local description="$1"; shift
  if "$@"; then echo "ok    $description"; else echo "FAIL  $description"; failed=1; fi
}
alive() { [ -n "$(adb shell pidof "$PACKAGE" | tr -d '\r')" ]; }
shot() { adb exec-out screencap -p > "$OUT/$1.png"; }
launch() { adb shell monkey -p "$PACKAGE" -c android.intent.category.LAUNCHER 1 2>&1 | tr -d '\r' | tail -n 3; }
# start: launch and wait up to 20 s for the process; try again if it never came
start() {
  local attempt
  for attempt in 1 2 3; do
    launch >> "$OUT/launch.txt"
    for _ in $(seq 1 20); do
      alive && return 0
      sleep 1
    done
    echo "note  attempt $attempt did not start the game within 20 s"
  done
  return 1
}

adb install -r "$APK" || { echo "FAIL  the APK installs"; exit 1; }
adb logcat -c
check "the game starts" start
sleep 30
check "the game is running after 30 s" alive
shot 1-after-30s

# Back on the home screen asks "Quit game?"; it must not close the app.
adb shell input keyevent KEYCODE_BACK
sleep 3
check "Back does not quit the game" alive
shot 2-after-back

# Home and return: the app is paused and resumed (a call, the lock screen).
adb shell input keyevent KEYCODE_HOME
sleep 3
check "the game starts again" start
sleep 5
check "the game survives Home and return" alive
shot 3-after-resume

adb logcat -d > "$OUT/logcat.txt"
if [ "$failed" -ne 0 ]; then
  # Why it died or never started, in the job log itself (the artifact is a download away): the launches, the activity manager and
  # the kernel's low-memory killer.
  echo "---- launch output ----"; cat "$OUT/launch.txt" 2>/dev/null
  echo "---- activity manager, process and memory lines about the game ----"
  grep -E "ActivityManager|ActivityTaskManager|lmkd|lowmemory|am_kill|am_proc_died|am_crash|$PACKAGE" "$OUT/logcat.txt" | tail -n 40
fi
if grep -E "SCRIPT ERROR|Fatal signal|FATAL EXCEPTION|ANR in $PACKAGE" "$OUT/logcat.txt"; then
  echo "FAIL  logcat has an error (lines above)"; failed=1
else
  echo "ok    logcat has no SCRIPT ERROR, fatal signal or crash"
fi
grep -m3 -E "Godot Engine v" "$OUT/logcat.txt" || echo "note  no Godot banner in logcat"
exit "$failed"
