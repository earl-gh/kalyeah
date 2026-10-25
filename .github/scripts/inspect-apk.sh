#!/usr/bin/env bash
# Fails the build when the APK is not the app we mean to ship.
#   inspect-apk.sh <apk> <version from version.txt>
#   inspect-apk.sh --dumps <badging.txt> <manifest.txt> <version>    only the manifest checks, on saved aapt2 output (the unit test)
# Needs the Android build-tools (aapt2, zipalign) and binutils (readelf); all are on the hosted runner.
# The manifest facts come from `aapt2 dump xmltree` (what the APK really contains); `dump badging` only supplies the package,
# the permissions and the native code. Whenever a check fails the raw aapt2 output is printed, so the next failure can be read.
set -euo pipefail

MAX_MB=150      # MiB; docs/ROADMAP.md "Budgets (M6)". The debug APK measures 145 (a local export of this tree), so a heavy new asset fails the build
PACKAGE="com.kalyeah.game"
PERMISSIONS="android.permission.INTERNET android.permission.VIBRATE"
ATTR_MIN_SDK="minSdkVersion\(0x0101020c\)"
ATTR_TARGET_SDK="targetSdkVersion\(0x01010270\)"
ATTR_GL_ES="glEsVersion\(0x01010281\)"
ATTR_ORIENTATION="screenOrientation\(0x0101001e\)"
ATTR_ALLOW_BACKUP="allowBackup\(0x01010280\)"

failed=0
check() { # check <description> <command...>
  local description="$1"; shift
  if "$@"; then echo "ok    $description"; else echo "FAIL  $description"; failed=1; fi
}

attr_number() { # attr_number <manifest file> <attribute pattern>: the number aapt2 printed, "=24", "=0x00030000" or "=(type 0x10)0x18"
  local raw
  raw="$(grep -oE "$2=(\(type 0x[0-9a-f]+\))?(0x[0-9a-f]+|[0-9]+)\$" "$1" | head -n 1 | sed -E 's/.*=(\(type 0x[0-9a-f]+\))?//')"
  [ -n "$raw" ] && echo $(( raw ))
}
attr_at_least() { # attr_at_least <manifest file> <attribute pattern> <minimum>
  local value
  value="$(attr_number "$1" "$2" || true)"
  [ -n "$value" ] && (( value >= $3 ))
}
attr_equals() { # attr_equals <manifest file> <attribute pattern> <number>
  local value
  value="$(attr_number "$1" "$2" || true)"
  [ -n "$value" ] && (( value == $3 ))
}
attr_at_most() { # attr_at_most <manifest file> <attribute pattern> <maximum>
  local value
  value="$(attr_number "$1" "$2" || true)"
  [ -n "$value" ] && (( value <= $3 ))
}

manifest_checks() { # manifest_checks <badging file> <manifest file> <version>
  local badging_file="$1" manifest_file="$2" version="$3"
  has() { grep -qE "$1" "$badging_file"; }
  check "package is $PACKAGE" has "^package: name='$PACKAGE' "
  check "version name starts with $version" has "versionName='$version"
  check "min SDK is 24 or lower" attr_at_most "$manifest_file" "$ATTR_MIN_SDK" 24
  check "target SDK is 35 or higher" attr_at_least "$manifest_file" "$ATTR_TARGET_SDK" 35
  check "it has a launcher activity" grep -q 'android.intent.category.LAUNCHER' "$manifest_file"
  check "it needs OpenGL ES 3.0" attr_at_least "$manifest_file" "$ATTR_GL_ES" $(( 0x30000 ))
  check "the activity is locked to portrait" attr_equals "$manifest_file" "$ATTR_ORIENTATION" 1
  check "backups are off" grep -qE "$ATTR_ALLOW_BACKUP=false\$" "$manifest_file"
  check "arm64-v8a is the only native code" test "$(grep '^native-code:' "$badging_file" || true)" = "native-code: 'arm64-v8a'"

  local declared expected
  declared="$(sed -n "s/^uses-permission: name='\([^']*\)'.*/\1/p" "$badging_file" | grep -v '\.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION$' | sort | tr '\n' ' ' || true)"
  expected="$(echo "$PERMISSIONS" | tr ' ' '\n' | sort | tr '\n' ' ')"
  echo "permissions: $declared"
  check "only the permissions the game uses (INTERNET, VIBRATE)" test "$declared" = "$expected"
}

show_raw() { # show_raw <badging file> <manifest file>: what aapt2 printed, to read after a failure
  echo "---- aapt2 dump badging (without the per-language labels) ----"
  grep -vE '^(application-label-|application-icon-|locales:|densities:)' "$1" | head -n 60
  echo "---- aapt2 dump xmltree --file AndroidManifest.xml (first 80 lines) ----"
  head -n 80 "$2"
}

if [ "$1" = "--dumps" ]; then
  manifest_checks "$2" "$3" "$4"
  if [ "$failed" -ne 0 ]; then show_raw "$2" "$3"; fi
  exit "$failed"
fi

APK="$1"
VERSION="$2"
TOOLS="$ANDROID_HOME/build-tools/${ANDROID_BUILD_TOOLS:-35.0.1}"
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT

# aapt2 warns "mipmap/themed_icon ... no such path exists": harmless, Godot drops that file when no monochrome launcher icon is set.
"$TOOLS/aapt2" dump badging "$APK" > "$scratch/badging.txt"
"$TOOLS/aapt2" dump xmltree --file AndroidManifest.xml "$APK" > "$scratch/manifest.txt"
manifest_checks "$scratch/badging.txt" "$scratch/manifest.txt" "$VERSION"
if [ "$failed" -ne 0 ]; then show_raw "$scratch/badging.txt" "$scratch/manifest.txt"; fi

libs="$(unzip -Z1 "$APK" | grep '^lib/' || true)"
check "no lib folder but arm64-v8a" test -z "$(grep -v '^lib/arm64-v8a/' <<< "$libs" || true)"
check "libgodot_android.so is inside" grep -q 'libgodot_android.so' <<< "$libs"

# Android 15+ on 16 KB pages needs every library aligned to 16 KB (0x4000) in the file and in the zip.
aligned() { # aligned <lib>: every LOAD segment has p_align >= 16384
  local value
  while read -r value; do (( value >= 16384 )) || return 1; done < <(readelf -lW "$1" | awk '$1=="LOAD" {print $NF}')
}
unzip -q -o "$APK" 'lib/arm64-v8a/*.so' -d "$scratch"
for so in "$scratch"/lib/arm64-v8a/*.so; do
  check "$(basename "$so") loads with 16 KB alignment" aligned "$so"
done
check "the zip is 16 KB aligned" "$TOOLS/zipalign" -c -P 16 4 "$APK"

size_mb=$(( $(stat -c %s "$APK") / 1048576 ))
echo "size: ${size_mb} MB (limit ${MAX_MB} MB)"
check "the APK is under ${MAX_MB} MB" test "$size_mb" -lt "$MAX_MB"
if [ "$size_mb" -ge "$MAX_MB" ]; then
  echo "---- the 15 biggest files in the APK ----"
  unzip -lv "$APK" | awk 'NR>3 && $1 ~ /^[0-9]+$/ {print $3, $8}' | sort -n -r | head -n 15
fi

exit "$failed"
