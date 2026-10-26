#!/usr/bin/env bash
# Runs inside the emulator (android-emulator-runner). Usage: smoke-test.sh <apk> <output folder>
# Installs the build, starts it, lets it run 30 s, sends Back and Home like a player would, and fails on a dead process, a SCRIPT ERROR
# or a fatal signal in logcat. Leaves screenshots and the log in the output folder.
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
start() { adb shell monkey -p "$PACKAGE" -c android.intent.category.LAUNCHER 1 > /dev/null 2>&1; }

adb install -r "$APK" || { echo "FAIL  the APK installs"; exit 1; }
adb logcat -c
start
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
start
sleep 5
check "the game survives Home and return" alive
shot 3-after-resume

adb logcat -d > "$OUT/logcat.txt"
if grep -E "SCRIPT ERROR|Fatal signal|FATAL EXCEPTION|ANR in $PACKAGE" "$OUT/logcat.txt"; then
  echo "FAIL  logcat has an error (lines above)"; failed=1
else
  echo "ok    logcat has no SCRIPT ERROR, fatal signal or crash"
fi
grep -m3 -E "Godot Engine v" "$OUT/logcat.txt" || echo "note  no Godot banner in logcat"
exit "$failed"
