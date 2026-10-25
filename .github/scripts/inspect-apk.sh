#!/usr/bin/env bash
# Fails the build when the APK is not the app we mean to ship. Usage: inspect-apk.sh <apk> <version from version.txt>
# Needs the Android build-tools (aapt2, zipalign) and binutils (readelf); all are on the hosted runner.
set -euo pipefail

APK="$1"
VERSION="$2"
TOOLS="$ANDROID_HOME/build-tools/${ANDROID_BUILD_TOOLS:-35.0.1}"
MAX_MB=150      # docs/ROADMAP.md "Budgets (M6)": APK under 150 MB
PACKAGE="com.kalyeah.game"
PERMISSIONS="android.permission.INTERNET android.permission.VIBRATE"

failed=0
check() { # check <description> <command...>
  local description="$1"; shift
  if "$@"; then echo "ok    $description"; else echo "FAIL  $description"; failed=1; fi
}

badging="$("$TOOLS/aapt2" dump badging "$APK")"
grep -E "^(package|sdkVersion|targetSdkVersion|uses-permission|uses-gl-es|uses-feature|native-code|launchable-activity|application-debuggable)" <<< "$badging" || true

has() { grep -qE "$1" <<< "$badging"; }
check "package is $PACKAGE" has "^package: name='$PACKAGE' "
check "version name starts with $VERSION" has "versionName='$VERSION"
check "min SDK is 24 or lower" has "^sdkVersion:'(1[0-9]|2[0-4])'"
check "target SDK is 35 or higher" has "^targetSdkVersion:'(3[5-9]|[4-9][0-9])'"
check "it has a launcher activity" has "^launchable-activity: name="
check "it needs OpenGL ES 3.0" has "^uses-gl-es:'0x30000'"
check "arm64-v8a is the only native code" test "$(grep '^native-code:' <<< "$badging" || true)" = "native-code: 'arm64-v8a'"

declared="$(sed -n "s/^uses-permission: name='\([^']*\)'.*/\1/p" <<< "$badging" | grep -v '\.DYNAMIC_RECEIVER_NOT_EXPORTED_PERMISSION$' | sort | tr '\n' ' ' || true)"
expected="$(echo "$PERMISSIONS" | tr ' ' '\n' | sort | tr '\n' ' ')"
echo "permissions: $declared"
check "only the permissions the game uses (INTERNET, VIBRATE)" test "$declared" = "$expected"

scratch="$(mktemp -d)"
"$TOOLS/aapt2" dump xmltree --file AndroidManifest.xml "$APK" > "$scratch/manifest.txt"
check "the activity is locked to portrait" grep -qE "screenOrientation\(0x0101001e\)=(\(type 0x1[0-9a-f]\))?(0x0*)?1$" "$scratch/manifest.txt"
check "backups are off" grep -qE "allowBackup\(0x01010280\)=(false|\(type 0x12\)0x0)$" "$scratch/manifest.txt"

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
rm -rf "$scratch"
check "the zip is 16 KB aligned" "$TOOLS/zipalign" -c -P 16 4 "$APK"

size_mb=$(( $(stat -c %s "$APK") / 1048576 ))
echo "size: ${size_mb} MB (limit ${MAX_MB} MB)"
check "the APK is under ${MAX_MB} MB" test "$size_mb" -lt "$MAX_MB"

exit "$failed"
